import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";

export default function TrainerChangePassword() {
  const navigate = useNavigate();
  const { user, changePassword, logout } = useAuth();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");

  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function handleSubmit(event) {
    event.preventDefault();

    setError("");

    if (newPassword.length < 8) {
      setError("New password must be at least 8 characters.");
      return;
    }

    if (newPassword !== confirmNewPassword) {
      setError("New password and confirmation do not match.");
      return;
    }

    if (currentPassword === newPassword) {
      setError("New password must be different from your current password.");
      return;
    }

    setLoading(true);

    try {
      await changePassword(
        currentPassword,
        newPassword,
        confirmNewPassword
      );

      /*
       * The backend intentionally requires the trainer
       * to authenticate again after changing the password.
       */
      logout();

      navigate("/trainer/login", {
        replace: true,
        state: {
          passwordChanged: true,
        },
      });
    } catch (err) {
      setError(
        err.message ||
          "Unable to change your password. Please try again."
      );
    } finally {
      setLoading(false);
    }
  }

  function getDisplayPhone() {
    return user?.phone || user?.mobile || "";
  }

  return (
    <div className="trainer-password-page">
      <div className="trainer-password-visual">
        <div className="trainer-password-visual-content">
          <span className="trainer-password-label">
            ETHOS / TRAINER STUDIO
          </span>

          <h1>
            Your studio.
            <br />
            Your access.
            <br />
            Your password.
          </h1>

          <p>
            Before entering the Trainer Studio, create a secure password
            that only you know.
          </p>
        </div>
      </div>

      <div className="trainer-password-panel">
        <div className="trainer-password-form">
          <div className="trainer-login-mark">E</div>

          <span className="trainer-eyebrow">
            FIRST-TIME ACCESS
          </span>

          <h2>
            Create your
            <br />
            new password.
          </h2>

          <p className="trainer-login-intro">
            Your temporary password has been accepted. For security,
            please create a new password before entering the Trainer
            Studio.
          </p>

          {getDisplayPhone() && (
            <div className="trainer-password-account">
              <span>REGISTERED MOBILE</span>
              <strong>{getDisplayPhone()}</strong>
            </div>
          )}

          <form onSubmit={handleSubmit}>
            <div className="trainer-password-field">
              <label htmlFor="current-password">
                Current password
              </label>

              <div className="trainer-password-input-wrap">
                <input
                  id="current-password"
                  type={showCurrentPassword ? "text" : "password"}
                  value={currentPassword}
                  onChange={(event) =>
                    setCurrentPassword(event.target.value)
                  }
                  placeholder="Enter temporary password"
                  autoComplete="current-password"
                  required
                />

                <button
                  type="button"
                  className="trainer-password-toggle"
                  onClick={() =>
                    setShowCurrentPassword((value) => !value)
                  }
                  aria-label={
                    showCurrentPassword
                      ? "Hide current password"
                      : "Show current password"
                  }
                >
                  {showCurrentPassword ? "HIDE" : "SHOW"}
                </button>
              </div>
            </div>

            <div className="trainer-password-field">
              <label htmlFor="new-password">
                New password
              </label>

              <div className="trainer-password-input-wrap">
                <input
                  id="new-password"
                  type={showNewPassword ? "text" : "password"}
                  value={newPassword}
                  onChange={(event) =>
                    setNewPassword(event.target.value)
                  }
                  placeholder="Create a new password"
                  autoComplete="new-password"
                  minLength={8}
                  required
                />

                <button
                  type="button"
                  className="trainer-password-toggle"
                  onClick={() =>
                    setShowNewPassword((value) => !value)
                  }
                  aria-label={
                    showNewPassword
                      ? "Hide new password"
                      : "Show new password"
                  }
                >
                  {showNewPassword ? "HIDE" : "SHOW"}
                </button>
              </div>
            </div>

            <div className="trainer-password-field">
              <label htmlFor="confirm-new-password">
                Confirm new password
              </label>

              <div className="trainer-password-input-wrap">
                <input
                  id="confirm-new-password"
                  type={showConfirmPassword ? "text" : "password"}
                  value={confirmNewPassword}
                  onChange={(event) =>
                    setConfirmNewPassword(event.target.value)
                  }
                  placeholder="Repeat your new password"
                  autoComplete="new-password"
                  minLength={8}
                  required
                />

                <button
                  type="button"
                  className="trainer-password-toggle"
                  onClick={() =>
                    setShowConfirmPassword((value) => !value)
                  }
                  aria-label={
                    showConfirmPassword
                      ? "Hide password confirmation"
                      : "Show password confirmation"
                  }
                >
                  {showConfirmPassword ? "HIDE" : "SHOW"}
                </button>
              </div>
            </div>

            <div className="trainer-password-requirements">
              <span>Password requirements</span>

              <p>At least 8 characters</p>
              <p>Use a password you do not use elsewhere</p>
            </div>

            {error && (
              <div className="trainer-form-error">
                {error}
              </div>
            )}

            <button
              type="submit"
              className="trainer-primary-button"
              disabled={loading}
            >
              {loading
                ? "UPDATING PASSWORD..."
                : "CREATE PASSWORD"}
            </button>
          </form>

          <button
            type="button"
            className="trainer-text-button trainer-password-back"
            onClick={() => {
              logout();
              navigate("/trainer/login", {
                replace: true,
              });
            }}
          >
            Return to trainer login
          </button>
        </div>
      </div>
    </div>
  );
}
