import { API_BASE_URL } from "../config/api";

export const AnalyticsEventType = Object.freeze({
  PageView: 1,
  WorkshopView: 2,
  WorkshopCheckoutStarted: 3,
  WorkshopCheckoutCompleted: 4,
  FeedbackOpened: 5,
  FeedbackSubmitted: 6,
  LoginStarted: 7,
  LoginCompleted: 8,
});

const VISITOR_ID_KEY = "ethos_visitor_id";
const SESSION_ID_KEY = "ethos_session_id";
const SESSION_ACTIVITY_KEY = "ethos_session_last_activity";
const SESSION_IDLE_TIMEOUT_MS = 30 * 60 * 1000; // 30 minutes

const ID_REGEX = /^[a-zA-Z0-9_-]{8,64}$/;

function generateId() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    try {
      return crypto.randomUUID();
    } catch {
      // Fallback below
    }
  }
  return "evt_" + Math.random().toString(36).substring(2, 15) + "_" + Date.now().toString(36);
}

export function getVisitorId() {
  if (typeof window === "undefined" || !window.localStorage) {
    return generateId();
  }

  try {
    let visitorId = localStorage.getItem(VISITOR_ID_KEY);
    if (!visitorId || !ID_REGEX.test(visitorId)) {
      visitorId = generateId();
      localStorage.setItem(VISITOR_ID_KEY, visitorId);
    }
    return visitorId;
  } catch {
    return generateId();
  }
}

export function getSessionId() {
  if (typeof window === "undefined" || !window.sessionStorage) {
    return generateId();
  }

  try {
    const now = Date.now();
    const lastActivityStr = sessionStorage.getItem(SESSION_ACTIVITY_KEY);
    const lastActivity = lastActivityStr ? Number(lastActivityStr) : 0;
    let sessionId = sessionStorage.getItem(SESSION_ID_KEY);

    if (!sessionId || !ID_REGEX.test(sessionId) || !lastActivity || now - lastActivity > SESSION_IDLE_TIMEOUT_MS) {
      sessionId = generateId();
      sessionStorage.setItem(SESSION_ID_KEY, sessionId);
    }

    sessionStorage.setItem(SESSION_ACTIVITY_KEY, String(now));
    return sessionId;
  } catch {
    return generateId();
  }
}

export function resetSession() {
  if (typeof window === "undefined" || !window.sessionStorage) return;
  try {
    sessionStorage.removeItem(SESSION_ID_KEY);
    sessionStorage.removeItem(SESSION_ACTIVITY_KEY);
  } catch {
    // Ignore storage errors
  }
}

function sanitizeMetadata(metadata) {
  if (!metadata || typeof metadata !== "object") return null;

  const sanitized = {};
  const entries = Object.entries(metadata).slice(0, 20);

  for (const [key, value] of entries) {
    if (!key || typeof key !== "string") continue;
    const cleanKey = key.trim().slice(0, 64);
    if (!cleanKey) continue;

    // Reject sensitive keys defensively
    const lowerKey = cleanKey.toLowerCase();
    if (
      lowerKey.includes("password") ||
      lowerKey.includes("token") ||
      lowerKey.includes("secret") ||
      lowerKey.includes("auth") ||
      lowerKey.includes("cookie") ||
      lowerKey.includes("credit") ||
      lowerKey.includes("card")
    ) {
      continue;
    }

    if (value === null || value === undefined) {
      sanitized[cleanKey] = "";
    } else if (typeof value === "object") {
      sanitized[cleanKey] = JSON.stringify(value).slice(0, 256);
    } else {
      sanitized[cleanKey] = String(value).slice(0, 256);
    }
  }

  return Object.keys(sanitized).length > 0 ? sanitized : null;
}

/**
 * Best-effort, non-blocking telemetry event dispatcher.
 * Uses navigator.sendBeacon when available, falling back to fetch with keepalive.
 * Never throws or interrupts calling application logic.
 */
export async function trackEvent(eventType, options = {}) {
  try {
    if (!eventType || typeof eventType !== "number") {
      return false;
    }

    const visitorId = getVisitorId();
    const sessionId = getSessionId();

    const path = typeof options.path === "string" 
      ? options.path.slice(0, 500) 
      : (typeof window !== "undefined" ? window.location.pathname.slice(0, 500) : "");

    const referrer = typeof options.referrer === "string"
      ? options.referrer.slice(0, 500)
      : (typeof document !== "undefined" && document.referrer ? document.referrer.slice(0, 500) : null);

    const workshopId = options.workshopId || null;

    // Automatic privacy-safe attribution extraction (UTM parameters & timezone)
    const rawMetadata = { ...(options.metadata || {}) };
    if (typeof window !== "undefined" && window.location && window.location.search) {
      try {
        const searchParams = new URLSearchParams(window.location.search);
        if (searchParams.has("utm_source")) rawMetadata.utm_source = searchParams.get("utm_source");
        if (searchParams.has("utm_medium")) rawMetadata.utm_medium = searchParams.get("utm_medium");
        if (searchParams.has("utm_campaign")) rawMetadata.utm_campaign = searchParams.get("utm_campaign");
      } catch {}
    }

    if (typeof Intl !== "undefined" && Intl.DateTimeFormat) {
      try {
        const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
        if (tz) rawMetadata.timeZone = tz;
      } catch {}
    }

    const metadata = sanitizeMetadata(rawMetadata);

    const payload = {
      eventType,
      visitorId,
      sessionId,
      workshopId,
      path: path || null,
      referrer: referrer || null,
      metadata,
    };

    const endpoint = `${API_BASE_URL}/api/analytics/events`;
    const jsonString = JSON.stringify(payload);

    // 1. Try navigator.sendBeacon
    if (typeof navigator !== "undefined" && typeof navigator.sendBeacon === "function") {
      try {
        const blob = new Blob([jsonString], { type: "application/json" });
        const enqueued = navigator.sendBeacon(endpoint, blob);
        if (enqueued) return true;
      } catch {
        // Fallback to fetch
      }
    }

    // 2. Fetch fallback with keepalive
    if (typeof fetch === "function") {
      fetch(endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: jsonString,
        keepalive: true,
      }).catch(() => {
        // Silently swallow network errors for analytics
      });
    }

    return true;
  } catch {
    // Fail silent to protect user experience
    return false;
  }
}

// Convenience Helpers
export function trackPageView(path, metadata = {}) {
  return trackEvent(AnalyticsEventType.PageView, { path, metadata });
}

export function trackWorkshopView(workshopId, metadata = {}) {
  return trackEvent(AnalyticsEventType.WorkshopView, { workshopId, metadata });
}

export function trackWorkshopCheckoutStarted(workshopId, metadata = {}) {
  return trackEvent(AnalyticsEventType.WorkshopCheckoutStarted, { workshopId, metadata });
}

export function trackWorkshopCheckoutCompleted(workshopId, metadata = {}) {
  return trackEvent(AnalyticsEventType.WorkshopCheckoutCompleted, { workshopId, metadata });
}

export function trackFeedbackOpened(workshopId, metadata = {}) {
  return trackEvent(AnalyticsEventType.FeedbackOpened, { workshopId, metadata });
}

export function trackFeedbackSubmitted(workshopId, metadata = {}) {
  return trackEvent(AnalyticsEventType.FeedbackSubmitted, { workshopId, metadata });
}

export function trackLoginStarted(metadata = {}) {
  return trackEvent(AnalyticsEventType.LoginStarted, { metadata });
}

export function trackLoginCompleted(metadata = {}) {
  return trackEvent(AnalyticsEventType.LoginCompleted, { metadata });
}
