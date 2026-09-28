import React, { useState, useRef, useEffect, useMemo, useCallback } from "react";
import { createPortal } from "react-dom";
import { X, ZoomIn, ZoomOut, RotateCcw, Check, Upload, AlertCircle } from "lucide-react";
import "./UniversalImageCropper.css";

import { PRESET_RATIOS } from "../../constants/imagePresets";
export { PRESET_RATIOS };

const MAX_FILE_SIZE_BYTES = 35 * 1024 * 1024; // 35 MB

export default function UniversalImageCropper({
  isOpen,
  preset = "3:4",
  allowedPresets = null, // e.g. ["3:4"] or ["3:4", "1:1", "16:9", "natural"]
  title = "Crop & Position Image",
  initialImage = null,
  destinationName = "",
  onCrop,
  onClose,
}) {
  const [imageSrc, setImageSrc] = useState(null);
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const [dragStart, setDragStart] = useState({ x: 0, y: 0 });
  const [selectedPresetKey, setSelectedPresetKey] = useState(preset);
  const [errorMsg, setErrorMsg] = useState("");
  const [imageLoaded, setImageLoaded] = useState(false);
  const [naturalDimensions, setNaturalDimensions] = useState({ width: 0, height: 0 });

  const containerRef = useRef(null);
  const imgRef = useRef(null);
  const fileInputRef = useRef(null);

  // Sync preset key when prop changes
  useEffect(() => {
    if (preset && PRESET_RATIOS[preset]) {
      setSelectedPresetKey(preset);
    }
  }, [preset]);

  // Trap ESC to close cropper without destructive effects
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        e.preventDefault();
        e.stopPropagation();
        onClose?.();
      }
    };
    window.addEventListener("keydown", handleKeyDown, true);
    return () => window.removeEventListener("keydown", handleKeyDown, true);
  }, [isOpen, onClose]);

  // Lock body scroll while modal is open
  useEffect(() => {
    if (!isOpen) return;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = prevOverflow;
    };
  }, [isOpen]);

  // Load initial image if provided
  useEffect(() => {
    if (!isOpen) {
      setImageSrc(null);
      setImageLoaded(false);
      setZoom(1);
      setOffset({ x: 0, y: 0 });
      setErrorMsg("");
      setNaturalDimensions({ width: 0, height: 0 });
      return;
    }

    if (initialImage) {
      if (typeof initialImage === "string") {
        setImageSrc(initialImage);
      } else if (initialImage instanceof Blob || initialImage instanceof File) {
        if (initialImage.size > MAX_FILE_SIZE_BYTES) {
          setErrorMsg("File size exceeds 35MB limit.");
          return;
        }
        let active = true;
        const reader = new FileReader();
        reader.onload = () => {
          if (active) setImageSrc(reader.result);
        };
        reader.readAsDataURL(initialImage);
        return () => {
          active = false;
        };
      }
    }
  }, [isOpen, initialImage]);

  const activePreset = useMemo(() => {
    return PRESET_RATIOS[selectedPresetKey] || PRESET_RATIOS["3:4"];
  }, [selectedPresetKey]);

  // File selection
  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith("image/")) {
      setErrorMsg("Please select a valid image (JPEG, PNG, WebP).");
      return;
    }
    if (file.size > MAX_FILE_SIZE_BYTES) {
      setErrorMsg("File size exceeds 35MB limit.");
      return;
    }

    setErrorMsg("");
    setImageLoaded(false);
    setZoom(1);
    setOffset({ x: 0, y: 0 });

    const reader = new FileReader();
    reader.onload = () => {
      setImageSrc(reader.result);
    };
    reader.readAsDataURL(file);
    e.target.value = "";
  };

  // Image load handler
  const handleImageLoad = (e) => {
    const target = e?.target || imgRef.current;
    if (!target) return;
    imgRef.current = target;
    const nw = target.naturalWidth || 800;
    const nh = target.naturalHeight || 600;
    setNaturalDimensions({ width: nw, height: nh });
    setImageLoaded(true);
    setZoom(1);
    setOffset({ x: 0, y: 0 });
  };

  // Compute base dimensions preserving exact natural aspect ratio without distortion
  const baseDimensions = useMemo(() => {
    if (!naturalDimensions.width || !naturalDimensions.height) {
      return { width: 0, height: 0 };
    }
    const cropBox = containerRef.current?.getBoundingClientRect();
    const boxW = cropBox?.width || (activePreset.ratio < 1 ? 270 : 360);
    const boxH = cropBox?.height || (activePreset.ratio < 1 ? 360 : 270);

    const imgAspect = naturalDimensions.width / naturalDimensions.height;
    const boxAspect = boxW / boxH;

    let baseW, baseH;
    if (imgAspect > boxAspect) {
      baseH = boxH;
      baseW = boxH * imgAspect;
    } else {
      baseW = boxW;
      baseH = boxW / imgAspect;
    }
    return { width: baseW, height: baseH };
  }, [naturalDimensions, activePreset]);

  // Clamps offset within boundaries so user cannot pan beyond image coverage
  const clampOffset = useCallback((x, y, zoomVal = zoom) => {
    if (!containerRef.current || !imgRef.current) return { x, y };
    const cropBox = containerRef.current.getBoundingClientRect();
    const nw = naturalDimensions.width;
    const nh = naturalDimensions.height;
    if (!cropBox.width || !cropBox.height || !nw || !nh) return { x, y };

    const imgAspect = nw / nh;
    const boxAspect = cropBox.width / cropBox.height;

    let baseW, baseH;
    if (imgAspect > boxAspect) {
      baseH = cropBox.height;
      baseW = cropBox.height * imgAspect;
    } else {
      baseW = cropBox.width;
      baseH = cropBox.width / imgAspect;
    }

    const currentW = baseW * zoomVal;
    const currentH = baseH * zoomVal;

    const maxOffsetX = Math.max(0, (currentW - cropBox.width) / 2);
    const maxOffsetY = Math.max(0, (currentH - cropBox.height) / 2);

    return {
      x: Math.min(Math.max(x, -maxOffsetX), maxOffsetX),
      y: Math.min(Math.max(y, -maxOffsetY), maxOffsetY),
    };
  }, [zoom, naturalDimensions]);

  // Zoom handler with 1.0 (cover fit) to 3.0 scale range
  const handleZoomChange = useCallback((newZoom) => {
    const clampedZoom = Math.min(Math.max(1.0, newZoom), 3.0);
    setZoom(clampedZoom);
    setOffset((prev) => clampOffset(prev.x, prev.y, clampedZoom));
  }, [clampOffset]);

  // Pointer drag handlers (touch, tablet & mouse unified)
  const handlePointerDown = (e) => {
    if (!imageLoaded) return;
    try {
      e.currentTarget.setPointerCapture(e.pointerId);
    } catch {
      // Ignore if pointer capture unsupported
    }
    setIsDragging(true);
    setDragStart({ x: e.clientX - offset.x, y: e.clientY - offset.y });
  };

  const handlePointerMove = (e) => {
    if (!isDragging) return;
    const rawX = e.clientX - dragStart.x;
    const rawY = e.clientY - dragStart.y;
    setOffset(clampOffset(rawX, rawY, zoom));
  };

  const handlePointerUp = (e) => {
    if (isDragging) {
      try {
        e.currentTarget.releasePointerCapture(e.pointerId);
      } catch {
        // Ignore if pointer capture already released
      }
      setIsDragging(false);
    }
  };

  // Wheel zoom
  const handleWheel = (e) => {
    if (!imageLoaded) return;
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.05 : 0.05;
    handleZoomChange(zoom + delta);
  };

  const handleReset = () => {
    setZoom(1);
    setOffset({ x: 0, y: 0 });
  };

  // Pixel-perfect canvas export on Apply
  const handleApplyCrop = async () => {
    if (!imgRef.current || !containerRef.current) return;

    try {
      const img = imgRef.current;
      const container = containerRef.current;
      const cropBox = container.getBoundingClientRect();

      const outWidth = activePreset.width || 900;
      const outHeight = activePreset.height || Math.round(outWidth / (activePreset.ratio || 1));

      const canvas = document.createElement("canvas");
      canvas.width = outWidth;
      canvas.height = outHeight;
      const ctx = canvas.getContext("2d");

      if (!ctx) {
        throw new Error("Could not initialize 2D canvas context");
      }

      const scaleFactor = outWidth / cropBox.width;

      const imgAspect = naturalDimensions.width / naturalDimensions.height;
      const boxAspect = cropBox.width / cropBox.height;

      let baseW, baseH;
      if (imgAspect > boxAspect) {
        baseH = cropBox.height;
        baseW = cropBox.height * imgAspect;
      } else {
        baseW = cropBox.width;
        baseH = cropBox.width / imgAspect;
      }

      const currentW = baseW * zoom;
      const currentH = baseH * zoom;

      const screenImgX = (cropBox.width - currentW) / 2 + offset.x;
      const screenImgY = (cropBox.height - currentH) / 2 + offset.y;

      ctx.drawImage(
        img,
        screenImgX * scaleFactor,
        screenImgY * scaleFactor,
        currentW * scaleFactor,
        currentH * scaleFactor
      );

      canvas.toBlob((blob) => {
        if (!blob) {
          setErrorMsg("Failed to generate cropped image blob.");
          return;
        }
        const previewUrl = URL.createObjectURL(blob);
        const fileName = `crop-${Date.now()}.webp`;
        const croppedFile = new File([blob], fileName, { type: "image/webp" });

        onCrop?.({
          blob,
          file: croppedFile,
          previewUrl,
          width: outWidth,
          height: outHeight,
          preset: selectedPresetKey,
        });
        onClose?.();
      }, "image/webp", 0.92);
    } catch (err) {
      console.error("Apply crop failed:", err);
      setErrorMsg(err.message || "Failed to crop image.");
    }
  };

  if (!isOpen) return null;

  const validPresets = allowedPresets && Array.isArray(allowedPresets) && allowedPresets.length > 0
    ? allowedPresets.filter((p) => PRESET_RATIOS[p])
    : Object.keys(PRESET_RATIOS);

  const modalContent = (
    <div className="universal-cropper-backdrop" onClick={onClose} role="presentation">
      <div
        className="universal-cropper-modal"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(e) => e.stopPropagation()}
      >
        {/* HEADER */}
        <div className="universal-cropper-header">
          <div className="universal-cropper-title-wrap">
            <h3 className="universal-cropper-title">
              {destinationName ? `CROP ${destinationName.toUpperCase()}` : title}
            </h3>
            <span className="universal-cropper-preset-badge">
              ✓ {activePreset.badge}
            </span>
          </div>
          <button
            type="button"
            className="universal-cropper-close-btn"
            onClick={onClose}
            aria-label="Cancel and Close"
          >
            <X size={18} />
          </button>
        </div>

        {/* PRESET SELECTOR (IF MULTIPLE ALLOWED) */}
        {validPresets.length > 1 && (
          <div className="universal-cropper-presets-bar">
            {validPresets.map((pKey) => {
              const p = PRESET_RATIOS[pKey];
              const isSelected = selectedPresetKey === pKey;
              return (
                <button
                  key={pKey}
                  type="button"
                  className={`universal-cropper-preset-btn ${isSelected ? "is-selected" : ""}`}
                  onClick={() => {
                    setSelectedPresetKey(pKey);
                    handleReset();
                  }}
                  title={p.description}
                >
                  {p.label}
                </button>
              );
            })}
          </div>
        )}

        {/* ERROR NOTIFICATION */}
        {errorMsg && (
          <div className="universal-cropper-error">
            <AlertCircle size={15} />
            <span>{errorMsg}</span>
          </div>
        )}

        {/* VIEWPORT AREA */}
        <div className="universal-cropper-body">
          {imageSrc ? (
            <div
              className="universal-cropper-viewport-wrap"
              style={{
                aspectRatio: activePreset.cssAspect !== "auto" ? activePreset.cssAspect : undefined,
                maxHeight: activePreset.ratio < 1 ? "380px" : "320px",
                maxWidth: activePreset.ratio < 1 ? "285px" : "400px",
              }}
              ref={containerRef}
              onPointerDown={handlePointerDown}
              onPointerMove={handlePointerMove}
              onPointerUp={handlePointerUp}
              onPointerCancel={handlePointerUp}
              onWheel={handleWheel}
            >
              <img
                ref={(el) => {
                  imgRef.current = el;
                  if (el && el.complete && el.naturalWidth > 0 && !imageLoaded) {
                    handleImageLoad({ target: el });
                  }
                }}
                src={imageSrc}
                alt="Crop preview"
                className="universal-cropper-img"
                onLoad={handleImageLoad}
                draggable={false}
                style={{
                  width: baseDimensions.width > 0 ? `${baseDimensions.width}px` : "100%",
                  height: baseDimensions.height > 0 ? `${baseDimensions.height}px` : "auto",
                  maxWidth: "none",
                  maxHeight: "none",
                  transform: `translate(${offset.x}px, ${offset.y}px) scale(${zoom})`,
                  transformOrigin: "center center",
                }}
              />
              <div className="universal-cropper-grid-overlay" pointerEvents="none" />
            </div>
          ) : (
            <div
              className="universal-cropper-empty-drop"
              onClick={() => fileInputRef.current?.click()}
              role="button"
              tabIndex={0}
            >
              <Upload size={32} color="#df806c" />
              <strong>Choose Image from Device</strong>
              <span>Supports JPEG, PNG, WebP up to 35 MB</span>
            </div>
          )}
        </div>

        {/* CONTROLS TOOLBAR */}
        {imageSrc && (
          <div className="universal-cropper-controls">
            <div className="universal-cropper-zoom-row">
              <button
                type="button"
                onClick={() => handleZoomChange(zoom - 0.05)}
                style={{ background: "none", border: "none", cursor: "pointer", display: "flex", alignItems: "center", padding: "2px" }}
                title="Zoom Out"
                aria-label="Zoom Out"
              >
                <ZoomOut size={16} color="#a1a1aa" />
              </button>
              <input
                type="range"
                min="1.0"
                max="3.0"
                step="0.02"
                value={zoom}
                onChange={(e) => handleZoomChange(parseFloat(e.target.value))}
                className="universal-cropper-slider"
                aria-label="Zoom Level"
              />
              <button
                type="button"
                onClick={() => handleZoomChange(zoom + 0.05)}
                style={{ background: "none", border: "none", cursor: "pointer", display: "flex", alignItems: "center", padding: "2px" }}
                title="Zoom In"
                aria-label="Zoom In"
              >
                <ZoomIn size={16} color="#a1a1aa" />
              </button>
              <span className="universal-cropper-zoom-text">{Math.round(zoom * 100)}%</span>
            </div>

            <div className="universal-cropper-actions-row">
              <div className="universal-cropper-left-actions">
                <button
                  type="button"
                  className="universal-cropper-btn-subtle"
                  onClick={handleReset}
                  title="Reset position and zoom"
                >
                  <RotateCcw size={14} /> Reset
                </button>
                <button
                  type="button"
                  className="universal-cropper-btn-subtle"
                  onClick={() => fileInputRef.current?.click()}
                >
                  <Upload size={14} /> Replace
                </button>
              </div>

              <div className="universal-cropper-right-actions">
                <button
                  type="button"
                  className="universal-cropper-btn-cancel"
                  onClick={onClose}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="universal-cropper-btn-apply"
                  disabled={!imageLoaded || !imageSrc}
                  onClick={handleApplyCrop}
                >
                  <Check size={16} /> Apply & Upload
                </button>
              </div>
            </div>
          </div>
        )}

        <input
          ref={fileInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          style={{ display: "none" }}
          onChange={handleFileChange}
        />
      </div>
    </div>
  );

  return createPortal(modalContent, document.body);
}
