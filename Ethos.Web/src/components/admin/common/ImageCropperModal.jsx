import React, { useState, useRef, useEffect } from "react";
import { createPortal } from "react-dom";
import { X, ZoomIn, ZoomOut, RotateCcw, Check, Upload, AlertCircle } from "lucide-react";
import "./ImageCropperModal.css";

const MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB

export default function ImageCropperModal({
  isOpen,
  aspectRatio = "3:4", // "3:4" | "16:9"
  targetWidth = 900,
  targetHeight = 1200,
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

  const containerRef = useRef(null);
  const imgRef = useRef(null);
  const fileInputRef = useRef(null);

  // Sync selectedRatio with prop
  useEffect(() => {
    setSelectedRatio(aspectRatio);
  }, [aspectRatio]);

  // Load initial image if provided
  useEffect(() => {
    if (!isOpen) {
      setImageSrc(null);
      setImageLoaded(false);
      setZoom(1);
      setOffset({ x: 0, y: 0 });
      setErrorMsg("");
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
        const url = URL.createObjectURL(initialImage);
        setImageSrc(url);
        return () => URL.revokeObjectURL(url);
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
    imgRef.current = e.target;
    setImageLoaded(true);
    setZoom(1);
    setOffset({ x: 0, y: 0 });
  };

  // Drag handlers
  const handleMouseDown = (e) => {
    if (!imageLoaded) return;
    setIsDragging(true);
    setDragStart({ x: e.clientX - offset.x, y: e.clientY - offset.y });
  };

  const handleMouseMove = (e) => {
    if (!isDragging) return;
    setOffset({
      x: e.clientX - dragStart.x,
      y: e.clientY - dragStart.y,
    });
  };

  const handleMouseUp = () => {
    setIsDragging(false);
  };

  // Wheel zoom
  const handleWheel = (e) => {
    if (!imageLoaded) return;
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.1 : 0.1;
    setZoom((prev) => Math.min(Math.max(0.5, prev + delta), 3.5));
  };

  const resetTransform = () => {
    setZoom(1);
    setOffset({ x: 0, y: 0 });
  };

  // Generate cropped output
  const handleApply = () => {
    if (!imgRef.current || !containerRef.current) return;

    const img = imgRef.current;
    const cropBox = containerRef.current.getBoundingClientRect();

    const outW = selectedRatio === "16:9" ? 1600 : 900;
    const outH = selectedRatio === "16:9" ? 900 : 1200;

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
        onCrop(blob, dataUrl, {
          aspectRatio: selectedRatio,
          width: outW,
          height: outH,
        });
        onClose();
      },
      "image/jpeg",
      0.92
    );
  };

  if (!isOpen) return null;

  return createPortal(
    <div className="image-cropper-overlay" onClick={onClose}>
      <div className="image-cropper-modal" onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className="image-cropper-header">
          <div className="image-cropper-title-area">
            <h3 className="image-cropper-title">{title}</h3>
            <span className="image-cropper-badge">
              {selectedRatio === "3:4" ? "900 × 1200 (Portrait 3:4)" : "1600 × 900 (Banner 16:9)"}
            </span>
          </div>
          <button className="cropper-close-btn" onClick={onClose} aria-label="Close">
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

        {/* Ratio Selector */}
        <div className="cropper-ratio-bar">
          <span className="ratio-label">Target Format:</span>
          <div className="ratio-pills">
            <button
              type="button"
              className={`ratio-pill ${selectedRatio === "3:4" ? "active" : ""}`}
              onClick={() => setSelectedRatio("3:4")}
            >
              Portrait 3:4 (Poster / Card)
            </button>
            <button
              type="button"
              className={`ratio-pill ${selectedRatio === "16:9" ? "active" : ""}`}
              onClick={() => setSelectedRatio("16:9")}
            >
              Landscape 16:9 (Wide Banner)
            </button>
          </div>
        </div>

        {/* Crop Viewport Area */}
        <div className="cropper-viewport-wrap">
          {imageSrc ? (
            <div
              className="cropper-cropbox"
              ref={containerRef}
              style={{
                aspectRatio: selectedRatio === "16:9" ? "16 / 9" : "3 / 4",
                maxHeight: selectedRatio === "16:9" ? "300px" : "380px",
              }}
              onMouseDown={handleMouseDown}
              onMouseMove={handleMouseMove}
              onMouseUp={handleMouseUp}
              onMouseLeave={handleMouseUp}
              onWheel={handleWheel}
            >
              <img
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
                min="0.5"
                max="3"
                step="0.05"
                value={zoom}
                onChange={(e) => setZoom(parseFloat(e.target.value))}
                className="cropper-zoom-slider"
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
