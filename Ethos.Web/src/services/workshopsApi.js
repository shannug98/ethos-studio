import { apiClient } from "./apiClient";

export const workshopsApi = {
  getAll() {
    return apiClient.get("/api/workshops");
  },

  getApprovedWorkshops() {
    return apiClient.get("/api/workshops");
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
    return apiClient.post(`/api/workshops/${workshopId}/order`, payload);
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
