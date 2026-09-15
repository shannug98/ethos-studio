using Ethos.Api.Contracts.Classes;
using Ethos.Api.Contracts.Packages;
using Ethos.Api.Contracts.Payments;
using Ethos.Api.Contracts.Students;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Contracts.Workshops;

namespace Ethos.Api.Contracts.Admin;

public class AdminDashboardResponse
{
    public AdminUserMetrics Users { get; set; } = new();
    public AdminTrainerMetrics Trainers { get; set; } = new();
    public AdminWorkshopMetrics Workshops { get; set; } = new();
    public AdminClassMetrics Classes { get; set; } = new();
    public AdminPackageMetrics Packages { get; set; } = new();
    public AdminFinanceMetrics Finance { get; set; } = new();
}

public class AdminUserMetrics
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int StudentsCount { get; set; }
    public int TrainersCount { get; set; }
    public int AdminsCount { get; set; }
}

public class AdminTrainerMetrics
{
    public int ActiveTrainers { get; set; }
    public int PendingApplications { get; set; }
    public int ApprovedApplications { get; set; }
    public int RejectedApplications { get; set; }
    public int PendingTierUpgrades { get; set; }
}

public class AdminWorkshopMetrics
{
    public int TotalWorkshops { get; set; }
    public int PendingApproval { get; set; }
    public int UpcomingWorkshops { get; set; }
    public int CompletedWorkshops { get; set; }
    public int CancelledWorkshops { get; set; }
    public int TotalRegistrations { get; set; }
}

public class AdminClassMetrics
{
    public int ActiveClasses { get; set; }
    public int InactiveClasses { get; set; }
    public int TotalEnrollments { get; set; }
}

public class AdminPackageMetrics
{
    public int ActivePackages { get; set; }
    public int PackagePurchasesCount { get; set; }
}

public class AdminFinanceMetrics
{
    public decimal TotalSuccessfulRevenue { get; set; }
    public decimal CurrentMonthRevenue { get; set; }
    public int SuccessfulTransactionsCount { get; set; }
    public int FailedTransactionsCount { get; set; }
    public int PendingTransactionsCount { get; set; }
}

