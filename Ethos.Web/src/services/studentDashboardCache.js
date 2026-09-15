import { studentApi } from "./studentApi";

const CACHE_TTL_MS = 30000; // 30 seconds freshness window

let dashboardCache = {
  data: null,
  timestamp: 0,
  inFlightPromise: null,
};

const listeners = new Set();

export const studentDashboardCache = {
  /**
   * Returns current in-memory cached data synchronously (or null if empty)
   */
  getCachedData() {
    return dashboardCache.data;
  },

  /**
   * Checks if cached data is within the 30-second freshness window
   */
  isFresh() {
    return (
      dashboardCache.data !== null &&
      Date.now() - dashboardCache.timestamp < CACHE_TTL_MS
    );
  },

  /**
   * Fetches dashboard data using SWR semantics:
   * - If fresh and not forced, resolves immediately with cached data
   * - If stale or cold, fetches from API, updates cache, and notifies listeners
   */
  async getDashboard({ forceRefresh = false } = {}) {
    if (!forceRefresh && this.isFresh()) {
      return dashboardCache.data;
    }

    // Reuse in-flight request if one is already active
    if (dashboardCache.inFlightPromise) {
      return dashboardCache.inFlightPromise;
    }

    dashboardCache.inFlightPromise = studentApi
      .getDashboard()
      .then((data) => {
        dashboardCache.data = data;
        dashboardCache.timestamp = Date.now();
        dashboardCache.inFlightPromise = null;
        this.notifyListeners(data);
        return data;
      })
      .catch((err) => {
        dashboardCache.inFlightPromise = null;
        throw err;
      });

    return dashboardCache.inFlightPromise;
  },

  /**
   * Invalidates cached dashboard data (e.g. after enrollment, booking, profile change)
   */
  invalidate() {
    dashboardCache.timestamp = 0;
  },

  /**
   * Clears cache completely (e.g. on logout)
   */
  clear() {
    dashboardCache.data = null;
    dashboardCache.timestamp = 0;
    dashboardCache.inFlightPromise = null;
  },

  /**
   * Subscribe to background cache updates
   */
  subscribe(listener) {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },

  notifyListeners(data) {
    listeners.forEach((fn) => {
      try {
        fn(data);
      } catch {}
    });
  },
};
