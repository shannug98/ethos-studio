export const TRAINER_PERMISSIONS = {
  VIEW_WORKSHOPS: "TRAINER_VIEW_WORKSHOPS",
  CREATE_WORKSHOP: "TRAINER_CREATE_WORKSHOP",
  UPDATE_WORKSHOP: "TRAINER_UPDATE_WORKSHOP",
  CANCEL_WORKSHOP: "TRAINER_CANCEL_WORKSHOP",
  VIEW_WORKSHOP_STUDENTS: "TRAINER_VIEW_WORKSHOP_STUDENTS",
  VIEW_WORKSHOP_FEEDBACK: "TRAINER_VIEW_WORKSHOP_FEEDBACK",
  VIEW_DASHBOARD: "TRAINER_VIEW_DASHBOARD",
  VIEW_PROFILE: "TRAINER_VIEW_PROFILE",
  UPDATE_PROFILE: "TRAINER_UPDATE_PROFILE",
  VIEW_PERFORMANCE: "TRAINER_VIEW_PERFORMANCE",
  VIEW_TIER: "TRAINER_VIEW_TIER",
  REQUEST_UPGRADE: "TRAINER_REQUEST_UPGRADE",
  REQUEST_TIER_UPGRADE: "TRAINER_REQUEST_TIER_UPGRADE",
  VIEW_NOTIFICATIONS: "TRAINER_VIEW_NOTIFICATIONS",
  VIEW_CLASSES: "TRAINER_VIEW_CLASSES",
  VIEW_STUDENTS: "TRAINER_VIEW_STUDENTS",
  MARK_ATTENDANCE: "TRAINER_MARK_ATTENDANCE",
  VIEW_AVAILABILITY: "TRAINER_VIEW_AVAILABILITY",
  MANAGE_AVAILABILITY: "TRAINER_MANAGE_AVAILABILITY",
  UPDATE_AVAILABILITY: "TRAINER_UPDATE_AVAILABILITY",
};

