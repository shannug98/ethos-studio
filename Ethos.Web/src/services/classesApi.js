import { apiClient } from "./apiClient";

export const classesApi = {
  getActiveClasses() {
    return apiClient.get("/api/classes");
  },

  getClassById(id) {
    return apiClient.get(`/api/classes/${id}`);
  },

  getClassSchedules(id) {
    return apiClient.get(`/api/classes/${id}/schedule`);
  },

  getAllSchedules() {
    return apiClient.get("/api/schedules");
  },
};
