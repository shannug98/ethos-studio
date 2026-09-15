/**
 * Maps raw backend event/action codes into user-friendly titles and human readable descriptions.
 */
export const EVENT_LABEL_MAP = {
  PAYMENT_RECEIPT_GENERATED: {
    title: "Payment receipt generated",
    category: "Payments",
    description: "An official payment receipt was generated for an incoming transaction.",
  },
  PAYMENT_SUCCESS: {
    title: "Payment processed successfully",
    category: "Payments",
    description: "Student payment completed and reconciled with Razorpay.",
  },
  PAYMENT_FAILED: {
    title: "Payment processing failed",
    category: "Payments",
    description: "Student transaction was declined or timed out at payment gateway.",
  },
  ADMIN_DEVICE_REGISTERED: {
    title: "Admin device registered",
    category: "Security",
    description: "A new browser / terminal session was authorized for administrative access.",
  },
  ADMIN_DEVICE_REVOKED: {
    title: "Admin device revoked",
    category: "Security",
    description: "An administrative device slot was revoked and disconnected.",
  },
  ADMIN_LOGIN_DEVICE_BLOCKED: {
    title: "Unauthorized device login blocked",
    category: "Security",
    description: "Login was intercepted because the device fingerprint is not in the approved 2-device fleet.",
  },
  ADMIN_LOGIN_REJECTED: {
    title: "Admin login rejected",
    category: "Security",
    description: "Administrative credentials or OTP verification failed during authentication.",
  },
  ADMIN_LOGIN_SUCCESS: {
    title: "Admin login successful",
    category: "Security",
    description: "Administrator authenticated into command session.",
  },
  ADMIN_MFA_FAILED: {
    title: "MFA challenge failed",
    category: "Security",
    description: "Multi-factor authentication attempt entered an invalid or expired passcode.",
  },
  ADMIN_AUTHORIZATION_DENIED: {
    title: "Authorization denied",
    category: "Security",
    description: "Gated administrative action blocked by permissions policy.",
  },
  WORKSHOP_CREATED: {
    title: "Workshop created",
    category: "Workshops",
    description: "A new dance studio workshop was scheduled.",
  },
  WORKSHOP_APPROVED: {
    title: "Workshop approved",
    category: "Workshops",
    description: "Administrator approved trainer workshop submission.",
  },
  WORKSHOP_REJECTED: {
    title: "Workshop rejected",
    category: "Workshops",
    description: "Administrator declined trainer workshop proposal.",
  },
  WORKSHOP_BOOKING_CREATED: {
    title: "Workshop booking confirmed",
    category: "Workshops",
    description: "A student successfully registered for a workshop slot.",
  },
  CLASS_BOOKING_CREATED: {
    title: "Class enrollment booked",
    category: "Classes",
    description: "Student enrolled into regular studio dance class.",
  },
  TRAINER_APPROVED: {
    title: "Trainer application approved",
    category: "Audit",
    description: "Administrator approved trainer onboarding dossier.",
  },
  TRAINER_REJECTED: {
    title: "Trainer application rejected",
    category: "Audit",
    description: "Administrator declined trainer onboarding application.",
  },
  PACKAGE_PURCHASED: {
    title: "Class package purchased",
    category: "Payments",
    description: "Student completed package pass purchase.",
  },
  PERMISSION_CHANGED: {
    title: "Permission matrix modified",
    category: "Security",
    description: "Administrative access override or role assignment was adjusted.",
  },
  INCIDENT_CREATED: {
    title: "Operational incident opened",
    category: "Incidents",
    description: "A new telemetry incident ticket was logged for technical investigation.",
  },
  INCIDENT_UPDATED: {
    title: "Incident status updated",
    category: "Incidents",
    description: "Incident mitigation status or assignee was modified.",
  },
  COMMUNICATION_RETRIED: {
    title: "Communication dispatch retried",
    category: "Communications",
    description: "System retried sending automated SMS / WhatsApp notification after initial failure.",
  },
  COMMUNICATION_SENT: {
    title: "Notification dispatched",
    category: "Communications",
    description: "Transactional message delivered to recipient.",
  },
};

export function getEventDisplay(rawAction) {
  if (!rawAction) {
    return {
      title: "Unknown action",
      category: "Audit",
      description: "No description available for this event.",
    };
  }

  const normalized = String(rawAction).trim().toUpperCase();
  if (EVENT_LABEL_MAP[normalized]) {
    return EVENT_LABEL_MAP[normalized];
  }

  const fallbackTitle = normalized
    .replace(/_/g, " ")
    .toLowerCase()
    .replace(/\b\w/g, (char) => char.toUpperCase());

  return {
    title: fallbackTitle,
    category: "Audit",
    description: `Administrative event: ${rawAction}`,
  };
}

export function formatShortTraceId(traceId) {
  if (!traceId || String(traceId).trim().length === 0 || traceId === "Not available") {
    return null;
  }
  const clean = String(traceId).trim();
  if (clean.length <= 14) return clean;
  return `${clean.slice(0, 11)}...`;
}

export function formatRelatedEntity(rawEntity) {
  if (!rawEntity || rawEntity === "—") return "General";
  const str = String(rawEntity).trim().toUpperCase();

  if (str.includes("PAYMENT")) return "Payment";
  if (str.includes("COMMUNICATION") || str.includes("MSG")) return "Communication";
  if (str.includes("WORKSHOP")) return "Workshop";
  if (str.includes("CLASS")) return "Class";
  if (str.includes("STUDENT")) return "Student";
  if (str.includes("TRAINER")) return "Trainer";
  if (str.includes("SECURITY") || str.includes("DEVICE") || str.includes("AUTH")) return "Security";
  if (str.includes("INCIDENT")) return "Incident";
  if (str.includes("PACKAGE")) return "Package";
  if (str.includes("AUDIT")) return "Audit";

  return rawEntity
    .replace(/_/g, " ")
    .toLowerCase()
    .replace(/\b\w/g, (c) => c.toUpperCase());
}
