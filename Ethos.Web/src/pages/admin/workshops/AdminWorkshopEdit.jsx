import React, { useState, useRef } from "react";
import { useOutletContext, useNavigate } from "react-router-dom";
import {
  Upload,
  Image as ImageIcon,
  Link2,
  X,
  Check,
  AlertTriangle,
  Loader2,
  Trash2,
  XCircle,
} from "lucide-react";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopEdit() {
  const { workshop, reloadWorkshop } = useOutletContext();
  const navigate = useNavigate();
  const workshopId = workshop.Id || workshop.id;

  const [title, setTitle] = useState(workshop.Title || workshop.title || "");
  const [description, setDescription] = useState(workshop.Description || workshop.description || "");
  const [danceStyle, setDanceStyle] = useState(workshop.DanceStyle || workshop.danceStyle || "");
  const [level, setLevel] = useState(workshop.Level || workshop.level || "Open Level");
  const [venue, setVenue] = useState(workshop.Venue || workshop.venue || "Ethos Main Studio");
  const [price, setPrice] = useState(workshop.Price ?? workshop.price ?? 0);
  const [capacity, setCapacity] = useState(workshop.Capacity ?? workshop.capacity ?? 30);
  const [imageUrl, setImageUrl] = useState(workshop.ImageUrl || workshop.imageUrl || "");

  const [submitting, setSubmitting] = useState(false);
  const [statusMsg, setStatusMsg] = useState(null);

  // Device image upload state
  const [imageTab, setImageTab] = useState("upload"); // "upload" | "url"
  const [uploadingImage, setUploadingImage] = useState(false);
  const [imageUploadError, setImageUploadError] = useState(null);
  const [dragOver, setDragOver] = useState(false);
  const fileInputRef = useRef(null);

  // Cancellation modal state
  const [cancelModal, setCancelModal] = useState({
    open: false,
    reason: "",
    submitting: false,
    error: null,
  });

  const handleDeviceFileSelect = async (file) => {
    if (!file) return;
    if (!file.type.startsWith("image/")) {
      setImageUploadError("Please select a valid image file (JPEG, PNG, WebP).");
      return;
    }

    setUploadingImage(true);
    setImageUploadError(null);

    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("section", "Workshop");
      formData.append("title", `${title || "Workshop"} Banner`);
      formData.append("mediaType", "Image");
      formData.append("isPublished", "true");

      const res = await adminApi.uploadMedia(formData);
      const url = res?.publicUrl || res?.PublicUrl || res?.r2Url || res?.R2Url || res?.mediaItem?.r2Url || res?.data?.r2Url;
      if (url) {
        setImageUrl(url);
      } else {
        throw new Error("No URL returned from upload");
      }
    } catch (err) {
      console.error("Image upload failed:", err);
      setImageUploadError(err?.message || "Failed to upload image from device.");
    } finally {
      setUploadingImage(false);
    }
  };

  const handleDragOver = (e) => {
    e.preventDefault();
    setDragOver(true);
  };

  const handleDragLeave = () => {
    setDragOver(false);
  };

  const handleDrop = (e) => {
    e.preventDefault();
    setDragOver(false);
    const files = e.dataTransfer.files;
    if (files && files.length > 0) {
      handleDeviceFileSelect(files[0]);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    setStatusMsg(null);

    try {
      await adminApi.updateWorkshop(workshopId, {
        title,
        description,
        danceStyle,
        level,
        venue,
        price: Number(price),
        capacity: Number(capacity),
        imageUrl: imageUrl.trim() || null,
      });

      setStatusMsg({ type: "success", text: "Workshop updated successfully." });
      if (reloadWorkshop) await reloadWorkshop();
      setTimeout(() => {
        navigate(`/admin_portal/workshops/${workshopId}/overview`);
      }, 1200);
    } catch (err) {
      setStatusMsg({ type: "error", text: err?.message || "Failed to update workshop." });
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmCancelWorkshop = async () => {
    if (!cancelModal.reason.trim()) {
      setCancelModal((prev) => ({ ...prev, error: "A cancellation reason is required." }));
      return;
    }

    setCancelModal((prev) => ({ ...prev, submitting: true, error: null }));

    try {
      await adminApi.cancelWorkshop(workshopId, cancelModal.reason.trim());
      setCancelModal({ open: false, reason: "", submitting: false, error: null });
      setStatusMsg({ type: "success", text: "Workshop has been cancelled successfully." });
      if (reloadWorkshop) await reloadWorkshop();
    } catch (err) {
      setCancelModal((prev) => ({
        ...prev,
        submitting: false,
        error: err?.message || "Failed to cancel workshop.",
      }));
    }
  };

  const phase = workshop.LifecyclePhase || workshop.lifecyclePhase || "Upcoming";
  const status = workshop.Status || workshop.status || "Published";
  const attendedCount = workshop.AttendedCount ?? workshop.attendedCount ?? (workshop.RecentCheckIns?.length || workshop.recentCheckIns?.length || 0);

  const isCheckInLocked = attendedCount > 0 || (status === "Approved" && attendedCount > 0) || (phase === "Ongoing" && attendedCount > 0) || phase === "Completed";
  const isLockedOut = !isCheckInLocked && (phase === "Ongoing" || phase === "Completed");
  const isCancelled = phase === "Cancelled";
  const isCompleted = phase === "Completed";

  return (
    <div className="workshop-subpage-container">
      <div className="subpage-header">
        <div>
          <h1 className="subpage-title">Edit Workshop Details</h1>
          <p className="subpage-subtitle">Update workshop title, capacity, price, venue, and banner image.</p>
        </div>
        <button
          type="button"
          className="admin-btn-secondary"
          disabled={isCheckInLocked}
          onClick={() => !isCheckInLocked && navigate(`/admin_portal/workshops/${workshopId}/wizard`)}
          style={{ opacity: isCheckInLocked ? 0.6 : 1, cursor: isCheckInLocked ? "not-allowed" : "pointer" }}
        >
          <span>✨ Open in 5-Step Wizard</span>
        </button>
      </div>

      {isCheckInLocked && (
        <div
          className="subpage-error-banner"
          style={{
            background: "rgba(239, 68, 68, 0.12)",
            color: "#b91c1c",
            border: "1px solid rgba(239, 68, 68, 0.3)",
            padding: "16px 20px",
            borderRadius: "12px",
            marginBottom: "20px",
            display: "flex",
            alignItems: "center",
            gap: "14px"
          }}
        >
          <span style={{ fontSize: "24px" }}>🔒</span>
          <div>
            <strong style={{ fontSize: "15px", display: "block" }}>Editing Permanently Locked</strong>
            <span style={{ fontSize: "13px", opacity: 0.9 }}>
              This workshop is <strong>{status} ({phase})</strong> and attendee check-ins have already occurred ({attendedCount} attendee check-in(s) recorded). Per studio security policy, core details, venue, pricing, and capacity cannot be modified once check-in activity has started (even by administrators).
            </span>
          </div>
        </div>
      )}

      {isLockedOut && (
        <div
          className="subpage-error-banner"
          style={{
            background: "rgba(245, 158, 11, 0.12)",
            color: "#b45309",
            border: "1px solid rgba(245, 158, 11, 0.3)",
          }}
        >
          ⚠️ This workshop is currently <strong>{phase}</strong>. Schedule mutations and core details may be restricted.
        </div>
      )}

      {isCancelled && (
        <div
          className="subpage-error-banner"
          style={{
            background: "rgba(239, 68, 68, 0.12)",
            color: "#b91c1c",
            border: "1px solid rgba(239, 68, 68, 0.3)",
          }}
        >
          🚫 This workshop is <strong>Cancelled</strong>. New bookings are suspended.
        </div>
      )}

      {statusMsg && (
        <div className={`subpage-${statusMsg.type}-banner`}>{statusMsg.text}</div>
      )}

      <div className="details-card">
        <form onSubmit={handleSubmit} className="workshop-edit-form">
          <fieldset disabled={isCheckInLocked} style={{ border: "none", padding: 0, margin: 0, opacity: isCheckInLocked ? 0.75 : 1 }}>
          <div className="form-group">
            <label className="form-label">Workshop Title *</label>
            <input
              type="text"
              className="form-control"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="e.g. Contemporary Masterclass"
              required
            />
          </div>

          <div className="form-row-2col">
            <div className="form-group">
              <label className="form-label">Dance Style *</label>
              <input
                type="text"
                className="form-control"
                value={danceStyle}
                onChange={(e) => setDanceStyle(e.target.value)}
                placeholder="e.g. Contemporary, Hip-Hop, Salsa"
                required
              />
            </div>

            <div className="form-group">
              <label className="form-label">Level *</label>
              <select
                className="form-control"
                value={level}
                onChange={(e) => setLevel(e.target.value)}
              >
                <option value="Beginner">Beginner</option>
                <option value="Intermediate">Intermediate</option>
                <option value="Advanced">Advanced</option>
                <option value="Open Level">Open Level</option>
              </select>
            </div>
          </div>

          <div className="form-row-2col">
            <div className="form-group">
              <label className="form-label">Price (₹) *</label>
              <input
                type="number"
                min="0"
                className="form-control"
                value={price}
                onChange={(e) => setPrice(e.target.value)}
                required
              />
            </div>

            <div className="form-group">
              <label className="form-label">Capacity (Max Seats) *</label>
              <input
                type="number"
                min="1"
                className="form-control"
                value={capacity}
                onChange={(e) => setCapacity(e.target.value)}
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label className="form-label">Venue / Studio Location *</label>
            <input
              type="text"
              className="form-control"
              value={venue}
              onChange={(e) => setVenue(e.target.value)}
              placeholder="e.g. Ethos Main Studio, Jubilee Hills"
              required
            />
          </div>

          {/* Cover Image Upload (Device + URL) */}
          <div className="form-group">
            <label className="form-label">
              <span>Cover Banner Image</span>
              {imageUrl && <span style={{ color: "#16a34a", fontSize: "12px" }}>✓ Image active</span>}
            </label>

            <div className="image-upload-wrapper">
              <div className="image-upload-tabs">
                <button
                  type="button"
                  className={`image-upload-tab ${imageTab === "upload" ? "active" : ""}`}
                  onClick={() => setImageTab("upload")}
                >
                  <Upload size={14} />
                  <span>Upload from Device</span>
                </button>
                <button
                  type="button"
                  className={`image-upload-tab ${imageTab === "url" ? "active" : ""}`}
                  onClick={() => setImageTab("url")}
                >
                  <Link2 size={14} />
                  <span>Enter Image URL</span>
                </button>
              </div>

              {imageTab === "upload" ? (
                <div>
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
                    <div className="uploading-spinner-row">
                      <Loader2 size={18} className="animate-spin" />
                      <span>Uploading image from device to Cloudflare R2...</span>
                    </div>
                  ) : (
                    <div
                      className={`image-dropzone ${dragOver ? "dragover" : ""}`}
                      onClick={() => !isCheckInLocked && fileInputRef.current?.click()}
                      onDragOver={handleDragOver}
                      onDragLeave={handleDragLeave}
                      onDrop={handleDrop}
                    >
                      <div className="dropzone-icon">
                        <Upload size={20} />
                      </div>
                      <p className="dropzone-text">Click to browse or drag & drop image from your device</p>
                      <p className="dropzone-subtext">Supports high-resolution JPEG, PNG, WebP (Saved to Cloudflare R2)</p>
                    </div>
                  )}

                  {imageUploadError && (
                    <div style={{ color: "#dc2626", fontSize: "12.5px", marginTop: "6px" }}>
                      ⚠️ {imageUploadError}
                    </div>
                  )}
                </div>
              ) : (
                <div>
                  <input
                    type="url"
                    className="form-control"
                    placeholder="https://images.unsplash.com/... or https://..."
                    value={imageUrl}
                    onChange={(e) => setImageUrl(e.target.value)}
                  />
                </div>
              )}

              {/* Live Preview Box */}
              {imageUrl && (
                <div className="image-preview-card">
                  <img
                    src={imageUrl}
                    alt="Workshop Banner Preview"
                    className="image-preview-img"
                    onError={(e) => {
                      e.target.style.display = "none";
                    }}
                  />
                  <div className="image-preview-overlay">
                    <button
                      type="button"
                      className="btn-image-action"
                      onClick={() => {
                        if (imageTab === "upload") {
                          fileInputRef.current?.click();
                        } else {
                          setImageUrl("");
                        }
                      }}
                    >
                      <Upload size={13} />
                      <span>Change Image</span>
                    </button>
                    <button
                      type="button"
                      className="btn-image-action danger"
                      onClick={() => setImageUrl("")}
                      title="Remove banner"
                    >
                      <Trash2 size={13} />
                      <span>Remove</span>
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="form-group">
            <label className="form-label">Syllabus & Description</label>
            <textarea
              rows="4"
              className="form-control"
              placeholder="Detail the choreography syllabus, techniques covered, what students should wear/bring..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
          </fieldset>

          <div className="edit-actions-row">
            <button
              type="button"
              className="admin-btn-secondary"
              onClick={() => navigate(`/admin_portal/workshops/${workshopId}/overview`)}
              disabled={submitting}
            >
              Cancel
            </button>
            <button type="submit" className="admin-btn-primary" disabled={submitting || isCheckInLocked}>
              {isCheckInLocked ? "🔒 Editing Disabled (Check-in Active)" : submitting ? "Saving Changes..." : "Save Workshop Changes"}
            </button>
          </div>
        </form>
      </div>

      {/* Danger Zone: Workshop Cancellation */}
      {!isCompleted && (
        <div className="danger-zone-card">
          <div className="danger-zone-info">
            <h4 className="danger-zone-title">
              <AlertTriangle size={18} />
              <span>Workshop Lifecycle: Cancel Workshop</span>
            </h4>
            <p className="danger-zone-desc">
              {isCancelled
                ? "This workshop has already been cancelled. Ticket sales are stopped and participants have been notified."
                : "Need to cancel this session? Cancelling will prevent further bookings, mark the status as Cancelled across all portals, and alert registered participants."}
            </p>
          </div>

          {!isCancelled && (
            <button
              type="button"
              className="admin-btn-danger"
              onClick={() => setCancelModal({ open: true, reason: "", submitting: false, error: null })}
            >
              <XCircle size={15} />
              <span>Cancel This Workshop</span>
            </button>
          )}
        </div>
      )}

      {/* Cancel Confirmation Modal */}
      {cancelModal.open && (
        <div className="cancel-modal-overlay" onClick={() => setCancelModal({ open: false, reason: "", submitting: false, error: null })}>
          <div className="cancel-modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="cancel-modal-header">
              <h3 className="cancel-modal-title">
                <AlertTriangle size={18} />
                <span>Confirm Workshop Cancellation</span>
              </h3>
              <button
                type="button"
                style={{ background: "transparent", border: "none", cursor: "pointer", color: "#64748b" }}
                onClick={() => setCancelModal({ open: false, reason: "", submitting: false, error: null })}
              >
                <X size={18} />
              </button>
            </div>

            <div className="cancel-modal-body">
              <p>
                Are you sure you want to cancel <strong>"{title}"</strong>? This will notify all registered students, lock further ticket sales, and move the workshop into the Cancelled archive.
              </p>

              <div className="form-group">
                <label className="form-label" style={{ color: "#991b1b" }}>
                  Cancellation Reason (Required) *
                </label>
                <textarea
                  rows="3"
                  className="form-control"
                  placeholder="e.g. Trainer emergency illness, studio maintenance, venue conflict..."
                  value={cancelModal.reason}
                  onChange={(e) => setCancelModal((prev) => ({ ...prev, reason: e.target.value, error: null }))}
                  required
                />
              </div>

              {cancelModal.error && (
                <div style={{ color: "#dc2626", fontSize: "13px", fontWeight: "600" }}>
                  ⚠️ {cancelModal.error}
                </div>
              )}
            </div>

            <div className="cancel-modal-footer">
              <button
                type="button"
                className="admin-btn-secondary"
                disabled={cancelModal.submitting}
                onClick={() => setCancelModal({ open: false, reason: "", submitting: false, error: null })}
              >
                Keep Workshop
              </button>
              <button
                type="button"
                className="admin-btn-danger-solid"
                disabled={cancelModal.submitting}
                onClick={handleConfirmCancelWorkshop}
              >
                {cancelModal.submitting ? "Cancelling Workshop..." : "Yes, Cancel Workshop"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
