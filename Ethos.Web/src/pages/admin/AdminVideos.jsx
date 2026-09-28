import React, { useState, useEffect, useRef, useMemo } from "react";
import {
  Cloud, Plus, ArrowRight, ArrowLeft, ExternalLink, Play, Camera,
  Users, Home, Image as ImageIcon, Calendar, Star, Shield, FileText,
  Lightbulb, X, Upload, Trash2, Edit2, CheckCircle, AlertCircle,
  ArrowUp, ArrowDown, ChevronDown, Eye
} from "lucide-react";
import { adminApi, getAdminUser } from "../../services/adminApi";
import {
  MEDIA_PLACEMENTS,
  DEVELOPER_OWNED_ASSETS,
  GALLERY_CATEGORIES,
  MEDIA_LIMITS,
  getPlacementById,
  getPlacementCropConfig,
} from "../../config/MediaPlacementCatalog";
import PlacementSlotCard from "../../components/admin/media/PlacementSlotCard";
import AdminMediaPreviewModal from "../../components/admin/media/AdminMediaPreviewModal";
import ImageCropperModal from "../../components/admin/common/ImageCropperModal";
import { getMediaUrl, handleMediaImgError, ETHOS_MEDIA_FALLBACK_SVG } from "../../utils/mediaUrl";
import "./AdminVideos.css";
import "../../styles/hero.css";

const SECTION_TABS = [
  { id: "all", label: "All Sections" },
  { id: "homepage", label: "Homepage" },
  { id: "trainers", label: "Trainers" },
  { id: "gallery", label: "Gallery" },
  { id: "other", label: "Developer Assets" },
];

