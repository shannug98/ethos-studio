import React, { useState, useEffect, useRef } from "react";
import { Search, MapPin, Calendar, Clock, Globe, Navigation, Edit3, CheckCircle2 } from "lucide-react";
import { adminApi } from "../../../../services/adminApi";

export default function Step3VenueSchedule({ form, onChange, errors }) {
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [manualVenueMode, setManualVenueMode] = useState(false);

  const searchBoxRef = useRef(null);

  // Debounced search when user types >= 3 characters
  useEffect(() => {
    if (searchQuery.trim().length < 3) {
      setSearchResults([]);
      setShowDropdown(false);
      return;
    }

    let isCancelled = false;
    const timer = setTimeout(async () => {
      setSearching(true);
      try {
        const results = await adminApi.searchVenues(searchQuery);
        if (!isCancelled) {
          setSearchResults(Array.isArray(results) ? results : []);
          setShowDropdown(true);
        }
      } catch (err) {
        console.warn("Venue search failed:", err);
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

  const handleSelectVenue = (item) => {
    onChange("venue", item.mainText || item.name || "");
    onChange("venueAddress", item.fullAddress || item.address || "");
    if (item.city) onChange("city", item.city);
    if (item.area) onChange("area", item.area);
    if (item.placeId) onChange("googlePlaceId", item.placeId);
    if (item.latitude != null) onChange("latitude", item.latitude);
    if (item.longitude != null) onChange("longitude", item.longitude);

    setSearchQuery("");
    setShowDropdown(false);
  };

  // Calculate schedule duration
  const getDurationText = () => {
    if (!form.startTime || !form.endTime) return "—";
    try {
      const [sh, sm] = form.startTime.split(":").map(Number);
      const [eh, em] = form.endTime.split(":").map(Number);
      const startMinutes = sh * 60 + sm;
      const endMinutes = eh * 60 + em;
      const diff = endMinutes - startMinutes;
      if (diff <= 0) return "Invalid timeframe";
      const hours = Math.floor(diff / 60);
      const minutes = diff % 60;
      if (hours === 0) return `${minutes} minutes`;
      if (minutes === 0) return `${hours} hour${hours > 1 ? "s" : ""}`;
      return `${hours}h ${minutes}m`;
    } catch {
      return "—";
    }
  };

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Venue & Schedule</h2>
        <p className="wizard-section-desc">
          Pinpoint the workshop location via Google Places or studio database, and establish the session date and timing in Asia/Kolkata timezone.
        </p>
      </div>

      {/* Venue Autocomplete Search */}
      <div className="wizard-form-group" ref={searchBoxRef}>
        <div className="wizard-label-row">
          <label className="wizard-label">
            Venue Search (Google Places & Ethos Studios)
          </label>
          <button
            type="button"
            className="wizard-link-btn"
            onClick={() => setManualVenueMode(!manualVenueMode)}
          >
            <Edit3 size={13} />
            <span>{manualVenueMode ? "Use Autocomplete" : "Enter Venue Manually"}</span>
          </button>
        </div>

        {!manualVenueMode && (
          <div className="wizard-search-box-wrap">
            <div className="wizard-search-input-box">
              <Search size={16} className="wizard-input-icon" />
              <input
                type="text"
                className="wizard-input search-input"
                placeholder="Type 3+ characters (e.g. Ethos Main Studio, Jubilee Hills, Hyderabad)..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                onFocus={() => {
                  if (searchResults.length > 0) setShowDropdown(true);
                }}
              />
              {searching && <span className="search-spinner">Searching...</span>}
            </div>

            {/* Dropdown results */}
            {showDropdown && (
              <div className="venue-autocomplete-dropdown">
                {searchResults.length > 0 ? (
                  searchResults.map((item, idx) => (
                    <div
                      key={item.placeId || idx}
                      className="venue-result-item"
                      onClick={() => handleSelectVenue(item)}
                    >
                      <MapPin size={16} className="venue-item-icon" />
                      <div className="venue-item-texts">
                        <span className="venue-item-title">{item.mainText}</span>
                        <span className="venue-item-sub">
                          {item.fullAddress || item.secondaryText}
                        </span>
                      </div>
                    </div>
                  ))
                ) : (
                  <div className="venue-empty-search">
                    <span>No venues found. Try entering manually or refining keywords.</span>
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </div>

      {/* Selected / Form Venue Details */}
      <div className="venue-selected-card">
        <div className="venue-card-header">
          <div className="venue-card-icon-wrap">
            <MapPin size={18} className="venue-pin-icon" />
          </div>
          <div className="venue-card-title-group">
            <h4 className="venue-card-title">
              {form.venue ? form.venue : "No venue specified"}
            </h4>
            <span className="venue-card-address">
              {form.venueAddress || "Select from search above or type in fields below"}
            </span>
          </div>
        </div>

        <div className="wizard-form-grid-2">
          <div className="wizard-form-group">
            <label className="wizard-label">
              Venue Name <span className="req">*</span>
            </label>
            <input
              type="text"
              className={`wizard-input ${errors.venue ? "has-error" : ""}`}
              placeholder="e.g. Ethos Main Studio A"
              value={form.venue}
              onChange={(e) => onChange("venue", e.target.value)}
            />
            {errors.venue && <span className="wizard-error-text">{errors.venue}</span>}
          </div>

          <div className="wizard-form-group">
            <label className="wizard-label">Detailed Street Address</label>
            <input
              type="text"
              className="wizard-input"
              placeholder="e.g. Plot 42, Road No 36, Jubilee Hills"
              value={form.venueAddress}
              onChange={(e) => onChange("venueAddress", e.target.value)}
            />
          </div>
        </div>
      </div>

      {/* Schedule: Date, Start Time, End Time */}
      <div className="wizard-schedule-section">
        <h3 className="wizard-subsection-title">Session Schedule</h3>

        <div className="wizard-form-grid-3">
          <div className="wizard-form-group">
            <label className="wizard-label">
              Date <span className="req">*</span>
            </label>
            <div className="wizard-input-wrap">
              <Calendar size={16} className="wizard-input-icon" />
              <input
                type="date"
                className={`wizard-input ${errors.workshopDate ? "has-error" : ""}`}
                value={form.workshopDate}
                min={new Date().toISOString().slice(0, 10)}
                onChange={(e) => onChange("workshopDate", e.target.value)}
              />
            </div>
            {errors.workshopDate && (
              <span className="wizard-error-text">{errors.workshopDate}</span>
            )}
          </div>

          <div className="wizard-form-group">
            <label className="wizard-label">
              Start Time (IST) <span className="req">*</span>
            </label>
            <div className="wizard-input-wrap">
              <Clock size={16} className="wizard-input-icon" />
              <input
                type="time"
                className={`wizard-input ${errors.startTime ? "has-error" : ""}`}
                value={form.startTime}
                onChange={(e) => onChange("startTime", e.target.value)}
              />
            </div>
            {errors.startTime && (
              <span className="wizard-error-text">{errors.startTime}</span>
            )}
          </div>

          <div className="wizard-form-group">
            <label className="wizard-label">
              End Time (IST) <span className="req">*</span>
            </label>
            <div className="wizard-input-wrap">
              <Clock size={16} className="wizard-input-icon" />
              <input
                type="time"
                className={`wizard-input ${errors.endTime ? "has-error" : ""}`}
                value={form.endTime}
                onChange={(e) => onChange("endTime", e.target.value)}
              />
            </div>
            {errors.endTime && (
              <span className="wizard-error-text">{errors.endTime}</span>
            )}
          </div>
        </div>

        {/* Schedule Summary Banner */}
        <div className="wizard-schedule-badge-bar">
          <div className="schedule-meta-item">
            <span className="meta-lbl">Duration:</span>
            <span className="meta-val highlight">{getDurationText()}</span>
          </div>
          <div className="schedule-meta-item">
            <Globe size={14} className="meta-icon" />
            <span className="meta-lbl">Timezone:</span>
            <span className="meta-val">Asia/Kolkata (IST • UTC+05:30)</span>
          </div>
        </div>
      </div>
    </div>
  );
}
