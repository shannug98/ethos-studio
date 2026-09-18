import React, { useState, useRef } from "react";
import { Upload, Crop, Trash2, Image as ImageIcon, CheckCircle2, AlertCircle } from "lucide-react";
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

  const portraitInputRef = useRef(null);
  const landscapeInputRef = useRef(null);

  const handleFileSelect = (field, e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 35 * 1024 * 1024) {
      alert("File size exceeds 35MB limit. Please choose a smaller image.");
      return;
    }

    const ratio = field === "imageUrl" ? "3:4" : "16:9";
    const title =
      field === "imageUrl"
        ? "Crop Portrait Poster (3:4 - 900×1200)"
        : "Crop Landscape Banner (16:9 - 1600×900)";

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
        ? "Crop Portrait Poster (3:4 - 900×1200)"
        : "Crop Landscape Banner (16:9 - 1600×900)";

    setCropperModal({
      isOpen: true,
      aspectRatio: ratio,
      title,
      initialImage: currentSrc,
      targetField: field,
    });
  };

  const handleCropComplete = (blob, dataUrl, meta) => {
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
      onChange("imageUrl", "");
    } else {
      setLandscapeBlob(null);
      onChange("landscapeImageUrl", "");
    }
  };

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Workshop Photos & Media</h2>
        <p className="wizard-section-desc">
          Upload crisp, high-resolution imagery. Use the built-in cropper for precise 3:4 portrait cards and 16:9 banner dimensions.
        </p>
      </div>

      <div className="wizard-photos-layout">
        {/* 1. Primary Portrait Cover (3:4) */}
        <div className="photo-card-wrap">
          <div className="photo-card-header">
            <div>
              <h3 className="photo-card-title">
                Portrait Cover Poster (3:4) <span className="req">*</span>
              </h3>
              <span className="photo-card-badge">Required • 900 × 1200 px</span>
            </div>
            {form.imageUrl && (
              <span className="photo-status-badge success">
                <CheckCircle2 size={13} /> Ready
              </span>
            )}
          </div>
          <p className="photo-card-hint">
            This primary visual is shown on the workshop listing cards, homepage highlights, and mobile booking sheets.
          </p>

          {form.imageUrl ? (
            <div className="photo-preview-box portrait-box">
              <img src={form.imageUrl} alt="Portrait Cover Preview" className="photo-preview-img" />
              <div className="photo-preview-overlay">
                <button
                  type="button"
                  className="photo-action-btn"
                  onClick={() => openCropperForExisting("imageUrl")}
                  title="Crop / Recenter"
                >
                  <Crop size={15} />
                  <span>Adjust Crop</span>
                </button>
                <button
                  type="button"
                  className="photo-action-btn danger"
                  onClick={() => handleRemovePhoto("imageUrl")}
                  title="Remove Image"
                >
                  <Trash2 size={15} />
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
              <span className="dropzone-primary-text">Upload Portrait Cover</span>
              <span className="dropzone-sub-text">Recommended: 900 × 1200 px • Max 5MB</span>
              <button type="button" className="dropzone-browse-btn">
                Browse Files
              </button>
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
              <h3 className="photo-card-title">Landscape Header Banner (16:9)</h3>
              <span className="photo-card-badge secondary">Optional • 1600 × 900 px</span>
            </div>
            {form.landscapeImageUrl && (
              <span className="photo-status-badge success">
                <CheckCircle2 size={13} /> Ready
              </span>
            )}
          </div>
          <p className="photo-card-hint">
            Shown as the panoramic hero banner on the full-page workshop details page and desktop event headers.
          </p>

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
                  onClick={() => openCropperForExisting("landscapeImageUrl")}
                  title="Crop / Recenter"
                >
                  <Crop size={15} />
                  <span>Adjust Crop</span>
                </button>
                <button
                  type="button"
                  className="photo-action-btn danger"
                  onClick={() => handleRemovePhoto("landscapeImageUrl")}
                  title="Remove Image"
                >
                  <Trash2 size={15} />
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
              <span className="dropzone-primary-text">Upload Landscape Banner</span>
              <span className="dropzone-sub-text">Recommended: 1600 × 900 px • Max 5MB</span>
              <button type="button" className="dropzone-browse-btn">
                Browse Files
              </button>
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
