import React, { useState, useEffect, useRef } from "react";
import {
  Search,
  MapPin,
  Calendar,
  Globe,
  Edit3,
  CheckCircle2,
  AlertCircle,
  ExternalLink,
  RefreshCw,
  Plus,
  Trash2,
  Lock,
  X,
  Image as ImageIcon,
  Star,
  Upload,
} from "lucide-react";
import { adminApi } from "../../../../services/adminApi";
import ImageCropperModal from "../../../../components/admin/common/ImageCropperModal";
import NumericInput from "../../../../components/common/NumericInput";

export default function Step3VenueSchedule({ form, onChange, trainers = [], errors = {} }) {
  const [searchQuery, setSearchQuery] = useState("");
  const [ethosVenues, setEthosVenues] = useState([]);
  const [googlePlaces, setGooglePlaces] = useState([]);
  const [googleAvailable, setGoogleAvailable] = useState(true);
  const [notice, setNotice] = useState("");
  const [searching, setSearching] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [venueMode, setVenueMode] = useState("search"); // "search" | "url" | "manual"
  const [isEditingVenue, setIsEditingVenue] = useState(false);
  const [pastedUrl, setPastedUrl] = useState("");
  const [resolvingUrl, setResolvingUrl] = useState(false);
  const [urlResolveError, setUrlResolveError] = useState("");
  const [resolvedVenuePreview, setResolvedVenuePreview] = useState(null);
  const sessionTokenRef = useRef(crypto.randomUUID ? crypto.randomUUID() : `st_${Date.now()}_${Math.random().toString(36).slice(2)}`);
  const [cropperSessionIdx, setCropperSessionIdx] = useState(null);

  const searchBoxRef = useRef(null);

  // Sessions list management - strictly explicit: no fake default session auto-created
  const sessions = Array.isArray(form.sessions) ? form.sessions : [];

  const [deleteConfirmSessionIdx, setDeleteConfirmSessionIdx] = useState(null);

  const updateSessions = (newSessions) => {
    const withClientIds = (newSessions || []).map((s) => ({
      ...s,
      clientId: s.clientId || (typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : `cl_${Date.now()}_${Math.random().toString(36).slice(2)}`),
    }));
    onChange("sessions", withClientIds);
    // Keep workshop-level date, start/end time, and capacity synchronized
    if (withClientIds.length > 0) {
      const validSessions = withClientIds.filter((s) => s.sessionDate);
      if (validSessions.length > 0) {
        const sortedStart = [...validSessions].sort((a, b) => {
          const d = (a.sessionDate || "").localeCompare(b.sessionDate || "");
          if (d !== 0) return d;
          return (a.startTime || "").localeCompare(b.startTime || "");
        });
        const sortedEnd = [...validSessions].sort((a, b) => {
          const d = (b.sessionDate || "").localeCompare(a.sessionDate || "");
          if (d !== 0) return d;
          return (b.endTime || "").localeCompare(a.endTime || "");
        });

        onChange("workshopDate", sortedStart[0].sessionDate);
        onChange("startTime", sortedStart[0].startTime);
        onChange("endTime", sortedEnd[0].endTime);
        if (sortedStart[0].bookingCutoffTime !== undefined) {
          onChange("bookingCutoffTime", sortedStart[0].bookingCutoffTime);
        }
        if (sortedStart[0].trainerProfileId && !form.trainerProfileId) {
          onChange("trainerProfileId", sortedStart[0].trainerProfileId);
        }

        const maxCap = Math.max(...withClientIds.map((s) => Number(s.capacity) || 0));
        if (maxCap > 0 && (!form.capacity || form.capacity < maxCap)) {
          onChange("capacity", maxCap);
        }
      }
    } else {
      onChange("workshopDate", "");
    }
  };

  // Assigned trainers from Step 1 (Authoritative Workshop Faculty Pool)
  const assignedTrainerIds = Array.isArray(form.trainerProfileIds) && form.trainerProfileIds.length > 0
    ? form.trainerProfileIds
    : (form.trainerProfileId ? [form.trainerProfileId] : []);

  const workshopTrainers = trainers.filter((t) => {
    const id = t.trainerId || t.id || t.trainerProfileId;
    return assignedTrainerIds.includes(id);
  });

  // Strict Faculty Scoping: Session trainers must strictly belong to workshopTrainers
  const availableSessionTrainers = workshopTrainers;

  const defaultTrainerId = assignedTrainerIds[0] || (workshopTrainers[0]?.id || workshopTrainers[0]?.trainerProfileId || "");

  // Explicit Date Selection Modal State
  const [showAddDateModal, setShowAddDateModal] = useState(false);
  const [selectedDateInput, setSelectedDateInput] = useState("");
  const [dateModalError, setDateModalError] = useState("");

  const handleOpenAddDateModal = () => {
    setSelectedDateInput("");
    setDateModalError("");
    setShowAddDateModal(true);
  };

  const handleConfirmAddDate = () => {
    if (!selectedDateInput) {
      setDateModalError("Please select a date.");
      return;
    }
    const alreadyExists = sessions.some((s) => s.sessionDate === selectedDateInput);
    if (alreadyExists) {
      setDateModalError("This date is already added to the workshop schedule.");
      return;
    }

    const newSession = {
      id: undefined,
      clientId: typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : `sess_${Date.now()}_${Math.random().toString(36).slice(2)}`,
      sessionDate: selectedDateInput,
      startTime: "10:00",
      endTime: "11:30",
      trainerProfileId: defaultTrainerId,
      trainerProfileIds: defaultTrainerId ? [defaultTrainerId] : [],
      posterImageUrl: "",
      posterBlob: null,
      posterPreviewUrl: "",
      title: `Session ${sessions.length + 1}`,
      description: "",
      capacity: form.capacity || 30,
      bookedSeats: 0,
      bookingCutoffTime: "",
      displayOrder: sessions.length + 1,
      isActive: true,
    };

    updateSessions([...sessions, newSession]);
    setShowAddDateModal(false);
    setSelectedDateInput("");
    setDateModalError("");
  };

  // Add a session to an existing specific date card
  const handleAddSessionToDate = (targetDate) => {
    const sessionsOnDate = sessions.filter((s) => s.sessionDate === targetDate);
    const lastSessionOnDate = sessionsOnDate[sessionsOnDate.length - 1];

    let nextStartTime = "10:00";
    let nextEndTime = "11:30";

    if (lastSessionOnDate && lastSessionOnDate.endTime) {
      nextStartTime = lastSessionOnDate.endTime.slice(0, 5);
      try {
        const [h, m] = nextStartTime.split(":").map(Number);
        const totalMinutes = h * 60 + m + 90; // +90 mins
        const nextH = Math.min(Math.floor(totalMinutes / 60), 23);
        const nextM = totalMinutes % 60;
        nextEndTime = `${String(nextH).padStart(2, "0")}:${String(nextM).padStart(2, "0")}`;
      } catch {
        nextEndTime = "12:00";
      }
    }

    const newSession = {
      id: undefined,
      clientId: typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : `sess_${Date.now()}_${Math.random().toString(36).slice(2)}`,
      sessionDate: targetDate,
      startTime: nextStartTime,
      endTime: nextEndTime,
      trainerProfileId: defaultTrainerId,
      trainerProfileIds: defaultTrainerId ? [defaultTrainerId] : [],
      posterImageUrl: "",
      posterBlob: null,
      posterPreviewUrl: "",
      title: `Session ${sessions.length + 1}`,
      description: "",
      capacity: form.capacity || 30,
      bookedSeats: 0,
      bookingCutoffTime: "",
      displayOrder: sessions.length + 1,
      isActive: true,
    };

    updateSessions([...sessions, newSession]);
  };

  // Change date of all sessions on that date card (blocked if already has booked seats)
  const handleUpdateDate = (oldDate, newDate) => {
    if (!newDate || oldDate === newDate) return;
    const hasBookings = sessions.some((s) => s.sessionDate === oldDate && (s.bookedSeats || 0) > 0);
    if (hasBookings) {
      alert("Cannot change this date because sessions on this date already have active bookings.");
      return;
    }
    const updated = sessions.map((s) => {
      if (s.sessionDate === oldDate) {
        return { ...s, sessionDate: newDate };
      }
      return s;
    });
    updateSessions(updated);
  };

  // Remove an entire date and its sessions (blocked if any session has bookings)
  const handleRemoveDate = (targetDate) => {
    const hasBookings = sessions.some((s) => s.sessionDate === targetDate && (s.bookedSeats || 0) > 0);
    if (hasBookings) {
      alert("Cannot remove this date because sessions on this date already have active bookings.");
      return;
    }
    const remaining = sessions.filter((s) => s.sessionDate !== targetDate);
    updateSessions(remaining.map((s, idx) => ({ ...s, displayOrder: idx + 1 })));
  };

  const handleUpdateSession = (index, field, value) => {
    const updated = sessions.map((s, idx) => {
      if (idx !== index) return s;
      return { ...s, [field]: value };
    });
    updateSessions(updated);
  };

  const handleUpdateSessionFields = (index, patchObj) => {
    const updated = sessions.map((s, idx) => {
      if (idx !== index) return s;
      return { ...s, ...patchObj };
    });
    updateSessions(updated);
  };

  const handleRemoveSession = (index) => {
    const session = sessions[index];
    if (session && (session.bookedSeats || 0) > 0) {
      alert(`Cannot delete '${session.title || "Session"}' because it already has active bookings.`);
      return;
    }
    const filtered = sessions.filter((_, idx) => idx !== index);
    const reindexed = filtered.map((s, idx) => ({ ...s, displayOrder: idx + 1 }));
    updateSessions(reindexed);
  };

  // Overlap verification: Same date & any shared trainer cannot overlap in time (A.Start < B.End && B.Start < A.End)
  // Touching boundaries (A.End === B.Start) do NOT overlap and are permitted.
  const sessionOverlaps = [];
  for (let i = 0; i < sessions.length; i++) {
    for (let j = i + 1; j < sessions.length; j++) {
      const s1 = sessions[i];
      const s2 = sessions[j];
      if (s1.sessionDate && s2.sessionDate && s1.sessionDate === s2.sessionDate && s1.startTime && s1.endTime && s2.startTime && s2.endTime) {
        const t1Ids = Array.isArray(s1.trainerProfileIds) && s1.trainerProfileIds.length > 0
          ? s1.trainerProfileIds
          : (s1.trainerProfileId ? [s1.trainerProfileId] : []);
        const t2Ids = Array.isArray(s2.trainerProfileIds) && s2.trainerProfileIds.length > 0
          ? s2.trainerProfileIds
          : (s2.trainerProfileId ? [s2.trainerProfileId] : []);

        const sharedTrainerIds = t1Ids.filter((tid) => t2Ids.some((t2) => String(t2) === String(tid)) && tid !== "none" && tid);
        if (sharedTrainerIds.length > 0) {
          if (s1.startTime < s2.endTime && s2.startTime < s1.endTime) {
            const sharedNames = sharedTrainerIds
              .map((tid) => {
                const tr = trainers.find((t) => String(t.trainerId || t.id || t.trainerProfileId) === String(tid));
                return tr?.fullName || tr?.name || "Instructor";
              })
              .join(", ");
            sessionOverlaps.push({
              idx1: i,
              idx2: j,
              date: s1.sessionDate,
              message: `"${s1.title || `Session ${i + 1}`}" and "${s2.title || `Session ${j + 1}`}" overlap in time for instructor(s) (${sharedNames}) on ${s1.sessionDate}.`,
            });
          }
        }
      }
    }
  }

  // Debounced search across India when user types >= 2 characters
  useEffect(() => {
    if (searchQuery.trim().length < 2) {
      setEthosVenues([]);
      setGooglePlaces([]);
      setShowDropdown(false);
      return;
    }

    let isCancelled = false;
    const timer = setTimeout(async () => {
      setSearching(true);
      try {
        const res = await adminApi.searchVenues(searchQuery, sessionTokenRef.current);
        if (!isCancelled) {
          if (res && (res.ethosVenues || res.googlePlaces)) {
            setEthosVenues(res.ethosVenues || []);
            setGooglePlaces(res.googlePlaces || []);
            setGoogleAvailable(res.googlePlacesAvailable !== false);
            setNotice(res.notice || "");
          } else if (Array.isArray(res)) {
            setEthosVenues(res.filter((v) => v.source === "ethos"));
            setGooglePlaces(res.filter((v) => v.source === "google"));
          }
          setShowDropdown(true);
        }
      } catch (err) {
        console.warn("Venue search failed:", err);
        if (!isCancelled) {
          setGoogleAvailable(false);
          setNotice("Google Places is unavailable. Search existing Ethos venues or enter the venue manually.");
          setShowDropdown(true);
        }
      } finally {
        if (!isCancelled) setSearching(false);
      }
    }, 300);

    return () => {
      isCancelled = true;
      clearTimeout(timer);
    };
  }, [searchQuery]);

  // Click outside to close dropdown
  useEffect(() => {
    function handleClickOutside(e) {
      if (searchBoxRef.current && !searchBoxRef.current.contains(e.target)) {
        setShowDropdown(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelectVenue = async (item) => {
    onChange("venue", item.mainText || item.name || "");
    onChange("venueAddress", item.fullAddress || item.secondaryText || "");
    if (item.city) onChange("city", item.city);
    if (item.area) onChange("area", item.area);
    if (item.placeId) onChange("googlePlaceId", item.placeId);
    if (item.latitude != null) onChange("latitude", item.latitude);
    if (item.longitude != null) onChange("longitude", item.longitude);
    if (item.canonicalLocationUrl) onChange("locationUrl", item.canonicalLocationUrl);

    setSearchQuery("");
    setShowDropdown(false);
    setIsEditingVenue(false);

    const token = sessionTokenRef.current;
    // Reset session token for subsequent searches per Google Places API guidelines
    sessionTokenRef.current = crypto.randomUUID ? crypto.randomUUID() : `st_${Date.now()}_${Math.random().toString(36).slice(2)}`;

    if (item.placeId && (!item.city || item.latitude == null)) {
      try {
        const details = await adminApi.getPlaceDetails(item.placeId, token);
        if (details) {
          if (details.city) onChange("city", details.city);
          if (details.area) onChange("area", details.area);
          if (details.latitude != null) onChange("latitude", details.latitude);
          if (details.longitude != null) onChange("longitude", details.longitude);
          if (details.fullAddress) onChange("venueAddress", details.fullAddress);
          if (details.canonicalLocationUrl) onChange("locationUrl", details.canonicalLocationUrl);
        }
      } catch (err) {
        // Silently use the values already populated
      }
    }
  };

  const handleClearVenue = () => {
    onChange("venue", "");
    onChange("venueAddress", "");
    onChange("city", "");
    onChange("area", "");
    onChange("googlePlaceId", "");
    onChange("latitude", null);
    onChange("longitude", null);
    onChange("locationUrl", "");
    setSearchQuery("");
    setIsEditingVenue(true);
  };

  const handleResolveUrl = async () => {
    if (!pastedUrl.trim()) {
      setUrlResolveError("Please paste a Google Maps URL first.");
      return;
    }
    setUrlResolveError("");
    setResolvingUrl(true);
    setResolvedVenuePreview(null);

    try {
      const resolved = await adminApi.resolveVenueUrl(pastedUrl.trim());
      if (!resolved || !resolved.venueName) {
        setUrlResolveError("Could not extract venue details from the provided URL. Please verify the link or enter details manually.");
      } else {
        setResolvedVenuePreview(resolved);
      }
    } catch (err) {
      console.warn("URL resolution failed:", err);
      setUrlResolveError(err.message || "Failed to resolve Google Maps link. Please verify the URL or enter venue manually.");
    } finally {
      setResolvingUrl(false);
    }
  };

  const handleApplyResolvedVenue = () => {
    if (!resolvedVenuePreview) return;
    onChange("venue", resolvedVenuePreview.venueName || "");
    onChange("venueAddress", resolvedVenuePreview.fullAddress || "");
    if (resolvedVenuePreview.city) onChange("city", resolvedVenuePreview.city);
    if (resolvedVenuePreview.area) onChange("area", resolvedVenuePreview.area);
    if (resolvedVenuePreview.latitude != null) onChange("latitude", resolvedVenuePreview.latitude);
    if (resolvedVenuePreview.longitude != null) onChange("longitude", resolvedVenuePreview.longitude);
    if (resolvedVenuePreview.googlePlaceId) onChange("googlePlaceId", resolvedVenuePreview.googlePlaceId);
    if (resolvedVenuePreview.locationUrl) onChange("locationUrl", resolvedVenuePreview.locationUrl);

    setResolvedVenuePreview(null);
    setPastedUrl("");
    setIsEditingVenue(false);
  };

  const handleDismissResolvedVenue = () => {
    setResolvedVenuePreview(null);
  };

  const hasVenueSelected = Boolean(form.venue && form.venue.trim());
  const mapsSearchUrl = form.venueAddress || form.venue
    ? "https://www.google.com/maps/search/?api=1&query=" + encodeURIComponent((form.venue || "") + ", " + (form.venueAddress || ""))
    : null;

  // Distinct dates in sorted chronological order
  const distinctDates = Array.from(
    new Set(sessions.map((s) => s.sessionDate).filter(Boolean))
  ).sort();

  // Earliest session and latest session across all dates
  const validSessions = sessions.filter((s) => s.sessionDate);
  const earliestSession = validSessions.length > 0
    ? [...validSessions].sort((a, b) => {
        const d = (a.sessionDate || "").localeCompare(b.sessionDate || "");
        if (d !== 0) return d;
        return (a.startTime || "").localeCompare(b.startTime || "");
      })[0]
    : null;

  const latestSession = validSessions.length > 0
    ? [...validSessions].sort((a, b) => {
        const d = (b.sessionDate || "").localeCompare(a.sessionDate || "");
        if (d !== 0) return d;
        return (b.endTime || "").localeCompare(a.endTime || "");
      })[0]
    : null;

  const maxSessionCap = sessions.length > 0
    ? Math.max(...sessions.map((s) => Number(s.capacity) || 0))
    : (form.capacity || 30);

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Venue & Schedule</h2>
        <p className="wizard-section-desc">
          Search any venue across India and configure workshop dates and sessions. A workshop can span one or multiple dates with distinct sessions and instructors.
        </p>
      </div>

      {/* 1. VENUE SEARCH & SELECTION */}
      <div className="form-card">
        <div className="form-card-header">
          <MapPin size={18} className="form-card-icon" />
          <div>
            <h3 className="form-card-title">Workshop Venue & Location</h3>
            <p className="form-card-subtitle">Search studios, auditoriums, hotels, or colleges nationwide</p>
          </div>
        </div>

        {/* Selected Venue Preview Box */}
        {hasVenueSelected && !isEditingVenue ? (
          <div className="selected-venue-card">
            <div className="venue-preview-header">
              <div className="venue-preview-badge">
                <CheckCircle2 size={15} />
                <span>Selected Venue</span>
              </div>
              <div className="venue-preview-actions">
                {(form.locationUrl?.trim() || mapsSearchUrl) && (
                  <a
                    href={form.locationUrl?.trim() || mapsSearchUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="venue-map-link-btn"
                    title="View Location"
                  >
                    <ExternalLink size={13} />
                    <span>View Map</span>
                  </a>
                )}
                <button
                  type="button"
                  className="venue-change-btn"
                  onClick={() => {
                    setIsEditingVenue(true);
                  }}
                >
                  <RefreshCw size={13} />
                  <span>Change Venue</span>
                </button>
              </div>
            </div>

            <h4 className="venue-preview-title">{form.venue}</h4>
            <p className="venue-preview-address">{form.venueAddress || "Address not provided"}</p>

            <div className="venue-meta-pills">
              {form.city && <span className="venue-pill">City: <strong>{form.city}</strong></span>}
              {form.area && <span className="venue-pill">Area: <strong>{form.area}</strong></span>}
              {form.latitude != null && form.longitude != null && (
                <span className="venue-pill coord-pill">
                  {Number(form.latitude).toFixed(4)}° N, {Number(form.longitude).toFixed(4)}° E
                </span>
              )}
            </div>

            <div className="venue-location-link-row" style={{ marginTop: "16px", paddingTop: "14px", borderTop: "1px solid #E2E8F0" }}>
              <label className="form-label" style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "6px" }}>
                <span>Authoritative Location Link</span>
                <span className="badge-recommended">Recommended</span>
              </label>
              <div className="location-url-input-wrap">
                <input
                  type="url"
                  className={"form-input " + (errors.locationUrl ? "input-error" : "")}
                  placeholder="https://maps.google.com/... or https://maps.app.goo.gl/..."
                  value={form.locationUrl || ""}
                  onChange={(e) => onChange("locationUrl", e.target.value)}
                />
                {form.locationUrl && form.locationUrl.trim() && (
                  <a
                    href={form.locationUrl.trim()}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="btn-open-location-link"
                    title="Opens the location link in a new tab"
                  >
                    <ExternalLink size={13} />
                    <span>Open Link</span>
                  </a>
                )}
              </div>
              <p className="form-field-helper">
                This Google Maps link is embedded in attendee ticket passes and provides 1-tap navigation for your students.
              </p>
              {errors.locationUrl && <span className="error-text">{errors.locationUrl}</span>}
            </div>
          </div>
        ) : (
          <div>
            {/* 3-Option Venue Mode Selector */}
            <div className="venue-mode-tabs-container">
              <div className="venue-mode-tabs" role="tablist">
                <button
                  type="button"
                  role="tab"
                  aria-selected={venueMode === "search"}
                  className={`venue-mode-tab-btn ${venueMode === "search" ? "active" : ""}`}
                  onClick={() => { setVenueMode("search"); setUrlResolveError(""); }}
                >
                  <Search size={15} />
                  <span>Option 1: Search Nationwide</span>
                </button>
                <button
                  type="button"
                  role="tab"
                  aria-selected={venueMode === "url"}
                  className={`venue-mode-tab-btn ${venueMode === "url" ? "active" : ""}`}
                  onClick={() => { setVenueMode("url"); setUrlResolveError(""); }}
                >
                  <Globe size={15} />
                  <span>Option 2: Paste Google Maps Link</span>
                </button>
                <button
                  type="button"
                  role="tab"
                  aria-selected={venueMode === "manual"}
                  className={`venue-mode-tab-btn ${venueMode === "manual" ? "active" : ""}`}
                  onClick={() => { setVenueMode("manual"); setUrlResolveError(""); }}
                >
                  <Edit3 size={15} />
                  <span>Option 3: Manual Entry</span>
                </button>
              </div>
            </div>

            {/* Option 1: Search Box */}
            {venueMode === "search" && (
              <div className="venue-search-container" ref={searchBoxRef}>
                <div className="venue-search-input-wrap">
                  <Search size={18} className="venue-search-icon" />
                  <input
                    type="text"
                    className="venue-search-input"
                    placeholder="Search Ethos studios or any venue across India..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    onFocus={() => {
                      if (searchQuery.length >= 2) setShowDropdown(true);
                    }}
                  />
                  {searching && <span className="venue-searching-spinner" />}
                </div>

                {/* Autocomplete Dropdown */}
                {showDropdown && (
                  <div className="venue-dropdown-panel">
                    {notice && (
                      <div className="venue-notice-banner">
                        <AlertCircle size={14} />
                        <span>{notice}</span>
                      </div>
                    )}

                    {/* Group 1: Ethos Venues */}
                    {ethosVenues.length > 0 && (
                      <div className="venue-dropdown-group">
                        <div className="venue-group-heading">
                          <span>Ethos Venues</span>
                          <span className="group-count-badge">{ethosVenues.length}</span>
                        </div>
                        {ethosVenues.map((v) => (
                          <div
                            key={v.placeId || v.name}
                            className="venue-dropdown-item ethos-item"
                            onClick={() => handleSelectVenue(v)}
                          >
                            <div className="item-icon-col">
                              <span className="ethos-logo-badge">ETHOS</span>
                            </div>
                            <div className="item-info-col">
                              <div className="item-primary-text">{v.mainText || v.name}</div>
                              <div className="item-secondary-text">{v.secondaryText || v.fullAddress}</div>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}

                    {/* Group 2: Google Places Nationwide */}
                    {googlePlaces.length > 0 && (
                      <div className="venue-dropdown-group">
                        <div className="venue-group-heading">
                          <span>Google Places (India)</span>
                          <span className="group-count-badge">{googlePlaces.length}</span>
                        </div>
                        {googlePlaces.map((v) => (
                          <div
                            key={v.placeId}
                            className="venue-dropdown-item"
                            onClick={() => handleSelectVenue(v)}
                          >
                            <div className="item-icon-col">
                              <MapPin size={16} className="pin-icon" />
                            </div>
                            <div className="item-info-col">
                              <div className="item-primary-text">{v.mainText}</div>
                              <div className="item-secondary-text">{v.secondaryText || v.fullAddress}</div>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}

                    {/* No Results State */}
                    {searchQuery.length >= 2 && !searching && ethosVenues.length === 0 && googlePlaces.length === 0 && (
                      <div className="venue-no-results">
                        <p>No matching places found across India.</p>
                        <button
                          type="button"
                          className="enter-manual-link-btn"
                          onClick={() => {
                            setVenueMode("manual");
                            setShowDropdown(false);
                            if (!form.venue) onChange("venue", searchQuery);
                          }}
                        >
                          Enter venue details manually
                        </button>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}

            {/* Option 2: Paste Google Maps URL & Fetch Details */}
            {venueMode === "url" && (
              <div className="venue-url-resolve-box">
                <div className="venue-url-help-text">
                  Paste a Google Maps share link, mobile shortlink (e.g. <code>maps.app.goo.gl</code>), or desktop map/directions URL. We will securely resolve the place name, full address, city, area, and coordinates for your review.
                </div>
                <div className="venue-url-input-row">
                  <input
                    type="url"
                    className="form-input venue-url-input"
                    placeholder="https://maps.app.goo.gl/... or https://www.google.com/maps/place/..."
                    value={pastedUrl}
                    onChange={(e) => {
                      setPastedUrl(e.target.value);
                      if (urlResolveError) setUrlResolveError("");
                    }}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") {
                        e.preventDefault();
                        handleResolveUrl();
                      }
                    }}
                  />
                  <button
                    type="button"
                    className="btn-fetch-venue-details"
                    onClick={handleResolveUrl}
                    disabled={resolvingUrl || !pastedUrl.trim()}
                  >
                    {resolvingUrl ? (
                      <>
                        <span className="btn-spinner" />
                        <span>Fetching...</span>
                      </>
                    ) : (
                      <>
                        <RefreshCw size={14} />
                        <span>Fetch Venue Details</span>
                      </>
                    )}
                  </button>
                </div>

                {urlResolveError && (
                  <div className="venue-url-error-banner">
                    <AlertCircle size={15} />
                    <span>{urlResolveError}</span>
                  </div>
                )}

                {/* Confirmation Preview Card - Invariant: Form fields NEVER silently overwritten */}
                {resolvedVenuePreview && (
                  <div className="venue-resolution-preview-card">
                    <div className="preview-card-header">
                      <div className="preview-header-left">
                        {resolvedVenuePreview.isVerified ? (
                          <span className="venue-status-badge verified">
                            <CheckCircle2 size={13} />
                            <span>Verified Google Place</span>
                          </span>
                        ) : (
                          <span className="venue-status-badge unverified">
                            <AlertCircle size={13} />
                            <span>Best-Effort URL Details (Please Review)</span>
                          </span>
                        )}
                        <span className="preview-card-subtitle">Review resolved location details below before applying to workshop</span>
                      </div>
                    </div>

                    <div className="preview-details-grid">
                      <div className="preview-detail-row">
                        <span className="detail-label">Venue Name:</span>
                        <span className="detail-value venue-name-highlight">{resolvedVenuePreview.venueName || "—"}</span>
                      </div>
                      <div className="preview-detail-row">
                        <span className="detail-label">Full Address:</span>
                        <span className="detail-value">{resolvedVenuePreview.fullAddress || "—"}</span>
                      </div>
                      <div className="preview-meta-row">
                        <div className="preview-meta-item">
                          <span className="detail-label">City:</span>
                          <span className="detail-value pill">{resolvedVenuePreview.city || "Not detected"}</span>
                        </div>
                        <div className="preview-meta-item">
                          <span className="detail-label">Area:</span>
                          <span className="detail-value pill">{resolvedVenuePreview.area || "Not detected"}</span>
                        </div>
                        {resolvedVenuePreview.latitude != null && resolvedVenuePreview.longitude != null && (
                          <div className="preview-meta-item">
                            <span className="detail-label">Coordinates:</span>
                            <span className="detail-value pill coord">
                              {Number(resolvedVenuePreview.latitude).toFixed(4)}°, {Number(resolvedVenuePreview.longitude).toFixed(4)}°
                            </span>
                          </div>
                        )}
                      </div>
                      {resolvedVenuePreview.locationUrl && (
                        <div className="preview-detail-row map-link-row">
                          <span className="detail-label">Canonical Map Link:</span>
                          <a
                            href={resolvedVenuePreview.locationUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="preview-map-link"
                          >
                            <ExternalLink size={12} />
                            <span>Test Map Link in New Tab</span>
                          </a>
                        </div>
                      )}
                    </div>

                    <div className="preview-card-actions">
                      <button
                        type="button"
                        className="btn-use-venue-details"
                        onClick={handleApplyResolvedVenue}
                      >
                        <CheckCircle2 size={15} />
                        <span>Use These Details</span>
                      </button>
                      <button
                        type="button"
                        className="btn-dismiss-venue-preview"
                        onClick={handleDismissResolvedVenue}
                      >
                        <X size={15} />
                        <span>Dismiss</span>
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* Option 3: Manual Venue Entry Fields */}
            {venueMode === "manual" && (
              <div className="manual-venue-form-box">
                <div className="venue-url-help-text" style={{ marginBottom: "16px" }}>
                  Directly enter or adjust the venue name, full postal address, city, area, and location link for your workshop.
                </div>
                <div className="form-grid-2">
                  <div className="form-group">
                    <label className="form-label">
                      Venue Name <span className="req">*</span>
                    </label>
                    <input
                      type="text"
                      className={"form-input " + (errors.venue ? "input-error" : "")}
                      placeholder="e.g. Phoenix Arena, Balayogi Auditorium"
                      value={form.venue || ""}
                      onChange={(e) => onChange("venue", e.target.value)}
                    />
                    {errors.venue && <span className="error-text">{errors.venue}</span>}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Full Address</label>
                    <input
                      type="text"
                      className="form-input"
                      placeholder="Street, Landmark, Postal Code"
                      value={form.venueAddress || ""}
                      onChange={(e) => onChange("venueAddress", e.target.value)}
                    />
                  </div>
                </div>

                <div className="form-grid-2" style={{ marginTop: "14px" }}>
                  <div className="form-group">
                    <label className="form-label">City</label>
                    <input
                      type="text"
                      className="form-input"
                      placeholder="e.g. Hyderabad, Mumbai, Bengaluru"
                      value={form.city || ""}
                      onChange={(e) => onChange("city", e.target.value)}
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">Area / Locality</label>
                    <input
                      type="text"
                      className="form-input"
                      placeholder="e.g. Jubilee Hills, Koramangala, Bandra"
                      value={form.area || ""}
                      onChange={(e) => onChange("area", e.target.value)}
                    />
                  </div>
                </div>

                <div className="form-group" style={{ marginTop: "14px" }}>
                  <label className="form-label" style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                    <span>Location Link (Google Maps)</span>
                    <span className="badge-recommended">Recommended</span>
                  </label>
                  <div className="location-url-input-wrap">
                    <input
                      type="url"
                      className={"form-input " + (errors.locationUrl ? "input-error" : "")}
                      placeholder="https://maps.google.com/... or https://maps.app.goo.gl/..."
                      value={form.locationUrl || ""}
                      onChange={(e) => onChange("locationUrl", e.target.value)}
                    />
                    {form.locationUrl && form.locationUrl.trim() && (
                      <a
                        href={form.locationUrl.trim()}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="btn-open-location-link"
                        title="Opens the location link in a new tab"
                      >
                        <ExternalLink size={13} />
                        <span>Open Link</span>
                      </a>
                    )}
                  </div>
                  <p className="form-field-helper">
                    Paste the Google Maps or venue location link customers should use to reach the workshop.
                  </p>
                  {errors.locationUrl && <span className="error-text">{errors.locationUrl}</span>}
                </div>

                {form.venue && form.venue.trim() && (
                  <div style={{ marginTop: "16px", display: "flex", justifyContent: "flex-end" }}>
                    <button
                      type="button"
                      className="btn-use-venue-details"
                      onClick={() => setIsEditingVenue(false)}
                    >
                      <CheckCircle2 size={14} />
                      <span>Done Editing Venue</span>
                    </button>
                  </div>
                )}
              </div>
            )}

            {isEditingVenue && hasVenueSelected && (
              <div style={{ marginTop: "12px", textAlign: "right" }}>
                <button
                  type="button"
                  className="venue-cancel-edit-btn"
                  onClick={() => setIsEditingVenue(false)}
                >
                  Cancel Changing Venue
                </button>
              </div>
            )}
          </div>
        )}

        {errors.venue && venueMode !== "manual" && !hasVenueSelected && (
          <span className="error-text mt-2 block" style={{ marginTop: "8px", display: "block" }}>{errors.venue}</span>
        )}
      </div>

      {/* 2. WORKSHOP DATES & SESSIONS MANAGER */}
      <div className="form-card" style={{ marginTop: "24px" }}>
        <div
          className="form-card-header"
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            flexWrap: "wrap",
            gap: "12px",
          }}
        >
          <div style={{ display: "flex", gap: "12px", alignItems: "center" }}>
            <Calendar size={22} className="form-card-icon" style={{ color: "#FF5500" }} />
            <div>
              <h3 className="form-card-title">
                Workshop Dates & Sessions ({distinctDates.length} {distinctDates.length === 1 ? "Date" : "Dates"}, {sessions.length} {sessions.length === 1 ? "Session" : "Sessions"})
              </h3>
              <p className="form-card-subtitle">
                Configure single-day or multi-day schedules. Group sessions under each date with individual instructors, capacities, and booking cutoffs.
              </p>
            </div>
          </div>

          {sessions.length > 0 && (
            <button
              type="button"
              className="btn btn-sm"
              onClick={handleOpenAddDateModal}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "6px",
                padding: "8px 16px",
                fontSize: "12px",
                fontWeight: 600,
                borderRadius: "8px",
                background: "#FF5500",
                color: "#fff",
                border: "none",
                cursor: "pointer",
              }}
            >
              <Plus size={14} /> Add Another Workshop Date
            </button>
          )}
        </div>

        {/* Global Schedule Overview Banner (Calculated automatically from sessions) */}
        {sessions.length > 0 && (
          <div className="schedule-summary-banner" style={{ margin: "16px 0 20px" }}>
            <div className="summary-col">
              <span className="summary-label">Timezone</span>
              <span className="summary-val timezone-val">
                <Globe size={13} />
                Asia/Kolkata (IST • UTC+05:30)
              </span>
            </div>

            <div className="summary-col">
              <span className="summary-label">Date Range</span>
              <span className="summary-val" style={{ color: "#0f172a", fontWeight: 600 }}>
                {distinctDates.length > 1
                  ? `${distinctDates[0]} to ${distinctDates[distinctDates.length - 1]} (${distinctDates.length} days)`
                  : (distinctDates[0] || "—")}
              </span>
            </div>

            <div className="summary-col">
              <span className="summary-label">Daily Schedule</span>
              <span className="summary-val duration-val">
                {earliestSession && latestSession
                  ? `${earliestSession.startTime?.slice(0, 5)} – ${latestSession.endTime?.slice(0, 5)} IST`
                  : "—"}
              </span>
            </div>

            <div className="summary-col">
              <span className="summary-label">Max Session Capacity</span>
              <span className="summary-val cutoff-val">
                {maxSessionCap} seats
              </span>
            </div>

            <div className="summary-col">
              <span className="summary-label">QR Check-in Window</span>
              <span className="summary-val qr-window-val">Start -30m to End</span>
            </div>
          </div>
        )}

        {/* Step-level sessions error */}
        {errors.sessions && (
          <div
            style={{
              margin: "12px 0",
              padding: "10px 14px",
              borderRadius: "8px",
              background: "rgba(239, 68, 68, 0.12)",
              border: "1px solid rgba(239, 68, 68, 0.4)",
              color: "#fca5a5",
              fontSize: "13px",
              display: "flex",
              alignItems: "center",
              gap: "8px",
            }}
          >
            <AlertCircle size={16} color="#ef4444" />
            <span>{errors.sessions}</span>
          </div>
        )}

        {/* Global Overlap Warning Alert */}
        {sessionOverlaps.length > 0 && (
          <div
            style={{
              margin: "12px 0 20px",
              padding: "12px 16px",
              borderRadius: "8px",
              background: "rgba(239, 68, 68, 0.12)",
              border: "1px solid rgba(239, 68, 68, 0.4)",
              color: "#fca5a5",
              fontSize: "13px",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "8px", fontWeight: 700, marginBottom: "4px" }}>
              <AlertCircle size={16} color="#ef4444" />
              <span>Schedule Conflict / Trainer Overlap Detected</span>
            </div>
            <ul style={{ margin: 0, paddingLeft: "20px" }}>
              {sessionOverlaps.map((ov, idx) => (
                <li key={idx} style={{ marginTop: "2px" }}>
                  ⚠ {ov.message}
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* EMPTY STATE: When no dates/sessions exist */}
        {sessions.length === 0 ? (
          <div
            style={{
              textAlign: "center",
              padding: "48px 24px",
              background: "rgba(255, 255, 255, 0.02)",
              border: "1px dashed rgba(255, 255, 255, 0.15)",
              borderRadius: "12px",
              margin: "16px 0",
            }}
          >
            <Calendar size={42} style={{ color: "#FF5500", marginBottom: "14px" }} />
            <h4 style={{ color: "#f8fafc", fontSize: "16px", fontWeight: 700, margin: "0 0 8px" }}>
              No Workshop Dates Added Yet
            </h4>
            <p style={{ color: "#94a3b8", fontSize: "13px", maxWidth: "480px", margin: "0 auto 20px", lineHeight: 1.5 }}>
              Every workshop requires at least one date with one or more scheduled sessions. Click below to add your first workshop date.
            </p>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleOpenAddDateModal}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "8px",
                padding: "10px 22px",
                fontSize: "13px",
                fontWeight: 600,
                borderRadius: "8px",
                background: "#FF5500",
                color: "#fff",
                border: "none",
                cursor: "pointer",
              }}
            >
              <Plus size={16} /> Add First Workshop Date
            </button>
          </div>
        ) : (
          /* DATE-GROUPED SESSIONS HIERARCHY */
          <div style={{ display: "flex", flexDirection: "column", gap: "24px", marginTop: "16px" }}>
            {distinctDates.map((dateStr, dateIdx) => {
              const sessionsOnThisDate = sessions
                .map((sess, globalIdx) => ({ sess, globalIdx }))
                .filter((item) => item.sess.sessionDate === dateStr)
                .sort((a, b) => (a.sess.startTime || "").localeCompare(b.sess.startTime || ""));

              const isDateBooked = sessionsOnThisDate.some((item) => (item.sess.bookedSeats || 0) > 0);

              let formattedDateTitle = dateStr;
              try {
                formattedDateTitle = new Date(`${dateStr}T00:00:00`).toLocaleDateString("en-IN", {
                  weekday: "long",
                  day: "numeric",
                  month: "long",
                  year: "numeric",
                });
              } catch {
                formattedDateTitle = dateStr;
              }

              return (
                <div
                  key={dateStr || dateIdx}
                  style={{
                    border: "1px solid #e2e8f0",
                    borderRadius: "12px",
                    background: "#ffffff",
                    boxShadow: "0 1px 3px rgba(0, 0, 0, 0.04)",
                    padding: "18px 20px",
                  }}
                >
                  {/* Date Block Header */}
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      paddingBottom: "14px",
                      marginBottom: "16px",
                      borderBottom: "1px solid #e2e8f0",
                      flexWrap: "wrap",
                      gap: "12px",
                    }}
                  >
                    <div style={{ display: "flex", alignItems: "center", gap: "12px", flexWrap: "wrap" }}>
                      <span
                        style={{
                          background: "rgba(255, 85, 0, 0.15)",
                          color: "#FF5500",
                          border: "1px solid rgba(255, 85, 0, 0.3)",
                          padding: "4px 10px",
                          borderRadius: "6px",
                          fontWeight: 700,
                          fontSize: "12px",
                          textTransform: "uppercase",
                        }}
                      >
                        Date {dateIdx + 1}
                      </span>

                      <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                        <input
                          type="date"
                          className="form-input"
                          value={dateStr}
                          disabled={isDateBooked}
                          title={isDateBooked ? "Date cannot be changed because sessions on this date have active bookings" : "Select date"}
                          onChange={(e) => handleUpdateDate(dateStr, e.target.value)}
                          style={{
                            padding: "4px 8px",
                            fontSize: "13px",
                            width: "155px",
                            cursor: isDateBooked ? "not-allowed" : "pointer",
                            opacity: isDateBooked ? 0.7 : 1,
                          }}
                        />
                        <strong style={{ fontSize: "14px", color: "#0f172a" }}>{formattedDateTitle}</strong>
                      </div>

                      {isDateBooked && (
                        <span
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "4px",
                            fontSize: "11px",
                            fontWeight: 600,
                            padding: "3px 8px",
                            borderRadius: "4px",
                            background: "rgba(245, 158, 11, 0.15)",
                            color: "#fbbf24",
                            border: "1px solid rgba(245, 158, 11, 0.3)",
                          }}
                        >
                          <Lock size={12} /> Active Bookings: Date Locked
                        </span>
                      )}
                    </div>

                    <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
                      <button
                        type="button"
                        onClick={() => handleAddSessionToDate(dateStr)}
                        style={{
                          background: "rgba(255, 85, 0, 0.1)",
                          border: "1px solid rgba(255, 85, 0, 0.4)",
                          color: "#FF5500",
                          padding: "5px 12px",
                          borderRadius: "6px",
                          fontSize: "12px",
                          fontWeight: 600,
                          cursor: "pointer",
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "4px",
                        }}
                      >
                        <Plus size={13} /> Add Session
                      </button>

                      {distinctDates.length > 1 && (
                        <button
                          type="button"
                          disabled={isDateBooked}
                          onClick={() => handleRemoveDate(dateStr)}
                          style={{
                            background: "none",
                            border: "none",
                            color: isDateBooked ? "#475569" : "#ef4444",
                            cursor: isDateBooked ? "not-allowed" : "pointer",
                            fontSize: "12px",
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "4px",
                          }}
                          title={isDateBooked ? "Cannot remove date with active bookings" : "Remove this entire date and its sessions"}
                        >
                          <Trash2 size={13} /> Remove Date
                        </button>
                      )}
                    </div>
                  </div>

                  {/* Sessions inside this Date */}
                  <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
                    {sessionsOnThisDate.map(({ sess, globalIdx }, localIdx) => {
                      const hasConflict = sessionOverlaps.some(
                        (o) => o.idx1 === globalIdx || o.idx2 === globalIdx
                      );

                      // Adjacent check: adjacent to previous or next session on same date
                      const isAdjacent = sessionsOnThisDate.some(({ sess: otherSess, globalIdx: otherIdx }) => {
                        if (otherIdx === globalIdx) return false;
                        return sess.startTime === otherSess.endTime || sess.endTime === otherSess.startTime;
                      });

                      const isSessionBooked = (sess.bookedSeats || 0) > 0;
                      const hasInvalidTime = sess.startTime && sess.endTime && sess.endTime <= sess.startTime;
                      const hasInvalidCapacity = sess.capacity != null && Number(sess.capacity) <= 0;

                      return (
                        <div
                          key={sess.id || globalIdx}
                          style={{
                            padding: "16px 18px",
                            borderRadius: "10px",
                            background: hasConflict
                              ? "#fef2f2"
                              : "#f8fafc",
                            border: hasConflict
                              ? "1.5px solid #ef4444"
                              : hasInvalidTime
                              ? "1.5px solid #f59e0b"
                              : "1px solid #e2e8f0",
                          }}
                        >
                          {/* Session Header Row */}
                          <div
                            style={{
                              display: "flex",
                              justifyContent: "space-between",
                              alignItems: "center",
                              marginBottom: "12px",
                              flexWrap: "wrap",
                              gap: "8px",
                            }}
                          >
                            <div style={{ display: "flex", alignItems: "center", gap: "10px", flex: 1, minWidth: "260px" }}>
                              <span
                                style={{
                                  padding: "3px 8px",
                                  borderRadius: "6px",
                                  background: "#0f172a",
                                  color: "#ffffff",
                                  fontSize: "11px",
                                  fontWeight: 700,
                                }}
                              >
                                Session {localIdx + 1}
                              </span>

                              <input
                                type="text"
                                className="form-input"
                                placeholder="Session Title (e.g. Urban Grooves & Footwork)"
                                value={sess.title || ""}
                                onChange={(e) => handleUpdateSession(globalIdx, "title", e.target.value)}
                                style={{ maxWidth: "360px", padding: "5px 10px", fontSize: "13px", fontWeight: 600 }}
                              />

                              {isSessionBooked && (
                                <span
                                  style={{
                                    fontSize: "11px",
                                    padding: "2px 8px",
                                    borderRadius: "4px",
                                    background: "rgba(16, 185, 129, 0.15)",
                                    color: "#34d399",
                                    border: "1px solid rgba(16, 185, 129, 0.3)",
                                    fontWeight: 600,
                                  }}
                                >
                                  {sess.bookedSeats} booked
                                </span>
                              )}

                              {isAdjacent && !hasConflict && (
                                <span
                                  style={{
                                    fontSize: "11px",
                                    padding: "2px 8px",
                                    borderRadius: "4px",
                                    background: "rgba(56, 189, 248, 0.12)",
                                    color: "#38bdf8",
                                    border: "1px solid rgba(56, 189, 248, 0.3)",
                                    fontWeight: 500,
                                  }}
                                  title="Continuous back-to-back schedule with adjacent session"
                                >
                                  Back-to-back
                                </span>
                              )}
                            </div>

                            <button
                              type="button"
                              disabled={isSessionBooked}
                              onClick={() => setDeleteConfirmSessionIdx(globalIdx)}
                              style={{
                                background: "none",
                                border: "none",
                                color: isSessionBooked ? "#475569" : "#ef4444",
                                cursor: isSessionBooked ? "not-allowed" : "pointer",
                                fontSize: "12px",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: "4px",
                              }}
                              title={isSessionBooked ? "Cannot delete session with active bookings" : "Delete session"}
                            >
                              <Trash2 size={13} /> Delete Session
                            </button>
                          </div>

                          {/* Session Description */}
                          <div className="form-group" style={{ marginBottom: "12px" }}>
                            <label className="form-label" style={{ fontSize: "11px", marginBottom: "4px" }}>
                              Session Description
                            </label>
                            <input
                              type="text"
                              className="form-input"
                              placeholder="e.g. 90-minute choreography focusing on musicality & footwork..."
                              value={sess.description || ""}
                              onChange={(e) => handleUpdateSession(globalIdx, "description", e.target.value)}
                              style={{ fontSize: "12px", padding: "6px 10px" }}
                            />
                          </div>

                          {/* Faculty & Time Grid */}
                          <div style={{ marginBottom: "14px" }}>
                            <label className="form-label" style={{ fontSize: "11px", marginBottom: "6px", display: "flex", justifyContent: "space-between" }}>
                              <span>Session Instructors (Faculty Pool) <span className="req">*</span></span>
                            </label>
                            {availableSessionTrainers.length === 0 ? (
                              <div style={{ padding: "8px 12px", borderRadius: "6px", background: "#fef3c7", border: "1px solid #fde68a", color: "#92400e", fontSize: "11px" }}>
                                ⚠ No faculty assigned to workshop in Step 1. Please add faculty in Step 1 first.
                              </div>
                            ) : (
                              <div style={{ display: "flex", flexWrap: "wrap", gap: "8px" }}>
                                {availableSessionTrainers.map((t) => {
                                  const id = t.trainerId || t.id || t.trainerProfileId;
                                  const curTrainerIds = Array.isArray(sess.trainerProfileIds) && sess.trainerProfileIds.length > 0
                                    ? sess.trainerProfileIds
                                    : (sess.trainerProfileId ? [sess.trainerProfileId] : []);
                                  const isSelected = curTrainerIds.some((tid) => String(tid) === String(id));

                                  return (
                                    <div
                                      key={id}
                                      role="button"
                                      tabIndex={0}
                                      style={{
                                        display: "inline-flex",
                                        alignItems: "center",
                                        gap: "6px",
                                        padding: "5px 12px",
                                        borderRadius: "20px",
                                        fontSize: "12px",
                                        fontWeight: 600,
                                        border: isSelected
                                          ? "1.5px solid #3b82f6"
                                          : "1px solid #cbd5e1",
                                        background: isSelected
                                          ? "rgba(59, 130, 246, 0.08)"
                                          : "#ffffff",
                                        color: isSelected ? "#0f172a" : "#64748b",
                                        cursor: "pointer",
                                        userSelect: "none",
                                        transition: "all 0.15s ease",
                                      }}
                                      onClick={() => {
                                        let nextIds;
                                        if (isSelected) {
                                          nextIds = curTrainerIds.filter((tid) => String(tid) !== String(id));
                                        } else {
                                          nextIds = [...curTrainerIds, id];
                                        }
                                        const nextLead = nextIds[0] || "";
                                        handleUpdateSessionFields(globalIdx, {
                                          trainerProfileIds: nextIds,
                                          trainerProfileId: nextLead,
                                        });
                                      }}
                                    >
                                      <span>{t.fullName || t.name}</span>
                                      {isSelected && (
                                        <span style={{ fontSize: "11px", color: "#3b82f6", fontWeight: 700 }}>✓</span>
                                      )}
                                    </div>
                                  );
                                })}
                              </div>
                            )}
                            {/* Validation warning if no instructor assigned */}
                            {(!sess.trainerProfileIds || sess.trainerProfileIds.length === 0) && !sess.trainerProfileId && (
                              <span className="error-text" style={{ fontSize: "11px", marginTop: "6px", display: "block" }}>
                                At least one instructor from the workshop faculty pool must be assigned to this session.
                              </span>
                            )}
                          </div>

                          {/* Times Grid */}
                          <div className="form-grid-2">
                            {/* Start Time */}
                            <div className="form-group">
                              <label className="form-label" style={{ fontSize: "11px" }}>
                                Start Time <span className="req">*</span>
                              </label>
                              <input
                                type="time"
                                className={"form-input " + (hasInvalidTime ? "input-error" : "")}
                                value={sess.startTime ? sess.startTime.slice(0, 5) : "10:00"}
                                onChange={(e) => handleUpdateSession(globalIdx, "startTime", e.target.value)}
                                style={{ fontSize: "12px" }}
                              />
                            </div>

                            {/* End Time */}
                            <div className="form-group">
                              <label className="form-label" style={{ fontSize: "11px" }}>
                                End Time <span className="req">*</span>
                              </label>
                              <input
                                type="time"
                                className={"form-input " + (hasInvalidTime ? "input-error" : "")}
                                value={sess.endTime ? sess.endTime.slice(0, 5) : "11:30"}
                                onChange={(e) => handleUpdateSession(globalIdx, "endTime", e.target.value)}
                                style={{ fontSize: "12px" }}
                              />
                            </div>
                          </div>

                          {/* Invalid Time Error */}
                          {hasInvalidTime && (
                            <span className="error-text" style={{ fontSize: "11px", marginTop: "4px", display: "block" }}>
                              End time ({sess.endTime}) must be after start time ({sess.startTime}).
                            </span>
                          )}

                          {/* Booking Cutoff Time */}
                          <div className="form-group" style={{ marginTop: "10px" }}>
                            <label className="form-label" style={{ fontSize: "11px" }}>
                              Booking Cutoff Time (IST)
                            </label>
                            <input
                              type="time"
                              className="form-input"
                              value={sess.bookingCutoffTime ? sess.bookingCutoffTime.slice(0, 5) : ""}
                              placeholder="Defaults to Start Time"
                              onChange={(e) => handleUpdateSession(globalIdx, "bookingCutoffTime", e.target.value)}
                              style={{ fontSize: "12px", maxWidth: "240px" }}
                            />
                            <span style={{ fontSize: "10px", color: "var(--text-muted, #71717a)", marginTop: "2px", display: "block" }}>
                              Optional. Defaults to session start time ({sess.startTime?.slice(0, 5) || "10:00"}) if not set.
                            </span>
                          </div>

                          {/* Session Poster Section (3:4 Portrait Crop Flow) */}
                          <div className="form-group" style={{ marginTop: "14px", paddingTop: "14px", borderTop: "1px dashed #e2e8f0" }}>
                            <label className="form-label" style={{ fontSize: "11px", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                              <span>Session Poster (3:4 Portrait)</span>
                              <span style={{ fontSize: "10px", color: "#64748b" }}>Optional • 900 × 1200 px</span>
                            </label>
                            {sess.posterPreviewUrl || sess.posterImageUrl ? (
                              <div style={{ display: "flex", alignItems: "center", gap: "14px", marginTop: "6px" }}>
                                <div style={{ width: "66px", height: "88px", borderRadius: "6px", overflow: "hidden", border: "1px solid #cbd5e1", background: "#0f172a", position: "relative", flexShrink: 0 }}>
                                  <img
                                    src={sess.posterPreviewUrl || sess.posterImageUrl}
                                    alt="Session poster"
                                    style={{ width: "100%", height: "100%", objectFit: "cover" }}
                                  />
                                </div>
                                <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
                                  <button
                                    type="button"
                                    onClick={() => setCropperSessionIdx(globalIdx)}
                                    style={{
                                      background: "#f1f5f9",
                                      border: "1px solid #cbd5e1",
                                      color: "#0f172a",
                                      padding: "4px 10px",
                                      borderRadius: "6px",
                                      fontSize: "11px",
                                      fontWeight: 600,
                                      cursor: "pointer",
                                      display: "inline-flex",
                                      alignItems: "center",
                                      gap: "4px",
                                    }}
                                  >
                                    <Edit3 size={12} /> Change Poster (3:4)
                                  </button>
                                  <button
                                    type="button"
                                    onClick={() => {
                                      handleUpdateSession(globalIdx, "posterImageUrl", "");
                                      handleUpdateSession(globalIdx, "posterPreviewUrl", "");
                                      handleUpdateSession(globalIdx, "posterBlob", null);
                                    }}
                                    style={{
                                      background: "none",
                                      border: "none",
                                      color: "#ef4444",
                                      fontSize: "11px",
                                      cursor: "pointer",
                                      display: "inline-flex",
                                      alignItems: "center",
                                      gap: "4px",
                                      padding: "2px 0",
                                    }}
                                  >
                                    <Trash2 size={12} /> Remove Poster (Use Workshop Cover)
                                  </button>
                                </div>
                              </div>
                            ) : (
                              <div
                                onClick={() => setCropperSessionIdx(globalIdx)}
                                style={{
                                  border: "1px dashed #cbd5e1",
                                  borderRadius: "8px",
                                  padding: "10px 14px",
                                  display: "flex",
                                  alignItems: "center",
                                  justifyContent: "space-between",
                                  cursor: "pointer",
                                  background: "#f8fafc",
                                  marginTop: "6px",
                                }}
                              >
                                <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                                  <ImageIcon size={16} color="#64748b" />
                                  <div>
                                    <span style={{ fontSize: "12px", fontWeight: 600, color: "#0f172a" }}>Upload Session Poster (3:4 Portrait)</span>
                                    <span style={{ fontSize: "11px", color: "#64748b", display: "block" }}>If omitted, workshop cover art will be used automatically.</span>
                                  </div>
                                </div>
                                <button
                                  type="button"
                                  style={{
                                    background: "rgba(255, 85, 0, 0.1)",
                                    border: "1px solid rgba(255, 85, 0, 0.3)",
                                    color: "#FF5500",
                                    padding: "4px 10px",
                                    borderRadius: "6px",
                                    fontSize: "11px",
                                    fontWeight: 600,
                                    cursor: "pointer",
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: "4px",
                                  }}
                                >
                                  <Upload size={12} /> Upload & Crop
                                </button>
                              </div>
                            )}
                          </div>

                          {/* Conflict Alert Banner on Card */}
                          {hasConflict && (
                            <div
                              style={{
                                marginTop: "10px",
                                padding: "8px 12px",
                                borderRadius: "6px",
                                background: "rgba(239, 68, 68, 0.15)",
                                border: "1px solid rgba(239, 68, 68, 0.4)",
                                color: "#fca5a5",
                                fontSize: "11px",
                                display: "flex",
                                alignItems: "center",
                                gap: "6px",
                              }}
                            >
                              <AlertCircle size={14} color="#ef4444" />
                              <span>This session overlaps with another session assigned to the same instructor on this date.</span>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* Add Another Date Button at Bottom */}
        {sessions.length > 0 && (
          <div style={{ marginTop: "24px", display: "flex", justifyContent: "center" }}>
            <button
              type="button"
              className="btn btn-sm"
              onClick={handleOpenAddDateModal}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "8px",
                padding: "10px 22px",
                borderRadius: "8px",
                background: "rgba(255, 255, 255, 0.05)",
                border: "1px solid rgba(255, 255, 255, 0.2)",
                color: "#f8fafc",
                fontWeight: 600,
                fontSize: "13px",
                cursor: "pointer",
              }}
            >
              <Plus size={15} /> Add Another Workshop Date
            </button>
          </div>
        )}
      </div>

      {/* Explicit Date Selection Modal */}
      {showAddDateModal && (
        <div className="admin-modal-backdrop" onClick={() => setShowAddDateModal(false)}>
          <div
            className="admin-modal-card"
            style={{ maxWidth: "440px", padding: "24px", background: "#ffffff", border: "1px solid #e2e8f0" }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px" }}>
              <h3 style={{ margin: 0, fontSize: "1.15rem", color: "#10203a", display: "flex", alignItems: "center", gap: "8px" }}>
                <Calendar size={18} style={{ color: "#FF5500" }} /> Add Workshop Date
              </h3>
              <button
                type="button"
                onClick={() => setShowAddDateModal(false)}
                style={{
                  background: "transparent",
                  border: "none",
                  color: "#64748b",
                  cursor: "pointer",
                  padding: "4px",
                  display: "inline-flex",
                  alignItems: "center",
                }}
              >
                <X size={18} />
              </button>
            </div>

            <p style={{ color: "#64748b", fontSize: "0.875rem", lineHeight: "1.45", margin: "0 0 16px" }}>
              Explicitly choose the calendar date for this session group. You can schedule single-day, consecutive, or non-consecutive dates.
            </p>

            <div style={{ marginBottom: "16px" }}>
              <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, color: "#334155", marginBottom: "6px" }}>
                Workshop Date <span style={{ color: "#ef4444" }}>*</span>
              </label>
              <input
                type="date"
                className="form-control"
                value={selectedDateInput}
                min={new Date().toISOString().split("T")[0]}
                onChange={(e) => {
                  setSelectedDateInput(e.target.value);
                  setDateModalError("");
                }}
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  borderRadius: "8px",
                  background: "#ffffff",
                  border: dateModalError ? "1px solid #ef4444" : "1px solid #cbd5e1",
                  color: "#0f172a",
                  fontSize: "14px",
                  outline: "none",
                }}
              />
              {selectedDateInput && (
                <div style={{ marginTop: "6px", fontSize: "0.8rem", color: "#0284c7" }}>
                  Selected: {(() => {
                    try {
                      return new Date(`${selectedDateInput}T00:00:00`).toLocaleDateString("en-IN", {
                        weekday: "long",
                        day: "numeric",
                        month: "long",
                        year: "numeric",
                      });
                    } catch {
                      return selectedDateInput;
                    }
                  })()}
                </div>
              )}
              {dateModalError && (
                <div style={{ marginTop: "6px", fontSize: "0.8rem", color: "#ef4444", display: "flex", alignItems: "center", gap: "4px" }}>
                  <AlertCircle size={14} /> {dateModalError}
                </div>
              )}
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px", marginTop: "20px" }}>
              <button
                type="button"
                onClick={() => setShowAddDateModal(false)}
                style={{
                  padding: "8px 16px",
                  fontSize: "13px",
                  fontWeight: 600,
                  borderRadius: "8px",
                  background: "#ffffff",
                  border: "1.5px solid #cbd5e1",
                  color: "#475569",
                  cursor: "pointer",
                }}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-primary"
                onClick={handleConfirmAddDate}
                style={{
                  padding: "8px 20px",
                  fontSize: "13px",
                  fontWeight: 600,
                  borderRadius: "8px",
                  background: "#FF5500",
                  color: "#fff",
                  border: "none",
                  cursor: "pointer",
                }}
              >
                Continue & Add Date
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Confirmation Modal for Session Deletion */}
      {deleteConfirmSessionIdx !== null && (() => {
        const targetSession = sessions[deleteConfirmSessionIdx];
        return (
          <div
            style={{
              position: "fixed",
              inset: 0,
              background: "rgba(15, 23, 42, 0.65)",
              backdropFilter: "blur(6px)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              zIndex: 10000,
              padding: "20px",
            }}
            onClick={() => setDeleteConfirmSessionIdx(null)}
          >
            <div
              style={{
                maxWidth: "440px",
                width: "100%",
                background: "#ffffff",
                borderRadius: "20px",
                padding: "28px 26px",
                boxShadow: "0 25px 50px -12px rgba(0, 0, 0, 0.25), 0 0 1px rgba(0, 0, 0, 0.1)",
                border: "1px solid #f1f5f9",
                textAlign: "left",
              }}
              onClick={(e) => e.stopPropagation()}
            >
              <div
                style={{
                  width: 48,
                  height: 48,
                  borderRadius: "50%",
                  background: "rgba(239, 68, 68, 0.1)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  color: "#ef4444",
                  marginBottom: 16,
                }}
              >
                <Trash2 size={24} />
              </div>

              <h3 style={{ margin: "0 0 8px", fontSize: "18px", fontWeight: 700, color: "#0f172a" }}>
                Delete this session?
              </h3>
              <p style={{ color: "#64748b", fontSize: "14px", lineHeight: "1.5", margin: "0 0 18px" }}>
                Deleting this session will remove it from the schedule and recalculate your overall workshop timetable. This action cannot be undone.
              </p>

              {targetSession && (
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "12px",
                    background: "#f8fafc",
                    border: "1px solid #e2e8f0",
                    borderRadius: "12px",
                    padding: "12px 14px",
                    marginBottom: "24px",
                  }}
                >
                  <Calendar size={18} color="#64748b" style={{ flexShrink: 0 }} />
                  <div>
                    <div style={{ fontSize: "13px", fontWeight: 600, color: "#0f172a" }}>
                      {targetSession.title || `Session ${deleteConfirmSessionIdx + 1}`}
                    </div>
                    <div style={{ fontSize: "12px", color: "#64748b", marginTop: "2px" }}>
                      {targetSession.sessionDate || "Date not set"} • {targetSession.startTime || "10:00"} - {targetSession.endTime || "11:30"}
                    </div>
                  </div>
                </div>
              )}

              <div style={{ display: "flex", justifyContent: "flex-end", gap: "12px" }}>
                <button
                  type="button"
                  onClick={() => setDeleteConfirmSessionIdx(null)}
                  style={{
                    padding: "10px 18px",
                    background: "#ffffff",
                    border: "1.5px solid #cbd5e1",
                    borderRadius: "10px",
                    color: "#475569",
                    fontWeight: 600,
                    fontSize: "14px",
                    cursor: "pointer",
                    transition: "all 0.15s ease",
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.background = "#f8fafc";
                    e.currentTarget.style.borderColor = "#94a3b8";
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = "#ffffff";
                    e.currentTarget.style.borderColor = "#cbd5e1";
                  }}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={() => {
                    handleRemoveSession(deleteConfirmSessionIdx);
                    setDeleteConfirmSessionIdx(null);
                  }}
                  style={{
                    padding: "10px 20px",
                    background: "#ef4444",
                    border: "none",
                    borderRadius: "10px",
                    color: "#ffffff",
                    fontWeight: 600,
                    fontSize: "14px",
                    cursor: "pointer",
                    display: "flex",
                    alignItems: "center",
                    gap: "8px",
                    boxShadow: "0 4px 12px rgba(239, 68, 68, 0.25)",
                    transition: "all 0.15s ease",
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.background = "#dc2626";
                    e.currentTarget.style.transform = "translateY(-1px)";
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.background = "#ef4444";
                    e.currentTarget.style.transform = "none";
                  }}
                >
                  <Trash2 size={16} />
                  Continue & Delete
                </button>
              </div>
            </div>
          </div>
        );
      })()}

      {/* 3:4 Portrait Session Poster Cropper */}
      <ImageCropperModal
        isOpen={cropperSessionIdx !== null}
        aspectRatio="3:4"
        allowedRatios={["3:4"]}
        targetWidth={900}
        targetHeight={1200}
        title="Crop Session Poster (3:4 Portrait)"
        initialImage={
          cropperSessionIdx !== null
            ? (sessions[cropperSessionIdx]?.posterPreviewUrl || sessions[cropperSessionIdx]?.posterImageUrl || null)
            : null
        }
        onCrop={(blob, dataUrl) => {
          if (cropperSessionIdx !== null) {
            handleUpdateSession(cropperSessionIdx, "posterBlob", blob);
            handleUpdateSession(cropperSessionIdx, "posterPreviewUrl", dataUrl);
          }
        }}
        onClose={() => setCropperSessionIdx(null)}
      />
    </div>
  );
}
