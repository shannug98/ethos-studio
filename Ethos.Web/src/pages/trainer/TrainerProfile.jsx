import { useCallback, useEffect, useRef, useState } from "react";
import { trainerApi } from "../../services/trainerApi";
import { trainerStateSync } from "../../services/trainerStateSync";
import { useTrainerPermissions } from "../../hooks/useTrainerPermissions";
import { TRAINER_PERMISSIONS } from "../../constants/trainerPermissions";
import { Camera } from "lucide-react";
import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import TrainerGallery from "../../components/trainer/TrainerGallery";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import { getMediaUrl } from "../../utils/mediaUrl";
import "./TrainerProfile.css";

import tierArtwork from "../../assets/trainer/ethos-tier-emblems.png";

const tierPositions = {
  SILVER: "silver",
  GOLD: "gold",
  DIAMOND: "diamond",
  PLATINUM: "platinum",
};

function isValidUrl(string) {
  if (!string || !string.trim()) return true;

  try {
    const url = new URL(string.trim());

    return (
      url.protocol === "http:" ||
      url.protocol === "https:"
    );
  } catch {
    return false;
  }
}

export default function TrainerProfile() {
  const mountedRef = useRef(true);
  const { hasPermission } = useTrainerPermissions();
  const canUpdateProfile = hasPermission(TRAINER_PERMISSIONS.UPDATE_PROFILE);

  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [editMode, setEditMode] = useState(false);

  const [form, setForm] = useState({
    fullName: "",
    city: "",
    profilePhotoUrl: "",
    primaryDanceStyle: "",
    secondaryDanceStyles: "",
    experienceYears: 0,
    currentStudio: "",
    bio: "",
    instagramUrl: "",
    youtubeUrl: "",
  });

  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState({});
  const [successMessage, setSuccessMessage] = useState("");

  const [photoFile, setPhotoFile] = useState(null);
  const [photoPreview, setPhotoPreview] = useState("");
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [showUrlInput, setShowUrlInput] = useState(false);
  const photoInputRef = useRef(null);

  function handlePhotoSelect(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
    if (!allowedTypes.includes(file.type)) {
      setError("Please select a JPG, PNG or WebP image.");
      event.target.value = "";
      return;
    }

    if (file.size > 35 * 1024 * 1024) {
      setError("Profile photo cannot exceed 35 MB.");
      event.target.value = "";
      return;
    }

    setError("");
    setPhotoFile(file);
    setPhotoPreview(URL.createObjectURL(file));
  }

  async function handlePhotoUpload() {
    if (!photoFile) return;

    setUploadingPhoto(true);
    setError("");

    try {
      const formData = new FormData();
      formData.append("file", photoFile);

      const updated = await trainerApi.uploadProfilePhoto(formData);
      if (!mountedRef.current) return;

      setProfile(updated);
      setForm((current) => ({
        ...current,
        profilePhotoUrl: updated?.profilePhotoUrl || "",
      }));

      trainerStateSync.emitPhotoUpdated(updated?.profilePhotoUrl);
      trainerStateSync.emitProfileUpdated(updated);

      setPhotoFile(null);
      if (photoPreview) {
        URL.revokeObjectURL(photoPreview);
        setPhotoPreview("");
      }

      if (photoInputRef.current) {
        photoInputRef.current.value = "";
      }

      setSuccessMessage("PROFILE PHOTO UPDATED: Your new profile photo is now live.");
      setTimeout(() => {
        if (mountedRef.current) setSuccessMessage("");
      }, 5000);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(getApiErrorMessage(err, "Could not upload your profile photo."));
    } finally {
      if (mountedRef.current) {
        setUploadingPhoto(false);
      }
    }
  }

  const loadProfile = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const data = await trainerApi.getProfile();
      if (!mountedRef.current) return;

      setProfile(data);

      setForm({
        fullName: data?.fullName ?? "",
        city: data?.city ?? "",
        profilePhotoUrl: data?.profilePhotoUrl ?? "",
        primaryDanceStyle: data?.primaryDanceStyle ?? "",
        secondaryDanceStyles: data?.secondaryDanceStyles ?? "",
        experienceYears: data?.experienceYears ?? 0,
        currentStudio: data?.currentStudio ?? "",
        bio: data?.bio ?? "",
        instagramUrl: data?.instagramUrl ?? "",
        youtubeUrl: data?.youTubeUrl ?? data?.youtubeUrl ?? "",
      });
    } catch (err) {
      if (!mountedRef.current) return;
      setError(
        getApiErrorMessage(err, "Unable to load your trainer profile.")
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    loadProfile();
    return () => {
      mountedRef.current = false;
    };
  }, [loadProfile]);

  function handleCancelEdit() {
    setEditMode(false);
    setFieldErrors({});
    setError("");

    if (profile) {
      setForm({
        fullName: profile.fullName ?? "",
        city: profile.city ?? "",
        profilePhotoUrl: profile.profilePhotoUrl ?? "",
        primaryDanceStyle: profile.primaryDanceStyle ?? "",
        secondaryDanceStyles: profile.secondaryDanceStyles ?? "",
        experienceYears: profile.experienceYears ?? 0,
        currentStudio: profile.currentStudio ?? "",
        bio: profile.bio ?? "",
        instagramUrl: profile.instagramUrl ?? "",
        youtubeUrl: profile.youTubeUrl ?? profile.youtubeUrl ?? "",
      });
    }
  }

  function validateForm() {
    const errors = {};

    if (!form.fullName.trim()) {
      errors.fullName = "Full name is required.";
    }

    if (!form.city.trim()) {
      errors.city = "City is required.";
    }

    if (!form.primaryDanceStyle.trim()) {
      errors.primaryDanceStyle = "Primary dance style is required.";
    }

    if (
      form.experienceYears === "" ||
      form.experienceYears === null ||
      Number(form.experienceYears) < 0
    ) {
      errors.experienceYears = "Experience must be 0 or greater.";
    }

    if (!form.bio.trim()) {
      errors.bio = "Bio is required.";
    }

    if (form.instagramUrl.trim() && !isValidUrl(form.instagramUrl)) {
      errors.instagramUrl = "Please enter a valid Instagram URL.";
    }

    if (form.youtubeUrl.trim() && !isValidUrl(form.youtubeUrl)) {
      errors.youtubeUrl = "Please enter a valid YouTube URL.";
    }

    setFieldErrors(errors);

    return Object.keys(errors).length === 0;
  }

  async function handleSave() {
    setError("");
    setSuccessMessage("");

    if (!validateForm()) {
      return;
    }

    setSaving(true);

    try {
      const payload = {
        fullName: form.fullName.trim(),
        city: form.city.trim() || null,
        profilePhotoUrl: form.profilePhotoUrl.trim() || null,
        primaryDanceStyle: form.primaryDanceStyle.trim() || null,
        secondaryDanceStyles: form.secondaryDanceStyles.trim() || null,
        experienceYears: Number(form.experienceYears),
        currentStudio: form.currentStudio.trim() || null,
        bio: form.bio.trim() || null,
        instagramUrl: form.instagramUrl.trim() || null,
        youTubeUrl: form.youtubeUrl.trim() || null,
      };

      const updated = await trainerApi.updateProfile(payload);
      if (!mountedRef.current) return;

      const finalProfile = updated || { ...profile, ...payload };
      setProfile(finalProfile);
      setEditMode(false);
      setSuccessMessage("PROFILE UPDATED: Your trainer profile has been updated successfully.");

      trainerStateSync.emitProfileUpdated(finalProfile);
      if (finalProfile.profilePhotoUrl) {
        trainerStateSync.emitPhotoUpdated(finalProfile.profilePhotoUrl);
      }

      setTimeout(() => {
        if (mountedRef.current) {
          setSuccessMessage("");
        }
      }, 5000);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(
        getApiErrorMessage(err, "PROFILE NOT UPDATED: Could not save your profile changes.")
      );
    } finally {
      if (mountedRef.current) {
        setSaving(false);
      }
    }
  }

  function formatDate(value) {
    if (!value) return "—";

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) return "—";

    return date.toLocaleDateString("en-IN", {
      day: "2-digit",
      month: "long",
      year: "numeric",
    });
  }

  function getInitials(name) {
    if (!name) return "TR";

    return name
      .split(" ")
      .map((part) => part[0])
      .filter(Boolean)
      .slice(0, 2)
      .join("")
      .toUpperCase();
  }

  if (loading) {
    return <LoadingState label="Loading your profile..." />;
  }

  if (error && !profile) {
    return (
      <ErrorState
        title="We couldn't load your profile"
        description={error}
        action={
          <button
            type="button"
            className="trainer-primary-btn"
            onClick={loadProfile}
          >
            TRY AGAIN
          </button>
        }
      />
    );
  }

  const tierCode = (profile?.tier || "SILVER").toUpperCase().replace(" TRAINER", "").replace(" MASTER", "");

  return (
    <main className="trainer-profile-page">
      <section className="trainer-profile-shell">

        {/* Top Header */}
        <header className="trainer-profile-header">
          <div>
            <span className="trainer-profile-kicker">
              ETHOS TRAINER PORTAL
            </span>

            <h1>
              Your
              <br />
              <em>profile.</em>
            </h1>
          </div>

          <div className="trainer-profile-header-actions">
            {editMode ? (
              <div className="trainer-edit-action-group">
                <button
                  type="button"
                  className="trainer-cancel-btn"
                  onClick={handleCancelEdit}
                  disabled={saving}
                >
                  CANCEL
                </button>

                <button
                  type="button"
                  className="trainer-save-btn"
                  onClick={handleSave}
                  disabled={saving}
                >
                  {saving ? "SAVING..." : "SAVE CHANGES"}
                </button>
              </div>
            ) : canUpdateProfile ? (
              <button
                type="button"
                className="trainer-edit-toggle-btn"
                onClick={() => setEditMode(true)}
              >
                EDIT PROFILE
              </button>
            ) : null}
          </div>
        </header>

        {/* Success Banner */}
        {successMessage && (
          <div className="trainer-profile-alert success" role="status" aria-live="polite">
            {successMessage}
          </div>
        )}

        {/* Error Banner */}
        {error && (
          <div className="trainer-profile-alert error" role="alert">
            {error}
          </div>
        )}

        {/* Profile Hero Section */}
        <section className="trainer-profile-hero-card">
          <div className="trainer-profile-avatar-area">
            {photoPreview ? (
              <img
                src={photoPreview}
                alt="Profile preview"
                className="trainer-avatar-img"
              />
            ) : profile?.profilePhotoUrl ? (
              <img
                src={getMediaUrl(profile.profilePhotoUrl)}
                alt={profile.fullName}
                className="trainer-avatar-img"
              />
            ) : (
              <div className="trainer-avatar-fallback">
                <span>{getInitials(profile?.fullName)}</span>
              </div>
            )}

            {editMode && canUpdateProfile && (
              <div className="trainer-photo-edit">
                <input
                  ref={photoInputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={handlePhotoSelect}
                  hidden
                />

                <button
                  type="button"
                  className="trainer-photo-upload-btn"
                  onClick={() => photoInputRef.current?.click()}
                  disabled={uploadingPhoto}
                >
                  <Camera size={14} />
                  {photoFile ? "CHANGE PHOTO" : "UPLOAD PHOTO"}
                </button>

                {photoFile && (
                  <button
                    type="button"
                    className="trainer-photo-save-btn"
                    onClick={handlePhotoUpload}
                    disabled={uploadingPhoto}
                  >
                    {uploadingPhoto ? "UPLOADING..." : "SAVE PHOTO"}
                  </button>
                )}
              </div>
            )}
          </div>

          <div className="trainer-profile-hero-details">
            <div className="trainer-hero-badge-row">
              <span className="trainer-code-pill">{profile?.trainerCode || "TRN—ETHOS"}</span>
              <span className="trainer-status-pill">{profile?.status?.toUpperCase() || "ACTIVE"}</span>
            </div>

            <h2>{profile?.fullName || "Ethos Trainer"}</h2>

            <p className="trainer-city-sub">{profile?.city || "India"}</p>

            <div className="trainer-tier-summary-box">
              <div className="trainer-tier-mini-art">
                <img
                  src={tierArtwork}
                  alt={profile?.tier}
                  className={`tier-art-${tierPositions[tierCode] || "silver"}`}
                />
              </div>

              <div>
                <span className="trainer-mini-kicker">ETHOS PATHWAY</span>

                <strong>{profile?.tier || "Silver Trainer"}</strong>
              </div>
            </div>
          </div>
        </section>

        {/* Section Cards */}
        <div className="trainer-profile-grid-stack">

          {/* Professional Identity (Read Only Core Values) */}
          <section className="trainer-profile-card">
            <div className="trainer-card-title">
              <span>01</span>
              <h3>PROFESSIONAL IDENTITY</h3>
            </div>

            <div className="trainer-fields-grid">
              <div className="trainer-profile-field readonly">
                <label>TRAINER CODE (READ ONLY)</label>
                <strong>{profile?.trainerCode || "N/A"}</strong>
              </div>

              <div className="trainer-profile-field">
                <label>FULL NAME *</label>

                {editMode ? (
                  <>
                    <input
                      type="text"
                      value={form.fullName}
                      onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                      placeholder="Full Name"
                    />

                    {fieldErrors.fullName && (
                      <span className="field-err">{fieldErrors.fullName}</span>
                    )}
                  </>
                ) : (
                  <strong>{profile?.fullName || "—"}</strong>
                )}
              </div>

              <div className="trainer-profile-field">
                <label>CITY *</label>

                {editMode ? (
                  <>
                    <input
                      type="text"
                      value={form.city}
                      onChange={(e) => setForm({ ...form, city: e.target.value })}
                      placeholder="City"
                    />

                    {fieldErrors.city && (
                      <span className="field-err">{fieldErrors.city}</span>
                    )}
                  </>
                ) : (
                  <strong>{profile?.city || "—"}</strong>
                )}
              </div>

              <div className="trainer-profile-field">
                <label>PROFILE PHOTO</label>

                {editMode ? (
                  <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                    <div style={{ display: "flex", alignItems: "center", gap: "10px", flexWrap: "wrap" }}>
                      <button
                        type="button"
                        className="trainer-photo-upload-btn"
                        onClick={() => photoInputRef.current?.click()}
                        disabled={uploadingPhoto}
                        style={{ padding: "6px 12px", fontSize: "12px", display: "inline-flex", alignItems: "center", gap: "6px" }}
                      >
                        {uploadingPhoto ? "UPLOADING..." : "UPLOAD FROM DEVICE"}
                      </button>

                      {photoFile && (
                        <button
                          type="button"
                          className="trainer-photo-upload-btn"
                          onClick={handlePhotoUpload}
                          disabled={uploadingPhoto}
                          style={{
                            background: "#16a34a",
                            borderColor: "#16a34a",
                            color: "#ffffff",
                            padding: "6px 12px",
                            fontSize: "12px",
                          }}
                        >
                          SAVE PHOTO
                        </button>
                      )}

                      {(photoPreview || form.profilePhotoUrl) && (
                        <span style={{ fontSize: "12px", color: "#16a34a", fontWeight: 600 }}>
                          ✓ Photo Selected
                        </span>
                      )}
                    </div>

                    {!showUrlInput ? (
                      <button
                        type="button"
                        style={{
                          background: "none",
                          border: "none",
                          color: "#94a3b8",
                          fontSize: "11px",
                          cursor: "pointer",
                          textDecoration: "underline",
                          textAlign: "left",
                          padding: 0,
                          width: "fit-content",
                        }}
                        onClick={() => setShowUrlInput(true)}
                      >
                        or enter image URL manually
                      </button>
                    ) : (
                      <input
                        type="url"
                        value={form.profilePhotoUrl}
                        onChange={(e) =>
                          setForm({ ...form, profilePhotoUrl: e.target.value })
                        }
                        placeholder="https://example.com/photo.jpg"
                        style={{ fontSize: "12px", marginTop: "2px" }}
                      />
                    )}
                  </div>
                ) : (
                  <strong style={{ wordBreak: "break-all" }}>
                    {profile?.profilePhotoUrl ? (
                      <span style={{ color: "#16a34a" }}>✓ Uploaded</span>
                    ) : (
                      "Not set"
                    )}
                  </strong>
                )}
              </div>

              <div className="trainer-profile-field readonly">
                <label>ACCOUNT STATUS</label>

                <strong className="status-highlight">{profile?.status || "Active"}</strong>
              </div>

              <div className="trainer-profile-field readonly">
                <label>CURRENT TIER</label>

                <strong>{profile?.tier || "Silver"}</strong>
              </div>

              <div className="trainer-profile-field readonly">
                <label>APPROVED DATE</label>

                <strong>{formatDate(profile?.approvedAt)}</strong>
              </div>
            </div>
          </section>

          {/* Dance Profile */}
          <section className="trainer-profile-card">
            <div className="trainer-card-title">
              <span>02</span>

              <h3>DANCE PROFILE</h3>
            </div>

            <div className="trainer-fields-grid">
              <div className="trainer-profile-field">
                <label>PRIMARY DANCE STYLE *</label>

                {editMode ? (
                  <>
                    <input
                      type="text"
                      value={form.primaryDanceStyle}
                      onChange={(e) => setForm({ ...form, primaryDanceStyle: e.target.value })}
                      placeholder="Primary Dance Style"
                    />

                    {fieldErrors.primaryDanceStyle && (
                      <span className="field-err">{fieldErrors.primaryDanceStyle}</span>
                    )}
                  </>
                ) : (
                  <strong>{profile?.primaryDanceStyle || "—"}</strong>
                )}
              </div>

              <div className="trainer-profile-field">
                <label>SECONDARY DANCE STYLES</label>

                {editMode ? (
                  <input
                    type="text"
                    value={form.secondaryDanceStyles}
                    onChange={(e) => setForm({ ...form, secondaryDanceStyles: e.target.value })}
                    placeholder="Jazz, Urban, Hip Hop"
                  />
                ) : (
                  <strong>{profile?.secondaryDanceStyles || "None listed"}</strong>
                )}
              </div>

              <div className="trainer-profile-field">
                <label>TEACHING EXPERIENCE (YEARS) *</label>

                {editMode ? (
                  <>
                    <div className="trainer-experience-input">
                      <input
                        type="number"
                        min="0"
                        value={form.experienceYears}
                        onChange={(e) => setForm({ ...form, experienceYears: e.target.value })}
                        placeholder="0"
                      />
                      <span className="trainer-experience-unit">YEARS</span>
                    </div>

                    {fieldErrors.experienceYears && (
                      <span className="field-err">{fieldErrors.experienceYears}</span>
                    )}
                  </>
                ) : (
                  <strong>{profile?.experienceYears != null ? `${profile.experienceYears} Years` : "—"}</strong>
                )}
              </div>
            </div>
          </section>

          {/* Professional Background */}
          <section className="trainer-profile-card">
            <div className="trainer-card-title">
              <span>03</span>

              <h3>PROFESSIONAL BACKGROUND</h3>
            </div>

            <div className="trainer-fields-grid single-col">
              <div className="trainer-profile-field">
                <label>CURRENT STUDIO</label>

                {editMode ? (
                  <input
                    type="text"
                    value={form.currentStudio}
                    onChange={(e) => setForm({ ...form, currentStudio: e.target.value })}
                    placeholder="Studio or Independent"
                  />
                ) : (
                  <strong>{profile?.currentStudio || "Independent"}</strong>
                )}
              </div>

              <div className="trainer-profile-field">
                <div className="label-row">
                  <label>BIO & TEACHING PHILOSOPHY *</label>

                  {editMode && (
                    <span className="char-counter">{form.bio.length} characters</span>
                  )}
                </div>

                {editMode ? (
                  <>
                    <textarea
                      rows={6}
                      value={form.bio}
                      onChange={(e) => setForm({ ...form, bio: e.target.value })}
                      placeholder="Tell us about your movement journey..."
                    />

                    {fieldErrors.bio && (
                      <span className="field-err">{fieldErrors.bio}</span>
                    )}
                  </>
                ) : (
                  <p className="trainer-bio-paragraph">{profile?.bio || "No bio provided."}</p>
                )}
              </div>
            </div>
          </section>

          {/* Social Presence */}
          <section className="trainer-profile-card">
            <div className="trainer-card-title">
              <span>04</span>

              <h3>SOCIAL PRESENCE</h3>
            </div>

            <div className="trainer-fields-grid">
              <div className="trainer-profile-field">
                <label>INSTAGRAM URL</label>

                {editMode ? (
                  <>
                    <input
                      type="url"
                      value={form.instagramUrl}
                      onChange={(e) => setForm({ ...form, instagramUrl: e.target.value })}
                      placeholder="https://instagram.com/..."
                    />

                    {fieldErrors.instagramUrl && (
                      <span className="field-err">{fieldErrors.instagramUrl}</span>
                    )}
                  </>
                ) : profile?.instagramUrl ? (
                  <a
                    href={profile.instagramUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="trainer-social-link"
                  >
                    Instagram Profile ↗
                  </a>
                ) : (
                  <span>No Instagram link</span>
                )}
              </div>

              <div className="trainer-profile-field">
                <label>YOUTUBE / PORTFOLIO URL</label>

                {editMode ? (
                  <>
                    <input
                      type="url"
                      value={form.youtubeUrl}
                      onChange={(e) => setForm({ ...form, youtubeUrl: e.target.value })}
                      placeholder="https://youtube.com/watch?v=..."
                    />

                    {fieldErrors.youtubeUrl && (
                      <span className="field-err">{fieldErrors.youtubeUrl}</span>
                    )}
                  </>
                ) : profile?.youTubeUrl || profile?.youtubeUrl ? (
                  <a
                    href={profile.youTubeUrl || profile.youtubeUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="trainer-social-link"
                  >
                    Video Reel ↗
                  </a>
                ) : (
                  <span>No Video link</span>
                )}
              </div>
            </div>
          </section>

          {/* Creative Portfolio Gallery */}
          <TrainerGallery />

        </div>

      </section>
    </main>
  );
}
