import React, { useState, useEffect, useRef } from "react";
import { RotateCw, Plus } from "lucide-react";
import { adminApi } from "../../services/adminApi";
import { API_BASE_URL } from "../../config/api";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import "./AdminVideos.css";

const DISPLAY_SECTIONS = [
  { id: "HomepageScrolling", label: "Hero Banner",            icon: "🎞️", group: "Homepage", hint: "Homepage Hero Scrolling Photos & Videos" },
  { id: "HomepageReels",    label: "Short Dance Reels",       icon: "🎬", group: "Homepage", hint: "9:16 Portrait Short Videos (max 30-40s)" },
  { id: "GalleryImages",   label: "Gallery Photos",           icon: "📸", group: "Gallery",  hint: "Public Masonry Photo Gallery" },
  { id: "GalleryVideos",   label: "Gallery Videos",           icon: "🎭", group: "Gallery",  hint: "Public Showcase Videos" },
  { id: "Workshop",        label: "Workshop Media",           icon: "💃", group: "Studio",   hint: "Workshop posters and media" },
  { id: "Events",          label: "Events & Festivals",       icon: "🎪", group: "Studio",   hint: "Events and performances" },
  { id: "AboutEthos",      label: "About Studio",             icon: "🏛️", group: "Studio",   hint: "About page visuals" },
  { id: "Trainers",        label: "Faculty / Trainers",       icon: "⭐", group: "Studio",   hint: "Faculty profile pictures" },
  { id: "Draft",           label: "Hidden / Draft",           icon: "🔒", group: "System",   hint: "Assets not published on website" },
];

const LAYOUT_TYPES = [
  { id: "Square", label: "Square (1:1)" },
  { id: "Portrait", label: "Portrait (3:4 Poster)" },
  { id: "Landscape", label: "Landscape (16:9 Wide)" },
  { id: "Featured", label: "Featured (Spans 2 Columns)" },
];

const CATEGORIES = [
  { id: "General", label: "General" },
  { id: "Workshops", label: "Workshops" },
  { id: "Performances", label: "Performances" },
  { id: "Community", label: "Community" },
  { id: "Studio", label: "Studio" },
  { id: "BehindTheScenes", label: "Behind The Scenes" },
];

const FOCAL_POINTS = [
  { id: "center", label: "Center" },
  { id: "top", label: "Top" },
  { id: "bottom", label: "Bottom" },
  { id: "left", label: "Left" },
  { id: "right", label: "Right" },
];

