import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

import adultImage from "../assets/about/about-main.jpg";
import kidsImage from "../assets/trainers/trainer-04.jpg";
import workshopImage from "../assets/workshops/workshop-02.jpg";

import { packagesApi } from "../services/packagesApi";
import { paymentApi } from "../services/paymentApi";
import { loadRazorpay } from "../services/razorpay";

import "../styles/classes.css";

const fallbackPasses = [
  {
    id: "6409cc48-124a-4b9f-8007-9b71bea8ea9c",
    category: "ADULTS",
    title: "Ethos Monthly Pass",
    description:
      "A monthly movement pass for adults who want to build consistency, confidence and strength through dance.",
    image: adultImage,
    validity: "30 DAYS",
    schedule: "WEEKLY CLASSES",
    level: "BEGINNER TO ADVANCED",
    price: "₹1,500",
    priceRaw: 1500,
    priceNote: "per month",
    included: [
      "Access to applicable monthly classes",
      "Student portal access",
      "Class schedule & updates",
      "Attendance tracking",
      "Applicable workshop pricing",
    ],
  },
  {
    id: "470bacfd-9977-4116-af11-77b205aab3c4",
    category: "UNLIMITED",
    title: "Admin VIP Unlimited Pass",
    description:
      "A structured dance experience designed to help dancers learn, move and grow with unlimited classes & workshops for 60 days.",
    image: kidsImage,
    validity: "60 DAYS",
    schedule: "UNLIMITED ACCESS",
    level: "ALL LEVELS",
    price: "₹4,999",
    priceRaw: 4999,
    priceNote: "per 60 days",
    included: [
      "Access to unlimited regular classes",
      "Student portal access",
      "Priority workshop reservations",
      "Attendance tracking",
      "VIP community access",
    ],
  },
];

