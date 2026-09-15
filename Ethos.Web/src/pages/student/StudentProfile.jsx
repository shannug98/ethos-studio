import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { studentApi } from "../../services/studentApi";
import { useAuth } from "../../context/AuthContext";
import { studentStateSync } from "../../services/studentStateSync";
import StudentAvatar from "../../components/student/StudentAvatar";
import "./StudentProfile.css";

export default function StudentProfile() {
  const navigate = useNavigate();
  const { logout } = useAuth();

  const [profile, setProfile] = useState(null);
  const [photoBlobUrl, setPhotoBlobUrl] = useState(null);
  const [activePackage, setActivePackage] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");

  const fileInputRef = useRef(null);

  // Form State (allow-listed editable fields)
  const [form, setForm] = useState({
    fullName: "",
    email: "",
    city: "",
    dateOfBirth: "",
    gender: "",
    bio: "",
    emergencyContactName: "",
    emergencyContactPhone: "",
  });

  const loadProfilePhoto = async (forceRefresh = false) => {
    try {
      const url = await studentStateSync.loadCanonicalPhoto(forceRefresh);
      setPhotoBlobUrl(url);
    } catch {
      setPhotoBlobUrl(null);
    }
  };

  const loadProfileData = async () => {
    try {
      setLoading(true);
      setError("");

      const [profileData, pkgData] = await Promise.all([
        studentApi.getProfile(),
        studentApi.getActivePackage().catch(() => null),
      ]);

      setProfile(profileData);
      setActivePackage(pkgData);

      if (profileData?.profilePhotoUrl) {
        loadProfilePhoto();
      }

      setForm({
        fullName: profileData.fullName || "",
        email: profileData.email || "",
        city: profileData.city || "",
        dateOfBirth: profileData.dateOfBirth || "",
        gender: profileData.gender || "",
        bio: profileData.bio || "",
        emergencyContactName: profileData.emergencyContactName || "",
        emergencyContactPhone: profileData.emergencyContactPhone || "",
      });
    } catch (err) {
      setError(
        err?.data?.message ||
        err?.message ||
        "Unable to load student account profile."
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProfileData();
    const unsub = studentStateSync.onPhotoUpdated((url) => {
      setPhotoBlobUrl(url);
    });
    return () => {
      unsub();
    };
  }, []);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleProfileSubmit = async (e) => {
    e.preventDefault();
    try {
      setSaving(true);
      setError("");
      setSuccessMessage("");

      const payload = {
        fullName: form.fullName.trim(),
        email: form.email.trim() || null,
        city: form.city.trim() || null,
        dateOfBirth: form.dateOfBirth.trim() || null,
        gender: form.gender.trim() || null,
        bio: form.bio.trim() || null,
        emergencyContactName: form.emergencyContactName.trim() || null,
        emergencyContactPhone: form.emergencyContactPhone.trim() || null,
      };

      const updated = await studentApi.updateProfile(payload);
      setProfile(updated);
      studentStateSync.emitProfileUpdated(updated);
      setSuccessMessage("Profile updated successfully!");
    } catch (err) {
      setError(
        err?.data?.message ||
        err?.message ||
        "Failed to update profile. Please verify all inputs."
      );
    } finally {
      setSaving(false);
    }
  };

  const handlePhotoSelect = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
    if (!allowedTypes.includes(file.type)) {
      setError("Please select a JPG, PNG or WebP image.");
      e.target.value = "";
      return;
    }

    if (file.size > 10 * 1024 * 1024) {
      setError("Image file cannot exceed 10 MB.");
      e.target.value = "";
      return;
    }

    try {
      setUploading(true);
      setError("");
      setSuccessMessage("");

      const formData = new FormData();
      formData.append("file", file);

      const updated = await studentApi.uploadProfilePhoto(formData);
      setProfile(updated);
      const newBlobUrl = await studentStateSync.loadCanonicalPhoto(true);
      setPhotoBlobUrl(newBlobUrl);
      studentStateSync.emitProfileUpdated(updated);
      setSuccessMessage("Profile photo uploaded successfully!");
    } catch (err) {
      setError(
        err?.data?.message ||
        err?.message ||
        "Failed to upload profile photo."
      );
    } finally {
      setUploading(false);
      e.target.value = "";
    }
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return "—";
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
        year: "numeric",
      });
    } catch {
      return dateStr;
    }
  };

  const handleSignOut = () => {
    logout();
    navigate("/student/login", { replace: true });
  };

  // Safe classes remaining calculation
  const allowed = activePackage?.classesAllowed ?? null;
  const used = activePackage?.classesUsed ?? 0;
  const remaining = allowed !== null ? Math.max(0, allowed - used) : "Unlimited";

  // Calculate dynamic completeness across all 9 studio profile fields
  const calculateCompleteness = () => {
    const fields = [
      { name: "Full Name", filled: Boolean(form.fullName?.trim()) },
      { name: "Email Address", filled: Boolean(form.email?.trim()) },
      { name: "Home Studio City", filled: Boolean(form.city?.trim()) },
      { name: "Date of Birth", filled: Boolean(form.dateOfBirth?.trim()) },
      { name: "Gender", filled: Boolean(form.gender?.trim()) },
      { name: "Dance Bio", filled: Boolean(form.bio?.trim()) },
      { name: "Emergency Contact Name", filled: Boolean(form.emergencyContactName?.trim()) },
      { name: "Emergency Contact Phone", filled: Boolean(form.emergencyContactPhone?.trim()) },
      { name: "Profile Photo", filled: Boolean(photoBlobUrl || profile?.profilePhotoUrl) },
    ];

    const completed = fields.filter((f) => f.filled).length;
    const total = fields.length;
    const percentage = completed === total ? 100 : Math.min(95, Math.round((completed / total) * 100));
    const missing = fields.filter((f) => !f.filled).map((f) => f.name);

    return { percentage, completed, total, missing };
  };

  const completeness = calculateCompleteness();

  return (
    <div className="student-profile-page">
      <div className="student-profile-container">

        {/* HEADER */}
        <header className="student-profile-header">
          <h1>
            Your Studio Identity &<br />
            <em>Account Profile.</em>
          </h1>
          <p>
            Manage your personal profile, keep emergency contacts up to date, and review
            your membership credentials. Critical identity fields remain studio-locked.
          </p>
        </header>

        {error && (
          <div className="student-dashboard-error" style={{ marginBottom: "24px" }}>
            <p>{error}</p>
          </div>
        )}

        {successMessage && (
          <div className="student-feedback-metric-card" style={{ borderLeft: "3px solid #73c880", marginBottom: "24px" }}>
            <p style={{ margin: 0, color: "#73c880", fontSize: "13px" }}>✓ {successMessage}</p>
          </div>
        )}

        {/* PROFILE COMPLETION STRIP */}
        {profile && (
          <div className="student-completion-strip">
            <div className="student-completion-header">
              <span>PROFILE COMPLETION</span>
              <strong>{completeness.percentage}% COMPLETED</strong>
            </div>
            <div className="student-completion-bar-bg">
              <div
                className="student-completion-bar-fill"
                style={{ width: `${completeness.percentage}%` }}
              />
            </div>
            <p className="student-completion-note">
              {completeness.percentage === 100 ? (
                <span style={{ color: "#73c880" }}>
                  ✓ All profile fields and studio emergency contacts are verified and 100% completed.
                </span>
              ) : (
                `Complete your ${completeness.missing.slice(0, 3).join(", ")} to finish your student profile.`
              )}
            </p>
          </div>
        )}

        {loading ? (
          <div className="student-dashboard-loading">
            <div className="student-spinner" />
            <span>Loading profile details...</span>
          </div>
        ) : (
          <div className="student-profile-grid">

            {/* LEFT SIDEBAR: AVATAR & IMMUTABLE IDENTITY */}
            <aside className="student-profile-sidebar">

              {/* AVATAR UPLOAD CARD */}
              <div className="student-profile-avatar-card">
                <div className="student-avatar-wrap" style={{ display: "grid", placeItems: "center" }}>
                  <StudentAvatar
                    photoUrl={photoBlobUrl || profile.profilePhotoUrl}
                    name={profile.fullName || user?.fullName}
                    size={110}
                    clickable
                  />
                </div>

                <input
                  type="file"
                  ref={fileInputRef}
                  style={{ display: "none" }}
                  accept="image/jpeg,image/png,image/webp"
                  onChange={handlePhotoSelect}
                />

                <button
                  type="button"
                  className="student-avatar-upload-btn"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={uploading}
                >
                  {uploading ? "UPLOADING..." : "UPLOAD PROFILE PHOTO"}
                </button>
              </div>

              {/* IMMUTABLE STUDIO IDENTITY CARD */}
              <div className="student-identity-card">
                <div className="student-identity-item">
                  <span>MEMBER CODE (STUDIO ID)</span>
                  <strong>{profile?.customerCode || "ETH-STUDENT"}</strong>
                </div>

                <div className="student-identity-item">
                  <span>REGISTERED MOBILE</span>
                  <strong>+91 {profile?.phone || "—"}</strong>
                  <div className="student-identity-pill">OTP LOGIN ENABLED</div>
                </div>

                <div className="student-identity-item">
                  <span>MEMBER SINCE</span>
                  <strong>{formatDate(profile?.memberSince || profile?.createdAt)}</strong>
                </div>

                <div className="student-identity-item">
                  <span>ACCOUNT STATUS</span>
                  <strong style={{ color: "#73c880" }}>ACTIVE STUDENT</strong>
                </div>
              </div>

              {/* SESSION / LOGOUT CARD */}
              <div className="student-identity-card">
                <button
                  type="button"
                  className="student-profile-back"
                  style={{ width: "100%", textAlign: "center" }}
                  onClick={handleSignOut}
                >
                  SIGN OUT OF PORTAL
                </button>
              </div>

            </aside>

            {/* RIGHT FORM: ALLOWED EDITABLE FIELDS */}
            <section className="student-profile-main">

              <form onSubmit={handleProfileSubmit}>

                {/* 1. PERSONAL INFORMATION */}
                <div className="student-profile-section">
                  <h3>Personal Details</h3>

                  <div className="student-profile-form-grid">
                    <div className="student-form-group student-form-group--full">
                      <label>FULL NAME *</label>
                      <input
                        type="text"
                        name="fullName"
                        className="student-input"
                        value={form.fullName}
                        onChange={handleChange}
                        required
                      />
                    </div>

                    <div className="student-form-group">
                      <label>EMAIL ADDRESS (OPTIONAL)</label>
                      <input
                        type="email"
                        name="email"
                        className="student-input"
                        placeholder="student@example.com"
                        value={form.email}
                        onChange={handleChange}
                      />
                    </div>

                    <div className="student-form-group">
                      <label>HOME STUDIO CITY</label>
                      <input
                        type="text"
                        name="city"
                        className="student-input"
                        placeholder="E.g. Hyderabad, Bengaluru"
                        value={form.city}
                        onChange={handleChange}
                      />
                    </div>

                    <div className="student-form-group">
                      <label>DATE OF BIRTH (YYYY-MM-DD)</label>
                      <input
                        type="date"
                        name="dateOfBirth"
                        className="student-input"
                        value={form.dateOfBirth}
                        onChange={handleChange}
                      />
                    </div>

                    <div className="student-form-group">
                      <label>GENDER</label>
                      <select
                        name="gender"
                        className="student-input"
                        value={form.gender}
                        onChange={handleChange}
                      >
                        <option value="">Select Gender</option>
                        <option value="Female">Female</option>
                        <option value="Male">Male</option>
                        <option value="Non-Binary">Non-Binary</option>
                        <option value="Prefer not to say">Prefer not to say</option>
                      </select>
                    </div>

                    <div className="student-form-group student-form-group--full">
                      <label>DANCE BACKGROUND & BIO</label>
                      <textarea
                        name="bio"
                        className="student-textarea"
                        placeholder="Tell us about your movement style, favorite genres, or dance goals..."
                        value={form.bio}
                        onChange={handleChange}
                      />
                    </div>
                  </div>
                </div>

                {/* 2. EMERGENCY CONTACT */}
                <div className="student-profile-section">
                  <h3>Emergency Contact Information</h3>
                  <p style={{ fontSize: "12px", color: "rgba(255,255,255,0.5)", margin: 0 }}>
                    In case of intense physical training or studio medical emergencies, please provide a trusted contact.
                  </p>

                  <div className="student-profile-form-grid">
                    <div className="student-form-group">
                      <label>CONTACT PERSON NAME</label>
                      <input
                        type="text"
                        name="emergencyContactName"
                        className="student-input"
                        placeholder="Parent / Guardian / Partner"
                        value={form.emergencyContactName}
                        onChange={handleChange}
                      />
                    </div>

                    <div className="student-form-group">
                      <label>CONTACT PHONE (10 DIGITS)</label>
                      <input
                        type="tel"
                        name="emergencyContactPhone"
                        className="student-input"
                        placeholder="9876543210"
                        value={form.emergencyContactPhone}
                        onChange={handleChange}
                      />
                    </div>
                  </div>
                </div>

                {/* 3. MEMBERSHIP INFORMATION (READ-ONLY DISPLAY) */}
                <div className="student-profile-section">
                  <h3>Active Membership Pass (Read-Only)</h3>

                  {activePackage ? (
                    <div className="student-membership-summary-card">
                      <div className="student-mem-info">
                        <h4>{activePackage.packageName}</h4>
                        <p>Valid through: {formatDate(activePackage.expiryDate)}</p>
                      </div>
                      <div className="student-mem-stats">
                        <div className="student-mem-stat">
                          <strong>{used}</strong>
                          <small>CLASSES USED</small>
                        </div>
                        <div className="student-mem-stat">
                          <strong>{remaining}</strong>
                          <small>REMAINING</small>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <p style={{ fontSize: "13px", color: "rgba(255,255,255,0.5)" }}>
                      No active monthly package found. Explore classes to activate a pass.
                    </p>
                  )}
                </div>

                {/* SUBMIT BUTTON */}
                <div className="student-profile-actions">
                  <button
                    type="submit"
                    className="student-save-profile-btn"
                    disabled={saving}
                  >
                    {saving ? "SAVING CHANGES..." : "SAVE PROFILE CHANGES"}
                  </button>
                </div>

              </form>

            </section>

          </div>
        )}

      </div>
    </div>
  );
}
