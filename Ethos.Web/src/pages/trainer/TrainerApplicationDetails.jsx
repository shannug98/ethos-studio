import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import tierArtwork from "../../assets/trainer/ethos-tier-emblems.png";
import "../../styles/trainer/trainer-application.css";

const INITIAL_FORM = {
  fullName: "",
  city: "",
  primaryDanceStyle: "",
  secondaryDanceStyles: "",
  experienceYears: "",
  currentStudio: "",
  bio: "",
  instagramUrl: "",
  youtubeUrl: "",
  tierId: "",
};

const DANCE_STYLES = [
  "Contemporary",
  "Hip Hop",
  "Bollywood",
  "Jazz",
  "Ballet",
  "Indian Classical",
  "Kuchipudi",
  "Bharatanatyam",
  "Western",
  "Freestyle",
  "Other",
];

export default function TrainerApplicationDetails() {
  const navigate = useNavigate();

  const [form, setForm] = useState(INITIAL_FORM);
  const [tiers, setTiers] = useState([]);
  const [application, setApplication] = useState(null);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  useEffect(() => {
    loadApplication();
  }, []);

  async function loadApplication() {
    setLoading(true);
    setError("");

    try {
      const [applicationData, tierData] = await Promise.all([
        trainerApi.getApplication().catch(() => null),
        trainerApi.getTiers().catch(() => []),
      ]);

      setTiers(Array.isArray(tierData) ? tierData : []);

      if (applicationData) {
        setApplication(applicationData);

        const profile = applicationData.trainerProfile || applicationData.profile || {};

        setForm({
          fullName:
            profile.fullName ||
            applicationData.fullName ||
            "",
          city:
            profile.city ||
            applicationData.city ||
            "",
          primaryDanceStyle:
            profile.primaryDanceStyle ||
            "",
          secondaryDanceStyles:
            profile.secondaryDanceStyles ||
            "",
          experienceYears:
            profile.experienceYears ?? "",
          currentStudio:
            profile.currentStudio ||
            "",
          bio:
            profile.bio ||
            "",
          instagramUrl:
            profile.instagramUrl ||
            "",
          youtubeUrl:
            profile.youtubeUrl ||
            "",
          tierId:
            profile.currentTierId ||
            applicationData.tier?.id ||
            "",
        });
      } else if (Array.isArray(tierData) && tierData.length > 0) {
        setForm((current) => ({
          ...current,
          tierId: tierData[0].id,
        }));
      }
    } catch (err) {
      setError(
        err.message ||
          "Unable to load your trainer application."
      );
    } finally {
      setLoading(false);
    }
  }

  function handleChange(event) {
    const { name, value } = event.target;

    setForm((current) => ({
      ...current,
      [name]: value,
    }));

    setError("");
    setMessage("");
  }

  function validate() {
    if (!form.fullName.trim()) {
      return "Full name is required.";
    }

    if (!form.city.trim()) {
      return "City is required.";
    }

    if (!form.primaryDanceStyle.trim()) {
      return "Primary dance style is required.";
    }

    if (
      form.experienceYears === "" ||
      Number(form.experienceYears) < 0
    ) {
      return "Please enter valid teaching experience.";
    }

    return "";
  }

  async function handleSubmit(event) {
    event.preventDefault();

    setError("");
    setMessage("");

    const validationError = validate();

    if (validationError) {
      setError(validationError);
      return;
    }

    setSaving(true);

    const payload = {
      fullName: form.fullName.trim(),
      city: form.city.trim(),
      primaryDanceStyle: form.primaryDanceStyle.trim(),
      secondaryDanceStyles:
        form.secondaryDanceStyles.trim() || null,
      experienceYears: Number(form.experienceYears),
      currentStudio:
        form.currentStudio.trim() || null,
      bio: form.bio.trim(),
      instagramUrl:
        form.instagramUrl.trim() || null,
      youtubeUrl:
        form.youtubeUrl.trim() || null,
    };

    if (form.tierId) {
      payload.tierId = form.tierId;
    }

    try {
      if (!application) {
        const created =
          await trainerApi.createApplication(payload);

        setApplication(created);
      } else {
        const updated =
          await trainerApi.updateApplication(payload);

        if (updated) {
          setApplication(updated);
        }
      }

      setMessage("Your trainer profile has been saved.");

      navigate("/trainer/application/introduction");
    } catch (err) {
      if (
        err.message?.includes("cannot be edited") ||
        err.message?.includes("current status")
      ) {
        setError(
          "This mobile number is already registered with Ethos. An Ethos account or trainer application is already associated with this number. Please sign in to continue or contact Ethos for assistance."
        );
      } else {
        setError(
          err.message ||
            "Unable to save your trainer application."
        );
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <main className="trainer-details-page">
        <div className="trainer-application-loading">
          <span className="trainer-eyebrow">
            ETHOS DANCE STUDIO
          </span>

          <h2>Preparing your application...</h2>

          <p>
            Your trainer journey is about to begin.
          </p>
        </div>
      </main>
    );
  }

  return (
    <main className="trainer-details-page">
      <div className="trainer-details-container">
        <button
          type="button"
          className="trainer-onboarding-back"
          onClick={() => navigate("/trainer/application")}
        >
          <span aria-hidden="true">←</span>
          BACK TO ETHOS
        </button>

        <div className="trainer-details-form">
          <div className="trainer-details-progress">
            <div className="trainer-progress-item trainer-progress-item--active">
              <span>01</span>
              <strong>PROFILE</strong>
            </div>

            <div className="trainer-progress-line" />

            <div className="trainer-progress-item">
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

            <div className="trainer-application-heading">
              <span className="trainer-eyebrow">
                STEP 01 — PERSONAL DETAILS
              </span>

              <h2>Tell us about you.</h2>

              <p>
                Build the foundation of your Ethos
                trainer profile.
              </p>
            </div>

            {error && (
              <div className="trainer-application-message trainer-application-message--error">
                {error}
              </div>
            )}

            {message && (
              <div className="trainer-application-message trainer-application-message--success">
                {message}
              </div>
            )}

            <form onSubmit={handleSubmit}>

              <div className="trainer-form-section">
                <div className="trainer-form-section-heading">
                  <span>01</span>
                  <div>
                    <h3>Personal information</h3>
                    <p>
                      The basics behind your trainer identity.
                    </p>
                  </div>
                </div>

                <div className="trainer-form-grid">
                  <div className="trainer-form-field trainer-form-field--full">
                    <label htmlFor="fullName">
                      FULL NAME *
                    </label>

                    <input
                      id="fullName"
                      name="fullName"
                      type="text"
                      value={form.fullName}
                      onChange={handleChange}
                      placeholder="Your full name"
                      disabled={saving}
                    />
                  </div>

                  <div className="trainer-form-field">
                    <label htmlFor="city">
                      CITY *
                    </label>

                    <input
                      id="city"
                      name="city"
                      type="text"
                      value={form.city}
                      onChange={handleChange}
                      placeholder="Where are you based?"
                      disabled={saving}
                    />
                  </div>

                  <div className="trainer-form-field">
                    <label htmlFor="experienceYears">
                      TEACHING EXPERIENCE *
                    </label>

                    <div className="trainer-experience-input">
                      <input
                        id="experienceYears"
                        name="experienceYears"
                        type="number"
                        min="0"
                        max="60"
                        step="1"
                        value={form.experienceYears}
                        onChange={handleChange}
                        placeholder="0"
                        disabled={saving}
                      />

                      <span className="trainer-experience-suffix">YEARS</span>
                    </div>
                  </div>

                  <div className="trainer-form-field trainer-form-field--full">
                    <label htmlFor="currentStudio">
                      CURRENT STUDIO
                    </label>

                    <input
                      id="currentStudio"
                      name="currentStudio"
                      type="text"
                      value={form.currentStudio}
                      onChange={handleChange}
                      placeholder="Current studio or organisation"
                      disabled={saving}
                    />
                  </div>
                </div>
              </div>

              <div className="trainer-form-section">
                <div className="trainer-form-section-heading">
                  <span>02</span>
                  <div>
                    <h3>Your movement</h3>
                    <p>
                      Tell us about your dance background.
                    </p>
                  </div>
                </div>

                <div className="trainer-form-grid">
                  <div className="trainer-form-field">
                    <label htmlFor="primaryDanceStyle">
                      PRIMARY DANCE STYLE *
                    </label>

                    <select
                      id="primaryDanceStyle"
                      name="primaryDanceStyle"
                      value={form.primaryDanceStyle}
                      onChange={handleChange}
                      disabled={saving}
                    >
                      <option value="">
                        Select your primary style
                      </option>

                      {DANCE_STYLES.map((style) => (
                        <option
                          key={style}
                          value={style}
                        >
                          {style}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="trainer-form-field">
                    <label htmlFor="secondaryDanceStyles">
                      SECONDARY STYLES
                    </label>

                    <input
                      id="secondaryDanceStyles"
                      name="secondaryDanceStyles"
                      type="text"
                      value={form.secondaryDanceStyles}
                      onChange={handleChange}
                      placeholder="e.g. Jazz, Ballet"
                      disabled={saving}
                    />
                  </div>
                </div>
              </div>

              <div className="trainer-application-actions" style={{ display: "flex", gap: "12px" }}>
                <button
                  type="button"
                  className="trainer-application-button trainer-application-button--back"
                  onClick={() => navigate("/trainer/application")}
                  style={{
                    minHeight: "56px",
                    padding: "0 24px",
                    border: "1px solid rgba(255, 255, 255, 0.16)",
                    background: "transparent",
                    color: "rgba(244, 238, 232, 0.65)",
                    cursor: "pointer",
                    fontSize: "9px",
                    fontWeight: "700",
                    letterSpacing: "0.16em",
                    textTransform: "uppercase"
                  }}
                >
                  ← BACK
                </button>

                <button
                  type="submit"
                  className="trainer-application-submit"
                  disabled={saving}
                  style={{ flex: 1 }}
                >
                  {saving
                    ? "SAVING PROFILE..."
                    : "SAVE & CONTINUE →"}
                </button>
              </div>
            </form>

            <div className="trainer-application-footer">
              <span>ETHOS DANCE STUDIO</span>
              <span>TRAINER REGISTRATION</span>
            </div>
          </div>
        </div>
    </main>
  );
}