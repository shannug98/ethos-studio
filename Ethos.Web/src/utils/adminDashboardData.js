export function normalizeAdminDashboardResponse(response) {
  const payload = response?.data ?? response ?? {};

  const summary =
    payload.summary ??
    payload.overview ??
    payload.metrics ??
    payload.kpis ??
    payload;

  const rawActivity =
    payload.activity?.series ??
    payload.activity?.Series ??
    payload.activity ??
    payload.activityTrend ??
    payload.telemetry ??
    payload.dailyActivity ??
    payload.data?.activity?.series ??
    payload.data?.activity ??
    payload.data?.items ??
    [];

  const activity = Array.isArray(rawActivity)
    ? rawActivity
    : rawActivity && typeof rawActivity === "object"
    ? rawActivity.series || rawActivity.Series || rawActivity.items || rawActivity.data || []
    : [];

  const rawUsers =
    payload.userDistribution ??
    payload.userBreakdown ??
    payload.users ??
    [];

  const rawRevenue =
    payload.revenueOverview ??
    payload.revenueTrend ??
    payload.revenue ??
    activity;

  const normalizedSummary = {
    students: Number(
      summary.students ??
      summary.studentCount ??
      summary.totalStudents ??
      summary.activeStudents ??
      0
    ),

    trainers: Number(
      summary.trainers ??
      summary.trainerCount ??
      summary.totalTrainers ??
      summary.activeTrainers ??
      0
    ),

    danceClasses: Number(
      summary.danceClasses ??
      summary.classes ??
      summary.classCount ??
      summary.activeClasses ??
      0
    ),

    workshops: Number(
      summary.workshops ??
      summary.workshopCount ??
      summary.totalWorkshops ??
      summary.upcomingWorkshops ??
      0
    ),

    todayRevenue: Number(
      summary.todayRevenue ??
      summary.revenueToday ??
      summary.revenue ??
      0
    ),

    todayBookings: Number(
      summary.todayBookings ??
      summary.bookingsToday ??
      0
    ),

    totalEnrollments: Number(
      summary.totalEnrollments ??
      summary.enrollments ??
      0
    ),

    activeStudents: Number(
      summary.activeStudents ??
      summary.students ??
      0
    ),

    pendingTrainerApplications: Number(
      summary.pendingTrainerApplications ??
      0
    ),

    pendingWorkshops: Number(
      summary.pendingWorkshops ??
      0
    )
  };

  const normalizedActivity = Array.isArray(activity)
    ? activity.map((item) => ({
        date: item.date ?? item.Date ?? item.day ?? item.activityDate ?? "",
        adminActions: Number(
          item.adminActions ??
          item.AdminActions ??
          item.adminActionCount ??
          item.actions ??
          0
        ),
        workshopBookings: Number(
          item.workshopBookings ??
          item.WorkshopBookings ??
          item.bookings ??
          item.Bookings ??
          item.bookingCount ??
          0
        ),
        revenue: Number(
          item.revenue ??
          item.Revenue ??
          item.revenueAmount ??
          item.totalRevenue ??
          0
        ),
        workshops: Number(
          item.workshops ??
          item.Workshops ??
          item.workshopCount ??
          0
        ),
        securityEvents: Number(
          item.securityEvents ??
          item.SecurityEvents ??
          item.securityEventCount ??
          item.security ??
          0
        )
      }))
    : [];

  const normalizedUsers = Array.isArray(rawUsers) && rawUsers.length > 0
    ? rawUsers.map((item) => ({
        name: item.name ?? item.label ?? item.role ?? "Other",
        value: Number(item.value ?? item.count ?? item.total ?? 0)
      }))
    : [
        { name: "Students", value: normalizedSummary.students },
        { name: "Trainers", value: normalizedSummary.trainers }
      ];

  const normalizedRevenue = Array.isArray(rawRevenue)
    ? rawRevenue.map((item) => ({
        date: item.date ?? item.Date ?? item.day ?? item.activityDate ?? "",
        revenue: Number(
          item.revenue ??
          item.Revenue ??
          item.revenueAmount ??
          item.totalRevenue ??
          0
        )
      }))
    : normalizedActivity.map((item) => ({
        date: item.date,
        revenue: item.revenue
      }));

  return {
    summary: normalizedSummary,
    activity: normalizedActivity,
    users: normalizedUsers,
    revenue: normalizedRevenue
  };
}

export function formatIndianCurrency(value) {
  const amount = Number(value ?? 0);

  return `₹${amount.toLocaleString("en-IN", {
    maximumFractionDigits: 2
  })}`;
}