function Classes() {
  const navigate = useNavigate();
  const [visible, setVisible] = useState(false);
  const pageRef = useRef(null);

  // Package & Payment States
  const [passesList, setPassesList] = useState(fallbackPasses);
  const [selectedPass, setSelectedPass] = useState(null);
  const [checkoutOpen, setCheckoutOpen] = useState(false);
  const [paymentProcessing, setPaymentProcessing] = useState(false);
  const [modalError, setModalError] = useState("");

  // Purchaser Form States
  const [fullName, setFullName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [purchaseSuccess, setPurchaseSuccess] = useState(false);

  useEffect(() => {
    window.scrollTo({
      top: 0,
      behavior: "instant",
    });

    const timer = setTimeout(() => {
      setVisible(true);
    }, 100);

    // Fetch active packages from backend to ensure synchronized pricing & IDs
    async function loadBackendPackages() {
      try {
        const data = await packagesApi.getActivePackages();
        if (data && Array.isArray(data) && data.length > 0) {
          const merged = data.map((pkg, idx) => {
            const fallback = fallbackPasses[idx] || fallbackPasses[0];
            return {
              id: pkg.id,
              category: pkg.classLimit ? "MONTHLY" : "UNLIMITED",
              title: pkg.name,
              description: pkg.description || fallback.description,
              image: idx % 2 === 0 ? adultImage : kidsImage,
              validity: `${pkg.durationDays} DAYS`,
              schedule: "WEEKLY CLASSES",
              level: "ALL STYLES",
              price: `₹${Number(pkg.price).toLocaleString("en-IN")}`,
              priceRaw: pkg.price,
              priceNote: `for ${pkg.durationDays} days`,
              included: fallback.included,
            };
          });
          setPassesList(merged);
        }
      } catch (err) {
        // Fallback already pre-set
        console.warn("Using default class passes definition:", err);
      }
    }

    loadBackendPackages();

    return () => clearTimeout(timer);
  }, []);

  function handleOpenCheckout(pass) {
    setSelectedPass(pass);
    setFullName("");
    setPhone("");
    setEmail("");
    setModalError("");
    setPurchaseSuccess(false);
    setCheckoutOpen(true);
  }

  function handleCloseCheckout() {
    if (paymentProcessing) return;
    setCheckoutOpen(false);
    setSelectedPass(null);
    setModalError("");
  }

  async function handleProceedToPayment(e) {
    e.preventDefault();
    setModalError("");

    const normalizedPhone = phone.replace(/\D/g, "");
    if (normalizedPhone.length !== 10) {
      setModalError("Please enter a valid 10-digit mobile number.");
      return;
    }

    if (!selectedPass?.id) {
      setModalError("Please select a valid membership pass.");
      return;
    }

    setPaymentProcessing(true);

    try {
      // 1. Create public order on backend
      const order = await paymentApi.createPublicPackageOrder({
        packageId: selectedPass.id,
        phone: normalizedPhone,
        fullName: fullName.trim() || null,
        email: email.trim() || null,
      });

      if (!order?.razorpayOrderId) {
        throw new Error("Razorpay Order ID was not received from server.");
      }

      // 2. Load Razorpay script
      await loadRazorpay();

      const razorpayKey = import.meta.env.VITE_RAZORPAY_KEY_ID;
      if (!razorpayKey) {
        throw new Error("Razorpay Key ID is not configured on the frontend.");
      }

      // 3. Launch Razorpay Checkout
      const options = {
        key: razorpayKey,
        amount: Math.round(Number(order.amount) * 100),
        currency: order.currency || "INR",
        name: "Ethos Dance Studio",
        description: selectedPass.title,
        order_id: order.razorpayOrderId,
        prefill: {
          name: fullName.trim() || "Ethos Student",
          contact: normalizedPhone,
          email: email.trim() || "",
        },
        theme: {
          color: "#d98d76",
        },
        handler: async function (response) {
          try {
            setPaymentProcessing(true);
            setModalError("");

            await paymentApi.verifyPublicPackagePayment({
              transactionId: order.id,
              razorpayOrderId: response.razorpay_order_id,
              razorpayPaymentId: response.razorpay_payment_id,
              razorpaySignature: response.razorpay_signature,
            });

            setPurchaseSuccess(true);
          } catch (verifyErr) {
            console.error("Verification error:", verifyErr);
            setModalError(
              verifyErr?.data?.message ||
              verifyErr?.message ||
              "Payment verification failed. If your payment was deducted, please contact Ethos support."
            );
          } finally {
            setPaymentProcessing(false);
          }
        },
        modal: {
          ondismiss: function () {
            setPaymentProcessing(false);
          },
        },
      };

      const razorpay = new window.Razorpay(options);
      razorpay.on("payment.failed", function (resp) {
        setPaymentProcessing(false);
        setModalError(
          resp?.error?.description || "Payment failed or was cancelled. Please try again."
        );
      });

      razorpay.open();
    } catch (err) {
      console.error("Order creation error:", err);
      setModalError(
        err?.data?.message ||
        err?.message ||
        "Unable to start checkout. Please check your details and try again."
      );
      setPaymentProcessing(false);
    }
  }

  return (
    <main
      ref={pageRef}
      className={`classes-page ${
        visible ? "classes-page--visible" : ""
      }`}
    >
      {/* HERO */}
      <section className="classes-hero">
        <div className="classes-hero__background">
          <div className="classes-hero__glow classes-hero__glow--one" />
          <div className="classes-hero__glow classes-hero__glow--two" />
        </div>

        <div className="classes-hero__content">
          <p className="classes-eyebrow">ETHOS CLASSES</p>

          <h1>
            FIND YOUR
            <span>RHYTHM.</span>
          </h1>

          <p className="classes-hero__intro">
            Choose your monthly pass. Build your practice. Become part of the Ethos movement.
          </p>

          <div className="classes-hero__actions">
            <a
              href="#passes"
              className="classes-button classes-button--primary"
            >
              EXPLORE PASSES
              <span>↓</span>
            </a>

            <Link
              to="/workshops"
              className="classes-button classes-button--ghost"
            >
              VIEW WORKSHOPS
              <span>↗</span>
            </Link>
          </div>
        </div>

        <div className="classes-hero__side-note">
          <span>MOVE</span>
          <span>CREATE</span>
          <span>BELONG</span>
        </div>
      </section>

      {/* INTRO */}
      <section className="classes-intro">
        <div className="classes-intro__heading">
          <p className="classes-eyebrow">MORE THAN A CLASS</p>

          <h2>
            YOUR MONTHLY
            <em>RITUAL.</em>
          </h2>
        </div>

        <div className="classes-intro__copy">
          <p>
            Ethos classes are designed around consistency. Your monthly pass gives you a place to return to every week, build your skills and become part of a community that moves together.
          </p>

          <p>
            Once your pass is active, you receive access to the Ethos Student Portal where you can manage your classes, schedule and student experience.
          </p>
        </div>
      </section>

      {/* PASSES — HORIZONTAL ROWS */}
      <section id="passes" className="classes-passes">
        <div className="classes-section-heading">
          <div>
            <p className="classes-eyebrow">MONTHLY PASSES</p>

            <h2>
              CHOOSE YOUR
              <span>PASS.</span>
            </h2>
          </div>

          <p>
            Start with the pass that fits your movement, schedule and goals.
          </p>
        </div>

        <div className="classes-pass-rows">
          {passesList.map((pass) => (
            <article key={pass.id} className="classes-details">
              <div className="classes-details__image">
                <img src={pass.image} alt={pass.title} />
              </div>

              <div className="classes-details__content">
                <h2>{pass.title}</h2>
                <p className="classes-details__description">{pass.description}</p>

                <div className="classes-included">
                  {pass.included.map((item) => (
                    <div key={item} className="classes-included__item">
                      <span>✓</span>
                      <p>{item}</p>
                    </div>
                  ))}
                </div>

                <div className="classes-details__price">
                  <div>
                    <strong>{pass.price}</strong>
                    <small>{pass.priceNote}</small>
                  </div>

                  <button
                    type="button"
                    onClick={() => handleOpenCheckout(pass)}
                  >
                    REGISTER & PAY
                  </button>
                </div>

                <p className="classes-details__note">
                  Secure payment activates instant Student Portal access.
                </p>
              </div>
            </article>
          ))}
        </div>
      </section>

      {/* STUDENT JOURNEY */}
      <section className="classes-journey">
        <div className="classes-journey__heading">
          <p className="classes-eyebrow">THE ETHOS EXPERIENCE</p>

          <h2>
            FROM YOUR
            <span>FIRST CLASS</span>
            TO THE FLOOR.
          </h2>
        </div>

        <div className="classes-journey__visual">
          <img src={workshopImage} alt="Ethos workshop" />
          <div className="classes-journey__overlay">
            <span>CLASS → COMMUNITY</span>
          </div>
        </div>

        <div className="classes-journey__steps">
          <article>
            <span>CHOOSE</span>
            <h3>SELECT YOUR PASS</h3>
            <p>Choose the monthly class pass that fits your movement goals.</p>
          </article>

          <article>
            <span>ACTIVATE</span>
            <h3>JOIN ETHOS</h3>
            <p>Complete registration and payment to activate your monthly experience.</p>
          </article>

          <article>
            <span>MOVE</span>
            <h3>ENTER YOUR PORTAL</h3>
            <p>Your active pass gives you access to the Ethos Student Portal.</p>
          </article>

          <article>
            <span>EXPLORE</span>
            <h3>UNLOCK WORKSHOPS</h3>
            <p>Eligible students can receive applicable discounted workshop pricing.</p>
          </article>
        </div>
      </section>

      {/* WORKSHOP CONNECTION */}
      <section className="classes-workshops">
        <div className="classes-workshops__content">
          <p className="classes-eyebrow">KEEP MOVING</p>

          <h2>
            YOUR CLASS
            <span>IS ONLY THE BEGINNING.</span>
          </h2>

          <p>
            Build your foundation through regular classes, then take your movement further through Ethos workshops, guest artists and special experiences.
          </p>

          <Link
            to="/workshops"
            className="classes-button classes-button--light"
          >
            EXPLORE WORKSHOPS
            <span>↗</span>
          </Link>
        </div>

        <div className="classes-workshops__word">ETHOS</div>
      </section>

      {/* FAQ / INFORMATION */}
      <section className="classes-info">
        <div>
          <p className="classes-eyebrow">BEFORE YOU JOIN</p>

          <h2>
            GOOD TO
            <em>KNOW.</em>
          </h2>
        </div>

        <div className="classes-info__list">
          <details>
            <summary>
              Who can join Ethos classes?
              <span>+</span>
            </summary>
            <p>
              Ethos welcomes dancers across different experience levels. The applicable level and class availability depend on the selected programme.
            </p>
          </details>

          <details>
            <summary>
              What happens after I purchase a pass?
              <span>+</span>
            </summary>
            <p>
              After successful payment verification, your monthly pass can be activated and your student experience will be available through the Student Portal.
            </p>
          </details>

          <details>
            <summary>
              Does a class pass include workshops?
              <span>+</span>
            </summary>
            <p>
              Workshops are separate experiences. An active student pass may make you eligible for applicable student workshop pricing.
            </p>
          </details>

          <details>
            <summary>
              Can I attend without a monthly pass?
              <span>+</span>
            </summary>
            <p>
              Class access depends on the programme and availability. Contact Ethos if you are unsure which option is right for you.
            </p>
          </details>
        </div>
      </section>

      {/* CONTACT CTA */}
      <section className="classes-contact">
        <div className="classes-contact__inner">
          <p className="classes-eyebrow">READY WHEN YOU ARE</p>

          <h2>
            LET'S
            <span>MOVE.</span>
          </h2>

          <p>
            Not sure which pass is right for you? Speak with Ethos before registering.
          </p>

          <div className="classes-contact__actions">
            <Link
              to="/#contact"
              className="classes-button classes-button--primary"
            >
              CONTACT ETHOS
              <span>↗</span>
            </Link>

            <a
              href="https://wa.me/918341701113"
              target="_blank"
              rel="noreferrer"
              className="classes-button classes-button--ghost"
            >
              WHATSAPP US
              <span>↗</span>
            </a>
          </div>
        </div>
      </section>

      {/* CHECKOUT MODAL */}
      {checkoutOpen && (
        <div className="classes-modal-overlay" onClick={handleCloseCheckout}>
          <div
            className="classes-modal"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
          >
            <button
              type="button"
              className="classes-modal-close"
              onClick={handleCloseCheckout}
              disabled={paymentProcessing}
            >
              ✕
            </button>

            {!purchaseSuccess ? (
              <>
                <div className="classes-modal-header">
                  <span className="classes-modal-eyebrow">STUDENT REGISTRATION & PASS</span>
                  <h3>{selectedPass?.title}</h3>
                  <div className="classes-modal-summary">
                    <span className="classes-modal-price">{selectedPass?.price}</span>
                    <span className="classes-modal-validity">• {selectedPass?.validity}</span>
                  </div>
                </div>

                {modalError && (
                  <div className="classes-modal-error">
                    {modalError}
                  </div>
                )}

                <form onSubmit={handleProceedToPayment} className="classes-modal-form">
                  <div className="classes-form-group">
                    <label htmlFor="student-name">Full Name</label>
                    <input
                      id="student-name"
                      type="text"
                      placeholder="e.g. Maya Lin"
                      value={fullName}
                      onChange={(e) => setFullName(e.target.value)}
                      disabled={paymentProcessing}
                      required
                    />
                  </div>

                  <div className="classes-form-group">
                    <label htmlFor="student-phone">Mobile Number</label>
                    <div className="classes-phone-input-wrap">
                      <span className="classes-phone-prefix">+91</span>
                      <input
                        id="student-phone"
                        type="tel"
                        maxLength="10"
                        placeholder="10-digit mobile"
                        value={phone}
                        onChange={(e) => setPhone(e.target.value.replace(/\D/g, "").slice(0, 10))}
                        disabled={paymentProcessing}
                        required
                      />
                    </div>
                    <small className="classes-field-note">
                      Your Student Portal login will be connected to this number.
                    </small>
                  </div>

                  <div className="classes-form-group">
                    <label htmlFor="student-email">Email Address (Optional)</label>
                    <input
                      id="student-email"
                      type="email"
                      placeholder="e.g. maya@example.com"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      disabled={paymentProcessing}
                    />
                  </div>

                  <button
                    type="submit"
                    className="classes-modal-submit"
                    disabled={paymentProcessing}
                  >
                    {paymentProcessing ? "PROCESSING SECURE ORDER..." : `PAY ${selectedPass?.price} VIA RAZORPAY`}
                  </button>

                  <p className="classes-modal-footer-note">
                    🔒 Secured by Razorpay. An active pass immediately authorizes Student Portal access.
                  </p>
                </form>
              </>
            ) : (
              <div className="classes-modal-success">
                <div className="classes-success-badge">✓</div>
                <h3>PAYMENT VERIFIED</h3>
                <p className="classes-success-lead">
                  Welcome to Ethos! Your <strong>{selectedPass?.title}</strong> is now active.
                </p>
                <p className="classes-success-sub">
                  Your mobile number <strong>+91 {phone}</strong> has been enrolled and your Student Portal membership has been activated.
                </p>

                <button
                  type="button"
                  className="classes-modal-submit classes-modal-submit--accent"
                  onClick={() => {
                    const normalized = phone.replace(/\D/g, "");
                    navigate(`/student/login?phone=${encodeURIComponent(normalized)}`);
                  }}
                >
                  CONTINUE TO STUDENT LOGIN →
                </button>
              </div>
            )}
          </div>
        </div>
      )}
    </main>
  );
}

export default Classes;
