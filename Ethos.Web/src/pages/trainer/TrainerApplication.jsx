import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import { authApi } from "../../services/authApi";
import "../../styles/trainer/trainer-application.css";
import trainerApplicationHero from "../../assets/trainer/trainer-application-hero.png";
import logo from "../../assets/logo.png";

const OTP_LENGTH = 6;
const RESEND_SECONDS = 30;

export default function TrainerApplication() {
  const navigate = useNavigate();
  const { verifyOtp } = useAuth();

  const [step, setStep] = useState("phone");
  const [phone, setPhone] = useState("");
  const [otp, setOtp] = useState("");
  const [devOtp, setDevOtp] = useState("");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
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

  function handlePhoneChange(event) {
    setPhone(normalizePhone(event.target.value));
    setError("");
  }

  async function handleSendOtp(event) {
    event.preventDefault();

    setError("");
    setMessage("");

    if (phone.length !== 10) {
      setError("Please enter a valid 10-digit mobile number.");
      return;
    }

    setLoading(true);

    try {
      const data = await authApi.requestOtp(phone, "TRAINER_REGISTRATION");

      setStep("otp");
      setOtp("");
      setResendTimer(RESEND_SECONDS);
      setMessage("OTP sent successfully. Please check your mobile.");

      if (data?.developmentOtp) {
        setDevOtp(data.developmentOtp);
      } else {
        setDevOtp("");
      }
    } catch (err) {
      setError(err.message || "Unable to send OTP.");
    } finally {
      setLoading(false);
    }
  }

  async function handleVerifyOtp(event) {
    event.preventDefault();

    setError("");
    setMessage("");

    if (otp.length !== OTP_LENGTH) {
      setError("Please enter the 6-digit OTP.");
      return;
    }

    setLoading(true);

    try {
      const result = await verifyOtp(phone, otp, "TRAINER_REGISTRATION");

      if (!result) {
        throw new Error("OTP verification failed.");
      }

      /*
       * OTP verification creates/authenticates the Ethos User.
       * The AuthContext stores the JWT in localStorage.
       *
       * 3A ends here.
       * 3B will create/load the trainer application and
       * continue with the personal-details experience.
       */
      navigate("/trainer/application/details", {
        replace: true,
      });
    } catch (err) {
      setError(err.message || "Invalid OTP. Please try again.");
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
      const data = await authApi.requestOtp(phone, "TRAINER_REGISTRATION");

      setOtp("");
      setResendTimer(RESEND_SECONDS);
      setMessage("A new OTP has been sent to your mobile.");

      if (data?.developmentOtp) {
        setDevOtp(data.developmentOtp);
      } else {
        setDevOtp("");
      }
    } catch (err) {
      setError(err.message || "Unable to resend OTP.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="trainer-application-page">
      <div className="trainer-application-shell">
        <section
          className="trainer-application-visual"
          style={{
            backgroundImage: `url(${trainerApplicationHero})`,
            backgroundSize: "cover",
            backgroundPosition: "center",
          }}
        >
          <div className="trainer-application-overlay" />

          <div className="trainer-application-visual-content">
            <span className="trainer-eyebrow">ETHOS DANCE STUDIO</span>

            <h1>
              Teach.
              <br />
              Create.
              <br />
              <em>Inspire.</em>
            </h1>

            <p>
              Become part of a movement built around artistry,
              discipline and the culture of dance.
            </p>

            <div className="trainer-application-mantra">
              <span>ETHOS</span>
              <span>ARTISTRY</span>
              <span>COMMUNITY</span>
            </div>
          </div>
        </section>

        <section className="trainer-application-form-area">
          <div className="trainer-application-form">
            <Link to="/" className="trainer-application-back">
              <span aria-hidden="true">←</span>
              Back to Ethos
            </Link>

            <div className="trainer-application-logo">
              <img src={logo} alt="Ethos Dance Studio" />
            </div>

            {step === "phone" ? (
              <>
                <div className="trainer-application-heading">
                  <span className="trainer-eyebrow trainer-application-eyebrow">
                    BECOME AN ETHOS TRAINER
                  </span>

                  <h1>Let&apos;s begin.</h1>

                  <p className="trainer-application-subtitle">
                    Verify your mobile number to start your trainer
                    application.
                  </p>
                </div>

                <form onSubmit={handleSendOtp}>
                  <div className="trainer-form-field trainer-phone-field">
                    <label htmlFor="trainer-phone">
                      MOBILE NUMBER
                    </label>

                    <div className="trainer-phone-input">
                      <span className="trainer-phone-prefix">+91</span>

                      <input
                        id="trainer-phone"
                        type="tel"
                        inputMode="numeric"
                        autoComplete="tel"
                        placeholder="Enter 10-digit number"
                        value={phone}
                        onChange={handlePhoneChange}
                        maxLength={10}
                        disabled={loading}
                      />
                    </div>
                  </div>

                  {error && (
                    <div className="trainer-application-message trainer-application-message--error">
                      {error}
                    </div>
                  )}

                  <button
                    type="submit"
                    className="trainer-application-primary-button trainer-application-submit"
                    disabled={loading}
                  >
                    {loading ? "SENDING OTP..." : "SEND OTP"}
                  </button>
                </form>

                <div className="trainer-application-security">
                  <div className="trainer-security-header">
                    <span className="trainer-security-number">01</span>

                    <div>
                      <h3>Secure verification</h3>
                      <p className="trainer-security-intro">
                        Your mobile number is used to securely verify your Ethos account
                        and protect your trainer application.
                      </p>
                    </div>
                  </div>

                  <p className="trainer-security-copy">
                    We’ll use it only for authentication, verification codes and
                    important account notifications.
                  </p>
                </div>
              </>
            ) : (
              <>
                <button
                  type="button"
                  className="trainer-application-back"
                  onClick={handleChangeNumber}
                  disabled={loading}
                >
                  ← CHANGE NUMBER
                </button>

                <div className="trainer-application-heading">
                  <span className="trainer-eyebrow">
                    MOBILE VERIFICATION
                  </span>

                  <h2>Check your phone.</h2>

                  <p>
                    Enter the 6-digit verification code sent to
                    <strong> +91 {phone}</strong>.
                  </p>
                </div>

                <form onSubmit={handleVerifyOtp}>
                  <label htmlFor="trainer-otp">
                    VERIFICATION CODE
                  </label>

                  <input
                    id="trainer-otp"
                    className="trainer-otp-input"
                    type="text"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    placeholder="000000"
                    value={otp}
                    onChange={(event) => {
                      setOtp(
                        event.target.value
                          .replace(/\D/g, "")
                          .slice(0, OTP_LENGTH)
                      );
                      setError("");
                    }}
                    maxLength={OTP_LENGTH}
                    disabled={loading}
                    autoFocus
                  />

                  {message && (
                    <div className="trainer-application-message trainer-application-message--success">
                      {message}
                    </div>
                  )}

                  {import.meta.env.DEV && devOtp && (
                    <div className="trainer-dev-otp-card">
                      <div className="trainer-dev-otp-label">DEV OTP</div>
                      <div className="trainer-dev-otp-code">{devOtp}</div>
                      <div className="trainer-dev-otp-note">
                        Development environment only
                      </div>
                    </div>
                  )}

                  {error && (
                    <div className="trainer-application-message trainer-application-message--error">
                      {error}
                    </div>
                  )}

                  <button
                    type="submit"
                    className="trainer-application-primary-button trainer-application-submit"
                    disabled={loading || otp.length !== OTP_LENGTH}
                  >
                    {loading ? "VERIFYING..." : "VERIFY & CONTINUE"}
                  </button>
                </form>

                <div className="trainer-otp-resend">
                  {resendTimer > 0 ? (
                    <span>
                      Resend code in{" "}
                      <strong>{resendTimer}s</strong>
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

                <div className="trainer-application-security">
                  <div className="trainer-security-header">
                    <span className="trainer-security-number">02</span>

                    <div>
                      <h3>Account protection</h3>
                      <p className="trainer-security-intro">
                        Once verified, you&apos;ll continue seamlessly to your trainer profile and application workspace.
                      </p>
                    </div>
                  </div>
                </div>
              </>
            )}

            <div className="trainer-application-footer">
              <span>ETHOS DANCE STUDIO</span>
              <span>TRAINER REGISTRATION</span>
            </div>
          </div>
        </section>
      </div>
    </main>
  );
}