export default function AdminVideos() {
  // Navigation & filtering
  const [activeTab, setActiveTab] = useState("all");
  const [activeGalleryCat, setActiveGalleryCat] = useState("All");

  // Managing a specific placement (modal / drill-down)
  const [managingPlacementId, setManagingPlacementId] = useState(null);

  // Backend media state
  const [mediaList, setMediaList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [actionSuccess, setActionSuccess] = useState(null);

  // Modals state
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [previewMedia, setPreviewMedia] = useState(null);
  const [previewContext, setPreviewContext] = useState(null);
  const [itemToEdit, setItemToEdit] = useState(null);
  const [itemToDelete, setItemToDelete] = useState(null);

  // Slot-targeted upload context
  const [targetSlot, setTargetSlot] = useState(null);
  const [replacingItemId, setReplacingItemId] = useState(null);

  // Upload Form State
  const fileInputRef = useRef(null);
  const [isDragging, setIsDragging] = useState(false);
  const [uploadFile, setUploadFile] = useState(null);
  const [rawUploadFile, setRawUploadFile] = useState(null);
  const [uploadPreviewUrl, setUploadPreviewUrl] = useState(null);
  const [cropperModal, setCropperModal] = useState({
    isOpen: false,
    initialImage: null,
    aspectRatio: "16:9",
    allowedRatios: ["16:9"],
    title: "Crop & Adjust Image",
  });
  const [uploadPlacementId, setUploadPlacementId] = useState("hero-banner");
  const [uploadTitle, setUploadTitle] = useState("");
  const [uploadCaption, setUploadCaption] = useState("");
  const [uploadCategory, setUploadCategory] = useState("Workshops");
  const [uploadLayout, setUploadLayout] = useState("Landscape");
  const [uploadOrder, setUploadOrder] = useState(1);
  const [uploadIsPublished, setUploadIsPublished] = useState(true);
  const [uploadIsFeatured, setUploadIsFeatured] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState(null);
  const [uploadProgress, setUploadProgress] = useState(null);

  // Edit Form State
  const [editTitle, setEditTitle] = useState("");
  const [editCaption, setEditCaption] = useState("");
  const [editCategory, setEditCategory] = useState("Workshops");
  const [editLayout, setEditLayout] = useState("Landscape");
  const [editIsPublished, setEditIsPublished] = useState(true);
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // Admin user information
  const adminUser = useMemo(() => getAdminUser(), []);
  const adminName = adminUser?.name || "Admin";
  const adminInitial = adminName.charAt(0).toUpperCase();

  // Load Media from Backend
  const loadMedia = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getAdminMedia({ pageSize: 200 });
      const items = res?.items || (Array.isArray(res) ? res : []);
      setMediaList(items);
    } catch (err) {
      setError(err.message || "Failed to load media assets from server.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMedia();
  }, []);

  const showNotification = (msg) => {
    setActionSuccess(msg);
    setTimeout(() => setActionSuccess(null), 5000);
  };

  // Keyboard shortcut (Escape to close modals)
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        if (cropperModal.isOpen) {
          // Crop modal is topmost and handles its own ESC dismissal
          return;
        }
        if (!uploading) setUploadModalOpen(false);
        if (!editing) setEditModalOpen(false);
        if (!deleting) setDeleteModalOpen(false);
        setPreviewMedia(null);
        setPreviewContext(null);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [uploading, editing, deleting, cropperModal.isOpen]);

  // Storage Used Calculation
  const totalSizeBytes = useMemo(() => {
    return mediaList.reduce((acc, item) => acc + (item.fileSizeBytes || 0), 0);
  }, [mediaList]);
  const storageUsedMb = (totalSizeBytes / (1024 * 1024)).toFixed(1);
  const storagePercent = Math.min(100, Math.round((totalSizeBytes / (10 * 1024 * 1024 * 1024)) * 100));

  // Helper to filter media items by section
  const getItemsForSection = (sectionKey) => {
    if (!sectionKey) return [];
    const targetKey = sectionKey.toLowerCase();
    return mediaList
      .filter((m) => {
        if (m.isArchived) return false;
        return m.placements?.some((p) => {
          const pSec = (p.section || "").toLowerCase();
          if (pSec === targetKey) return true;
          if ((targetKey === "workshop" || targetKey === "workshops") && (pSec === "workshop" || pSec === "workshops")) return true;
          if ((targetKey === "founder" || targetKey === "founders") && (pSec === "founder" || pSec === "founders")) return true;
          return false;
        });
      })
      .sort((a, b) => {
        const getOrder = (item) => item.placements?.find((p) => {
          const pSec = (p.section || "").toLowerCase();
          return pSec === targetKey ||
            ((targetKey === "workshop" || targetKey === "workshops") && (pSec === "workshop" || pSec === "workshops")) ||
            ((targetKey === "founder" || targetKey === "founders") && (pSec === "founder" || pSec === "founders"));
        })?.displayOrder ?? 0;
        return getOrder(a) - getOrder(b);
      });
  };

  // Section item collections
  const heroItems = useMemo(() => getItemsForSection("HomepageScrolling"), [mediaList]);
  const reelItems = useMemo(() => getItemsForSection("HomepageReels"), [mediaList]);
  const ethosItems = useMemo(() => getItemsForSection("AboutEthos"), [mediaList]);
  const founderItems = useMemo(() => getItemsForSection("Founders"), [mediaList]);
  const trainerItems = useMemo(() => getItemsForSection("Trainers"), [mediaList]);
  const slideshowItems = useMemo(() => getItemsForSection("GallerySlideshow"), [mediaList]);
  const galleryVideoItems = useMemo(() => getItemsForSection("GalleryVideos"), [mediaList]);
  const galleryAllPhotoItems = useMemo(() => getItemsForSection("GalleryImages"), [mediaList]);
  const galleryPhotoItems = useMemo(() => {
    const all = getItemsForSection("GalleryImages");
    if (activeGalleryCat === "All") return all;
    return all.filter((m) => (m.category || "").toLowerCase() === activeGalleryCat.toLowerCase());
  }, [mediaList, activeGalleryCat]);

  // Managing Placement Active Object
  const managingPlacement = useMemo(() => {
    return managingPlacementId ? getPlacementById(managingPlacementId) : null;
  }, [managingPlacementId]);

  const managingPlacementItems = useMemo(() => {
    if (!managingPlacement) return [];
    return getItemsForSection(managingPlacement.sectionKey);
  }, [managingPlacement, mediaList]);

  // Open slot-targeted upload modal
  const handleOpenSlotUpload = (placement, slot) => {
    setUploadPlacementId(placement.id);
    setTargetSlot(slot);
    setReplacingItemId(null);
    setUploadFile(null);
    setRawUploadFile(null);
    setUploadPreviewUrl(null);
    setCropperModal({ isOpen: false, initialImage: null, aspectRatio: "16:9", allowedRatios: ["16:9"], title: "" });
    setUploadTitle("");
    setUploadCaption("");
    setUploadCategory("Workshops");
    setUploadLayout(placement.aspectRatio.includes("9:16") ? "Portrait" : placement.aspectRatio.includes("4:5") ? "Portrait" : "Landscape");
    setUploadOrder(slot.order);
    setUploadIsPublished(true);
    setUploadIsFeatured(false);
    setUploadError(null);
    setUploadModalOpen(true);
  };

  // Open slot-targeted replace modal
  const handleOpenSlotReplace = (placement, slot, currentItem) => {
    setUploadPlacementId(placement.id);
    setTargetSlot(slot);
    setReplacingItemId(currentItem.id);
    setUploadFile(null);
    setRawUploadFile(null);
    setUploadPreviewUrl(null);
    setCropperModal({ isOpen: false, initialImage: null, aspectRatio: "16:9", allowedRatios: ["16:9"], title: "" });
    setUploadTitle(currentItem.title || "");
    setUploadCaption(currentItem.caption || "");
    setUploadCategory(currentItem.category || "Workshops");
    setUploadLayout(currentItem.layoutType || "Landscape");
    setUploadOrder(slot.order);
    setUploadIsPublished(true);
    setUploadIsFeatured(currentItem.isFeatured || false);
    setUploadError(null);
    setUploadModalOpen(true);
  };

  // Open generic upload modal
  const handleOpenGlobalUpload = (defaultPlacementId = "hero-banner") => {
    const pl = getPlacementById(defaultPlacementId) || MEDIA_PLACEMENTS[0];
    setUploadPlacementId(pl.id);
    setTargetSlot(null);
    setReplacingItemId(null);
    setUploadFile(null);
    setRawUploadFile(null);
    setUploadPreviewUrl(null);
    setCropperModal({ isOpen: false, initialImage: null, aspectRatio: "16:9", allowedRatios: ["16:9"], title: "" });
    setUploadTitle("");
    setUploadCaption("");
    setUploadCategory("Workshops");
    const defaultLayout = (pl.aspectRatio?.includes("9:16") || pl.aspectRatio?.includes("Portrait")) ? "Portrait" : "Landscape";
    setUploadLayout(defaultLayout);
    setUploadOrder(1);
    setUploadIsPublished(true);
    setUploadIsFeatured(false);
    setUploadError(null);
    setUploadModalOpen(true);
  };

  const handleOpenPreview = (placement, slot, item) => {
    if (!item) return;
    setPreviewContext({ placement, slot, item });
    setPreviewMedia(item);
  };

  const handleClosePreview = () => {
    setPreviewMedia(null);
    setPreviewContext(null);
  };

  useEffect(() => {
    if (typeof window !== "undefined") {
      window.__openAdminPreview = (placement, slot, item) => handleOpenPreview(placement, slot, item);
    }
    return () => {
      if (typeof window !== "undefined") {
        delete window.__openAdminPreview;
      }
    };
  }, []);



  // File selection validation & crop interception
  const handleFileChange = (file) => {
    setUploadError(null);
    if (!file) {
      setRawUploadFile(null);
      setUploadFile(null);
      setUploadPreviewUrl(null);
      return;
    }

    const currentPl = getPlacementById(uploadPlacementId) || MEDIA_PLACEMENTS[0];
    const isImage = file.type?.startsWith("image/");
    const isVideo = file.type?.startsWith("video/");

    if (currentPl.allowedMedia === "video_only" && !isVideo) {
      setUploadError(`${currentPl.placement} accepts Video files only (.mp4, .webm, .mov).`);
      return;
    }
    if (currentPl.allowedMedia === "image_only" && !isImage) {
      setUploadError(`${currentPl.placement} accepts Image files only (.jpg, .png, .webp).`);
      return;
    }

    if (isImage && file.size > MEDIA_LIMITS.IMAGE_MAX_BYTES) {
      setUploadError(`Image exceeds the 5 MB limit (${(file.size / (1024 * 1024)).toFixed(1)} MB).`);
      return;
    }
    if (isVideo) {
      const maxBytes = currentPl.id === "gallery-videos" ? MEDIA_LIMITS.GALLERY_VIDEO_MAX_BYTES : MEDIA_LIMITS.HOMEPAGE_REEL_MAX_BYTES;
      if (file.size > maxBytes) {
        setUploadError(`Video exceeds the limit of ${(maxBytes / (1024 * 1024)).toFixed(0)} MB.`);
        return;
      }
    }

    // Retain original browser file in state for re-crop
    setRawUploadFile(file);

    if (!uploadTitle.trim()) {
      const suggested = file.name.replace(/\.[^/.]+$/, "").replace(/[-_]/g, " ");
      setUploadTitle(suggested);
    }

    if (isVideo) {
      // Video bypasses cropper completely
      setUploadFile(file);
      setUploadPreviewUrl(null);
      return;
    }

    // Image selected -> Open ImageCropperModal before staging for upload
    const cropConfig = getPlacementCropConfig(uploadPlacementId, targetSlot?.order || uploadOrder);
    setCropperModal({
      isOpen: true,
      initialImage: file,
      aspectRatio: cropConfig.aspectRatio,
      allowedRatios: cropConfig.allowedRatios,
      title: cropConfig.title,
    });
  };

  // Crop completion callback - normalizes JPEG blob and sets clean .jpg filename
  const handleCropComplete = (blob, dataUrl) => {
    if (!blob) return;
    const originalName = rawUploadFile?.name || "image.jpg";
    const baseName = originalName.replace(/\.[^/.]+$/, "");
    const normalizedFileName = `${baseName}.jpg`;

    const croppedFile = new File([blob], normalizedFileName, {
      type: "image/jpeg",
      lastModified: Date.now(),
    });

    setUploadFile(croppedFile);
    setUploadPreviewUrl(dataUrl);
    setCropperModal((prev) => ({ ...prev, isOpen: false }));
  };

  // Re-crop handler using the preserved original browser file
  const handleReCrop = () => {
    if (!rawUploadFile) return;
    const cropConfig = getPlacementCropConfig(uploadPlacementId, targetSlot?.order || uploadOrder);
    setCropperModal({
      isOpen: true,
      initialImage: rawUploadFile,
      aspectRatio: cropConfig.aspectRatio,
      allowedRatios: cropConfig.allowedRatios,
      title: cropConfig.title,
    });
  };

  // Submit Upload
  const handleUploadSubmit = async (e) => {
    e.preventDefault();
    if (!uploadFile) {
      setUploadError("Please select a file to upload.");
      return;
    }

    const currentPl = getPlacementById(uploadPlacementId) || MEDIA_PLACEMENTS[0];
    const effectiveTitle = uploadTitle.trim() || uploadFile.name.replace(/\.[^/.]+$/, "").replace(/[-_]/g, " ");
    setUploading(true);
    setUploadError(null);
    setUploadProgress(null);

    try {
      if (currentPl.id === "gallery-videos") {
        setUploadProgress(1);
        const presign = await adminApi.presignGalleryVideoUpload({
          fileName: uploadFile.name,
          contentType: uploadFile.type || "video/mp4",
          fileSizeBytes: uploadFile.size,
        });

        const isDirectHttps = presign.uploadUrl && presign.uploadUrl.startsWith("https://");
        let directUploadSucceeded = false;

        // Case A: Presign returned non-HTTPS URL (e.g. dev mock /uploads/...)
        if (!isDirectHttps) {
          if (uploadFile.size > 100 * 1024 * 1024) {
            throw new Error(`Direct Cloudflare R2 upload required for videos over 100MB (${(uploadFile.size / (1024 * 1024)).toFixed(1)}MB). Cloudflare R2 storage is not configured in this environment.`);
          }
          showNotification("Cloudflare R2 direct endpoint unavailable in this environment; uploading via studio gateway...");
        } else {
          // Attempt direct Cloudflare R2 presigned PUT
          let bytesSent = 0;
          try {
            await new Promise((resolve, reject) => {
              const xhr = new XMLHttpRequest();
              xhr.open("PUT", presign.uploadUrl, true);
              xhr.setRequestHeader("Content-Type", uploadFile.type || "video/mp4");
              xhr.upload.onprogress = (evt) => {
                if (evt.lengthComputable) {
                  bytesSent = evt.loaded;
                  const pct = Math.round((evt.loaded / evt.total) * 100);
                  setUploadProgress(pct);
                }
              };
              xhr.onload = () => {
                if (xhr.status >= 200 && xhr.status < 300) resolve(xhr.response);
                else reject(new Error(`Direct R2 upload failed with status ${xhr.status}.`));
              };
              xhr.onerror = () => reject(new Error("Network connection error during direct Cloudflare R2 upload."));
              xhr.send(uploadFile);
            });
            directUploadSucceeded = true;
          } catch (putErr) {
            // Case B vs Case C:
            if (bytesSent === 0 && uploadFile.size <= 100 * 1024 * 1024) {
              // Case B: Pre-transmission failure and file <= 100MB -> gateway fallback
              showNotification("Direct storage upload failed before transmission; falling back to studio gateway...");
            } else {
              // Case C: Ambiguous result or large file -> fail closed
              throw new Error(
                uploadFile.size > 100 * 1024 * 1024
                  ? `Direct Cloudflare R2 upload failed for large video (${(uploadFile.size / (1024 * 1024)).toFixed(1)}MB). Please check network and retry.`
                  : `Upload transmission interrupted or blocked by storage provider. Please check your connection and retry.`
              );
            }
          }

          if (directUploadSucceeded) {
            await adminApi.confirmGalleryVideoUpload({
              uploadToken: presign.uploadToken,
              objectKey: presign.objectKey,
              title: effectiveTitle,
              caption: uploadCaption.trim(),
              altText: effectiveTitle,
              category: uploadCategory || "Workshop",
              displayOrder: parseInt(targetSlot?.order || uploadOrder, 10) || 1,
              isPublished: uploadIsPublished,
              isFeatured: uploadIsFeatured,
            });

            setUploadModalOpen(false);
            setUploadFile(null);
            setRawUploadFile(null);
            setUploadPreviewUrl(null);
            showNotification(`Video "${effectiveTitle}" uploaded successfully to Cloudflare R2!`);
            await loadMedia();
            return;
          }
        }
      }

      const effectiveSlotOrder = parseInt(targetSlot?.order || uploadOrder, 10) || 1;

      const formData = new FormData();
      formData.append("file", uploadFile);
      formData.append("section", currentPl.sectionKey);
      formData.append("title", effectiveTitle);
      formData.append("caption", uploadCaption.trim());
      formData.append("altText", effectiveTitle);
      formData.append("focalPoint", "center");
      formData.append("category", uploadCategory || "Workshop");
      formData.append("layoutType", uploadLayout || "Landscape");
      formData.append("displayOrder", effectiveSlotOrder);
      formData.append("isPublished", uploadIsPublished);
      formData.append("isFeatured", uploadIsFeatured);

      await adminApi.uploadAdminMedia(formData);

      setUploadModalOpen(false);
      setUploadFile(null);
      setRawUploadFile(null);
      setUploadPreviewUrl(null);
      showNotification(`Media "${effectiveTitle}" uploaded successfully!`);
      await loadMedia();
    } catch (err) {
      setUploadError(err.message || "Failed to upload media. Please try again.");
    } finally {
      setUploading(false);
    }
  };

  // Clear a slot
  const handleClearSlot = async (placement, slot, currentItem) => {
    if (!currentItem) return;
    const confirmed = window.confirm(`Remove media from ${slot.label || `Slot 0${slot.order}`}? The slot will become empty.`);
    if (!confirmed) return;

    try {
      await adminApi.deleteAdminMedia(currentItem.id);
      showNotification(`${slot.label || `Slot 0${slot.order}`} cleared.`);
      await loadMedia();
    } catch (err) {
      alert(err.message || "Failed to clear slot.");
    }
  };

  // Reorder items in a collection
  const handleMoveOrder = async (item, direction, itemsList, sectionKey) => {
    const idx = itemsList.findIndex((m) => m.id === item.id);
    if (idx < 0) return;
    const targetIdx = direction === "up" ? idx - 1 : idx + 1;
    if (targetIdx < 0 || targetIdx >= itemsList.length) return;

    const currentOrder = item.placements?.find(p => p.section?.toLowerCase() === sectionKey?.toLowerCase())?.displayOrder ?? idx + 1;
    const targetItem = itemsList[targetIdx];
    const targetOrder = targetItem.placements?.find(p => p.section?.toLowerCase() === sectionKey?.toLowerCase())?.displayOrder ?? targetIdx + 1;

    try {
      await adminApi.updateAdminMedia(item.id, {
        section: sectionKey,
        displayOrder: targetOrder,
        title: item.title,
        caption: item.caption,
        category: item.category,
        layoutType: item.layoutType,
        isPublished: item.placements?.find(p => p.section?.toLowerCase() === sectionKey?.toLowerCase())?.isPublished ?? true,
      });

      await adminApi.updateAdminMedia(targetItem.id, {
        section: sectionKey,
        displayOrder: currentOrder,
        title: targetItem.title,
        caption: targetItem.caption,
        category: targetItem.category,
        layoutType: targetItem.layoutType,
        isPublished: targetItem.placements?.find(p => p.section?.toLowerCase() === sectionKey?.toLowerCase())?.isPublished ?? true,
      });

      await loadMedia();
    } catch (err) {
      alert("Failed to reorder items: " + err.message);
    }
  };

  // Open Edit Modal
  const handleOpenEdit = (item, sectionKey) => {
    setItemToEdit({ ...item, currentSection: sectionKey });
    setEditTitle(item.title || "");
    setEditCaption(item.caption || "");
    setEditCategory(item.category || "Workshop");
    setEditLayout(item.layoutType || "Landscape");
    const pl = item.placements?.find(p => p.section?.toLowerCase() === sectionKey?.toLowerCase());
    setEditIsPublished(pl ? pl.isPublished : true);
    setEditModalOpen(true);
  };

  // Submit Edit Form
  const handleEditSubmit = async (e) => {
    e.preventDefault();
    if (!itemToEdit) return;
    setEditing(true);
    try {
      const pl = itemToEdit.placements?.find(p => p.section?.toLowerCase() === itemToEdit.currentSection?.toLowerCase());
      await adminApi.updateAdminMedia(itemToEdit.id, {
        section: itemToEdit.currentSection,
        title: editTitle.trim(),
        caption: editCaption.trim(),
        category: editCategory,
        layoutType: editLayout,
        displayOrder: pl ? pl.displayOrder : 0,
        isPublished: editIsPublished,
      });
      setEditModalOpen(false);
      showNotification("Media details updated successfully!");
      await loadMedia();
    } catch (err) {
      alert(err.message || "Failed to update media details.");
    } finally {
      setEditing(false);
    }
  };

  // Delete Media
  const handleDeleteConfirm = async () => {
    if (!itemToDelete) return;
    setDeleting(true);
    try {
      await adminApi.deleteAdminMedia(itemToDelete.id);
      setDeleteModalOpen(false);
      showNotification("Media deleted successfully.");
      await loadMedia();
    } catch (err) {
      alert(err.message || "Failed to delete media.");
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="media-lib-container">
      {/* ----------------------------------------------------------------------
         TOP HEADER AREA
         ---------------------------------------------------------------------- */}
      <header className="media-lib-header">
        <div className="media-lib-title-wrap">
          <h1 className="media-lib-heading">Media Library</h1>
          <p className="media-lib-subtitle">
            Manage all website media in one place. Upload, organize and control exactly where your photos and videos appear.
          </p>
        </div>

        <div className="media-lib-header-actions">
          {/* Storage Used Card */}
          <div className="storage-card">
            <div className="storage-icon-circle">
              <Cloud size={18} />
            </div>
            <div className="storage-info">
              <span className="storage-label">Storage Used</span>
              <span className="storage-numbers">{storageUsedMb} MB / 10 GB</span>
              <div className="storage-bar-wrap">
                <div className="storage-progress-bar">
                  <div
                    className="storage-progress-fill"
                    style={{ width: `${storagePercent}%` }}
                  />
                </div>
                <span className="storage-pct">{storagePercent}%</span>
              </div>
            </div>
          </div>

          {/* + Upload Media Button */}
          <button
            type="button"
            className="btn-upload-primary"
            onClick={() => handleOpenGlobalUpload("hero-banner")}
          >
            <Plus size={16} />
            Upload Media
          </button>

          {/* Admin Identity Pill */}
          <div className="admin-user-badge">
            <div className="admin-avatar-circle">{adminInitial}</div>
            <span>{adminName}</span>
            <ChevronDown size={14} color="#6b7280" />
          </div>
        </div>
      </header>

      {/* Action notification */}
      {actionSuccess && (
        <div style={{
          background: "#ecfdf5",
          border: "1px solid #10b981",
          color: "#065f46",
          padding: "10px 16px",
          borderRadius: "8px",
          fontSize: "13px",
          fontWeight: "600",
          marginBottom: "16px",
          display: "flex",
          alignItems: "center",
          gap: "8px"
        }}>
          <CheckCircle size={16} />
          {actionSuccess}
        </div>
      )}

      {/* ----------------------------------------------------------------------
         SECTION NAVIGATION TABS
         ---------------------------------------------------------------------- */}
      <nav className="section-nav-tabs" aria-label="Media Sections">
        {SECTION_TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={`section-nav-tab ${activeTab === tab.id ? "active" : ""}`}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {/* ----------------------------------------------------------------------
         1. HOMEPAGE SECTION
         ---------------------------------------------------------------------- */}
      {(activeTab === "all" || activeTab === "homepage" || activeTab === "trainers") && (
        <section className="placement-section-container">
          <div className="section-header-banner banner-homepage">
            <div className="section-header-left">
              {activeTab === "trainers" ? <Users size={18} /> : <Home size={18} />}
              <span className="section-header-title">{activeTab === "trainers" ? "Trainers (Master Faculty)" : "Homepage"}</span>
            </div>
            <a href={activeTab === "trainers" ? "/#trainers" : "/"} target="_blank" rel="noopener noreferrer" className="section-live-link">
              View Live Page <ExternalLink size={12} />
            </a>
          </div>

          <div className={`section-cards-grid ${activeTab === "trainers" ? "grid-1" : "grid-5"}`}>
            {(activeTab === "all" || activeTab === "homepage") && (
              <>
            {/* Card 1: Hero Banner */}
            <div className="placement-card">
              <div className="placement-card-header">
                <h4 className="placement-card-title">Hero Banner</h4>
                <p className="placement-card-desc">6 slots • Images or Videos</p>
              </div>

              <div className="placement-preview-box">
                {heroItems[0] ? (
                  <div
                    className="single-preview-wrap"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenPreview(getPlacementById("hero-banner"), getPlacementById("hero-banner")?.slots?.[0], heroItems[0])}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleOpenPreview(getPlacementById("hero-banner"), getPlacementById("hero-banner")?.slots?.[0], heroItems[0]);
                      }
                    }}
                    aria-label="Preview Hero Banner"
                    title="Click to preview"
                  >
                    <img
                      src={getMediaUrl(heroItems[0])}
                      alt={heroItems[0].title || "Hero banner"}
                      className="single-preview-img"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                    <div className="hero-preview-overlay">
                      <div className="hero-preview-headline">MOVE BEYOND ORDINARY.</div>
                    </div>
                    <div className="hero-preview-dots">
                      {[0, 1, 2, 3, 4, 5].map((i) => (
                        <span key={i} className={i === 0 ? "active" : ""} />
                      ))}
                    </div>
                    <div className="single-preview-hover-overlay">
                      <Eye size={16} />
                      <span>Click to Preview</span>
                    </div>
                  </div>
                ) : (
                  <div className="single-preview-empty">
                    <ImageIcon size={26} className="empty-preview-icon" />
                    <span className="empty-preview-text">Empty Slot</span>
                  </div>
                )}
              </div>

              <div className="placement-card-footer">
                <span className="placement-count-badge">{heroItems.length} / 6</span>
                <button
                  type="button"
                  className="btn-manage-placement"
                  onClick={() => setManagingPlacementId("hero-banner")}
                >
                  Manage <ArrowRight size={13} />
                </button>
              </div>
            </div>

            {/* Card 2: Homepage Reels */}
            <div className="placement-card">
              <div className="placement-card-header">
                <h4 className="placement-card-title">Homepage Reels</h4>
                <p className="placement-card-desc">20 videos • Videos only</p>
              </div>

              <div className="placement-preview-box">
                {reelItems[0] ? (
                  <div
                    className="single-preview-wrap"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenPreview(getPlacementById("homepage-reels"), null, reelItems[0])}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleOpenPreview(getPlacementById("homepage-reels"), null, reelItems[0]);
                      }
                    }}
                    aria-label="Preview Homepage Reels"
                    title="Click to preview"
                  >
                    <img
                      src={reelItems[0].thumbnailUrl && reelItems[0].thumbnailUrl.match(/\.(jpg|jpeg|png|webp)$/i) ? reelItems[0].thumbnailUrl : getMediaUrl(reelItems[0])}
                      alt={reelItems[0].title || "Latest Reel"}
                      className="single-preview-img"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                    <div className="single-preview-play-btn">
                      <Play size={16} fill="#ffffff" />
                    </div>
                    <div className="single-preview-hover-overlay">
                      <Eye size={16} />
                      <span>Click to Preview</span>
                    </div>
                  </div>
                ) : (
                  <div className="single-preview-empty">
                    <Play size={24} className="empty-preview-icon" />
                    <span className="empty-preview-text">Empty Slot</span>
                  </div>
                )}
              </div>

              <div className="placement-card-footer">
                <span className="placement-count-badge">{reelItems.length} / 20</span>
                <button
                  type="button"
                  className="btn-manage-placement"
                  onClick={() => setManagingPlacementId("homepage-reels")}
                >
                  Manage <ArrowRight size={13} />
                </button>
              </div>
            </div>

            {/* Card 3: We Are Ethos */}
            <div className="placement-card">
              <div className="placement-card-header">
                <h4 className="placement-card-title">We Are Ethos</h4>
                <p className="placement-card-desc">3 images • Images only</p>
              </div>

              <div className="placement-preview-box">
                {ethosItems[0] ? (
                  <div
                    className="single-preview-wrap"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenPreview(getPlacementById("we-are-ethos"), getPlacementById("we-are-ethos")?.slots?.[0], ethosItems[0])}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleOpenPreview(getPlacementById("we-are-ethos"), getPlacementById("we-are-ethos")?.slots?.[0], ethosItems[0]);
                      }
                    }}
                    aria-label="Preview We Are Ethos"
                    title="Click to preview"
                  >
                    <img
                      src={getMediaUrl(ethosItems[0])}
                      alt={ethosItems[0].title || "We Are Ethos"}
                      className="single-preview-img"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                    <div className="single-preview-hover-overlay">
                      <Eye size={16} />
                      <span>Click to Preview</span>
                    </div>
                  </div>
                ) : (
                  <div className="single-preview-empty">
                    <Camera size={26} className="empty-preview-icon" />
                    <span className="empty-preview-text">Empty Slot</span>
                  </div>
                )}
              </div>

              <div className="placement-card-footer">
                <span className="placement-count-badge">{ethosItems.length} / 3</span>
                <button
                  type="button"
                  className="btn-manage-placement"
                  onClick={() => setManagingPlacementId("we-are-ethos")}
                >
                  Manage <ArrowRight size={13} />
                </button>
              </div>
            </div>

            {/* Card 4: Founders */}
            <div className="placement-card">
              <div className="placement-card-header">
                <h4 className="placement-card-title">Founders</h4>
                <p className="placement-card-desc">2 images • Images only</p>
              </div>

              <div className="placement-preview-box">
                {founderItems[0] ? (
                  <div
                    className="single-preview-wrap"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenPreview(getPlacementById("founder"), getPlacementById("founder")?.slots?.[0], founderItems[0])}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleOpenPreview(getPlacementById("founder"), getPlacementById("founder")?.slots?.[0], founderItems[0]);
                      }
                    }}
                    aria-label="Preview Founder 1"
                    title="Click to preview"
                  >
                    <img
                      src={getMediaUrl(founderItems[0])}
                      alt={founderItems[0].title || "Founder 1"}
                      className="single-preview-img"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                    <div className="single-preview-hover-overlay">
                      <Eye size={16} />
                      <span>Click to Preview</span>
                    </div>
                  </div>
                ) : (
                  <div className="single-preview-empty">
                    <Users size={26} className="empty-preview-icon" />
                    <span className="empty-preview-text">Empty Slot</span>
                  </div>
                )}
              </div>

              <div className="placement-card-footer">
                <span className="placement-count-badge">{founderItems.length} / 2</span>
                <button
                  type="button"
                  className="btn-manage-placement"
                  onClick={() => setManagingPlacementId("founder")}
                >
                  Manage <ArrowRight size={13} />
                </button>
              </div>
            </div>
            </>
            )}

            {/* Card 5: Trainers */}
            <div className="placement-card">
              <div className="placement-card-header">
                <h4 className="placement-card-title">Trainers</h4>
                <p className="placement-card-desc">4 images • Images only</p>
              </div>

              <div className="placement-preview-box">
                {trainerItems[0] ? (
                  <div
                    className="single-preview-wrap"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenPreview(getPlacementById("trainers"), getPlacementById("trainers")?.slots?.[0], trainerItems[0])}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleOpenPreview(getPlacementById("trainers"), getPlacementById("trainers")?.slots?.[0], trainerItems[0]);
                      }
                    }}
                    aria-label="Preview Trainers"
                    title="Click to preview"
                  >
                    <img
                      src={getMediaUrl(trainerItems[0])}
                      alt={trainerItems[0].title || "Faculty"}
                      className="single-preview-img"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                    <div className="single-preview-hover-overlay">
                      <Eye size={16} />
                      <span>Click to Preview</span>
                    </div>
                  </div>
                ) : (
                  <div className="single-preview-empty">
                    <Users size={26} className="empty-preview-icon" />
                    <span className="empty-preview-text">Empty Slot</span>
                  </div>
                )}
              </div>

              <div className="placement-card-footer">
                <span className="placement-count-badge">{trainerItems.length} / 4</span>
                <button
                  type="button"
                  className="btn-manage-placement"
                  onClick={() => setManagingPlacementId("trainers")}
                >
                  Manage <ArrowRight size={13} />
                </button>
              </div>
            </div>
          </div>
        </section>
      )}

      {/* ----------------------------------------------------------------------
         2. GALLERY SECTION
         ---------------------------------------------------------------------- */}
      {(activeTab === "all" || activeTab === "gallery") && (
        <section className="placement-section-container">
          <div className="section-header-banner banner-gallery">
            <div className="section-header-left">
              <ImageIcon size={18} />
              <span className="section-header-title">Gallery</span>
            </div>
            <a href="/gallery" target="_blank" rel="noopener noreferrer" className="section-live-link">
              View Live Page <ExternalLink size={12} />
            </a>
          </div>

          <div className="section-cards-grid grid-gallery">
            {/* Card 1: Featured Slideshow */}
            <div className="placement-card">
              <div className="placement-card-header">
                <h4 className="placement-card-title">Featured Slideshow</h4>
                <p className="placement-card-desc">20 images • Images only</p>
              </div>

              <div className="placement-preview-box">
                {slideshowItems[0] ? (
                  <div
                    className="single-preview-wrap"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenPreview(getPlacementById("gallery-slideshow"), null, slideshowItems[0])}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleOpenPreview(getPlacementById("gallery-slideshow"), null, slideshowItems[0]);
                      }
                    }}
                    aria-label="Preview Featured Slideshow"
                    title="Click to preview"
                  >
                    <img
                      src={getMediaUrl(slideshowItems[0])}
                      alt={slideshowItems[0].title || "Slideshow item"}
                      className="single-preview-img"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                    <div className="single-preview-hover-overlay">
                      <Eye size={16} />
                      <span>Click to Preview</span>
                    </div>
                  </div>
                ) : (
                  <div className="single-preview-empty">
                    <Camera size={26} className="empty-preview-icon" />
                    <span className="empty-preview-text">Empty Slot</span>
                  </div>
                )}
              </div>

              <div className="placement-card-footer">
                <span className="placement-count-badge">{slideshowItems.length} / 20</span>
                <button
                  type="button"
                  className="btn-manage-placement"
                  onClick={() => setManagingPlacementId("gallery-slideshow")}
                >
                  Manage <ArrowRight size={13} />
                </button>
              </div>
            </div>

            {/* Card 2: Large Videos */}
            <div className="placement-card">
              <div className="placement-card-header">
                <h4 className="placement-card-title">Large Videos</h4>
                <p className="placement-card-desc">8 videos • Videos only</p>
              </div>

              <div className="placement-preview-box">
                {galleryVideoItems[0] ? (
                  <div
                    className="single-preview-wrap"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenPreview(getPlacementById("gallery-videos"), null, galleryVideoItems[0])}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleOpenPreview(getPlacementById("gallery-videos"), null, galleryVideoItems[0]);
                      }
                    }}
                    aria-label="Preview Large Videos"
                    title="Click to preview"
                  >
                    <img
                      src={galleryVideoItems[0].thumbnailUrl && galleryVideoItems[0].thumbnailUrl.match(/\.(jpg|jpeg|png|webp)$/i) ? galleryVideoItems[0].thumbnailUrl : getMediaUrl(galleryVideoItems[0])}
                      alt={galleryVideoItems[0].title || "Large video"}
                      className="single-preview-img"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                    <div className="single-preview-play-btn">
                      <Play size={16} fill="#ffffff" />
                    </div>
                    <div className="single-preview-hover-overlay">
                      <Eye size={16} />
                      <span>Click to Preview</span>
                    </div>
                  </div>
                ) : (
                  <div className="single-preview-empty">
                    <Play size={24} className="empty-preview-icon" />
                    <span className="empty-preview-text">Empty Slot</span>
                  </div>
                )}
              </div>

              <div className="placement-card-footer">
                <span className="placement-count-badge">{galleryVideoItems.length} / 8</span>
                <button
                  type="button"
                  className="btn-manage-placement"
                  onClick={() => setManagingPlacementId("gallery-videos")}
                >
                  Manage <ArrowRight size={13} />
                </button>
              </div>
            </div>

            {/* Card 3: All Gallery Photos */}
            <div className="placement-card">
              <div className="placement-card-header">
                <h4 className="placement-card-title">All Gallery Photos</h4>
                <p className="placement-card-desc">Categorized • Images only</p>
              </div>

              <div className="placement-preview-box">
                {galleryAllPhotoItems[0] ? (
                  <div
                    className="single-preview-wrap"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenPreview(getPlacementById("gallery-all-photos"), null, galleryAllPhotoItems[0])}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleOpenPreview(getPlacementById("gallery-all-photos"), null, galleryAllPhotoItems[0]);
                      }
                    }}
                    aria-label="Preview Gallery Photo"
                    title="Click to preview"
                  >
                    <img
                      src={getMediaUrl(galleryAllPhotoItems[0])}
                      alt={galleryAllPhotoItems[0].title || "Gallery photo"}
                      className="single-preview-img"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                    <div className="single-preview-hover-overlay">
                      <Eye size={16} />
                      <span>Click to Preview</span>
                    </div>
                  </div>
                ) : (
                  <div className="single-preview-empty">
                    <Camera size={26} className="empty-preview-icon" />
                    <span className="empty-preview-text">Empty Slot</span>
                  </div>
                )}
              </div>

              <div className="placement-card-footer">
                <span className="placement-count-badge">{galleryAllPhotoItems.length} items</span>
                <button
                  type="button"
                  className="btn-manage-placement"
                  onClick={() => setManagingPlacementId("gallery-all-photos")}
                >
                  Manage <ArrowRight size={13} />
                </button>
              </div>
            </div>
          </div>
        </section>
      )}

      {/* ----------------------------------------------------------------------
         3. OTHER: PROTECTED DEVELOPER ASSETS SECTION
         ---------------------------------------------------------------------- */}
      {(activeTab === "all" || activeTab === "other") && (
        <section className="placement-section-container">
          <div className="section-header-banner banner-other">
            <div className="section-header-left">
              <Shield size={18} />
              <span className="section-header-title">Protected Code & Build Assets</span>
            </div>
            <span style={{ fontSize: "12px", fontWeight: "600", color: "#92400e" }}>
              Read Only • Retained for Build & Offline Fallbacks
            </span>
          </div>

          <div className="section-cards-grid grid-5">
            {DEVELOPER_OWNED_ASSETS.map((asset) => (
              <div key={asset.id} className="placement-card">
                <div className="placement-card-header">
                  <div style={{
                    fontSize: "10px",
                    fontWeight: "700",
                    textTransform: "uppercase",
                    color: "#b45309",
                    background: "#fef3c7",
                    padding: "2px 6px",
                    borderRadius: "4px",
                    display: "inline-block",
                    marginBottom: "6px"
                  }}>
                    {asset.protectionBadge}
                  </div>
                  <h4 className="placement-card-title">{asset.name}</h4>
                  <p className="placement-card-desc">{asset.relativePath}</p>
                </div>

                <div className="placement-preview-box" style={{ background: "#f8fafc" }}>
                  <div style={{
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                    gap: "6px",
                    color: "#64748b",
                    padding: "12px",
                    textAlign: "center"
                  }}>
                    <Shield size={24} color="#f59e0b" />
                    <span style={{ fontSize: "11px", fontWeight: "600" }}>{asset.dimensions}</span>
                    <span style={{ fontSize: "10px", color: "#94a3b8" }}>
                      {(asset.sizeBytes / 1024).toFixed(1)} KB
                    </span>
                  </div>
                </div>

                <div className="placement-card-footer">
                  <span className="placement-count-badge" style={{ fontSize: "11px", color: "#64748b" }}>
                    Protected
                  </span>
                  <span style={{ fontSize: "11px", fontWeight: "600", color: "#9ca3af" }}>
                    Locked
                  </span>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* ----------------------------------------------------------------------
         5. BOTTOM INFORMATIONAL STRIP (3 COLUMNS)
         ---------------------------------------------------------------------- */}
      <div className="media-info-strip">
        <div className="info-col">
          <div className="info-icon-circle amber">
            <Lightbulb size={20} />
          </div>
          <div className="info-text-wrap">
            <span className="info-title">Need to update content?</span>
            <p className="info-desc">
              Simply select a section above, upload your media, and it will automatically appear on the website in the right place.
            </p>
          </div>
        </div>

        <div className="info-col">
          <div className="info-icon-circle blue">
            <FileText size={20} />
          </div>
          <div className="info-text-wrap">
            <span className="info-title">Supported Formats</span>
            <p className="info-desc">
              Images: JPG, PNG, WebP (max 5MB)<br />
              Videos: MP4, MOV (max 100MB)<br />
              Recommended: 16:9 for banners, vertical 9:16 for reels
            </p>
          </div>
        </div>

        <div className="info-col">
          <div className="info-icon-circle green">
            <Shield size={20} />
          </div>
          <div className="info-text-wrap">
            <span className="info-title">Secure & Reliable</span>
            <p className="info-desc">
              All media is stored securely on Cloudflare R2 and delivered with high performance.
            </p>
          </div>
        </div>
      </div>

      {/* ----------------------------------------------------------------------
         6. FOOTER BAR
         ---------------------------------------------------------------------- */}
      <footer className="media-lib-footer">
        <span>© 2026 Ethos Dance Studio. All rights reserved.</span>
        <span>People Move Higher.</span>
      </footer>

      {/* ----------------------------------------------------------------------
         PLACEMENT MANAGER DRAWER / MODAL
         Opens when an admin clicks "Manage →" on any card
         ---------------------------------------------------------------------- */}
      {managingPlacement && (
        <div className="placement-manager-modal-backdrop" onClick={() => setManagingPlacementId(null)}>
          <div className="placement-manager-card" onClick={(e) => e.stopPropagation()}>
            <div className="placement-manager-header">
              <div className="manager-header-left">
                <button
                  type="button"
                  className="btn-back-library"
                  onClick={() => setManagingPlacementId(null)}
                >
                  <ArrowLeft size={14} />
                  Back
                </button>
                <div className="manager-title-group">
                  <h3>{managingPlacement.placement}</h3>
                  <p>
                    {managingPlacement.area} • {managingPlacement.aspectRatio} • {managingPlacement.allowedMediaLabel}
                  </p>
                </div>
              </div>

              <div className="manager-header-actions">
                <button
                  type="button"
                  className="btn-upload-primary"
                  onClick={() => handleOpenGlobalUpload(managingPlacement.id)}
                >
                  <Plus size={15} />
                  Upload into {managingPlacement.placement}
                </button>
                <button
                  type="button"
                  className="btn-modal-close"
                  onClick={() => setManagingPlacementId(null)}
                >
                  <X size={20} />
                </button>
              </div>
            </div>

            <div className="placement-manager-body">
              {/* Fixed slots layout */}
              {managingPlacement.type === "fixed_slots" && managingPlacement.slots && (
                <div className="slots-grid">
                  {managingPlacement.slots.map((slot) => {
                    const assignedItem = managingPlacementItems.find((item) => {
                      const pl = item.placements?.find(
                        (p) => p.section?.toLowerCase() === managingPlacement.sectionKey?.toLowerCase()
                      );
                      return (pl && Number(pl.displayOrder) === Number(slot.order)) || Number(item.displayOrder) === Number(slot.order);
                    });

                    return (
                      <PlacementSlotCard
                        key={slot.order}
                        slot={slot}
                        placement={managingPlacement}
                        occupiedItem={assignedItem}
                        onUploadClick={() => handleOpenSlotUpload(managingPlacement, slot)}
                        onReplaceClick={() => handleOpenSlotReplace(managingPlacement, slot, assignedItem)}
                        onEditClick={() => handleOpenEdit(assignedItem, managingPlacement.sectionKey)}
                        onPreviewClick={(item) => handleOpenPreview(managingPlacement, slot, item || assignedItem)}
                        onRemoveClick={() => handleClearSlot(managingPlacement, slot, assignedItem)}
                      />
                    );
                  })}
                </div>
              )}

              {/* Collection layout */}
              {managingPlacement.type !== "fixed_slots" && (
                <div className="collection-grid">
                  {managingPlacementItems.map((item, idx) => (
                    <div key={item.id} className="collection-item-card">
                      <div
                        className="collection-item-thumb"
                        role="button"
                        tabIndex={0}
                        onClick={() => handleOpenPreview(managingPlacement, null, item)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            handleOpenPreview(managingPlacement, null, item);
                          }
                        }}
                        aria-label={`Preview ${item.title || "media asset"}`}
                        title="Click to preview"
                        style={{ cursor: "pointer", position: "relative" }}
                      >
                        {item.mediaType === "Video" ? (
                          <div style={{ position: "relative", width: "100%", height: "100%" }}>
                            <img
                              src={item.thumbnailUrl && item.thumbnailUrl.match(/\.(jpg|jpeg|png|webp)$/i) ? item.thumbnailUrl : getMediaUrl(item)}
                              alt={item.title}
                              style={{ width: "100%", height: "100%", objectFit: "cover" }}
                              onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                            />
                            <div className="reel-play-circle" style={{ top: "50%", left: "50%", transform: "translate(-50%, -50%)" }}>
                              <Play size={14} fill="#fff" />
                            </div>
                          </div>
                        ) : (
                          <img
                            src={getMediaUrl(item)}
                            alt={item.title}
                            style={{ width: "100%", height: "100%", objectFit: "cover" }}
                            onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                          />
                        )}
                        <div className="preview-hover-overlay">
                          <Eye size={18} />
                          <span style={{ fontSize: 11 }}>Click to Preview</span>
                        </div>
                      </div>

                      <div className="collection-item-info">
                        <h5 className="collection-item-title">{item.title || "Untitled"}</h5>
                        <p className="collection-item-meta">
                          {item.mediaType} • {((item.fileSizeBytes || 0) / (1024 * 1024)).toFixed(2)} MB • {item.category || "General"}
                        </p>
                      </div>

                      <div className="collection-item-actions">
                        <div className="collection-reorder-btns">
                          <button
                            type="button"
                            className="btn-icon-tiny"
                            title="Move Up"
                            disabled={idx === 0}
                            onClick={() => handleMoveOrder(item, "up", managingPlacementItems, managingPlacement.sectionKey)}
                          >
                            <ArrowUp size={13} />
                          </button>
                          <button
                            type="button"
                            className="btn-icon-tiny"
                            title="Move Down"
                            disabled={idx === managingPlacementItems.length - 1}
                            onClick={() => handleMoveOrder(item, "down", managingPlacementItems, managingPlacement.sectionKey)}
                          >
                            <ArrowDown size={13} />
                          </button>
                        </div>

                        <div style={{ display: "flex", gap: "6px" }}>
                          <button
                            type="button"
                            className="btn-icon-tiny"
                            title="Edit details"
                            onClick={() => handleOpenEdit(item, managingPlacement.sectionKey)}
                          >
                            <Edit2 size={13} />
                          </button>
                          <button
                            type="button"
                            className="btn-icon-tiny"
                            title="Delete item"
                            onClick={() => {
                              setItemToDelete(item);
                              setDeleteModalOpen(true);
                            }}
                          >
                            <Trash2 size={13} color="#ef4444" />
                          </button>
                        </div>
                      </div>
                    </div>
                  ))}

                  {managingPlacementItems.length === 0 && (
                    <div style={{
                      gridColumn: "1 / -1",
                      textAlign: "center",
                      padding: "48px 24px",
                      background: "#ffffff",
                      border: "2px dashed #cbd5e1",
                      borderRadius: "12px",
                      color: "#6b7280"
                    }}>
                      <Upload size={32} color="#94a3b8" style={{ marginBottom: "12px" }} />
                      <h4 style={{ margin: "0 0 6px 0", color: "#111827", fontSize: "16px" }}>
                        No media in {managingPlacement.placement} yet
                      </h4>
                      <p style={{ margin: "0 0 16px 0", fontSize: "13px" }}>
                        Click below to upload images or videos for this placement.
                      </p>
                      <button
                        type="button"
                        className="btn-upload-primary"
                        onClick={() => handleOpenGlobalUpload(managingPlacement.id)}
                      >
                        <Plus size={15} /> Upload Media Now
                      </button>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ----------------------------------------------------------------------
         GLOBAL UPLOAD MODAL
         ---------------------------------------------------------------------- */}
      {uploadModalOpen && (
        <div className="admin-modal-overlay" onClick={() => !uploading && setUploadModalOpen(false)}>
          <div className="admin-modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="admin-modal-header">
              <h3>Upload Media</h3>
              <button
                type="button"
                className="btn-modal-close"
                disabled={uploading}
                onClick={() => setUploadModalOpen(false)}
              >
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleUploadSubmit}>
              <div className="admin-modal-body">
                {uploadError && (
                  <div style={{
                    background: "#fef2f2",
                    border: "1px solid #ef4444",
                    color: "#b91c1c",
                    padding: "10px 14px",
                    borderRadius: "6px",
                    fontSize: "12.5px",
                    marginBottom: "14px",
                    display: "flex",
                    alignItems: "center",
                    gap: "8px"
                  }}>
                    <AlertCircle size={16} />
                    {uploadError}
                  </div>
                )}

                {/* Placement selector */}
                <div className="form-group">
                  <label>Target Website Placement</label>
                  <select
                    className="form-select"
                    value={uploadPlacementId}
                    onChange={(e) => {
                      const newId = e.target.value;
                      setUploadPlacementId(newId);
                      setTargetSlot(null);
                      const pl = getPlacementById(newId);
                      if (pl) {
                        const defLayout = (pl.aspectRatio?.includes("9:16") || pl.aspectRatio?.includes("Portrait")) ? "Portrait" : "Landscape";
                        setUploadLayout(defLayout);
                      }
                    }}
                  >
                    {MEDIA_PLACEMENTS.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.area} → {p.placement} ({p.allowedMediaLabel})
                      </option>
                    ))}
                  </select>
                </div>

                {/* Slot selector if fixed slots */}
                {(() => {
                  const currPl = getPlacementById(uploadPlacementId);
                  if (currPl && currPl.type === "fixed_slots" && currPl.slots) {
                    return (
                      <div className="form-group">
                        <label>Assigned Slot</label>
                        <select
                          className="form-select"
                          value={uploadOrder}
                          onChange={(e) => setUploadOrder(parseInt(e.target.value, 10))}
                        >
                          {currPl.slots.map((s) => (
                            <option key={s.order} value={s.order}>
                              Slot {s.order < 10 ? `0${s.order}` : s.order}: {s.label} ({s.recommended})
                            </option>
                          ))}
                        </select>
                      </div>
                    );
                  }
                  return null;
                })()}

                {/* File Dropzone */}
                <div
                  className={`dropzone-container ${isDragging ? "dragging" : ""}`}
                  onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
                  onDragLeave={() => setIsDragging(false)}
                  onDrop={(e) => {
                    e.preventDefault();
                    setIsDragging(false);
                    if (e.dataTransfer.files?.[0]) handleFileChange(e.dataTransfer.files[0]);
                  }}
                  onClick={() => {
                    if (!uploadFile) fileInputRef.current?.click();
                  }}
                >
                  <input
                    type="file"
                    ref={fileInputRef}
                    style={{ display: "none" }}
                    accept="image/png,image/jpeg,image/webp,image/jpg,video/mp4,video/webm,video/quicktime"
                    onChange={(e) => {
                      if (e.target.files?.[0]) handleFileChange(e.target.files[0]);
                      e.target.value = "";
                    }}
                  />
                  {uploadPreviewUrl && uploadFile ? (
                    <div className="dropzone-cropped-card" onClick={(e) => e.stopPropagation()}>
                      <div className="dropzone-cropped-thumb-wrap">
                        <img src={uploadPreviewUrl} alt="Cropped preview" className="dropzone-cropped-thumb" />
                      </div>
                      <div className="dropzone-cropped-info">
                        <div className="dropzone-cropped-badge">
                          <CheckCircle size={12} />
                          <span>Cropped & Ready for R2</span>
                        </div>
                        <div className="dropzone-cropped-filename" title={uploadFile.name}>
                          {uploadFile.name}
                        </div>
                        <div className="dropzone-cropped-meta">
                          {(uploadFile.size / 1024).toFixed(0)} KB • JPEG
                        </div>
                      </div>
                      <div className="dropzone-cropped-actions">
                        <button
                          type="button"
                          className="btn-dropzone-action primary"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleReCrop();
                          }}
                          title="Re-adjust crop area and zoom"
                        >
                          <Edit2 size={12} />
                          <span>Re-crop</span>
                        </button>
                        <button
                          type="button"
                          className="btn-dropzone-action"
                          onClick={(e) => {
                            e.stopPropagation();
                            fileInputRef.current?.click();
                          }}
                          title="Choose a different image file"
                        >
                          <Upload size={12} />
                          <span>Replace</span>
                        </button>
                      </div>
                    </div>
                  ) : uploadFile ? (
                    <div onClick={() => fileInputRef.current?.click()}>
                      <div style={{ fontWeight: "700", color: "#111827", fontSize: "13.5px" }}>
                        {uploadFile.name}
                      </div>
                      <div style={{ color: "#6b7280", fontSize: "12px", marginTop: "2px" }}>
                        {(uploadFile.size / (1024 * 1024)).toFixed(2)} MB • Click to change file
                      </div>
                    </div>
                  ) : (
                    <div>
                      <Upload size={28} color="#94a3b8" style={{ marginBottom: "8px" }} />
                      <div style={{ fontWeight: "600", color: "#111827", fontSize: "13px" }}>
                        Click to select or drag and drop file
                      </div>
                      <div style={{ color: "#6b7280", fontSize: "11.5px", marginTop: "4px" }}>
                        Images (.jpg, .png, .webp) will open the crop tool • Videos (.mp4, .mov) upload directly
                      </div>
                    </div>
                  )}
                </div>

                {/* Title & Category */}
                <div className="form-row-2">
                  <div className="form-group">
                    <label>Title</label>
                    <input
                      type="text"
                      className="form-input"
                      placeholder="e.g. Master Faculty Movement"
                      value={uploadTitle}
                      onChange={(e) => setUploadTitle(e.target.value)}
                    />
                  </div>

                  <div className="form-group">
                    <label>Gallery Category</label>
                    <select
                      className="form-select"
                      value={uploadCategory}
                      onChange={(e) => setUploadCategory(e.target.value)}
                    >
                      {GALLERY_CATEGORIES.map((cat) => (
                        <option key={cat} value={cat}>{cat}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Caption */}
                <div className="form-group">
                  <label>Caption / Narrative</label>
                  <textarea
                    className="form-textarea"
                    placeholder="Short description for accessibility and overlays..."
                    value={uploadCaption}
                    onChange={(e) => setUploadCaption(e.target.value)}
                  />
                </div>

                {/* Progress bar if direct R2 upload */}
                {uploadProgress !== null && (
                  <div style={{ marginTop: "12px" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "12px", marginBottom: "4px" }}>
                      <span>Uploading directly to Cloudflare R2...</span>
                      <span style={{ fontWeight: "700" }}>{uploadProgress}%</span>
                    </div>
                    <div style={{ height: "6px", background: "#e2e8f0", borderRadius: "3px", overflow: "hidden" }}>
                      <div style={{ width: `${uploadProgress}%`, height: "100%", background: "#e07a5f", transition: "width 0.2s" }} />
                    </div>
                  </div>
                )}
              </div>

              <div className="admin-modal-footer">
                <button
                  type="button"
                  className="btn-secondary"
                  disabled={uploading}
                  onClick={() => setUploadModalOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="btn-upload-primary"
                  disabled={uploading || !uploadFile}
                >
                  {uploading ? "Uploading..." : "Save to Cloud"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ----------------------------------------------------------------------
         EDIT METADATA MODAL
         ---------------------------------------------------------------------- */}
      {editModalOpen && itemToEdit && (
        <div className="admin-modal-overlay" onClick={() => !editing && setEditModalOpen(false)}>
          <div className="admin-modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="admin-modal-header">
              <h3>Edit Media Details</h3>
              <button
                type="button"
                className="btn-modal-close"
                disabled={editing}
                onClick={() => setEditModalOpen(false)}
              >
                <X size={18} />
              </button>
            </div>

            <form onSubmit={handleEditSubmit}>
              <div className="admin-modal-body">
                <div className="form-group">
                  <label>Title</label>
                  <input
                    type="text"
                    className="form-input"
                    value={editTitle}
                    onChange={(e) => setEditTitle(e.target.value)}
                    required
                  />
                </div>

                <div className="form-row-2">
                  <div className="form-group">
                    <label>Category</label>
                    <select
                      className="form-select"
                      value={editCategory}
                      onChange={(e) => setEditCategory(e.target.value)}
                    >
                      {GALLERY_CATEGORIES.map((c) => (
                        <option key={c} value={c}>{c}</option>
                      ))}
                    </select>
                  </div>

                  <div className="form-group">
                    <label>Layout</label>
                    <select
                      className="form-select"
                      value={editLayout}
                      onChange={(e) => setEditLayout(e.target.value)}
                    >
                      <option value="Landscape">Landscape (16:9)</option>
                      <option value="Portrait">Portrait (3:4 / 9:16)</option>
                      <option value="Square">Square (1:1)</option>
                    </select>
                  </div>
                </div>

                <div className="form-group">
                  <label>Caption</label>
                  <textarea
                    className="form-textarea"
                    value={editCaption}
                    onChange={(e) => setEditCaption(e.target.value)}
                  />
                </div>

                <div className="form-group" style={{ display: "flex", alignItems: "center", gap: "8px", marginTop: "12px" }}>
                  <input
                    type="checkbox"
                    id="editPublished"
                    checked={editIsPublished}
                    onChange={(e) => setEditIsPublished(e.target.checked)}
                  />
                  <label htmlFor="editPublished" style={{ margin: 0, cursor: "pointer" }}>
                    Visible on public website
                  </label>
                </div>
              </div>

              <div className="admin-modal-footer">
                <button
                  type="button"
                  className="btn-secondary"
                  disabled={editing}
                  onClick={() => setEditModalOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="btn-upload-primary"
                  disabled={editing}
                >
                  {editing ? "Saving..." : "Save Changes"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ----------------------------------------------------------------------
         DELETE CONFIRMATION MODAL
         ---------------------------------------------------------------------- */}
      {deleteModalOpen && itemToDelete && (
        <div className="admin-modal-overlay" onClick={() => !deleting && setDeleteModalOpen(false)}>
          <div className="admin-modal-content" style={{ maxWidth: 440 }} onClick={(e) => e.stopPropagation()}>
            <div className="admin-modal-header">
              <h3 style={{ color: "#b91c1c" }}>Delete Media Item</h3>
              <button
                type="button"
                className="btn-modal-close"
                disabled={deleting}
                onClick={() => setDeleteModalOpen(false)}
              >
                <X size={18} />
              </button>
            </div>

            <div className="admin-modal-body">
              <p style={{ margin: "0 0 12px 0", fontSize: "13.5px", color: "#374151" }}>
                Are you sure you want to delete <strong>{itemToDelete.title || "this item"}</strong>?
              </p>
              <p style={{ margin: 0, fontSize: "12px", color: "#6b7280" }}>
                This will remove the item from all website placements.
              </p>
            </div>

            <div className="admin-modal-footer">
              <button
                type="button"
                className="btn-secondary"
                disabled={deleting}
                onClick={() => setDeleteModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn-danger"
                disabled={deleting}
                onClick={handleDeleteConfirm}
              >
                {deleting ? "Deleting..." : "Delete Media"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ----------------------------------------------------------------------
         UNIVERSAL MEDIA PREVIEW MODAL (All Placements)
         ---------------------------------------------------------------------- */}
      {previewMedia && (
        <AdminMediaPreviewModal
          media={previewMedia}
          placement={previewContext?.placement}
          slot={previewContext?.slot}
          onClose={handleClosePreview}
        />
      )}

      {/* ----------------------------------------------------------------------
         UNIVERSAL IMAGE CROPPER MODAL (All Image Placements)
         ---------------------------------------------------------------------- */}
      <ImageCropperModal
        isOpen={cropperModal.isOpen}
        aspectRatio={cropperModal.aspectRatio}
        allowedRatios={cropperModal.allowedRatios}
        title={cropperModal.title}
        initialImage={cropperModal.initialImage}
        onCrop={handleCropComplete}
        onClose={() => setCropperModal((prev) => ({ ...prev, isOpen: false }))}
      />
    </div>
  );
}
