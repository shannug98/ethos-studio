/**
 * Trainer State Sync Event Bus
 * Synchronizes photo, profile, and unread notification state across
 * TrainerSidebar, TrainerTopbar, and individual trainer pages without reload.
 */

const photoListeners = new Set();
const profileListeners = new Set();
const unreadListeners = new Set();

export const trainerStateSync = {
  emitPhotoUpdated(url) {
    photoListeners.forEach((listener) => {
      try {
        listener(url);
      } catch (err) {
        console.error("Error in trainer photo listener:", err);
      }
    });
  },

  onPhotoUpdated(listener) {
    photoListeners.add(listener);
    return () => photoListeners.delete(listener);
  },

  emitProfileUpdated(profile) {
    profileListeners.forEach((listener) => {
      try {
        listener(profile);
      } catch (err) {
        console.error("Error in trainer profile listener:", err);
      }
    });
  },

  onProfileUpdated(listener) {
    profileListeners.add(listener);
    return () => profileListeners.delete(listener);
  },

  emitUnreadCount(count) {
    const validCount = Math.max(0, Number(count) || 0);
    unreadListeners.forEach((listener) => {
      try {
        listener(validCount);
      } catch (err) {
        console.error("Error in trainer unread listener:", err);
      }
    });
  },

  onUnreadCount(listener) {
    unreadListeners.add(listener);
    return () => unreadListeners.delete(listener);
  },
};
