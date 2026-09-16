import React, { useState, useEffect, useMemo, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  Plus,
  RefreshCw,
  Search,
  LayoutGrid,
  List,
  Calendar,
  Clock,
  MapPin,
  Users,
  IndianRupee,
  QrCode,
  Edit,
  ExternalLink,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Lock,
  ChevronRight,
  Filter,
} from "lucide-react";
import { adminApi } from "../../services/adminApi";
import AdminWorkshopFormModal from "../../components/admin/AdminWorkshopFormModal";
import "./AdminWorkshops.css";

const TABS = [
  { key: "pending", label: "Pending Review", countKey: "pendingReview", phaseParam: "PendingReview" },
  { key: "upcoming", label: "Upcoming", countKey: "upcoming", phaseParam: "Upcoming" },
  { key: "ongoing", label: "Ongoing", countKey: "ongoing", phaseParam: "Ongoing" },
  { key: "completed", label: "Completed", countKey: "completed", phaseParam: "Completed" },
  { key: "cancelled", label: "Cancelled", countKey: "cancelled", phaseParam: "Cancelled" },
  { key: "all", label: "All Workshops", countKey: "all", phaseParam: null },
];

export default function AdminWorkshops() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState("all");
  const [viewMode, setViewMode] = useState("cards"); // "cards" | "list"
  const [workshops, setWorkshops] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCity, setSelectedCity] = useState("all");
  const [dateFilter, setDateFilter] = useState("all"); // "all" | "today" | "week" | "month"

  // Counts from backend
  const [counts, setCounts] = useState({
    pendingReview: 0,
    upcoming: 0,
    ongoing: 0,
    completed: 0,
    cancelled: 0,
    all: 0,
  });

  // Modals
  const [workshopFormModal, setWorkshopFormModal] = useState({ open: false, workshop: null });
  const [pricingTierModal, setPricingTierModal] = useState({
    open: false,
    workshop: null,
    tiers: [],
    loading: false,
    saving: false,
    reason: "",
    error: "",
  });
  const [actionModal, setActionModal] = useState({
    open: false,
    type: "", // "approve" | "reject" | "cancel" | "complete" | "publish" | "unpublish"
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
    filter: "all",
    search: "",
    toastMessage: "",
  });

  const [revealedContacts, setRevealedContacts] = useState({});
  const [copiedFeedbackLinks, setCopiedFeedbackLinks] = useState({});

  const maskPhone = (phone) => {
    if (!phone) return "—";
    const cleaned = String(phone).replace(/\s+/g, "");
    if (cleaned.length <= 4) return cleaned;
    return `${cleaned.slice(0, 3)}••••${cleaned.slice(-3)}`;
  };

  const toggleContactReveal = (bookingId) => {
    setRevealedContacts((prev) => ({ ...prev, [bookingId]: !prev[bookingId] }));
  };

  // Fetch counts from backend
  const loadCounts = useCallback(async () => {
    try {
      const res = await adminApi.getWorkshopCounts();
      if (res) {
        setCounts({
          pendingReview: res.pendingReview ?? 0,
          upcoming: res.upcoming ?? 0,
          ongoing: res.ongoing ?? 0,
          completed: res.completed ?? 0,
          cancelled: res.cancelled ?? 0,
          all: res.all ?? 0,
        });
      }
    } catch (err) {
      console.warn("Could not fetch workshop counts:", err);
    }
  }, []);

  // Fetch workshops
  const loadWorkshops = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const tabConfig = TABS.find((t) => t.key === activeTab);
      const params = [];
      if (tabConfig && tabConfig.phaseParam) {
        params.push(`phase=${encodeURIComponent(tabConfig.phaseParam)}`);
      }
      if (selectedCity && selectedCity !== "all") {
        params.push(`city=${encodeURIComponent(selectedCity)}`);
      }

      const queryString = params.join("&");
      let data;
      if (activeTab === "pending" && !selectedCity) {
        data = await adminApi.getPendingWorkshops();
      } else {
        data = await adminApi.getWorkshops(queryString);
      }

      const list = Array.isArray(data) ? data : data?.items || [];
      setWorkshops(list);
    } catch (err) {
      setError(err.message || "Failed to load workshops.");
    } finally {
      setLoading(false);
    }
  }, [activeTab, selectedCity]);

  useEffect(() => {
    loadCounts();
  }, [loadCounts]);

  useEffect(() => {
    loadWorkshops();
  }, [loadWorkshops]);

  // Distinct cities extracted from loaded workshops
  const availableCities = useMemo(() => {
    const set = new Set();
    workshops.forEach((w) => {
      if (w.city) set.add(w.city);
    });
    return Array.from(set);
  }, [workshops]);

  // Date filter logic
  const isWithinDateFilter = (workshopDateStr) => {
    if (dateFilter === "all" || !workshopDateStr) return true;
    const wsDate = new Date(workshopDateStr);
    const now = new Date();

    if (dateFilter === "today") {
      return (
        wsDate.getFullYear() === now.getFullYear() &&
        wsDate.getMonth() === now.getMonth() &&
        wsDate.getDate() === now.getDate()
      );
    }
    if (dateFilter === "week") {
      const startOfWeek = new Date(now);
      startOfWeek.setDate(now.getDate() - now.getDay());
      const endOfWeek = new Date(startOfWeek);
      endOfWeek.setDate(startOfWeek.getDate() + 7);
      return wsDate >= startOfWeek && wsDate <= endOfWeek;
    }
    if (dateFilter === "month") {
      return (
        wsDate.getFullYear() === now.getFullYear() &&
        wsDate.getMonth() === now.getMonth()
      );
    }
    return true;
  };

  // Client filtered workshops
  const filteredWorkshops = useMemo(() => {
    let list = workshops;

    if (dateFilter !== "all") {
      list = list.filter((w) => isWithinDateFilter(w.workshopDate));
    }

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      list = list.filter(
        (w) =>
          (w.title && w.title.toLowerCase().includes(q)) ||
          (w.danceStyle && w.danceStyle.toLowerCase().includes(q)) ||
          (w.trainerName && w.trainerName.toLowerCase().includes(q)) ||
          (w.workshopReference && w.workshopReference.toLowerCase().includes(q)) ||
          (w.venue && w.venue.toLowerCase().includes(q)) ||
          (w.city && w.city.toLowerCase().includes(q))
      );
    }

    return list;
  }, [workshops, searchQuery, dateFilter]);

  // Determine if workshop is locked out from modifications
  const isWorkshopLocked = (ws) => {
    const phase = ws.lifecyclePhase || ws.LifecyclePhase;
    return phase === "Ongoing" || phase === "Completed";
  };

  // Open action modal
  const handleOpenAction = (type, ws) => {
    if (isWorkshopLocked(ws) && (type === "cancel" || type === "unpublish")) {
      alert(`This workshop is ${ws.lifecyclePhase}. It has already started and cannot be cancelled or unpublished.`);
      return;
    }

    setActionModal({
      open: true,
      type,
      workshop: ws,
      price: ws.trainerProposedPrice || ws.price || "",
      reason: "",
      isEarlyCompletion: false,
    });
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
      loadCounts();
    } catch (err) {
      alert(err.message || "Action failed.");
    }
  };

  const openPricingTierModal = async (ws) => {
    setPricingTierModal({
      open: true,
      workshop: ws,
      tiers: [],
      loading: true,
      saving: false,
      reason: "",
      error: "",
    });

    try {
      const res = await adminApi.getWorkshopPricingTiers(ws.id);
      setPricingTierModal((prev) => ({
        ...prev,
        tiers: res?.tiers || [],
        loading: false,
      }));
    } catch (err) {
      const base = ws.price || 500;
      setPricingTierModal((prev) => ({
        ...prev,
        tiers: [
          { tierNumber: 1, tierName: "Tier 1 (1–10)", minTickets: 1, maxTickets: 10, price: base },
          { tierNumber: 2, tierName: "Tier 2 (11–20)", minTickets: 11, maxTickets: 20, price: base + 100 },
          { tierNumber: 3, tierName: "Tier 3 (21–30)", minTickets: 21, maxTickets: 30, price: base + 200 },
          { tierNumber: 4, tierName: "Tier 4 (31+)", minTickets: 31, maxTickets: null, price: base + 300 },
        ],
        loading: false,
      }));
    }
  };

  const handleTierPriceChange = (tierNumber, newPrice) => {
    setPricingTierModal((prev) => ({
      ...prev,
      tiers: prev.tiers.map((t) =>
        t.tierNumber === tierNumber ? { ...t, price: parseFloat(newPrice) || 0 } : t
      ),
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
        error: err.message || "Failed to update pricing tiers.",
      }));
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
      setAttendeeModal((prev) => ({
        ...prev,
        loading: false,
        toastMessage: "Could not load attendees: " + (err.message || "Unknown error"),
      }));
    }
  };

  const filteredAttendees = useMemo(() => {
    let list = attendeeModal.items;
    if (attendeeModal.filter === "present") {
      list = list.filter((r) => r.attendanceStatus === "Present" || r.status === "Attended");
    } else if (attendeeModal.filter === "absent") {
      list = list.filter((r) => r.attendanceStatus === "Absent" || r.status === "NoShow");
    } else if (attendeeModal.filter === "not_marked") {
      list = list.filter(
        (r) =>
          (!r.attendanceStatus || r.attendanceStatus === "Not marked") &&
          r.status !== "Attended" &&
          r.status !== "NoShow"
      );
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

  const attendeeStats = useMemo(() => {
    const items = attendeeModal.items;
    const total = items.length;
    const guests = items.filter((r) => r.isGuest || r.attendeeType === "Workshop Attendee").length;
    const students = total - guests;
    const present = items.filter((r) => r.attendanceStatus === "Present" || r.status === "Attended").length;
    const absent = items.filter((r) => r.attendanceStatus === "Absent" || r.status === "NoShow").length;
    const notMarked = Math.max(0, total - present - absent);
    return { total, guests, students, present, absent, notMarked };
  }, [attendeeModal.items]);

  const getCapacityStatus = (booked, capacity) => {
    if (!capacity) return { label: "Open", color: "#2563eb", pct: 0 };
    const pct = Math.round((booked / capacity) * 100);
    if (pct >= 100) return { label: "Full", color: "#dc2626", pct };
    if (pct >= 80) return { label: "Near Capacity", color: "#d97706", pct };
    return { label: "Open", color: "#059669", pct };
  };

  const getPhaseBadgeClass = (phase) => {
    switch (phase) {
      case "Upcoming":
        return "phase-badge upcoming";
      case "Ongoing":
        return "phase-badge ongoing";
      case "Completed":
        return "phase-badge completed";
      case "Cancelled":
        return "phase-badge cancelled";
      case "Pending Review":
      case "PendingApproval":
        return "phase-badge pending";
      default:
        return "phase-badge default";
    }
  };

  const getApprovalBadgeClass = (status) => {
    switch (status) {
      case "Approved":
        return "approval-badge approved";
      case "PendingApproval":
      case "Pending Review":
        return "approval-badge pending";
      case "Draft":
        return "approval-badge draft";
      case "Rejected":
        return "approval-badge rejected";
      default:
        return "approval-badge default";
    }
  };

  return (
    <div className="admin-workshops-container">
      {/* Top Header */}
      <div className="workshops-header">
        <div>
          <h1 className="workshops-page-title">Workshops & Events Management</h1>
          <p className="subtitle">
            Lifecycle monitoring, 5-step wizard creation, dynamic 4-tier pricing, and workshop-scoped QR check-in.
          </p>
        </div>

        <div className="workshops-top-actions">
          <button
            className="btn-create-workshop-primary"
            onClick={() => navigate("/admin_portal/workshops/create")}
          >
            <Plus size={16} />
            <span>Create Workshop</span>
          </button>

          <button
            className="btn-refresh-workshops"
            onClick={() => {
              loadWorkshops();
              loadCounts();
            }}
            disabled={loading}
            title="Refresh list and counts"
          >
            <RefreshCw size={15} className={loading ? "spin-icon" : ""} />
            <span>Refresh</span>
          </button>
        </div>
      </div>

      {/* 6 Category Tabs with Dynamic Badge Counts */}
      <div className="workshops-tabs-bar">
        {TABS.map((t) => {
          const count = counts[t.countKey] ?? 0;
          return (
            <button
              key={t.key}
              className={`workshop-tab-btn ${activeTab === t.key ? "active" : ""}`}
              onClick={() => setActiveTab(t.key)}
            >
              <span>{t.label}</span>
              <span className="tab-count-badge">{count}</span>
            </button>
          );
        })}
      </div>

      {/* Search & Secondary Filter Bar */}
      <div className="workshops-filter-row">
        {/* Search */}
        <div className="workshops-search-wrapper">
          <Search size={16} className="search-input-icon" />
          <input
            type="text"
            className="workshops-search-input"
            placeholder="Search by title, instructor, style, venue, or city..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button className="clear-search-btn" onClick={() => setSearchQuery("")}>
              ✕
            </button>
          )}
        </div>

        {/* City Filter */}
        <div className="filter-dropdown-group">
          <MapPin size={14} className="filter-group-icon" />
          <select
            className="filter-select-input"
            value={selectedCity}
            onChange={(e) => setSelectedCity(e.target.value)}
          >
            <option value="all">All Cities</option>
            {availableCities.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </div>

        {/* Date Filter */}
        <div className="filter-dropdown-group">
          <Calendar size={14} className="filter-group-icon" />
          <select
            className="filter-select-input"
            value={dateFilter}
            onChange={(e) => setDateFilter(e.target.value)}
          >
            <option value="all">All Dates</option>
            <option value="today">Today</option>
            <option value="week">This Week</option>
            <option value="month">This Month</option>
          </select>
        </div>

        {/* Cards vs List View Toggle */}
        <div className="view-mode-toggle-group">
          <button
            type="button"
            className={`view-toggle-btn ${viewMode === "cards" ? "active" : ""}`}
            onClick={() => setViewMode("cards")}
            title="Grid Cards View"
          >
            <LayoutGrid size={15} />
            <span>Cards</span>
          </button>
          <button
            type="button"
            className={`view-toggle-btn ${viewMode === "list" ? "active" : ""}`}
            onClick={() => setViewMode("list")}
            title="Data Table View"
          >
            <List size={15} />
            <span>List</span>
          </button>
        </div>
      </div>

      {/* Loading & Error States */}
      {loading && (
        <div className="workshops-loading-state">
          <div className="loading-spinner" />
          <span>Loading workshops...</span>
        </div>
      )}

      {error && (
        <div className="workshops-error-banner">
          <AlertTriangle size={18} />
          <span>Error loading workshops: {error}</span>
        </div>
      )}

      {/* Main Content Area */}
      {!loading && !error && (
        <>
          {filteredWorkshops.length === 0 ? (
            <div className="workshops-empty-panel">
              <div className="empty-icon-wrap">
                <Calendar size={40} />
              </div>
              <h3 className="empty-title">No workshops found</h3>
              <p className="empty-desc">
                There are no workshops matching your selected tab and filters.
              </p>
              <button
                className="btn-create-workshop-subtle"
                onClick={() => navigate("/admin_portal/workshops/create")}
              >
                + Create New Workshop
              </button>
            </div>
          ) : viewMode === "cards" ? (
            /* CARDS VIEW */
            <div className="workshops-cards-grid">
              {filteredWorkshops.map((w) => {
                const phase = w.lifecyclePhase || "Upcoming";
                const approval = w.approvalStatus || w.status || "Approved";
                const isLocked = isWorkshopLocked(w);
                const capInfo = getCapacityStatus(w.bookedCount || 0, w.capacity || 50);

                return (
                  <div key={w.id} className="workshop-feature-card">
                    {/* Card Media Header */}
                    <div
                      className="card-media-wrap"
                      onClick={() => navigate(`/admin_portal/workshops/${w.id}/overview`)}
                    >
                      {w.imageUrl ? (
                        <img src={w.imageUrl} alt={w.title} className="card-cover-img" />
                      ) : (
                        <div className="card-placeholder-img">
                          <div className="card-placeholder-emblem">ETHOS</div>
                          <span className="card-placeholder-label">{w.danceStyle || "Workshop Masterclass"}</span>
                        </div>
                      )}

                      {/* Overlaid Badges */}
                      <div className="card-badges-overlay">
                        <span className={getPhaseBadgeClass(phase)}>{phase}</span>
                        {approval !== "Approved" && (
                          <span className={getApprovalBadgeClass(approval)}>
                            {approval === "PendingApproval" ? "Pending Review" : approval}
                          </span>
                        )}
                      </div>

                      {/* Starting Price Pill */}
                      <div className="card-price-overlay">
                        <span className="price-prefix">From</span>
                        <span className="price-val">₹{w.price || 500}</span>
                      </div>
                    </div>

                    {/* Card Body */}
                    <div className="card-body-content">
                      <div className="card-tags-row">
                        <span className="style-tag">{w.danceStyle}</span>
                        <span className="level-tag">{w.level || "Open Level"}</span>
                      </div>

                      <h3
                        className="card-title"
                        onClick={() => navigate(`/admin_portal/workshops/${w.id}/overview`)}
                        title={w.title}
                      >
                        {w.title}
                      </h3>

                      <div className="card-instructor">
                        <span className="instructor-lbl">Trainer:</span>
                        <span className="instructor-val">{w.trainerName || "Ethos Faculty"}</span>
                      </div>

                      <div className="card-meta-list">
                        <div className="card-meta-item">
                          <Calendar size={13} />
                          <span>{w.workshopDate?.slice(0, 10)}</span>
                        </div>
                        <div className="card-meta-item">
                          <Clock size={13} />
                          <span>
                            {w.startTime?.slice(0, 5)} – {w.endTime?.slice(0, 5)} IST
                          </span>
                        </div>
                        <div className="card-meta-item venue-item">
                          <MapPin size={13} />
                          <span title={w.venueAddress || w.venue}>
                            {w.venue}
                            {w.city ? `, ${w.city}` : ""}
                          </span>
                        </div>
                      </div>

                      {/* Capacity Bar */}
                      <div className="card-capacity-box">
                        <div className="capacity-label-row">
                          <span className="cap-lbl">Registrations</span>
                          <span className="cap-val" style={{ color: capInfo.color }}>
                            {w.bookedCount || 0} / {w.capacity || 50} ({capInfo.label})
                          </span>
                        </div>
                        <div className="capacity-progress-track">
                          <div
                            className="capacity-progress-fill"
                            style={{
                              width: `${Math.min(100, capInfo.pct)}%`,
                              background: capInfo.color,
                            }}
                          />
                        </div>
                      </div>

                      {/* Card Action Buttons */}
                      <div className="card-action-bar">
                        <button
                          type="button"
                          className="btn-card-primary"
                          onClick={() => navigate(`/admin_portal/workshops/${w.id}/overview`)}
                        >
                          <span>Manage</span>
                          <ChevronRight size={14} />
                        </button>

                        <button
                          type="button"
                          className="btn-card-scanner"
                          onClick={() => navigate(`/admin_portal/workshops/${w.id}/scanner`)}
                          title="Open Workshop QR Check-in Scanner (Ticket Validation)"
                        >
                          <QrCode size={15} />
                        </button>

                        {isLocked ? (
                          <button
                            type="button"
                            className="btn-card-edit locked"
                            disabled
                            title="Modifications locked: workshop has already started"
                          >
                            <Lock size={14} />
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="btn-card-edit"
                            onClick={() => navigate(`/admin_portal/workshops/${w.id}/wizard`)}
                            title="Edit Workshop in Multi-Step Wizard"
                          >
                            <Edit size={14} />
                          </button>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            /* DATA TABLE LIST VIEW */
            <div className="workshops-table-card">
              <table className="workshops-main-table">
                <thead>
                  <tr>
                    <th style={{ width: "320px" }}>Workshop & Trainer</th>
                    <th>Date & Schedule</th>
                    <th>Location</th>
                    <th>Tiers & Price</th>
                    <th>Capacity</th>
                    <th>Lifecycle & Approval</th>
                    <th style={{ width: "240px" }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredWorkshops.map((w) => {
                    const phase = w.lifecyclePhase || "Upcoming";
                    const approval = w.approvalStatus || w.status || "Approved";
                    const isLocked = isWorkshopLocked(w);
                    const capInfo = getCapacityStatus(w.bookedCount || 0, w.capacity || 50);

                    return (
                      <tr key={w.id} className="workshop-data-row">
                        {/* Title & Trainer */}
                        <td>
                          <div className="table-workshop-cell">
                            {w.imageUrl ? (
                              <img src={w.imageUrl} alt="" className="table-thumb-img" />
                            ) : (
                              <div className="table-thumb-placeholder">W</div>
                            )}
                            <div className="table-workshop-texts">
                              <div
                                className="table-workshop-title"
                                onClick={() => navigate(`/admin_portal/workshops/${w.id}/overview`)}
                              >
                                {w.title}
                              </div>
                              <div className="table-workshop-sub">
                                {w.trainerName || "Lead Instructor"} • {w.danceStyle}
                              </div>
                            </div>
                          </div>
                        </td>

                        {/* Date & Schedule */}
                        <td>
                          <div className="schedule-date">{w.workshopDate?.slice(0, 10)}</div>
                          <div className="schedule-time">
                            {w.startTime?.slice(0, 5)} – {w.endTime?.slice(0, 5)} IST
                          </div>
                        </td>

                        {/* Location */}
                        <td>
                          <div className="location-venue">{w.venue}</div>
                          <div className="location-city">{w.city || "Hyderabad"}</div>
                        </td>

                        {/* Pricing */}
                        <td>
                          <div className="effective-price">₹{w.price || 500}</div>
                          <button
                            className="tier-inspect-btn"
                            onClick={() => openPricingTierModal(w)}
                          >
                            4 Tiers (₹500+)
                          </button>
                        </td>

                        {/* Capacity */}
                        <td>
                          <div className="cap-progress-cell">
                            <span style={{ color: capInfo.color, fontWeight: 700 }}>
                              {w.bookedCount || 0} / {w.capacity || 50}
                            </span>
                            <button
                              className="view-attendees-link"
                              onClick={() => openAttendeeList(w)}
                            >
                              View Roster
                            </button>
                          </div>
                        </td>

                        {/* Status Badges */}
                        <td>
                          <div className="table-status-stack">
                            <span className={getPhaseBadgeClass(phase)}>{phase}</span>
                            <span className={getApprovalBadgeClass(approval)}>
                              {approval === "PendingApproval" ? "Pending Review" : approval}
                            </span>
                          </div>
                        </td>

                        {/* Actions */}
                        <td>
                          <div className="table-actions-group">
                            <button
                              className="btn-table-primary"
                              onClick={() => navigate(`/admin_portal/workshops/${w.id}/overview`)}
                            >
                              Portal
                            </button>
                            <button
                              className="btn-table-scanner"
                              onClick={() => navigate(`/admin_portal/workshops/${w.id}/scanner`)}
                              title="QR Scanner"
                            >
                              <QrCode size={14} />
                            </button>
                            {isLocked ? (
                              <button
                                className="btn-table-edit locked"
                                disabled
                                title="Editing locked: workshop has started"
                              >
                                <Lock size={13} />
                              </button>
                            ) : (
                              <button
                                className="btn-table-edit"
                                onClick={() => navigate(`/admin_portal/workshops/${w.id}/wizard`)}
                                title="Edit in Multi-Step Wizard"
                              >
                                <Edit size={13} />
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {/* Pricing Tier Modal */}
      {pricingTierModal.open && (
        <div className="modal-backdrop" onClick={() => setPricingTierModal({ ...pricingTierModal, open: false })}>
          <div className="modal-card pricing-modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Dynamic 4-Tier Pricing: {pricingTierModal.workshop?.title}</h3>
              <button
                className="close-btn"
                onClick={() => setPricingTierModal({ ...pricingTierModal, open: false })}
              >
                ✕
              </button>
            </div>
            <div className="modal-body">
              <p className="pricing-modal-desc">
                Authoritative 4-tier pricing model (₹500 / ₹600 / ₹700 / ₹800). Ticket prices update server-side exclusively as confirmed registrations accumulate.
              </p>

              {pricingTierModal.loading ? (
                <div className="loading-state">Loading tiers...</div>
              ) : (
                <div className="tiers-list">
                  {pricingTierModal.tiers.map((t) => (
                    <div key={t.tierNumber} className="tier-row-input">
                      <span className="tier-name-label">{t.tierName || `Tier ${t.tierNumber}`}</span>
                      <span className="tier-range-label">
                        {t.minTickets} – {t.maxTickets ? t.maxTickets : "Max"} Bookings
                      </span>
                      <div className="tier-price-input-wrap">
                        <span className="currency-prefix">₹</span>
                        <input
                          type="number"
                          className="tier-price-field"
                          value={t.price}
                          onChange={(e) => handleTierPriceChange(t.tierNumber, e.target.value)}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {pricingTierModal.error && (
                <div className="error-banner" style={{ marginTop: "12px" }}>
                  {pricingTierModal.error}
                </div>
              )}
            </div>
            <div className="modal-actions">
              <button
                className="admin-btn secondary"
                onClick={() => setPricingTierModal({ ...pricingTierModal, open: false })}
              >
                Close
              </button>
              <button
                className="admin-btn primary"
                onClick={savePricingTiers}
                disabled={pricingTierModal.saving}
              >
                {pricingTierModal.saving ? "Saving..." : "Save Pricing Tiers"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Action Modal (Approve, Reject, Cancel, etc.) */}
      {actionModal.open && (
        <div className="modal-backdrop" onClick={() => setActionModal({ ...actionModal, open: false })}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>
                {actionModal.type === "approve" && "Approve Workshop"}
                {actionModal.type === "reject" && "Reject Workshop"}
                {actionModal.type === "cancel" && "Cancel Workshop"}
                {actionModal.type === "complete" && "Mark Workshop Completed"}
                {actionModal.type === "publish" && "Publish Workshop"}
                {actionModal.type === "unpublish" && "Unpublish Workshop"}
              </h3>
              <button
                className="close-btn"
                onClick={() => setActionModal({ ...actionModal, open: false })}
              >
                ✕
              </button>
            </div>
            <div className="modal-body">
              <p>
                Target Workshop: <strong>{actionModal.workshop?.title}</strong>
              </p>

              {actionModal.type === "approve" && (
                <div className="modal-field">
                  <label>Approved Starting Price (₹)</label>
                  <input
                    type="number"
                    className="modal-input"
                    value={actionModal.price}
                    onChange={(e) => setActionModal({ ...actionModal, price: e.target.value })}
                  />
                </div>
              )}

              {(actionModal.type === "reject" || actionModal.type === "cancel") && (
                <div className="modal-field">
                  <label>Reason (Mandatory)</label>
                  <textarea
                    className="modal-textarea"
                    rows={3}
                    placeholder="Enter reason..."
                    value={actionModal.reason}
                    onChange={(e) => setActionModal({ ...actionModal, reason: e.target.value })}
                  />
                </div>
              )}
            </div>
            <div className="modal-actions">
              <button
                className="admin-btn secondary"
                onClick={() => setActionModal({ ...actionModal, open: false })}
              >
                Cancel
              </button>
              <button
                className={`admin-btn ${actionModal.type === "reject" || actionModal.type === "cancel" ? "danger" : "primary"}`}
                onClick={confirmAction}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Attendees Roster Modal */}
      {attendeeModal.open && (
        <div className="modal-backdrop" onClick={() => setAttendeeModal({ ...attendeeModal, open: false })}>
          <div className="modal-card attendees-roster-modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Attendee Roster: {attendeeModal.workshop?.title}</h3>
              <button
                className="close-btn"
                onClick={() => setAttendeeModal({ ...attendeeModal, open: false })}
              >
                ✕
              </button>
            </div>
            <div className="modal-body">
              <div className="roster-stats-strip">
                <span className="stat-pill">Total: {attendeeStats.total}</span>
                <span className="stat-pill present">Present: {attendeeStats.present}</span>
                <span className="stat-pill absent">Absent: {attendeeStats.absent}</span>
                <span className="stat-pill unmarked">Pending: {attendeeStats.notMarked}</span>
              </div>

              <div className="roster-table-wrap">
                {attendeeModal.loading ? (
                  <div className="loading-state">Loading attendees...</div>
                ) : filteredAttendees.length === 0 ? (
                  <div className="empty-roster">No attendees found.</div>
                ) : (
                  <table className="roster-table">
                    <thead>
                      <tr>
                        <th>Attendee Name</th>
                        <th>Ticket Code</th>
                        <th>Contact</th>
                        <th>Attendance</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredAttendees.map((r, i) => (
                        <tr key={r.bookingId || i}>
                          <td>{r.studentName || "Guest Attendee"}</td>
                          <td><code>{r.customerCode || r.bookingReference || "—"}</code></td>
                          <td>{maskPhone(r.studentPhone)}</td>
                          <td>
                            <span className={`attend-pill ${(r.attendanceStatus || r.status || "").toLowerCase()}`}>
                              {r.attendanceStatus || r.status || "Registered"}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
            <div className="modal-actions">
              <button
                className="admin-btn secondary"
                onClick={() => setAttendeeModal({ ...attendeeModal, open: false })}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
