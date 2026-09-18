import { API_BASE_URL } from "../config/api";

export const publicApi = {
  getPublicMedia: async (params = {}) => {
    const cleanParams = {};
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== "" && v !== "all") {
        cleanParams[k] = v;
      }
    });
    const qs = new URLSearchParams(cleanParams).toString();
    const res = await fetch(`${API_BASE_URL}/api/media/public${qs ? `?${qs}` : ""}`);
    if (!res.ok) {
      throw new Error(`Public media fetch failed with status ${res.status}`);
    }
    return res.json();
  },
};