public class AdminUserListResponse
{
    public Guid UserId { get; set; }
    public string Phone { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? PrimaryRole { get; set; }
    public List<string> OtherRoles { get; set; } = new();
    public bool IsActive { get; set; }
    public string CustomerCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AdminUserDetailsResponse
{
    public Guid UserId { get; set; }
    public string Phone { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    public string CustomerCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public StudentProfileResponse? StudentProfile { get; set; }
    public TrainerResponse? TrainerProfile { get; set; }
}

public class AdminUpdateStatusRequest
{
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class AdminUserAuditHistoryItem
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = null!; // "ADMIN_ACTION" or "SECURITY_EVENT"
    public string Event { get; set; } = null!;
    public string Severity { get; set; } = "INFO";
    public string? ActorAdminUserId { get; set; }
    public string? ActorName { get; set; }
    public string? TraceId { get; set; }
    public string? Reason { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? DetailsJson { get; set; }
}

public class AdminUserAuditHistoryResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string CustomerCode { get; set; } = null!;
    public List<AdminUserAuditHistoryItem> History { get; set; } = new();
}

public class AdminStudentListResponse
{
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }
    public string CustomerCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool ProfileCompleted { get; set; }
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; }

    // Enriched Directory Fields
    public string? ActivePackageName { get; set; }
    public int? ClassesAllowed { get; set; }
    public int ClassesUsed { get; set; }
    public int TotalAttendanceCount { get; set; }
    public int MissingFieldsCount { get; set; }
    public List<string> MissingFields { get; set; } = new();
}

public class AdminStudentSummaryStatsResponse
{
    public int TotalStudents { get; set; }
    public int CompleteProfiles { get; set; }
    public int IncompleteProfiles { get; set; }
    public int ActiveStudents { get; set; }
}

public class AdminStudentAttendanceItemResponse
{
    public Guid Id { get; set; }
    public Guid ClassSessionId { get; set; }
    public string ClassName { get; set; } = null!;
    public string DanceStyle { get; set; } = null!;
    public DateTime SessionDate { get; set; }
    public string Status { get; set; } = null!;
    public DateTime MarkedAt { get; set; }
    public string? Notes { get; set; }
}

public class AdminStudentWorkshopBookingItemResponse
{
    public Guid Id { get; set; }
    public Guid WorkshopId { get; set; }
    public string WorkshopTitle { get; set; } = string.Empty;
    public string? TrainerName { get; set; }
    public DateTime WorkshopDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? DanceStyle { get; set; }
    public string? Venue { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!; // "Confirmed", "Attended", etc.
    public int StatusCode { get; set; }
    public DateTime BookedAt { get; set; }
    public Guid? PaymentTransactionId { get; set; }
    public string? PaymentStatus { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public string AttendanceStatus { get; set; } = "Not Attended"; // "Attended", "No Show", "Scheduled"
    public string FeedbackStatus { get; set; } = "Pending"; // "Submitted", "Pending", "Ineligible"
    public int? FeedbackRating { get; set; }
    public string? FeedbackToken { get; set; }
}

public class AdminStudentDetailsResponse
{
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }
    public string CustomerCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool ProfileCompleted { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> MissingFields { get; set; } = new();

    public StudentPackageResponse? ActivePackage { get; set; }
    public List<StudentPackageResponse> PackageHistory { get; set; } = new();
    public List<ClassEnrollmentResponse> ClassEnrollments { get; set; } = new();
    public List<AdminStudentAttendanceItemResponse> AttendanceRecords { get; set; } = new();
    public List<AdminStudentWorkshopBookingItemResponse> WorkshopBookings { get; set; } = new();
    public List<WorkshopFeedbackResponse> SubmittedFeedback { get; set; } = new();
    public List<PaymentTransactionResponse> PaymentTransactions { get; set; } = new();
}

public class AdminTrainerListResponse
{
    public Guid TrainerId { get; set; }
    public Guid UserId { get; set; }
    public string TrainerCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public string? City { get; set; }
    public string Status { get; set; } = null!;
    public string? TierName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminTrainerSummaryStatsResponse
{
    public int TotalTrainers { get; set; }
    public int ActiveTrainers { get; set; }
    public int PendingApplications { get; set; }
    public int UpgradeRequests { get; set; }
}

public class AdminTrainerDetailsResponse
{
    public Guid TrainerId { get; set; }
    public Guid UserId { get; set; }
    public string TrainerCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? City { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public string? PrimaryDanceStyle { get; set; }
    public string? SecondaryDanceStyles { get; set; }
    public int? ExperienceYears { get; set; }
    public string? CurrentStudio { get; set; }
    public string? Bio { get; set; }
    public string? InstagramUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string Status { get; set; } = null!;
    public string? TierName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public TrainerApplicationResponse? LatestApplication { get; set; }
    public List<TrainerTierHistoryResponse> TierHistory { get; set; } = new();
    public List<AdminTrainerPermissionDetailResponse> Permissions { get; set; } = new();
    public List<TrainerWorkshopResponse> Workshops { get; set; } = new();
    public TrainerPerformanceResponse? Performance { get; set; }
    public List<TrainerWorkshopFeedbackResponse> Feedback { get; set; } = new();
}

public class AdminTrainerPermissionDetailResponse
{
    public string PermissionCode { get; set; } = null!;
    public string PermissionName { get; set; } = null!;
    public string? Description { get; set; }
    public bool RoleDefault { get; set; }
    public string Override { get; set; } = "None"; // Allowed, Denied, None
    public bool EffectiveValue { get; set; }
    public Guid? PermissionId { get; set; }
    public DateTime? OverrideModifiedAt { get; set; }
    public string? OverrideReason { get; set; }
}

public class AdminWorkshopRegistrationResponse
{
    public Guid BookingId { get; set; }
    public Guid WorkshopId { get; set; }
    public string WorkshopTitle { get; set; } = null!;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentPhone { get; set; } = null!;
    public string? StudentEmail { get; set; }
    public string Status { get; set; } = null!;
    public DateTime BookedAt { get; set; }
    public Guid? PaymentTransactionId { get; set; }
    public string? PaymentStatus { get; set; }

    // Enriched Roster & Attendee details
    public string CustomerCode { get; set; } = "GUEST";
    public string BookingReference { get; set; } = null!;
    public bool IsGuest { get; set; }
    public string AttendeeType { get; set; } = "Workshop Attendee";
    public string AttendanceStatus { get; set; } = "Not marked";
    public string FeedbackStatus { get; set; } = "Pending";
    public int? FeedbackRating { get; set; }
    public string? FeedbackComment { get; set; }
}

public class AdminPaymentTransactionResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string UserPhone { get; set; } = null!;
    public int Purpose { get; set; }
    public string PurposeName { get; set; } = null!;
    public string ReferenceId { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public int Status { get; set; }
    public string StatusName { get; set; } = null!;
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    // Enriched Finance Metadata (Batch 5)
    public string? UserCustomerCode { get; set; }
    public string ItemName { get; set; } = null!;
    public string? ItemDetails { get; set; }
    public string DisplayPurpose { get; set; } = null!;
    public string DisplayStatus { get; set; } = null!;
    public bool IsGatewayVerified { get; set; }
    public bool IsReconciled { get; set; }
    public decimal TotalRefundedAmount { get; set; }
    public decimal RemainingRefundableAmount { get; set; }

    // Admin-Centric Operational Fields
    public string FulfillmentStatus { get; set; } = "Not Completed";
    public string CustomerImpact { get; set; } = "Payment Pending";
    public string ActionNeeded { get; set; } = "None";
    public bool HasCustomerReportedDiscrepancy { get; set; }
    public string? LatestAdminNote { get; set; }
}

public class AdminRevenueResponse
{
    public decimal TotalSuccessfulRevenue { get; set; } // Net Collected
    public decimal GrossSuccessfulRevenue { get; set; } // Gross before refunds
    public decimal TotalRefundedRevenue { get; set; }   // Total refunds recorded
    public decimal CurrentMonthRevenue { get; set; }
    public decimal PackageRevenue { get; set; }
    public decimal WorkshopRevenue { get; set; }
    public decimal TrainerApplicationRevenue { get; set; }
    public decimal TrainerUpgradeRevenue { get; set; }
    public int SuccessfulTransactionsCount { get; set; }
    public int FailedTransactionsCount { get; set; }
    public int PendingTransactionsCount { get; set; }

    // Operational Admin KPIs
    public int PaymentsNeedingAttentionCount { get; set; }
    public int CustomerIssuesCount { get; set; }
    public int TrainerPayoutsPendingCount { get; set; }
    public decimal TrainerPayoutsPendingAmount { get; set; }
    public int RefundsIssuedCount { get; set; }
}

public class AdminFeedbackResponse
{
    public Guid Id { get; set; }
    public Guid WorkshopId { get; set; }
    public string WorkshopTitle { get; set; } = null!;
    public Guid TrainerId { get; set; }
    public string TrainerName { get; set; } = null!;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentPhone { get; set; } = null!;
    public int Rating { get; set; }
    public int TeachingRating { get; set; }
    public int EnergyRating { get; set; }
    public int ContentRating { get; set; }
    public string? Comment { get; set; }
    public bool WouldRecommend { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class AdminAuditLogResponse
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string AdminName { get; set; } = null!;
    public string? AdminCustomerCode { get; set; }
    public string AdminPhone { get; set; } = null!;
    public Guid? AdminDeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string ActionType { get; set; } = null!;
    public string Category { get; set; } = "SYSTEM";
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public bool Success { get; set; } = true;
    public string? OutcomeCode { get; set; }
    public string? Reason { get; set; }
    public string? TraceId { get; set; }
    public string? RequestId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminSecurityEventResponse
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? UserId { get; set; }
    public string? AdminName { get; set; }
    public string? AdminCustomerCode { get; set; }
    public Guid? AdminDeviceId { get; set; }
    public string? DeviceName { get; set; }
    public Guid? AdminSessionId { get; set; }
    public string? TraceId { get; set; }
    public string? MaskedPhone { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminCommandDashboardResponse
{
    public AdminCommandOverview Overview { get; set; } = new();
    public AdminCommandActivity Activity { get; set; } = new();
    public AdminCommandSecurity Security { get; set; } = new();
    public AdminCommandHealth Health { get; set; } = new();
    public List<AdminAttentionItem> Attention { get; set; } = new();
    public List<AdminDeviceResponse> ApprovedDevices { get; set; } = new();
    public List<AdminRecentActivityItem> RecentActivity { get; set; } = new();
}

public class AdminCommandOverview
{
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int ActiveTrainers { get; set; }
    public int PendingTrainerApplications { get; set; }
    public int ActiveClasses { get; set; }
    public int TotalEnrollments { get; set; }
    public int UpcomingWorkshops { get; set; }
    public int PendingWorkshops { get; set; }
    public int TodayBookings { get; set; }
    public decimal TodayRevenue { get; set; }
    public int PendingActionsCount { get; set; }
}

public class AdminCommandActivity
{
    public string Range { get; set; } = "week"; // day, week, month
    public List<AdminActivityDataPoint> Series { get; set; } = new();
}

public class AdminActivityDataPoint
{
    public string Date { get; set; } = null!;
    public int AdminActions { get; set; }
    public int Workshops { get; set; }
    public int Bookings { get; set; }
    public decimal Revenue { get; set; }
    public int SecurityEvents { get; set; }
}

public class AdminCommandSecurity
{
    public int FailedAdminLogins { get; set; }
    public int AuthorizationDenials { get; set; }
    public int DeviceEvents { get; set; }
    public int HighSeverityEvents { get; set; }
    public int TotalSecurityEvents { get; set; }
}

public class AdminCommandHealth
{
    public string Api { get; set; } = "Healthy";
    public string Database { get; set; } = "Healthy";
    public string Authentication { get; set; } = "Healthy";
    public string Storage { get; set; } = "Healthy";
    public string Payments { get; set; } = "Healthy";
    public string Messaging { get; set; } = "NotMonitored"; // Per adjustment 4: strictly real, never fake healthy
}

public class AdminAttentionItem
{
    public string Id { get; set; } = null!;
    public string Category { get; set; } = null!; // TRAINERS, PAYMENTS, SECURITY, PACKAGES
    public string Severity { get; set; } = null!; // INFO, WARNING, HIGH, CRITICAL
    public int Count { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? ActionPath { get; set; }
}

public class AdminRecentActivityItem
{
    public Guid Id { get; set; }
    public string Source { get; set; } = null!; // AUDIT or SECURITY
    public string ActorName { get; set; } = null!;
    public string? ActorCustomerCode { get; set; }
    public string Action { get; set; } = null!;
    public string? EntityType { get; set; }
    public string? Outcome { get; set; }
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CreateDanceClassRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string DanceStyle { get; set; } = null!;
    public string Level { get; set; } = null!;
    public int DurationMinutes { get; set; } = 60;
    public string? ImageUrl { get; set; }
}

public class UpdateDanceClassRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string DanceStyle { get; set; } = null!;
    public string Level { get; set; } = null!;
    public int DurationMinutes { get; set; } = 60;
    public string? ImageUrl { get; set; }
}

public class CreatePackageRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int? ClassLimit { get; set; }
    public bool IsFeatured { get; set; }
    public string? FeaturesJson { get; set; }
}

public class UpdatePackageRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int? ClassLimit { get; set; }
    public bool IsFeatured { get; set; }
    public string? FeaturesJson { get; set; }
}

public class AdminPackageStatsResponse
{
    public int TotalPackages { get; set; }
    public int AvailableForNewPurchases { get; set; }
    public int NotAvailableForNewPurchases { get; set; }
    public int StudentPackagesPurchased { get; set; }
    public decimal TotalRevenue { get; set; }
    public int IncludedClassesAllocated { get; set; }
    public int IncludedClassesRemaining { get; set; }
    public decimal AveragePackagePrice { get; set; }
}

public class PackageDependenciesDetail
{
    public int StudentPackageRecords { get; set; }
    public int ActiveStudentPackages { get; set; }
    public int PaymentRecords { get; set; }
    public int ClassEnrollmentRecords { get; set; }
}

public class PackageDependencyCheckResponse
{
    public Guid PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public bool CanDelete { get; set; }
    public bool CanDeactivate { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public PackageDependenciesDetail RecordsUsingThisPackage { get; set; } = new();
}

public class AdminPackageDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int? ClassLimit { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public List<string> Features { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Authoritative Student-Package & Usage Metrics
    public int StudentPackagesPurchased { get; set; }
    public int ActiveStudentPasses { get; set; }
    public int TotalClassesAllocated { get; set; }
    public int TotalClassesUsed { get; set; }
    public int TotalClassesRemaining { get; set; }
    public double? ClassConsumptionRatePercent { get; set; }
    public int PaymentRecordsCount { get; set; }
    public int ClassEnrollmentRecordsCount { get; set; }
}

public class PackageAuditValues
{
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    public int? DurationDays { get; set; }
    public int? ClassLimit { get; set; }
}

public class PackageUpdateMetadata
{
    public Guid PackageId { get; set; }
    public PackageAuditValues? OldValues { get; set; }
    public PackageAuditValues? NewValues { get; set; }
}

public class AdminPackageActivityItem
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = null!;
    public string DisplayAction { get; set; } = null!;
    public string? Reason { get; set; }
    public string? AdminName { get; set; }
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; }
    public PackageUpdateMetadata? UpdateDetails { get; set; }
}

public class DiagnosticIssue
{
    public string Code { get; set; } = null!;
    public string Category { get; set; } = null!; // "BUSINESS_STATE" or "TECHNICAL_FAILURE"
    public string Severity { get; set; } = "INFO"; // "INFO", "WARNING", "CRITICAL"
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public object? Evidence { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }
    public string RecommendedAction { get; set; } = null!;
}

public class StudentDiagnosticReport
{
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }
    public string CustomerCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string OverallStatus { get; set; } = "HEALTHY"; // "HEALTHY", "ATTENTION_REQUIRED", "CRITICAL_FAILURE"
    public List<DiagnosticIssue> BusinessIssues { get; set; } = new();
    public List<DiagnosticIssue> TechnicalFailures { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public int TotalClassesAttended { get; set; }
    public int TotalClassesMissed { get; set; }
    public double AttendanceRate { get; set; }
    public int ActivePackagesCount { get; set; }
    public int TotalBookingsCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }
}

public class TrainerDiagnosticReport
{
    public Guid TrainerId { get; set; }
    public Guid UserId { get; set; }
    public string TrainerCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? CurrentTier { get; set; }
    public string OverallStatus { get; set; } = "HEALTHY"; // "HEALTHY", "ATTENTION_REQUIRED", "CRITICAL_FAILURE"
    public List<DiagnosticIssue> BusinessIssues { get; set; } = new();
    public List<DiagnosticIssue> TechnicalFailures { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public double AverageRating { get; set; }
    public int TotalFeedbackCount { get; set; }
    public int ActiveWorkshopsCount { get; set; }
    public int UnpricedWorkshopsCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }
}

// ==========================================
// BATCH 3: PHASE 18.9 - SCHEDULES & WORKSHOPS
// ==========================================

public class AdminClassScheduleRequest
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? StudioRoom { get; set; }
    public int Capacity { get; set; }
}

public class AdminClassScheduleResponse
{
    public Guid Id { get; set; }
    public Guid DanceClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public string DayOfWeekName => DayOfWeek.ToString();
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? StudioRoom { get; set; }
    public int Capacity { get; set; }
    public int EnrolledCount { get; set; }
    public int TotalSessions { get; set; }
    public bool HasHistory => TotalSessions > 0 || EnrolledCount > 0;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClassDependenciesDetail
{
    public int Enrollments { get; set; }
    public int Bookings { get; set; }
    public int AttendanceRecords { get; set; }
    public int PaymentReferences { get; set; }
    public int FeedbackRecords { get; set; }
    public int Schedules { get; set; }
    public int AuditRecords { get; set; }
}

public class ClassDependencyCheckResponse
{
    public Guid ClassId { get; set; }
    public bool CanDelete { get; set; }
    public bool CanArchive { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ClassDependenciesDetail Dependencies { get; set; } = new();
}

public class ScheduleDependencyCheckResponse
{
    public Guid ScheduleId { get; set; }
    public bool CanDelete { get; set; }
    public bool CanDeactivate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int SessionsCount { get; set; }
    public int AttendanceRecordsCount { get; set; }
}

public class ArchiveDanceClassRequest
{
    public string? Reason { get; set; }
}

public class RestoreDanceClassRequest
{
    public string? Reason { get; set; }
}

// ==========================================
// BATCH 3: PHASE 18.10 - BOOKINGS & ATTENDANCE
// ==========================================

public class AdminClassEnrollmentResponse
{
    public Guid Id { get; set; }
    public Guid StudentProfileId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentPhone { get; set; } = null!;
    public string StudentCustomerCode { get; set; } = null!;
    public Guid DanceClassId { get; set; }
    public string DanceClassName { get; set; } = null!;
    public Guid? StudentPackageId { get; set; }
    public string? PackageName { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class AdminCancelBookingRequest
{
    public string Reason { get; set; } = null!;
}

public class AdminManualEnrollmentRequest
{
    public Guid StudentProfileId { get; set; }
    public Guid DanceClassId { get; set; }
    public Guid? StudentPackageId { get; set; }
    public bool AllowPackageBypass { get; set; }
    public string? BypassReason { get; set; }
}

public class AdminClassSessionResponse
{
    public Guid Id { get; set; }
    public Guid ClassScheduleId { get; set; }
    public Guid DanceClassId { get; set; }
    public string DanceClassName { get; set; } = null!;
    public string? StudioRoom { get; set; }
    public DateTime SessionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Status { get; set; } = null!;
    public int EnrolledCount { get; set; }
    public int AttendedCount { get; set; }
    public int Capacity { get; set; }
}

public class AdminSessionRosterResponse
{
    public Guid SessionId { get; set; }
    public Guid DanceClassId { get; set; }
    public string DanceClassName { get; set; } = null!;
    public DateTime SessionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? StudioRoom { get; set; }
    public int Capacity { get; set; }
    public List<AdminRosterStudentItem> Students { get; set; } = new();
}

public class AdminRosterStudentItem
{
    public Guid StudentProfileId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentPhone { get; set; } = null!;
    public string CustomerCode { get; set; } = null!;
    public int? AttendanceStatus { get; set; }
    public string? AttendanceStatusName { get; set; }
    public DateTime? MarkedAt { get; set; }
    public string? MarkedByAdminName { get; set; }
    public string? Notes { get; set; }
}

public class AdminMarkAttendanceRequest
{
    public List<AdminStudentAttendanceItem> Records { get; set; } = new();
}

public class AdminStudentAttendanceItem
{
    public Guid StudentProfileId { get; set; }
    public int Status { get; set; } // 1=Present, 2=Absent, 3=Late, 4=Excused
    public string? Notes { get; set; }
}

public class AdminMarkWorkshopAttendanceRequest
{
    public Guid StudentProfileId { get; set; }
    public Guid? BookingId { get; set; }
    public int Status { get; set; } // 2=Confirmed (Undo), 4=Attended, 5=NoShow
}

// ==========================================
// BATCH 3: PHASE 18.11 - FINANCE, RECONCILIATION, PAYOUTS & NON-GST RECEIPTS
// ==========================================

public class AdminRecordRefundRequest
{
    public decimal RefundAmount { get; set; }
    public string Reason { get; set; } = null!;
    public string GatewayRefundId { get; set; } = null!;
    public string? Notes { get; set; }
}

public class AdminRefundResponse
{
    public Guid TransactionId { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal PreviouslyRefunded { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal RemainingRefundable { get; set; }
    public string Status { get; set; } = null!;
    public string GatewayRefundId { get; set; } = null!;
    public DateTime RefundedAt { get; set; }
    public string Reason { get; set; } = null!;
}

public class AdminReconcilePaymentRequest
{
    public int TargetStatus { get; set; } // PaymentStatus (e.g. 4=Paid, 5=Failed)
    public string Reason { get; set; } = null!;
    public string? GatewayPaymentId { get; set; }
    public string? GatewayPayload { get; set; }
    public string? Notes { get; set; }
}

public class AdminResolvePaymentIssueRequest
{
    public string IssueType { get; set; } = "MONEY_DEDUCTED_CLAIM"; // "MONEY_DEDUCTED_CLAIM", "GATEWAY_TIMEOUT", "FULFILLMENT_FAILED", "PAYMENT_FAILED_NO_MONEY", "OTHER"
    public string Action { get; set; } = "CONFIRM_AND_FULFILL"; // "CONFIRM_AND_FULFILL", "MARK_NOT_RECEIVED", "RECORD_NOTE", "FLAG_DISCREPANCY"
    public string? GatewayPaymentId { get; set; } // Razorpay Payment ID or Bank UTR
    public string AdminReason { get; set; } = null!;
    public string? AdminNotes { get; set; }
    public string? CustomerMessage { get; set; }
}

public class AdminResolvePaymentIssueResponse
{
    public Guid TransactionId { get; set; }
    public string PreviousStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
    public string FulfillmentResult { get; set; } = null!;
    public bool FulfillmentExecuted { get; set; }
    public string Message { get; set; } = null!;
    public DateTime ResolvedAt { get; set; }
    public string? CustomerMessageTemplate { get; set; }
    public AdminPaymentTransactionResponse Transaction { get; set; } = null!;
}

public class AdminPaymentTimelineEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!;
    public string EventTitle { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string? Actor { get; set; }
    public string? Payload { get; set; }
    public string Severity { get; set; } = "INFO"; // "INFO", "WARNING", "SUCCESS", "DANGER"
}

public class AdminPaymentReceiptResponse
{
    public string ReceiptNumber { get; set; } = null!;
    public DateTime ReceiptDate { get; set; }
    public AdminReceiptStudioDetails Studio { get; set; } = new();
    public AdminReceiptCustomerDetails Customer { get; set; } = new();
    public AdminReceiptTransactionDetails Transaction { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalRefundedAmount { get; set; }
    public decimal NetAmountPaid { get; set; }
    public decimal RemainingRefundableAmount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = "PAID";
    public string DocumentType { get; set; } = "Official Commercial Payment Receipt (Non-GST)";
    public string Disclaimer { get; set; } = "Ethos Dance Studio is not registered under GST. This document serves as an official commercial payment receipt for studio services rendered.";
    public string SystemGeneratedNotice { get; set; } = "This is a computer-generated commercial receipt and does not require a physical signature.";
}

public class AdminReceiptStudioDetails
{
    public string Name { get; set; } = "Ethos Dance Studio";
    public string Address { get; set; } = "Ethos Dance Studio, Main Studio";
    public string City { get; set; } = "Hyderabad, Telangana";
    public string Phone { get; set; } = "+91 8466021834, +91 9110745710";
    public string Email { get; set; } = "ethosdancestudio@gmail.com";
    public string Website { get; set; } = "https://ethosdance.in";
    public string Instagram { get; set; } = "@ethos_dancestudio";
}

public class AdminReceiptCustomerDetails
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public string CustomerCode { get; set; } = null!;
}

public class AdminReceiptTransactionDetails
{
    public Guid TransactionId { get; set; }
    public int Purpose { get; set; }
    public string PurposeName { get; set; } = null!;
    public string ReferenceId { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? ItemDetails { get; set; }
    public string ItemDescription { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public decimal UnitAmount { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string PaymentMethod { get; set; } = "Online / Razorpay";
    public bool IsGatewayVerified { get; set; }
    public bool IsReconciled { get; set; }
    public string DisplayStatus { get; set; } = "Paid";
}

public class AdminTrainerPayoutResponse
{
    public AdminTrainerPayoutSummary Summary { get; set; } = new();
    public List<AdminTrainerPayoutItem> Payouts { get; set; } = new();
}

public class AdminTrainerPayoutSummary
{
    public int TotalTrainers { get; set; }
    public decimal TotalGrossRevenue { get; set; }
    public decimal TotalTrainerPayouts { get; set; }
    public decimal TotalStudioRetention { get; set; }
}

public class AdminTrainerPayoutItem
{
    public Guid TrainerId { get; set; }
    public Guid UserId { get; set; }
    public string TrainerCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string TierCode { get; set; } = null!;
    public string TierName { get; set; } = null!;
    public decimal TrainerSharePercentage { get; set; }
    public decimal StudioSharePercentage { get; set; }
    public int TotalWorkshops { get; set; }
    public int TotalBookings { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal TrainerPayoutAmount { get; set; }
    public decimal StudioRetentionAmount { get; set; }
    public string Status { get; set; } = "CALCULATED"; // "CALCULATED", "APPROVED", "PROCESSING", "PROCESSED"
    public string? PayoutReference { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessedByAdmin { get; set; }
    public bool HasConfiguredCommission { get; set; } = true;
    public string? Notes { get; set; }
}

public class AdminProcessTrainerPayoutRequest
{
    public decimal Amount { get; set; }
    public string PayoutReference { get; set; } = null!;
    public string? Notes { get; set; }
}

public sealed class AdminDailyActivityResponse
{
    public DateOnly Date { get; set; }

    public AdminDailyActivitySummary Summary { get; set; } = new();

    public List<AdminDailyStudentItem> Students { get; set; } = [];

    public List<AdminDailyWorkshopBookingItem> WorkshopBookings { get; set; } = [];

    public List<AdminDailyClassBookingItem> ClassBookings { get; set; } = [];

    public List<AdminDailyPaymentItem> Payments { get; set; } = [];

    public List<AdminDailySecurityEventItem> SecurityEvents { get; set; } = [];

    public List<AdminDailyActivityItem> Activity { get; set; } = [];
}

public sealed class AdminDailyActivitySummary
{
    public int StudentsRegistered { get; set; }

    public int ClassEnrollments { get; set; }

    public int ClassBookings { get; set; }

    public int WorkshopBookings { get; set; }

    public int WorkshopsCreated { get; set; }

    public int AdminActions { get; set; }

    public int SecurityEvents { get; set; }

    public decimal Revenue { get; set; }

    public int FailedPayments { get; set; }
}

public sealed class AdminDailyStudentItem
{
    public Guid StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string? MobileNumber { get; set; }

    public DateTime RegisteredAt { get; set; }
}

public sealed class AdminDailyWorkshopBookingItem
{
    public Guid BookingId { get; set; }

    public string WorkshopName { get; set; } = string.Empty;

    public string TrainerName { get; set; } = string.Empty;

    public string StudentName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime BookedAt { get; set; }
}

public sealed class AdminDailyClassBookingItem
{
    public Guid BookingId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string StudentName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime BookedAt { get; set; }
}

public sealed class AdminDailyPaymentItem
{
    public Guid PaymentId { get; set; }

    public string? RazorpayOrderId { get; set; }

    public string? RazorpayPaymentId { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public sealed class AdminDailySecurityEventItem
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string? Actor { get; set; }

    public string? TraceId { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class AdminDailyActivityItem
{
    public string Source { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? Entity { get; set; }

    public string? Outcome { get; set; }

    public string? Actor { get; set; }

    public string? TraceId { get; set; }

    public DateTime Timestamp { get; set; }
}



