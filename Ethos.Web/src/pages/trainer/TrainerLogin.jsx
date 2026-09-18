import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";

import { useAuth } from "../../context/AuthContext";
import { authApi } from "../../services/authApi";
import { trainerApi } from "../../services/trainerApi";
import trainerLoginHero from "../../assets/trainer/trainer-login-hero.png";
import ethosLogo from "../../assets/brand/ethos-emblem.png";
import "./TrainerLogin.css";

export default function TrainerLogin() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { loginWithOtp } = useAuth();

  const [phoneNumber, setPhoneNumber] = useState("");
  const [otp, setOtp] = useState("");
  const [otpRequested, setOtpRequested] = useState(false);
  const [devOtpHint, setDevOtpHint] = useState("");

  const [mobileNotFound, setMobileNotFound] = useState(false);
  const [noTrainerApp, setNoTrainerApp] = useState(false);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function handleSendOtp(event) {
    event.preventDefault();

    setError("");
    setMobileNotFound(false);
    setLoading(true);

    try {
      const result = await authApi.requestOtp(phoneNumber.trim());

      if (result?.developmentOtp) {
        setDevOtpHint(result.developmentOtp);
      }

      setOtpRequested(true);
    } catch (err) {
      if (
        err?.response?.status === 404 ||
        err?.response?.data?.userExists === false ||
        (err?.response?.data?.message &&
          err.response.data.message.toLowerCase().includes("not found"))
      ) {
        setMobileNotFound(true);
        setError("Mobile number not found. Please register first to continue.");
      } else {
        setError(
          err?.response?.data?.message ||
            err?.message ||
            "Unable to request OTP. Please check your mobile number."
        );
      }
    } finally {
      setLoading(false);
    }
  }

  async function handleVerifyOtp(event) {
    event.preventDefault();

    setError("");
    setLoading(true);

    try {
      const result = await loginWithOtp(
        phoneNumber.trim(),
        otp.trim()
      );

      const roles = result?.user?.roles || [];
      const returnUrl = searchParams.get("returnUrl");

      // 1. Approved TRAINER role -> Trainer Dashboard
      if (roles.includes("TRAINER")) {
        if (returnUrl && returnUrl.startsWith("/trainer/")) {
          navigate(returnUrl, { replace: true });
        } else {
          navigate("/trainer/dashboard", { replace: true });
        }
        return;
      }

      // 2. Pre-Approval Applicant / Student login -> Check Application State
      try {
        const app = await trainerApi.getApplication();

        if (!app || !app.id) {
          setNoTrainerApp(true);
          setLoading(false);
          return;
        }

        const status = (app.status || "").toLowerCase();

        if (
          status === "underreview" ||
          status === "submitted" ||
          status === "rejected" ||
          status === "cancelled" ||
          status === "changesrequested"
        ) {
          navigate("/trainer/application/status", { replace: true });
        } else if (status === "paymentpending") {
          navigate("/trainer/application/payment", { replace: true });
        } else if (status === "paymentverified") {
          navigate("/trainer/application/review", { replace: true });
        } else if (status === "approved") {
          navigate("/trainer/dashboard", { replace: true });
        } else {
          // Default / Draft
          navigate("/trainer/application/details", { replace: true });
        }
      } catch {
        setNoTrainerApp(true);
        setLoading(false);
      }
    } catch (err) {
      setError(
        err?.response?.data?.message || err?.message || "Unable to verify OTP."
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="trainer-login-page">

      {/* LEFT — CINEMATIC IMAGE */}
      <section className="trainer-login-visual" aria-label="Ethos Trainer Studio">
        <div
          className="trainer-login-visual-image"
          style={{ backgroundImage: `url(${trainerLoginHero})` }}
        />

        <div className="trainer-login-visual-overlay" />

        <div className="trainer-login-visual-content">
          <span className="trainer-login-eyebrow">
            ETHOS / TRAINER STUDIO
          </span>

          <h1>
            Teach.
            <br />
            Create.
            <br />
            Evolve.
          </h1>

          <p>
            Your private space for workshops,
            <br />
            performance, schedule and growth.
          </p>

          <div className="trainer-login-visual-line" />

          <span className="trainer-login-visual-meta">
            ART&nbsp;&nbsp; | &nbsp;&nbsp;DISCIPLINE&nbsp;&nbsp; | &nbsp;&nbsp;COMMUNITY
          </span>
        </div>
      </section>

      {/* RIGHT — LOGIN PANEL */}
      <section className="trainer-login-panel">
        <div className="trainer-login-form-wrap">

          <Link to="/login" className="trainer-login-home">
            <span aria-hidden="true">←</span>
            BACK TO PORTAL SELECTION
          </Link>

          <div className="trainer-login-mark">
            <img
              src={ethosLogo}
              alt="Ethos Dance Studio"
              className="trainer-login-logo"
            />
          </div>

          <span className="trainer-login-eyebrow">
            ETHOS TRAINER PORTAL
          </span>

          {mobileNotFound ? (
            <div className="trainer-login-card-alert">
              <h2>Account not found</h2>
              <p className="trainer-login-subtitle" style={{ marginBottom: "24px" }}>
                Mobile number not found. Please register first to continue.
              </p>

              <button
                type="button"
                className="trainer-login-submit"
                onClick={() => navigate("/trainer/application")}
              >
                REGISTER AS TRAINER →
              </button>

              <button
                type="button"
                onClick={() => {
                  setMobileNotFound(false);
                  setError("");
                }}
                className="trainer-password-toggle"
                style={{
                  position: "static",
                  transform: "none",
                  display: "block",
                  margin: "20px auto 0",
                  textAlign: "center",
                }}
              >
                ← TRY ANOTHER MOBILE NUMBER
              </button>
            </div>
          ) : noTrainerApp ? (
            <div className="trainer-login-card-alert">
              <h2>No Trainer Profile</h2>
              <p className="trainer-login-subtitle" style={{ marginBottom: "24px" }}>
                You already have an ETHOS account, but you haven't registered as a trainer.
              </p>

              <button
                type="button"
                className="trainer-login-submit"
                onClick={() => navigate("/trainer/application")}
              >
                START TRAINER REGISTRATION →
              </button>

              <button
                type="button"
                onClick={() => {
                  setNoTrainerApp(false);
                  setOtpRequested(false);
                  setOtp("");
                  setError("");
                }}
                className="trainer-password-toggle"
                style={{
                  position: "static",
                  transform: "none",
                  display: "block",
                  margin: "20px auto 0",
                  textAlign: "center",
                }}
              >
                ← TRY ANOTHER MOBILE NUMBER
              </button>
            </div>
          ) : (
            <>
              <h2>Welcome back.</h2>

              <p className="trainer-login-subtitle">
                {!otpRequested
                  ? "We'll send a verification code to your registered mobile."
                  : "Enter the 6-digit verification code sent to your phone."}
              </p>

              {!otpRequested ? (
                <form onSubmit={handleSendOtp}>

                  <label htmlFor="mobile">
                    REGISTERED MOBILE NUMBER
                  </label>

                  <input
                    id="mobile"
                    type="tel"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    placeholder="Enter mobile number"
                    autoComplete="tel"
                    required
                  />

                  {error && (
                    <div className="trainer-login-error" role="alert">
                      {error}
                    </div>
                  )}

                  <button
                    type="submit"
                    className="trainer-login-submit"
                    disabled={loading}
                  >
                    {loading ? "SENDING OTP..." : "SEND OTP"}
                  </button>

                </form>
              ) : (
                <form onSubmit={handleVerifyOtp}>
                  {devOtpHint && (
                    <div
                      style={{
                        background: "rgba(233, 121, 99, 0.15)",
                        border: "1px solid rgba(233, 121, 99, 0.4)",
                        color: "#f09a88",
                        padding: "0.75rem 1rem",
                        borderRadius: "4px",
                        fontSize: "0.85rem",
                        marginBottom: "1.25rem",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: "12px",
                      }}
                    >
                      <div>
                        Dev Mode OTP: <strong style={{ color: "#ffffff", letterSpacing: "0.08em" }}>{devOtpHint}</strong>
                      </div>
                      <button
                        type="button"
                        onClick={() => {
                          setOtp(devOtpHint);
                          setError("");
                        }}
                        style={{
                          background: "#e97963",
                          color: "#160d0b",
                          border: "none",
                          padding: "5px 10px",
                          fontSize: "10px",
                          fontWeight: "800",
                          letterSpacing: "0.1em",
                          cursor: "pointer",
                          textTransform: "uppercase",
                          borderRadius: "2px",
                          whiteSpace: "nowrap",
                        }}
                      >
                        Auto-Fill
                      </button>
                    </div>
                  )}

                  <label htmlFor="otp">
                    ENTER VERIFICATION CODE
                  </label>

                  <div className="trainer-otp-field">
                    <input
                      id="otp"
                      type="text"
                      inputMode="numeric"
                      maxLength={6}
                      value={otp}
                      onChange={(e) => {
                        setOtp(e.target.value.replace(/\D/g, "").slice(0, 6));
                        setError("");
                      }}
                      placeholder="••••••"
                      autoFocus
                      autoComplete="one-time-code"
                      required
                    />
                  </div>

                  {error && (
                    <div className="trainer-login-error" role="alert">
                      {error}
                    </div>
                  )}

                  <button
                    type="submit"
                    className="trainer-login-submit"
                    disabled={loading}
                  >
                    {loading ? "VERIFYING..." : "VERIFY & CONTINUE"}
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      setOtpRequested(false);
                      setOtp("");
                      setError("");
                    }}
                    className="trainer-password-toggle"
                    style={{
                      position: "static",
                      transform: "none",
                      display: "block",
                      margin: "16px auto 0",
                      textAlign: "center",
                    }}
                  >
                    ← BACK TO MOBILE NUMBER
                  </button>
                </form>
              )}

              <div className="trainer-login-divider" />

              <p className="trainer-login-register">
                New to Ethos?{" "}
                <Link to="/trainer/application">
                  REGISTER AS A TRAINER
                </Link>
              </p>
            </>
          )}

        </div>
      </section>

    </div>
  );
}


