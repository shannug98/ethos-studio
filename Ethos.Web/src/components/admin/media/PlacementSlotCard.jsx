import React from "react";
import { Upload, Replace, Edit2, Eye, Trash2, Video, Image as ImageIcon, CheckCircle, EyeOff } from "lucide-react";
import { getMediaUrl, handleMediaImgError, ETHOS_MEDIA_FALLBACK_SVG } from "../../../utils/mediaUrl";

/**
 * PlacementSlotCard
 * 
 * Renders a single fixed slot in its empty or occupied state.
 * Required for:
 * - Homepage Hero 01–06
 * - We Are Ethos 01–03
 * - Founder 01
 * - Trainers 01–04
 */
export default function PlacementSlotCard({
  slot,
  placement,
  occupiedItem,
  onUploadClick,
  onReplaceClick,
  onEditClick,
  onPreviewClick,
  onRemoveClick,
  onTogglePublishClick,
}) {
  const isOccupied = Boolean(occupiedItem);
  const isVideo = (occupiedItem?.mediaType || "").toLowerCase() === "video";
  const placementRecord = occupiedItem?.placements?.find(
    (p) => p.section?.toLowerCase() === placement.sectionKey?.toLowerCase()
  );
  const isPublished = placementRecord ? placementRecord.isPublished : true;

  return (
    <div className={`placement-slot-card ${isOccupied ? "occupied" : "empty"}`}>
      {/* Slot Header */}
      <div className="slot-card-header">
        <div className="slot-number-badge">
          <span className="slot-prefix">SLOT</span>
          <span className="slot-num">{String(slot.order).padStart(2, "0")}</span>
        </div>
        <div className="slot-title-wrap">
          <h4 className="slot-title">{slot.label}</h4>
          <span className="slot-role">{slot.role}</span>
        </div>
        <div className="slot-status-indicator">
          {isOccupied ? (
            <span className={`status-pill ${isPublished ? "published" : "draft"}`}>
              {isPublished ? <CheckCircle size={12} /> : <EyeOff size={12} />}
              {isPublished ? "Live" : "Hidden"}
            </span>
          ) : (
            <span className="status-pill vacant">Empty Slot</span>
          )}
        </div>
      </div>

      {/* Slot Body: Occupied Media or Empty Upload Zone */}
      <div className="slot-card-body">
        {isOccupied ? (
          <div className="slot-media-preview-container">
            <div
              className="slot-thumbnail-wrapper"
              role="button"
              tabIndex={0}
              onClick={() => onPreviewClick && onPreviewClick(occupiedItem)}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  if (onPreviewClick) onPreviewClick(occupiedItem);
                }
              }}
              aria-label={`Preview ${slot.label}: ${occupiedItem.title || "media asset"}`}
              title="Click to preview"
            >
              {isVideo ? (
                <div className="video-thumb-container">
                  {occupiedItem.thumbnailUrl && occupiedItem.thumbnailUrl.match(/\.(jpg|jpeg|png|webp)$/i) ? (
                    <img
                      src={occupiedItem.thumbnailUrl}
                      alt={occupiedItem.altText || occupiedItem.title || slot.label}
                      className="slot-img-thumb"
                      onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                    />
                  ) : (
                    <div style={{
                      width: "100%",
                      height: "100%",
                      display: "flex",
                      flexDirection: "column",
                      alignItems: "center",
                      justifyContent: "center",
                      background: "linear-gradient(135deg, #181920 0%, #0d0e12 100%)",
                      color: "#e07a5f"
                    }}>
                      <Video size={28} />
                      <span style={{ fontSize: 11, color: "#94a3b8", marginTop: 4, fontWeight: 600 }}>
                        Video Asset
                      </span>
                    </div>
                  )}
                  <div className="video-overlay-badge">
                    <Video size={14} />
                    <span>Video</span>
                  </div>
                </div>
              ) : (
                <div className="image-thumb-container">
                  <img
                    src={getMediaUrl(occupiedItem)}
                    alt={occupiedItem.altText || occupiedItem.title || slot.label}
                    className="slot-img-thumb"
                    onError={(e) => handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG)}
                  />
                  <div className="image-overlay-badge">
                    <ImageIcon size={14} />
                  </div>
                </div>
              )}
              <div className="preview-hover-overlay">
                <Eye size={20} />
                <span>Click to Preview</span>
              </div>
            </div>

            {/* Media Metadata Details */}
            <div className="slot-media-details">
              <p className="media-item-title" title={occupiedItem.title || "Untitled"}>
                {occupiedItem.title || "Untitled Studio Asset"}
              </p>
              <div className="media-item-meta-row">
                <span className="meta-tag">{occupiedItem.mediaType || "Image"}</span>
                {occupiedItem.fileSizeBytes > 0 && (
                  <span className="meta-tag">{(occupiedItem.fileSizeBytes / (1024 * 1024)).toFixed(2)} MB</span>
                )}
                {occupiedItem.layoutType && (
                  <span className="meta-tag layout">{occupiedItem.layoutType}</span>
                )}
              </div>
            </div>
          </div>
        ) : (
          <div
            className="slot-empty-dropzone"
            onClick={() => onUploadClick(placement, slot)}
            title={`Click to upload media for ${slot.label}`}
          >
            <div className="empty-icon-circle">
              <Upload size={22} className="empty-upload-icon" />
            </div>
            <p className="empty-action-label">+ Upload {slot.label}</p>
            <span className="empty-recommended-spec">{slot.recommended}</span>
            <span className="empty-limit-hint">
              {placement.allowedMediaLabel} • Max{" "}
              {placement.allowedMedia === "video_only"
                ? "100 MB"
                : placement.allowedMedia === "image_video"
                ? "5MB img / 100MB vid"
                : "5 MB"}
            </span>
          </div>
        )}
      </div>

      {/* Slot Footer Actions */}
      {isOccupied && (
        <div className="slot-card-footer">
          <button
            type="button"
            className="slot-btn replace-btn"
            onClick={() => onReplaceClick(placement, slot, occupiedItem)}
            title="Replace this asset with a new upload"
          >
            <Replace size={14} />
            <span>Replace</span>
          </button>
          <button
            type="button"
            className="slot-btn edit-btn"
            onClick={() => onEditClick(occupiedItem)}
            title="Edit Title, Caption, and Metadata"
          >
            <Edit2 size={14} />
            <span>Edit</span>
          </button>
          <button
            type="button"
            className="slot-btn toggle-btn"
            onClick={() => onTogglePublishClick(occupiedItem, placementRecord)}
            title={isPublished ? "Hide from public site" : "Publish to public site"}
          >
            {isPublished ? <EyeOff size={14} /> : <Eye size={14} />}
            <span>{isPublished ? "Hide" : "Show"}</span>
          </button>
          <button
            type="button"
            className="slot-btn remove-btn"
            onClick={() => onRemoveClick(occupiedItem, placementRecord, slot)}
            title="Remove from this slot (sets slot empty)"
          >
            <Trash2 size={14} />
            <span>Clear</span>
          </button>
        </div>
      )}
    </div>
  );
}
