import { apiClient } from "./apiClient";

export const studentApi = {
  getDashboard() {
    return apiClient.get("/api/students/me/dashboard");
  },

  getProfile() {
    return apiClient.get("/api/students/me");
  },

  updateProfile(payload) {
    return apiClient.put("/api/students/me", payload);
  },

  uploadProfilePhoto(formData) {
    return apiClient.post("/api/students/me/profile-photo", formData);
  },

  getProfilePhotoBlob() {
    return apiClient.getBlob("/api/students/me/profile-photo");
  },

  getPackages() {
    return apiClient.get("/api/students/me/packages");
  },

  getActivePackage() {
    return apiClient.get("/api/students/me/packages/active");
  },

  getClasses() {
    return apiClient.get("/api/students/me/classes");
  },

  getSchedule() {
    return apiClient.get("/api/students/me/schedule");
  },

  getEnrollments() {
    return apiClient.get("/api/students/me/enrollments");
  },

  enrollInClass(danceClassId) {
    return apiClient.post("/api/students/me/enrollments", { danceClassId });
  },

  cancelEnrollment(enrollmentId) {
    return apiClient.delete(`/api/students/me/enrollments/${enrollmentId}`);
  },

  /* =========================
     NOTIFICATIONS
  ========================= */
  getNotifications(type) {
    const query = type ? `?type=${encodeURIComponent(type)}` : "";
    return apiClient.get(`/api/notifications/me${query}`);
  },

  getUnreadNotificationCount() {
    return apiClient.get("/api/notifications/me/unread-count");
  },

  markNotificationRead(id) {
    return apiClient.patch(`/api/notifications/me/${id}/read`);
  },

  markAllNotificationsRead() {
    return apiClient.patch("/api/notifications/me/read-all");
  },

  deleteNotification(id) {
    return apiClient.delete(`/api/notifications/me/${id}`);
  },
};