export default function AdminVideos() {
  const [activeTab, setActiveTab] = useState("HomepageScrolling"); // Section ID, "all", or "archived"
  const [mediaList, setMediaList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [actionSuccess, setActionSuccess] = useState(null);

  // Modals state
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [previewMedia, setPreviewMedia] = useState(null);
  const [selectedItem, setSelectedItem] = useState(null);

  // Form states
  const fileInputRef = useRef(null);
  const [isDragging, setIsDragging] = useState(false);
  const [uploadFile, setUploadFile] = useState(null);
  const [uploadTitle, setUploadTitle] = useState("");
  const [uploadCaption, setUploadCaption] = useState("");
  const [uploadAltText, setUploadAltText] = useState("");
  const [uploadFocalPoint, setUploadFocalPoint] = useState("center");
  const [uploadSection, setUploadSection] = useState("Draft");
  const [uploadCategory, setUploadCategory] = useState("General");
  const [uploadLayout, setUploadLayout] = useState("Square");
  const [uploadOrder, setUploadOrder] = useState("");
  const [uploadIsPublished, setUploadIsPublished] = useState(true);
  const [uploadIsFeatured, setUploadIsFeatured] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState(null);

  // Edit form state
  const [editTitle, setEditTitle] = useState("");
  const [editCaption, setEditCaption] = useState("");
  const [editAltText, setEditAltText] = useState("");
  const [editFocalPoint, setEditFocalPoint] = useState("center");
  const [editSection, setEditSection] = useState("Draft");
  const [editCategory, setEditCategory] = useState("General");
  const [editLayout, setEditLayout] = useState("Square");
  const [editOrder, setEditOrder] = useState(0);
  const [editIsPublished, setEditIsPublished] = useState(true);
  const [editIsFeatured, setEditIsFeatured] = useState(false);
  const [editing, setEditing] = useState(false);

  // Delete / Archive state
  const [deleting, setDeleting] = useState(false);
  const [archivingId, setArchivingId] = useState(null);

  const loadMedia = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getAdminMedia({ pageSize: 200 });
      const items = res?.items || (Array.isArray(res) ? res : []);
      setMediaList(items);
    } catch (err) {
      setError(err.message || "Failed to load media assets.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMedia();
  }, []);

  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        if (!uploading) setUploadModalOpen(false);
        if (!editing) setEditModalOpen(false);
        if (!deleting) setDeleteModalOpen(false);
        setPreviewMedia(null);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [uploading, editing, deleting]);

  const showNotification = (msg) => {
    setActionSuccess(msg);
    setTimeout(() => setActionSuccess(null), 5000);
  };

  // Helper: get primary section from placements
  const getPrimarySection = (item) => {
    if (item.placements && item.placements.length > 0) return item.placements[0].section || "";
    return item.section || ""; // legacy fallback
  };

  // Segregate active vs archived
  const activeItems = mediaList.filter((m) => !m.isArchived);
  const archivedItems = mediaList.filter((m) => m.isArchived);

  // Filtered by current tab
  const displayedItems = (
    activeTab === "archived"
      ? archivedItems
      : activeTab === "all"
      ? activeItems
      : activeItems.filter((m) => {
          if (m.placements && m.placements.length > 0) {
            return m.placements.some((p) => (p.section || "").toLowerCase() === activeTab.toLowerCase());
          }
          return (m.section || "").toLowerCase() === activeTab.toLowerCase();
        })
  ).sort((a, b) => {
    const aOrder = a.placements?.find((p) => p.section?.toLowerCase() === activeTab.toLowerCase())?.displayOrder ?? a.displayOrder ?? 0;
    const bOrder = b.placements?.find((p) => p.section?.toLowerCase() === activeTab.toLowerCase())?.displayOrder ?? b.displayOrder ?? 0;
    return aOrder - bOrder;
  });

  const totalActive = activeItems.length;
  const totalPublished = activeItems.filter((m) =>
    m.placements?.some((p) => p.isPublished) ?? m.isPublished
  ).length;
  const totalArchived = archivedItems.length;
  const totalStorageBytes = mediaList.reduce((acc, m) => acc + (m.fileSizeBytes || 0), 0);

  const formatFileSize = (bytes) => {
    if (!bytes || bytes === 0) return "0.0 KB";
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const getEffectiveMediaUrl = (item) => {
    if (!item) return "";
    const url = item.thumbnailUrl || item.publicUrl || "";
    if (url && !url.includes("media.ethosdancestudio.com")) {
      return url;
    }
    const apiBase = API_BASE_URL;
    return `${apiBase}/api/media/content/${item.id}`;
  };

  // --- Handlers ---
  const handleOpenUpload = () => {
    const targetSec = activeTab === "all" || activeTab === "archived" ? "HomepageScrolling" : activeTab;
    setUploadFile(null);
    setUploadTitle("");
    setUploadCaption("");
    setUploadAltText("");
    setUploadFocalPoint("center");
    setUploadSection(targetSec);
    setUploadCategory("General");
    setUploadLayout("Square");
    setUploadOrder(displayedItems.length + 1);
    setUploadIsPublished(true);
    setUploadIsFeatured(false);
    setUploadError(null);
    setUploadModalOpen(true);
  };

  const handleFileChange = (file) => {
    setUploadFile(file);
    if (file && !uploadTitle.trim()) {
      const suggested = file.name.replace(/\.[^/.]+$/, "").replace(/[-_]/g, " ");
      setUploadTitle(suggested);
    }
  };

  const handleUploadSubmit = async (e) => {
    e.preventDefault();
    if (!uploadFile) {
      setUploadError("Please select an image or video file.");
      return;
    }

    const effectiveTitle = uploadTitle.trim() || uploadFile.name.replace(/\.[^/.]+$/, "").replace(/[-_]/g, " ");

    setUploading(true);
    setUploadError(null);

    const formData = new FormData();
    formData.append("file", uploadFile);
    formData.append("section", uploadSection);
    formData.append("title", effectiveTitle);
    formData.append("caption", uploadCaption.trim());
    formData.append("altText", effectiveTitle);
    formData.append("focalPoint", "center");
    formData.append("category", "General");
    formData.append("layoutType", "Square");
    formData.append("displayOrder", uploadOrder || 0);
    formData.append("isPublished", uploadIsPublished);
    formData.append("isFeatured", uploadIsFeatured);

    try {
      await adminApi.uploadAdminMedia(formData);
      setUploadModalOpen(false);
      showNotification(`"${effectiveTitle}" successfully uploaded to Cloudflare R2!`);
      await loadMedia();
    } catch (err) {
      setUploadError(err.message || "Failed to upload media to Cloudflare R2.");
    } finally {
      setUploading(false);
    }
  };

  const handleOpenEdit = (item) => {
    setSelectedItem(item);
    setEditTitle(item.title || "");
    setEditCaption(item.caption || "");
    setEditAltText(item.altText || item.title || "");
    setEditFocalPoint(item.focalPoint || "center");
    const activePlacement = item.placements?.find(
      (p) => (p.section || "").toLowerCase() === activeTab.toLowerCase()
    ) || item.placements?.[0];
    setEditSection(activePlacement?.section || item.section || "Draft");
    setEditCategory(item.category || "General");
    setEditLayout(item.layoutType || "Square");
    setEditOrder(activePlacement?.displayOrder ?? item.displayOrder ?? 0);
    setEditIsPublished(activePlacement?.isPublished ?? item.isPublished ?? true);
    setEditIsFeatured(activePlacement?.isFeatured ?? item.isFeatured ?? false);
    setEditModalOpen(true);
  };

  const handleEditSubmit = async (e) => {
    e.preventDefault();
    if (!selectedItem) return;
    setEditing(true);
    try {
      await adminApi.updateAdminMedia(selectedItem.id, {
        title: editTitle.trim(),
        caption: editCaption.trim(),
        altText: editAltText.trim() || editTitle.trim(),
        focalPoint: editFocalPoint,
        category: editCategory,
        layoutType: editLayout,
      });

      const activePlacement = selectedItem.placements?.find(
        (p) => (p.section || "").toLowerCase() === activeTab.toLowerCase()
      ) || selectedItem.placements?.[0];

      if (activePlacement) {
        await adminApi.updateMediaPlacement(selectedItem.id, activePlacement.id, {
          section: editSection,
          displayOrder: parseInt(editOrder, 10) || 0,
          isPublished: editIsPublished,
          isFeatured: editIsFeatured,
        });
      } else if (editSection) {
        await adminApi.addMediaPlacement(selectedItem.id, {
          section: editSection,
          displayOrder: parseInt(editOrder, 10) || 0,
          isPublished: editIsPublished,
          isFeatured: editIsFeatured,
        });
      }

      setEditModalOpen(false);
      showNotification("Media details updated successfully.");
      await loadMedia();
    } catch (err) {
      setError(err.message || "Failed to update media.");
    } finally {
      setEditing(false);
    }
  };

  const handleTogglePublish = async (item) => {
    try {
      // Find the active placement for the current section (or first placement)
      const placement = item.placements?.find(
        (p) => (p.section || "").toLowerCase() === activeTab.toLowerCase()
      ) || item.placements?.[0];

      if (!placement) {
        setError("No placement found to toggle visibility.");
        return;
      }

      const newStatus = !placement.isPublished;
      await adminApi.togglePlacementPublish(item.id, placement.id, newStatus);
      showNotification(
        newStatus ? `"${item.title}" is now PUBLISHED on website.` : `"${item.title}" is now HIDDEN.`
      );
      await loadMedia();
    } catch (err) {
      setError(err.message || "Failed to toggle media visibility.");
    }
  };

  const handleArchive = async (item) => {
    setArchivingId(item.id);
    try {
      await adminApi.archiveAdminMedia(item.id);
      showNotification(`"${item.title}" moved to archive.`);
      await loadMedia();
    } catch (err) {
      setError(err.message || "Failed to archive media item.");
    } finally {
      setArchivingId(null);
    }
  };

  const handleRestore = async (item) => {
    setArchivingId(item.id);
    try {
      await adminApi.restoreAdminMedia(item.id);
      showNotification(`"${item.title}" restored from archive.`);
      await loadMedia();
    } catch (err) {
      setError(err.message || "Failed to restore media item.");
    } finally {
      setArchivingId(null);
    }
  };

  const handleOpenDelete = (item) => {
    setSelectedItem(item);
    setDeleteModalOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!selectedItem) return;
    setDeleting(true);
    try {
      await adminApi.permanentDeleteAdminMedia(selectedItem.id);
      setDeleteModalOpen(false);
      showNotification(`"${selectedItem.title}" permanently deleted from Cloudflare R2 and DB.`);
      await loadMedia();
    } catch (err) {
      setError(err.message || "Failed to permanently delete media.");
    } finally {
      setDeleting(false);
    }
  };

  const handleMoveOrder = async (item, direction) => {
    const list = [...displayedItems];
    const currentIndex = list.findIndex((m) => m.id === item.id);
    if (currentIndex === -1) return;

    const targetIndex = direction === "up" ? currentIndex - 1 : currentIndex + 1;
    if (targetIndex < 0 || targetIndex >= list.length) return;

    const targetItem = list[targetIndex];

    // Find placements for current section
    const getPlacementOrder = (m) => {
      const p = m.placements?.find((pl) => (pl.section || "").toLowerCase() === activeTab.toLowerCase());
      return p?.displayOrder ?? 0;
    };
    const getPlacementId = (m) => {
      const p = m.placements?.find((pl) => (pl.section || "").toLowerCase() === activeTab.toLowerCase());
      return p?.id;
    };

    const itemPlacementId   = getPlacementId(item);
    const targetPlacementId = getPlacementId(targetItem);

    if (!itemPlacementId || !targetPlacementId) {
      setError("Cannot reorder: placement not found for section.");
      return;
    }

    const updatedItems = [
      { placementId: itemPlacementId,   displayOrder: getPlacementOrder(targetItem) },
      { placementId: targetPlacementId, displayOrder: getPlacementOrder(item) },
    ];

    try {
      await adminApi.reorderAdminMedia({ section: activeTab, items: updatedItems });
      await loadMedia();
    } catch (err) {
      setError(err.message || "Failed to reorder media items.");
    }
  };

  return (
    <div className="admin-videos-page">
      {/* Page Header */}
      <div className="admin-page-header">
        <div>
          <div className="admin-breadcrumb-tag">MEDIA & VISUAL ASSETS · CLOUDFLARE R2</div>
          <h1 className="admin-page-title">Studio Media & Gallery Management</h1>
          <p className="admin-page-subtitle">
            Single source of visual content for the Ethos website: homepage hero banners, short dance reels, gallery, and section-specific media.
          </p>
        </div>
        <div className="admin-header-actions">
          <button
            type="button"
            className="btn-media-header btn-refresh"
            onClick={loadMedia}
            disabled={loading}
          >
            <RotateCw size={14} className={loading ? "animate-spin" : ""} />
            <span>{loading ? "Refreshing..." : "Refresh"}</span>
          </button>
          <button
            type="button"
            className="btn-media-header btn-upload-primary"
            onClick={handleOpenUpload}
          >
            <Plus size={16} />
            <span>Upload Media Asset</span>
          </button>
        </div>
      </div>

      {/* KPI Overview Grid */}
      <div className="admin-kpi-grid">
        <AdminKpiCard
          label="Active Media Items"
          value={totalActive}
          tone="brand"
          sublabel="Live in Cloudflare R2"
          icon="🖼️"
        />
        <AdminKpiCard
          label="Published Live"
          value={`${totalPublished} / ${totalActive}`}
          tone="success"
          sublabel="Visible on public website"
          icon="👁️"
        />
        <AdminKpiCard
          label="Archived Items"
          value={totalArchived}
          tone="neutral"
          sublabel="Soft-archived assets"
          icon="🗄️"
        />
        <AdminKpiCard
          label="Storage Used"
          value={formatFileSize(totalStorageBytes)}
          tone="info"
          sublabel="Cloudflare R2 Bucket"
          icon="☁️"
        />
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {actionSuccess && <div className="alert alert-success">{actionSuccess}</div>}

      {/* Section Tabs */}
      <div className="video-tabs-bar">
        <div className="video-tabs-group">
          {DISPLAY_SECTIONS.map((sec) => {
            const count = activeItems.filter((m) => {
              if (m.placements && m.placements.length > 0) return m.placements.some((p) => (p.section || "").toLowerCase() === sec.id.toLowerCase());
              return (m.section || "").toLowerCase() === sec.id.toLowerCase();
            }).length;
            return (
              <button
                key={sec.id}
                type="button"
                className={`video-tab-btn ${activeTab === sec.id ? "active" : ""}`}
                onClick={() => setActiveTab(sec.id)}
              >
                <span className="tab-icon">{sec.icon}</span>
                <span className="tab-label">{sec.label}</span>
                <span className="tab-count-pill">{count}</span>
              </button>
            );
          })}
          <div className="video-tab-divider"></div>
          <button
            type="button"
            className={`video-tab-btn ${activeTab === "all" ? "active" : ""}`}
            onClick={() => setActiveTab("all")}
          >
            <span className="tab-icon">🌐</span>
            <span className="tab-label">All Active</span>
            <span className="tab-count-pill">{totalActive}</span>
          </button>
          <button
            type="button"
            className={`video-tab-btn tab-archived ${activeTab === "archived" ? "active" : ""}`}
            onClick={() => setActiveTab("archived")}
          >
            <span className="tab-icon">🗄️</span>
            <span className="tab-label">Archived</span>
            <span className="tab-count-pill pill-archived-count">{totalArchived}</span>
          </button>
        </div>

        {/* Active Section Info Guideline */}
        <div className="section-active-guideline-bar">
          <div className="guideline-main">
            <span className="guideline-badge">
              {DISPLAY_SECTIONS.find((s) => s.id === activeTab)?.icon || "📂"} Section:{" "}
              <strong>{DISPLAY_SECTIONS.find((s) => s.id === activeTab)?.label || (activeTab === "all" ? "All Active" : "Archived")}</strong>
            </span>
            <span className="guideline-text">
              {activeTab === "HomepageScrolling" && "Powers the cycling banner on the Homepage Hero. Accepts photos (≤10MB) and short muted background videos (≤25MB, ≤30s)."}
              {activeTab === "HomepageReels" && "Powers 'Short Dance Videos' on the Homepage. Strictly portrait 9:16 videos only (MP4, ≤25MB, max 30-40s duration)."}
              {activeTab === "GalleryImages" && "Powers the public Masonry Photo Gallery (/gallery). Accepts high-resolution dance photos (≤10MB)."}
              {activeTab === "GalleryVideos" && "Powers the public Showcase Videos (/gallery). Accepts choreography & masterclass showcase videos (≤100MB)."}
              {activeTab === "Workshop" && "Media assets specific to workshops, choreography sessions, and masterclass showcases."}
              {activeTab === "Events" && "Media assets from studio events, battle jams, annual showcases, and festivals."}
              {activeTab === "AboutEthos" && "Visual assets for the studio About page and facility showcases."}
              {activeTab === "Trainers" && "Faculty and master instructor profile pictures (1:1 square recommended, ≤5MB)."}
              {activeTab === "Draft" && "Hidden draft media stored in Cloudflare R2, not currently visible on any public website section."}
              {activeTab === "all" && "All active studio visual media across all sections."}
              {activeTab === "archived" && "Soft-archived media assets preserved safely in storage."}
            </span>
          </div>
          {activeTab !== "archived" && (
            <button type="button" className="btn-sm-upload-tab" onClick={handleOpenUpload}>
              + Upload to {DISPLAY_SECTIONS.find((s) => s.id === activeTab)?.label || "Section"}
            </button>
          )}
        </div>
      </div>

      {/* Media Items List */}
      <div className="videos-container">
        {loading ? (
          <div className="videos-loading-state">
            <div className="spinner"></div>
            <p>Loading media assets from Cloudflare R2...</p>
          </div>
        ) : displayedItems.length === 0 ? (
          <div className="videos-empty-state">
            <div className="empty-icon">{activeTab === "archived" ? "🗄️" : "📁"}</div>
            <h3>{activeTab === "archived" ? "No archived media items" : `No custom uploads in ${DISPLAY_SECTIONS.find((s) => s.id === activeTab)?.label || "this section"} yet`}</h3>
            <p>
              {activeTab === "HomepageScrolling"
                ? "Default curated studio scrolling photos & hero video are currently powering the homepage hero. Upload your own photos or videos here to replace or add to the banner!"
                : activeTab === "HomepageReels"
                ? "Default curated short dance reels are currently displaying on the homepage. Upload your own 9:16 portrait videos (max 30-40s) here!"
                : activeTab === "archived"
                ? "Archived items will be stored safely here without appearing on the website."
                : "Upload photos or video reels to display on the Ethos public website."}
            </p>
            {activeTab !== "archived" && (
              <button
                type="button"
                className="btn btn-primary btn-empty-upload"
                onClick={handleOpenUpload}
              >
                + Upload to {DISPLAY_SECTIONS.find((s) => s.id === activeTab)?.label || "Section"}
              </button>
            )}
          </div>
        ) : (
          <div className="videos-grid">
            {displayedItems.map((item, idx) => {
              const sizeMb = ((item.fileSizeBytes || 0) / (1024 * 1024)).toFixed(1);
              const isFirst = idx === 0;
              const isLast = idx === displayedItems.length - 1;
              const isVideo = (item.mediaType || "").toLowerCase() === "video";
              const isArchived = item.isArchived;

              // Get placement-specific values for current tab
              const activePlacement = item.placements?.find(
                (p) => (p.section || "").toLowerCase() === activeTab.toLowerCase()
              ) || item.placements?.[0];
              const itemIsPublished = activePlacement?.isPublished ?? item.isPublished ?? true;
              const itemIsFeatured  = activePlacement?.isFeatured  ?? item.isFeatured  ?? false;
              const itemDisplayOrder= activePlacement?.displayOrder ?? item.displayOrder ?? 0;

              return (
                <div
                  key={item.id}
                  className={`video-card ${isArchived ? "is-archived" : itemIsPublished ? "is-active" : "is-hidden"}`}
                >
                  {/* Card Thumbnail / Player Preview */}
                  <div className="video-player-wrap" onClick={() => setPreviewMedia(item)}>
                    {isVideo ? (
                      <video
                        src={getEffectiveMediaUrl(item)}
                        className="video-player-element"
                        muted
                        preload="metadata"
                        playsInline
                        onError={(e) => {
                          const apiBase = API_BASE_URL;
                          const fallback = `${apiBase}/api/media/content/${item.id}`;
                          if (e.target.src !== fallback) e.target.src = fallback;
                        }}
                      />
                    ) : (
                      <img
                        src={getEffectiveMediaUrl(item)}
                        alt={item.altText || item.title}
                        className="video-player-element"
                        style={{ objectFit: "cover", objectPosition: item.focalPoint || "center" }}
                        onError={(e) => {
                          const apiBase = API_BASE_URL;
                          const fallback = `${apiBase}/api/media/content/${item.id}`;
                          if (e.target.src !== fallback) e.target.src = fallback;
                        }}
                      />
                    )}

                    <div className="video-overlay-badge">
                      <span className="order-pill">#{itemDisplayOrder}</span>
                      {isArchived ? (
                        <span className="status-pill pill-archived">Archived</span>
                      ) : (
                        <span className={`status-pill ${itemIsPublished ? "pill-active" : "pill-hidden"}`}>
                          {itemIsPublished ? "Published" : "Hidden"}
                        </span>
                      )}
                      {itemIsFeatured && <span className="status-pill pill-featured">★ Featured</span>}
                    </div>

                    <button
                      type="button"
                      className="video-play-overlay-btn"
                      onClick={(e) => {
                        e.stopPropagation();
                        setPreviewMedia(item);
                      }}
                      title="Enlarge Media"
                    >
                      {isVideo ? "▶" : "🔍"}
                    </button>
                  </div>

                  {/* Media Metadata */}
                  <div className="video-card-body">
                    <div className="video-title-row">
                      <h3 className="video-title" title={item.title}>
                        {item.title}
                      </h3>
                    </div>

                    {item.caption && <p className="video-desc">{item.caption}</p>}

                    <div className="video-specs-meta">
                      <span className="spec-tag">🏷️ {item.category || "General"}</span>
                      <span className="spec-tag">📐 {item.layoutType || "Square"}</span>
                      <span className="spec-tag">📁 {formatFileSize(item.fileSizeBytes)}</span>
                      <span className="spec-tag">{isVideo ? "🎬 Video" : "📷 Photo"}</span>
                      {item.focalPoint && item.focalPoint !== "center" && (
                        <span className="spec-tag">🎯 {item.focalPoint}</span>
                      )}
                      {item.placements && item.placements.length > 0 && item.placements.map((p) => (
                        <span key={p.id} className="spec-tag" style={{ background: "rgba(99,102,241,0.15)", color: "#818cf8", fontWeight: 600 }}>
                          📌 {p.section}{!p.isPublished ? " (Hidden)" : ""}
                        </span>
                      ))}
                    </div>
                  </div>

                  {/* Reorder and Management Controls */}
                  <div className="video-card-actions">
                    {!isArchived ? (
                      <div className="order-buttons-group">
                        <button
                          type="button"
                          className="btn-icon-order"
                          disabled={isFirst}
                          onClick={() => handleMoveOrder(item, "up")}
                          title="Move Earlier"
                        >
                          ▲
                        </button>
                        <button
                          type="button"
                          className="btn-icon-order"
                          disabled={isLast}
                          onClick={() => handleMoveOrder(item, "down")}
                          title="Move Later"
                        >
                          ▼
                        </button>
                      </div>
                    ) : (
                      <div className="archived-indicator-label">Archived</div>
                    )}

                    <div className="crud-buttons-group">
                      {!isArchived ? (
                        <>
                          <button
                            type="button"
                            className={`btn-sm-action ${itemIsPublished ? "btn-toggle-hide" : "btn-toggle-show"}`}
                            onClick={() => handleTogglePublish(item)}
                            title={itemIsPublished ? "Hide from website" : "Publish to website"}
                          >
                            {itemIsPublished ? "👁️ Hide" : "👁️ Show"}
                          </button>

                          <button
                            type="button"
                            className="btn-sm-action btn-edit"
                            onClick={() => handleOpenEdit(item)}
                            title="Edit metadata"
                          >
                            ✏️ Edit
                          </button>

                          <button
                            type="button"
                            className="btn-sm-action btn-archive"
                            disabled={archivingId === item.id}
                            onClick={() => handleArchive(item)}
                            title="Move to archive (soft delete)"
                          >
                            {archivingId === item.id ? "..." : "📦 Archive"}
                          </button>

                          <button
                            type="button"
                            className="btn-sm-action btn-delete"
                            onClick={() => handleOpenDelete(item)}
                            title="Permanently delete from Cloudflare R2 & DB"
                          >
                            🗑️ Delete
                          </button>
                        </>
                      ) : (
                        <>
                          <button
                            type="button"
                            className="btn-sm-action btn-restore"
                            disabled={archivingId === item.id}
                            onClick={() => handleRestore(item)}
                            title="Restore from archive"
                          >
                            {archivingId === item.id ? "..." : "♻️ Restore"}
                          </button>

                          <button
                            type="button"
                            className="btn-sm-action btn-delete"
                            onClick={() => handleOpenDelete(item)}
                            title="Permanently delete from Cloudflare R2 & DB"
                          >
                            🗑️ Delete
                          </button>
                        </>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* --- UPLOAD NEW MEDIA MODAL --- */}
      {uploadModalOpen && (
        <div className="admin-modal-backdrop" onClick={() => !uploading && setUploadModalOpen(false)}>
          <div className="admin-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div>
                <h2>Upload Media Asset</h2>
                <div style={{ display: "flex", alignItems: "center", gap: "8px", marginTop: "4px" }}>
                  <span style={{ fontSize: "12px", color: "#64748b" }}>Target Section:</span>
                  <span style={{ fontSize: "12px", fontWeight: "700", background: "#f1f5f9", padding: "2px 8px", borderRadius: "6px", color: "#0f172a" }}>
                    {DISPLAY_SECTIONS.find((s) => s.id === uploadSection)?.label || uploadSection}
                  </span>
                </div>
              </div>
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

              {/* Only show target selector if user is on 'All Active' tab */}
              {activeTab === "all" && (
                <div className="form-group">
                  <label>Section Placement *</label>
                  <select
                    className="form-control"
                    value={uploadSection}
                    onChange={(e) => setUploadSection(e.target.value)}
                  >
                    {DISPLAY_SECTIONS.map((sec) => (
                      <option key={sec.id} value={sec.id}>
                        {sec.icon} {sec.label}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {/* Target Section Rules & Specification Guidelines */}
              {(() => {
                const secObj = DISPLAY_SECTIONS.find((s) => s.id === uploadSection);
                if (!secObj) return null;
                return (
                  <div className="section-specs-banner">
                    <div className="section-specs-header">
                      <span className="specs-icon">{secObj.icon}</span>
                      <div>
                        <strong>Section Placement: {secObj.label}</strong>
                        <p>{secObj.hint}</p>
                      </div>
                    </div>
                  </div>
                );
              })()}

              {/* Drag & Drop Upload Zone */}
              <div className="form-group">
                <label>Media File (Image or Video) *</label>
                <div
                  className={`media-upload-dropzone ${isDragging ? "is-dragging" : ""} ${uploadFile ? "has-file" : ""}`}
                  onDragOver={(e) => {
                    e.preventDefault();
                    setIsDragging(true);
                  }}
                  onDragLeave={(e) => {
                    e.preventDefault();
                    setIsDragging(false);
                  }}
                  onDrop={(e) => {
                    e.preventDefault();
                    setIsDragging(false);
                    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
                      handleFileChange(e.dataTransfer.files[0]);
                    }
                  }}
                  onClick={() => !uploadFile && fileInputRef.current?.click()}
                >
                  <input
                    type="file"
                    ref={fileInputRef}
                    style={{ display: "none" }}
                    accept="image/jpeg,image/png,image/webp,video/mp4,video/webm"
                    onChange={(e) => handleFileChange(e.target.files?.[0])}
                  />

                  {!uploadFile ? (
                    <div className="dropzone-empty-content">
                      <div className="dropzone-upload-icon">☁️</div>
                      <div className="dropzone-text-group">
                        <strong className="dropzone-title">Drag & drop your file here, or click to browse</strong>
                        <span className="dropzone-subtitle">Select high quality images or videos for Ethos Studio</span>
                      </div>
                      <div className="dropzone-specs-pills">
                        <span className="spec-pill img-spec">📸 JPG, PNG, WEBP (≤ 35MB)</span>
                        <span className="spec-pill vid-spec">🎥 MP4, WEBM (≤ 100MB)</span>
                        <span className="spec-pill reels-spec">📱 Homepage Reels: 9:16 Vertical (≤ 25MB, ≤ 40s)</span>
                      </div>
                    </div>
                  ) : (
                    <div className="dropzone-file-selected-card">
                      <div className="selected-file-thumbnail">
                        {uploadFile.type?.startsWith("image/") ? (
                          <img src={URL.createObjectURL(uploadFile)} alt="Preview" />
                        ) : (
                          <div className="video-file-icon">🎬</div>
                        )}
                      </div>
                      <div className="selected-file-info">
                        <strong className="file-name-text">{uploadFile.name}</strong>
                        <div className="file-meta-row">
                          <span className="file-size-badge">{formatFileSize(uploadFile.size)}</span>
                          <span className="file-type-badge">{uploadFile.type || "Media File"}</span>
                          <span className="file-status-badge">✅ File Selected</span>
                        </div>
                      </div>
                      <button
                        type="button"
                        className="btn-change-file"
                        onClick={(e) => {
                          e.stopPropagation();
                          setUploadFile(null);
                        }}
                      >
                        Change File
                      </button>
                    </div>
                  )}
                </div>
              </div>

              <div className="form-group">
                <label>Title *</label>
                <input
                  type="text"
                  className="form-control"
                  value={uploadTitle}
                  onChange={(e) => setUploadTitle(e.target.value)}
                  placeholder="e.g. Urban Grooves Choreography Showcase"
                  required
                />
              </div>

              <div className="form-group">
                <label>Caption / Description</label>
                <textarea
                  className="form-control"
                  rows={2}
                  value={uploadCaption}
                  onChange={(e) => setUploadCaption(e.target.value)}
                  placeholder="Optional brief description for gallery and cards"
                />
              </div>

              <div className="form-grid-2col">
                <div className="form-group">
                  <label>Category</label>
                  <select
                    className="form-control"
                    value={uploadCategory}
                    onChange={(e) => setUploadCategory(e.target.value)}
                  >
                    {CATEGORIES.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="form-group">
                  <label>Layout Type</label>
                  <select
                    className="form-control"
                    value={uploadLayout}
                    onChange={(e) => setUploadLayout(e.target.value)}
                  >
                    {LAYOUT_TYPES.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="form-grid-2col">
                <div className="form-group">
                  <label>Display Order</label>
                  <input
                    type="number"
                    className="form-control"
                    value={uploadOrder}
                    onChange={(e) => setUploadOrder(e.target.value)}
                    min={1}
                  />
                </div>

                <div className="form-group" style={{ display: "flex", flexDirection: "column", justifyContent: "center" }}>
                  <label className="checkbox-label" style={{ display: "flex", alignItems: "center", gap: "8px", marginTop: "16px", cursor: "pointer" }}>
                    <input
                      type="checkbox"
                      checked={uploadIsPublished}
                      onChange={(e) => setUploadIsPublished(e.target.checked)}
                    />
                    <span>Publish Immediately to Website</span>
                  </label>
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
                  {uploading ? "Uploading to Cloudflare R2..." : "Upload Media Asset"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- EDIT MEDIA METADATA MODAL --- */}
      {editModalOpen && selectedItem && (
        <div className="admin-modal-backdrop" onClick={() => !editing && setEditModalOpen(false)}>
          <div className="admin-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div>
                <h2>Edit Media Details</h2>
                <span style={{ fontSize: "12px", color: "#64748b" }}>ID: {selectedItem.id}</span>
              </div>
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
                <label>Title *</label>
                <input
                  type="text"
                  className="form-control"
                  value={editTitle}
                  onChange={(e) => setEditTitle(e.target.value)}
                  required
                />
              </div>

              <div className="form-group">
                <label>Caption / Description</label>
                <textarea
                  className="form-control"
                  rows={2}
                  value={editCaption}
                  onChange={(e) => setEditCaption(e.target.value)}
                />
              </div>

              <div className="form-grid-2col">
                <div className="form-group">
                  <label>Category</label>
                  <select
                    className="form-control"
                    value={editCategory}
                    onChange={(e) => setEditCategory(e.target.value)}
                  >
                    {CATEGORIES.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="form-group">
                  <label>Layout Type</label>
                  <select
                    className="form-control"
                    value={editLayout}
                    onChange={(e) => setEditLayout(e.target.value)}
                  >
                    {LAYOUT_TYPES.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="form-grid-2col">
                <div className="form-group">
                  <label>Display Order (Current Section)</label>
                  <input
                    type="number"
                    className="form-control"
                    value={editOrder}
                    onChange={(e) => setEditOrder(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label>Focal Point</label>
                  <select
                    className="form-control"
                    value={editFocalPoint}
                    onChange={(e) => setEditFocalPoint(e.target.value)}
                  >
                    {FOCAL_POINTS.map((fp) => (
                      <option key={fp.id} value={fp.id}>
                        {fp.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="form-group" style={{ display: "flex", gap: "20px" }}>
                <label className="checkbox-label" style={{ display: "flex", alignItems: "center", gap: "8px", cursor: "pointer" }}>
                  <input
                    type="checkbox"
                    checked={editIsPublished}
                    onChange={(e) => setEditIsPublished(e.target.checked)}
                  />
                  <span>Published in Current Section</span>
                </label>
                <label className="checkbox-label" style={{ display: "flex", alignItems: "center", gap: "8px", cursor: "pointer" }}>
                  <input
                    type="checkbox"
                    checked={editIsFeatured}
                    onChange={(e) => setEditIsFeatured(e.target.checked)}
                  />
                  <span>Featured Asset</span>
                </label>
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
                  {editing ? "Saving Changes..." : "Save Changes"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* --- PERMANENT DELETE CONFIRMATION MODAL --- */}
      {deleteModalOpen && selectedItem && (
        <div className="admin-modal-backdrop" onClick={() => !deleting && setDeleteModalOpen(false)}>
          <div className="admin-modal-card modal-danger" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Delete "{selectedItem.title}"?</h2>
              <button
                type="button"
                className="modal-close-btn"
                disabled={deleting}
                onClick={() => setDeleteModalOpen(false)}
              >
                ✕
              </button>
            </div>

            <div className="delete-body" style={{ padding: "20px 24px" }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "12px" }}>
                <span style={{ fontSize: "13px", fontWeight: "600", color: "#64748b" }}>
                  File Size: <strong style={{ color: "#0f172a" }}>{formatFileSize(selectedItem.fileSizeBytes)}</strong>
                </span>
                {(selectedItem.fileSizeBytes || 0) > 25 * 1024 * 1024 && (
                  <span style={{ background: "#fef2f2", color: "#991b1b", fontSize: "11px", fontWeight: "700", padding: "2px 8px", borderRadius: "6px", border: "1px solid #fecaca" }}>
                    ⚠️ High Storage Asset ({formatFileSize(selectedItem.fileSizeBytes)})
                  </span>
                )}
              </div>

              <div className="delete-warning-banner" style={{ background: "#fff5f5", border: "1px solid #feb2b2", borderRadius: "10px", padding: "14px 16px", color: "#7f1d1d" }}>
                <strong style={{ fontSize: "14px", display: "block", marginBottom: "8px", color: "#991b1b" }}>
                  This will permanently delete:
                </strong>
                <ul style={{ margin: "0 0 10px 18px", padding: 0, fontSize: "13px", lineHeight: "1.6" }}>
                  <li>Media database record & all section placements (Neon PostgreSQL)</li>
                  <li>Original object file from Cloudflare R2 storage (<code>{selectedItem.objectKey}</code>)</li>
                  {selectedItem.thumbnailObjectKey && <li>Associated thumbnail file (<code>{selectedItem.thumbnailObjectKey}</code>)</li>}
                  {selectedItem.posterObjectKey && <li>Associated poster/preview file (<code>{selectedItem.posterObjectKey}</code>)</li>}
                </ul>
                <div style={{ fontSize: "12px", color: "#991b1b", fontWeight: "700", marginTop: "8px", borderTop: "1px solid #fecaca", paddingTop: "8px" }}>
                  ⚠️ Note: If this media is currently published or referenced by Homepage Hero, Workshop Posters, or Trainer Profiles, permanent deletion will be safely blocked until unpublished.
                </div>
              </div>

              <p style={{ marginTop: "14px", marginBottom: 0, fontSize: "13px", fontWeight: "700", color: "#dc2626", textAlign: "center" }}>
                This action cannot be undone.
              </p>
            </div>

            <div className="modal-actions" style={{ padding: "16px 24px", background: "#f8fafc", borderTop: "1px solid #e2e8f0", display: "flex", justifyContent: "flex-end", gap: "12px" }}>
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
                style={{ background: "#dc2626", color: "#ffffff", border: "none", padding: "10px 20px", borderRadius: "8px", fontWeight: "700", cursor: "pointer" }}
              >
                {deleting ? "Purging R2 Storage & DB..." : "Delete Permanently"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* --- LIGHTBOX PREVIEW MODAL --- */}
      {previewMedia && (
        <div className="admin-modal-backdrop" onClick={() => setPreviewMedia(null)}>
          <div className="video-preview-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="preview-header">
              <div>
                <h3>{previewMedia.title}</h3>
                <span style={{ fontSize: "12px", color: "#a1a1aa", marginTop: "2px", display: "block" }}>
                  ID: {previewMedia.id}
                </span>
              </div>
              <button
                type="button"
                className="modal-close-btn"
                onClick={() => setPreviewMedia(null)}
              >
                ✕
              </button>
            </div>

            <div className="preview-player-container">
              {(previewMedia.mediaType || "").toLowerCase() === "video" ? (
                <video
                  src={getEffectiveMediaUrl(previewMedia)}
                  controls
                  autoPlay
                  className="full-preview-player"
                  onError={(e) => {
                    const apiBase = API_BASE_URL;
                    const fallback = `${apiBase}/api/media/content/${previewMedia.id}`;
                    if (e.target.src !== fallback) e.target.src = fallback;
                  }}
                />
              ) : (
                <img
                  src={getEffectiveMediaUrl(previewMedia)}
                  alt={previewMedia.altText || previewMedia.title}
                  onError={(e) => {
                    const apiBase = API_BASE_URL;
                    const fallback = `${apiBase}/api/media/content/${previewMedia.id}`;
                    if (e.target.src !== fallback) e.target.src = fallback;
                  }}
                />
              )}
            </div>

            <div className="preview-footer-meta">
              <div className="preview-tags-grid">
                <div className="preview-meta-badge section-badge">
                  <span className="meta-badge-label">Section Placement</span>
                  <span className="meta-badge-value">
                    {previewMedia.placements && previewMedia.placements.length > 0
                      ? previewMedia.placements.map((p) => {
                          const sec = DISPLAY_SECTIONS.find((s) => s.id === p.section);
                          return sec ? `${sec.icon} ${sec.label}` : p.section;
                        }).join(", ")
                      : (DISPLAY_SECTIONS.find((s) => s.id === previewMedia.section)?.label
                          ? `${DISPLAY_SECTIONS.find((s) => s.id === previewMedia.section).icon} ${DISPLAY_SECTIONS.find((s) => s.id === previewMedia.section).label}`
                          : previewMedia.section || "Draft")}
                  </span>
                </div>

                <div className="preview-meta-badge category-badge">
                  <span className="meta-badge-label">Category</span>
                  <span className="meta-badge-value">{previewMedia.category || "General"}</span>
                </div>

                <div className="preview-meta-badge layout-badge">
                  <span className="meta-badge-label">Layout Type</span>
                  <span className="meta-badge-value">{previewMedia.layoutType || "Square"}</span>
                </div>

                <div className="preview-meta-badge order-badge">
                  <span className="meta-badge-label">Display Order</span>
                  <span className="meta-badge-value">#{previewMedia.displayOrder ?? 1}</span>
                </div>

                {previewMedia.focalPoint && (
                  <div className="preview-meta-badge focal-badge">
                    <span className="meta-badge-label">Focal Point</span>
                    <span className="meta-badge-value">{previewMedia.focalPoint}</span>
                  </div>
                )}
              </div>

              {previewMedia.caption && (
                <div className="preview-caption-box">
                  <strong>Caption / Description</strong>
                  <p>{previewMedia.caption}</p>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
