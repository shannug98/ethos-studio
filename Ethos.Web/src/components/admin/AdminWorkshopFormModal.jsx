import React, { useState, useEffect, useRef } from "react";
import "./AdminWorkshopFormModal.css";
import { adminApi } from "../../services/adminApi";
import { Upload, Loader2, Trash2, Image as ImageIcon } from "lucide-react";

const DANCE_STYLE_PRESETS = [
  "Hip Hop",
  "Urban Choreography",
  "Contemporary",
  "Commercial",
  "Jazz",
  "Ballet",
  "Salsa",
  "Bachata",
  "Afro",
  "Dancehall",
  "Bollywood",
  "Heels",
  "Waacking",
  "Krump",
  "Fusion",
];

const LEVEL_PRESETS = ["Open Level", "Beginner", "Intermediate", "Advanced"];

export default function AdminWorkshopFormModal({
  isOpen,
  workshop = null, // null for create, object for edit
  onClose,
  onSaved,
}) {
  const isEdit = Boolean(workshop?.id);

  const [form, setForm] = useState({
    title: "",
    description: "",
    danceStyle: "Urban Choreography",
    customStyle: "",
    level: "Open Level",
    workshopDate: "",
    startTime: "18:00",
    endTime: "19:30",
    venue: "Main Studio A",
    price: 500,
    capacity: 25,
    trainerProfileId: "",
    imageUrl: "",
    status: 3, // 3: Approved, 1: Draft
    allowReEntry: true,
    requireReEntryVerification: false,
    reEntryCooldownMinutes: "",
  });

  const [trainers, setTrainers] = useState([]);
  const [loadingTrainers, setLoadingTrainers] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  // Device upload state
  const fileInputRef = useRef(null);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [imageUploadError, setImageUploadError] = useState("");
  const [showUrlInput, setShowUrlInput] = useState(false);

  const handleDeviceFileSelect = async (file) => {
    if (!file) return;
    if (!file.type.startsWith("image/")) {
      setImageUploadError("Please select a valid image file (JPEG, PNG, WebP).");
      return;
    }
    if (file.size > 35 * 1024 * 1024) {
      setImageUploadError("File size exceeds 35 MB limit.");
      return;
    }

    setUploadingImage(true);
    setImageUploadError("");

    try {
      const fd = new FormData();
      fd.append("file", file);
      fd.append("section", "Workshop");
      fd.append("title", `${form.title || "Workshop"} Cover Image`);
      fd.append("mediaType", "Image");
      fd.append("isPublished", "true");

      const res = await adminApi.uploadMedia(fd);
      const url = res?.publicUrl || res?.PublicUrl || res?.r2Url || res?.R2Url || res?.mediaItem?.r2Url;
      if (url) {
        setForm((prev) => ({ ...prev, imageUrl: url }));
      } else {
        throw new Error("No URL returned from upload");
      }
    } catch (err) {
      console.error("Workshop image upload failed:", err);
      setImageUploadError(err.message || "Failed to upload image from device.");
    } finally {
      setUploadingImage(false);
    }
  };

  // Load trainers for trainer dropdown
  useEffect(() => {
    if (!isOpen) return;
    let mounted = true;
    async function loadTrainers() {
      setLoadingTrainers(true);
      try {
        const res = await adminApi.getTrainers("pageSize=100");
        const list = res?.items || res || [];
        if (mounted) setTrainers(list);
      } catch (err) {
        console.warn("Could not load trainers list:", err);
      } finally {
        if (mounted) setLoadingTrainers(false);
      }
    }
    loadTrainers();
    return () => {
      mounted = false;
    };
  }, [isOpen]);

  // Pre-fill form when editing or initialize when creating
  useEffect(() => {
    if (!isOpen) return;
    setError("");

    if (workshop) {
      const isPresetStyle = DANCE_STYLE_PRESETS.includes(workshop.danceStyle);
      setForm({
        title: workshop.title || "",
        description: workshop.description || "",
        danceStyle: isPresetStyle ? workshop.danceStyle : "Other",
        customStyle: isPresetStyle ? "" : workshop.danceStyle || "",
        level: workshop.level || "Open Level",
        workshopDate: workshop.workshopDate ? workshop.workshopDate.slice(0, 10) : "",
        startTime: workshop.startTime ? workshop.startTime.slice(0, 5) : "18:00",
        endTime: workshop.endTime ? workshop.endTime.slice(0, 5) : "19:30",
        venue: workshop.venue || "Main Studio A",
        price: workshop.price ?? 500,
        capacity: workshop.capacity ?? 25,
        trainerProfileId: workshop.trainerProfileId || "",
        imageUrl: workshop.imageUrl || "",
        status:
          workshop.status === "Approved"
            ? 3
            : workshop.status === "Draft"
            ? 1
            : workshop.status === "Completed"
            ? 6
            : workshop.status === "Cancelled"
            ? 5
            : 3,
        allowReEntry: workshop.allowReEntry !== false,
        requireReEntryVerification: Boolean(workshop.requireReEntryVerification),
        reEntryCooldownMinutes: workshop.reEntryCooldown ? String(workshop.reEntryCooldown) : "",
      });
    } else {
      // Default creation state
      const tomorrow = new Date();
      tomorrow.setDate(tomorrow.getDate() + 1);
      const tomorrowStr = tomorrow.toISOString().slice(0, 10);

      setForm({
        title: "",
        description: "",
        danceStyle: "Urban Choreography",
        customStyle: "",
        level: "Open Level",
        workshopDate: tomorrowStr,
        startTime: "18:00",
        endTime: "19:30",
        venue: "Main Studio A",
        price: 500,
        capacity: 25,
        trainerProfileId: "",
        imageUrl: "",
        status: 3,
        allowReEntry: true,
        requireReEntryVerification: false,
        reEntryCooldownMinutes: "",
      });
    }
  }, [isOpen, workshop]);

  // Calculate duration in minutes
  const calculateDuration = () => {
    if (!form.startTime || !form.endTime) return null;
    const [sh, sm] = form.startTime.split(":").map(Number);
    const [eh, em] = form.endTime.split(":").map(Number);
    const totalStart = sh * 60 + sm;
    const totalEnd = eh * 60 + em;
    if (totalEnd <= totalStart) return null;
    return totalEnd - totalStart;
  };

  const durationMinutes = calculateDuration();

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setForm((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    // Validations
    if (!form.title.trim()) {
      setError("Workshop title is required.");
      return;
    }

    const effectiveStyle =
      form.danceStyle === "Other" ? form.customStyle.trim() : form.danceStyle;
    if (!effectiveStyle) {
      setError("Please specify a dance style.");
      return;
    }

    if (!form.workshopDate) {
      setError("Workshop date is required.");
      return;
    }

    if (!form.startTime || !form.endTime) {
      setError("Start and end times are required.");
      return;
    }

    if (!durationMinutes || durationMinutes <= 0) {
      setError("End time must be after start time.");
      return;
    }

    if (!form.venue.trim()) {
      setError("Venue or studio room is required.");
      return;
    }

    const priceNum = Number(form.price);
    if (isNaN(priceNum) || priceNum < 0) {
      setError("Price must be a valid positive amount or 0.");
      return;
    }

    const capNum = Number(form.capacity);
    if (isNaN(capNum) || capNum <= 0) {
      setError("Capacity must be at least 1 seat.");
      return;
    }

    setSaving(true);
    try {
      const payload = {
        title: form.title.trim(),
        description: form.description.trim() || null,
        danceStyle: effectiveStyle,
        level: form.level,
        workshopDate: form.workshopDate,
        startTime: `${form.startTime}:00`,
        endTime: `${form.endTime}:00`,
        venue: form.venue.trim(),
        price: priceNum,
        capacity: capNum,
        trainerProfileId: form.trainerProfileId || null,
        imageUrl: form.imageUrl.trim() || null,
        status: Number(form.status),
        allowReEntry: form.allowReEntry,
        requireReEntryVerification: form.requireReEntryVerification,
        reEntryCooldown: null,
      };

      if (isEdit) {
        await adminApi.updateWorkshop(workshop.id, payload);
      } else {
        await adminApi.createWorkshop(payload);
      }

      if (onSaved) onSaved();
      onClose();
    } catch (err) {
      setError(err.message || "Failed to save workshop.");
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="workshop-form-backdrop" onClick={onClose}>
      <div className="workshop-form-modal" onClick={(e) => e.stopPropagation()}>
        {/* HEADER */}
        <div className="workshop-form-header">
          <div>
            <h3>{isEdit ? "Edit Workshop Details" : "Create Masterclass Workshop"}</h3>
            <p>
              {isEdit
                ? "Update workshop schedule, pricing, capacity, and studio logistics."
                : "Schedule an immediately approved and published workshop on ETHOS."}
            </p>
          </div>
          <button className="workshop-form-close-btn" onClick={onClose}>
            ✕
          </button>
        </div>

        {/* BODY */}
        <form onSubmit={handleSubmit}>
          <div className="workshop-form-body">
            {error && <div className="workshop-form-error">⚠️ {error}</div>}

            <div className="workshop-form-grid">
              {/* Title */}
              <div className="workshop-form-field full-width">
                <label>
                  Workshop Title <span style={{ color: "#dc2626" }}>*</span>
                </label>
                <input
                  type="text"
                  name="title"
                  value={form.title}
                  onChange={handleChange}
                  placeholder="e.g. Masterclass: Urban Grooves & Body Control"
                  required
                  autoFocus
                />
              </div>

              {/* Description */}
              <div className="workshop-form-field full-width">
                <label>Description & Syllabus</label>
                <textarea
                  name="description"
                  value={form.description}
                  onChange={handleChange}
                  placeholder="Comprehensive description of the workshop choreography, techniques covered, and participant takeaways..."
                  rows={3}
                />
              </div>

              {/* Dance Style */}
              <div className="workshop-form-field">
                <label>
                  Dance Style / Category <span style={{ color: "#dc2626" }}>*</span>
                </label>
                <select name="danceStyle" value={form.danceStyle} onChange={handleChange}>
                  {DANCE_STYLE_PRESETS.map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                  <option value="Other">Other (Custom Style)</option>
                </select>
                {form.danceStyle === "Other" && (
                  <input
                    type="text"
                    name="customStyle"
                    value={form.customStyle}
                    onChange={handleChange}
                    placeholder="Enter custom dance style"
                    style={{ marginTop: "6px" }}
                  />
                )}
              </div>

              {/* Level */}
              <div className="workshop-form-field">
                <label>
                  Skill Level <span style={{ color: "#dc2626" }}>*</span>
                </label>
                <select name="level" value={form.level} onChange={handleChange}>
                  {LEVEL_PRESETS.map((l) => (
                    <option key={l} value={l}>
                      {l}
                    </option>
                  ))}
                </select>
              </div>

              {/* Workshop Date */}
              <div className="workshop-form-field">
                <label>
                  Workshop Date <span style={{ color: "#dc2626" }}>*</span>
                </label>
                <input
                  type="date"
                  name="workshopDate"
                  value={form.workshopDate}
                  onChange={handleChange}
                  required
                />
              </div>

              {/* Venue */}
              <div className="workshop-form-field">
                <label>
                  Venue / Studio Room <span style={{ color: "#dc2626" }}>*</span>
                </label>
                <input
                  type="text"
                  name="venue"
                  value={form.venue}
                  onChange={handleChange}
                  placeholder="e.g. Main Studio A, Studio B, Auditorium"
                  required
                />
              </div>

              {/* Start Time */}
              <div className="workshop-form-field">
                <label>
                  Start Time <span style={{ color: "#dc2626" }}>*</span>
                </label>
                <input
                  type="time"
                  name="startTime"
                  value={form.startTime}
                  onChange={handleChange}
                  required
                />
              </div>

              {/* End Time */}
              <div className="workshop-form-field">
                <label>
                  <span>
                    End Time <span style={{ color: "#dc2626" }}>*</span>
                  </span>
                  {durationMinutes && durationMinutes > 0 && (
                    <span className="duration-hint">Duration: {durationMinutes} mins</span>
                  )}
                </label>
                <input
                  type="time"
                  name="endTime"
                  value={form.endTime}
                  onChange={handleChange}
                  required
                />
              </div>

              {/* Price */}
              <div className="workshop-form-field">
                <label>
                  Ticket Price (INR) <span style={{ color: "#dc2626" }}>*</span>
                </label>
                <input
                  type="number"
                  name="price"
                  value={form.price}
                  onChange={handleChange}
                  min={0}
                  step={10}
                  required
                />
              </div>

              {/* Capacity */}
              <div className="workshop-form-field">
                <label>
                  Capacity (Max Seats) <span style={{ color: "#dc2626" }}>*</span>
                </label>
                <input
                  type="number"
                  name="capacity"
                  value={form.capacity}
                  onChange={handleChange}
                  min={1}
                  required
                />
              </div>

              {/* Trainer Assignment */}
              <div className="workshop-form-field">
                <label>Assigned Instructor / Trainer</label>
                <select
                  name="trainerProfileId"
                  value={form.trainerProfileId}
                  onChange={handleChange}
                  disabled={loadingTrainers}
                >
                  <option value="">No Instructor Assigned (Studio Directed)</option>
                  {trainers.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.fullName || t.name || t.trainerCode} ({t.trainerCode || "Trainer"})
                    </option>
                  ))}
                </select>
              </div>

              {/* Status */}
              <div className="workshop-form-field">
                <label>Publication Status</label>
                <select name="status" value={form.status} onChange={handleChange}>
                  <option value={3}>Approved / Published (Open for Bookings)</option>
                  <option value={1}>Draft (Hidden from Students)</option>
                  {isEdit && <option value={6}>Completed</option>}
                  {isEdit && <option value={5}>Cancelled</option>}
                </select>
              </div>

              {/* Workshop Cover Image */}
              <div className="workshop-form-field full-width">
                <label>Workshop Cover Image</label>

                {form.imageUrl ? (
                  <div className="workshop-image-preview-box">
                    <img
                      src={form.imageUrl}
                      alt="Workshop Cover"
                      className="workshop-image-preview-thumb"
                      onError={(e) => {
                        e.target.style.display = "none";
                      }}
                    />
                    <div className="workshop-image-preview-details">
                      <span className="image-active-badge">✓ Cover Image Active</span>
                      <div className="workshop-image-actions">
                        <input
                          type="file"
                          ref={fileInputRef}
                          accept="image/jpeg,image/png,image/webp"
                          style={{ display: "none" }}
                          onChange={(e) => {
                            if (e.target.files && e.target.files[0]) {
                              handleDeviceFileSelect(e.target.files[0]);
                            }
                          }}
                        />
                        <button
                          type="button"
                          className="btn-ws-image-action"
                          onClick={() => fileInputRef.current?.click()}
                          disabled={uploadingImage}
                        >
                          {uploadingImage ? <Loader2 size={13} className="animate-spin" /> : <Upload size={13} />}
                          <span>Change Image</span>
                        </button>
                        <button
                          type="button"
                          className="btn-ws-image-action delete"
                          onClick={() => setForm((prev) => ({ ...prev, imageUrl: "" }))}
                          disabled={uploadingImage}
                        >
                          <Trash2 size={13} />
                          <span>Remove</span>
                        </button>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="workshop-image-upload-drop">
                    <input
                      type="file"
                      ref={fileInputRef}
                      accept="image/jpeg,image/png,image/webp"
                      style={{ display: "none" }}
                      onChange={(e) => {
                        if (e.target.files && e.target.files[0]) {
                          handleDeviceFileSelect(e.target.files[0]);
                        }
                      }}
                    />

                    {uploadingImage ? (
                      <div className="workshop-image-uploading">
                        <Loader2 size={18} className="animate-spin" />
                        <span>Uploading image from device to Cloudflare R2...</span>
                      </div>
                    ) : (
                      <div
                        className="workshop-dropzone-panel"
                        onClick={() => fileInputRef.current?.click()}
                      >
                        <div className="workshop-dropzone-icon">
                          <Upload size={20} />
                        </div>
                        <div className="workshop-dropzone-info">
                          <div className="workshop-dropzone-title">Upload Cover Image from Device</div>
                          <div className="workshop-dropzone-sub">Supports JPEG, PNG, WebP up to 35 MB (Saved directly to R2)</div>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {imageUploadError && (
                  <div style={{ color: "#dc2626", fontSize: "12.5px", marginTop: "6px" }}>
                    ⚠️ {imageUploadError}
                  </div>
                )}

                {/* Optional manual URL override */}
                <div style={{ marginTop: "6px" }}>
                  {!showUrlInput ? (
                    <button
                      type="button"
                      style={{
                        background: "none",
                        border: "none",
                        color: "#64748b",
                        fontSize: "12px",
                        cursor: "pointer",
                        textDecoration: "underline",
                        padding: 0,
                      }}
                      onClick={() => setShowUrlInput(true)}
                    >
                      or enter image URL manually
                    </button>
                  ) : (
                    <div style={{ marginTop: "6px" }}>
                      <input
                        type="url"
                        name="imageUrl"
                        value={form.imageUrl}
                        onChange={handleChange}
                        placeholder="https://images.unsplash.com/... or https://..."
                        style={{ fontSize: "13px" }}
                      />
                    </div>
                  )}
                </div>
              </div>

              {/* Re-Entry Settings */}
              <div className="workshop-form-field full-width">
                <div className="workshop-form-section-title">Re-Entry & Access Controls</div>
                <div style={{ display: "flex", gap: "24px", marginTop: "8px", flexWrap: "wrap" }}>
                  <label style={{ display: "flex", alignItems: "center", gap: "8px", cursor: "pointer", fontWeight: 500 }}>
                    <input
                      type="checkbox"
                      name="allowReEntry"
                      checked={form.allowReEntry}
                      onChange={handleChange}
                      style={{ width: "auto" }}
                    />
                    Allow Attendee Re-Entry
                  </label>

                  <label style={{ display: "flex", alignItems: "center", gap: "8px", cursor: "pointer", fontWeight: 500 }}>
                    <input
                      type="checkbox"
                      name="requireReEntryVerification"
                      checked={form.requireReEntryVerification}
                      onChange={handleChange}
                      style={{ width: "auto" }}
                    />
                    Require QR Verification on Re-Entry
                  </label>
                </div>
              </div>
            </div>
          </div>

          {/* FOOTER */}
          <div className="workshop-form-footer">
            <button
              type="button"
              className="btn-form-cancel"
              onClick={onClose}
              disabled={saving}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn-form-submit"
              disabled={saving}
            >
              {saving ? "Saving Workshop..." : isEdit ? "Update Workshop" : "+ Create & Publish"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
