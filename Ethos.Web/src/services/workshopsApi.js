import { apiClient } from "./apiClient";
import { API_BASE_URL } from "../config/api";

export const workshopsApi = {
  getAll() {
    return apiClient.get("/api/workshops");
  },

  async getApprovedWorkshops() {
    try {
      const data = await apiClient.get("/api/workshops");
      if (Array.isArray(data)) return data;
      if (Array.isArray(data?.items)) return data.items;
      return [];
    } catch (err) {
      console.warn("workshopsApi: apiClient error, trying direct API fallback:", err);
      try {
        const res = await fetch(`${API_BASE_URL}/api/workshops`);
        if (res.ok) {
          const fallbackData = await res.json();
          if (Array.isArray(fallbackData)) return fallbackData;
          if (Array.isArray(fallbackData?.items)) return fallbackData.items;
        }
      } catch (fallbackErr) {
        console.error("workshopsApi: direct fallback fetch failed:", fallbackErr);
      }
      throw err;
    }
  },

  getWorkshopById(id) {
    return apiClient.get(`/api/workshops/${id}`);
  },

  getWorkshopPricing(id) {
    return apiClient.get(`/api/workshops/${id}/pricing`);
  },

  getWorkshopQuote(id, quantity = 1) {
    return apiClient.get(`/api/workshops/${id}/quote?quantity=${quantity}`);
  },

  createWorkshopOrder(workshopId, payload = { quantity: 1 }) {
    const data = typeof payload === "object" && payload !== null ? { ...payload } : { quantity: payload || 1 };
    if (!data.idempotencyKey) {
      data.idempotencyKey = crypto.randomUUID();
    }
    return apiClient.post(`/api/workshops/${workshopId}/order`, data);
  },

  verifyWorkshopPayment(workshopId, payload) {
    return apiClient.post(`/api/workshops/${workshopId}/verify-payment`, payload);
  },

  getMyWorkshops() {
    return apiClient.get("/api/students/me/workshops");
  },

  getBookingTickets(bookingId) {
    return apiClient.get(`/api/students/bookings/${bookingId}/tickets`);
  },

  updateTicketAttendee(bookingId, ticketId, payload) {
    return apiClient.put(`/api/students/bookings/${bookingId}/tickets/${ticketId}/attendee`, payload);
  },

  resendTicketPass(bookingId, ticketId) {
    return apiClient.post(`/api/students/bookings/${bookingId}/tickets/${ticketId}/resend`, {});
  },

  getTicketPass(ticketId) {
    return apiClient.get(`/api/workshops/tickets/${ticketId}/pass`);
  },
};
