import { useCallback, useEffect, useRef, useState } from "react";
import { Upload, Trash2, Eye, X, Image as ImageIcon, AlertCircle } from "lucide-react";
import { trainerApi } from "../../services/trainerApi";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import "./TrainerGallery.css";

const MAX_IMAGES = 12;
const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB
const ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp"];

export default function TrainerGallery() {
  const mountedRef = useRef(true);
  const fileInputRef = useRef(null);

  const [images, setImages] = useState([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [selectedImage, setSelectedImage] = useState(null);

  const loadGallery = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const data = await trainerApi.getGallery();
      if (!mountedRef.current) return;
      setImages(data || []);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(getApiErrorMessage(err, "Unable to load gallery images."));
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    loadGallery();
    return () => {
      mountedRef.current = false;
    };
  }, [loadGallery]);

  async function handleFileChange(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    // Reset input so same file can be selected again
    event.target.value = "";

    setError("");
    setSuccess("");

    if (!ALLOWED_TYPES.includes(file.type.toLowerCase())) {
      setError("Only JPG, PNG and WEBP image formats are supported.");
      return;
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      setError("File size must not exceed 10 MB.");
      return;
    }

    if (images.length >= MAX_IMAGES) {
      setError(`Maximum ${MAX_IMAGES} images allowed in gallery.`);
      return;
    }

    setUploading(true);

    try {
      const formData = new FormData();
      formData.append("file", file);

      const newImage = await trainerApi.uploadGalleryImage(formData);

      if (!mountedRef.current) return;

      setImages((prev) => [...prev, newImage]);
      setSuccess("IMAGE UPLOADED: Portfolio photo added to gallery successfully.");

      setTimeout(() => {
        if (mountedRef.current) setSuccess("");
      }, 4000);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(getApiErrorMessage(err, "Could not upload image. Please try again."));
    } finally {
      if (mountedRef.current) {
        setUploading(false);
      }
    }
  }

  async function handleDelete(imageId) {
    if (!window.confirm("Are you sure you want to remove this photo from your gallery?")) {
      return;
    }

    setError("");
    setSuccess("");

    try {
      await trainerApi.deleteGalleryImage(imageId);
      if (!mountedRef.current) return;

      setImages((prev) => prev.filter((img) => img.id !== imageId));
      setSuccess("IMAGE REMOVED: Photo deleted from your gallery.");

      if (selectedImage?.id === imageId) {
        setSelectedImage(null);
      }

      setTimeout(() => {
        if (mountedRef.current) setSuccess("");
      }, 4000);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(getApiErrorMessage(err, "Failed to delete gallery image."));
    }
  }

  return (
    <section className="trainer-gallery-section">
      <div className="trainer-gallery-header">
        <div>
          <span className="trainer-gallery-kicker">PORTFOLIO & MEDIA</span>
          <h2>Creative Gallery</h2>
          <p>Showcase your dance performances, workshops, and high-energy studio moments.</p>
        </div>

        <div className="trainer-gallery-count-badge">
          <ImageIcon size={16} />
          <span>{images.length} / {MAX_IMAGES} photos</span>
        </div>
      </div>

      {/* Banner Alerts */}
      {success && (
        <div className="trainer-gallery-alert success" role="status">
          {success}
        </div>
      )}

      {error && (
        <div className="trainer-gallery-alert error" role="alert">
          <AlertCircle size={16} />
          <span>{error}</span>
        </div>
      )}

      {/* Upload Zone */}
      <div className="trainer-gallery-upload-card">
        <input
          ref={fileInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          style={{ display: "none" }}
          onChange={handleFileChange}
          disabled={uploading || images.length >= MAX_IMAGES}
        />

        <div className="trainer-gallery-dropzone">
          <div className="upload-icon-circle">
            <Upload size={22} />
          </div>

          <div className="upload-text">
            <strong>{uploading ? "UPLOADING IMAGE..." : "Add Photo to Gallery"}</strong>
            <span>JPG, PNG or WEBP up to 10 MB ({MAX_IMAGES - images.length} slots left)</span>
          </div>

          <button
            type="button"
            className="trainer-primary-btn upload-btn"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading || images.length >= MAX_IMAGES}
          >
            {uploading ? "UPLOADING..." : "CHOOSE FILE"}
          </button>
        </div>
      </div>

      {/* Gallery Grid */}
      {loading ? (
        <div className="trainer-gallery-loading">
          <span>Loading portfolio gallery...</span>
        </div>
      ) : images.length === 0 ? (
        <div className="trainer-gallery-empty">
          <ImageIcon size={36} />
          <h4>No Gallery Photos Yet</h4>
          <p>Upload your performance photos, choreography stills, or workshop action shots to complete your trainer profile.</p>
        </div>
      ) : (
        <div className="trainer-gallery-grid">
          {images.map((img) => {
            const imageUrl = trainerApi.getGalleryImageUrl(img.id);
            return (
              <div key={img.id} className="trainer-gallery-item">
                <img
                  src={imageUrl}
                  alt={img.fileName || "Trainer portfolio"}
                  loading="lazy"
                />

                <div className="trainer-gallery-overlay">
                  <button
                    type="button"
                    className="gallery-action-btn view-btn"
                    title="View Fullsize"
                    onClick={() => setSelectedImage({ ...img, url: imageUrl })}
                  >
                    <Eye size={18} />
                  </button>

                  <button
                    type="button"
                    className="gallery-action-btn delete-btn"
                    title="Delete Photo"
                    onClick={() => handleDelete(img.id)}
                  >
                    <Trash2 size={18} />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Lightbox Modal */}
      {selectedImage && (
        <div className="trainer-gallery-lightbox" onClick={() => setSelectedImage(null)}>
          <div className="lightbox-content" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="lightbox-close"
              onClick={() => setSelectedImage(null)}
            >
              <X size={20} />
            </button>

            <img src={selectedImage.url} alt={selectedImage.fileName} />

            <div className="lightbox-meta">
              <span>{selectedImage.fileName}</span>
              <button
                type="button"
                className="lightbox-delete-btn"
                onClick={() => handleDelete(selectedImage.id)}
              >
                <Trash2 size={14} /> Remove Photo
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
