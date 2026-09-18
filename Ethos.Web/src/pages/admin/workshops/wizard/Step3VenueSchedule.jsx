import React, { useState, useEffect, useRef } from "react";
import { Search, MapPin, Calendar, Clock, Globe, Navigation, Edit3, CheckCircle2, AlertCircle, ExternalLink, RefreshCw } from "lucide-react";
import { adminApi } from "../../../../services/adminApi";

export default function Step3VenueSchedule({ form, onChange, errors }) {
  const [searchQuery, setSearchQuery] = useState("");
  const [ethosVenues, setEthosVenues] = useState([]);
  const [googlePlaces, setGooglePlaces] = useState([]);
  const [googleAvailable, setGoogleAvailable] = useState(true);
  const [notice, setNotice] = useState("");
  const [searching, setSearching] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [manualVenueMode, setManualVenueMode] = useState(false);

  const searchBoxRef = useRef(null);

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
        const res = await adminApi.searchVenues(searchQuery);
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

    setSearchQuery("");
    setShowDropdown(false);
    setManualVenueMode(false);

    // If coordinates or city are missing, try fetching details
    if (item.placeId && (!item.city || item.latitude == null)) {
      try {
        const details = await adminApi.getPlaceDetails(item.placeId);
        if (details) {
          if (details.city) onChange("city", details.city);
          if (details.area) onChange("area", details.area);
          if (details.latitude != null) onChange("latitude", details.latitude);
          if (details.longitude != null) onChange("longitude", details.longitude);
          if (details.fullAddress) onChange("venueAddress", details.fullAddress);
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
    setSearchQuery("");
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
      if (hours === 0) return minutes + " minutes";
      if (minutes === 0) return hours + " hr";
      return hours + " hr " + minutes + " min";
    } catch {
      return "—";
    }
  };

  const hasVenueSelected = Boolean(form.venue && form.venue.trim());
  const mapsSearchUrl = form.venueAddress || form.venue
    ? "https://www.google.com/maps/search/?api=1&query=" + encodeURIComponent((form.venue || "") + ", " + (form.venueAddress || ""))
    : null;

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Venue & Schedule</h2>
        <p className="wizard-section-desc">
          Search any venue across India, select an Ethos dance studio, or enter venue address details manually.
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
        {hasVenueSelected && !manualVenueMode ? (
          <div className="selected-venue-card">
            <div className="venue-preview-header">
              <div className="venue-preview-badge">
                <CheckCircle2 size={15} />
                <span>Selected Venue</span>
              </div>
              <div className="venue-preview-actions">
                {mapsSearchUrl && (
                  <a
                    href={mapsSearchUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="venue-map-link-btn"
                    title="View on Google Maps"
                  >
                    <ExternalLink size={13} />
                    <span>View Map</span>
                  </a>
                )}
                <button
                  type="button"
                  className="venue-change-btn"
                  onClick={() => {
                    handleClearVenue();
                    setManualVenueMode(false);
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
              {form.latitude && form.longitude && (
                <span className="venue-pill coord-pill">
                  {form.latitude.toFixed(4)}° N, {form.longitude.toFixed(4)}° E
                </span>
              )}
            </div>
          </div>
        ) : (
          <div>
            {/* Search Box */}
            <div className="venue-search-container" ref={searchBoxRef}>
              <div className="venue-search-input-wrap">
                <Search size={18} className="venue-search-icon" />
                <input
                  type="text"
                  className="venue-search-input"
                  placeholder="Search venues, studios, pubs, auditoriums, or any place in India..."
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
                  {/* Notice if Google Places unavailable */}
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
                          key={v.placeId}
                          className="venue-dropdown-item ethos-item"
                          onClick={() => handleSelectVenue(v)}
                        >
                          <div className="item-icon-col">
                            <span className="ethos-logo-badge">ETHOS</span>
                          </div>
                          <div className="item-info-col">
                            <div className="item-primary-text">{v.mainText}</div>
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
                          setManualVenueMode(true);
                          setShowDropdown(false);
                          if (!form.venue) onChange("venue", searchQuery);
                        }}
                      >
                        Enter venue details manually
                      </button>
                    </div>
                  )}

                  {/* Footer Action to Enter Manually */}
                  <div className="venue-dropdown-footer">
                    <button
                      type="button"
                      className="venue-manual-trigger-btn"
                      onClick={() => {
                        setManualVenueMode(true);
                        setShowDropdown(false);
                      }}
                    >
                      <Edit3 size={13} />
                      <span>Enter venue manually</span>
                    </button>
                  </div>
                </div>
              )}
            </div>

            {/* Quick manual toggle if not searching */}
            <div className="manual-toggle-row">
              <button
                type="button"
                className="manual-toggle-link"
                onClick={() => setManualVenueMode(!manualVenueMode)}
              >
                <Edit3 size={13} />
                <span>{manualVenueMode ? "Use location search instead" : "Can't find your venue? Enter details manually"}</span>
              </button>
            </div>
          </div>
        )}

        {/* Manual Venue Fields (Rendered if manual mode or edit requested) */}
        {manualVenueMode && (
          <div className="manual-venue-form-box">
            <h4 className="manual-form-title">Manual Venue Entry</h4>
            <div className="form-grid-2">
              <div className="form-group">
                <label className="form-label">
                  Venue Name <span className="req">*</span>
                </label>
                <input
                  type="text"
                  className={"form-input " + (errors.venue ? "input-error" : "")}
                  placeholder="e.g. Phoenix Arena, Balayogi Auditorium"
                  value={form.venue}
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
                  value={form.venueAddress}
                  onChange={(e) => onChange("venueAddress", e.target.value)}
                />
              </div>
            </div>

            <div className="form-grid-2">
              <div className="form-group">
                <label className="form-label">City</label>
                <input
                  type="text"
                  className="form-input"
                  placeholder="e.g. Hyderabad, Mumbai, Bengaluru"
                  value={form.city}
                  onChange={(e) => onChange("city", e.target.value)}
                />
              </div>

              <div className="form-group">
                <label className="form-label">Area / Locality</label>
                <input
                  type="text"
                  className="form-input"
                  placeholder="e.g. Jubilee Hills, Koramangala, Bandra"
                  value={form.area}
                  onChange={(e) => onChange("area", e.target.value)}
                />
              </div>
            </div>
          </div>
        )}

        {errors.venue && !manualVenueMode && !hasVenueSelected && (
          <span className="error-text mt-2 block">{errors.venue}</span>
        )}
      </div>

      {/* 2. SCHEDULE & TIMINGS */}
      <div className="form-card">
        <div className="form-card-header">
          <Calendar size={18} className="form-card-icon" />
          <div>
            <h3 className="form-card-title">Schedule & Timing</h3>
            <p className="form-card-subtitle">Set event date, start time, and finish time (Asia/Kolkata timezone)</p>
          </div>
        </div>

        <div className="form-grid-3">
          {/* Workshop Date */}
          <div className="form-group">
            <label className="form-label">
              Workshop Date <span className="req">*</span>
            </label>
            <div className="input-icon-wrap">
              <Calendar size={15} className="input-left-icon" />
              <input
                type="date"
                className={"form-input with-left-icon " + (errors.workshopDate ? "input-error" : "")}
                value={form.workshopDate}
                min={new Date().toISOString().split("T")[0]}
                onChange={(e) => onChange("workshopDate", e.target.value)}
              />
            </div>
            {errors.workshopDate && <span className="error-text">{errors.workshopDate}</span>}
          </div>

          {/* Start Time */}
          <div className="form-group">
            <label className="form-label">
              Start Time <span className="req">*</span>
            </label>
            <div className="input-icon-wrap">
              <Clock size={15} className="input-left-icon" />
              <input
                type="time"
                className={"form-input with-left-icon " + (errors.startTime ? "input-error" : "")}
                value={form.startTime}
                onChange={(e) => onChange("startTime", e.target.value)}
              />
            </div>
            {errors.startTime && <span className="error-text">{errors.startTime}</span>}
          </div>

          {/* End Time */}
          <div className="form-group">
            <label className="form-label">
              End Time <span className="req">*</span>
            </label>
            <div className="input-icon-wrap">
              <Clock size={15} className="input-left-icon" />
              <input
                type="time"
                className={"form-input with-left-icon " + (errors.endTime ? "input-error" : "")}
                value={form.endTime}
                onChange={(e) => onChange("endTime", e.target.value)}
              />
            </div>
            {errors.endTime && <span className="error-text">{errors.endTime}</span>}
          </div>
        </div>

        {/* Schedule Summary Banner */}
        <div className="schedule-summary-banner">
          <div className="summary-col">
            <span className="summary-label">Timezone</span>
            <span className="summary-val timezone-val">
              <Globe size={13} />
              Asia/Kolkata (IST • UTC+05:30)
            </span>
          </div>

          <div className="summary-col">
            <span className="summary-label">Session Duration</span>
            <span className="summary-val duration-val">{getDurationText()}</span>
          </div>

          <div className="summary-col">
            <span className="summary-label">Booking Cutoff</span>
            <span className="summary-val cutoff-val">Until Start Time</span>
          </div>

          <div className="summary-col">
            <span className="summary-label">QR Check-in Window</span>
            <span className="summary-val qr-window-val">Start -30m to End</span>
          </div>
        </div>
      </div>
    </div>
  );
}
