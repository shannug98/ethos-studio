/**
 * ETHOS DANCE STUDIO — SINGLE AUTHORITATIVE ADMIN ROUTE & MODULE REGISTRY
 *
 * This file is the single source of truth for all Admin Portal routes and modules.
 * It drives:
 * 1. Route declarations in App.jsx
 * 2. Navigation items in AdminSidebar.jsx
 * 3. Quick launcher commands in AdminCommandPalette.jsx (Ctrl+K)
 * 4. Route protection & RBAC verification tests
 * 5. Navigation coverage tests (ensuring no route exists without registry entry)
 */

export const ADMIN_NAV_SECTIONS = [
  "COMMAND",
  "OPERATIONS",
  "SYSTEM HEALTH & MONITORING",
  "ACTIONS & NOTIFICATIONS",
  "SECURITY & DEVICES",
];

export const ADMIN_MODULE_REGISTRY = [
  // 1. Central Command Dashboard
  {
    id: "dashboard",
    path: "/admin_portal/dashboard",
    relativeRoute: "dashboard",
    label: "Dashboard",
    description: "Central command overview, real health diagnostics, and quick actions",
    section: "COMMAND",
    icon: "📊",
    requiredPermission: null, // Basic ADMIN role
    isNavigable: true,
    componentName: "AdminDashboard",
  },

  // 2. User & Account Control
  {
    id: "users",
    path: "/admin_portal/users",
    relativeRoute: "users",
    label: "Users & Accounts",
    description: "Platform user registry, role governance, and status mutations",
    section: "OPERATIONS",
    icon: "🔑",
    requiredPermission: "ADMIN_USER_VIEW",
    isNavigable: true,
    componentName: "AdminUsers",
  },

  // 3. Student Directory
  {
    id: "students",
    path: "/admin_portal/students",
    relativeRoute: "students",
    label: "Students",
    description: "Student profiles, onboarding progress, and diagnostic health",
    section: "OPERATIONS",
    icon: "👥",
    requiredPermission: "STUDENT_VIEW",
    isNavigable: true,
    componentName: "AdminStudents",
  },

  // 4. Student Dossier (Parameterized subroute)
  {
    id: "student-dossier",
    path: "/admin_portal/students/:studentId",
    relativeRoute: "students/:studentId",
    label: "Student Dossier 360",
    description: "Deep dive student dossier, package quotas, bookings, and diagnostic telemetry",
    section: "OPERATIONS",
    icon: "👤",
    requiredPermission: "STUDENT_VIEW",
    isNavigable: false,
    componentName: "AdminStudentDossier",
  },

  // 5. Trainer Directory & Review Queue
  {
    id: "trainers",
    path: "/admin_portal/trainers",
    relativeRoute: "trainers",
    label: "Trainers",
    description: "Trainer registry, application approvals, tiers, and compensation",
    section: "OPERATIONS",
    icon: "👤",
    requiredPermission: "TRAINER_VIEW",
    isNavigable: true,
    componentName: "AdminTrainers",
  },

  // 6. Trainer Dossier (Parameterized subroute)
  {
    id: "trainer-dossier",
    path: "/admin_portal/trainers/:trainerId",
    relativeRoute: "trainers/:trainerId",
    label: "Trainer Dossier 360",
    description: "Trainer profile, compliance documents, workshop history, and feedback metrics",
    section: "OPERATIONS",
    icon: "👤",
    requiredPermission: "TRAINER_VIEW",
    isNavigable: false,
    componentName: "AdminTrainerDossier",
  },

  // 7. Dance Classes & Schedules
  {
    id: "classes",
    path: "/admin_portal/classes",
    relativeRoute: "classes",
    label: "Dance Classes",
    description: "Studio class curriculum, capacities, and recurring weekly schedules",
    section: "OPERATIONS",
    icon: "🩰",
    requiredPermission: "CLASS_VIEW",
    isNavigable: true,
    componentName: "AdminClasses",
  },

  // 8. Workshops Management & Price Approval
  {
    id: "workshops",
    path: "/admin_portal/workshops",
    relativeRoute: "workshops",
    label: "Workshops",
    description: "Workshop proposal reviews, price approvals, and attendee rosters",
    section: "OPERATIONS",
    icon: "🎪",
    requiredPermission: "WORKSHOP_VIEW",
    isNavigable: true,
    componentName: "AdminWorkshops",
  },

  // 9. Bookings Ledger & Cancellations
  {
    id: "bookings",
    path: "/admin_portal/bookings",
    relativeRoute: "bookings",
    label: "Bookings",
    description: "Class enrollments, workshop registrations, and governed cancellations",
    section: "OPERATIONS",
    icon: "📑",
    requiredPermission: "BOOKING_VIEW",
    isNavigable: true,
    componentName: "AdminBookings",
  },

  // 10. Attendance Tracking & Rosters
  {
    id: "attendance",
    path: "/admin_portal/attendance",
    relativeRoute: "attendance",
    label: "Attendance",
    description: "Class session rosters, check-ins, and student attendance logs",
    section: "OPERATIONS",
    icon: "📋",
    requiredPermission: "ATTENDANCE_VIEW",
    isNavigable: true,
    componentName: "AdminAttendance",
  },

  // 11. Dance Packages & Pricing
  {
    id: "packages",
    path: "/admin_portal/packages",
    relativeRoute: "packages",
    label: "Dance Packages",
    description: "Studio package tiers, class limits, validity days, and zero-GST pricing",
    section: "OPERATIONS",
    icon: "📦",
    requiredPermission: "PACKAGE_VIEW",
    isNavigable: true,
    componentName: "AdminPackages",
  },

  // 12. Payments, Refunds & Non-GST Receipts
  {
    id: "payments",
    path: "/admin_portal/payments",
    relativeRoute: "payments",
    label: "Payments & Finance",
    description: "Financial transactions, gateway reconciliations, refunds, and trainer payouts",
    section: "OPERATIONS",
    icon: "💳",
    requiredPermission: "PAYMENT_VIEW",
    isNavigable: true,
    componentName: "AdminPayments",
  },

  // 13. Student Feedback & Reviews
  {
    id: "feedback",
    path: "/admin_portal/feedback",
    relativeRoute: "feedback",
    label: "Student Feedback & Reviews",
    description: "Student workshop feedback, ratings distribution, and attendee reviews",
    section: "OPERATIONS",
    icon: "⭐",
    requiredPermission: "TRAINER_VIEW",
    isNavigable: true,
    componentName: "AdminFeedback",
  },

  // 14. Video Management (Short Videos & Gallery)
  {
    id: "videos",
    path: "/admin_portal/videos",
    relativeRoute: "videos",
    label: "Video Management",
    description: "Manage short dance clips (reels) and high-production gallery video showcases",
    section: "OPERATIONS",
    icon: "🎬",
    requiredPermission: null, // Core admin media management
    isNavigable: true,
    componentName: "AdminVideos",
  },

  // 13. Connected Platforms & Services
  {
    id: "platforms",
    path: "/admin_portal/platforms",
    relativeRoute: "platforms",
    label: "Connected Platforms",
    description: "Architectural breakdown of external platforms, databases, payment rails, and cloud services",
    section: "SYSTEM HEALTH & MONITORING",
    icon: "🌐",
    requiredPermission: null, // Core administrative infrastructure view
    isNavigable: true,
    componentName: "AdminPlatforms",
  },

  // 14. System Monitoring
  {
    id: "observability",
    path: "/admin_portal/observability",
    relativeRoute: "observability",
    label: "System Monitoring",
    description: "System health metrics, request tracing, and endpoint response times",
    section: "SYSTEM HEALTH & MONITORING",
    icon: "📡",
    requiredPermission: "OBSERVABILITY_VIEW",
    isNavigable: true,
    componentName: "AdminObservability",
  },

  // 14. Security & Access
  {
    id: "security",
    path: "/admin_portal/security",
    relativeRoute: "security",
    label: "Security & Access",
    description: "Account security events, risk alerts, and access protection",
    section: "SYSTEM HEALTH & MONITORING",
    icon: "🛡️",
    requiredPermission: "SECURITY_VIEW",
    isNavigable: true,
    componentName: "AdminSecurity",
  },

  // 15. Problems & Incidents
  {
    id: "incidents",
    path: "/admin_portal/incidents",
    relativeRoute: "incidents",
    label: "Problems & Incidents",
    description: "Operational problems, system alerts, and outage management",
    section: "SYSTEM HEALTH & MONITORING",
    icon: "🚨",
    requiredPermission: "INCIDENT_VIEW",
    isNavigable: true,
    componentName: "AdminIncidents",
  },

  // 16. Activity History
  {
    id: "audit-logs",
    path: "/admin_portal/audit-logs",
    relativeRoute: "audit-logs",
    label: "Activity History",
    description: "Complete, immutable administrative action history and audit ledger",
    section: "SYSTEM HEALTH & MONITORING",
    icon: "📜",
    requiredPermission: "AUDIT_LOG_VIEW",
    isNavigable: true,
    componentName: "AdminAudit",
  },

  // 17. Corrective Actions
  {
    id: "corrective-actions",
    path: "/admin_portal/corrective-actions",
    relativeRoute: "corrective-actions",
    label: "Corrective Actions",
    description: "Governed state remediation, dry-run simulations, and incident recovery",
    section: "ACTIONS & NOTIFICATIONS",
    icon: "⚡",
    requiredPermission: "CORRECTIVE_ACTION_VIEW",
    isNavigable: true,
    componentName: "AdminCorrectiveActions",
  },

  // 18. Communications & Notifications
  {
    id: "communications",
    path: "/admin_portal/communications",
    relativeRoute: "communications",
    label: "Communications",
    description: "Approved notification templates, outbound messaging history, and delivery status",
    section: "ACTIONS & NOTIFICATIONS",
    icon: "💬",
    requiredPermission: "COMMUNICATIONS_VIEW",
    isNavigable: true,
    componentName: "AdminCommunications",
  },

  // 19. Login Devices
  {
    id: "devices",
    path: "/admin_portal/devices",
    relativeRoute: "devices",
    label: "Login Devices",
    description: "Authorized admin hardware slots, device security, and session management",
    section: "SECURITY & DEVICES",
    icon: "💻",
    requiredPermission: null, // Core admin fleet security
    isNavigable: true,
    componentName: "AdminDevices",
  },
];

