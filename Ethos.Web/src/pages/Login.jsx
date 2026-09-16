import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import LoginComingSoonModal from "../components/common/LoginComingSoonModal";
import "./Login.css";

export default function Login() {
  const navigate = useNavigate();
  const [modalOpen, setModalOpen] = useState(true);

  const handleContact = () => {
    navigate("/", { state: { scrollTo: "contact" } });
  };

  const handleBackHome = () => {
    navigate("/");
  };

  return (
    <main className="ethos-login-gateway">
      <LoginComingSoonModal
        isOpen={modalOpen}
        onClose={handleBackHome}
      />
      <div className="ethos-login-gateway-inner">

        <div className="ethos-login-header">
          <span className="ethos-login-eyebrow">
            ETHOS DANCE STUDIO ✦ MEMBER SERVICES
          </span>

          <h1>
            Login & Portals<br />
            <em>Coming Soon.</em>
          </h1>

          <p>
            Login and member services are coming soon. Please contact Ethos Dance Studio for more information, course admissions, or workshop reservations.
          </p>

          <div style={{ display: "flex", gap: "16px", marginTop: "32px", flexWrap: "wrap" }}>
            <button
              type="button"
              className="ethos-login-card-action"
              onClick={handleContact}
              style={{
                background: "#e97963",
                color: "#11100f",
                border: "none",
                padding: "14px 28px",
                cursor: "pointer",
                fontWeight: "700",
                letterSpacing: "0.14em"
              }}
            >
              CONTACT ETHOS →
            </button>

            <button
              type="button"
              className="ethos-login-card-action"
              onClick={handleBackHome}
              style={{
                background: "transparent",
                color: "#f5f1eb",
                border: "1px solid rgba(255, 255, 255, 0.2)",
                padding: "14px 28px",
                cursor: "pointer",
                fontWeight: "700",
                letterSpacing: "0.14em"
              }}
            >
              BACK TO HOME
            </button>
          </div>
        </div>

      </div>
    </main>
  );
}
