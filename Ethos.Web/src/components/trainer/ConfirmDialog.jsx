import { useEffect } from "react";

export default function ConfirmDialog({
  open,
  title = "Are you sure?",
  description,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  onConfirm,
  onCancel,
  loading = false,
  destructive = false,
}) {
  useEffect(() => {
    if (!open) return;

    function handleKeyDown(event) {
      if (event.key === "Escape" && !loading) {
        onCancel?.();
      }
    }

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open, loading, onCancel]);

  if (!open) {
    return null;
  }

  return (
    <div
      className="trainer-confirm-backdrop"
      role="presentation"
      onMouseDown={(event) => {
        if (
          event.target === event.currentTarget &&
          !loading
        ) {
          onCancel?.();
        }
      }}
    >
      <div
        className="trainer-confirm-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="trainer-confirm-title"
        aria-describedby={
          description
            ? "trainer-confirm-description"
            : undefined
        }
        onMouseDown={(event) => {
          event.stopPropagation();
        }}
      >
        <div className="trainer-confirm-symbol" aria-hidden="true">
          {destructive ? "!" : "?"}
        </div>

        <h2 id="trainer-confirm-title">
          {title}
        </h2>

        {description && (
          <p id="trainer-confirm-description">
            {description}
          </p>
        )}

        <div className="trainer-confirm-actions">
          <button
            type="button"
            className="trainer-button trainer-button--secondary"
            onClick={onCancel}
            disabled={loading}
          >
            {cancelLabel}
          </button>

          <button
            type="button"
            className={
              destructive
                ? "trainer-button trainer-button--danger"
                : "trainer-button trainer-button--primary"
            }
            onClick={onConfirm}
            disabled={loading}
          >
            {loading ? "Processing..." : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
