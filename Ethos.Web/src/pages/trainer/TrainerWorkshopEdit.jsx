import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import { getMediaUrl } from "../../utils/mediaUrl";
import "./TrainerWorkshopEdit.css";

function toInputDate(isoString) {
  if (!isoString) return "";
  const date = new Date(isoString);
  if (Number.isNaN(date.getTime())) return "";
  return date.toISOString().split("T")[0];
}

function toInputTime(timeString) {
  if (!timeString) return "";
  const parts = timeString.split(":");
  if (parts.length < 2) return "";
  const hh = parts[0].padStart(2, "0");
  const mm = parts[1].padStart(2, "0");
  return `${hh}:${mm}`;
}

function toUtcDate(value) {
  if (!value) return null;
  return new Date(`${value}T00:00:00`).toISOString();
}

export default function TrainerWorkshopEdit() {
  const { id } = useParams();
  const navigate = useNavigate();
  const mountedRef = useRef(true);

  const [form, setForm] = useState({
    title: "",
    description: "",
    danceStyle: "",
    level: "Beginner",
    workshopDate: "",
    startTime: "",
    endTime: "",
    venue: "",
    proposedPrice: "",
    capacity: "",
    imageUrl: "",
  });

  const imageInputRef = useRef(null);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [workshopStatus, setWorkshopStatus] = useState("");
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState("");

  const handleImageChange = (event) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
    if (!allowedTypes.includes(file.type)) {
      setError("Please upload a JPG, PNG or WEBP image.");
      return;
    }

    const maxSize = 35 * 1024 * 1024;
    if (file.size > maxSize) {
      setError("Workshop image cannot exceed 35 MB.");
      return;
    }

    setError("");
    setImageFile(file);

    const previewUrl = URL.createObjectURL(file);
    setImagePreview(previewUrl);

    const reader = new FileReader();
    reader.onloadend = () => {
      if (!mountedRef.current) return;
      setForm((current) => ({
        ...current,
        imageUrl: reader.result,
      }));
    };
    reader.readAsDataURL(file);
  };

  const handleRemoveImage = () => {
    setImageFile(null);
    setImagePreview("");
    setForm((current) => ({
      ...current,
      imageUrl: "",
    }));
    if (imageInputRef.current) {
      imageInputRef.current.value = "";
    }
  };

  const loadWorkshop = useCallback(async () => {
    try {
      setLoading(true);
      setError("");
      const response = await trainerApi.getWorkshop(id);
      if (!mountedRef.current) return;

      const w = response?.data || response;
      setWorkshopStatus(w?.status);

      if (w?.status !== "Draft" && w?.status !== "Rejected") {
        setError("Workshops can only be edited when in Draft or Rejected status.");
        return;
      }

      setForm({
        title: w.title || "",
        description: w.description || "",
        danceStyle: w.danceStyle || "",
        level: w.level || "Beginner",
        workshopDate: toInputDate(w.workshopDate),
        startTime: toInputTime(w.startTime),
        endTime: toInputTime(w.endTime),
        venue: w.venue || "",
        proposedPrice: w.trainerProposedPrice ?? w.price ?? "",
        capacity: w.capacity ?? "",
        imageUrl: w.imageUrl || "",
      });

      if (w.imageUrl) {
        setImagePreview(getMediaUrl(w.imageUrl));
      }
    } catch (err) {
      if (!mountedRef.current) return;
      setError(
        getApiErrorMessage(err, "We couldn't load the workshop details for editing.")
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, [id]);

  useEffect(() => {
    mountedRef.current = true;
    loadWorkshop();
    return () => {
      mountedRef.current = false;
    };
  }, [loadWorkshop]);

  function updateField(event) {
    const { name, value } = event.target;

    setForm((current) => ({
      ...current,
      [name]: value,
    }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError("");

    if (!form.title.trim()) {
      setError("Workshop title is required.");
      return;
    }

    if (!form.description.trim()) {
      setError("Workshop description is required.");
      return;
    }

    if (!form.danceStyle.trim()) {
      setError("Dance style is required.");
      return;
    }

    if (!form.workshopDate) {
      setError("Workshop date is required.");
      return;
    }

    if (!form.startTime || !form.endTime) {
      setError("Start and end time are required.");
      return;
    }

    if (form.endTime <= form.startTime) {
      setError("End time must be after start time.");
      return;
    }

    if (!form.venue.trim()) {
      setError("Venue is required.");
      return;
    }

    if (Number(form.proposedPrice) < 0) {
      setError("Workshop price cannot be negative.");
      return;
    }

    if (Number(form.capacity) <= 0) {
      setError("Capacity must be greater than zero.");
      return;
    }

    const payload = {
      title: form.title.trim(),
      description: form.description.trim(),
      danceStyle: form.danceStyle.trim(),
      level: form.level,
      workshopDate: toUtcDate(form.workshopDate),
      startTime: form.startTime.length === 5 ? `${form.startTime}:00` : form.startTime,
      endTime: form.endTime.length === 5 ? `${form.endTime}:00` : form.endTime,
      venue: form.venue.trim(),
      proposedPrice: Number(form.proposedPrice),
      capacity: Number(form.capacity),
      imageUrl: form.imageUrl.trim() || null,
    };

    try {
      setSaving(true);
      await trainerApi.updateWorkshop(id, payload);
      if (!mountedRef.current) return;
      navigate(`/trainer/workshops/${id}`);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(
        getApiErrorMessage(err, "The workshop could not be updated.")
      );
    } finally {
      if (mountedRef.current) {
        setSaving(false);
      }
    }
  }

  if (loading) {
    return <LoadingState label="Loading workshop details..." />;
  }

  if (error && (workshopStatus !== "Draft" && workshopStatus !== "Rejected")) {
    return (
      <ErrorState
        title="Cannot edit workshop"
        description={error}
        action={
          <Link
            to={`/trainer/workshops/${id}`}
            className="trainer-primary-button"
          >
            BACK TO WORKSHOP
          </Link>
        }
      />
    );
  }

  return (
    <main className="trainer-workshop-create-page">
      <div className="workshop-form-shell">
        <Link
          to={`/trainer/workshops/${id}`}
          className="workshop-back-button"
        >
          ← CANCEL EDITING
        </Link>

        <header className="workshop-form-heading">
          <span className="trainer-section-eyebrow">
            EDIT WORKSHOP / {workshopStatus.toUpperCase()}
          </span>

          <h1>
            Refine <em>experience.</em>
          </h1>

          <p>
            Update your workshop details below. Saving changes will reset the status to Draft.
          </p>
        </header>

        {error && (
          <div className="workshop-form-error" role="alert">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <section className="workshop-form-section">
            <div className="workshop-form-section-title">
              <h2>Workshop Identity</h2>
            </div>

            <div className="workshop-form-grid">
              <label className="wide">
                <span>TITLE</span>
                <input
                  name="title"
                  value={form.title}
                  onChange={updateField}
                  placeholder="Contemporary Floorwork Masterclass"
                />
              </label>

              <label className="wide">
                <span>DESCRIPTION</span>
                <textarea
                  name="description"
                  value={form.description}
                  onChange={updateField}
                  rows={5}
                  placeholder="Describe what students will explore..."
                />
              </label>

              <label>
                <span>DANCE STYLE</span>
                <input
                  name="danceStyle"
                  value={form.danceStyle}
                  onChange={updateField}
                  placeholder="Contemporary"
                />
              </label>

              <label>
                <span>LEVEL</span>
                <select
                  name="level"
                  value={form.level}
                  onChange={updateField}
                >
                  <option>Beginner</option>
                  <option>Intermediate</option>
                  <option>Advanced</option>
                  <option>Open Level</option>
                </select>
              </label>
            </div>
          </section>

          <section className="workshop-form-section">
            <div className="workshop-form-section-title">
              <h2>Schedule & Venue</h2>
            </div>

            <div className="workshop-form-grid">
              <label>
                <span>DATE</span>
                <input
                  type="date"
                  name="workshopDate"
                  value={form.workshopDate}
                  onChange={updateField}
                />
              </label>

              <label>
                <span>START TIME</span>
                <input
                  type="time"
                  name="startTime"
                  value={form.startTime}
                  onChange={updateField}
                />
              </label>

              <label>
                <span>END TIME</span>
                <input
                  type="time"
                  name="endTime"
                  value={form.endTime}
                  onChange={updateField}
                />
              </label>

              <label className="wide">
                <span>VENUE</span>
                <input
                  name="venue"
                  value={form.venue}
                  onChange={updateField}
                  placeholder="Studio A - Ethos HQ"
                />
              </label>
            </div>
          </section>

          <section className="workshop-form-section">
            <div className="workshop-form-section-title">
              <h2>Booking Setup</h2>
            </div>

            <div className="workshop-form-grid">
              <label>
                <span>PROPOSED PRICE (INR)</span>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  name="proposedPrice"
                  value={form.proposedPrice}
                  onChange={updateField}
                  placeholder="1500"
                />
              </label>

              <label>
                <span>CAPACITY</span>
                <input
                  type="number"
                  min="1"
                  step="1"
                  name="capacity"
                  value={form.capacity}
                  onChange={updateField}
                  placeholder="25"
                />
              </label>
            </div>
          </section>

          <section className="workshop-form-section">
            <div className="workshop-form-section-title">
              <h2>Visual</h2>
            </div>

            <div className="workshop-form-grid">
              <div className="wide">
                <label style={{ marginBottom: "12px" }}>
                  <span>WORKSHOP IMAGE</span>
                </label>

                <div className="trainer-workshop-image-upload">
                  <input
                    ref={imageInputRef}
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    onChange={handleImageChange}
                  />

                  {imagePreview ? (
                    <div className="trainer-workshop-image-preview">
                      <img
                        src={imagePreview}
                        alt="Workshop preview"
                      />

                      <div className="trainer-workshop-image-overlay">
                        <span>CHANGE IMAGE</span>
                      </div>
                    </div>
                  ) : (
                    <div className="trainer-workshop-image-placeholder">
                      <span className="trainer-upload-icon">↑</span>
                      <strong>UPLOAD IMAGE</strong>
                      <span>JPG, PNG or WEBP · MAX 5 MB</span>
                    </div>
                  )}
                </div>

                {imagePreview && (
                  <div className="trainer-workshop-image-actions">
                    <button
                      type="button"
                      onClick={handleRemoveImage}
                    >
                      REMOVE IMAGE
                    </button>
                  </div>
                )}
              </div>
            </div>
          </section>

          <div className="trainer-form-actions">
            <button
              type="button"
              className="trainer-secondary-button"
              onClick={() => navigate(`/trainer/workshops/${id}`)}
            >
              ← CANCEL EDITING
            </button>

            <button
              type="submit"
              className="trainer-primary-button"
              disabled={saving}
            >
              {saving ? "SAVING..." : "SAVE CHANGES →"}
            </button>
          </div>
        </form>
      </div>
    </main>
  );
}
