import { apiClient } from "./apiClient";
import { getMediaUrl } from "../utils/mediaUrl";

export const trainerApi = {
  /* =========================
     TRAINER CORE
  ========================= */

  getProfile() {
    return apiClient.get("/api/trainers/me");
  },

  updateProfile(payload) {
    return apiClient.put("/api/trainers/me", payload);
  },

  uploadProfilePhoto(formData) {
    return apiClient.post(
      "/api/trainers/me/profile-photo",
      formData
    );
  },

  getDashboard() {
    return apiClient.get("/api/trainers/me/dashboard");
  },

  getTiers() {
    return apiClient.get("/api/trainers/tiers");
  },

  getTier() {
    return apiClient.get("/api/trainers/me/tier");
  },

  getTierHistory() {
    return apiClient.get("/api/trainers/me/tier-history");
  },

  getPermissions() {
    return apiClient.get("/api/trainers/me/permissions");
  },

  /* =========================
     APPLICATION
  ========================= */

  getApplication() {
    return apiClient.get("/api/trainers/me/application");
  },

  createApplication(payload) {
    return apiClient.post(
      "/api/trainers/me/application",
      payload
    );
  },

  updateApplication(payload) {
    return apiClient.put(
      "/api/trainers/me/application",
      payload
    );
  },

  getDocuments() {
    return Promise.resolve([]);
  },

  submitApplication() {
    return apiClient.post(
      "/api/trainers/me/application/submit",
      {}
    );
  },

  getApplicationVideo() {
    return apiClient.get("/api/trainers/me/application/video");
  },

  uploadApplicationVideo(formData) {
    return apiClient.post(
      "/api/trainers/me/application/video",
      formData
    );
  },

  deleteApplicationVideo() {
    return apiClient.delete(
      "/api/trainers/me/application/video"
    );
  },

  /* =========================
     AVAILABILITY
  ========================= */

  getAvailability() {
    return apiClient.get("/api/trainers/me/availability");
  },

  updateAvailability(payload) {
    return apiClient.put(
      "/api/trainers/me/availability",
      payload
    );
  },

  /* =========================
     WORKSHOPS
  ========================= */

  getWorkshops() {
    return apiClient.get("/api/trainers/me/workshops");
  },

  getWorkshop(id) {
    return apiClient.get(`/api/trainers/me/workshops/${id}`);
  },

  createWorkshop(payload) {
    return apiClient.post("/api/trainers/me/workshops", payload);
  },

  updateWorkshop(id, payload) {
    return apiClient.put(`/api/trainers/me/workshops/${id}`, payload);
  },

  submitWorkshop(id) {
    return apiClient.post(`/api/trainers/me/workshops/${id}/submit`, {});
  },

  cancelWorkshop(id) {
    return apiClient.post(`/api/trainers/me/workshops/${id}/cancel`, {});
  },

  getWorkshopStudents(id) {
    return apiClient.get(`/api/trainers/me/workshops/${id}/students`);
  },

  getWorkshopFeedback(id) {
    return apiClient.get(`/api/trainers/me/workshops/${id}/feedback`);
  },

  validateWorkshopTicket(workshopId, tokenOrNumber) {
    return apiClient.post(`/api/trainers/me/workshops/${workshopId}/tickets/validate`, { tokenOrNumber });
  },

  checkInWorkshopTicket(workshopId, ticketId, payload = { method: 0 }) {
    return apiClient.post(`/api/trainers/me/workshops/${workshopId}/tickets/${ticketId}/check-in`, payload);
  },

  groupCheckInWorkshop(workshopId, bookingId) {
    return apiClient.post(`/api/trainers/me/workshops/${workshopId}/bookings/${bookingId}/group-check-in`, {});
  },

  getWorkshopAttendance(workshopId) {
    return apiClient.get(`/api/trainers/me/workshops/${workshopId}/attendance`);
  },

  recordWorkshopReEntry(workshopId, ticketId, eventType) {
    return apiClient.post(`/api/trainers/me/workshops/${workshopId}/tickets/${ticketId}/re-entry?eventType=${eventType}`, {});
  },

  /* =========================
     PERFORMANCE
  ========================= */

  getPerformance() {
    return apiClient.get(
      "/api/trainers/me/performance"
    );
  },

  getPerformanceHistory() {
    return apiClient.get(
      "/api/trainers/me/performance/history"
    );
  },

  /* =========================
     TIER UPGRADE
  ========================= */

  getUpgradeRequests() {
    return apiClient.get("/api/trainers/me/upgrade-requests");
  },

  requestUpgrade(payload) {
    return apiClient.post(
      "/api/trainers/me/upgrade-requests",
      payload
    );
  },

  cancelUpgradeRequest(id) {
    return apiClient.delete(
      `/api/trainers/me/upgrade-requests/${id}`
    );
  },

  /* =========================
     NOTIFICATIONS
  ========================= */

  getNotifications() {
    return apiClient.get("/api/notifications/me");
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
    return apiClient.delete(`/api/trainers/me/notifications/${id}`);
  },

  /* =========================
     GALLERY
  ========================= */

  getGallery() {
    return apiClient.get("/api/trainers/me/gallery");
  },

  uploadGalleryImage(formData) {
    return apiClient.post("/api/trainers/me/gallery", formData);
  },

  deleteGalleryImage(imageId) {
    return apiClient.delete(`/api/trainers/me/gallery/${imageId}`);
  },

  getGalleryImageUrl(imageId) {
    return getMediaUrl(`/api/trainers/me/gallery/${imageId}/image`);
  },
};
