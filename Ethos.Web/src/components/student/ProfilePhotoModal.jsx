import { useEffect } from "react";
import { X } from "lucide-react";
import "./ProfilePhotoModal.css";

export default function ProfilePhotoModal({
  isOpen,
  photoUrl,
  name = "Student",
  onClose,
}) {
  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        onClose?.();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = originalOverflow;
    };
  }, [isOpen, onClose]);

  if (!isOpen || !photoUrl) return null;

  return (
    <div
      className="student-photo-modal-backdrop"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-label={name ? `${name} Profile Photo` : "Student Profile Photo"}
    >
      <div
        className="student-photo-modal-card"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="student-photo-modal-header">
          <div className="student-photo-modal-meta">
            <span className="student-photo-modal-tag">ETHOS IDENTITY</span>
            <h3 className="student-photo-modal-name">{name}</h3>
          </div>
          <button
            type="button"
            className="student-photo-modal-close"
            onClick={onClose}
            aria-label="Close photo viewer"
          >
            <X size={20} />
          </button>
        </div>

        <div className="student-photo-modal-body">
          <img
            src={photoUrl}
            alt={name || "Student Profile Photo"}
            className="student-photo-modal-img"
          />
        </div>
      </div>
    </div>
  );
}
