import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import { trainerApi } from "../../services/trainerApi";
import { paymentApi } from "../../services/paymentApi";
import { loadRazorpay } from "../../services/razorpay";

import "./TrainerApplicationPayment.css";

const TRAINER_APPLICATION_PURPOSE = 2;

export default function TrainerApplicationPayment() {
  const navigate = useNavigate();

  const [application, setApplication] = useState(null);
  const [tier, setTier] = useState(null);

  const [loading, setLoading] = useState(true);
  const [paying, setPaying] = useState(false);

  const [error, setError] = useState("");
  const [paymentMessage, setPaymentMessage] = useState("");

  useEffect(() => {
    let mounted = true;

    async function loadPaymentDetails() {
      try {
        const [applicationData, tiers] =
          await Promise.all([
            trainerApi.getApplication(),
            trainerApi.getTiers(),
          ]);

        if (!mounted) return;

        setApplication(applicationData);

        const selectedTier = (tiers || []).find(
          (item) =>
            item.id === applicationData?.currentTierId
        );

        if (!selectedTier) {
          throw new Error(
            "Your selected trainer tier could not be found."
          );
        }

        setTier(selectedTier);

        /*
         * If payment was already verified, don't create
         * another Razorpay order.
         */
        if (
          String(applicationData?.status)
            .toLowerCase()
            .includes("paymentverified")
        ) {
          navigate(
            "/trainer/application/review",
            { replace: true }
          );
        }
      } catch (err) {
        if (!mounted) return;

        setError(
          err?.message ||
            "Unable to load your payment details."
        );
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    }

    loadPaymentDetails();

    return () => {
      mounted = false;
    };
  }, [navigate]);

  async function handlePayment() {
    if (!application?.id) {
      setError(
        "Your trainer application could not be identified."
      );
      return;
    }

    if (!tier) {
      setError(
        "Your selected trainer tier could not be identified."
      );
      return;
    }

    if (
      tier.applicationFee === null ||
      tier.applicationFee === undefined
    ) {
      setError(
        "This trainer tier does not currently have a configured application fee."
      );
      return;
    }

    setPaying(true);
    setError("");
    setPaymentMessage("");

    try {
      /*
       * Load Razorpay checkout first.
       */
      await loadRazorpay();

      /*
       * IMPORTANT:
       *
       * We do NOT send the amount.
       *
       * Backend determines:
       *
       * application
       *   -> trainer profile
       *   -> current tier
       *   -> application fee
       */
      const order = await paymentApi.createOrder({
        purpose: TRAINER_APPLICATION_PURPOSE,
        referenceId: application.id,
      });

      if (!order?.id) {
        throw new Error(
          "Payment transaction could not be created."
        );
      }

      if (!order?.razorpayOrderId) {
        throw new Error(
          "Razorpay order ID was not returned by the server."
        );
      }

      const razorpayKey =
        import.meta.env.VITE_RAZORPAY_KEY_ID;

      if (!razorpayKey) {
        throw new Error(
          "Razorpay Key ID is not configured."
        );
      }

      /*
       * Backend response amount is INR.
       *
       * Razorpay Checkout expects paise.
       */
      const amountInPaise = Math.round(
        Number(order.amount) * 100
      );

      if (!Number.isFinite(amountInPaise) || amountInPaise <= 0) {
        throw new Error(
          "Invalid payment amount returned by server."
        );
      }

      const options = {
        key: razorpayKey,

        amount: amountInPaise,

        currency: order.currency || "INR",

        name: "ETHOS Dance Studio",

        description:
          `${tier.name} — Trainer Application`,

        order_id: order.razorpayOrderId,

        prefill: {
          name:
            application.fullName ||
            "ETHOS Trainer Applicant",
        },

        theme: {
          color: "#e98a68",
        },

        modal: {
          ondismiss: () => {
            setPaying(false);
            setPaymentMessage(
              "Payment window closed. Your application has not been submitted."
            );
          },
        },

        handler: async (response) => {
          try {
            setPaymentMessage(
              "Payment received. Verifying securely..."
            );

            /*
             * NEVER trust the browser callback as proof
             * of payment.
             *
             * Send Razorpay's identifiers to the backend.
             */
            const verified =
              await paymentApi.verifyPayment({
                transactionId: order.id,
                razorpayOrderId:
                  response.razorpay_order_id,
                razorpayPaymentId:
                  response.razorpay_payment_id,
                razorpaySignature:
                  response.razorpay_signature,
              });

            if (
              !verified ||
              !verified.id
            ) {
              throw new Error(
                "Payment verification failed."
              );
            }

            setPaymentMessage(
              "Payment verified successfully."
            );

            navigate(
              "/trainer/application/review",
              { replace: true }
            );
          } catch (err) {
            setError(
              err?.message ||
                "Payment was received but could not be verified."
            );

            setPaymentMessage("");
            setPaying(false);
          }
        },
      };

      const razorpay = new window.Razorpay(options);

      razorpay.on(
        "payment.failed",
        (response) => {
          setError(
            response?.error?.description ||
              "Payment failed. Please try again."
          );

          setPaymentMessage("");
          setPaying(false);
        }
      );

      razorpay.open();
    } catch (err) {
      setError(
        err?.message ||
          "Unable to start the payment."
      );

      setPaymentMessage("");
      setPaying(false);
    }
  }

  if (loading) {
    return (
      <main className="trainer-payment-page">
        <div className="trainer-payment-loading">
          Preparing your secure payment...
        </div>
      </main>
    );
  }

  if (!application || !tier) {
    return (
      <main className="trainer-payment-page">
        <div className="trainer-payment-error-page">
          <span>PAYMENT</span>

          <h1>
            We couldn't prepare
            <br />
            <em>your payment.</em>
          </h1>

          <p>{error}</p>

          <button
            type="button"
            onClick={() =>
              navigate(
                "/trainer/application/tier"
              )
            }
          >
            ← BACK TO TIERS
          </button>
        </div>
      </main>
    );
  }

  const amount = Number(
    tier.applicationFee || 0
  );

  return (
    <main className="trainer-payment-page">
      <section className="trainer-payment-shell">

        <header className="trainer-payment-header">
          <div>
            <span className="trainer-payment-eyebrow">
              ETHOS TRAINER APPLICATION · STEP 04 — PAYMENT
            </span>

            <h1>
              Begin your
              <br />
              <em>journey.</em>
            </h1>

            <p>
              Your application is almost ready.
              Complete the application fee to continue
              to the final review.
            </p>
          </div>

          <div className="trainer-payment-progress">
            <span>05 / 05</span>

            <div>
              <i />
            </div>

            <small>SECURE PAYMENT</small>
          </div>
        </header>

        {error && (
          <div className="trainer-payment-alert">
            {error}
          </div>
        )}

        {paymentMessage && (
          <div className="trainer-payment-message">
            {paymentMessage}
          </div>
        )}

        <section className="trainer-payment-card">

          <div className="trainer-payment-card-top">
            <span>YOUR SELECTED PATH</span>

            <span>
              {tier.code}
            </span>
          </div>

          <div className="trainer-payment-tier">
            <div className="trainer-payment-tier-mark">
              {String(
                tier.displayOrder
              ).padStart(2, "0")}
            </div>

            <div>
              <span>TRAINER TIER</span>

              <h2>{tier.name}</h2>

              <p>
                {tier.description ||
                  "Your selected ETHOS trainer pathway."}
              </p>
            </div>
          </div>

          <div className="trainer-payment-divider" />

          <div className="trainer-payment-amount">
            <div>
              <span>APPLICATION FEE</span>

              <small>
                One-time trainer application fee
              </small>
            </div>

            <strong>
              ₹
              {amount.toLocaleString("en-IN", {
                minimumFractionDigits: 0,
                maximumFractionDigits: 2,
              })}
            </strong>
          </div>

          <div className="trainer-payment-security">
            <div>⌁</div>

            <div>
              <strong>
                Secure Razorpay payment
              </strong>

              <p>
                Your payment is securely processed
                through Razorpay. ETHOS never stores
                your card or banking credentials.
              </p>
            </div>
          </div>

        </section>

        <footer className="trainer-payment-actions">
          <button
            type="button"
            disabled={paying}
            onClick={() =>
              navigate(
                "/trainer/application/tier"
              )
            }
          >
            ← CHANGE TIER
          </button>

          <button
            type="button"
            disabled={paying}
            onClick={handlePayment}
          >
            {paying
              ? "PROCESSING..."
              : `PAY ₹${amount.toLocaleString(
                  "en-IN"
                )} & CONTINUE →`}
          </button>
        </footer>

      </section>
    </main>
  );
}
