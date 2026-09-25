import React, { useState, useRef, useEffect, useMemo, useCallback } from "react";
import { createPortal } from "react-dom";
import { X, ZoomIn, ZoomOut, RotateCcw, Check, Upload, AlertCircle } from "lucide-react";
import "./ImageCropperModal.css";

const MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB

const RATIO_PRESETS = {
  "16:9": { label: "Landscape 16:9 (Wide Banner)", ratio: 16 / 9, cssAspect: "16 / 9", width: 1600, height: 900, badge: "1600 × 900 (Landscape 16:9)" },
  "3:4": { label: "Portrait 3:4 (Poster / Card)", ratio: 3 / 4, cssAspect: "3 / 4", width: 900, height: 1200, badge: "900 × 1200 (Portrait 3:4)" },
  "4:5": { label: "Portrait 4:5 (Studio Feature)", ratio: 4 / 5, cssAspect: "4 / 5", width: 1200, height: 1500, badge: "1200 × 1500 (Portrait 4:5)" },
  "1:1": { label: "Square 1:1 (Community)", ratio: 1, cssAspect: "1 / 1", width: 1080, height: 1080, badge: "1080 × 1080 (Square 1:1)" },
  "4:3": { label: "Standard 4:3 (Feature)", ratio: 4 / 3, cssAspect: "4 / 3", width: 1400, height: 1050, badge: "1400 × 1050 (Standard 4:3)" },
  "9:16": { label: "Vertical 9:16 (Reels / Stories)", ratio: 9 / 16, cssAspect: "9 / 16", width: 1080, height: 1920, badge: "1080 × 1920 (Vertical 9:16)" },
  "natural": { label: "Original / Natural", ratio: null, cssAspect: "auto", width: null, height: null, badge: "Original / Natural Ratio" },
};

