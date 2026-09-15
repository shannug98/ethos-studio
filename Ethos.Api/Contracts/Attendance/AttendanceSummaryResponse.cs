namespace Ethos.Api.Contracts.Attendance;

public class AttendanceSummaryResponse
{
    public int TotalSessions { get; set; }

    public int Present { get; set; }

    public int Absent { get; set; }

    public int Late { get; set; }

    public int Excused { get; set; }

    public double AttendancePercentage { get; set; }
}
