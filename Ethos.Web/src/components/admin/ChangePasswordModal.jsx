import React, { useState } from "react";
import AdminActionModal from "./common/AdminActionModal";
import { adminApi, getAdminUser } from "../../services/adminApi";

export default function ChangePasswordModal({ isOpen, onClose }) {
  const adminUser = getAdminUser();
  const adminPhone = adminUser?.phone || "8019013757";

  const [step, setStep] = useState(1); // 1: Request OTP, 2: Enter OTP & New Password, 3: Success
  const [otp, setOtp] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [devOtp, setDevOtp] = useState("");
  const [loading, setLoading] = useState(false);
  const [requestingOtp, setRequestingOtp] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const resetState = () => {
    setStep(1);
    setOtp("");
    setNewPassword("");
    setConfirmPassword("");
    setDevOtp("");
    setLoading(false);
    setRequestingOtp(false);
    setError("");
    setSuccess("");
    setShowNewPassword(false);
    setShowConfirmPassword(false);
  };

  const handleClose = () => {
    resetState();
    onClose();
  };

  const handleRequestOtp = async () => {
    setError("");
    setSuccess("");
    setRequestingOtp(true);
    try {
      const res = await adminApi.requestChangePasswordOtp();
      setSuccess(res.message || "Verification code dispatched to your registered mobile number.");
      if (res.developmentOtp) {
        setDevOtp(res.developmentOtp);
      }
      setStep(2);
    } catch (err) {
      setError(err.message || "Failed to send verification code. Please try again.");
    } finally {
      setRequestingOtp(false);
    }
  };

  const handleResendOtp = async () => {
    setError("");
    setRequestingOtp(true);
    try {
      const res = await adminApi.requestChangePasswordOtp();
      setSuccess(res.message || "Verification code resent.");
      if (res.developmentOtp) {
        setDevOtp(res.developmentOtp);
      }
    } catch (err) {
      setError(err.message || "Failed to resend code.");
    } finally {
      setRequestingOtp(false);
    }
  };

  // Password rules check
  const hasMinLength = newPassword.length >= 12;
  const hasUpper = /[A-Z]/.test(newPassword);
  const hasLower = /[a-z]/.test(newPassword);
  const hasDigit = /[0-9]/.test(newPassword);
  const hasSpecial = /[^A-Za-z0-9]/.test(newPassword);
  const passwordsMatch = newPassword.length > 0 && newPassword === confirmPassword;
  const isPasswordValid = hasMinLength && hasUpper && hasLower && hasDigit && hasSpecial;

  const handleSubmit = async (e) => {
    if (e) e.preventDefault();
    setError("");
    setSuccess("");

    const cleanOtp = otp.trim();
    if (!cleanOtp || cleanOtp.length !== 6) {
      setError("Please enter the 6-digit verification code sent to your phone.");
      return;
    }

    if (!isPasswordValid) {
      setError("Please ensure your new password satisfies all security criteria.");
      return;
    }

    if (!passwordsMatch) {
      setError("New password and confirmation password do not match.");
      return;
    }

    setLoading(true);
    try {
      const res = await adminApi.changePassword(newPassword, cleanOtp);
      setSuccess(res.message || "Administrative password updated successfully!");
      setStep(3);
    } catch (err) {
      setError(err.message || "Failed to update administrative password.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <AdminActionModal
      isOpen={isOpen}
      onClose={handleClose}
      title={
        step === 1
          ? "Change Administrative Password"
          : step === 2
          ? "Set New Administrative Password"
          : "Password Updated"
      }
      subtitle={
        step === 1
          ? "Step 1 of 3: Verify Administrator Identity"
          : step === 2
          ? "Step 2 of 3: Enter OTP & New Password"
          : "Step 3 of 3: Security Update Complete"
      }
      tone="neutral"
      maxWidth="540px"
      primaryAction={
        step === 1
          ? {
              label: requestingOtp ? "Sending Code..." : "Request Verification Code",
              onClick: handleRequestOtp,
              disabled: requestingOtp,
              loading: requestingOtp,
            }
          : step === 2
          ? {
              label: loading ? "Updating Password..." : "Update Password",
              onClick: handleSubmit,
              disabled: loading || !isPasswordValid || !passwordsMatch || otp.trim().length !== 6,
              loading: loading,
            }
          : {
              label: "Return to Dashboard",
              onClick: handleClose,
            }
      }
      secondaryAction={
        step < 3
          ? {
              label: step === 2 ? "Back" : "Cancel",
              onClick: step === 2 ? () => setStep(1) : handleClose,
              disabled: loading || requestingOtp,
            }
          : null
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        {error && (
          <div
            style={{
              padding: "10px 14px",
              backgroundColor: "rgba(239, 68, 68, 0.1)",
              border: "1px solid rgba(239, 68, 68, 0.3)",
              borderRadius: "6px",
              color: "#dc2626",
              fontSize: "0.875rem",
              fontWeight: 500,
            }}
          >
            ⚠️ {error}
          </div>
        )}

        {/* STEP 1: Verify Identity */}
        {step === 1 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
            <div
              style={{
                padding: "16px",
                backgroundColor: "#f8fafc",
                borderRadius: "8px",
                border: "1px solid #e2e8f0",
              }}
            >
              <p style={{ margin: "0 0 10px 0", fontSize: "0.9rem", color: "#334155", lineHeight: "1.5" }}>
                To safeguard the administrative console, password updates require multi-factor authentication.
                A single-use verification code will be sent to your registered phone number:
              </p>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "10px",
                  padding: "10px 14px",
                  backgroundColor: "#ffffff",
                  border: "1px solid #cbd5e1",
                  borderRadius: "6px",
                  fontSize: "1.05rem",
                  fontWeight: 700,
                  color: "#0f172a",
                  letterSpacing: "0.5px",
                }}
              >
                <span>📱</span>
                <span>+91 {adminPhone}</span>
              </div>
            </div>

            <p style={{ margin: 0, fontSize: "0.8rem", color: "#64748b" }}>
              Click <strong>Request Verification Code</strong> to receive your 6-digit OTP. Current password is not required.
            </p>
          </div>
        )}

        {/* STEP 2: Enter OTP & New Password */}
        {step === 2 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
            {/* OTP Alert / Info */}
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                padding: "10px 12px",
                backgroundColor: "#f0fdf4",
                border: "1px solid #bbf7d0",
                borderRadius: "6px",
              }}
            >
              <span style={{ fontSize: "0.825rem", color: "#166534" }}>
                Code sent to <strong>+91 {adminPhone}</strong>
              </span>
              <button
                type="button"
                onClick={handleResendOtp}
                disabled={requestingOtp}
                style={{
                  background: "none",
                  border: "none",
                  color: "#2563eb",
                  fontSize: "0.8rem",
                  fontWeight: 600,
                  cursor: "pointer",
                  textDecoration: "underline",
                }}
              >
                {requestingOtp ? "Sending..." : "Resend OTP"}
              </button>
            </div>

            {devOtp && (
              <div
                style={{
                  padding: "8px 12px",
                  backgroundColor: "rgba(59, 130, 246, 0.1)",
                  border: "1px dashed rgba(59, 130, 246, 0.4)",
                  borderRadius: "6px",
                  color: "#2563eb",
                  fontSize: "0.85rem",
                  fontWeight: 600,
                }}
              >
                🔑 Development Code: {devOtp}
              </div>
            )}

            {/* OTP Input */}
            <div>
              <label style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, color: "#374151", marginBottom: "6px" }}>
                Verification Code (6-digit OTP)
              </label>
              <input
                type="text"
                value={otp}
                onChange={(e) => setOtp(e.target.value.replace(/\D/g, "").substring(0, 6))}
                placeholder="000000"
                maxLength={6}
                disabled={loading}
                autoFocus
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  borderRadius: "6px",
                  border: "1px solid #d1d5db",
                  fontSize: "1.2rem",
                  letterSpacing: "6px",
                  textAlign: "center",
                  fontWeight: 700,
                  backgroundColor: "#ffffff",
                  boxSizing: "border-box",
                }}
              />
            </div>

            {/* New Password */}
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "6px" }}>
                <label style={{ fontSize: "0.85rem", fontWeight: 600, color: "#374151" }}>
                  New Administrative Password
                </label>
                <button
                  type="button"
                  style={{ background: "none", border: "none", color: "#4f46e5", fontSize: "0.8rem", cursor: "pointer" }}
                  onClick={() => setShowNewPassword(!showNewPassword)}
                >
                  {showNewPassword ? "Hide" : "Show"}
                </button>
              </div>
              <input
                type={showNewPassword ? "text" : "password"}
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="Enter strong new password"
                disabled={loading}
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  borderRadius: "6px",
                  border: "1px solid #d1d5db",
                  fontSize: "0.95rem",
                  boxSizing: "border-box",
                }}
              />
            </div>

            {/* Confirm Password */}
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "6px" }}>
                <label style={{ fontSize: "0.85rem", fontWeight: 600, color: "#374151" }}>
                  Confirm New Password
                </label>
                <button
                  type="button"
                  style={{ background: "none", border: "none", color: "#4f46e5", fontSize: "0.8rem", cursor: "pointer" }}
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                >
                  {showConfirmPassword ? "Hide" : "Show"}
                </button>
              </div>
              <input
                type={showConfirmPassword ? "text" : "password"}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Re-enter new password"
                disabled={loading}
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  borderRadius: "6px",
                  border: "1px solid #d1d5db",
                  fontSize: "0.95rem",
                  boxSizing: "border-box",
                }}
              />
            </div>

            {/* Live Security Checklist */}
            <div
              style={{
                padding: "12px 14px",
                backgroundColor: "#f9fafb",
                borderRadius: "6px",
                border: "1px solid #e5e7eb",
                fontSize: "0.8rem",
              }}
            >
              <div style={{ fontWeight: 600, color: "#374151", marginBottom: "8px" }}>
                Password Security Requirements:
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6px" }}>
                <div style={{ color: hasMinLength ? "#16a34a" : "#9ca3af", display: "flex", alignItems: "center", gap: "6px" }}>
                  <span>{hasMinLength ? "✓" : "○"}</span> At least 12 characters
                </div>
                <div style={{ color: hasUpper ? "#16a34a" : "#9ca3af", display: "flex", alignItems: "center", gap: "6px" }}>
                  <span>{hasUpper ? "✓" : "○"}</span> Upper case (A-Z)
                </div>
                <div style={{ color: hasLower ? "#16a34a" : "#9ca3af", display: "flex", alignItems: "center", gap: "6px" }}>
                  <span>{hasLower ? "✓" : "○"}</span> Lower case (a-z)
                </div>
                <div style={{ color: hasDigit ? "#16a34a" : "#9ca3af", display: "flex", alignItems: "center", gap: "6px" }}>
                  <span>{hasDigit ? "✓" : "○"}</span> Number (0-9)
                </div>
                <div style={{ color: hasSpecial ? "#16a34a" : "#9ca3af", display: "flex", alignItems: "center", gap: "6px" }}>
                  <span>{hasSpecial ? "✓" : "○"}</span> Special (!@#$%^&*)
                </div>
                <div style={{ color: passwordsMatch ? "#16a34a" : "#9ca3af", display: "flex", alignItems: "center", gap: "6px" }}>
                  <span>{passwordsMatch ? "✓" : "○"}</span> Passwords match
                </div>
              </div>
            </div>
          </div>
        )}

        {/* STEP 3: Completion */}
        {step === 3 && (
          <div
            style={{
              padding: "24px 16px",
              textAlign: "center",
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              gap: "12px",
            }}
          >
            <div
              style={{
                width: "56px",
                height: "56px",
                borderRadius: "50%",
                backgroundColor: "#dcfce7",
                color: "#16a34a",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: "28px",
              }}
            >
              ✓
            </div>
            <h4 style={{ margin: 0, fontSize: "1.2rem", fontWeight: 700, color: "#111827" }}>
              Password Updated Successfully
            </h4>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "#4b5563", maxWidth: "380px", lineHeight: "1.5" }}>
              Your administrative credentials have been securely updated. The previous password is now invalid and your new credentials will be required on your next login.
            </p>
          </div>
        )}
      </div>
    </AdminActionModal>
  );
}
