import React, { useState, useEffect, useMemo } from "react";
import { adminApi } from "../../services/adminApi";
import AdminWorkshopFormModal from "../../components/admin/AdminWorkshopFormModal";
import "./AdminWorkshops.css";

export default function AdminWorkshops() {
  const [activeTab, setActiveTab] = useState("pending");
  const [workshops, setWorkshops] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [searchQuery, setSearchQuery] = useState("");

  // Modals
  const [workshopFormModal, setWorkshopFormModal] = useState({ open: false, workshop: null });
  const [pricingTierModal, setPricingTierModal] = useState({
    open: false,
    workshop: null,
    tiers: [],
    loading: false,
    saving: false,
    reason: "",
    error: ""
  });
  const [actionModal, setActionModal] = useState({
    open: false,
    type: "", // "approve" | "reject" | "cancel" | "complete" | "details"
    workshop: null,
    price: "",
    reason: "",
    isEarlyCompletion: false,
  });

  const [attendeeModal, setAttendeeModal] = useState({
    open: false,
    workshop: null,
    items: [],
    loading: false,
    filter: "all", // "all" | "present" | "absent" | "not_marked" | "guests" | "students"
    search: "",
    toastMessage: "",
  });

  const [revealedContacts, setRevealedContacts] = useState({});
  const [copiedFeedbackLinks, setCopiedFeedbackLinks] = useState({});

  const maskPhone = (phone) => {
    if (!phone) return "—";
    const cleaned = String(phone).replace(/\s+/g, "");
    if (cleaned.length <= 4) return cleaned;
    const start = cleaned.slice(0, 3);
    const end = cleaned.slice(-3);
    return `${start}••••${end}`;
  };

  const toggleContactReveal = (bookingId) => {
    setRevealedContacts((prev) => ({ ...prev, [bookingId]: !prev[bookingId] }));
  };

  const loadWorkshops = async () => {
    setLoading(true);
    setError(null);
    try {
      if (activeTab === "pending") {
        const data = await adminApi.getPendingWorkshops();
        setWorkshops(Array.isArray(data) ? data : data?.items || []);
      } else {
        const data = await adminApi.getWorkshops();
        const allItems = data?.items || [];
        if (activeTab === "approved") {
          setWorkshops(allItems.filter((w) => w.status === "Approved"));
        } else if (activeTab === "completed") {
          setWorkshops(allItems.filter((w) => w.status === "Completed"));
        } else {
          setWorkshops(allItems);
        }
      }
    } catch (err) {
      setError(err.message || "Failed to load workshops.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadWorkshops();
  }, [activeTab]);

  // Determine if workshop date & end time have already passed
  const isWorkshopPassed = (ws) => {
    if (!ws || !ws.workshopDate) return false;
    try {
      const datePart = ws.workshopDate.slice(0, 10);
      const timePart = ws.endTime ? ws.endTime.slice(0, 8) : "23:59:59";
      const workshopEnd = new Date(`${datePart}T${timePart}`);
      return workshopEnd < new Date();
    } catch {
      return false;
    }
  };

  const handleOpenAction = (type, ws) => {
    const isPassed = isWorkshopPassed(ws);
    setActionModal({
      open: true,
      type,
      workshop: ws,
      price: ws.trainerProposedPrice || ws.price || "",
      reason: "",
      isEarlyCompletion: type === "complete" && !isPassed,
    });
  };

  
  const openPricingTierModal = async (ws) => {
    setPricingTierModal({
      open: true,
      workshop: ws,
      tiers: [],
      loading: true,
      saving: false,
      reason: "",
      error: ""
    });

    try {
      const res = await adminApi.getWorkshopPricingTiers(ws.id);
      setPricingTierModal((prev) => ({
        ...prev,
        tiers: res?.tiers || [],
        loading: false
      }));
    } catch (err) {
      console.error("Failed to load tiers:", err);
      // Fallback default tiers
      const base = ws.price || 299;
      setPricingTierModal((prev) => ({
        ...prev,
        tiers: [
          { tierNumber: 1, tierName: "Tier 1 (1–10)", minTickets: 1, maxTickets: 10, price: base },
          { tierNumber: 2, tierName: "Tier 2 (11–20)", minTickets: 11, maxTickets: 20, price: base + 100 },
          { tierNumber: 3, tierName: "Tier 3 (21–30)", minTickets: 21, maxTickets: 30, price: base + 200 },
          { tierNumber: 4, tierName: "Tier 4 (31+)", minTickets: 31, maxTickets: null, price: base + 300 },
        ],
        loading: false
      }));
    }
  };

  const handleTierPriceChange = (tierNumber, newPrice) => {
    setPricingTierModal((prev) => ({
      ...prev,
      tiers: prev.tiers.map((t) =>
        t.tierNumber === tierNumber ? { ...t, price: parseFloat(newPrice) || 0 } : t
      )
    }));
  };

  const savePricingTiers = async () => {
    const { workshop, tiers, reason } = pricingTierModal;
    setPricingTierModal((prev) => ({ ...prev, saving: true, error: "" }));
    try {
      await adminApi.updateWorkshopPricingTiers(workshop.id, tiers, reason);
      setPricingTierModal((prev) => ({ ...prev, open: false, saving: false }));
      loadWorkshops();
    } catch (err) {
      setPricingTierModal((prev) => ({
        ...prev,
        saving: false,
        error: err.message || "Failed to update pricing tiers."
      }));
    }
  };

  const confirmAction = async () => {
    const { type, workshop, price, reason } = actionModal;
    try {
      if (type === "approve") {
        const p = parseFloat(price);
        if (isNaN(p) || p <= 0) {
          alert("Approved price must be greater than zero.");
          return;
        }
        await adminApi.approveWorkshopPrice(workshop.id, p);
      } else if (type === "reject") {
        if (!reason.trim()) {
          alert("A rejection reason is mandatory.");
          return;
        }
        await adminApi.rejectWorkshop(workshop.id, reason.trim());
      } else if (type === "cancel") {
        if (!reason.trim()) {
          alert("A cancellation reason is mandatory.");
          return;
        }
        await adminApi.cancelWorkshop(workshop.id, reason.trim());
      } else if (type === "complete") {
        await adminApi.completeWorkshop(workshop.id);
      } else if (type === "publish") {
        await adminApi.publishWorkshop(workshop.id);
      } else if (type === "unpublish") {
        await adminApi.unpublishWorkshop(workshop.id);
      } else if (type === "archive") {
        await adminApi.archiveWorkshop(workshop.id);
      }
      setActionModal({ open: false, type: "", workshop: null, price: "", reason: "", isEarlyCompletion: false });
      loadWorkshops();
    } catch (err) {
      alert(err.message || "Action failed.");
    }
  };

  const openAttendeeList = async (ws) => {
    setAttendeeModal({
      open: true,
      workshop: ws,
      items: [],
      loading: true,
      filter: "all",
      search: "",
      toastMessage: "",
    });
    try {
      const res = await adminApi.getWorkshopRegistrations(ws.id);
      setAttendeeModal((prev) => ({
        ...prev,
        items: res?.items || [],
        loading: false,
      }));
    } catch (err) {
      alert(err.message || "Failed to load attendees.");
      setAttendeeModal((prev) => ({ ...prev, loading: false }));
    }
  };

  const handleExportCsv = async () => {
    if (!attendeeModal.workshop) return;
    try {
      const res = await adminApi.exportWorkshopAttendance(attendeeModal.workshop.id);
      const blob = new Blob([res], { type: "text/csv;charset=utf-8;" });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `attendance_${attendeeModal.workshop.id}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      alert(err.message || "Failed to export attendance CSV.");
    }
  };

  const handleMarkAttendance = async (studentProfileId, targetStatus, bookingId = null) => {
    if (!attendeeModal.workshop) return;
    try {
      await adminApi.markWorkshopAttendance(attendeeModal.workshop.id, studentProfileId, targetStatus, bookingId);
      // Refresh attendees list
      const res = await adminApi.getWorkshopRegistrations(attendeeModal.workshop.id);
      setAttendeeModal((prev) => ({
        ...prev,
        items: res?.items || [],
        toastMessage:
          targetStatus === 4
            ? "Marked as Present (Attended)"
            : targetStatus === 2
            ? "Reverted Check-In (Confirmed)"
            : "Marked as Absent (No-Show)",
      }));
      setTimeout(() => {
        setAttendeeModal((prev) => ({ ...prev, toastMessage: "" }));
      }, 3000);
      loadWorkshops();
    } catch (err) {
      alert(err.message || "Failed to update attendance.");
    }
  };

  const handleCopyFeedbackLink = async (bookingId) => {
    try {
      const res = await adminApi.generateWorkshopFeedbackToken(bookingId);
      if (res && res.feedbackUrl) {
        const fullUrl = res.feedbackUrl.startsWith("http")
          ? res.feedbackUrl
          : `${window.location.origin}${res.feedbackUrl}`;
        await navigator.clipboard.writeText(fullUrl);
        setCopiedFeedbackLinks((prev) => ({ ...prev, [bookingId]: true }));
        setAttendeeModal((prev) => ({
          ...prev,
          toastMessage: "Feedback link copied to clipboard!",
        }));
        setTimeout(() => {
          setAttendeeModal((prev) => ({ ...prev, toastMessage: "" }));
        }, 3000);
      } else {
        alert("Failed to generate feedback link.");
      }
    } catch (err) {
      alert(err.message || "Failed to generate feedback link.");
    }
  };

  // Filtered workshops based on search query
  const filteredWorkshops = useMemo(() => {
    if (!searchQuery.trim()) return workshops;
    const q = searchQuery.toLowerCase();
    return workshops.filter((w) =>
      (w.title && w.title.toLowerCase().includes(q)) ||
      (w.danceStyle && w.danceStyle.toLowerCase().includes(q)) ||
      (w.trainerName && w.trainerName.toLowerCase().includes(q)) ||
      (w.workshopReference && w.workshopReference.toLowerCase().includes(q)) ||
      (w.venue && w.venue.toLowerCase().includes(q))
    );
  }, [workshops, searchQuery]);

  // Filtered attendees in modal
  const filteredAttendees = useMemo(() => {
    let list = attendeeModal.items;
    if (attendeeModal.filter === "present") {
      list = list.filter((r) => r.attendanceStatus === "Present" || r.status === "Attended");
    } else if (attendeeModal.filter === "absent") {
      list = list.filter((r) => r.attendanceStatus === "Absent" || r.status === "NoShow");
    } else if (attendeeModal.filter === "not_marked") {
      list = list.filter((r) => (!r.attendanceStatus || r.attendanceStatus === "Not marked") && r.status !== "Attended" && r.status !== "NoShow");
    } else if (attendeeModal.filter === "guests") {
      list = list.filter((r) => r.isGuest || r.attendeeType === "Workshop Attendee");
    } else if (attendeeModal.filter === "students") {
      list = list.filter((r) => !r.isGuest && r.attendeeType !== "Workshop Attendee");
    }

    if (attendeeModal.search.trim()) {
      const q = attendeeModal.search.toLowerCase();
      list = list.filter(
        (r) =>
          (r.studentName && r.studentName.toLowerCase().includes(q)) ||
          (r.customerCode && r.customerCode.toLowerCase().includes(q)) ||
          (r.bookingReference && r.bookingReference.toLowerCase().includes(q)) ||
          (r.studentPhone && r.studentPhone.includes(q)) ||
          (r.studentEmail && r.studentEmail.toLowerCase().includes(q))
      );
    }
    return list;
  }, [attendeeModal.items, attendeeModal.filter, attendeeModal.search]);

  // Statistics for the open workshop attendees
  const attendeeStats = useMemo(() => {
    const items = attendeeModal.items;
    const total = items.length;
    const guests = items.filter((r) => r.isGuest || r.attendeeType === "Workshop Attendee").length;
    const students = total - guests;
    const present = items.filter((r) => r.attendanceStatus === "Present" || r.status === "Attended").length;
    const absent = items.filter((r) => r.attendanceStatus === "Absent" || r.status === "NoShow").length;
    const notMarked = Math.max(0, total - present - absent);
    const feedbackSubmitted = items.filter((r) => r.feedbackRating || (r.feedbackStatus && r.feedbackStatus.startsWith("Submitted"))).length;
    return { total, guests, students, present, absent, notMarked, feedbackSubmitted };
  }, [attendeeModal.items]);

  const getWorkshopReference = (w) => {
    if (w.workshopReference) return w.workshopReference;
    const year = w.workshopDate ? w.workshopDate.slice(0, 4) : "2026";
    const shortId = w.id ? w.id.slice(0, 6).toUpperCase() : "WKS";
    return `WKS-${year}-${shortId}`;
  };

  const getCapacityStatus = (booked, capacity) => {
    if (!capacity) return { label: "Open", color: "#2563eb", pct: 0 };
    const pct = Math.round((booked / capacity) * 100);
    if (pct >= 100) return { label: "Full", color: "#dc2626", pct };
    if (pct >= 80) return { label: "Near Capacity", color: "#d97706", pct };
    return { label: "Open", color: "#059669", pct };
  };

  const handleOpenCreate = () => {
    setWorkshopFormModal({ open: true, workshop: null });
  };

  const handleOpenEdit = (w) => {
    setWorkshopFormModal({ open: true, workshop: w });
  };

  return (
    <div className="admin-workshops-container">
      {/* Header */}
      <div className="workshops-header">
        <div>
          <h1>Workshops Management & Attendee Rosters</h1>
          <p className="subtitle">
            Review trainer proposed workshops, approve pricing, monitor capacities, and track student & guest attendees.
          </p>
        </div>
        <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
          <button
            className="admin-btn primary"
            onClick={handleOpenCreate}
          >
            + Create Workshop
          </button>
          <button className="admin-btn secondary" onClick={loadWorkshops} disabled={loading}>
            {loading ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      </div>

      {/* Tabs & Search Controls */}
      <div className="workshops-controls-row">
        <div className="workshops-tabs">
          <button
            className={`tab-btn ${activeTab === "pending" ? "active" : ""}`}
            onClick={() => setActiveTab("pending")}
          >
            Pending Review Queue
          </button>
          <button
            className={`tab-btn ${activeTab === "approved" ? "active" : ""}`}
            onClick={() => setActiveTab("approved")}
          >
            Approved & Upcoming
          </button>
          <button
            className={`tab-btn ${activeTab === "completed" ? "active" : ""}`}
            onClick={() => setActiveTab("completed")}
          >
            Completed
          </button>
          <button
            className={`tab-btn ${activeTab === "all" ? "active" : ""}`}
            onClick={() => setActiveTab("all")}
          >
            All Workshops
          </button>
        </div>

        <div className="workshops-search-wrapper">
          <input
            type="text"
            className="workshops-search-input"
            placeholder="Search title, trainer, style, venue..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button className="clear-search-btn" onClick={() => setSearchQuery("")}>
              ✕
            </button>
          )}
        </div>
      </div>

      {loading && <div className="loading-state">Loading workshops data...</div>}
      {error && <div className="error-banner">Error: {error}</div>}

      {/* Table */}
      {!loading && !error && (
        <div className="workshops-table-card">
          <table className="workshops-main-table">
            <thead>
              <tr>
                <th style={{ width: "260px" }}>Workshop & Style</th>
                <th>Trainer</th>
                <th>Date & Schedule</th>
                <th>Pricing</th>
                <th style={{ width: "160px" }}>Capacity & Bookings</th>
                <th>Status</th>
                <th style={{ width: "260px" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredWorkshops.length === 0 ? (
                <tr>
                  <td colSpan={7} className="empty-table-cell">
                    No workshops found matching your criteria.
                  </td>
                </tr>
              ) : (
                filteredWorkshops.map((w) => {
                  const capInfo = getCapacityStatus(w.bookedCount, w.capacity);
                  const isPassed = isWorkshopPassed(w);
                  const refCode = getWorkshopReference(w);

                  return (
                    <tr key={w.id} className="workshop-data-row">
                      {/* Workshop & Style */}
                      <td>
                        <div className="workshop-ref-code">{refCode}</div>
                        <div className="workshop-title">{w.title}</div>
                        <div className="workshop-tags">
                          <span className="style-tag">{w.danceStyle}</span>
                          <span className="level-tag">{w.level}</span>
                        </div>
                        {w.venue && <div className="workshop-venue">📍 {w.venue}</div>}
                      </td>

                      {/* Trainer */}
                      <td>
                        <div className="trainer-name">{w.trainerName}</div>
                      </td>

                      {/* Date & Schedule */}
                      <td>
                        <div className="schedule-date">{w.workshopDate?.slice(0, 10)}</div>
                        <div className="schedule-time">
                          {w.startTime?.slice(0, 5)} – {w.endTime?.slice(0, 5)}
                        </div>
                        <span className={`timing-badge ${isPassed ? "past" : "upcoming"}`}>
                          {isPassed ? "Session Passed" : "Upcoming"}
                        </span>
                      </td>

                      {/* Pricing */}
                      <td>
                        <div className="effective-price">₹{w.price}</div>
                        {w.trainerProposedPrice && w.trainerProposedPrice !== w.adminApprovedPrice && (
                          <div className="proposed-price-hint">
                            Proposed: ₹{w.trainerProposedPrice}
                          </div>
                        )}
                        {w.adminApprovedPrice && (
                          <div className="approved-price-hint">
                            Approved: ₹{w.adminApprovedPrice}
                          </div>
                        )}
                      </td>

                      {/* Capacity & Bookings */}
                      <td>
                        <div className="capacity-numbers">
                          <span className="booked-count">{w.bookedCount}</span>
                          <span className="capacity-slash"> / </span>
                          <span className="total-cap">{w.capacity} seats</span>
                        </div>
                        <div className="capacity-bar-container">
                          <div
                            className="capacity-bar-fill"
                            style={{
                              width: `${Math.min(100, capInfo.pct)}%`,
                              backgroundColor: capInfo.color,
                            }}
                          />
                        </div>
                        <span className="capacity-chip" style={{ color: capInfo.color }}>
                          {capInfo.label} ({capInfo.pct}%)
                        </span>
                      </td>

                      {/* Status */}
                      <td>
                        <span className={`status-pill ${w.status?.toLowerCase()}`}>
                          {w.status === "PendingApproval" ? "Pending Review" : w.status}
                        </span>
                      </td>

                      {/* Contextual Actions */}
                      <td>
                        <div className="actions-cell">
                          {/* Main Roster Action with capacity indicator */}
                          <button
                            className="admin-btn tier-pricing-btn"
                            onClick={() => openPricingTierModal(w)}
                            title="Configure workshop 4-tier pricing model"
                          >
                            Tier Pricing
                          </button>
                          <button
                            className="admin-btn view-attendees-btn"
                            onClick={() => openAttendeeList(w)}
                            title="View registered students and guest attendees"
                          >
                            View Attendees ({w.bookedCount}/{w.capacity || "—"})
                          </button>
                          <button
                            className="admin-btn small"
                            style={{ backgroundColor: "#2563eb", color: "#ffffff", fontWeight: 600 }}
                            onClick={() => handleOpenEdit(w)}
                            title="Edit workshop details"
                          >
                            ✎ Edit
                          </button>

                          {/* Pending Review Actions */}
                          {w.status === "PendingApproval" && (
                            <div className="context-btn-group">
                              <button
                                className="admin-btn primary small"
                                onClick={() => handleOpenAction("approve", w)}
                              >
                                Review & Approve
                              </button>
                              <button
                                className="admin-btn danger small"
                                onClick={() => handleOpenAction("reject", w)}
                              >
                                Reject
                              </button>
                            </div>
                          )}

                          {/* Published / Approved Actions */}
                          {(w.status === "Approved" || w.status === "Published") && (
                            <div className="context-btn-group">
                              <button
                                className="admin-btn small"
                                style={{ backgroundColor: "#d97706", color: "#ffffff", fontWeight: 600 }}
                                onClick={() => handleOpenAction("unpublish", w)}
                                title="Unpublish workshop from public website"
                              >
                                Unpublish
                              </button>
                              <button
                                className="admin-btn complete-btn small"
                                onClick={() => handleOpenAction("complete", w)}
                              >
                                Complete
                              </button>
                              <button
                                className="admin-btn danger small"
                                onClick={() => handleOpenAction("cancel", w)}
                              >
                                Cancel
                              </button>
                            </div>
                          )}

                          {/* Draft / Unpublished Actions */}
                          {(w.status === "Draft" || w.status === "Unpublished") && (
                            <div className="context-btn-group">
                              <button
                                className="admin-btn small"
                                style={{ backgroundColor: "#10b981", color: "#ffffff", fontWeight: 600 }}
                                onClick={() => handleOpenAction("publish", w)}
                                title="Publish workshop to public website"
                              >
                                Publish Live
                              </button>
                              <button
                                className="admin-btn danger small"
                                onClick={() => handleOpenAction("archive", w)}
                              >
                                Archive
                              </button>
                            </div>
                          )}

                          {/* Completed Actions */}
                          {w.status === "Completed" && (
                            <div className="context-btn-group">
                              <button
                                className="admin-btn feedback-btn small"
                                onClick={() => openAttendeeList(w)}
                              >
                                View Feedback
                              </button>
                            </div>
                          )}

                          {/* Cancelled or Rejected Actions */}
                          {(w.status === "Cancelled" || w.status === "Rejected") && (
                            <div className="context-btn-group">
                              <button
                                className="admin-btn secondary small"
                                onClick={() => handleOpenAction("details", w)}
                              >
                                View Details
                              </button>
                            </div>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* ACTION CONFIRMATION MODALS */}
      {actionModal.open && (
        <div className="admin-modal-overlay">
          <div className="admin-modal-box">
            <div className="modal-header">
              <h3>
                {actionModal.type === "approve" && "Review & Approve Workshop Price"}
                {actionModal.type === "reject" && "Reject Workshop Proposal"}
                {actionModal.type === "cancel" && "Cancel Workshop"}
                {actionModal.type === "complete" && "Mark Workshop as Completed"}
                {actionModal.type === "publish" && "Publish Workshop to Live Website"}
                {actionModal.type === "unpublish" && "Unpublish Workshop from Live Website"}
                {actionModal.type === "archive" && "Archive Workshop"}
                {actionModal.type === "details" && "Workshop Details"}
              </h3>
              <button
                className="modal-close-icon"
                onClick={() =>
                  setActionModal({
                    open: false,
                    type: "",
                    workshop: null,
                    price: "",
                    reason: "",
                    isEarlyCompletion: false,
                  })
                }
              >
                ✕
              </button>
            </div>

            <div className="modal-body">
              <div className="modal-workshop-summary">
                <span className="summary-ref">{getWorkshopReference(actionModal.workshop)}</span>
                <strong className="summary-title">{actionModal.workshop?.title}</strong>
                <span className="summary-trainer">Trainer: {actionModal.workshop?.trainerName}</span>
              </div>

              {actionModal.type === "approve" && (
                <div className="modal-form-group">
                  <p className="form-info-text">
                    Trainer proposed price:{" "}
                    <strong>
                      ₹{actionModal.workshop?.trainerProposedPrice || actionModal.workshop?.price}
                    </strong>
                  </p>
                  <label className="form-label">
                    Official Approved Price (INR):
                    <input
                      type="number"
                      className="form-input"
                      value={actionModal.price}
                      onChange={(e) => setActionModal({ ...actionModal, price: e.target.value })}
                      placeholder="e.g. 799"
                      min="1"
                    />
                  </label>
                  <p className="form-hint">
                    This price will be active for student & guest bookings upon approval.
                  </p>
                </div>
              )}

              {actionModal.type === "reject" && (
                <div className="modal-form-group">
                  <p className="form-info-text danger-text">
                    Please provide a reason for rejecting this proposal. This will be logged and visible to the trainer.
                  </p>
                  <label className="form-label">
                    Mandatory Rejection Reason:
                    <textarea
                      className="form-textarea"
                      value={actionModal.reason}
                      onChange={(e) => setActionModal({ ...actionModal, reason: e.target.value })}
                      placeholder="Specify rationale for rejection..."
                      rows={3}
                    />
                  </label>
                </div>
              )}

              {actionModal.type === "cancel" && (
                <div className="modal-form-group">
                  <p className="form-info-text danger-text">
                    ⚠️ Cancelling an active workshop will prevent any further bookings and mark existing bookings as cancelled.
                  </p>
                  <label className="form-label">
                    Mandatory Cancellation Reason:
                    <textarea
                      className="form-textarea"
                      value={actionModal.reason}
                      onChange={(e) => setActionModal({ ...actionModal, reason: e.target.value })}
                      placeholder="Specify rationale for cancellation..."
                      rows={3}
                    />
                  </label>
                </div>
              )}

              {actionModal.type === "complete" && (
                <div className="modal-form-group">
                  {actionModal.isEarlyCompletion ? (
                    <div className="warning-callout">
                      <strong>⚠️ Early Completion Warning:</strong>
                      <p>
                        This workshop is scheduled for{" "}
                        <strong>{actionModal.workshop?.workshopDate?.slice(0, 10)}</strong> at{" "}
                        <strong>{actionModal.workshop?.startTime?.slice(0, 5)}</strong>, which is in the future.
                        Marking it completed early will close the workshop session and trigger feedback collection.
                      </p>
                    </div>
                  ) : (
                    <p className="form-info-text">
                      The scheduled workshop date has passed. Confirm marking this workshop as successfully completed?
                    </p>
                  )}
                </div>
              )}

              {actionModal.type === "publish" && (
                <div className="modal-form-group">
                  <p className="form-info-text">
                    Publishing this workshop will immediately make it visible on the public Ethos website and open it for attendee registrations.
                  </p>
                </div>
              )}

              {actionModal.type === "unpublish" && (
                <div className="modal-form-group">
                  <p className="form-info-text danger-text">
                    Unpublishing will immediately remove this workshop from the public listings. Existing attendee registrations remain safely stored.
                  </p>
                </div>
              )}

              {actionModal.type === "archive" && (
                <div className="modal-form-group">
                  <p className="form-info-text">
                    Archiving will remove this workshop from active operational lists.
                  </p>
                </div>
              )}

              {actionModal.type === "details" && (
                <div className="details-grid">
                  <div className="detail-item">
                    <span className="detail-label">Status:</span>
                    <span className="detail-val">{actionModal.workshop?.status}</span>
                  </div>
                  <div className="detail-item">
                    <span className="detail-label">Date:</span>
                    <span className="detail-val">{actionModal.workshop?.workshopDate?.slice(0, 10)}</span>
                  </div>
                  <div className="detail-item">
                    <span className="detail-label">Time:</span>
                    <span className="detail-val">
                      {actionModal.workshop?.startTime?.slice(0, 5)} – {actionModal.workshop?.endTime?.slice(0, 5)}
                    </span>
                  </div>
                  <div className="detail-item">
                    <span className="detail-label">Venue:</span>
                    <span className="detail-val">{actionModal.workshop?.venue || "Main Studio"}</span>
                  </div>
                  <div className="detail-item">
                    <span className="detail-label">Capacity:</span>
                    <span className="detail-val">{actionModal.workshop?.capacity} seats</span>
                  </div>
                  <div className="detail-item">
                    <span className="detail-label">Booked Attendees:</span>
                    <span className="detail-val">{actionModal.workshop?.bookedCount}</span>
                  </div>
                  {actionModal.workshop?.description && (
                    <div className="detail-item full-width">
                      <span className="detail-label">Description:</span>
                      <p className="detail-desc">{actionModal.workshop?.description}</p>
                    </div>
                  )}
                </div>
              )}
            </div>

            <div className="modal-footer">
              <button
                className="admin-btn secondary"
                onClick={() =>
                  setActionModal({
                    open: false,
                    type: "",
                    workshop: null,
                    price: "",
                    reason: "",
                    isEarlyCompletion: false,
                  })
                }
              >
                {actionModal.type === "details" ? "Close" : "Cancel"}
              </button>
              {actionModal.type !== "details" && (
                <button
                  className={`admin-btn ${
                    actionModal.type === "reject" || actionModal.type === "cancel" ? "danger" : "primary"
                  }`}
                  onClick={confirmAction}
                >
                  Confirm {actionModal.type.charAt(0).toUpperCase() + actionModal.type.slice(1)}
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* COMPREHENSIVE ATTENDEES (ROSTER) MODAL */}
      {attendeeModal.open && (
        <div className="admin-modal-overlay">
          <div className="admin-modal-box large-roster-box">
            {/* Modal Header */}
            <div className="modal-header roster-header">
              <div>
                <div className="roster-meta-tag">
                  {getWorkshopReference(attendeeModal.workshop)} • {attendeeModal.workshop?.danceStyle} ({attendeeModal.workshop?.level})
                </div>
                <h2>Attendee Roster: {attendeeModal.workshop?.title}</h2>
                <div className="roster-subinfo">
                  <span>Instructor: <strong>{attendeeModal.workshop?.trainerName}</strong></span>
                  <span>•</span>
                  <span>Date: <strong>{attendeeModal.workshop?.workshopDate?.slice(0, 10)}</strong> ({attendeeModal.workshop?.startTime?.slice(0, 5)} - {attendeeModal.workshop?.endTime?.slice(0, 5)})</span>
                  <span>•</span>
                  <span>Venue: <strong>{attendeeModal.workshop?.venue || "Studio"}</strong></span>
                </div>
              </div>
              <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
                <button
                  type="button"
                  className="export-attendance-csv-btn"
                  onClick={handleExportCsv}
                  style={{
                    background: "rgba(233, 121, 99, 0.15)",
                    border: "1px solid #e97963",
                    color: "#e97963",
                    padding: "6px 14px",
                    borderRadius: "6px",
                    fontSize: "12px",
                    fontWeight: "600",
                    cursor: "pointer"
                  }}
                >
                  ↓ Export Attendance (CSV)
                </button>
                <button
                  className="modal-close-icon"
                  onClick={() =>
                    setAttendeeModal({
                      open: false,
                      workshop: null,
                      items: [],
                      loading: false,
                      filter: "all",
                      search: "",
                      toastMessage: "",
                    })
                  }
                >
                  ✕
                </button>
              </div>
            </div>

            {/* KPI Summary Cards */}
            <div className="roster-stats-row">
              <div className="roster-stat-card">
                <span className="stat-label">Total Bookings</span>
                <span className="stat-value">
                  {attendeeStats.total} <span className="stat-capacity-denom">/ {attendeeModal.workshop?.capacity}</span>
                </span>
                <span className="stat-caption">Seats Filled</span>
              </div>
              <div className="roster-stat-card">
                <span className="stat-label">Registered Students</span>
                <span className="stat-value text-blue">{attendeeStats.students}</span>
                <span className="stat-caption">ETHOS Student Accounts</span>
              </div>
              <div className="roster-stat-card">
                <span className="stat-label">Guest Attendees</span>
                <span className="stat-value text-purple">{attendeeStats.guests}</span>
                <span className="stat-caption">Workshop Guests</span>
              </div>
              <div className="roster-stat-card">
                <span className="stat-label">Attendance Marked</span>
                <span className="stat-value text-green">
                  {attendeeStats.present} <span className="stat-subval">/ {attendeeStats.notMarked} Pending</span>
                </span>
                <span className="stat-caption">
                  {attendeeStats.absent > 0 ? `${attendeeStats.absent} No-Show(s)` : "All marked present or pending"}
                </span>
              </div>
              <div className="roster-stat-card">
                <span className="stat-label">Feedback Submitted</span>
                <span className="stat-value text-amber">{attendeeStats.feedbackSubmitted}</span>
                <span className="stat-caption">Completed Reviews</span>
              </div>
            </div>

            {/* Toast feedback banner */}
            {attendeeModal.toastMessage && (
              <div className="roster-toast-banner">
                ✓ {attendeeModal.toastMessage}
              </div>
            )}

            {/* Filter Pills & Attendee Search */}
            <div className="roster-filter-row">
              <div className="roster-filter-pills">
                <button
                  className={`filter-pill ${attendeeModal.filter === "all" ? "active" : ""}`}
                  onClick={() => setAttendeeModal({ ...attendeeModal, filter: "all" })}
                >
                  All ({attendeeStats.total})
                </button>
                <button
                  className={`filter-pill ${attendeeModal.filter === "present" ? "active" : ""}`}
                  onClick={() => setAttendeeModal({ ...attendeeModal, filter: "present" })}
                >
                  Present ({attendeeStats.present})
                </button>
                <button
                  className={`filter-pill ${attendeeModal.filter === "absent" ? "active" : ""}`}
                  onClick={() => setAttendeeModal({ ...attendeeModal, filter: "absent" })}
                >
                  No-Show ({attendeeStats.absent})
                </button>
                <button
                  className={`filter-pill ${attendeeModal.filter === "not_marked" ? "active" : ""}`}
                  onClick={() => setAttendeeModal({ ...attendeeModal, filter: "not_marked" })}
                >
                  Not Marked ({attendeeStats.notMarked})
                </button>
                <button
                  className={`filter-pill ${attendeeModal.filter === "guests" ? "active" : ""}`}
                  onClick={() => setAttendeeModal({ ...attendeeModal, filter: "guests" })}
                >
                  Guests ({attendeeStats.guests})
                </button>
                <button
                  className={`filter-pill ${attendeeModal.filter === "students" ? "active" : ""}`}
                  onClick={() => setAttendeeModal({ ...attendeeModal, filter: "students" })}
                >
                  Students ({attendeeStats.students})
                </button>
              </div>

              <div className="roster-search-box">
                <input
                  type="text"
                  placeholder="Search attendee or booking reference..."
                  value={attendeeModal.search}
                  onChange={(e) => setAttendeeModal({ ...attendeeModal, search: e.target.value })}
                />
              </div>
            </div>

            <div className="roster-scroll-cue">
              ↔ Scroll horizontally to view booking reference and contact details
            </div>

            {/* Attendees Table */}
            <div className="roster-table-wrapper">
              {attendeeModal.loading ? (
                <div className="loading-state">Loading registered attendees...</div>
              ) : (
                <table className="roster-table">
                  <thead>
                    <tr>
                      <th>Attendee</th>
                      <th>Booking Status</th>
                      <th>Payment</th>
                      <th>Attendance</th>
                      <th>Feedback</th>
                      <th>Booking Reference</th>
                      <th>Contact Details</th>
                      <th>Booked Date</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredAttendees.length === 0 ? (
                      <tr>
                        <td colSpan={9} className="empty-table-cell">
                          No attendees match this filter.
                        </td>
                      </tr>
                    ) : (
                      filteredAttendees.map((r) => {
                        const isPresent = r.attendanceStatus === "Present" || r.status === "Attended";
                        const isAbsent = r.attendanceStatus === "Absent" || r.status === "NoShow";
                        const isNotMarked = !isPresent && !isAbsent;
                        const isCancelled = r.status === "Cancelled" || r.status === "CANCELLED";
                        const isGuest = r.isGuest || r.attendeeType === "Workshop Attendee";

                        return (
                          <tr key={r.bookingId} className="roster-row">
                            {/* 1. Attendee Name & Type */}
                            <td className="attendee-cell-main">
                              <div className="attendee-name">{r.studentName}</div>
                              <span className={`attendee-type-badge ${isGuest ? "guest-badge" : "student-badge"}`}>
                                {isGuest ? "Guest Attendee" : "Registered Student"}
                              </span>
                            </td>

                            {/* 2. Booking Status */}
                            <td>
                              <span className={`booking-status-badge ${
                                isPresent ? "attended" :
                                isAbsent ? "noshow" :
                                isCancelled ? "cancelled" :
                                r.paymentStatus?.toLowerCase().includes("pending") ? "pending" :
                                "confirmed"
                              }`}>
                                {isCancelled ? "Cancelled" : isPresent ? "Attended" : isAbsent ? "No-Show" : (r.status || "Confirmed")}
                              </span>
                            </td>

                            {/* 3. Payment Status */}
                            <td>
                              <span className={`payment-badge ${r.paymentStatus?.toLowerCase().includes("paid") ? "paid" : "pending"}`}>
                                {r.paymentStatus || "Paid"}
                              </span>
                            </td>

                            {/* 4. Attendance Status & Actions */}
                            <td>
                              <div className="attendance-cell">
                                <span className={`attendance-badge ${isPresent ? "present" : isAbsent ? "absent" : "not-marked"}`}>
                                  {isPresent ? "✓ Present" : isAbsent ? "✕ Absent" : "● Not Marked"}
                                </span>
                                {!isCancelled ? (
                                  <div className="attendance-quick-actions">
                                    {isNotMarked && (
                                      <>
                                        <button
                                          type="button"
                                          className="attendance-action-btn present-btn"
                                          title="Mark attendee as Present"
                                          onClick={() => handleMarkAttendance(r.studentId, 4, r.bookingId)}
                                        >
                                          ✓ Mark Present
                                        </button>
                                        <button
                                          type="button"
                                          className="attendance-action-btn absent-btn"
                                          title="Mark attendee as Absent (No-Show)"
                                          onClick={() => handleMarkAttendance(r.studentId, 5, r.bookingId)}
                                        >
                                          ✕ Mark No-Show
                                        </button>
                                      </>
                                    )}
                                    {isPresent && (
                                      <button
                                        type="button"
                                        className="attendance-action-btn undo-btn"
                                        title="Undo check-in and revert to Confirmed"
                                        onClick={() => handleMarkAttendance(r.studentId, 2, r.bookingId)}
                                      >
                                        Undo Attendance
                                      </button>
                                    )}
                                    {isAbsent && (
                                      <button
                                        type="button"
                                        className="attendance-action-btn present-btn"
                                        title="Correct status to Present"
                                        onClick={() => handleMarkAttendance(r.studentId, 4, r.bookingId)}
                                      >
                                        ✓ Mark Present
                                      </button>
                                    )}
                                  </div>
                                ) : (
                                  <span className="attendance-cancelled-note">Booking Cancelled</span>
                                )}
                              </div>
                            </td>

                            {/* 5. Feedback Status */}
                            <td>
                              <div className="feedback-cell">
                                {r.feedbackRating || (r.feedbackStatus && r.feedbackStatus.startsWith("Submitted")) ? (
                                  <span className="feedback-submitted-badge">
                                    ★ {r.feedbackRating || r.feedbackStatus.replace("Submitted ★", "").trim()} Submitted
                                  </span>
                                ) : (
                                  <div className="feedback-pending-wrapper">
                                    <span className="feedback-pending-badge">Not Submitted</span>
                                    {isGuest && (
                                      <button
                                        type="button"
                                        className={`copy-feedback-link-btn ${copiedFeedbackLinks[r.bookingId] ? "copied" : ""}`}
                                        title="Generate and copy single-use feedback link for this guest"
                                        onClick={() => handleCopyFeedbackLink(r.bookingId)}
                                      >
                                        {copiedFeedbackLinks[r.bookingId] ? "✓ Link Sent" : "🔗 Send Link"}
                                      </button>
                                    )}
                                  </div>
                                )}
                              </div>
                            </td>

                            {/* 6. Customer / Booking Ref */}
                            <td>
                              <div className="booking-ref-code">
                                {r.bookingReference || `BK-${r.bookingId?.slice(0, 8).toUpperCase()}`}
                              </div>
                              <div className="customer-code-sub">
                                {isGuest ? "Guest Ref: " + (r.customerCode || "GUEST") : r.customerCode}
                              </div>
                            </td>

                            {/* 7. Contact Details (Masked with on-demand reveal) */}
                            <td>
                              <div className="roster-contact-cell">
                                <div className="contact-phone-line">
                                  <span className="contact-icon">📞</span>
                                  <span className="contact-phone font-mono">
                                    {revealedContacts[r.bookingId] ? (r.studentPhone || "—") : maskPhone(r.studentPhone)}
                                  </span>
                                </div>
                                {revealedContacts[r.bookingId] && r.studentEmail && (
                                  <div className="contact-email-line">
                                    <span className="contact-icon">✉️</span>
                                    <span className="contact-email">{r.studentEmail}</span>
                                  </div>
                                )}
                                {r.studentPhone && (
                                  <button
                                    type="button"
                                    className="contact-reveal-btn"
                                    onClick={() => toggleContactReveal(r.bookingId)}
                                  >
                                    {revealedContacts[r.bookingId] ? "Hide Details" : "View Contact"}
                                  </button>
                                )}
                              </div>
                            </td>

                            {/* 8. Booked Date */}
                            <td>
                              <div className="booking-date-cell">
                                {r.bookedAt?.slice(0, 10)}
                                <span className="booking-time-sub">{r.bookedAt?.slice(11, 16)}</span>
                              </div>
                            </td>

                            {/* 9. Row Actions */}
                            <td>
                              <div className="roster-row-actions">
                                {!isGuest && r.studentId ? (
                                  <a
                                    href={`/admin/students/${r.studentId}`}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className="roster-view-profile-link"
                                    title="Open student dossier in new tab"
                                  >
                                    Dossier ↗
                                  </a>
                                ) : (
                                  <span className="roster-guest-indicator">Guest Pass</span>
                                )}
                              </div>
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              )}
            </div>

            {/* Modal Footer */}
            <div className="modal-footer roster-footer">
              <span className="footer-count-text">
                Showing {filteredAttendees.length} of {attendeeModal.items.length} attendees
              </span>
              <button
                className="admin-btn secondary"
                onClick={() =>
                  setAttendeeModal({
                    open: false,
                    workshop: null,
                    items: [],
                    loading: false,
                    filter: "all",
                    search: "",
                    toastMessage: "",
                  })
                }
              >
                Close Roster
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Create / Edit Workshop Modal */}
      {workshopFormModal.open && (
        <AdminWorkshopFormModal
          isOpen={workshopFormModal.open}
          workshop={workshopFormModal.workshop}
          onClose={() => setWorkshopFormModal({ open: false, workshop: null })}
          onSaved={loadWorkshops}
        />
      )}
    </div>
  );
}