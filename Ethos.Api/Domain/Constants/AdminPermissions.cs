namespace Ethos.Api.Domain.Constants;

public static class AdminPermissions
{
    // Command & Monitoring
    public const string AdminDashboardView = "ADMIN_DASHBOARD_VIEW";
    public const string AdminAuditView = "ADMIN_AUDIT_VIEW";
    public const string AdminSecurityView = "ADMIN_SECURITY_VIEW";

    // Students
    public const string StudentView = "STUDENT_VIEW";
    public const string StudentUpdate = "STUDENT_UPDATE";
    public const string StudentSuspend = "STUDENT_SUSPEND";
    public const string StudentCorrect = "STUDENT_CORRECT"; // Future definition only

    // Trainers
    public const string TrainerView = "TRAINER_VIEW";
    public const string TrainerApprove = "TRAINER_APPROVE";
    public const string TrainerReject = "TRAINER_REJECT";
    public const string TrainerCorrect = "TRAINER_CORRECT"; // Future definition only

    // Classes
    public const string ClassView = "CLASS_VIEW";
    public const string ClassCreate = "CLASS_CREATE";
    public const string ClassUpdate = "CLASS_UPDATE";
    public const string ClassCancel = "CLASS_CANCEL";

    // Workshops
    public const string WorkshopView = "WORKSHOP_VIEW";
    public const string WorkshopCreate = "WORKSHOP_CREATE";
    public const string WorkshopApprove = "WORKSHOP_APPROVE";
    public const string WorkshopUpdate = "WORKSHOP_UPDATE";
    public const string WorkshopCancel = "WORKSHOP_CANCEL";

    // Bookings & Attendance
    public const string BookingView = "BOOKING_VIEW";
    public const string BookingCorrect = "BOOKING_CORRECT"; // Future definition only
    public const string AttendanceView = "ATTENDANCE_VIEW";
    public const string AttendanceCorrect = "ATTENDANCE_CORRECT"; // Future definition only

    // Payments
    public const string PaymentView = "PAYMENT_VIEW";
    public const string PaymentReconcile = "PAYMENT_RECONCILE"; // Future definition only

    // Communications
    public const string NotificationView = "NOTIFICATION_VIEW";
    public const string NotificationSend = "NOTIFICATION_SEND";
    public const string CommunicationsView = "COMMUNICATIONS_VIEW";
    public const string CommunicationsManage = "COMMUNICATIONS_MANAGE";

    // Devices & Sessions
    public const string DeviceView = "DEVICE_VIEW";
    public const string DeviceRevoke = "DEVICE_REVOKE";

    // Users & Accounts
    public const string UserView = "ADMIN_USER_VIEW";
    public const string UserUpdateStatus = "ADMIN_USER_UPDATE_STATUS";
    public const string UserAuditView = "ADMIN_USER_AUDIT_VIEW";

    // Observability & Security Center
    public const string ObservabilityView = "OBSERVABILITY_VIEW";
    public const string ObservabilityManage = "OBSERVABILITY_MANAGE";
    public const string SecurityCenterView = "SECURITY_CENTER_VIEW";
    public const string SecurityCenterManage = "SECURITY_CENTER_MANAGE";

    // Dance Packages
    public const string PackageView = "PACKAGE_VIEW";
    public const string PackageCreate = "PACKAGE_CREATE";
    public const string PackageUpdate = "PACKAGE_UPDATE";
    public const string PackageDelete = "PACKAGE_DELETE";

    // Legacy / Aliased User View for backward compatibility
    public const string LegacyUserView = "USER_VIEW";

    // Incidents & Corrective Actions (Definitions for future phases)
    public const string IncidentView = "INCIDENT_VIEW";
    public const string IncidentManage = "INCIDENT_MANAGE";
    public const string CorrectiveActionView = "CORRECTIVE_ACTION_VIEW";
    public const string CorrectiveActionExecute = "CORRECTIVE_ACTION_EXECUTE";

    // Media & Storage
    public const string MediaView = "MEDIA_VIEW";
    public const string MediaUpload = "MEDIA_UPLOAD";
    public const string MediaDelete = "MEDIA_DELETE";

    private static readonly HashSet<string> AllPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        MediaView, MediaUpload, MediaDelete,
        AdminDashboardView, AdminAuditView, AdminSecurityView,
        ObservabilityView, ObservabilityManage, SecurityCenterView, SecurityCenterManage,
        UserView, UserUpdateStatus, UserAuditView, LegacyUserView,
        StudentView, StudentUpdate, StudentSuspend, StudentCorrect,
        TrainerView, TrainerApprove, TrainerReject, TrainerCorrect,
        ClassView, ClassCreate, ClassUpdate, ClassCancel,
        WorkshopView, WorkshopCreate, WorkshopApprove, WorkshopUpdate, WorkshopCancel,
        PackageView, PackageCreate, PackageUpdate, PackageDelete,
        BookingView, BookingCorrect, AttendanceView, AttendanceCorrect,
        PaymentView, PaymentReconcile,
        NotificationView, NotificationSend,
        CommunicationsView, CommunicationsManage,
        DeviceView, DeviceRevoke,
        IncidentView, IncidentManage,
        CorrectiveActionView, CorrectiveActionExecute
    };

    public static bool IsValid(string permissionCode) =>
        !string.IsNullOrWhiteSpace(permissionCode) && AllPermissions.Contains(permissionCode.Trim());

    public static IReadOnlyCollection<string> GetAll() => AllPermissions;
}
