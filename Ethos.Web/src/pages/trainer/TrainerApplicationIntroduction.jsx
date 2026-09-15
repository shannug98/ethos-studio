import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import "../../styles/trainer/trainer-application.css";
import "./TrainerApplicationIntroduction.css";
import trainerApplicationHero from "../../assets/trainer/trainer-application-hero.png";

const MAX_BIO_LENGTH = 1500;
const MAX_NOTES_LENGTH = 1000;

function isValidUrl(value) {
  if (!value || !value.trim()) return true;

  try {
    const url = new URL(value.trim());

    return (
      url.protocol === "http:" ||
      url.protocol === "https:"
    );
  } catch {
    return false;
  }
}

function isYouTubeUrl(value) {
  if (!value || !value.trim()) return true;

  try {
    const url = new URL(value.trim());
    const host = url.hostname.toLowerCase();

    return (
      host === "youtube.com" ||
      host === "www.youtube.com" ||
      host === "youtu.be" ||
      host === "m.youtube.com"
    );
  } catch {
    return false;
  }
}

export default function TrainerApplicationIntroduction() {
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const [application, setApplication] = useState(null);

  const [form, setForm] = useState({
    bio: "",
    youtubeUrl: "",
    instagramUrl: "",
    applicationNotes: "",
  });

  const [videoMode, setVideoMode] = useState("youtube");
  const [existingVideo, setExistingVideo] = useState(null);
  const [videoFile, setVideoFile] = useState(null);
  const [videoPreviewUrl, setVideoPreviewUrl] = useState("");
  const [videoDuration, setVideoDuration] = useState(null);

  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState({});

  useEffect(() => {
    let mounted = true;

    async function loadData() {
      try {
        const [appResult, videoResult] = await Promise.allSettled([
          trainerApi.getApplication(),
          trainerApi.getApplicationVideo(),
        ]);

        if (!mounted) return;

        if (appResult.status === "fulfilled") {
          const result = appResult.value;
          setApplication(result);

          setForm({
            bio: result?.bio ?? "",
            youtubeUrl: result?.youtubeUrl ?? "",
            instagramUrl: result?.instagramUrl ?? "",
            applicationNotes: result?.applicationNotes ?? "",
          });
        } else {
          throw appResult.reason;
        }

        if (
          videoResult.status === "fulfilled" &&
          videoResult.value &&
          videoResult.value.id
        ) {
          setExistingVideo(videoResult.value);
          setVideoMode("upload");
          setVideoDuration(videoResult.value.durationSeconds);
        } else if (appResult.value?.youtubeUrl) {
          setVideoMode("youtube");
        }
      } catch (err) {
        if (!mounted) return;

        setError(
          getApiErrorMessage(err) ||
            "Unable to load your trainer application."
        );
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    }

    loadData();

    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    return () => {
      if (videoPreviewUrl) {
        URL.revokeObjectURL(videoPreviewUrl);
      }
    };
  }, [videoPreviewUrl]);

  const bioCount = form.bio.length;
  const notesCount = form.applicationNotes.length;

  const progress = useMemo(() => {
    let completed = 0;

    if (form.bio.trim()) completed += 1;
    if (form.youtubeUrl.trim() || videoFile || existingVideo) completed += 1;

    return Math.round((completed / 2) * 100);
  }, [form.bio, form.youtubeUrl, videoFile, existingVideo]);

  function updateField(field, value) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));

    setFieldErrors((current) => ({
      ...current,
      [field]: "",
    }));

    setError("");
  }

  function handleVideoModeChange(mode) {
    setVideoMode(mode);
    setError("");

    if (mode === "youtube") {
      setVideoFile(null);
      setVideoDuration(null);

      if (videoPreviewUrl) {
        URL.revokeObjectURL(videoPreviewUrl);
        setVideoPreviewUrl("");
      }
    } else {
      updateField("youtubeUrl", "");
    }
  }

  function handleVideoFileChange(event) {
    const file = event.target.files?.[0];

    if (!file) return;

    setError("");

    if (!file.type.startsWith("video/")) {
      setFieldErrors((current) => ({
        ...current,
        video: "Please choose a valid video file (MP4, MOV, WEBM).",
      }));
      return;
    }

    const previewUrl = URL.createObjectURL(file);
    const video = document.createElement("video");

    video.preload = "metadata";

    video.onloadedmetadata = () => {
      const duration = video.duration;

      URL.revokeObjectURL(previewUrl);

      if (duration > 60) {
        setVideoFile(null);
        setVideoDuration(null);

        setFieldErrors((current) => ({
          ...current,
          video: "Your video must be 1 minute or less.",
        }));

        return;
      }

      setVideoDuration(duration);
      setVideoFile(file);

      const nextPreviewUrl = URL.createObjectURL(file);
      setVideoPreviewUrl(nextPreviewUrl);

      setFieldErrors((current) => ({
        ...current,
        video: "",
      }));
    };

    video.onerror = () => {
      URL.revokeObjectURL(previewUrl);

      setFieldErrors((current) => ({
        ...current,
        video: "We couldn't read this video file. Please select another file.",
      }));
    };

    video.src = previewUrl;
  }

  async function removeVideoFile() {
    setError("");

    if (existingVideo) {
      try {
        setSaving(true);
        await trainerApi.deleteApplicationVideo();
        setExistingVideo(null);
      } catch (err) {
        setError(getApiErrorMessage(err) || "Failed to remove video.");
        setSaving(false);
        return;
      } finally {
        setSaving(false);
      }
    }

    setVideoFile(null);
    setVideoDuration(null);

    if (videoPreviewUrl) {
      URL.revokeObjectURL(videoPreviewUrl);
      setVideoPreviewUrl("");
    }

    setFieldErrors((current) => ({
      ...current,
      video: "",
    }));
  }

  function validate() {
    const errors = {};

    if (!form.bio.trim()) {
      errors.bio =
        "Please tell us about your dance journey and teaching philosophy.";
    } else if (form.bio.trim().length < 50) {
      errors.bio =
        "Please provide a little more detail. Your introduction should be at least 50 characters.";
    }

    if (form.bio.length > MAX_BIO_LENGTH) {
      errors.bio = `Introduction must be ${MAX_BIO_LENGTH} characters or less.`;
    }

    if (videoMode === "youtube") {
      if (!form.youtubeUrl.trim()) {
        errors.youtubeUrl =
          "Please provide a YouTube video URL for your introduction.";
      } else if (!isValidUrl(form.youtubeUrl)) {
        errors.youtubeUrl = "Please enter a valid video URL.";
      } else if (!isYouTubeUrl(form.youtubeUrl)) {
        errors.youtubeUrl =
          "Please enter a valid YouTube URL for your introduction video.";
      }
    }

    if (videoMode === "upload" && !videoFile && !existingVideo) {
      errors.video = "Please upload a video introduction clip.";
    }

    if (
      videoMode === "upload" &&
      videoFile &&
      videoDuration !== null &&
      videoDuration > 60
    ) {
      errors.video = "Your video must be 1 minute or less.";
    }

    if (form.instagramUrl.trim() && !isValidUrl(form.instagramUrl)) {
      errors.instagramUrl = "Please enter a valid Instagram URL.";
    }

    if (form.applicationNotes.length > MAX_NOTES_LENGTH) {
      errors.applicationNotes = `Notes must be ${MAX_NOTES_LENGTH} characters or less.`;
    }

    setFieldErrors(errors);

    return Object.keys(errors).length === 0;
  }

  async function handleSaveAndContinue(e) {
    if (e) e.preventDefault();
    setError("");

    if (!validate()) {
      window.scrollTo({
        top: 0,
        behavior: "smooth",
      });
      return;
    }

    setSaving(true);

    try {
      if (videoMode === "upload" && videoFile) {
        const formData = new FormData();
        formData.append("file", videoFile);

        const uploadedVideo = await trainerApi.uploadApplicationVideo(formData);
        setExistingVideo(uploadedVideo);
        setVideoFile(null);
      }

      const payload = {
        bio: form.bio.trim(),
        youtubeUrl: videoMode === "youtube" ? form.youtubeUrl.trim() || null : null,
        instagramUrl: form.instagramUrl.trim() || null,
        applicationNotes: form.applicationNotes.trim() || null,
      };

      await trainerApi.updateApplication(payload);

      navigate("/trainer/application/tier");
    } catch (err) {
      setError(
        getApiErrorMessage(err) || "Unable to save your introduction."
      );
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <main className="trainer-introduction-page">
        <div className="trainer-introduction-loading">
          <div className="trainer-introduction-loading-inner">
            <span className="trainer-eyebrow">ETHOS TRAINER APPLICATION</span>
            <h2>Loading introduction...</h2>
          </div>
        </div>
      </main>
    );
  }

  if (!application) {
    return (
      <main className="trainer-introduction-page">
        <div className="trainer-introduction-shell">
          <div className="trainer-introduction-form-area" style={{ width: "100%" }}>
            <div className="trainer-introduction-error" role="alert">
              <span className="trainer-introduction-error-mark">!</span>
              <div>
                <strong>We couldn't load your application.</strong>
                <p>{error}</p>
                <button
                  type="button"
                  className="trainer-introduction-button trainer-introduction-button--back"
                  onClick={() => navigate("/trainer/application/details")}
                  style={{ marginTop: "16px" }}
                >
                  ← BACK TO APPLICATION
                </button>
              </div>
            </div>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="trainer-introduction-page">
      <div className="trainer-details-container" style={{ width: "min(1380px, calc(100% - 64px))", margin: "0 auto", padding: "48px 0 30px" }}>

        {/* BACK LINK */}
        <button
          type="button"
          className="trainer-application-back"
          onClick={() => navigate("/trainer/application/details")}
          style={{ marginBottom: "24px", cursor: "pointer", background: "none", border: "none" }}
        >
          ← BACK TO PROFILE
        </button>

        {/* 5-STEP PROGRESS BAR */}
        <div className="trainer-details-progress">
          <div className="trainer-progress-item trainer-progress-item--done">
            <span>01</span>
            <strong>PROFILE</strong>
          </div>

          <div className="trainer-progress-line trainer-progress-line--done" />

          <div className="trainer-progress-item trainer-progress-item--active">
            <span>02</span>
            <strong>INTRODUCTION</strong>
          </div>

          <div className="trainer-progress-line" />

          <div className="trainer-progress-item">
            <span>03</span>
            <strong>TIER</strong>
          </div>

          <div className="trainer-progress-line" />

          <div className="trainer-progress-item">
            <span>04</span>
            <strong>REVIEW</strong>
          </div>
        </div>

        {/* SPLIT SCREEN SHELL */}
        <div className="trainer-introduction-shell">

          {/* LEFT VISUAL HERO */}
          <section
            className="trainer-introduction-visual"
            style={{
              backgroundImage: `url(${trainerApplicationHero})`,
              backgroundSize: "cover",
              backgroundPosition: "center",
            }}
          >
            <div className="trainer-introduction-overlay" />

            <div className="trainer-introduction-visual-content">
              <span className="trainer-eyebrow">
                ETHOS TRAINER APPLICATION
              </span>

              <h1>
                Your
                <br />
                voice.
                <br />
                <em>Your vision.</em>
              </h1>

              <p>
                Tell us about the artist, teacher and person
                behind your movement.
              </p>
            </div>
          </section>

          {/* RIGHT FORM AREA */}
          <section className="trainer-introduction-form-area">
            <div className="trainer-introduction-form">

              <div className="trainer-introduction-heading">
                <span className="trainer-eyebrow">
                  STEP 02 — INTRODUCTION
                </span>

                <h2>
                  Tell us
                  <br />
                  <em>your story.</em>
                </h2>

                <p>
                  This is where we get to know the artist,
                  teacher and person behind the movement.
                </p>
              </div>

            {error && (
              <div className="trainer-introduction-error" role="alert">
                <span className="trainer-introduction-error-mark" aria-hidden="true">
                  !
                </span>
                <span>{error}</span>
              </div>
            )}

            <form onSubmit={handleSaveAndContinue}>

              {/* 01 STORY */}
              <section className="trainer-introduction-section">
                <div className="trainer-introduction-section-header">
                  <span className="trainer-introduction-section-number">01</span>
                  <div>
                    <h3>Your story</h3>
                    <p>Tell us what shaped your journey as a dancer and teacher.</p>
                  </div>
                </div>

                <div className="trainer-introduction-field">
                  <label htmlFor="trainer-bio">
                    TEACHING BIO & PHILOSOPHY <span>*</span>
                  </label>

                  <textarea
                    id="trainer-bio"
                    rows={7}
                    maxLength={MAX_BIO_LENGTH}
                    value={form.bio}
                    onChange={(e) => updateField("bio", e.target.value)}
                    placeholder="Tell us about your dance journey, teaching philosophy, experience and what you hope to bring to the Ethos community..."
                  />

                  <div className="trainer-introduction-field-meta">
                    <span>Share what makes your teaching approach yours.</span>
                    <span>{bioCount} / {MAX_BIO_LENGTH}</span>
                  </div>

                  {fieldErrors.bio && (
                    <div className="trainer-introduction-field-error">
                      {fieldErrors.bio}
                    </div>
                  )}
                </div>
              </section>

              {/* 02 VIDEO */}
              <section className="trainer-introduction-section">
                <div className="trainer-introduction-section-header">
                  <span className="trainer-introduction-section-number">
                    02
                  </span>

                  <div>
                    <h3>Video introduction</h3>
                    <p>
                      Show us how you teach, perform or move.
                      Please provide one short video.
                    </p>
                  </div>
                </div>

                <div className="trainer-introduction-video-requirement">
                  <span className="trainer-introduction-video-requirement-mark">
                    *
                  </span>

                  <div>
                    <strong>VIDEO REQUIRED</strong>
                    <p>
                      Choose one option below. Your video should be
                      no longer than 1 minute.
                    </p>
                  </div>
                </div>

                <div className="trainer-video-options">

                  <button
                    type="button"
                    className={`trainer-video-option ${
                      videoMode === "youtube" ? "is-selected" : ""
                    }`}
                    onClick={() => handleVideoModeChange("youtube")}
                  >
                    <span className="trainer-video-option-radio" />

                    <span className="trainer-video-option-content">
                      <strong>YouTube link</strong>
                      <small>
                        Paste a link to your video
                      </small>
                    </span>
                  </button>

                  <button
                    type="button"
                    className={`trainer-video-option ${
                      videoMode === "upload" ? "is-selected" : ""
                    }`}
                    onClick={() => handleVideoModeChange("upload")}
                  >
                    <span className="trainer-video-option-radio" />

                    <span className="trainer-video-option-content">
                      <strong>Upload video</strong>
                      <small>
                        Choose a video from your device
                      </small>
                    </span>
                  </button>

                </div>

                {videoMode === "youtube" && (
                  <div className="trainer-introduction-field">
                    <label htmlFor="trainer-youtube">
                      YOUTUBE VIDEO URL <span>*</span>
                    </label>

                    <input
                      id="trainer-youtube"
                      type="url"
                      value={form.youtubeUrl}
                      onChange={(e) =>
                        updateField("youtubeUrl", e.target.value)
                      }
                      placeholder="https://www.youtube.com/watch?v=..."
                    />

                    {fieldErrors.youtubeUrl && (
                      <div className="trainer-introduction-field-error">
                        {fieldErrors.youtubeUrl}
                      </div>
                    )}
                  </div>
                )}

                {videoMode === "upload" && (
                  <div className="trainer-video-upload">

                    {!videoFile && !existingVideo ? (
                      <>
                        <label
                          htmlFor="trainer-video-file"
                          className="trainer-video-dropzone"
                        >
                          <span className="trainer-video-upload-icon">
                            +
                          </span>

                          <span className="trainer-video-upload-title">
                            Choose your video
                          </span>

                          <span className="trainer-video-upload-subtitle">
                            MP4, MOV, WEBM or M4V format
                            <br />
                            Maximum duration: 1 minute
                          </span>
                        </label>

                        <input
                          id="trainer-video-file"
                          type="file"
                          accept="video/*"
                          onChange={handleVideoFileChange}
                          hidden
                        />
                      </>
                    ) : (
                      <div className="trainer-video-selected">

                        {videoPreviewUrl ? (
                          <video
                            className="trainer-video-preview"
                            src={videoPreviewUrl}
                            controls
                            playsInline
                          />
                        ) : (
                          <div className="trainer-video-preview-placeholder">
                            <span>🎥 Video Uploaded</span>
                          </div>
                        )}

                        <div className="trainer-video-selected-info">
                          <div>
                            <strong>
                              {videoFile
                                ? videoFile.name
                                : existingVideo?.fileName ?? "Uploaded Video"}
                            </strong>

                            <span>
                              {videoDuration !== null && videoDuration !== undefined
                                ? `${Math.round(videoDuration)} sec`
                                : "Video ready"}
                            </span>
                          </div>

                          <button
                            type="button"
                            className="trainer-video-remove"
                            onClick={removeVideoFile}
                            disabled={saving}
                          >
                            REMOVE
                          </button>
                        </div>

                      </div>
                    )}

                    {fieldErrors.video && (
                      <div className="trainer-introduction-field-error">
                        {fieldErrors.video}
                      </div>
                    )}

                  </div>
                )}

                <div className="trainer-video-guidance">
                  <span>VIDEO GUIDELINES</span>

                  <p>
                    Keep your introduction natural and authentic.
                    You can share a teaching sample, performance,
                    choreography or short dance portfolio.
                  </p>
                </div>
              </section>

              {/* 03 SOCIAL */}
              <section className="trainer-introduction-section">
                <div className="trainer-introduction-section-header">
                  <span className="trainer-introduction-section-number">03</span>
                  <div>
                    <h3>Social presence</h3>
                    <p>Optional links that help the Ethos team understand your artistic work.</p>
                  </div>
                </div>

                <div className="trainer-introduction-field">
                  <label htmlFor="trainer-instagram">
                    INSTAGRAM <span className="optional">OPTIONAL</span>
                  </label>

                  <input
                    id="trainer-instagram"
                    type="url"
                    value={form.instagramUrl}
                    onChange={(e) => updateField("instagramUrl", e.target.value)}
                    placeholder="https://instagram.com/..."
                  />

                  {fieldErrors.instagramUrl && (
                    <div className="trainer-introduction-field-error">
                      {fieldErrors.instagramUrl}
                    </div>
                  )}
                </div>
              </section>

              {/* 04 APPLICATION NOTES */}
              <section className="trainer-introduction-section">
                <div className="trainer-introduction-section-header">
                  <span className="trainer-introduction-section-number">04</span>
                  <div>
                    <h3>One last thing</h3>
                    <p>Anything else you'd like the Ethos review team to know?</p>
                  </div>
                </div>

                <div className="trainer-introduction-field">
                  <label htmlFor="trainer-notes">
                    APPLICATION NOTES <span className="optional">OPTIONAL</span>
                  </label>

                  <textarea
                    id="trainer-notes"
                    rows={5}
                    maxLength={MAX_NOTES_LENGTH}
                    value={form.applicationNotes}
                    onChange={(e) => updateField("applicationNotes", e.target.value)}
                    placeholder="Anything else you'd like to share with the Ethos team..."
                  />

                  <div className="trainer-introduction-field-meta">
                    <span>Optional</span>
                    <span>{notesCount} / {MAX_NOTES_LENGTH}</span>
                  </div>

                  {fieldErrors.applicationNotes && (
                    <div className="trainer-introduction-field-error">
                      {fieldErrors.applicationNotes}
                    </div>
                  )}
                </div>
              </section>

              {/* ACTIONS */}
              <div className="trainer-introduction-actions">
                <button
                  type="button"
                  className="trainer-introduction-button trainer-introduction-button--back"
                  onClick={() => navigate("/trainer/application/details")}
                  disabled={saving}
                >
                  ← BACK
                </button>

                <button
                  type="submit"
                  className="trainer-introduction-button trainer-introduction-button--primary"
                  disabled={saving}
                >
                  {saving ? "SAVING..." : "CONTINUE TO TIER SELECTION →"}
                </button>
              </div>

            </form>
          </div>
        </section>

      </div>

    </div>
  </main>
);
}
