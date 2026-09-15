using Ethos.Api.Contracts.Dashboard;

namespace Ethos.Api.Application.Dashboard;

public interface IStudentDashboardService
{
    Task<StudentDashboardResponse> GetStudentDashboardAsync();
}
