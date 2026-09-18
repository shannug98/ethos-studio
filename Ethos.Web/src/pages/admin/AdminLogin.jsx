import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  adminApi,
  setAdminToken,
  setAdminUser,
  setAdminDeviceCredential,
  clearAdminDeviceCredential,
} from "../../services/adminApi";
import { formatAdminLastActive } from "../../utils/adminFormatters";
import ethosLogo from "../../assets/brand/ethos-emblem.png";
import "./AdminLogin.css";

export default function AdminLogin() {
  const navigate = useNavigate();

  // Steps: 'CREDENTIALS' | 'FORGOT_REQUEST' | 'FORGOT_RESET'
  const [step, setStep] = useState("CREDENTIALS");

  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [resetToken, setResetToken] = useState("");
  const [showPassword, setShowPassword] = useState(false);

  // Forgot password specific state
  const [forgotNewPassword, setForgotNewPassword] = useState("");
  const [forgotConfirmPassword, setForgotConfirmPassword] = useState("");
  const [showForgotNewPassword, setShowForgotNewPassword] = useState(false);

  const [loading, setLoading] = useState(false);
  // Structured error: { title: string, message: string } | null
  const [error, setError] = useState(null);

  // Session limit remote logout state
  const [rawBlockedSessions, setRawBlockedSessions] = useState([]);
  const [terminatingSessionId, setTerminatingSessionId] = useState(null);
  const [actionSuccess, setActionSuccess] = useState("");

  // Strictly filter only genuinely active, unrevoked, unlogged-out, unexpired sessions
  const activeBlockedSessions = (rawBlockedSessions || []).filter((s) => {
    if (!s.isActive) return false;
    if (s.isRevoked || s.revokedAt) return false;
    if (s.loggedOutAt) return false;
    if (s.expiresAt && new Date(s.expiresAt) <= new Date()) return false;
    return true;
  });

  const handleForgotRequestToken = async (e) => {
    if (e) e.preventDefault();
    setError(null);
    setActionSuccess("");

    const cleanPhone = phone.replace(/\D/g, "");
    if (!cleanPhone || cleanPhone.length < 10) {
      setError({
        title: "Invalid Mobile Number",
        message: "Please enter a valid 10-digit mobile number.",
      });
      return;
    }

    setLoading(true);
    try {
      const res = await adminApi.requestForgotPassword(cleanPhone);
      setStep("FORGOT_RESET");
      setActionSuccess(res.message || "If the number belongs to an authorized administrator, password reset instructions have been dispatched.");
    } catch (err) {
      setError({
        title: "Request Failed",
        message: err.message || "Failed to process password reset request.",
      });
    } finally {
      setLoading(false);
    }
  };

  const handleForgotResetSubmit = async (e) => {
    if (e) e.preventDefault();
    setError(null);

    const cleanToken = resetToken.trim();

    if (!cleanToken) {
      setError({
        title: "Reset Token Required",
        message: "Please enter the password reset token.",
      });
      return;
    }

    if (!forgotNewPassword || forgotNewPassword.length < 12) {
      setError({
        title: "Password Too Short",
        message: "New password must be at least 12 characters long.",
      });
      return;
    }

    if (forgotNewPassword !== forgotConfirmPassword) {
      setError({
        title: "Passwords Do Not Match",
        message: "The new password and confirmation password do not match.",
      });
      return;
    }

    setLoading(true);
    try {
      const res = await adminApi.resetForgotPassword(cleanToken, forgotNewPassword);
      setActionSuccess(res.message || "Administrative password reset successfully. Please log in with your new password.");
      setStep("CREDENTIALS");
      setPassword("");
      setResetToken("");
      setForgotNewPassword("");
      setForgotConfirmPassword("");
    } catch (err) {
      setError({
        title: "Password Reset Failed",
        message: err.message || "Failed to reset administrative password.",
      });
    } finally {
      setLoading(false);
    }
  };

  const handleCredentialsSubmit = async (e) => {
    if (e) e.preventDefault();
    setError(null);
    setActionSuccess("");

    const cleanPhone = phone.replace(/\D/g, "");
    if (!cleanPhone || cleanPhone.length < 10) {
      setError({
        title: "Invalid Mobile Number",
        message: "Please enter a valid 10-digit mobile number.",
      });
      return;
    }

    if (!password) {
      setError({
        title: "Password Required",
        message: "Please enter your administrative password.",
      });
      return;
    }

    setLoading(true);
    try {
      const deviceName = `${navigator.userAgent.includes("Mac") ? "Mac" : navigator.userAgent.includes("Win") ? "Windows" : "Device"} - ${navigator.userAgent.includes("Chrome") ? "Chrome" : navigator.userAgent.includes("Safari") ? "Safari" : "Browser"}`;
      const res = await adminApi.login(cleanPhone, password, deviceName);

      if (res.accessToken) {
        setAdminToken(res.accessToken);
        setAdminUser(res.user);
        if (res.deviceCredential) {
          setAdminDeviceCredential(res.deviceCredential);
        }
        navigate("/admin_portal/dashboard", { replace: true });
      } else {
        setError({
          title: "Authentication Failed",
          message: res.message || "Invalid administrative credentials.",
        });
      }
    } catch (err) {
      if (err.status === 403) {
        if (err.code === "DEVICE_REVOKED" || err.message?.toLowerCase().includes("revoked")) {
          clearAdminDeviceCredential();
          setError({
            title: "This device authorization has been revoked.",
            message: "Please use an approved device or ask an administrator to authorize this device again.",
          });
          setRawBlockedSessions([]);
        } else if (
          err.code === "DEVICE_AUTHORIZATION_BLOCKED" ||
          err.message?.toLowerCase().includes("approved") ||
          err.message?.toLowerCase().includes("active")
        ) {
          setError({
            title: "Both approved device sessions are currently active.",
            message: "Please sign out from one approved device to continue.",
          });
          if (Array.isArray(err.data?.activeSessions)) {
            setRawBlockedSessions(err.data.activeSessions);
          }
        } else {
          setError({
            title: "Access Denied",
            message: err.message || "This device is not authorized.",
          });
          setRawBlockedSessions([]);
        }
      } else {
        setRawBlockedSessions([]);
        setError({
          title: "Authentication Failed",
          message: err.message || "Invalid administrative credentials.",
        });
      }
      setStep("CREDENTIALS");
    } finally {
      setLoading(false);
    }
  };

  const handleRemoteLogout = async (sessionId) => {
    setTerminatingSessionId(sessionId);
    setError(null);
    setActionSuccess("");
    try {
      const cleanPhone = phone.replace(/\D/g, "");
      await adminApi.terminateSession(cleanPhone, password, sessionId);
      setActionSuccess("Device logged out successfully! Proceeding with login...");
      setRawBlockedSessions([]);

      const deviceName = `${navigator.userAgent.includes("Mac") ? "Mac" : navigator.userAgent.includes("Win") ? "Windows" : "Device"} - ${navigator.userAgent.includes("Chrome") ? "Chrome" : navigator.userAgent.includes("Safari") ? "Safari" : "Browser"}`;
      const res = await adminApi.login(cleanPhone, password, deviceName);
      if (res.accessToken) {
        setAdminToken(res.accessToken);
        setAdminUser(res.user);
        if (res.deviceCredential) {
          setAdminDeviceCredential(res.deviceCredential);
        }
        navigate("/admin_portal/dashboard", { replace: true });
      }
    } catch (err) {
      setError({
        title: "Remote Logout Failed",
        message: err.message || "Failed to log out device.",
      });
    } finally {
      setTerminatingSessionId(null);
    }
  };

  const handleReset = () => {
    setStep("CREDENTIALS");
    setResetToken("");
    setError(null);
    setRawBlockedSessions([]);
    setActionSuccess("");
  };

  return (
    <div className="admin-login-page">
      <div className="admin-login-brand">
        <img
          src={ethosLogo}
          alt="Ethos Dance Studio emblem"
        />
        <h1>Ethos Dance Studio</h1>
        <p>Administration Command Center</p>
      </div>

      <section className="admin-login-card">
        <h2>
          {step === "CREDENTIALS" && "Welcome back"}
          {step === "FORGOT_REQUEST" && "Forgot Administrative Password"}
          {step === "FORGOT_RESET" && "Set New Administrative Password"}
        </h2>

        <p className="description">
          {step === "CREDENTIALS" && "Sign in to manage studio operations, users, payments, trainers, students, and system telemetry."}
          {step === "FORGOT_REQUEST" && "Enter your registered administrative mobile number. Password reset instructions will be dispatched if authorized."}
          {step === "FORGOT_RESET" && "Enter your reset token and set your new administrative password."}
        </p>

        {error && (
          <div className="admin-login-alert" role="alert">
            <span className="alert-icon" aria-hidden="true">⚠️</span>
            <div>
              <strong>{error.title}</strong>
              <p>{error.message}</p>
            </div>
          </div>
        )}

        {actionSuccess && (
          <div className="admin-login-success-banner" role="status">
            <span aria-hidden="true">✅</span>
            <span>{actionSuccess}</span>
          </div>
        )}

        {/* Hotstar-style Session Limit Section */}
        {step === "CREDENTIALS" && activeBlockedSessions.length > 0 && (
          <div className="login-session-limit-section">
            <div className="login-session-limit-header">
              <span className="login-session-limit-title">
                Active device sessions: {activeBlockedSessions.length} of 2
              </span>
            </div>

            <div className="login-session-limit-cards">
              {activeBlockedSessions.map((sess) => {
                const title = `${sess.operatingSystem || "Windows"} · ${sess.browser || "Browser"}`;
                const lastActiveStr = formatAdminLastActive(sess.lastActivityAt || sess.lastSeenAt);

                return (
                  <div key={sess.id} className="login-session-card">
                    <div className="login-session-card-title">{title}</div>
                    <div className="login-session-card-active">Last active: {lastActiveStr}</div>
                    <button
                      type="button"
                      className="login-remote-logout-btn"
                      disabled={terminatingSessionId === sess.id}
                      onClick={() => handleRemoteLogout(sess.id)}
                    >
                      {terminatingSessionId === sess.id ? "Logging out..." : "Log out this device"}
                    </button>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {step === "CREDENTIALS" && (
          <form onSubmit={handleCredentialsSubmit}>
            <div className="admin-login-field">
              <label htmlFor="admin-phone">Mobile Number</label>
              <div className="admin-phone-group">
                <span className="admin-phone-prefix">+91</span>
                <input
                  id="admin-phone"
                  name="phone"
                  type="tel"
                  className="admin-login-input"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  autoComplete="tel"
                  placeholder="XXXXXXXXXX"
                  maxLength={10}
                  disabled={loading}
                  required
                />
              </div>
            </div>

            <div className="admin-login-field">
              <div className="admin-password-heading">
                <label htmlFor="admin-password">Password</label>
                <button
                  type="button"
                  className="admin-password-toggle"
                  onClick={() => setShowPassword((prev) => !prev)}
                >
                  {showPassword ? "Hide password" : "Show password"}
                </button>
              </div>

              <input
                id="admin-password"
                name="password"
                type={showPassword ? "text" : "password"}
                className="admin-login-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                placeholder="Enter administrative password"
                disabled={loading}
                required
              />
              <div className="admin-forgot-password-row">
                <button
                  type="button"
                  className="admin-forgot-password-link"
                  onClick={() => {
                    setError(null);
                    setActionSuccess("");
                    setStep("FORGOT_REQUEST");
                  }}
                >
                  Forgot administrative password?
                </button>
              </div>
            </div>

            <button
              type="submit"
              className="admin-login-submit"
              disabled={loading}
            >
              {loading ? "Authenticating..." : "Sign In to Admin Portal"}
            </button>
          </form>
        )}

        {step === "FORGOT_REQUEST" && (
          <form onSubmit={handleForgotRequestToken}>
            <div className="admin-login-field">
              <label htmlFor="forgot-phone">Registered Admin Mobile Number</label>
              <div className="admin-phone-group">
                <span className="admin-phone-prefix">+91</span>
                <input
                  id="forgot-phone"
                  name="phone"
                  type="tel"
                  className="admin-login-input"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  autoComplete="tel"
                  placeholder="XXXXXXXXXX"
                  maxLength={10}
                  disabled={loading}
                  required
                  autoFocus
                />
              </div>
            </div>

            <button
              type="submit"
              className="admin-login-submit"
              disabled={loading}
            >
              {loading ? "Requesting..." : "Send Password Reset Instructions"}
            </button>

            <button
              type="button"
              className="admin-back-btn"
              onClick={handleReset}
              disabled={loading}
            >
              ← Back to Sign In
            </button>
          </form>
        )}

        {step === "FORGOT_RESET" && (
          <form onSubmit={handleForgotResetSubmit}>
            <div className="admin-login-field">
              <label htmlFor="reset-token">Password Reset Token</label>
              <input
                id="reset-token"
                name="resetToken"
                type="text"
                className="admin-login-input"
                placeholder="Enter reset token"
                value={resetToken}
                onChange={(e) => setResetToken(e.target.value)}
                autoFocus
                disabled={loading}
                required
              />
            </div>

            <div className="admin-login-field">
              <div className="admin-password-heading">
                <label htmlFor="new-password">New Password (min 12 chars)</label>
                <button
                  type="button"
                  className="admin-password-toggle"
                  onClick={() => setShowForgotNewPassword((prev) => !prev)}
                >
                  {showForgotNewPassword ? "Hide" : "Show"}
                </button>
              </div>
              <input
                id="new-password"
                type={showForgotNewPassword ? "text" : "password"}
                className="admin-login-password"
                value={forgotNewPassword}
                onChange={(e) => setForgotNewPassword(e.target.value)}
                placeholder="Must include uppercase, lowercase, digit, symbol"
                disabled={loading}
                required
              />
            </div>

            <div className="admin-login-field">
              <label htmlFor="confirm-password">Confirm New Password</label>
              <input
                id="confirm-password"
                type={showForgotNewPassword ? "text" : "password"}
                className="admin-login-password"
                value={forgotConfirmPassword}
                onChange={(e) => setForgotConfirmPassword(e.target.value)}
                placeholder="Re-enter new password"
                disabled={loading}
                required
              />
            </div>

            <button
              type="submit"
              className="admin-login-submit"
              disabled={loading}
            >
              {loading ? "Updating Password..." : "Reset Password & Return to Login"}
            </button>

            <button
              type="button"
              className="admin-back-btn"
              onClick={handleReset}
              disabled={loading}
            >
              ← Back to Sign In
            </button>
          </form>
        )}

        <p className="admin-login-security-note">
          Authorized studio partners only. All connection attempts and device telemetry are cryptographically logged.
        </p>
      </section>

      <div className="admin-login-protection">
        <span aria-hidden="true">🔒</span>
        <span> Protected administrative access</span>
      </div>
    </div>
  );
}