export const TRAINER_PERMISSION_METADATA = {
  [TRAINER_PERMISSIONS.CANCEL_WORKSHOP]: {
    name: "Cancel Workshops",
    description: "Allows the trainer to cancel their own workshops",
    effectOnRestrict: "Restricting this permission will prevent the trainer from cancelling scheduled workshops. Emergency cancellations must be processed by studio administration.",
    effectOnAllow: "Allows the trainer to cancel their scheduled workshops in the trainer portal.",
  },
  [TRAINER_PERMISSIONS.CREATE_WORKSHOP]: {
    name: "Create Workshops",
    description: "Allows the trainer to propose or create workshops",
    effectOnRestrict: "Restricting this permission will prevent the trainer from proposing or creating new workshops. Existing workshops and bookings will not be deleted or cancelled.",
    effectOnAllow: "Allows the trainer to propose and create new workshops for studio review.",
  },
  [TRAINER_PERMISSIONS.MANAGE_AVAILABILITY]: {
    name: "Manage Availability",
    description: "Allows the trainer to manage available teaching times",
    effectOnRestrict: "Restricting this permission prevents the trainer from modifying general teaching availability schedules.",
    effectOnAllow: "Allows full management of teaching availability in the trainer portal.",
  },
  [TRAINER_PERMISSIONS.MARK_ATTENDANCE]: {
    name: "Mark Attendance",
    description: "Allows the trainer to record attendee attendance",
    effectOnRestrict: "Restricting this permission prevents the trainer from checking in students or marking session attendance.",
    effectOnAllow: "Allows the trainer to check in attendees and mark attendance for active sessions.",
  },
  [TRAINER_PERMISSIONS.REQUEST_TIER_UPGRADE]: {
    name: "Request Tier Upgrade",
    description: "Allows the trainer to request a higher trainer tier",
    effectOnRestrict: "Restricting this permission prevents the trainer from submitting tier promotion requests.",
    effectOnAllow: "Allows the trainer to request promotion to a higher trainer level.",
  },
  [TRAINER_PERMISSIONS.REQUEST_UPGRADE]: {
    name: "Request Trainer Upgrade",
    description: "Allows the trainer to request an upgrade",
    effectOnRestrict: "Restricting this permission prevents the trainer from requesting account or tier upgrades.",
    effectOnAllow: "Allows the trainer to submit upgrade requests.",
  },
  [TRAINER_PERMISSIONS.UPDATE_AVAILABILITY]: {
    name: "Update Availability",
    description: "Allows the trainer to change available time slots",
    effectOnRestrict: "Restricting this permission locks the trainer's availability calendar against modifications.",
    effectOnAllow: "Allows the trainer to change available teaching time slots.",
  },
  [TRAINER_PERMISSIONS.UPDATE_PROFILE]: {
    name: "Update Profile",
    description: "Allows the trainer to edit their profile",
    effectOnRestrict: "Restricting this permission locks the trainer profile against bio, photo, and credential edits.",
    effectOnAllow: "Allows the trainer to edit their bio, dance styles, and profile details.",
  },
  [TRAINER_PERMISSIONS.UPDATE_WORKSHOP]: {
    name: "Edit Workshops",
    description: "Allows the trainer to update workshop information",
    effectOnRestrict: "Restricting this permission prevents the trainer from editing workshop details, dates, or prices.",
    effectOnAllow: "Allows the trainer to update workshop descriptions and details.",
  },
  [TRAINER_PERMISSIONS.VIEW_AVAILABILITY]: {
    name: "View Availability",
    description: "Allows the trainer to view their available time slots",
    effectOnRestrict: "Hides the availability schedule tab from the trainer portal.",
    effectOnAllow: "Allows the trainer to view their available teaching time slots.",
  },
  [TRAINER_PERMISSIONS.VIEW_CLASSES]: {
    name: "View Assigned Classes",
    description: "Allows the trainer to see assigned dance classes",
    effectOnRestrict: "Hides assigned class rosters and timetable schedules from the trainer.",
    effectOnAllow: "Allows the trainer to see assigned dance classes and schedules.",
  },
  [TRAINER_PERMISSIONS.VIEW_DASHBOARD]: {
    name: "View Dashboard",
    description: "Allows access to the trainer dashboard",
    effectOnRestrict: "Restricting this permission blocks access to the trainer home dashboard.",
    effectOnAllow: "Allows access to the trainer home dashboard.",
  },
  [TRAINER_PERMISSIONS.VIEW_NOTIFICATIONS]: {
    name: "View Notifications",
    description: "Allows access to trainer notifications",
    effectOnRestrict: "Mutes notification feeds and operational alerts in the trainer portal.",
    effectOnAllow: "Allows access to trainer notifications and announcements.",
  },
  [TRAINER_PERMISSIONS.VIEW_PERFORMANCE]: {
    name: "View Performance",
    description: "Allows the trainer to view performance and feedback metrics",
    effectOnRestrict: "Hides performance dashboards, student ratings, and revenue shares.",
    effectOnAllow: "Allows the trainer to view performance, rating, and feedback metrics.",
  },
  [TRAINER_PERMISSIONS.VIEW_WORKSHOPS]: {
    name: "View Workshops",
    description: "Allows the trainer to view workshop listings and schedules",
    effectOnRestrict: "Hides workshop listings from the trainer portal.",
    effectOnAllow: "Allows the trainer to view workshop listings and schedules.",
  },
  [TRAINER_PERMISSIONS.VIEW_WORKSHOP_STUDENTS]: {
    name: "View Workshop Students",
    description: "Allows the trainer to view student rosters for their workshops",
    effectOnRestrict: "Hides attendee lists and student contact details for workshops.",
    effectOnAllow: "Allows viewing confirmed workshop student rosters.",
  },
  [TRAINER_PERMISSIONS.VIEW_WORKSHOP_FEEDBACK]: {
    name: "View Workshop Feedback",
    description: "Allows the trainer to view student reviews and ratings for workshops",
    effectOnRestrict: "Hides student ratings and written feedback for workshops.",
    effectOnAllow: "Allows the trainer to view attendee reviews and ratings.",
  },
  [TRAINER_PERMISSIONS.VIEW_TIER]: {
    name: "View Trainer Level",
    description: "Allows the trainer to view current trainer level and benefits",
    effectOnRestrict: "Hides trainer level progression and compensation details.",
    effectOnAllow: "Allows the trainer to view their assigned trainer level and benefits.",
  },
  [TRAINER_PERMISSIONS.VIEW_STUDENTS]: {
    name: "View Student Roster",
    description: "Allows the trainer to view students enrolled in assigned classes",
    effectOnRestrict: "Hides enrolled student names and details from assigned classes.",
    effectOnAllow: "Allows the trainer to view students enrolled in assigned classes.",
  },
};

export function getTrainerPermissionMeta(code) {
  if (TRAINER_PERMISSION_METADATA[code]) {
    return TRAINER_PERMISSION_METADATA[code];
  }
  const clean = (code || "")
    .replace(/^TRAINER_/, "")
    .split("_")
    .map((w) => w.charAt(0) + w.slice(1).toLowerCase())
    .join(" ");
  return {
    name: clean || "Unknown Permission",
    description: `Allows the trainer to perform ${clean.toLowerCase()} actions in the trainer portal.`,
    effectOnRestrict: `Restricting this permission will block access to ${clean.toLowerCase()} actions in the trainer portal.`,
    effectOnAllow: `Allows the trainer to perform ${clean.toLowerCase()} actions in the trainer portal.`,
  };
}