/**
 * Returns all navigable items grouped by section
 */
export function getNavSections(attentionCounts = {}) {
  const sections = [];
  for (const sectionName of ADMIN_NAV_SECTIONS) {
    const items = ADMIN_MODULE_REGISTRY
      .filter((m) => m.isNavigable && m.section === sectionName)
      .map((m) => ({
        id: m.id,
        to: m.path,
        label: m.label,
        icon: m.icon,
        badge: attentionCounts[m.id] || null,
        badgeVariant: m.id === "security" ? "danger" : "default",
      }));

    if (items.length > 0) {
      sections.push({
        title: sectionName,
        items,
      });
    }
  }
  return sections;
}

/**
 * Authoritative breadcrumb map for business-friendly navigation hierarchy
 */
export const ADMIN_BREADCRUMBS = {
  "/admin_portal": ["Admin Portal", "Dashboard"],
  "/admin_portal/dashboard": ["Admin Portal", "Dashboard"],
  "/admin_portal/users": ["Admin Portal", "Users & Accounts"],
  "/admin_portal/students": ["Admin Portal", "Students"],
  "/admin_portal/trainers": ["Admin Portal", "Trainers"],
  "/admin_portal/classes": ["Admin Portal", "Dance Classes"],
  "/admin_portal/workshops": ["Admin Portal", "Workshops"],
  "/admin_portal/bookings": ["Admin Portal", "Bookings"],
  "/admin_portal/attendance": ["Admin Portal", "Attendance Records"],
  "/admin_portal/packages": ["Admin Portal", "Dance Packages"],
  "/admin_portal/payments": ["Admin Portal", "Payments & Finance"],
  "/admin_portal/feedback": ["Admin Portal", "Student Feedback & Reviews"],
  "/admin_portal/observability": ["Admin Portal", "System Monitoring"],
  "/admin_portal/security": ["Admin Portal", "Security & Access"],
  "/admin_portal/security-events": ["Admin Portal", "Security & Access"],
  "/admin_portal/incidents": ["Admin Portal", "Problems & Incidents"],
  "/admin_portal/audit-logs": ["Admin Portal", "Activity History"],
  "/admin_portal/corrective-actions": ["Admin Portal", "Corrective Actions"],
  "/admin_portal/communications": ["Admin Portal", "Communications"],
  "/admin_portal/devices": ["Admin Portal", "Login Devices"],
};

