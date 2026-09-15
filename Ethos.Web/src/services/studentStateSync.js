import { studentDashboardCache } from "./studentDashboardCache";
import { studentApi } from "./studentApi";

const unreadListeners = new Set();
const profileListeners = new Set();
const bookingListeners = new Set();
const photoListeners = new Set();
const lightboxListeners = new Set();

let canonicalPhotoBlobUrl = null;
let inFlightPhotoPromise = null;
let hasCheckedInitialPhoto = false;

let lightboxState = {
  isOpen: false,
  photoUrl: null,
  name: "Student",
};

export const studentStateSync = {
  /**
   * Broadcast updated unread notification count
   */
  emitUnreadCount(count) {
    const validCount = Math.max(0, Number(count) || 0);

    // Update in-memory dashboard cache if present
    const cached = studentDashboardCache.getCachedData();
    if (cached) {
      cached.unreadNotifications = validCount;
    }

    unreadListeners.forEach((listener) => {
      try {
        listener(validCount);
      } catch (err) {
        console.error("Error in unread listener:", err);
      }
    });
  },

  /**
   * Subscribe to unread count changes
   */
  onUnreadCount(listener) {
    unreadListeners.add(listener);
    return () => unreadListeners.delete(listener);
  },

  /**
   * Broadcast updated student profile data
   */
  emitProfileUpdated(profile) {
    if (!profile) return;

    // Invalidate dashboard cache so next fetch gets latest profile
    studentDashboardCache.invalidate();

    profileListeners.forEach((listener) => {
      try {
        listener(profile);
      } catch (err) {
        console.error("Error in profile listener:", err);
      }
    });
  },

  /**
   * Subscribe to student profile updates
   */
  onProfileUpdated(listener) {
    profileListeners.add(listener);
    return () => profileListeners.delete(listener);
  },

  /**
   * Broadcast workshop booking completion
   */
  emitBookingCompleted(booking) {
    studentDashboardCache.invalidate();

    bookingListeners.forEach((listener) => {
      try {
        listener(booking);
      } catch (err) {
        console.error("Error in booking listener:", err);
      }
    });
  },

  /**
   * Subscribe to workshop booking completions
   */
  onBookingCompleted(listener) {
    bookingListeners.add(listener);
    return () => bookingListeners.delete(listener);
  },

  /**
   * Get current canonical photo blob URL
   */
  getPhotoUrl() {
    return canonicalPhotoBlobUrl;
  },

  /**
   * Check whether initial photo check has completed
   */
  hasCheckedPhoto() {
    return hasCheckedInitialPhoto;
  },

  /**
   * Set canonical photo blob URL directly (with object URL revocation of old URL)
   */
  setPhotoUrl(url) {
    if (
      canonicalPhotoBlobUrl &&
      canonicalPhotoBlobUrl !== url &&
      canonicalPhotoBlobUrl.startsWith("blob:")
    ) {
      try {
        URL.revokeObjectURL(canonicalPhotoBlobUrl);
      } catch {}
    }
    canonicalPhotoBlobUrl = url;
    hasCheckedInitialPhoto = true;

    photoListeners.forEach((listener) => {
      try {
        listener(url);
      } catch (err) {
        console.error("Error in photo listener:", err);
      }
    });
  },

  /**
   * Load canonical photo from authenticated backend endpoint with in-flight deduplication
   */
  async loadCanonicalPhoto(forceRefresh = false) {
    // If not forcing refresh and we already have a loaded canonical blob, return it
    if (!forceRefresh && canonicalPhotoBlobUrl) {
      return canonicalPhotoBlobUrl;
    }

    // If an in-flight request is already running and not forcing refresh, reuse it
    if (inFlightPhotoPromise && !forceRefresh) {
      return inFlightPhotoPromise;
    }

    inFlightPhotoPromise = (async () => {
      try {
        const blob = await studentApi.getProfilePhotoBlob();
        hasCheckedInitialPhoto = true;

        if (blob && blob.size > 0) {
          const newUrl = URL.createObjectURL(blob);
          if (
            canonicalPhotoBlobUrl &&
            canonicalPhotoBlobUrl !== newUrl &&
            canonicalPhotoBlobUrl.startsWith("blob:")
          ) {
            try {
              URL.revokeObjectURL(canonicalPhotoBlobUrl);
            } catch {}
          }
          canonicalPhotoBlobUrl = newUrl;

          photoListeners.forEach((listener) => {
            try {
              listener(newUrl);
            } catch (err) {
              console.error("Error in photo listener:", err);
            }
          });

          return newUrl;
        } else {
          if (
            canonicalPhotoBlobUrl &&
            canonicalPhotoBlobUrl.startsWith("blob:")
          ) {
            try {
              URL.revokeObjectURL(canonicalPhotoBlobUrl);
            } catch {}
          }
          canonicalPhotoBlobUrl = null;

          photoListeners.forEach((listener) => {
            try {
              listener(null);
            } catch (err) {
              console.error("Error in photo listener:", err);
            }
          });

          return null;
        }
      } catch (err) {
        console.error("Error loading canonical student photo:", err);
        return canonicalPhotoBlobUrl;
      } finally {
        inFlightPhotoPromise = null;
      }
    })();

    return inFlightPhotoPromise;
  },

  /**
   * Subscribe to canonical photo updates
   */
  onPhotoUpdated(listener) {
    photoListeners.add(listener);
    if (canonicalPhotoBlobUrl !== null || hasCheckedInitialPhoto) {
      try {
        listener(canonicalPhotoBlobUrl);
      } catch {}
    }
    return () => photoListeners.delete(listener);
  },

  /**
   * Clear canonical photo and revoke blob URLs on logout
   */
  clearPhoto() {
    if (
      canonicalPhotoBlobUrl &&
      canonicalPhotoBlobUrl.startsWith("blob:")
    ) {
      try {
        URL.revokeObjectURL(canonicalPhotoBlobUrl);
      } catch {}
    }
    canonicalPhotoBlobUrl = null;
    inFlightPhotoPromise = null;
    hasCheckedInitialPhoto = false;
    lightboxState = { isOpen: false, photoUrl: null, name: "Student" };

    photoListeners.forEach((listener) => {
      try {
        listener(null);
      } catch {}
    });
  },

  /**
   * Lightbox viewer controls
   */
  openPhotoViewer(photoUrl, name = "Student") {
    const url = photoUrl || canonicalPhotoBlobUrl;
    if (!url) return;
    lightboxState = { isOpen: true, photoUrl: url, name };
    lightboxListeners.forEach((fn) => {
      try {
        fn(lightboxState);
      } catch {}
    });
  },

  closePhotoViewer() {
    lightboxState = { isOpen: false, photoUrl: null, name: "Student" };
    lightboxListeners.forEach((fn) => {
      try {
        fn(lightboxState);
      } catch {}
    });
  },

  onPhotoViewerChange(listener) {
    lightboxListeners.add(listener);
    try {
      listener(lightboxState);
    } catch {}
    return () => lightboxListeners.delete(listener);
  },
};
