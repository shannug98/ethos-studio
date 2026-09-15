import React from "react";
import ReactDOM from "react-dom/client";

import App from "./App";
import { AuthProvider } from "./context/AuthContext";

import "./styles/global.css";
import "./styles/trainer/trainer-global.css";
import "./styles/trainer/trainer-layout.css";
import "./styles/trainer/trainer-dashboard.css";
import "./styles/trainer/trainer-application.css";
import "./styles/trainer/trainer-workshops.css";
import "./styles/trainer/trainer-schedule.css";
import "./styles/trainer/trainer-performance.css";
import "./styles/trainer/trainer-tier.css";
import "./styles/trainer/trainer-notifications.css";
import "./styles/admin-overhaul.css";

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <AuthProvider>
      <App />
    </AuthProvider>
  </React.StrictMode>
);
