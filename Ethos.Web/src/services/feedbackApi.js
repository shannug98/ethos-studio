import { apiClient } from "./apiClient";

export const feedbackApi = {
  getOverview() {
    return apiClient.get("/api/students/me/feedback/overview");
  },

  getMyFeedback() {
    return apiClient.get("/api/students/me/feedback");
  },

  getPendingFeedback() {
    return apiClient.get("/api/students/me/feedback/pending");
  },

  submitClassFeedback(classId, payload) {
    return apiClient.post(`/api/students/me/feedback/classes/${classId}`, payload);
  },

  submitWorkshopFeedback(workshopId, payload) {
    return apiClient.post(`/api/students/me/feedback/workshops/${workshopId}`, payload);
  },
};
