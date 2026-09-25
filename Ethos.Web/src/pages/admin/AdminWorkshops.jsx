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
  MoreHorizontal,
  Globe,
  Trash2,
} from "lucide-react";
import { adminApi } from "../../services/adminApi";
import AdminWorkshopFormModal from "../../components/admin/AdminWorkshopFormModal";
import "./AdminWorkshops.css";

const TABS = [
  { key: "draft", label: "Draft", countKey: "draft", phaseParam: "Draft" },
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

  // Active standalone wizard draft state (resumable creation)
  const [activeWizardDraft, setActiveWizardDraft] = useState(null);
  const [loadingDraft, setLoadingDraft] = useState(false);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCity, setSelectedCity] = useState("all");
  const [dateFilter, setDateFilter] = useState("all"); // "all" | "today" | "week" | "month"

  // Pagination State (Default 9 cards per page from img 1)
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(9);

  // Counts from backend
  const [counts, setCounts] = useState({
    draft: 0,
    upcoming: 0,
    ongoing: 0,
    ended: 0,
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
  const [openActionMenuId, setOpenActionMenuId] = useState(null);

  useEffect(() => {
    const handleOutsideClick = () => setOpenActionMenuId(null);
    if (openActionMenuId) {
      document.addEventListener("click", handleOutsideClick);
      return () => document.removeEventListener("click", handleOutsideClick);
    }
  }, [openActionMenuId]);

  const formatWorkshopDate = (dateStr) => {
    if (!dateStr) return "TBD";
    try {
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return dateStr.slice(0, 10);
      return d.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
    } catch {
      return dateStr.slice(0, 10);
    }
  };

  const formatWorkshopDuration = (startTime, endTime) => {
    if (!startTime || !endTime) return "2 hours";
    try {
      const [sh, sm] = startTime.split(":").map(Number);
      const [eh, em] = endTime.split(":").map(Number);
      const startMins = sh * 60 + sm;
      const endMins = eh * 60 + em;
      const diff = endMins - startMins;
      if (diff <= 0) return "2 hours";
      const hours = Math.floor(diff / 60);
      const mins = diff % 60;
      if (mins === 0) return `${hours} hour${hours > 1 ? "s" : ""}`;
      if (hours === 0) return `${mins} mins`;
      return `${hours}h ${mins}m`;
    } catch {
      return "2 hours";
    }
  };

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
          draft: res.draft ?? 0,
          upcoming: res.upcoming ?? 0,
          ongoing: res.ongoing ?? 0,
          ended: res.ended ?? 0,
          completed: (res.completed ?? 0) + (res.ended ?? 0),
          cancelled: res.cancelled ?? 0,
          all: res.all ?? 0,
        });
      }
    } catch (err) {
      console.warn("Could not fetch workshop counts:", err);
    }
  }, []);

  // Fetch active standalone wizard draft when Draft tab is active
  const loadWizardDraft = useCallback(async () => {
    if (activeTab !== "draft") {
      setActiveWizardDraft(null);
      return;
    }
    setLoadingDraft(true);
    try {
      const draft = await adminApi.getWorkshopDraft(null);
      if (draft && draft.draftJson) {
        let parsed = null;
        try {
          parsed = JSON.parse(draft.draftJson);
        } catch {}
        setActiveWizardDraft({ ...draft, parsed });
      } else {
        setActiveWizardDraft(null);
      }
    } catch {
      setActiveWizardDraft(null);
    } finally {
      setLoadingDraft(false);
    }
  }, [activeTab]);

  const handleDiscardWizardDraft = async (draftId) => {
    if (!window.confirm("Are you sure you want to discard this in-progress wizard draft? Any unsaved progress in the creation wizard will be permanently cleared.")) return;
    try {
      await adminApi.discardWorkshopDraft(draftId);
      setActiveWizardDraft(null);
      loadCounts();
      loadWorkshops();
    } catch (err) {
      alert(err.message || "Failed to discard draft.");
    }
  };

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
      if (activeTab === "pending" && (!selectedCity || selectedCity === "all")) {
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

  useEffect(() => {
    loadWizardDraft();
  }, [loadWizardDraft]);

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

  // Reset pagination to page 1 whenever filters or tabs change
  useEffect(() => {
    setCurrentPage(1);
  }, [activeTab, searchQuery, selectedCity, dateFilter]);

  // Derived Pagination (Image 1: 9 cards per page)
  const totalItems = filteredWorkshops.length;
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));

  const paginatedWorkshops = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredWorkshops.slice(start, start + pageSize);
  }, [filteredWorkshops, currentPage, pageSize]);

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
      case "Ended":
      case "EndedPendingCompletion":
      case "Ended (Completion Pending)":
        return "phase-badge ended-pending";
      case "Cancelled":
        return "phase-badge cancelled";
      case "Draft":
        return "phase-badge draft";
      case "Rejected":
        return "phase-badge rejected";
      case "Unpublished":
        return "phase-badge unpublished";
      case "Archived":
        return "phase-badge archived";
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
      case "Unpublished":
        return "approval-badge unpublished";
      case "Archived":
        return "approval-badge archived";
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
          {(() => {
            const hasDraftCard = activeTab === "draft" && Boolean(activeWizardDraft);
            const isListEmpty = filteredWorkshops.length === 0 && !hasDraftCard;

            if (isListEmpty) {
              return (
                <div className="workshops-empty-panel">
                  <div className="empty-icon-wrap">
                    <Calendar size={40} />
                  </div>
                  <h3 className="empty-title">No workshops found</h3>
                  <p className="empty-desc">
                    There are no workshops matching your selected tab and filters.
                  </p>
                  <div style={{ display: "flex", gap: "10px", justifyContent: "center", marginTop: "16px", flexWrap: "wrap" }}>
                    {(activeTab !== "all" || searchQuery || selectedCity !== "all" || dateFilter !== "all") && (
                      <button
                        className="btn-view-all-workshops-empty"
                        style={{
                          background: "#111827",
                          color: "#ffffff",
                          border: "none",
                          padding: "10px 18px",
                          borderRadius: "10px",
                          fontWeight: "600",
                          fontSize: "14px",
                          cursor: "pointer",
                        }}
                        onClick={() => {
                          setActiveTab("all");
                          setSearchQuery("");
                          setSelectedCity("all");
                          setDateFilter("all");
                        }}
                      >
                        View All Workshops ({counts.all || workshops.length})
                      </button>
                    )}
                    <button
                      className="btn-create-workshop-subtle"
                      onClick={() => navigate("/admin_portal/workshops/create")}
                    >
                      + Create New Workshop
                    </button>
                  </div>
                </div>
              );
            }

            if (viewMode === "cards") {
              return (
                <div className="workshops-cards-grid">
                  {/* Standalone Wizard Draft Card (Active in-progress wizard draft) */}
                  {hasDraftCard && (() => {
                    const draftData = activeWizardDraft.parsed || {};
                    const draftTitle = draftData.title || draftData.step1?.title || "Untitled Workshop Draft";
                    const draftCoverImage = draftData.landscapeImageUrl || draftData.imageUrl || draftData.step2?.landscapeImageUrl || draftData.step2?.imageUrl || "";
                    const draftVenue = draftData.venue || draftData.city || draftData.step3?.venue || draftData.step3?.city || "Venue configuration pending";
                    const draftDate = draftData.workshopDate || draftData.step3?.workshopDate || null;
                    const draftDanceStyle = draftData.danceStyle || draftData.step1?.danceStyle || "Ethos Faculty";
                    const draftStep = draftData.currentStep || draftData.step || 1;

                    return (
                      <div className="workshop-feature-card workshop-draft-card">
                        <div
                          className="card-media-wrap"
                          onClick={() => navigate("/admin_portal/workshops/create")}
                        >
                          {draftCoverImage ? (
                            <img
                              src={draftCoverImage}
                              alt={draftTitle}
                              className="card-cover-img"
                            />
                          ) : (
                            <div className="card-placeholder-img draft-placeholder-img">
                              <div className="card-placeholder-emblem">DRAFT</div>
                              <span className="card-placeholder-label">
                                {draftDanceStyle || "In-Progress Wizard Draft"}
                              </span>
                            </div>
                          )}

                          <div className="card-status-pill-badge">
                            <span className="status-pill-dot dot-draft" />
                            <span className="status-pill-text">Draft (In Progress)</span>
                          </div>
                        </div>

                        <div className="card-body-content">
                          <div className="card-header-row">
                            <h3
                              className="card-title-modern"
                              onClick={() => navigate("/admin_portal/workshops/create")}
                              title={draftTitle}
                            >
                              {draftTitle}
                            </h3>

                            <div className="card-menu-anchor">
                              <button
                                type="button"
                                className="btn-card-more-menu"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setOpenActionMenuId(openActionMenuId === "draft_standalone" ? null : "draft_standalone");
                                }}
                                title="Options"
                              >
                                <MoreHorizontal size={18} />
                              </button>

                              {openActionMenuId === "draft_standalone" && (
                                <div className="card-menu-dropdown" onClick={(e) => e.stopPropagation()}>
                                  <button
                                    type="button"
                                    className="menu-dropdown-item"
                                    onClick={() => {
                                      setOpenActionMenuId(null);
                                      navigate("/admin_portal/workshops/create");
                                    }}
                                  >
                                    <Edit size={14} />
                                    <span>Resume Creation</span>
                                  </button>
                                  <button
                                    type="button"
                                    className="menu-dropdown-item menu-dropdown-danger"
                                    onClick={() => {
                                      setOpenActionMenuId(null);
                                      handleDiscardWizardDraft(activeWizardDraft.id);
                                    }}
                                  >
                                    <Trash2 size={14} />
                                    <span>Discard Draft</span>
                                  </button>
                                </div>
                              )}
                            </div>
                          </div>

                          {/* Venue Row */}
                          <div className="card-venue-row">
                            <MapPin size={14} className="card-venue-pin" />
                            <span>{draftVenue}</span>
                          </div>

                          {/* Meta Chips Strip */}
                          <div className="card-meta-chips-strip">
                            <div className="meta-chip-item">
                              <Calendar size={14} />
                              <span>
                                {draftDate ? formatWorkshopDate(draftDate) : "Date pending"}
                              </span>
                            </div>
                            <div className="meta-chip-divider" />
                            <div className="meta-chip-item">
                              <Users size={14} />
                              <span>{draftDanceStyle}</span>
                            </div>
                            <div className="meta-chip-divider" />
                            <div className="meta-chip-item">
                              <Clock size={14} />
                              <span>Step {draftStep} of 6</span>
                            </div>
                          </div>

                          {/* Status Bar */}
                          <div className="card-performance-pill card-draft-pill">
                            <span className="perf-metric">
                              Last saved: <strong>{new Date(activeWizardDraft.updatedAt || activeWizardDraft.createdAt).toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit" })}</strong>
                            </span>
                            <span className="perf-dot">•</span>
                            <span className="perf-metric">Active Session</span>
                          </div>

                          {/* Bottom Action Row: Direct Discard + Resume Creation */}
                          <div className="card-footer-action-row draft-action-row">
                            <button
                              type="button"
                              className="btn-card-discard-draft"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleDiscardWizardDraft(activeWizardDraft.id);
                              }}
                              title="Discard this draft"
                            >
                              <Trash2 size={14} />
                              <span>Discard Draft</span>
                            </button>

                            <button
                              type="button"
                              className="btn-card-view-details btn-card-resume-draft"
                              onClick={() => navigate("/admin_portal/workshops/create")}
                              title="Continue creating and publish workshop"
                            >
                              <span>Resume Creation</span>
                              <ChevronRight size={16} />
                            </button>
                          </div>
                        </div>
                      </div>
                    );
                  })()}

                  {paginatedWorkshops.map((w) => {
                const phase = w.lifecyclePhase || "Upcoming";
                const isLocked = isWorkshopLocked(w);
                const bookings = w.bookedCount || 0;
                const revenue = w.totalRevenue != null ? Number(w.totalRevenue) : bookings * (w.price || 0);
                const formattedRevenue = revenue.toLocaleString("en-IN");
                const isEnded = phase === "Ended" || phase === "EndedPendingCompletion" || (w.status === "Published" && new Date(w.endUtc || w.workshopDate) <= new Date());
                const phaseClean = phase === "PendingApproval" ? "Pending Review" : (isEnded ? "Ended" : phase);

                return (
                  <div key={w.id} className="workshop-feature-card">
                    {/* Card Media Header - Landscape Banner */}
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

                      {/* Status Pill Badge (Top-Left matching Img 3) */}
                      <div className="card-status-pill-badge">
                        <span className={`status-pill-dot dot-${phase.toLowerCase().replace(/\s+/g, "-")}`} />
                        <span className="status-pill-text">{phaseClean}</span>
                      </div>
                    </div>

                    {/* Card Body */}
                    <div className="card-body-content">
                      {/* Title + Action Menu Header */}
                      <div className="card-header-row">
                        <h3
                          className="card-title-modern"
                          onClick={() => navigate(`/admin_portal/workshops/${w.id}/overview`)}
                          title={w.title}
                        >
                          {w.title}
                        </h3>

                        <div className="card-menu-anchor">
                          <button
                            type="button"
                            className="btn-card-more-menu"
                            onClick={(e) => {
                              e.stopPropagation();
                              setOpenActionMenuId(openActionMenuId === w.id ? null : w.id);
                            }}
                            title="Options"
                          >
                            <MoreHorizontal size={18} />
                          </button>

                          {openActionMenuId === w.id && (
                            <div className="card-menu-dropdown" onClick={(e) => e.stopPropagation()}>
                              <button
                                type="button"
                                className="menu-dropdown-item"
                                onClick={() => {
                                  setOpenActionMenuId(null);
                                  navigate(`/admin_portal/workshops/${w.id}/overview`);
                                }}
                              >
                                <ExternalLink size={14} />
                                <span>Overview & Roster</span>
                              </button>
                              {!isLocked && (
                                <button
                                  type="button"
                                  className="menu-dropdown-item"
                                  onClick={() => {
                                    setOpenActionMenuId(null);
                                    navigate(`/admin_portal/workshops/${w.id}/wizard`);
                                  }}
                                >
                                  <Edit size={14} />
                                  <span>Edit Workshop</span>
                                </button>
                              )}
                              <button
                                type="button"
                                className="menu-dropdown-item"
                                onClick={() => {
                                  setOpenActionMenuId(null);
                                  navigate(`/admin_portal/workshops/${w.id}/scanner`);
                                }}
                              >
                                <QrCode size={14} />
                                <span>Check-in Scanner</span>
                              </button>

                              {w.status === "Published" && (phase === "EndedPendingCompletion" || new Date(w.endUtc || w.workshopDate) <= new Date()) && (
                                <button
                                  type="button"
                                  className="menu-dropdown-item menu-dropdown-complete"
                                  onClick={() => {
                                    setOpenActionMenuId(null);
                                    setActionModal({
                                      open: true,
                                      type: "complete",
                                      workshop: w,
                                      reason: "",
                                    });
                                  }}
                                >
                                  <CheckCircle size={14} />
                                  <span>Complete Workshop</span>
                                </button>
                              )}

                              {(w.status === "Approved" || w.status === "Unpublished") && (
                                <button
                                  type="button"
                                  className="menu-dropdown-item menu-dropdown-publish"
                                  onClick={() => {
                                    setOpenActionMenuId(null);
                                    setActionModal({
                                      open: true,
                                      type: "publish",
                                      workshop: w,
                                      reason: "",
                                    });
                                  }}
                                >
                                  <Globe size={14} />
                                  <span>Publish Workshop</span>
                                </button>
                              )}

                              {phase !== "Completed" && phase !== "Cancelled" && (
                                <button
                                  type="button"
                                  className="menu-dropdown-item menu-dropdown-danger"
                                  onClick={() => {
                                    setOpenActionMenuId(null);
                                    setActionModal({
                                      open: true,
                                      type: "cancel",
                                      workshop: w,
                                      reason: "",
                                    });
                                  }}
                                >
                                  <XCircle size={14} />
                                  <span>Cancel Workshop</span>
                                </button>
                              )}
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Approved Notice & Direct Publish Action */}
                      {w.status === "Approved" && (
                        <div className="card-approved-action-banner">
                          <div className="card-approved-notice">
                            <CheckCircle size={13} />
                            <span>Approved • Ready to publish</span>
                          </div>
                          <button
                            type="button"
                            className="btn-card-publish-action"
                            onClick={(e) => {
                              e.stopPropagation();
                              setActionModal({
                                open: true,
                                type: "publish",
                                workshop: w,
                                reason: "",
                              });
                            }}
                            title="Publish Workshop to Live Site"
                          >
                            <Globe size={13} />
                            <span>Publish Workshop</span>
                          </button>
                        </div>
                      )}

                      {/* Ended Notice & Direct Complete Action */}
                      {isEnded && (
                        <div className="card-ended-action-banner">
                          <div className="card-ended-notice">
                            <AlertTriangle size={13} />
                            <span>Completion pending</span>
                          </div>
                          <button
                            type="button"
                            className="btn-card-complete-action"
                            onClick={(e) => {
                              e.stopPropagation();
                              setActionModal({
                                open: true,
                                type: "complete",
                                workshop: w,
                                reason: "",
                              });
                            }}
                            title="Complete Workshop & Clean Media"
                          >
                            <CheckCircle size={13} />
                            <span>Complete Workshop</span>
                          </button>
                        </div>
                      )}

                      {/* Venue Row */}
                      <div className="card-venue-row">
                        <MapPin size={14} className="card-venue-pin" />
                        <span title={w.venueAddress || w.venue}>
                          {w.venue || "Skyhy Super Club"}{w.city ? `, ${w.city}` : ""}
                        </span>
                      </div>

                      {/* Meta Chips Strip: Date | Trainer | Duration */}
                      <div className="card-meta-chips-strip">
                        <div className="meta-chip-item">
                          <Calendar size={14} />
                          <span>{formatWorkshopDate(w.workshopDate)}</span>
                        </div>
                        <div className="meta-chip-divider" />
                        <div className="meta-chip-item">
                          <Users size={14} />
                          <span title={w.trainerName}>{w.trainerName || "Multiple"}</span>
                        </div>
                        <div className="meta-chip-divider" />
                        <div className="meta-chip-item">
                          <Clock size={14} />
                          <span>{formatWorkshopDuration(w.startTime, w.endTime)}</span>
                        </div>
                      </div>

                      {/* Performance Bar: Bookings & Revenue */}
                      <div className="card-performance-pill">
                        <span className="perf-metric">
                          Bookings: <strong>{bookings}</strong>
                        </span>
                        <span className="perf-dot">•</span>
                        <span className="perf-metric">
                          Revenue: <strong>₹{formattedRevenue}</strong>
                        </span>
                      </div>

                      {/* Bottom Row: Starting Price + View Details Button */}
                      <div className="card-footer-action-row">
                        <div className="card-price-badge">
                          <span>₹{w.price || 500}</span>
                        </div>

                        <button
                          type="button"
                          className="btn-card-view-details"
                          onClick={() => navigate(`/admin_portal/workshops/${w.id}/overview`)}
                        >
                          <span>View Details</span>
                          <ChevronRight size={16} />
                        </button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          );
        }

        return (
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
                    {/* Standalone Wizard Draft Table Row */}
                    {hasDraftCard && (() => {
                      const draftData = activeWizardDraft.parsed || {};
                      const draftTitle = draftData.title || draftData.step1?.title || "Untitled Workshop Draft";
                      const draftCoverImage = draftData.landscapeImageUrl || draftData.imageUrl || draftData.step2?.landscapeImageUrl || draftData.step2?.imageUrl || "";
                      const draftVenue = draftData.venue || draftData.city || draftData.step3?.venue || draftData.step3?.city || "Venue configuration pending";
                      const draftDanceStyle = draftData.danceStyle || draftData.step1?.danceStyle || "Ethos Faculty";
                      const draftStep = draftData.currentStep || draftData.step || 1;
                      const draftPrice = draftData.price || draftData.passTypes?.[0]?.price || draftData.pricingTiers?.[0]?.price || 500;

                      return (
                        <tr className="workshop-data-row draft-row">
                          <td>
                            <div className="table-workshop-cell">
                              {draftCoverImage ? (
                                <img src={draftCoverImage} alt={draftTitle} className="table-workshop-thumb" />
                              ) : (
                                <div className="table-thumb-placeholder draft-thumb">DRAFT</div>
                              )}
                              <div className="table-workshop-texts">
                                <div
                                  className="table-workshop-title"
                                  onClick={() => navigate("/admin_portal/workshops/create")}
                                >
                                  {draftTitle}
                                </div>
                                <div className="table-workshop-trainer">
                                  {draftDanceStyle}
                                </div>
                              </div>
                            </div>
                          </td>
                          <td>
                            <div className="table-schedule-cell">
                              <strong>Step {draftStep} of 6</strong>
                              <span>Last saved {new Date(activeWizardDraft.updatedAt || activeWizardDraft.createdAt).toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit" })}</span>
                            </div>
                          </td>
                          <td>
                            <div className="table-location-cell">
                              <span className="table-venue-name">{draftVenue}</span>
                              <span className="table-city-name">{draftData.city || "Hyderabad"}</span>
                            </div>
                          </td>
                          <td>
                            <span className="badge-draft-cell">₹{draftPrice}</span>
                          </td>
                          <td>
                            <span className="badge-draft-cell">—</span>
                          </td>
                          <td>
                            <div className="table-status-stack">
                              <span className="phase-badge draft">Draft (In Progress)</span>
                            </div>
                          </td>
                          <td>
                            <div className="table-actions-group">
                              <button
                                type="button"
                                className="btn-table-danger"
                                onClick={() => handleDiscardWizardDraft(activeWizardDraft.id)}
                                title="Discard Draft"
                              >
                                <Trash2 size={14} />
                              </button>
                              <button
                                type="button"
                                className="btn-table-primary"
                                onClick={() => navigate("/admin_portal/workshops/create")}
                                title="Resume Creation"
                              >
                                Resume
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })()}
                    {paginatedWorkshops.map((w) => {
                    const phase = w.lifecyclePhase || "Upcoming";
                    const approval = w.approvalStatus || w.status || "Approved";
                    const isLocked = isWorkshopLocked(w);
                    const isEndedRow = phase === "Ended" || phase === "EndedPendingCompletion" || (w.status === "Published" && new Date(w.endUtc || w.workshopDate) <= new Date());
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
                              <div className="table-workshop-trainer">
                                {w.trainerName || "Ethos Master Faculty"}
                              </div>
                            </div>
                          </div>
                        </td>

                        {/* Date & Schedule */}
                        <td>
                          <div className="table-schedule-cell">
                            <strong>{formatWorkshopDate(w.workshopDate)}</strong>
                            <span>{w.startTime?.slice(0, 5) || "10:00"} - {w.endTime?.slice(0, 5) || "12:00"}</span>
                          </div>
                        </td>

                        {/* Location */}
                        <td>
                          <div className="table-location-cell">
                            <span className="table-venue-name">{w.venue || "Studio Arena"}</span>
                            <span className="table-city-name">{w.city || "Hyderabad"}</span>
                          </div>
                        </td>

                        {/* Tiers & Starting Price */}
                        <td>
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
                            <span className={getPhaseBadgeClass(phase)}>
                              {phase === "PendingApproval" ? "Pending Review" : (isEndedRow ? "Ended" : phase)}
                            </span>
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
                            {(w.status === "Approved" || w.status === "Unpublished") && (
                              <button
                                className="btn-table-publish"
                                onClick={() => setActionModal({ open: true, type: "publish", workshop: w, reason: "" })}
                                title="Publish Workshop to Live Site"
                              >
                                <Globe size={14} />
                              </button>
                            )}
                            {isEndedRow && (
                              <button
                                className="btn-table-complete"
                                onClick={() => setActionModal({ open: true, type: "complete", workshop: w, reason: "" })}
                                title="Complete Workshop & Clean Media"
                              >
                                <CheckCircle size={14} />
                              </button>
                            )}
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
          );
        })()}

          {/* Pagination Bar matching Image 1 (Max 9 cards/page default) */}
          {filteredWorkshops.length > 0 && (
            <div className="workshops-pagination-bar">
              <div className="pagination-info-text">
                Page {currentPage} of {totalPages} - {totalItems} total
              </div>

              <div className="pagination-controls-group">
                <select
                  className="pagination-pagesize-select"
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setCurrentPage(1);
                  }}
                  aria-label="Cards or rows per page"
                >
                  <option value={9}>9 {viewMode === "cards" ? "cards" : "rows"}</option>
                  <option value={18}>18 {viewMode === "cards" ? "cards" : "rows"}</option>
                  <option value={27}>27 {viewMode === "cards" ? "cards" : "rows"}</option>
                </select>

                <button
                  type="button"
                  className="pagination-nav-btn"
                  disabled={currentPage <= 1}
                  onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </button>

                <button
                  type="button"
                  className="pagination-nav-btn"
                  disabled={currentPage >= totalPages}
                  onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                >
                  Next
                </button>
              </div>
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
