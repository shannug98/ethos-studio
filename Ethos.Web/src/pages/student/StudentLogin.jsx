import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import { authApi } from "../../services/authApi";
import "./StudentLogin.css";

const OTP_LENGTH = 6;
const RESEND_SECONDS = 30;

export default function StudentLogin() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { loginWithOtp } = useAuth();

  const [step, setStep] = useState("phone");
  const [phone, setPhone] = useState(() => searchParams.get("phone") || "");
  const [otp, setOtp] = useState("");
  const [devOtp, setDevOtp] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [packageRequired, setPackageRequired] = useState(false);
  const [resendTimer, setResendTimer] = useState(0);

  useEffect(() => {
    if (resendTimer <= 0) return;

    const timer = setInterval(() => {
      setResendTimer((current) => {
        if (current <= 1) {
          clearInterval(timer);
          return 0;
        }
        return current - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [resendTimer]);

  function normalizePhone(value) {
    return value.replace(/\D/g, "").slice(0, 10);
  }

  function handlePhoneChange(e) {
    setPhone(normalizePhone(e.target.value));
    setError("");
    setPackageRequired(false);
  }

  async function handleSendOtp(e) {
    e.preventDefault();
    setError("");
    setMessage("");
    setPackageRequired(false);

    if (phone.length !== 10) {
      setError("Please enter a valid 10-digit mobile number.");
      return;
    }

    setLoading(true);

    try {
      const result = await authApi.requestOtp(phone, "STUDENT_LOGIN");

      setStep("otp");
      setOtp("");
      setResendTimer(RESEND_SECONDS);
      setMessage("OTP sent successfully. Please check your mobile.");

      if (result?.developmentOtp) {
        setDevOtp(result.developmentOtp);
      } else {
        setDevOtp("");
      }
    } catch (err) {
      const status = err?.status || err?.response?.status;
      const msg = err?.data?.message || err?.response?.data?.message || err?.message || "";

      if (status === 404 || msg.toLowerCase().includes("not found")) {
        setError("No Ethos membership found for this mobile number.");
      } else if (status === 403 || msg.toLowerCase().includes("package")) {
        setPackageRequired(true);
        setError("Please purchase a monthly class package to access the Student Portal.");
      } else {
        setError(msg || "Unable to request OTP. Please try again.");
      }
    } finally {
      setLoading(false);
    }
  }

  async function handleVerifyOtp(e) {
    e.preventDefault();
    setError("");
    setMessage("");

    if (otp.length !== OTP_LENGTH) {
      setError("Please enter the 6-digit verification code.");
      return;
    }

    setLoading(true);

    try {
      await loginWithOtp(phone, otp, "STUDENT_LOGIN");

      const returnUrl = searchParams.get("returnUrl");
      const isSafeReturnUrl =
        returnUrl &&
        returnUrl.startsWith("/student/") &&
        !returnUrl.startsWith("//") &&
        !returnUrl.includes("\\") &&
        !returnUrl.includes(":") &&
        !returnUrl.includes("\0");

      if (isSafeReturnUrl) {
        navigate(returnUrl, { replace: true });
      } else {
        navigate("/student/dashboard", { replace: true });
      }
    } catch (err) {
      setError(
        err?.data?.message ||
        err?.response?.data?.message ||
        err?.message ||
        "Invalid or expired verification code."
      );
    } finally {
      setLoading(false);
    }
  }

  function handleChangeNumber() {
    setStep("phone");
    setOtp("");
    setDevOtp("");
    setError("");
    setMessage("");
    setResendTimer(0);
  }

  async function handleResendOtp() {
    if (resendTimer > 0 || loading) return;

    setError("");
    setMessage("");
    setLoading(true);

    try {
      const result = await authApi.requestOtp(phone, "STUDENT_LOGIN");

      setOtp("");
      setResendTimer(RESEND_SECONDS);
      setMessage("A new OTP has been sent to your mobile.");

      if (result?.developmentOtp) {
        setDevOtp(result.developmentOtp);
      } else {
        setDevOtp("");
      }
    } catch (err) {
      setError(err?.data?.message || err?.message || "Unable to resend OTP.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="student-login-page">
      <div className="student-login-shell">

        {step === "phone" ? (
          <button
            type="button"
            className="student-login-back"
            onClick={() => navigate("/login")}
          >
            ← BACK TO PORTAL SELECTION
          </button>
        ) : (
          <button
            type="button"
            className="student-login-back"
            onClick={handleChangeNumber}
            disabled={loading}
          >
            ← CHANGE MOBILE NUMBER
          </button>
        )}

        <div className="student-login-content">

          <span className="student-login-eyebrow">
            ETHOS STUDENT PORTAL
          </span>

          <h1>
            Welcome<br />
            <em>back.</em>
          </h1>

          <p className="student-login-intro">
            {step === "phone"
              ? "Sign in using the mobile number associated with your Ethos membership."
              : `Enter the 6-digit verification code sent to +91 ${phone}.`}
          </p>

          <div className="student-login-notice">
            <span>MEMBERSHIP REQUIRED</span>

            <p>
              Student portal access is available to students with an active Ethos monthly
              class package.
            </p>

            <button
              type="button"
              onClick={() => navigate("/classes")}
            >
              EXPLORE MONTHLY PACKAGES →
            </button>
          </div>

          {step === "phone" ? (
            <form className="student-login-form" onSubmit={handleSendOtp}>
              <label htmlFor="student-phone">
                MOBILE NUMBER
              </label>

              <div className="student-phone-input">
                <span>+91</span>

                <input
                  id="student-phone"
                  type="tel"
                  inputMode="numeric"
                  maxLength={10}
                  placeholder="Enter 10-digit number"
                  value={phone}
                  onChange={handlePhoneChange}
                  disabled={loading}
                  autoFocus
                />
              </div>

              {error && (
                <div className="student-login-message student-login-message--error">
                  {error}
                </div>
              )}

              {packageRequired && (
                <div className="student-package-cta-box">
                  <span>NO ACTIVE PASS DETECTED</span>
                  <p>Choose an Ethos monthly package to activate your student membership.</p>
                  <button
                    type="button"
                    className="student-package-cta-button"
                    onClick={() => navigate("/classes")}
                  >
                    SELECT A MONTHLY PACKAGE →
                  </button>
                </div>
              )}

              <button
                type="submit"
                disabled={loading || phone.length !== 10}
              >
                {loading ? "CHECKING ELIGIBILITY..." : "SEND OTP"}
              </button>
            </form>
          ) : (
            <form className="student-login-form" onSubmit={handleVerifyOtp}>
              <label htmlFor="student-otp">
                VERIFICATION CODE
              </label>

              <div className="student-phone-input student-phone-input--otp">
                <input
                  id="student-otp"
                  type="text"
                  inputMode="numeric"
                  maxLength={OTP_LENGTH}
                  placeholder="000000"
                  value={otp}
                  onChange={(e) => {
                    setOtp(e.target.value.replace(/\D/g, "").slice(0, OTP_LENGTH));
                    setError("");
                  }}
                  disabled={loading}
                  autoFocus
                />
              </div>

              {message && (
                <div className="student-login-message student-login-message--success">
                  {message}
                </div>
              )}

              {import.meta.env.DEV && devOtp && (
                <div className="student-dev-otp-card">
                  <div className="student-dev-otp-label">DEV OTP</div>
                  <div className="student-dev-otp-code">{devOtp}</div>
                  <div className="student-dev-otp-note">
                    Development environment only
                  </div>
                </div>
              )}

              {error && (
                <div className="student-login-message student-login-message--error">
                  {error}
                </div>
              )}

              <button
                type="submit"
                disabled={loading || otp.length !== OTP_LENGTH}
              >
                {loading ? "VERIFYING..." : "ENTER STUDENT PORTAL →"}
              </button>

              <div className="student-otp-resend">
                {resendTimer > 0 ? (
                  <span>
                    Resend code in <strong>{resendTimer}s</strong>
                  </span>
                ) : (
                  <button
                    type="button"
                    onClick={handleResendOtp}
                    disabled={loading}
                  >
                    RESEND OTP
                  </button>
                )}
              </div>
            </form>
          )}

        </div>
      </div>
    </main>
  );
}
