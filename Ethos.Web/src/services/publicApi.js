import { API_BASE_URL } from "../config/api";

const trainerProfileCache = new Map();

export const publicApi = {
  getPublicMedia: async (params = {}) => {
    const cleanParams = {};
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== "" && v !== "all") {
        cleanParams[k] = v;
      }
    });
    const qs = new URLSearchParams(cleanParams).toString();
    const res = await fetch(`${API_BASE_URL}/api/media/public${qs ? `?${qs}` : ""}`, {
      headers: {
        "Cache-Control": "no-cache",
        "Pragma": "no-cache",
      },
    });
    if (!res.ok) {
      throw new Error(`Public media fetch failed with status ${res.status}`);
    }
    return res.json();
  },

  getTrainerPublicProfile: async (slug) => {
    if (!slug) return null;
    const key = String(slug).toLowerCase().trim();
    if (trainerProfileCache.has(key)) {
      return trainerProfileCache.get(key);
    }
    const res = await fetch(`${API_BASE_URL}/api/trainers/${encodeURIComponent(key)}/public-profile`);
    if (!res.ok) {
      if (res.status === 404) return null;
      throw new Error(`Public trainer profile fetch failed with status ${res.status}`);
    }
    const data = await res.json();
    trainerProfileCache.set(key, data);
    if (data.slug) {
      trainerProfileCache.set(String(data.slug).toLowerCase().trim(), data);
    }
    if (data.id) {
      trainerProfileCache.set(String(data.id).toLowerCase().trim(), data);
    }
    return data;
  },

  clearTrainerProfileCache: () => {
    trainerProfileCache.clear();
  },
};

