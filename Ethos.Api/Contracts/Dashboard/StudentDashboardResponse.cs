using Ethos.Api.Contracts.Attendance;
using Ethos.Api.Contracts.Feedback;
using Ethos.Api.Contracts.Packages;
using Ethos.Api.Contracts.Payments;
using Ethos.Api.Contracts.Students;
using Ethos.Api.Contracts.Workshops;

namespace Ethos.Api.Contracts.Dashboard;

public class StudentDashboardResponse
{
    public StudentProfileResponse Profile { get; set; } = null!;

    public StudentPackageResponse? ActivePackage { get; set; }

    public List<UpcomingClassSessionResponse> UpcomingClasses { get; set; } = [];

    public List<WorkshopBookingResponse> UpcomingWorkshops { get; set; } = [];

    public LearningActivitySummaryResponse LearningActivity { get; set; } = new();

    public AttendanceSummaryResponse? AttendanceSummary { get; set; }

    public List<PaymentTransactionResponse> RecentPayments { get; set; } = [];

    public int UnreadNotifications { get; set; }
}
