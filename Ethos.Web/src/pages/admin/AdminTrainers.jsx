import React, { useState, useEffect, useRef } from "react";
import { Search, UserPlus, Edit2, Power, Trash2, CheckCircle2, AlertCircle, RefreshCw, X, Upload, Loader2, Image as ImageIcon } from "lucide-react";
import { adminApi } from "../../services/adminApi";
import "./AdminTrainers.css";

export default function AdminTrainers() {
  const [trainers, setTrainers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");

  // Create / Edit Modal state
  const [modalOpen, setModalOpen] = useState(false);
  const [editingTrainer, setEditingTrainer] = useState(null);
  const [formData, setFormData] = useState({
    fullName: "",
    phone: "",
    email: "",
    dateOfBirth: "",
    city: "Hyderabad",
    experienceYears: 3,
    primaryDanceStyle: "Urban Choreography",
    secondaryDanceStyles: "",
    bio: "",
    profilePhotoUrl: "",
    isActive: true,
  });
  const [formSaving, setFormSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [formSuccess, setFormSuccess] = useState("");

  // Device photo upload state
  const photoInputRef = useRef(null);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [photoUploadError, setPhotoUploadError] = useState("");
  const [showUrlInput, setShowUrlInput] = useState(false);

  const handlePhotoUpload = async (file) => {
    if (!file) return;
    if (!file.type.startsWith("image/")) {
      setPhotoUploadError("Please select a valid image file (JPEG, PNG, WebP).");
      return;
    }
    if (file.size > 35 * 1024 * 1024) {
      setPhotoUploadError("File size exceeds 35 MB limit.");
      return;
    }

    setUploadingPhoto(true);
    setPhotoUploadError("");

    try {
      const fd = new FormData();
      fd.append("file", file);
      fd.append("section", "Trainers");
      fd.append("title", `${formData.fullName || "Trainer"} Profile Photo`);
      fd.append("mediaType", "Image");
      fd.append("isPublished", "true");

      const res = await adminApi.uploadMedia(fd);
      const url = res?.publicUrl || res?.PublicUrl || res?.r2Url || res?.R2Url || res?.mediaItem?.r2Url;
      if (url) {
        setFormData((prev) => ({ ...prev, profilePhotoUrl: url }));
      } else {
        throw new Error("No URL returned from upload");
      }
    } catch (err) {
      console.error("Trainer photo upload failed:", err);
      setPhotoUploadError(err.message || "Failed to upload photo from device.");
    } finally {
      setUploadingPhoto(false);
    }
  };

  // Delete / Archive confirmation modal
  const [deleteModal, setDeleteModal] = useState({
    open: false,
    trainer: null,
    loading: false,
    message: "",
  });

  const loadTrainers = async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (search) params.append("search", search);
      if (statusFilter !== "all") params.append("status", statusFilter);

      const res = await adminApi.getTrainers(params.toString());
      setTrainers(Array.isArray(res?.items) ? res.items : Array.isArray(res) ? res : []);
    } catch (err) {
      setError(err.message || "Failed to load trainers directory.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTrainers();
  }, [search, statusFilter]);

  const openCreateModal = () => {
    setEditingTrainer(null);
    setFormData({
      fullName: "",
      phone: "",
      email: "",
      dateOfBirth: "",
      city: "Hyderabad",
      experienceYears: 3,
      primaryDanceStyle: "Urban Choreography",
      secondaryDanceStyles: "",
      bio: "",
      profilePhotoUrl: "",
      isActive: true,
    });
    setFormError("");
    setFormSuccess("");
    setPhotoUploadError("");
    setShowUrlInput(false);
    setModalOpen(true);
  };

  const openEditModal = (t) => {
    setEditingTrainer(t);
    setFormData({
      fullName: t.fullName || "",
      phone: t.phone || "",
      email: t.email || "",
      dateOfBirth: t.dateOfBirth ? t.dateOfBirth.slice(0, 10) : "",
      city: t.city || "Hyderabad",
      experienceYears: t.experienceYears || 1,
      primaryDanceStyle: t.primaryDanceStyle || "Urban Choreography",
      secondaryDanceStyles: t.secondaryDanceStyles || "",
      bio: t.bio || "",
      profilePhotoUrl: t.profilePhotoUrl || "",
      isActive: t.status === "Active",
    });
    setFormError("");
    setFormSuccess("");
    setPhotoUploadError("");
    setShowUrlInput(false);
    setModalOpen(true);
  };

  const handleSaveTrainer = async (e) => {
    e.preventDefault();
    setFormError("");
    setFormSuccess("");

    if (!formData.fullName.trim()) {
      setFormError("Trainer full name is required.");
      return;
    }
    const cleanPhone = formData.phone.replace(/\D/g, "");
    if (cleanPhone.length !== 10) {
      setFormError("Please enter a valid 10-digit Indian phone number.");
      return;
    }

    setFormSaving(true);
    try {
      if (editingTrainer) {
        const id = editingTrainer.trainerId || editingTrainer.id;
        await adminApi.updateTrainer(id, {
          fullName: formData.fullName.trim(),
          phone: cleanPhone,
          email: formData.email.trim() || null,
          city: formData.city.trim() || "Hyderabad",
          experienceYears: Number(formData.experienceYears) || 0,
          primaryDanceStyle: formData.primaryDanceStyle.trim(),
          secondaryDanceStyles: formData.secondaryDanceStyles.trim() || null,
          bio: formData.bio.trim() || null,
          profilePhotoUrl: formData.profilePhotoUrl.trim() || null,
          isActive: formData.isActive,
          dateOfBirth: formData.dateOfBirth ? new Date(formData.dateOfBirth).toISOString() : null,
        });
        setFormSuccess("Trainer profile updated successfully!");
      } else {
        await adminApi.createTrainer({
          fullName: formData.fullName.trim(),
          phone: cleanPhone,
          email: formData.email.trim() || null,
          city: formData.city.trim() || "Hyderabad",
          experienceYears: Number(formData.experienceYears) || 0,
          primaryDanceStyle: formData.primaryDanceStyle.trim(),
          secondaryDanceStyles: formData.secondaryDanceStyles.trim() || null,
          bio: formData.bio.trim() || null,
          profilePhotoUrl: formData.profilePhotoUrl.trim() || null,
          isActive: formData.isActive,
          dateOfBirth: formData.dateOfBirth ? new Date(formData.dateOfBirth).toISOString() : null,
        });
        setFormSuccess("Trainer created and activated successfully!");
      }

      setTimeout(() => {
        setModalOpen(false);
        loadTrainers();
      }, 900);
    } catch (err) {
      setFormError(err.message || "Failed to save trainer profile.");
    } finally {
      setFormSaving(false);
    }
  };

  const handleToggleStatus = async (t) => {
    const id = t.trainerId || t.id;
    const currentActive = t.status === "Active";
    const nextStatus = currentActive ? "Suspended" : "Active";
    const actionLabel = currentActive ? "deactivate" : "activate";

    if (!window.confirm(`Are you sure you want to ${actionLabel} ${t.fullName}?`)) return;

    try {
      await adminApi.updateTrainerStatus(id, nextStatus, `Admin ${actionLabel}d trainer.`);
      loadTrainers();
    } catch (err) {
      alert(err.message || `Failed to ${actionLabel} trainer.`);
    }
  };

  const confirmDeleteOrArchive = async () => {
    if (!deleteModal.trainer) return;
    const id = deleteModal.trainer.trainerId || deleteModal.trainer.id;
    setDeleteModal(prev => ({ ...prev, loading: true }));

    try {
      const res = await adminApi.deleteTrainer(id);
      alert(res.message || "Trainer removed successfully.");
      setDeleteModal({ open: false, trainer: null, loading: false, message: "" });
      loadTrainers();
    } catch (err) {
      alert(err.message || "Action failed.");
      setDeleteModal(prev => ({ ...prev, loading: false }));
    }
  };

  const activeTrainersCount = trainers.filter(t => t.status === "Active").length;

  return (
    <div className="admin-trainers-page">
      {/* Header Banner */}
      <div className="trainers-page-header">
        <div>
          <h1 className="trainers-page-title">Manage Trainers</h1>
          <p className="trainers-page-subtitle">
            Directory of approved dance faculty, choreographers, and workshop mentors.
          </p>
        </div>
        <button
          type="button"
          className="btn-create-trainer"
          onClick={openCreateModal}
        >
          <UserPlus size={16} />
          <span>+ Create Trainer</span>
        </button>
      </div>

      {/* Filter & Search Toolbar */}
      <div className="trainers-toolbar">
        <div className="search-box-wrap">
          <Search size={16} className="search-icon" />
          <input
            type="text"
            className="search-input"
            placeholder="Search by trainer name, dance style, or phone..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {search && (
            <button className="clear-btn" onClick={() => setSearch("")}>✕</button>
          )}
        </div>

        <div className="status-filter-wrap">
          <select
            className="status-select"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
          >
            <option value="all">All Statuses ({trainers.length})</option>
            <option value="Active">Active ({activeTrainersCount})</option>
            <option value="Suspended">Inactive ({trainers.length - activeTrainersCount})</option>
          </select>
        </div>

        <button
          type="button"
          className="refresh-btn"
          onClick={loadTrainers}
          title="Refresh Directory"
        >
          <RefreshCw size={15} />
        </button>
      </div>

      {/* Main Table */}
      <div className="trainers-table-card">
        {loading ? (
          <div className="trainers-loading-state">
            <div className="spinner" />
            <span>Loading trainers...</span>
          </div>
        ) : error ? (
          <div className="trainers-error-state">
            <AlertCircle size={20} />
            <span>{error}</span>
          </div>
        ) : trainers.length === 0 ? (
          <div className="trainers-empty-state">
            <p>No trainers found matching your search.</p>
            <button className="btn-create-trainer-subtle" onClick={openCreateModal}>
              + Add First Trainer
            </button>
          </div>
        ) : (
          <div className="table-responsive">
            <table className="trainers-data-table">
              <thead>
                <tr>
                  <th>Photo</th>
                  <th>Trainer</th>
                  <th>Styles</th>
                  <th>Experience</th>
                  <th>Status</th>
                  <th className="th-actions">Actions</th>
                </tr>
              </thead>
              <tbody>
                {trainers.map((t) => {
                  const id = t.trainerId || t.id;
                  const isActive = t.status === "Active";

                  return (
                    <tr key={id} className={!isActive ? "row-inactive" : ""}>
                      {/* 1. Photo (Circle) */}
                      <td className="td-photo">
                        {t.profilePhotoUrl ? (
                          <img
                            src={t.profilePhotoUrl}
                            alt={t.fullName}
                            className="trainer-circle-photo"
                          />
                        ) : (
                          <div className="trainer-photo-fallback">
                            {(t.fullName || "T")[0].toUpperCase()}
                          </div>
                        )}
                      </td>

                      {/* 2. Trainer Details */}
                      <td className="td-trainer">
                        <div className="trainer-name-col">
                          <span className="trainer-fullname">{t.fullName}</span>
                          <span className="trainer-phone">{t.phone || "No phone"}</span>
                          {t.city && <span className="trainer-city-tag">{t.city}</span>}
                        </div>
                      </td>

                      {/* 3. Styles */}
                      <td className="td-styles">
                        <div className="styles-tags-wrap">
                          <span className="primary-style-pill">
                            {t.primaryDanceStyle || "Choreography"}
                          </span>
                          {t.secondaryDanceStyles && (
                            <span className="secondary-styles-text">
                              {t.secondaryDanceStyles}
                            </span>
                          )}
                        </div>
                      </td>

                      {/* 4. Experience */}
                      <td className="td-experience">
                        <span className="exp-val">
                          {t.experienceYears ? `${t.experienceYears} years` : "1+ years"}
                        </span>
                      </td>

                      {/* 5. Status */}
                      <td className="td-status">
                        {isActive ? (
                          <span className="badge-active">
                            <span className="status-dot" /> Active
                          </span>
                        ) : (
                          <span className="badge-inactive">
                            <span className="status-dot dot-inactive" /> Deactivated
                          </span>
                        )}
                      </td>

                      {/* 6. Actions */}
                      <td className="td-actions">
                        <div className="action-buttons-group">
                          <button
                            type="button"
                            className="btn-action edit"
                            onClick={() => openEditModal(t)}
                            title="Edit Trainer Details"
                          >
                            <Edit2 size={14} />
                            <span>Edit</span>
                          </button>

                          <button
                            type="button"
                            className={`btn-action ${isActive ? "deactivate" : "activate"}`}
                            onClick={() => handleToggleStatus(t)}
                            title={isActive ? "Deactivate Trainer" : "Activate Trainer"}
                          >
                            <Power size={14} />
                            <span>{isActive ? "Deactivate" : "Activate"}</span>
                          </button>

                          <button
                            type="button"
                            className="btn-action delete"
                            onClick={() => setDeleteModal({
                              open: true,
                              trainer: t,
                              loading: false,
                              message: `Are you sure you want to remove ${t.fullName}? If they are assigned to workshops, they will be safely archived.`
                            })}
                            title="Delete or Archive Trainer"
                          >
                            <Trash2 size={14} />
                            <span>Delete</span>
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* CREATE / EDIT MODAL */}
      {modalOpen && (
        <div className="trainer-modal-backdrop" onClick={() => setModalOpen(false)}>
          <div className="trainer-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="trainer-modal-header">
              <h3>{editingTrainer ? "Edit Trainer Profile" : "Create New Trainer"}</h3>
              <button className="btn-modal-close" onClick={() => setModalOpen(false)}>
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleSaveTrainer} className="trainer-modal-form">
              {formError && (
                <div className="form-alert-banner error">
                  <AlertCircle size={15} />
                  <span>{formError}</span>
                </div>
              )}
              {formSuccess && (
                <div className="form-alert-banner success">
                  <CheckCircle2 size={15} />
                  <span>{formSuccess}</span>
                </div>
              )}

              <div className="modal-form-grid-2">
                <div className="form-field-group">
                  <label className="field-label">Trainer Name <span className="req">*</span></label>
                  <input
                    type="text"
                    className="field-input"
                    placeholder="e.g. Arjun Verma"
                    value={formData.fullName}
                    onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                    required
                  />
                </div>

                <div className="form-field-group">
                  <label className="field-label">Phone Number (10 digits) <span className="req">*</span></label>
                  <input
                    type="tel"
                    className="field-input"
                    placeholder="e.g. 9848012345"
                    value={formData.phone}
                    maxLength={10}
                    onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                    required
                  />
                </div>
              </div>

              <div className="modal-form-grid-2">
                <div className="form-field-group">
                  <label className="field-label">Email Address (Optional)</label>
                  <input
                    type="email"
                    className="field-input"
                    placeholder="e.g. arjun@ethosdance.com"
                    value={formData.email}
                    onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  />
                </div>

                <div className="form-field-group">
                  <label className="field-label">Date of Birth</label>
                  <input
                    type="date"
                    className="field-input"
                    value={formData.dateOfBirth}
                    onChange={(e) => setFormData({ ...formData, dateOfBirth: e.target.value })}
                  />
                </div>
              </div>

              <div className="modal-form-grid-2">
                <div className="form-field-group">
                  <label className="field-label">Years of Experience</label>
                  <input
                    type="number"
                    min="0"
                    max="40"
                    className="field-input"
                    value={formData.experienceYears}
                    onChange={(e) => setFormData({ ...formData, experienceYears: Number(e.target.value) })}
                  />
                </div>

                <div className="form-field-group">
                  <label className="field-label">City</label>
                  <input
                    type="text"
                    className="field-input"
                    placeholder="e.g. Hyderabad, Mumbai, Bengaluru"
                    value={formData.city}
                    onChange={(e) => setFormData({ ...formData, city: e.target.value })}
                  />
                </div>
              </div>

              <div className="modal-form-grid-2">
                <div className="form-field-group">
                  <label className="field-label">Primary Dance Style <span className="req">*</span></label>
                  <input
                    type="text"
                    className="field-input"
                    placeholder="e.g. Urban Choreography, Hip Hop, Locking"
                    value={formData.primaryDanceStyle}
                    onChange={(e) => setFormData({ ...formData, primaryDanceStyle: e.target.value })}
                    required
                  />
                </div>

                <div className="form-field-group">
                  <label className="field-label">Secondary Dance Styles</label>
                  <input
                    type="text"
                    className="field-input"
                    placeholder="e.g. Popping, House, Waacking"
                    value={formData.secondaryDanceStyles}
                    onChange={(e) => setFormData({ ...formData, secondaryDanceStyles: e.target.value })}
                  />
                </div>
              </div>

              <div className="form-field-group">
                <label className="field-label">Profile Photo</label>

                {formData.profilePhotoUrl ? (
                  <div className="trainer-photo-preview-wrap">
                    <div className="trainer-photo-preview-avatar">
                      <img
                        src={formData.profilePhotoUrl}
                        alt={formData.fullName || "Trainer"}
                        onError={(e) => {
                          e.target.style.display = "none";
                        }}
                      />
                    </div>
                    <div className="trainer-photo-preview-info">
                      <div className="photo-status-badge">✓ Photo Uploaded</div>
                      <div className="photo-actions-row">
                        <input
                          type="file"
                          ref={photoInputRef}
                          accept="image/jpeg,image/png,image/webp"
                          style={{ display: "none" }}
                          onChange={(e) => {
                            if (e.target.files && e.target.files[0]) {
                              handlePhotoUpload(e.target.files[0]);
                            }
                          }}
                        />
                        <button
                          type="button"
                          className="btn-photo-action"
                          onClick={() => photoInputRef.current?.click()}
                          disabled={uploadingPhoto}
                        >
                          {uploadingPhoto ? <Loader2 size={13} className="animate-spin" /> : <Upload size={13} />}
                          <span>Change Photo</span>
                        </button>
                        <button
                          type="button"
                          className="btn-photo-action delete"
                          onClick={() => setFormData((prev) => ({ ...prev, profilePhotoUrl: "" }))}
                          disabled={uploadingPhoto}
                        >
                          <Trash2 size={13} />
                          <span>Remove</span>
                        </button>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="trainer-photo-upload-zone">
                    <input
                      type="file"
                      ref={photoInputRef}
                      accept="image/jpeg,image/png,image/webp"
                      style={{ display: "none" }}
                      onChange={(e) => {
                        if (e.target.files && e.target.files[0]) {
                          handlePhotoUpload(e.target.files[0]);
                        }
                      }}
                    />

                    {uploadingPhoto ? (
                      <div className="trainer-uploading-row">
                        <Loader2 size={18} className="animate-spin" />
                        <span>Uploading photo from device to Cloudflare R2...</span>
                      </div>
                    ) : (
                      <div
                        className="trainer-photo-dropzone"
                        onClick={() => photoInputRef.current?.click()}
                      >
                        <div className="photo-dropzone-icon">
                          <Upload size={18} />
                        </div>
                        <div className="photo-dropzone-texts">
                          <span className="photo-dropzone-main">Upload Photo from Device</span>
                          <span className="photo-dropzone-sub">Supports high-res JPEG, PNG, WebP up to 35 MB</span>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {photoUploadError && (
                  <div style={{ color: "#dc2626", fontSize: "12px", marginTop: "5px" }}>
                    ⚠️ {photoUploadError}
                  </div>
                )}

                {/* Optional URL input for manual override */}
                <div style={{ marginTop: "6px" }}>
                  {!showUrlInput ? (
                    <button
                      type="button"
                      style={{
                        background: "none",
                        border: "none",
                        color: "#64748b",
                        fontSize: "11.5px",
                        cursor: "pointer",
                        textDecoration: "underline",
                        padding: 0,
                      }}
                      onClick={() => setShowUrlInput(true)}
                    >
                      or enter image URL manually
                    </button>
                  ) : (
                    <div style={{ marginTop: "4px" }}>
                      <input
                        type="url"
                        className="field-input"
                        placeholder="https://images.unsplash.com/... or https://..."
                        value={formData.profilePhotoUrl}
                        onChange={(e) =>
                          setFormData({ ...formData, profilePhotoUrl: e.target.value })
                        }
                        style={{ fontSize: "12px", padding: "6px 10px" }}
                      />
                    </div>
                  )}
                </div>
              </div>

              <div className="form-field-group">
                <label className="field-label">Short Bio / Highlights</label>
                <textarea
                  className="field-textarea"
                  rows={3}
                  placeholder="Professional experience, dance mentors, performance highlights..."
                  value={formData.bio}
                  onChange={(e) => setFormData({ ...formData, bio: e.target.value })}
                />
              </div>

              <div className="form-checkbox-row">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={formData.isActive}
                    onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                  />
                  <span>Active Trainer (Eligible to mentor & lead workshops immediately)</span>
                </label>
              </div>

              <div className="modal-actions-bar">
                <button
                  type="button"
                  className="btn-modal-cancel"
                  onClick={() => setModalOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="btn-modal-submit"
                  disabled={formSaving}
                >
                  {formSaving ? "Saving..." : editingTrainer ? "Save Changes" : "Create Trainer"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* CONFIRM DELETE / ARCHIVE MODAL */}
      {deleteModal.open && (
        <div className="trainer-modal-backdrop" onClick={() => setDeleteModal({ open: false, trainer: null, loading: false, message: "" })}>
          <div className="trainer-modal-card confirm-card" onClick={(e) => e.stopPropagation()}>
            <div className="trainer-modal-header">
              <h3>Delete or Archive Trainer</h3>
              <button className="btn-modal-close" onClick={() => setDeleteModal({ open: false, trainer: null, loading: false, message: "" })}>
                <X size={18} />
              </button>
            </div>
            <div className="confirm-body">
              <p>{deleteModal.message}</p>
              <p className="confirm-note">
                Note: Trainers with assigned workshop or ticket history cannot be permanently destroyed to preserve audit & financial records. They will be archived and deactivated.
              </p>
            </div>
            <div className="modal-actions-bar">
              <button
                type="button"
                className="btn-modal-cancel"
                onClick={() => setDeleteModal({ open: false, trainer: null, loading: false, message: "" })}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn-modal-delete"
                onClick={confirmDeleteOrArchive}
                disabled={deleteModal.loading}
              >
                {deleteModal.loading ? "Processing..." : "Confirm Delete / Archive"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