export function getAdminBreadcrumbs(pathname) {
  if (!pathname) return ["Admin Portal", "Dashboard"];
  const cleanPath = pathname.replace(/\/+$/, "");

  if (ADMIN_BREADCRUMBS[cleanPath]) {
    return ADMIN_BREADCRUMBS[cleanPath];
  }

  // Handle dynamic subroutes (e.g. students/:id, trainers/:id, observability/trace/:id)
  if (cleanPath.startsWith("/admin_portal/students/")) {
    return ["Admin Portal", "Students", "Student Details"];
  }
  if (cleanPath.startsWith("/admin_portal/trainers/")) {
    return ["Admin Portal", "Trainers", "Trainer Details"];
  }
  if (cleanPath.startsWith("/admin_portal/workshops/")) {
    return ["Admin Portal", "Workshops", "Workshop Details"];
  }
  if (cleanPath.startsWith("/admin_portal/dashboard/day/") || cleanPath.startsWith("/admin_portal/dashboard/daily/")) {
    return ["Admin Portal", "Dashboard", "Daily Activity"];
  }
  if (cleanPath.startsWith("/admin_portal/observability/trace/")) {
    return ["Admin Portal", "System Monitoring", "Trace Diagnostics"];
  }

  return ["Admin Portal", "Dashboard"];
}

