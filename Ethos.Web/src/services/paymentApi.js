import { apiClient } from "./apiClient";

export const paymentApi = {
  createOrder(payload) {
    return apiClient.post("/api/payments/orders", payload);
  },

  verifyPayment(payload) {
    return apiClient.post("/api/payments/verify", payload);
  },

  getPayment(id) {
    return apiClient.get(`/api/payments/${id}`);
  },

  createPublicPackageOrder(payload) {
    return apiClient.post("/api/payments/public/package-order", payload);
  },

  verifyPublicPackagePayment(payload) {
    return apiClient.post("/api/payments/public/verify-package-payment", payload);
  },
};

