import { API_BASE_URL } from "../config/api";

const ADMIN_TOKEN_KEY = "ethos_admin_token";
const ADMIN_USER_KEY = "ethos_admin_user";
const ADMIN_DEVICE_ID_KEY = "ethos_admin_device_id";
const ADMIN_DEVICE_CRED_KEY = "ethos_admin_device_cred";
let lastTraceId = null;

export function getAdminDeviceId() {
  let deviceId = localStorage.getItem(ADMIN_DEVICE_ID_KEY);
  if (!deviceId) {
    deviceId = crypto.randomUUID ? crypto.randomUUID() : `dev_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`;
    localStorage.setItem(ADMIN_DEVICE_ID_KEY, deviceId);
  }
  return deviceId;
}

export function getAdminDeviceCredential() {
  return localStorage.getItem(ADMIN_DEVICE_CRED_KEY);
}

export function setAdminDeviceCredential(cred) {
  if (cred) {
    localStorage.setItem(ADMIN_DEVICE_CRED_KEY, cred);
  } else {
    localStorage.removeItem(ADMIN_DEVICE_CRED_KEY);
  }
}

export function getAdminToken() {
  return localStorage.getItem(ADMIN_TOKEN_KEY);
}

export function setAdminToken(token) {
  if (token) {
    localStorage.setItem(ADMIN_TOKEN_KEY, token);
  } else {
    localStorage.removeItem(ADMIN_TOKEN_KEY);
  }
}

