import { apiClient } from "./apiClient";

export const packagesApi = {
  getActivePackages() {
    return apiClient.get("/api/packages");
  },

  getPackageById(id) {
    return apiClient.get(`/api/packages/${id}`);
  },
};
