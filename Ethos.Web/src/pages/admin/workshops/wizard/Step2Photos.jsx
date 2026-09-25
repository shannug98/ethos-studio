import React, { useState, useRef } from "react";
import { Upload, Crop, Trash2, Image as ImageIcon, CheckCircle2, AlertCircle, RefreshCw } from "lucide-react";
import ImageCropperModal from "../../../../components/admin/common/ImageCropperModal";

export default function Step2Photos({
  form,
  onChange,
  portraitBlob,
  setPortraitBlob,
  landscapeBlob,
  setLandscapeBlob,
  errors,
}) {
  const [cropperModal, setCropperModal] = useState({
    isOpen: false,
    aspectRatio: "3:4",
    title: "",
    initialImage: null,
    targetField: null, // "imageUrl" | "landscapeImageUrl"
  });

  const [portraitMeta, setPortraitMeta] = useState({ fileName: "", sizeBytes: 0, orientationWarning: "" });
  const [landscapeMeta, setLandscapeMeta] = useState({ fileName: "", sizeBytes: 0, orientationWarning: "" });

  const portraitInputRef = useRef(null);
  const landscapeInputRef = useRef(null);

  const formatFileSize = (bytes) => {
    if (!bytes) return "0.0 MB";
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  const handleFileSelect = (field, e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Strict 5 MB check
    if (file.size > 5 * 1024 * 1024) {
      alert(`File size exceeds the 5 MB limit (${(file.size / (1024 * 1024)).toFixed(1)} MB). Please select an image under 5 MB.`);
      e.target.value = "";
      return;
    }

    // Inspect image orientation
    const objectUrl = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      let warning = "";
      if (field === "imageUrl" && img.naturalWidth > img.naturalHeight) {
        warning = "Uploaded image appears horizontal. A 3:4 portrait crop is recommended for optimal card presentation.";
      } else if (field === "landscapeImageUrl" && img.naturalHeight > img.naturalWidth) {
        warning = "Uploaded image appears vertical. A 16:9 landscape crop is recommended for optimal panoramic banner presentation.";
      }

      if (field === "imageUrl") {
        setPortraitMeta({ fileName: file.name, sizeBytes: file.size, orientationWarning: warning });
      } else {
        setLandscapeMeta({ fileName: file.name, sizeBytes: file.size, orientationWarning: warning });
      }
      URL.revokeObjectURL(objectUrl);
    };
    img.src = objectUrl;

    const ratio = field === "imageUrl" ? "3:4" : "16:9";
    const title =
      field === "imageUrl"
        ? "Crop Portrait Poster (3:4 - 900×1200 px)"
        : "Crop Landscape Banner (16:9 - 1600×900 px)";

    setCropperModal({
      isOpen: true,
      aspectRatio: ratio,
      title,
      initialImage: file,
      targetField: field,
    });
    // Reset file input so re-selecting same file triggers change
    e.target.value = "";
  };

  const openCropperForExisting = (field) => {
    const currentSrc = field === "imageUrl" ? form.imageUrl : form.landscapeImageUrl;
    if (!currentSrc) return;
    const ratio = field === "imageUrl" ? "3:4" : "16:9";
    const title =
      field === "imageUrl"
        ? "Crop Portrait Poster (3:4 - 900×1200 px)"
        : "Crop Landscape Banner (16:9 - 1600×900 px)";

    setCropperModal({
      isOpen: true,
      aspectRatio: ratio,
      title,
      initialImage: currentSrc,
      targetField: field,
    });
  };

  const handleCropComplete = (blob, dataUrl) => {
    if (cropperModal.targetField === "imageUrl") {
      setPortraitBlob(blob);
      onChange("imageUrl", dataUrl);
    } else {
      setLandscapeBlob(blob);
      onChange("landscapeImageUrl", dataUrl);
    }
  };

  const handleRemovePhoto = (field) => {
    if (field === "imageUrl") {
      setPortraitBlob(null);
      setPortraitMeta({ fileName: "", sizeBytes: 0, orientationWarning: "" });
      onChange("imageUrl", "");
    } else {
      setLandscapeBlob(null);
      setLandscapeMeta({ fileName: "", sizeBytes: 0, orientationWarning: "" });
      onChange("landscapeImageUrl", "");
    }
  };

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Workshop Photos & Media</h2>
        <p className="wizard-section-desc">
          Upload crisp, high-resolution imagery. Both images have a strict maximum size limit of 5 MB.
        </p>
      </div>

      <div className="wizard-photos-layout">
        {/* 1. Primary Portrait Image (3:4) */}
        <div className="photo-card-wrap">
          <div className="photo-card-header">
            <div>
              <h3 className="photo-card-title">
                Portrait Image (3:4) <span className="req">*</span>
              </h3>
              <span className="photo-card-badge">Required • 900 × 1200 px • Max 5 MB</span>
            </div>
            {form.imageUrl && (
              <span className="photo-status-badge success">
                <CheckCircle2 size={13} /> Ready
              </span>
            )}
          </div>
          <p className="photo-card-hint">
            <strong>Used for:</strong> Workshop cards, mobile presentation, and homepage/catalogue highlights.
          </p>

          {portraitMeta.orientationWarning && (
            <div style={{ margin: "8px 0", padding: "8px 12px", borderRadius: "8px", background: "rgba(245, 158, 11, 0.12)", border: "1px solid rgba(245, 158, 11, 0.3)", color: "#fbbf24", fontSize: "12px", display: "flex", alignItems: "center", gap: "6px" }}>
              <AlertCircle size={14} />
              <span>{portraitMeta.orientationWarning}</span>
            </div>
          )}

          {form.imageUrl ? (
            <div className="photo-preview-box portrait-box">
              <img src={form.imageUrl} alt="Portrait Cover Preview" className="photo-preview-img" />
              <div className="photo-preview-overlay">
                <button
                  type="button"
                  className="photo-action-btn"
                  onClick={() => portraitInputRef.current?.click()}
                  title="Replace Image"
                >
                  <RefreshCw size={14} />
                  <span>Replace</span>
                </button>
                <button
                  type="button"
                  className="photo-action-btn"
                  onClick={() => openCropperForExisting("imageUrl")}
                  title="Adjust Crop"
                >
                  <Crop size={14} />
                  <span>Adjust</span>
                </button>
                <button
                  type="button"
                  className="photo-action-btn danger"
                  onClick={() => handleRemovePhoto("imageUrl")}
                  title="Remove Image"
                >
                  <Trash2 size={14} />
                  <span>Remove</span>
                </button>
              </div>
            </div>
          ) : (
            <div
              className={`photo-dropzone portrait-drop ${errors.imageUrl ? "dropzone-error" : ""}`}
              onClick={() => portraitInputRef.current?.click()}
            >
              <Upload size={32} className="dropzone-icon" />
              <span className="dropzone-primary-text">Upload Portrait</span>
              <span className="dropzone-sub-text">Recommended: 900 × 1200 px (3:4) • Max 5 MB</span>
              <button type="button" className="dropzone-browse-btn">
                Browse Files
              </button>
            </div>
          )}

          {portraitMeta.fileName && (
            <div style={{ marginTop: "8px", fontSize: "12px", color: "#94a3b8", display: "flex", justifyContent: "space-between" }}>
              <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: "200px" }}>{portraitMeta.fileName}</span>
              <span>{formatFileSize(portraitMeta.sizeBytes)}</span>
            </div>
          )}

          <input
            ref={portraitInputRef}
            type="file"
            accept="image/png,image/jpeg,image/webp,image/jpg"
            style={{ display: "none" }}
            onChange={(e) => handleFileSelect("imageUrl", e)}
          />
          {errors.imageUrl && <span className="wizard-error-text">{errors.imageUrl}</span>}
        </div>

        {/* 2. Wide Landscape Banner (16:9) */}
        <div className="photo-card-wrap">
          <div className="photo-card-header">
            <div>
              <h3 className="photo-card-title">Landscape Image (16:9)</h3>
              <span className="photo-card-badge secondary">Recommended • 1600 × 900 px • Max 5 MB</span>
            </div>
            {form.landscapeImageUrl && (
              <span className="photo-status-badge success">
                <CheckCircle2 size={13} /> Ready
              </span>
            )}
          </div>
          <p className="photo-card-hint">
            <strong>Used for:</strong> Workshop details page and large panoramic banner presentation.
          </p>

          {landscapeMeta.orientationWarning && (
            <div style={{ margin: "8px 0", padding: "8px 12px", borderRadius: "8px", background: "rgba(245, 158, 11, 0.12)", border: "1px solid rgba(245, 158, 11, 0.3)", color: "#fbbf24", fontSize: "12px", display: "flex", alignItems: "center", gap: "6px" }}>
              <AlertCircle size={14} />
              <span>{landscapeMeta.orientationWarning}</span>
            </div>
          )}

          {form.landscapeImageUrl ? (
            <div className="photo-preview-box landscape-box">
              <img
                src={form.landscapeImageUrl}
                alt="Landscape Banner Preview"
                className="photo-preview-img"
              />
              <div className="photo-preview-overlay">
                <button
                  type="button"
                  className="photo-action-btn"
                  onClick={() => landscapeInputRef.current?.click()}
                  title="Replace Image"
                >
                  <RefreshCw size={14} />
                  <span>Replace</span>
                </button>
                <button
                  type="button"
                  className="photo-action-btn"
                  onClick={() => openCropperForExisting("landscapeImageUrl")}
                  title="Adjust Crop"
                >
                  <Crop size={14} />
                  <span>Adjust</span>
                </button>
                <button
                  type="button"
                  className="photo-action-btn danger"
                  onClick={() => handleRemovePhoto("landscapeImageUrl")}
                  title="Remove Image"
                >
                  <Trash2 size={14} />
                  <span>Remove</span>
                </button>
              </div>
            </div>
          ) : (
            <div
              className="photo-dropzone landscape-drop"
              onClick={() => landscapeInputRef.current?.click()}
            >
              <ImageIcon size={32} className="dropzone-icon" />
              <span className="dropzone-primary-text">Upload Landscape</span>
              <span className="dropzone-sub-text">Recommended: 1600 × 900 px (16:9) • Max 5 MB</span>
              <button type="button" className="dropzone-browse-btn">
                Browse Files
              </button>
            </div>
          )}

          {landscapeMeta.fileName && (
            <div style={{ marginTop: "8px", fontSize: "12px", color: "#94a3b8", display: "flex", justifyContent: "space-between" }}>
              <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: "200px" }}>{landscapeMeta.fileName}</span>
              <span>{formatFileSize(landscapeMeta.sizeBytes)}</span>
            </div>
          )}

          <input
            ref={landscapeInputRef}
            type="file"
            accept="image/png,image/jpeg,image/webp,image/jpg"
            style={{ display: "none" }}
            onChange={(e) => handleFileSelect("landscapeImageUrl", e)}
          />
        </div>
      </div>

      {/* Cropper Modal Instance */}
      <ImageCropperModal
        isOpen={cropperModal.isOpen}
        aspectRatio={cropperModal.aspectRatio}
        title={cropperModal.title}
        initialImage={cropperModal.initialImage}
        onCrop={handleCropComplete}
        onClose={() => setCropperModal((prev) => ({ ...prev, isOpen: false }))}
      />
    </div>
  );
}