export function getAdminUser() {
  const raw = localStorage.getItem(ADMIN_USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

export function setAdminUser(user) {
  if (user) {
    localStorage.setItem(ADMIN_USER_KEY, JSON.stringify(user));
  } else {
    localStorage.removeItem(ADMIN_USER_KEY);
  }
}

export function clearAdminAuth() {
  localStorage.removeItem(ADMIN_TOKEN_KEY);
  localStorage.removeItem(ADMIN_USER_KEY);
}

export function isAdminAuthenticated() {
  const token = getAdminToken();
  const user = getAdminUser();
  return !!token && !!user && Array.isArray(user.roles) && user.roles.includes("ADMIN");
}

export function getLastTraceId() {
  return lastTraceId;
}

async function adminRequest(endpoint, options = {}) {
  const url = `${API_BASE_URL}${endpoint}`;
  console.log("[Admin API Request]", {
    method: options.method || "GET",
    url,
  });
  const headers = {
    "Content-Type": "application/json",
    ...(options.headers || {}),
  };

  const token = getAdminToken();
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const deviceCred = getAdminDeviceCredential();
  if (deviceCred) {
    headers["X-Admin-Device-Credential"] = deviceCred;
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  const traceHeader = response.headers.get("x-trace-id");
  if (traceHeader) {
    lastTraceId = traceHeader;
  }

  let data = null;
  const contentType = response.headers.get("content-type");
  if (contentType && contentType.includes("application/json")) {
    try {
      data = await response.json();
    } catch {
      data = null;
    }
  }

  if (!response.ok) {
    const error = new Error(
      data?.message || data?.detail || `Admin request failed (${response.status})`
    );
    error.status = response.status;
    error.code = data?.errorCode || data?.code;
    error.traceId = traceHeader;
    error.data = data;
    throw error;
  }

  return data;
}

export function clearAdminDeviceCredential() {
  localStorage.removeItem(ADMIN_DEVICE_CRED_KEY);
  localStorage.removeItem(ADMIN_DEVICE_ID_KEY);
}

export const adminApi = {
  login: (phone, password) => {
    const deviceCredential = getAdminDeviceCredential();
    return adminRequest("/api/admin/auth/login", {
      method: "POST",
      body: JSON.stringify({ phone, password, deviceCredential }),
    });
  },

  verifyMfa: (phone, otp, deviceName = null, fingerprintTelemetry = null) => {
    const deviceCredential = getAdminDeviceCredential();
    const telemetryString =
      typeof fingerprintTelemetry === "object" && fingerprintTelemetry !== null
        ? JSON.stringify(fingerprintTelemetry)
        : fingerprintTelemetry;

    return adminRequest("/api/admin/auth/verify-mfa", {
      method: "POST",
      body: JSON.stringify({
        phone,
        otp,
        deviceName,
        fingerprintTelemetry: telemetryString,
        deviceCredential,
      }),
    });
  },

  heartbeat: () =>
    adminRequest("/api/admin/sessions/heartbeat", {
      method: "POST",
    }),

  me: () => adminRequest("/api/admin/auth/me"),

  getDashboard: (range = "week") =>
    adminRequest(`/api/admin/dashboard?range=${encodeURIComponent(range)}`),

  getDailyActivity: (date) =>
    adminRequest(`/api/admin/dashboard/daily-activity?date=${encodeURIComponent(date)}`),

  getAuditLogs: (params = "") =>
    adminRequest(`/api/admin/audit-logs${params ? `?${params}` : ""}`),

  getAuditLogById: (id) => adminRequest(`/api/admin/audit-logs/${id}`),

  getSecurityEvents: (params = "") =>
    adminRequest(`/api/admin/security-events${params ? `?${params}` : ""}`),

  getSecurityEventById: (id) => adminRequest(`/api/admin/security-events/${id}`),

  getDevices: () => adminRequest("/api/admin/devices"),

  revokeDevice: (id) =>
    adminRequest(`/api/admin/devices/${id}/revoke`, {
      method: "POST",
    }),

  getSessions: () => adminRequest("/api/admin/sessions"),

  logoutSession: (id) =>
    adminRequest(`/api/admin/sessions/${id}/revoke`, {
      method: "POST",
    }),

  revokeSession: (id) =>
    adminRequest(`/api/admin/sessions/${id}/revoke`, {
      method: "POST",
    }),

  terminateSession: (phone, password, sessionId) =>
    adminRequest("/api/admin/auth/terminate-session", {
      method: "POST",
      body: JSON.stringify({ phone, password, sessionId }),
    }),

  logout: () =>
    adminRequest("/api/admin/auth/logout", {
      method: "POST",
    }),

  logoutAll: () =>
    adminRequest("/api/admin/auth/logout-all", {
      method: "POST",
    }),

  requestChangePasswordOtp: () =>
    adminRequest("/api/admin/auth/change-password/request-otp", {
      method: "POST",
    }),

  changePassword: (newPassword, otp) =>
    adminRequest("/api/admin/auth/change-password", {
      method: "POST",
      body: JSON.stringify({ newPassword, otp }),
    }),

  requestForgotPasswordOtp: (phone) =>
    adminRequest("/api/admin/auth/forgot-password/request-otp", {
      method: "POST",
      body: JSON.stringify({ phone }),
    }),

  resetForgotPassword: (phone, otp, newPassword) =>
    adminRequest("/api/admin/auth/forgot-password/reset", {
      method: "POST",
      body: JSON.stringify({ phone, otp, newPassword }),
    }),

  // Phase 18.6: Users & Accounts
  getUsers: (params = "") =>
    adminRequest(`/api/admin/users${params ? `?${params}` : ""}`),

  getUserById: (id) => adminRequest(`/api/admin/users/${id}`),

  updateUserStatus: (id, isActive, reason) =>
    adminRequest(`/api/admin/users/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive, reason }),
    }),

  getUserAuditHistory: (id) =>
    adminRequest(`/api/admin/users/${id}/audit-history`),

  // Phase 18.7: Students & Diagnostics
  getStudents: (params = "") =>
    adminRequest(`/api/admin/students${params ? `?${params}` : ""}`),

  getStudentStats: () => adminRequest("/api/admin/students/stats"),

  getStudentById: (id) => adminRequest(`/api/admin/students/${id}`),

  getStudentDiagnostics: (id) =>
    adminRequest(`/api/admin/students/${id}/diagnostics`),

  updateStudentStatus: (id, isActive, reason) =>
    adminRequest(`/api/admin/students/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive, reason }),
    }),

  // Phase 18.8: Trainers, Diagnostics & Applications
  getTrainers: (params = "") =>
    adminRequest(`/api/admin/trainers${params ? `?${params}` : ""}`),

  getTrainerStats: () => adminRequest("/api/admin/trainers/stats"),

  getTrainerById: (id) => adminRequest(`/api/admin/trainers/${id}`),

  getTrainerDiagnostics: (id) =>
    adminRequest(`/api/admin/trainers/${id}/diagnostics`),

  updateTrainerTier: (id, tierId, reason) =>
    adminRequest(`/api/admin/trainers/${id}/tier`, {
      method: "PATCH",
      body: JSON.stringify({ tierId, reason }),
    }),

  updateTrainerStatus: (id, status, reason) =>
    adminRequest(`/api/admin/trainers/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ status, reason }),
    }),

  getTrainerPermissions: (id) =>
    adminRequest(`/api/admin/trainers/${id}/permissions`),

  overrideTrainerPermission: (id, permissionCode, isAllowed, reason) =>
    adminRequest(`/api/admin/trainers/${id}/permissions/${permissionCode}`, {
      method: "PATCH",
      body: JSON.stringify({ isAllowed, clearOverride: false, reason }),
    }),

  clearTrainerPermissionOverride: (id, permissionCode, reason) =>
    adminRequest(
      `/api/admin/trainers/${id}/permissions/${permissionCode}?reason=${encodeURIComponent(reason)}`,
      {
        method: "DELETE",
      }
    ),

  getTrainerApplications: (params = "") =>
    adminRequest(`/api/admin/trainer-applications${params ? `?${params}` : ""}`),

  getTrainerApplicationById: (id) =>
    adminRequest(`/api/admin/trainer-applications/${id}`),

  approveTrainerApplication: (id, adminNotes) =>
    adminRequest(`/api/admin/trainer-applications/${id}/approve`, {
      method: "POST",
      body: JSON.stringify({ adminNotes }),
    }),

  rejectTrainerApplication: (id, rejectionReason, adminNotes) =>
    adminRequest(`/api/admin/trainer-applications/${id}/reject`, {
      method: "POST",
      body: JSON.stringify({ rejectionReason, adminNotes }),
    }),

  requestChangesTrainerApplication: (id, notes) =>
    adminRequest(`/api/admin/trainer-applications/${id}/request-changes`, {
      method: "POST",
      body: JSON.stringify({ notes }),
    }),

  getTrainerTiers: () => adminRequest("/api/admin/trainer-tiers"),

  getTrainerTierHistory: (trainerId) =>
    adminRequest(`/api/admin/trainers/${trainerId}/tier-history`),

  getTrainerUpgrades: (params = "") =>
    adminRequest(`/api/admin/trainer-upgrades${params ? `?${params}` : ""}`),

  getTrainerUpgradeById: (id) =>
    adminRequest(`/api/admin/trainer-upgrades/${id}`),

  approveTrainerUpgrade: (id, notes) =>
    adminRequest(`/api/admin/trainer-upgrades/${id}/approve`, {
      method: "POST",
      body: JSON.stringify({ notes }),
    }),

  rejectTrainerUpgrade: (id, reason) =>
    adminRequest(`/api/admin/trainer-upgrades/${id}/reject`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  updateTierPermissions: (tierId, permissions) =>
    adminRequest(`/api/admin/trainer-tiers/${tierId}/permissions`, {
      method: "PATCH",
      body: JSON.stringify({ permissions }),
    }),

  getTrainerPerformance: (trainerId) =>
    adminRequest(`/api/admin/trainers/${trainerId}/performance`),

  // Phase 18.9: Classes & Schedules
  getClasses: (params = "") =>
    adminRequest(`/api/admin/classes${params ? `?${params}` : ""}`),

  getClassById: (id) => adminRequest(`/api/admin/classes/${id}`),

  createClass: (data) =>
    adminRequest("/api/admin/classes", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updateClass: (id, data) =>
    adminRequest(`/api/admin/classes/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  updateClassStatus: (id, isActive, reason) =>
    adminRequest(`/api/admin/classes/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive, reason }),
    }),

  checkClassDependencies: (id) =>
    adminRequest(`/api/admin/classes/${id}/dependencies`),

  deleteClass: (id) =>
    adminRequest(`/api/admin/classes/${id}`, {
      method: "DELETE",
    }),

  archiveClass: (id, reason = "") =>
    adminRequest(`/api/admin/classes/${id}/archive`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  restoreClass: (id, reason = "") =>
    adminRequest(`/api/admin/classes/${id}/restore`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  getClassSchedules: (classId) =>
    adminRequest(`/api/admin/classes/${classId}/schedules`),

  getAllSchedules: () => adminRequest("/api/admin/schedules"),

  createSchedule: (classId, data) =>
    adminRequest(`/api/admin/classes/${classId}/schedules`, {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updateSchedule: (classId, scheduleId, data) =>
    adminRequest(`/api/admin/classes/${classId}/schedules/${scheduleId}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  activateSchedule: (classId, scheduleId, reason = "") =>
    adminRequest(`/api/admin/classes/${classId}/schedules/${scheduleId}/activate${reason ? `?reason=${encodeURIComponent(reason)}` : ""}`, {
      method: "PATCH",
    }),

  deactivateSchedule: (classId, scheduleId, reason = "") =>
    adminRequest(`/api/admin/classes/${classId}/schedules/${scheduleId}${reason ? `?reason=${encodeURIComponent(reason)}` : ""}`, {
      method: "DELETE",
    }),

  checkScheduleDependencies: (classId, scheduleId) =>
    adminRequest(`/api/admin/classes/${classId}/schedules/${scheduleId}/dependencies`),

  deleteSchedulePermanent: (classId, scheduleId) =>
    adminRequest(`/api/admin/classes/${classId}/schedules/${scheduleId}/permanent`, {
      method: "DELETE",
    }),

  // Phase 18.9: Workshops Management & Review
  getWorkshops: (params = "") =>
    adminRequest(`/api/admin/workshops${params ? `?${params}` : ""}`),

  createWorkshop: (payload) =>
    adminRequest("/api/admin/workshops", {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  updateWorkshop: (id, payload) =>
    adminRequest(`/api/admin/workshops/${id}`, {
      method: "PUT",
      body: JSON.stringify(payload),
    }),

  getPendingWorkshops: () => adminRequest("/api/admin/workshops/pending"),

  getWorkshopById: (id) => adminRequest(`/api/admin/workshops/${id}`),

  getWorkshopPricingTiers: (id) => adminRequest(`/api/admin/workshops/${id}/pricing-tiers`),

  updateWorkshopPricingTiers: (id, tiers, reason = "") =>
    adminRequest(`/api/admin/workshops/${id}/pricing-tiers`, {
      method: "PUT",
      body: JSON.stringify({ tiers, reason }),
    }),

  approveWorkshopPrice: (id, approvedPrice) =>
    adminRequest(`/api/admin/workshops/${id}/approve-price`, {
      method: "POST",
      body: JSON.stringify({ approvedPrice }),
    }),

  approveWorkshop: (id, approvedPrice) =>
    adminRequest(`/api/admin/workshops/${id}/approve`, {
      method: "POST",
      body: JSON.stringify({ approvedPrice }),
    }),

  rejectWorkshop: (id, reason) =>
    adminRequest(`/api/admin/workshops/${id}/reject`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  cancelWorkshop: (id, reason) =>
    adminRequest(`/api/admin/workshops/${id}/cancel`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  completeWorkshop: (id) =>
    adminRequest(`/api/admin/workshops/${id}/complete`, {
      method: "POST",
    }),

  publishWorkshop: (id) =>
    adminRequest(`/api/admin/workshops/${id}/publish`, {
      method: "POST",
    }),

  unpublishWorkshop: (id) =>
    adminRequest(`/api/admin/workshops/${id}/unpublish`, {
      method: "POST",
    }),

  archiveWorkshop: (id) =>
    adminRequest(`/api/admin/workshops/${id}/archive`, {
      method: "POST",
    }),

  uploadMedia: (formData) => {
    const token = getAdminToken();
    return fetch(`${API_BASE_URL}/api/admin/media/upload`, {
      method: "POST",
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: formData,
    }).then(async (res) => {
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || `Upload failed (${res.status})`);
      }
      return res.json();
    });
  },

  getAdminMedia: (params = "") =>
    adminRequest(`/api/admin/media${params ? `?${params}` : ""}`),

  deleteAdminMedia: (id) =>
    adminRequest(`/api/admin/media/${id}`, {
      method: "DELETE",
    }),

  getWorkshopRegistrations: (id, page = 1, pageSize = 50) =>
    adminRequest(`/api/admin/workshops/${id}/registrations?page=${page}&pageSize=${pageSize}`),

  generateWorkshopFeedbackToken: (bookingId) =>
    adminRequest(`/api/admin/workshops/bookings/${bookingId}/feedback-token`, {
      method: "POST",
    }),

  getWorkshopTickets: (id) =>
    adminRequest(`/api/admin/workshops/${id}/tickets`),

  overrideWorkshopTicket: (workshopId, ticketId, reason) =>
    adminRequest(`/api/admin/workshops/${workshopId}/tickets/${ticketId}/override`, {
      method: "POST",
      body: { reason },
    }),

  undoWorkshopTicketCheckIn: (workshopId, ticketId, reason) =>
    adminRequest(`/api/admin/workshops/${workshopId}/tickets/${ticketId}/undo-check-in`, {
      method: "POST",
      body: { reason },
    }),

  exportWorkshopAttendance: (workshopId) =>
    adminRequest(`/api/admin/workshops/${workshopId}/attendance/export`),

  // Phase 18.10b: Feedback & Reviews
  getFeedback: (params = "") =>
    adminRequest(`/api/admin/feedback${params ? `?${params}` : ""}`),

  getFeedbackById: (id) => adminRequest(`/api/admin/feedback/${id}`),

  // Phase 18.10: Bookings Ledger
  getClassEnrollments: (params = "") =>
    adminRequest(`/api/admin/bookings/classes${params ? `?${params}` : ""}`),

  getWorkshopBookings: (params = "") =>
    adminRequest(`/api/admin/bookings/workshops${params ? `?${params}` : ""}`),

  cancelClassEnrollment: (enrollmentId, reason) =>
    adminRequest(`/api/admin/bookings/classes/${enrollmentId}/cancel`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  cancelWorkshopBooking: (bookingId, reason) =>
    adminRequest(`/api/admin/bookings/workshops/${bookingId}/cancel`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  manualClassEnrollment: (data) =>
    adminRequest("/api/admin/bookings/classes/manual", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  // Phase 18.10: Attendance
  getAttendanceSessions: (params = "") =>
    adminRequest(`/api/admin/attendance/sessions${params ? `?${params}` : ""}`),

  getSessionRoster: (sessionId) =>
    adminRequest(`/api/admin/attendance/sessions/${sessionId}/roster`),

  markSessionAttendance: (sessionId, records) =>
    adminRequest(`/api/admin/attendance/sessions/${sessionId}/mark`, {
      method: "POST",
      body: JSON.stringify({ records }),
    }),

  markWorkshopAttendance: (workshopId, studentProfileId, status, bookingId = null) =>
    adminRequest(`/api/admin/attendance/workshops/${workshopId}/mark`, {
      method: "POST",
      body: JSON.stringify({ studentProfileId, status, bookingId }),
    }),

  // Phase 18.11: Finance, Reconciliation, Receipts & Payouts
  getPayments: (params = "") =>
    adminRequest(`/api/admin/payments${params ? `?${params}` : ""}`),

  getPaymentById: (id) => adminRequest(`/api/admin/payments/${id}`),

  getRevenue: () => adminRequest("/api/admin/revenue"),

  recordRefund: (transactionId, refundAmount, reason, gatewayRefundId, notes, idempotencyKey = null) => {
    const headers = {};
    if (idempotencyKey) headers["Idempotency-Key"] = idempotencyKey;
    return adminRequest(`/api/admin/payments/${transactionId}/refund`, {
      method: "POST",
      headers,
      body: JSON.stringify({ refundAmount, reason, gatewayRefundId, notes }),
    });
  },


  resolvePaymentIssue: (transactionId, payload) =>
    adminRequest(`/api/admin/payments/${transactionId}/resolve`, {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  getPaymentTimeline: (transactionId) =>
    adminRequest(`/api/admin/payments/${transactionId}/timeline`),

  getPaymentReceipt: (transactionId) =>
    adminRequest(`/api/admin/payments/${transactionId}/receipt`),

  getTrainerPayouts: () => adminRequest("/api/admin/finance/trainer-payouts"),

  processTrainerPayout: (trainerId, amount, payoutReference, notes = null) =>
    adminRequest(`/api/admin/finance/trainer-payouts/${trainerId}/process`, {
      method: "POST",
      body: JSON.stringify({ amount, payoutReference, notes }),
    }),

  // Phase 18.12: Observability & Telemetry Engine
  getObservabilityLogs: (params = "") =>
    adminRequest(`/api/admin/observability/logs${params ? `?${params}` : ""}`),

  getTraceDeepDive: (traceId) =>
    adminRequest(`/api/admin/observability/traces/${traceId}`),

  getObservabilityMetrics: () =>
    adminRequest("/api/admin/observability/metrics"),

  getDeepHealthCheck: () =>
    adminRequest("/api/admin/observability/health"),

  getUserTechnicalTimeline: (userId) =>
    adminRequest(`/api/admin/observability/users/${userId}/timeline`),

  // Phase 18.13: Security Center & Threat Monitoring
  getSecurityFleet: () =>
    adminRequest("/api/admin/security/fleet"),

  getSecurityThreats: () =>
    adminRequest("/api/admin/security/threats"),

  investigateSecurityEntity: (targetType, targetValue) =>
    adminRequest(`/api/admin/security/investigate?targetType=${encodeURIComponent(targetType)}&targetValue=${encodeURIComponent(targetValue)}`),

  revokeSecuritySession: (id, reason) =>
    adminRequest(`/api/admin/security/sessions/${id}/revoke`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  revokeSecurityDevice: (id, reason) =>
    adminRequest(`/api/admin/security/devices/${id}/revoke`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    }),

  // Phase 18.14: Incident Center & Outage Management
  getIncidents: (params = "") =>
    adminRequest(`/api/admin/incidents${params ? `?${params}` : ""}`),

  getIncidentById: (id) =>
    adminRequest(`/api/admin/incidents/${id}`),

  createIncident: (data) =>
    adminRequest("/api/admin/incidents", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  createIncidentFromTrace: (data) =>
    adminRequest("/api/admin/incidents/from-trace", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  createIncidentFromSecurityEvent: (data) =>
    adminRequest("/api/admin/incidents/from-security-event", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updateIncidentStatus: (id, data) =>
    adminRequest(`/api/admin/incidents/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify(data),
    }),

  addIncidentUpdate: (id, data) =>
    adminRequest(`/api/admin/incidents/${id}/updates`, {
      method: "POST",
      body: JSON.stringify(data),
    }),

  assignIncident: (id, assignedAdminId) =>
    adminRequest(`/api/admin/incidents/${id}/assign`, {
      method: "PUT",
      body: JSON.stringify({ assignedAdminId }),
    }),

  getIncidentMetrics: () =>
    adminRequest("/api/admin/incidents/metrics"),

  // Phase 18.15: Corrective Action Engine
  getCorrectiveActions: (params = "") =>
    adminRequest(`/api/admin/corrective-actions${params ? `?${params}` : ""}`),

  getCorrectiveActionById: (id) =>
    adminRequest(`/api/admin/corrective-actions/${id}`),

  simulateCorrectiveAction: (data) =>
    adminRequest("/api/admin/corrective-actions/simulate", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  executeCorrectiveAction: (data, idempotencyKey) =>
    adminRequest("/api/admin/corrective-actions/execute", {
      method: "POST",
      headers: idempotencyKey ? { "Idempotency-Key": idempotencyKey } : {},
      body: JSON.stringify(data),
    }),

  getCorrectiveActionMetrics: () =>
    adminRequest("/api/admin/corrective-actions/metrics"),

  // Phase 18.16: Communications & MSG91 Engine
  getCommunicationLogs: (params = "") =>
    adminRequest(`/api/admin/communications/logs${params ? `?${params}` : ""}`),

  getCommunicationLogById: (id) =>
    adminRequest(`/api/admin/communications/logs/${id}`),

  sendCommunication: (data, idempotencyKey) =>
    adminRequest("/api/admin/communications/send", {
      method: "POST",
      headers: idempotencyKey ? { "Idempotency-Key": idempotencyKey } : {},
      body: JSON.stringify(data),
    }),

  retryCommunication: (data, idempotencyKey) =>
    adminRequest("/api/admin/communications/retry", {
      method: "POST",
      headers: idempotencyKey ? { "Idempotency-Key": idempotencyKey } : {},
      body: JSON.stringify(data),
    }),

  getCommunicationTemplates: () =>
    adminRequest("/api/admin/communications/templates"),

  getCommunicationMetrics: () =>
    adminRequest("/api/admin/communications/metrics"),

  // Packages Management
  getPackages: (params = "") =>
    adminRequest(`/api/admin/packages${params ? `?${params}` : ""}`),

  getPackageById: (id) =>
    adminRequest(`/api/admin/packages/${id}`),

  createPackage: (data) =>
    adminRequest("/api/admin/packages", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updatePackage: (id, data) =>
    adminRequest(`/api/admin/packages/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  updatePackageStatus: (id, isActive, reason) =>
    adminRequest(`/api/admin/packages/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive, reason }),
    }),

  getPackageStats: () =>
    adminRequest("/api/admin/packages/stats"),

  checkPackageDependencies: (id) =>
    adminRequest(`/api/admin/packages/${id}/dependencies`),

  getPackageDetails: (id) =>
    adminRequest(`/api/admin/packages/${id}/details`),

  getPackageActivity: (id, page = 1, pageSize = 20) =>
    adminRequest(`/api/admin/packages/${id}/activity?page=${page}&pageSize=${pageSize}`),

  deletePackage: (id) =>
    adminRequest(`/api/admin/packages/${id}`, {
      method: "DELETE",
    }),

  // Authorized Devices & Fleet Management
  getDevices: () =>
    adminRequest("/api/admin/devices"),

  revokeDevice: (id) =>
    adminRequest(`/api/admin/devices/${id}/revoke`, {
      method: "POST",
    }),

  getSessions: () =>
    adminRequest("/api/admin/sessions"),

  revokeSession: (id) =>
    adminRequest(`/api/admin/sessions/${id}/revoke`, {
      method: "POST",
    }),
};

export const getDailyActivity = adminApi.getDailyActivity;