export default function ImageCropperModal({
  isOpen,
  aspectRatio = "16:9",
  allowedRatios = null, // e.g. ["16:9"] or ["3:4"] or ["16:9", "3:4", "natural"]
  targetWidth = null,
  targetHeight = null,
  title = "Crop & Adjust Image",
  initialImage = null,
  onCrop,
  onClose,
}) {
  const [imageSrc, setImageSrc] = useState(null);
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const [dragStart, setDragStart] = useState({ x: 0, y: 0 });
  const [selectedRatio, setSelectedRatio] = useState(aspectRatio);
  const [errorMsg, setErrorMsg] = useState("");
  const [imageLoaded, setImageLoaded] = useState(false);
  const [naturalDimensions, setNaturalDimensions] = useState({ width: 0, height: 0 });

  const containerRef = useRef(null);
  const imgRef = useRef(null);
  const fileInputRef = useRef(null);

  // Sync selectedRatio with prop
  useEffect(() => {
    setSelectedRatio(aspectRatio || "16:9");
  }, [aspectRatio]);

  // Trap ESC on capture phase to close cropper only
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();
        onClose?.();
      }
    };
    window.addEventListener("keydown", handleKeyDown, true);
    return () => window.removeEventListener("keydown", handleKeyDown, true);
  }, [isOpen, onClose]);

  // Lock body scroll while crop modal is active
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
          setErrorMsg("File size exceeds 5MB limit. Please choose a smaller image.");
          return;
        }
        let active = true;
        const reader = new FileReader();
        reader.onload = () => {
          if (active) {
            setImageSrc(reader.result);
          }
        };
        reader.readAsDataURL(initialImage);
        return () => {
          active = false;
        };
      }
    }
  }, [isOpen, initialImage]);

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > MAX_FILE_SIZE_BYTES) {
      setErrorMsg("File size exceeds 5MB limit. Please upload an image up to 5MB.");
      return;
    }

    setErrorMsg("");
    const reader = new FileReader();
    reader.onload = () => {
      setImageSrc(reader.result);
      setZoom(1);
      setOffset({ x: 0, y: 0 });
      setImageLoaded(false);
    };
    reader.readAsDataURL(file);
  };

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

  // Clamps offset within boundaries so the image always covers 100% of the crop box
  const clampOffset = useCallback((x, y, zoomVal = zoom) => {
    if (!containerRef.current || !imgRef.current) return { x, y };
    const cropBox = containerRef.current.getBoundingClientRect();
    const img = imgRef.current;
    const nw = img.naturalWidth || 800;
    const nh = img.naturalHeight || 600;
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
  }, [zoom]);

  // Zoom handler with 1.0 (cover scale) minimum boundary
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

  // Wheel zoom with 1.0 (cover scale) minimum boundary
  const handleWheel = (e) => {
    if (!imageLoaded) return;
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.1 : 0.1;
    handleZoomChange(zoom + delta);
  };

  const resetTransform = () => {
    setZoom(1);
    setOffset({ x: 0, y: 0 });
  };

  // Compute active ratio config
  const activePreset = useMemo(() => {
    if (selectedRatio === "natural") {
      const nw = naturalDimensions.width || 1200;
      const nh = naturalDimensions.height || 800;
      const natRatio = nw / nh;
      return {
        label: "Original / Natural",
        ratio: natRatio,
        cssAspect: `${nw} / ${nh}`,
        width: Math.min(nw, 1920),
        height: Math.round(Math.min(nw, 1920) / natRatio),
        badge: `${nw} × ${nh} (Original Natural)`,
      };
    }
    return RATIO_PRESETS[selectedRatio] || RATIO_PRESETS["16:9"];
  }, [selectedRatio, naturalDimensions]);

  // Crop box style calculation
  const cropBoxStyle = useMemo(() => {
    const isPortrait = activePreset.ratio < 1;
    const isSquare = Math.abs(activePreset.ratio - 1) < 0.05;
    return {
      aspectRatio: activePreset.cssAspect,
      maxHeight: isPortrait ? "360px" : isSquare ? "320px" : "280px",
      maxWidth: isPortrait ? "300px" : isSquare ? "320px" : "480px",
    };
  }, [activePreset]);

  // Generate cropped output
  const handleApply = useCallback(() => {
    if (!imgRef.current || !containerRef.current) return;

    const img = imgRef.current;
    const cropBox = containerRef.current.getBoundingClientRect();

    const outW = targetWidth || activePreset.width || 1600;
    const outH = targetHeight || activePreset.height || Math.round(outW / (activePreset.ratio || 1));

    const canvas = document.createElement("canvas");
    canvas.width = outW;
    canvas.height = outH;
    const ctx = canvas.getContext("2d");

    const scaleFactor = outW / cropBox.width;

    const imgAspect = img.naturalWidth / img.naturalHeight;
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

    canvas.toBlob(
      (blob) => {
        if (!blob) return;
        const dataUrl = canvas.toDataURL("image/jpeg", 0.92);
        onCrop?.(blob, dataUrl, {
          aspectRatio: selectedRatio,
          width: outW,
          height: outH,
        });
        onClose?.();
      },
      "image/jpeg",
      0.92
    );
  }, [activePreset, offset, onCrop, onClose, selectedRatio, targetHeight, targetWidth, zoom]);

  if (!isOpen) return null;

  const selectableRatios = allowedRatios && allowedRatios.length > 0 ? allowedRatios : [selectedRatio];

  return createPortal(
    <div
      className="image-cropper-overlay"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="cropper-dialog-title"
    >
      <div className="image-cropper-modal" onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className="image-cropper-header">
          <div className="image-cropper-title-area">
            <h3 id="cropper-dialog-title" className="image-cropper-title">{title}</h3>
            <span className="image-cropper-badge">{activePreset.badge}</span>
          </div>
          <button className="cropper-close-btn" onClick={onClose} aria-label="Close cropper">
            <X size={18} />
          </button>
        </div>

        {/* Error Alert */}
        {errorMsg && (
          <div className="cropper-error-banner">
            <AlertCircle size={16} />
            <span>{errorMsg}</span>
          </div>
        )}

        {/* Ratio Selector (only shown if multiple ratios are configured) */}
        {selectableRatios.length > 1 && (
          <div className="cropper-ratio-bar">
            <span className="ratio-label">Target Format:</span>
            <div className="ratio-pills">
              {selectableRatios.map((ratioKey) => {
                const preset = RATIO_PRESETS[ratioKey] || { label: ratioKey };
                return (
                  <button
                    key={ratioKey}
                    type="button"
                    className={`ratio-pill ${selectedRatio === ratioKey ? "active" : ""}`}
                    onClick={() => {
                      setSelectedRatio(ratioKey);
                      resetTransform();
                    }}
                  >
                    {preset.label}
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {/* Crop Viewport Area */}
        <div className="cropper-viewport-wrap">
          {imageSrc ? (
            <div
              className="cropper-cropbox"
              ref={containerRef}
              style={cropBoxStyle}
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
                className="cropper-source-img"
                onLoad={handleImageLoad}
                draggable={false}
                style={{
                  transform: `translate(${offset.x}px, ${offset.y}px) scale(${zoom})`,
                }}
              />
              {/* Rule-of-thirds grid */}
              <div className="cropper-grid-overlay">
                <div className="cropper-grid-line h1" />
                <div className="cropper-grid-line h2" />
                <div className="cropper-grid-line v1" />
                <div className="cropper-grid-line v2" />
              </div>
            </div>
          ) : (
            <div className="cropper-empty-dropzone" onClick={() => fileInputRef.current?.click()}>
              <Upload size={36} className="cropper-dropzone-icon" />
              <p className="cropper-dropzone-text">Click or drag image here to crop</p>
              <span className="cropper-dropzone-hint">Supports JPEG, PNG, WebP up to 5MB</span>
            </div>
          )}

          <input
            ref={fileInputRef}
            type="file"
            accept="image/png,image/jpeg,image/webp,image/jpg"
            style={{ display: "none" }}
            onChange={handleFileChange}
          />
        </div>

        {/* Controls Toolbar */}
        {imageSrc && (
          <div className="cropper-controls-toolbar">
            <div className="cropper-zoom-controls">
              <ZoomOut size={16} className="zoom-icon" />
              <input
                type="range"
                min="1"
                max="3"
                step="0.05"
                value={zoom}
                onChange={(e) => handleZoomChange(parseFloat(e.target.value))}
                className="cropper-zoom-slider"
                aria-label="Image Zoom Slider"
              />
              <ZoomIn size={16} className="zoom-icon" />
              <span className="zoom-val">{Math.round(zoom * 100)}%</span>
            </div>

            <div className="cropper-tool-btns">
              <button
                type="button"
                className="cropper-btn-subtle"
                onClick={resetTransform}
                title="Reset zoom and position"
              >
                <RotateCcw size={14} />
                <span>Reset</span>
              </button>
              <button
                type="button"
                className="cropper-btn-subtle"
                onClick={() => fileInputRef.current?.click()}
              >
                <Upload size={14} />
                <span>Replace Image</span>
              </button>
            </div>
          </div>
        )}

        {/* Footer Actions */}
        <div className="image-cropper-footer">
          <button type="button" className="cropper-btn-cancel" onClick={onClose}>
            Cancel
          </button>
          <button
            type="button"
            className="cropper-btn-apply"
            disabled={!imageLoaded || !imageSrc}
            onClick={handleApply}
          >
            <Check size={16} />
            <span>Apply Crop</span>
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
}
