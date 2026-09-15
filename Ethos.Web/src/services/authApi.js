import { apiClient } from "./apiClient";

export const authApi = {
  requestOtp: (phone, purpose = null) =>
    apiClient.post("/api/auth/request-otp", {
      phone,
      ...(purpose ? { purpose } : {}),
    }),

  login: (phone, password) =>
    apiClient.post("/api/auth/login", {
      phone,
      password,
    }),

  verifyOtp: (phone, otp, purpose = null) =>
    apiClient.post("/api/auth/verify-otp", {
      phone,
      otp,
      ...(purpose ? { purpose } : {}),
    }),

  changePassword: (currentPassword, newPassword, confirmNewPassword) =>
    apiClient.post("/api/auth/change-password", {
      currentPassword,
      newPassword,
      confirmNewPassword,
    }),

  me: () =>
    apiClient.get("/api/auth/me"),
};
