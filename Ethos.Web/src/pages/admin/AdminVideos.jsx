import React, { useState, useEffect } from "react";
import { adminApi } from "../../services/adminApi";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import "./AdminVideos.css";

export default function AdminVideos() {
  const [activeTab, setActiveTab] = useState("ShortVideos"); // "ShortVideos" or "Gallery"
  const [videos, setVideos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [actionSuccess, setActionSuccess] = useState(null);

  // Modals state
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [replaceModalOpen, setReplaceModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [previewModalVideo, setPreviewModalVideo] = useState(null);

  // Target item for edit/replace/delete
  const [selectedVideo, setSelectedVideo] = useState(null);

  // Form states
  const [uploadFile, setUploadFile] = useState(null);
  const [uploadTitle, setUploadTitle] = useState("");
  const [uploadDesc, setUploadDesc] = useState("");
  const [uploadOrder, setUploadOrder] = useState("");
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState(null);

  // Replace form state
  const [replaceFile, setReplaceFile] = useState(null);
  const [replacing, setReplacing] = useState(false);
  const [replaceError, setReplaceError] = useState(null);

  // Edit form state
  const [editTitle, setEditTitle] = useState("");
  const [editDesc, setEditDesc] = useState("");
  const [editOrder, setEditOrder] = useState(0);
  const [editing, setEditing] = useState(false);

  // Deleting state
  const [deleting, setDeleting] = useState(false);

  const loadVideos = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getAdminVideos();
      setVideos(Array.isArray(res) ? res : []);
    } catch (err) {
      setError(err.message || "Failed to load video library.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadVideos();
  }, []);

  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        if (!uploading) setUploadModalOpen(false);
        if (!replacing) setReplaceModalOpen(false);
        if (!editing) setEditModalOpen(false);
        if (!deleting) setDeleteModalOpen(false);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [uploading, replacing, editing, deleting]);

  const showNotification = (msg) => {
    setActionSuccess(msg);
    setTimeout(() => setActionSuccess(null), 5000);
  };

  // Filtered by current tab
  const tabVideos = videos
    .filter((v) => v.section === activeTab)
    .sort((a, b) => a.displayOrder - b.displayOrder);

  const shortVideosList = videos.filter((v) => v.section === "ShortVideos");
  const galleryVideosList = videos.filter((v) => v.section === "Gallery");

  const activeShorts = shortVideosList.filter((v) => v.isActive).length;
  const activeGallery = galleryVideosList.filter((v) => v.isActive).length;

  const totalStorageBytes = videos.reduce((acc, v) => acc + (v.fileSizeBytes || 0), 0);
  const totalStorageMb = (totalStorageBytes / (1024 * 1024)).toFixed(1);

  // --- Handlers ---
  const handleOpenUpload = () => {
    setUploadFile(null);
    setUploadTitle("");
    setUploadDesc("");
    setUploadOrder(tabVideos.length + 1);
    setUploadError(null);
    setUploadModalOpen(true);
  };

  const handleUploadSubmit = async (e) => {
    e.preventDefault();
    if (!uploadFile) {
      setUploadError("Please select an MP4 video file to upload.");
      return;
    }
    if (!uploadTitle.trim()) {
      setUploadError("Please provide a title for the video.");
      return;
    }

    const maxMb = activeTab === "ShortVideos" ? 25 : 100;
    if (uploadFile.size > maxMb * 1024 * 1024) {
      setUploadError(`File exceeds maximum size of ${maxMb} MB for ${activeTab}.`);
      return;
    }

    setUploading(true);
    setUploadError(null);

    const formData = new FormData();
    formData.append("file", uploadFile);
    formData.append("section", activeTab);
    formData.append("title", uploadTitle.trim());
    if (uploadDesc.trim()) formData.append("description", uploadDesc.trim());
    formData.append("displayOrder", uploadOrder || 0);

    try {
      await adminApi.uploadVideo(formData);
      setUploadModalOpen(false);
      showNotification(`"${uploadTitle.trim()}" successfully uploaded to Cloudflare R2!`);
      await loadVideos();
    } catch (err) {
      setUploadError(err.message || "Failed to upload video to Cloudflare R2.");
    } finally {
      setUploading(false);
    }
  };

  const handleOpenEdit = (video) => {
    setSelectedVideo(video);
    setEditTitle(video.title || "");
    setEditDesc(video.description || "");
    setEditOrder(video.displayOrder ?? 0);
    setEditModalOpen(true);
  };

  const handleEditSubmit = async (e) => {
    e.preventDefault();
    if (!selectedVideo) return;
    setEditing(true);
    try {
      await adminApi.updateVideo(selectedVideo.id, {
        title: editTitle.trim(),
        description: editDesc.trim(),
        displayOrder: parseInt(editOrder, 10) || 0,
      });
      setEditModalOpen(false);
      showNotification("Video details updated successfully.");
      await loadVideos();
    } catch (err) {
      setError(err.message || "Failed to update video.");
    } finally {
      setEditing(false);
    }
  };

  const handleOpenReplace = (video) => {
    setSelectedVideo(video);
    setReplaceFile(null);
    setReplaceError(null);
    setReplaceModalOpen(true);
  };

  const handleReplaceSubmit = async (e) => {
    e.preventDefault();
    if (!selectedVideo || !replaceFile) {
      setReplaceError("Please choose a replacement video file.");
      return;
    }

    const maxMb = selectedVideo.section === "ShortVideos" ? 25 : 100;
    if (replaceFile.size > maxMb * 1024 * 1024) {
      setReplaceError(`Replacement file exceeds maximum size of ${maxMb} MB.`);
      return;
    }

    setReplacing(true);
    setReplaceError(null);

    const formData = new FormData();
    formData.append("file", replaceFile);

    try {
      await adminApi.replaceVideo(selectedVideo.id, formData);
      setReplaceModalOpen(false);
      showNotification(`Video file for "${selectedVideo.title}" was replaced in Cloudflare R2.`);
      await loadVideos();
    } catch (err) {
      setReplaceError(err.message || "Failed to replace video file.");
    } finally {
      setReplacing(false);
    }
  };

  const handleToggleActive = async (video) => {
    try {
      const newStatus = !video.isActive;
      await adminApi.toggleVideoActive(video.id, newStatus);
      showNotification(
        newStatus ? `"${video.title}" is now ACTIVE on website.` : `"${video.title}" is now HIDDEN from website.`
      );
      await loadVideos();
    } catch (err) {
      setError(err.message || "Failed to toggle video visibility.");
    }
  };

  const handleOpenDelete = (video) => {
    setSelectedVideo(video);
    setDeleteModalOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!selectedVideo) return;
    setDeleting(true);
    try {
      await adminApi.deleteVideo(selectedVideo.id);
      setDeleteModalOpen(false);
      showNotification(`"${selectedVideo.title}" permanently deleted from Cloudflare R2 and Neon DB.`);
      await loadVideos();
    } catch (err) {
      setError(err.message || "Failed to delete video.");
    } finally {
      setDeleting(false);
    }
  };

  const handleMoveOrder = async (video, direction) => {
    const list = [...tabVideos];
    const currentIndex = list.findIndex((v) => v.id === video.id);
    if (currentIndex === -1) return;

    const targetIndex = direction === "up" ? currentIndex - 1 : currentIndex + 1;
    if (targetIndex < 0 || targetIndex >= list.length) return;

    const targetVideo = list[targetIndex];

    const updatedItems = [
      { id: video.id, displayOrder: targetVideo.displayOrder },
      { id: targetVideo.id, displayOrder: video.displayOrder },
    ];

    try {
      await adminApi.reorderVideos(updatedItems);
      await loadVideos();
    } catch (err) {
      setError(err.message || "Failed to reorder videos.");
    }
  };

  return (
    <div className="admin-videos-page">
      {/* Page Header */}
      <div className="admin-page-header">
        <div>
          <div className="admin-breadcrumb-tag">MEDIA & ASSET STORAGE · CLOUD REELS</div>
          <h1 className="admin-page-title">Studio Video Management</h1>
          <p className="admin-page-subtitle">
            Manage short dance clips (reels) and high-production gallery video assets with cloud object storage and database records.
          </p>
        </div>
        <div className="admin-header-actions">
          <button type="button" className="btn btn-secondary" onClick={loadVideos} disabled={loading}>
            {loading ? "Refreshing..." : "↻ Refresh"}
          </button>
          <button type="button" className="btn btn-primary" onClick={handleOpenUpload}>
            + Upload New Video
          </button>
        </div>
      </div>

      {/* KPI Overview Grid */}
      <div className="admin-kpi-grid">
        <AdminKpiCard
          label="Short Dance Videos"
          value={`${activeShorts} Active`}
          tone="brand"
          sublabel={`${shortVideosList.length} total in Storage (Max 20s · 25MB)`}
          icon="📱"
        />
        <AdminKpiCard
          label="Gallery Showcases"
          value={`${activeGallery} of 7`}
          tone="success"
          sublabel={`${galleryVideosList.length} total in Storage (Max 90s · 100MB)`}
          icon="🎭"
        />
        <AdminKpiCard
          label="Storage Used"
          value={`${totalStorageMb} MB`}
          tone="info"
          sublabel={`${videos.length} MP4 assets stored`}
          icon="☁️"
        />
        <AdminKpiCard
          label="Storage Provider"
          value="Operational"
          tone="success"
          sublabel="Zero egress · High-speed streaming"
          icon="⚡"
        />
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {actionSuccess && <div className="alert alert-success">{actionSuccess}</div>}

      {/* Section Tabs */}
      <div className="video-tabs-bar">
        <div className="video-tabs-group">
          <button
            type="button"
            className={`video-tab-btn ${activeTab === "ShortVideos" ? "active" : ""}`}
            onClick={() => setActiveTab("ShortVideos")}
          >
            <span className="tab-icon">📱</span>
            Short Dance Videos (10–20s)
            <span className="tab-count-pill">{shortVideosList.length}</span>
          </button>

          <button
            type="button"
            className={`video-tab-btn ${activeTab === "Gallery" ? "active" : ""}`}
            onClick={() => setActiveTab("Gallery")}
          >
            <span className="tab-icon">🎭</span>
            Gallery Showcase (1 min)
            <span className="tab-count-pill">{galleryVideosList.length}</span>
          </button>
        </div>

        <div className="tab-spec-hint">
          {activeTab === "ShortVideos" ? (
            <span>💡 <strong>Short Videos Spec:</strong> 10–20s max duration · 25 MB max · Auto-looping mobile reels</span>
          ) : (
            <span>💡 <strong>Gallery Spec:</strong> 60–90s duration · 100 MB max · Full routine showcases with sound</span>
          )}
        </div>
      </div>

      {/* Videos List */}
      <div className="videos-container">
        {loading ? (
          <div className="videos-loading-state">
            <div className="spinner"></div>
            <p>Loading {activeTab === "ShortVideos" ? "Short Dance Videos" : "Gallery Showcases"} from cloud storage...</p>
          </div>
        ) : tabVideos.length === 0 ? (
          <div className="videos-empty-state">
            <div className="empty-icon">{activeTab === "ShortVideos" ? "📱" : "🎭"}</div>
            <h3>No {activeTab === "ShortVideos" ? "Short Dance Videos" : "Gallery Showcases"} uploaded yet</h3>
            <p>
              {activeTab === "ShortVideos"
                ? "Upload 10–20 second quick dance clips, reels, or choreography highlights (Max 25 MB)."
                : "Upload approximately 6–7 high-production showcase videos (Max 100 MB)."}
            </p>
            <button type="button" className="btn btn-primary" onClick={handleOpenUpload}>
              + Upload First Video
            </button>
          </div>
        ) : (
          <div className="videos-grid">
            {tabVideos.map((video, idx) => {
              const sizeMb = ((video.fileSizeBytes || 0) / (1024 * 1024)).toFixed(1);
              const isFirst = idx === 0;
              const isLast = idx === tabVideos.length - 1;

              return (
                <div key={video.id} className={`video-card ${video.isActive ? "is-active" : "is-hidden"}`}>
                  {/* Card Thumbnail / Player Preview */}
                  <div className="video-player-wrap">
                    <video
                      src={video.publicUrl}
                      className="video-player-element"
                      muted
                      preload="metadata"
                      playsInline
                      onClick={() => setPreviewModalVideo(video)}
                    />
                    <div className="video-overlay-badge">
                      <span className="order-pill">#{video.displayOrder}</span>
                      <span className={`status-pill ${video.isActive ? "pill-active" : "pill-hidden"}`}>
                        {video.isActive ? "Active on Web" : "Hidden"}
                      </span>
                    </div>
                    <button
                      type="button"
                      className="video-play-overlay-btn"
                      onClick={() => setPreviewModalVideo(video)}
                      title="Play Preview"
                    >
                      ▶
                    </button>
                  </div>

                  {/* Video Metadata */}
                  <div className="video-card-body">
                    <div className="video-title-row">
                      <h3 className="video-title" title={video.title}>
                        {video.title}
                      </h3>
                    </div>

                    {video.description && <p className="video-desc">{video.description}</p>}

                    <div className="video-specs-meta">
                      <span className="spec-tag">📁 {sizeMb} MB</span>
                      <span className="spec-tag">📅 {new Date(video.createdAt).toLocaleDateString()}</span>
                      <span className="spec-tag code-font" title={video.objectKey}>
                        🔑 {video.objectKey.split("/").pop()}
                      </span>
                    </div>
                  </div>

                  {/* Reorder and Management Controls */}
                  <div className="video-card-actions">
                    <div className="reorder-btn-group">
                      <button
                        type="button"
                        className="reorder-btn"
                        disabled={isFirst}
                        onClick={() => handleMoveOrder(video, "up")}
                        title="Move Earlier"
                      >
                        ▲
                      </button>
                      <button
                        type="button"
                        className="reorder-btn"
                        disabled={isLast}
                        onClick={() => handleMoveOrder(video, "down")}
                        title="Move Later"
                      >
                        ▼
                      </button>
                    </div>

                    <div className="manage-btn-group">
                      <button
                        type="button"
                        className={`action-pill-btn ${video.isActive ? "btn-hide" : "btn-show"}`}
                        onClick={() => handleToggleActive(video)}
                        title={video.isActive ? "Hide from website" : "Show on website"}
                      >
                        {video.isActive ? "👁️ Hide" : "👁️ Show"}
                      </button>

                      <button
                        type="button"
                        className="action-pill-btn btn-edit"
                        onClick={() => handleOpenEdit(video)}
                        title="Edit title & description"
                      >
                        ✏️ Edit
                      </button>

                      <button
                        type="button"
                        className="action-pill-btn btn-replace"
                        onClick={() => handleOpenReplace(video)}
                        title="Replace video file in R2"
                      >
                        🔄 Replace
                      </button>

                      <button
                        type="button"
                        className="action-pill-btn btn-delete"
                        onClick={() => handleOpenDelete(video)}
                        title="Permanently delete from R2 & DB"
                      >
                        🗑️ Delete
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* --- UPLOAD NEW VIDEO MODAL --- */}
      {uploadModalOpen && (
        <div className="admin-modal-backdrop" onClick={() => !uploading && setUploadModalOpen(false)}>
          <div className="admin-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Upload {activeTab === "ShortVideos" ? "Short Dance Video" : "Gallery Video"}</h2>
              <button
                type="button"
                className="modal-close-btn"
                disabled={uploading}
                onClick={() => setUploadModalOpen(false)}
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleUploadSubmit} className="modal-form">
              {uploadError && <div className="alert alert-danger">{uploadError}</div>}

              <div className="form-group">
                <label className="form-label">
                  Video File (MP4, WebM, MOV) <span className="text-danger">*</span>
                </label>
                <input
                  type="file"
                  accept="video/mp4,video/webm,video/quicktime"
                  className="form-control file-input"
                  onChange={(e) => setUploadFile(e.target.files[0])}
                  disabled={uploading}
                  required
                />
                <span className="field-hint">
                  {activeTab === "ShortVideos"
                    ? "Maximum size: 25 MB · Recommended duration: 10–20 seconds (H.264 MP4)"
                    : "Maximum size: 100 MB · Recommended duration: 60–90 seconds (H.264 MP4)"}
                </span>
              </div>

              <div className="form-group">
                <label className="form-label">
                  Video Title <span className="text-danger">*</span>
                </label>
                <input
                  type="text"
                  className="form-control"
                  placeholder="e.g. Contemporary Routine with Master Raghav"
                  value={uploadTitle}
                  onChange={(e) => setUploadTitle(e.target.value)}
                  disabled={uploading}
                  maxLength={150}
                  required
                />
              </div>

              <div className="form-group">
                <label className="form-label">Description (Optional)</label>
                <textarea
                  className="form-control"
                  rows="2"
                  placeholder="Brief context, choreography credits, or workshop highlight..."
                  value={uploadDesc}
                  onChange={(e) => setUploadDesc(e.target.value)}
                  disabled={uploading}
                  maxLength={500}
                />
              </div>

              <div className="form-row">
                <div className="form-group col-half">
                  <label className="form-label">Section</label>
                  <input
                    type="text"
                    className="form-control"
                    value={activeTab === "ShortVideos" ? "Short Dance Videos" : "Gallery Showcase"}
                    disabled
                  />
                </div>
                <div className="form-group col-half">
                  <label className="form-label">Display Order</label>
                  <input
                    type="number"
                    className="form-control"
                    value={uploadOrder}
                    onChange={(e) => setUploadOrder(e.target.value)}
                    disabled={uploading}
                    min="1"
                  />
                </div>
              </div>

              <div className="modal-actions">
                <button
                  type="button"
                  className="btn btn-secondary"
                  disabled={uploading}
                  onClick={() => setUploadModalOpen(false)}
                >
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary" disabled={uploading}>
                  {uploading ? "Uploading to Cloudflare R2..." : "Upload to R2"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- REPLACE VIDEO FILE MODAL --- */}
      {replaceModalOpen && selectedVideo && (
        <div className="admin-modal-backdrop" onClick={() => !replacing && setReplaceModalOpen(false)}>
          <div className="admin-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Replace Video File in Cloudflare R2</h2>
              <button
                type="button"
                className="modal-close-btn"
                disabled={replacing}
                onClick={() => setReplaceModalOpen(false)}
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleReplaceSubmit} className="modal-form">
              {replaceError && <div className="alert alert-danger">{replaceError}</div>}

              <div className="replace-warning-box">
                <strong>Target Video:</strong> "{selectedVideo.title}"
                <p>
                  Uploading a replacement video will <strong>permanently delete the old file</strong> from Cloudflare R2
                  and swap it with the new file, keeping the title and display order intact.
                </p>
              </div>

              <div className="form-group">
                <label className="form-label">
                  Choose New Video File <span className="text-danger">*</span>
                </label>
                <input
                  type="file"
                  accept="video/mp4,video/webm,video/quicktime"
                  className="form-control file-input"
                  onChange={(e) => setReplaceFile(e.target.files[0])}
                  disabled={replacing}
                  required
                />
                <span className="field-hint">
                  {selectedVideo.section === "ShortVideos" ? "Max size: 25 MB" : "Max size: 100 MB"}
                </span>
              </div>

              <div className="modal-actions">
                <button
                  type="button"
                  className="btn btn-secondary"
                  disabled={replacing}
                  onClick={() => setReplaceModalOpen(false)}
                >
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary" disabled={replacing}>
                  {replacing ? "Swapping in R2..." : "Confirm & Replace in R2"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- EDIT METADATA MODAL --- */}
      {editModalOpen && selectedVideo && (
        <div className="admin-modal-backdrop" onClick={() => !editing && setEditModalOpen(false)}>
          <div className="admin-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Edit Video Metadata</h2>
              <button
                type="button"
                className="modal-close-btn"
                disabled={editing}
                onClick={() => setEditModalOpen(false)}
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleEditSubmit} className="modal-form">
              <div className="form-group">
                <label className="form-label">
                  Video Title <span className="text-danger">*</span>
                </label>
                <input
                  type="text"
                  className="form-control"
                  value={editTitle}
                  onChange={(e) => setEditTitle(e.target.value)}
                  disabled={editing}
                  maxLength={150}
                  required
                />
              </div>

              <div className="form-group">
                <label className="form-label">Description</label>
                <textarea
                  className="form-control"
                  rows="3"
                  value={editDesc}
                  onChange={(e) => setEditDesc(e.target.value)}
                  disabled={editing}
                  maxLength={500}
                />
              </div>

              <div className="form-group">
                <label className="form-label">Display Order</label>
                <input
                  type="number"
                  className="form-control"
                  value={editOrder}
                  onChange={(e) => setEditOrder(e.target.value)}
                  disabled={editing}
                  min="0"
                />
              </div>

              <div className="modal-actions">
                <button
                  type="button"
                  className="btn btn-secondary"
                  disabled={editing}
                  onClick={() => setEditModalOpen(false)}
                >
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary" disabled={editing}>
                  {editing ? "Saving..." : "Save Changes"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- DELETE CONFIRMATION MODAL --- */}
      {deleteModalOpen && selectedVideo && (
        <div className="admin-modal-backdrop" onClick={() => !deleting && setDeleteModalOpen(false)}>
          <div className="admin-modal-card modal-danger" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>⚠️ Permanently Delete Video?</h2>
              <button
                type="button"
                className="modal-close-btn"
                disabled={deleting}
                onClick={() => setDeleteModalOpen(false)}
              >
                ✕
              </button>
            </div>

            <div className="delete-body">
              <p>
                Are you sure you want to permanently delete <strong>"{selectedVideo.title}"</strong>?
              </p>
              <div className="delete-warning-banner">
                <span className="warning-icon">🚨</span>
                <div>
                  <strong>Dual Deletion Execution:</strong>
                  <ul>
                    <li>The MP4 video file will be <strong>permanently deleted from Cloudflare R2</strong>.</li>
                    <li>The metadata record will be removed from <strong>Neon PostgreSQL</strong>.</li>
                    <li>This action cannot be undone.</li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="modal-actions">
              <button
                type="button"
                className="btn btn-secondary"
                disabled={deleting}
                onClick={() => setDeleteModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-danger"
                disabled={deleting}
                onClick={handleDeleteConfirm}
              >
                {deleting ? "Deleting from R2..." : "Yes, Delete Permanently"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* --- PREVIEW MODAL --- */}
      {previewModalVideo && (
        <div className="admin-modal-backdrop" onClick={() => setPreviewModalVideo(null)}>
          <div className="video-preview-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="preview-header">
              <h3>{previewModalVideo.title}</h3>
              <button type="button" className="modal-close-btn" onClick={() => setPreviewModalVideo(null)}>
                ✕
              </button>
            </div>
            <div className="preview-video-container">
              <video
                src={previewModalVideo.publicUrl}
                controls
                autoPlay
                className="preview-video-element"
              />
            </div>
            {previewModalVideo.description && (
              <p className="preview-desc">{previewModalVideo.description}</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
