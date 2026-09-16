import React, { useState, useEffect, useCallback } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  ResponsiveContainer,
  LineChart,
  Line,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  Tooltip,
} from "recharts";
import { getAdminUser, adminApi } from "../../services/adminApi";
import "./AdminDashboard.css";

// Workshop Status Color Mapping per Visual Reference
const WORKSHOP_COLORS = {
  Published: "#2563eb", // Royal blue
  Scheduled: "#06b6d4", // Cyan
  Draft: "#f59e0b",     // Amber
  Completed: "#10b981", // Emerald
  Archived: "#9ca3af",  // Slate gray
  Cancelled: "#ef4444", // Red
};

export default function AdminDashboard() {
  const navigate = useNavigate();
  const adminUser = getAdminUser();

  // 1. Data States (Zero & Empty Initial States - No Mock Data)
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [trendRange, setTrendRange] = useState("last6months");
  const [revenuePeriod, setRevenuePeriod] = useState("thisMonth");

  const [summary, setSummary] = useState({
    totalBookings: 0,
    bookingsGrowthPercent: 0,
    totalRevenue: 0,
    revenueGrowthPercent: 0,
    upcomingWorkshopsCount: 0,
    workshopsThisWeekCount: 0,
    unreadMessagesCount: 0,
    messagesGrowthPercent: 0,
    pendingActionsCount: 0,
    failedPaymentsCount: 0,
  });

  const [trends, setTrends] = useState({
    range: "last6months",
    dataPoints: [],
  });

  const [workshopStatus, setWorkshopStatus] = useState({
    publishedCount: 0,
    scheduledCount: 0,
    draftCount: 0,
    completedCount: 0,
    archivedCount: 0,
    cancelledCount: 0,
    totalCount: 0,
  });

  const [priorities, setPriorities] = useState([]);
  const [recentBookings, setRecentBookings] = useState([]);
  const [upcomingWorkshops, setUpcomingWorkshops] = useState([]);
  const [systemHealth, setSystemHealth] = useState({
    overallStatus: "Operational",
    formattedLastChecked: "",
    subsystems: [],
  });
  const [recentActivity, setRecentActivity] = useState([]);
  const [revenueOverview, setRevenueOverview] = useState({
    formattedCurrentMonthRevenue: "₹ 0",
    monthGrowthPercent: 0,
    weeklyBreakdown: [],
  });

  // 2. Fetch Initial Real DB Data
  const loadDashboardData = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const results = await Promise.allSettled([
        adminApi.getDashboardSummary(),
        adminApi.getDashboardTrends(trendRange),
        adminApi.getWorkshopStatusDonut(),
        adminApi.getDashboardPriorities(),
        adminApi.getRecentBookings(5),
        adminApi.getUpcomingWorkshops(4),
        adminApi.getSystemHealth(),
        adminApi.getRecentActivity(5),
        adminApi.getRevenueOverview(),
      ]);

      const [
        summaryRes,
        trendsRes,
        donutRes,
        prioritiesRes,
        bookingsRes,
        workshopsRes,
        healthRes,
        activityRes,
        revenueRes,
      ] = results;

      // Count rejections
      const failedCount = results.filter((r) => r.status === "rejected").length;
      if (failedCount >= 5) {
        setError("Unable to connect to Ethos Admin API. Please check your backend connection or session.");
      }

      if (summaryRes.status === "fulfilled" && summaryRes.value) {
        setSummary(summaryRes.value);
      }
      if (trendsRes.status === "fulfilled" && trendsRes.value) {
        setTrends(trendsRes.value);
      }
      if (donutRes.status === "fulfilled" && donutRes.value) {
        setWorkshopStatus(donutRes.value);
      }
      if (prioritiesRes.status === "fulfilled" && prioritiesRes.value?.items) {
        setPriorities(prioritiesRes.value.items);
      }
      if (bookingsRes.status === "fulfilled" && Array.isArray(bookingsRes.value)) {
        setRecentBookings(bookingsRes.value);
      }
      if (workshopsRes.status === "fulfilled" && Array.isArray(workshopsRes.value)) {
        setUpcomingWorkshops(workshopsRes.value);
      }
      if (healthRes.status === "fulfilled" && healthRes.value) {
        setSystemHealth(healthRes.value);
      }
      if (activityRes.status === "fulfilled" && Array.isArray(activityRes.value)) {
        setRecentActivity(activityRes.value);
      }
      if (revenueRes.status === "fulfilled" && revenueRes.value) {
        setRevenueOverview(revenueRes.value);
      }
    } catch (err) {
      setError(err?.message || "Failed to load dashboard data.");
    } finally {
      setLoading(false);
    }
  }, [trendRange]);

  useEffect(() => {
    loadDashboardData();
  }, [loadDashboardData]);

  // Handle Trends Range Change
  const handleTrendRangeChange = (newRange) => {
    setTrendRange(newRange);
    adminApi
      .getDashboardTrends(newRange)
      .then((res) => {
        if (res) setTrends(res);
      })
      .catch(() => {});
  };

  // Prepare Donut Chart Data
  const donutData = [
    { name: "Published", value: workshopStatus?.publishedCount ?? 0, color: WORKSHOP_COLORS.Published },
    { name: "Scheduled", value: workshopStatus?.scheduledCount ?? 0, color: WORKSHOP_COLORS.Scheduled },
    { name: "Draft", value: workshopStatus?.draftCount ?? 0, color: WORKSHOP_COLORS.Draft },
    { name: "Completed", value: workshopStatus?.completedCount ?? 0, color: WORKSHOP_COLORS.Completed },
    { name: "Archived", value: workshopStatus?.archivedCount ?? 0, color: WORKSHOP_COLORS.Archived },
  ].filter((item) => item.value > 0);

  const totalWorkshopsCount = Number(workshopStatus?.totalCount ?? 0);

  const getActivityIcon = (type) => {
    switch (type) {
      case "WORKSHOP":
        return "🎪";
      case "PAYMENT":
        return "💳";
      case "MESSAGE":
        return "💬";
      case "MEDIA":
        return "🎬";
      case "USER":
        return "👤";
      default:
        return "⚡";
    }
  };

  const isAllHealthy =
    systemHealth?.overallStatus === "Operational" &&
    (!systemHealth.subsystems || systemHealth.subsystems.every((s) => s.status === "Operational"));

  return (
    <div className="ethos-dashboard-page">
      {/* 1. HERO GREETING HEADER */}
      <div className="ethos-dashboard-hero">
        <div className="hero-left">
          <h1 className="hero-title">
            Good Morning, {adminUser?.fullName?.split(" ")[0] || "Admin"} <span className="hero-wave">👋</span>
          </h1>
          <p className="hero-subtitle">
            Here's what's happening at Ethos Dance Studio today.
          </p>
        </div>
        <div className="hero-right">
          <blockquote className="hero-quote">
            "Dance empowers, and so does a well-run system."
          </blockquote>
        </div>
      </div>

      {/* Error Banner with Retry */}
      {error && (
        <div className="dashboard-error-banner">
          <div className="error-message-content">
            <span className="error-icon">⚠️</span>
            <span>{error}</span>
          </div>
          <button type="button" className="dashboard-retry-btn" onClick={loadDashboardData}>
            Retry Connection ↻
          </button>
        </div>
      )}

      {/* 2. ACTION-FOCUSED KPI CARDS (5 Cards) */}
      <div className="ethos-kpi-grid">
        {/* Card 1: Total Bookings */}
        <div
          className="ethos-kpi-card tone-purple"
          onClick={() => navigate("/admin_portal/bookings")}
          role="button"
          tabIndex={0}
        >
          <div className="kpi-icon-wrap">👥</div>
          <div className="kpi-content">
            <span className="kpi-label">Total Bookings</span>
            <div className="kpi-value">{Number(summary?.totalBookings ?? 0).toLocaleString()}</div>
            {summary?.bookingsGrowthPercent ? (
              <div className={`kpi-subtext ${summary.bookingsGrowthPercent >= 0 ? "growth-up" : "growth-down"}`}>
                <span className="subtext-arrow">{summary.bookingsGrowthPercent >= 0 ? "↑" : "↓"}</span>{" "}
                {Math.abs(summary.bookingsGrowthPercent)}% vs last month
              </div>
            ) : (
              <div className="kpi-subtext normal">0% vs last month</div>
            )}
          </div>
        </div>

        {/* Card 2: Total Revenue */}
        <div
          className="ethos-kpi-card tone-teal"
          onClick={() => navigate("/admin_portal/payments")}
          role="button"
          tabIndex={0}
        >
          <div className="kpi-icon-wrap">₹</div>
          <div className="kpi-content">
            <span className="kpi-label">Total Revenue</span>
            <div className="kpi-value">₹ {Number(summary?.totalRevenue ?? 0).toLocaleString("en-IN")}</div>
            {summary?.revenueGrowthPercent ? (
              <div className={`kpi-subtext ${summary.revenueGrowthPercent >= 0 ? "growth-up" : "growth-down"}`}>
                <span className="subtext-arrow">{summary.revenueGrowthPercent >= 0 ? "↑" : "↓"}</span>{" "}
                {Math.abs(summary.revenueGrowthPercent)}% vs last month
              </div>
            ) : (
              <div className="kpi-subtext normal">0% vs last month</div>
            )}
          </div>
        </div>

        {/* Card 3: Upcoming Workshops */}
        <div
          className="ethos-kpi-card tone-peach"
          onClick={() => navigate("/admin_portal/workshops")}
          role="button"
          tabIndex={0}
        >
          <div className="kpi-icon-wrap">📅</div>
          <div className="kpi-content">
            <span className="kpi-label">Upcoming Workshops</span>
            <div className="kpi-value">{Number(summary?.upcomingWorkshopsCount ?? 0)}</div>
            <div className="kpi-subtext normal">
              {Number(summary?.workshopsThisWeekCount ?? 0)} this week
            </div>
          </div>
        </div>

        {/* Card 4: Unread Messages */}
        <div
          className="ethos-kpi-card tone-blue"
          onClick={() => navigate("/admin_portal/communications")}
          role="button"
          tabIndex={0}
        >
          <div className="kpi-icon-wrap">💬</div>
          <div className="kpi-content">
            <span className="kpi-label">Unread Messages</span>
            <div className="kpi-value">{Number(summary?.unreadMessagesCount ?? 0)}</div>
            {summary?.messagesGrowthPercent ? (
              <div className={`kpi-subtext ${summary.messagesGrowthPercent <= 0 ? "growth-up" : "growth-down"}`}>
                <span className="subtext-arrow">{summary.messagesGrowthPercent <= 0 ? "↓" : "↑"}</span>{" "}
                {Math.abs(summary.messagesGrowthPercent)}% vs last week
              </div>
            ) : (
              <div className="kpi-subtext normal">All messages reviewed</div>
            )}
          </div>
        </div>

        {/* Card 5: Pending Actions */}
        <div
          className="ethos-kpi-card tone-rose"
          onClick={() => navigate("/admin_portal/incidents")}
          role="button"
          tabIndex={0}
        >
          <div className="kpi-icon-wrap">❗</div>
          <div className="kpi-content">
            <span className="kpi-label">Pending Actions</span>
            <div className="kpi-value">{Number(summary?.pendingActionsCount ?? 0)}</div>
            <div className={`kpi-subtext ${Number(summary?.pendingActionsCount ?? 0) > 0 ? "attention" : "normal"}`}>
              {Number(summary?.pendingActionsCount ?? 0) > 0 ? "Needs attention" : "All clear"}
            </div>
          </div>
        </div>
      </div>

      {/* 3. ROW 1 GRID (3 Columns): Trends, Donut, Priorities */}
      <div className="ethos-dashboard-row-3col">
        {/* Col 1: Bookings & Revenue Trend Chart */}
        <div className="ethos-card trend-chart-card">
          <div className="card-header">
            <div>
              <h3 className="card-title">Bookings & Revenue Trend</h3>
              <div className="trend-legend-pills">
                <span className="legend-dot dot-bookings"></span>
                <span className="legend-text">Bookings</span>
                <span className="legend-dot dot-revenue"></span>
                <span className="legend-text">Revenue (₹)</span>
              </div>
            </div>
            <select
              className="card-select-dropdown"
              value={trendRange}
              onChange={(e) => handleTrendRangeChange(e.target.value)}
            >
              <option value="last6months">Last 6 Months</option>
              <option value="last30days">Last 30 Days</option>
              <option value="yeartodate">Year to Date</option>
            </select>
          </div>

          <div className="chart-canvas-wrap">
            {trends?.dataPoints && trends.dataPoints.length > 0 ? (
              <ResponsiveContainer width="100%" height={210}>
                <LineChart data={trends.dataPoints} margin={{ top: 10, right: 10, left: -15, bottom: 0 }}>
                  <XAxis
                    dataKey="label"
                    axisLine={{ stroke: "#f1f5f9" }}
                    tickLine={false}
                    tick={{ fontSize: 11, fill: "#94a3b8" }}
                  />
                  <YAxis
                    yAxisId="bookings"
                    axisLine={false}
                    tickLine={false}
                    tick={{ fontSize: 11, fill: "#94a3b8" }}
                  />
                  <YAxis
                    yAxisId="revenue"
                    orientation="right"
                    axisLine={false}
                    tickLine={false}
                    tick={{ fontSize: 10, fill: "#94a3b8" }}
                    tickFormatter={(val) => `₹${(val / 100000).toFixed(0)}L`}
                  />
                  <Tooltip
                    contentStyle={{
                      background: "#ffffff",
                      borderRadius: 8,
                      boxShadow: "0 4px 12px rgba(0,0,0,0.08)",
                      border: "1px solid #e2e8f0",
                      fontSize: 12,
                    }}
                    formatter={(val, name) => [
                      name === "Revenue" ? `₹${Number(val).toLocaleString("en-IN")}` : val,
                      name,
                    ]}
                  />
                  <Bar
                    yAxisId="revenue"
                    dataKey="revenueAmount"
                    name="Revenue"
                    fill="#e0e7ff"
                    radius={[4, 4, 0, 0]}
                    barSize={20}
                  />
                  <Line
                    yAxisId="bookings"
                    type="monotone"
                    dataKey="bookingsCount"
                    name="Bookings"
                    stroke="#6366f1"
                    strokeWidth={2.5}
                    dot={{ r: 3, fill: "#6366f1" }}
                    activeDot={{ r: 5 }}
                  />
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <div className="card-empty-state chart-empty-state">
                <span className="empty-state-icon">📈</span>
                <span className="empty-state-title">No Trend Data Recorded Yet</span>
                <span className="empty-state-subtitle">
                  Historical bookings and revenue trends will populate this chart once bookings are made.
                </span>
              </div>
            )}
          </div>
        </div>

        {/* Col 2: Workshop Status Donut Chart */}
        <div className="ethos-card workshop-status-card">
          <div className="card-header">
            <h3 className="card-title">Workshop Status</h3>
          </div>
          {totalWorkshopsCount > 0 && donutData.length > 0 ? (
            <div className="donut-content-layout">
              <div className="donut-chart-container">
                <ResponsiveContainer width={150} height={150}>
                  <PieChart>
                    <Pie
                      data={donutData}
                      dataKey="value"
                      nameKey="name"
                      innerRadius={46}
                      outerRadius={65}
                      paddingAngle={3}
                      cx="50%"
                      cy="50%"
                    >
                      {donutData.map((entry, idx) => (
                        <Cell key={`donut-${idx}`} fill={entry.color} />
                      ))}
                    </Pie>
                  </PieChart>
                </ResponsiveContainer>
                <div className="donut-center-badge">
                  <span className="donut-center-number">{totalWorkshopsCount}</span>
                  <span className="donut-center-label">Total</span>
                </div>
              </div>

              <div className="donut-legend-list">
                <div className="legend-row">
                  <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Published }}></span>
                  <span className="legend-name">Published</span>
                  <span className="legend-count">{workshopStatus.publishedCount ?? 0}</span>
                </div>
                <div className="legend-row">
                  <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Scheduled }}></span>
                  <span className="legend-name">Scheduled</span>
                  <span className="legend-count">{workshopStatus.scheduledCount ?? 0}</span>
                </div>
                <div className="legend-row">
                  <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Draft }}></span>
                  <span className="legend-name">Draft</span>
                  <span className="legend-count">{workshopStatus.draftCount ?? 0}</span>
                </div>
                <div className="legend-row">
                  <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Completed }}></span>
                  <span className="legend-name">Completed</span>
                  <span className="legend-count">{workshopStatus.completedCount ?? 0}</span>
                </div>
                <div className="legend-row">
                  <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Archived }}></span>
                  <span className="legend-name">Archived</span>
                  <span className="legend-count">{workshopStatus.archivedCount ?? 0}</span>
                </div>
              </div>
            </div>
          ) : (
            <div className="card-empty-state">
              <div className="donut-empty-circle">
                <span className="donut-center-number">0</span>
                <span className="donut-center-label">Total</span>
              </div>
              <span className="empty-state-title">No Workshops Created</span>
              <span className="empty-state-subtitle">Get started by creating your studio's first dance workshop.</span>
              <button
                type="button"
                className="admin-btn-secondary"
                style={{ fontSize: "12px", padding: "6px 12px", marginTop: "8px" }}
                onClick={() => navigate("/admin_portal/workshops")}
              >
                + Create Workshop
              </button>
            </div>
          )}
        </div>

        {/* Col 3: Today's Priorities */}
        <div className="ethos-card priorities-card">
          <div className="card-header">
            <h3 className="card-title">Today's Priorities</h3>
            <Link to="/admin_portal/incidents" className="card-view-all-link">View All</Link>
          </div>
          {priorities && priorities.length > 0 ? (
            <div className="priorities-list">
              {priorities.map((item) => (
                <div
                  key={item.id}
                  className="priority-item-row"
                  onClick={() => navigate(item.actionUrl)}
                  role="button"
                  tabIndex={0}
                >
                  <div className={`priority-icon-pill ${item.severity?.toLowerCase() || "info"}`}>
                    {item.type === "WORKSHOPS_AWAITING" && "🎪"}
                    {item.type === "FAILED_PAYMENTS" && "💳"}
                    {item.type === "UNREAD_MESSAGES" && "💬"}
                    {item.type === "MEDIA_PENDING" && "🎬"}
                    {item.type === "NEW_REGISTRATIONS" && "👤"}
                  </div>
                  <div className="priority-info-col">
                    <div className="priority-title">{item.title}</div>
                    <div className="priority-subtitle">{item.subtitle}</div>
                  </div>
                  <span className="priority-arrow">›</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="card-empty-state">
              <span className="empty-state-icon">✅</span>
              <span className="empty-state-title">All Priorities Up to Date</span>
              <span className="empty-state-subtitle">No pending workshop approvals, failed payments, or unread messages.</span>
            </div>
          )}
        </div>
      </div>

      {/* 4. ROW 2 GRID (3 Columns): Recent Bookings, Upcoming Workshops, System Health */}
      <div className="ethos-dashboard-row-3col">
        {/* Col 1: Recent Bookings Table */}
        <div className="ethos-card recent-bookings-card">
          <div className="card-header">
            <h3 className="card-title">Recent Bookings</h3>
            <Link to="/admin_portal/bookings" className="card-view-all-link">View All</Link>
          </div>
          {recentBookings && recentBookings.length > 0 ? (
            <div className="table-responsive">
              <table className="ethos-table">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Workshop</th>
                    <th>Date</th>
                    <th>Amount</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {recentBookings.map((b) => (
                    <tr key={b.bookingId}>
                      <td className="customer-name-cell">{b.customerNameMasked}</td>
                      <td>{b.workshopTitle}</td>
                      <td className="date-cell">{b.formattedDate}</td>
                      <td className="amount-cell">{b.formattedAmount}</td>
                      <td>
                        <span className={`status-pill ${b.paymentStatus?.toLowerCase() || "pending"}`}>
                          {b.paymentStatus}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="card-empty-state">
              <span className="empty-state-icon">📋</span>
              <span className="empty-state-title">No Bookings Recorded Yet</span>
              <span className="empty-state-subtitle">When students enroll in workshops, their booking records will appear here.</span>
            </div>
          )}
        </div>

        {/* Col 2: Upcoming Workshops */}
        <div className="ethos-card upcoming-workshops-card">
          <div className="card-header">
            <h3 className="card-title">Upcoming Workshops</h3>
            <Link to="/admin_portal/workshops" className="card-view-all-link">View All</Link>
          </div>
          {upcomingWorkshops && upcomingWorkshops.length > 0 ? (
            <div className="upcoming-workshops-list">
              {upcomingWorkshops.map((w) => (
                <div
                  key={w.workshopId}
                  className="workshop-item-row"
                  onClick={() => navigate("/admin_portal/workshops")}
                  role="button"
                  tabIndex={0}
                >
                  <div className="workshop-thumb">
                    {w.thumbnailUrl ? (
                      <img src={w.thumbnailUrl} alt={w.title} onError={(e) => { e.target.style.display = "none"; }} />
                    ) : null}
                    <div className="workshop-thumb-fallback">🩰</div>
                  </div>
                  <div className="workshop-info">
                    <div className="workshop-title">{w.title}</div>
                    <div className="workshop-meta">{w.formattedDate}</div>
                  </div>
                  <div className="workshop-occupancy">
                    <div className="seat-count-text">
                      <strong>{w.bookedSeats}</strong> / {w.capacity}
                    </div>
                    <div className="occupancy-progress-bar">
                      <div
                        className="occupancy-progress-fill"
                        style={{ width: `${Math.min(100, w.occupancyPercentage || 0)}%` }}
                      ></div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="card-empty-state">
              <span className="empty-state-icon">📅</span>
              <span className="empty-state-title">No Upcoming Workshops Scheduled</span>
              <span className="empty-state-subtitle">Create and publish workshops to open enrollment for dancers.</span>
              <button
                type="button"
                className="admin-btn-secondary"
                style={{ fontSize: "12px", padding: "6px 12px", marginTop: "8px" }}
                onClick={() => navigate("/admin_portal/workshops")}
              >
                + Schedule Workshop
              </button>
            </div>
          )}
        </div>

        {/* Col 3: System Health */}
        <div className="ethos-card system-health-card">
          <div className="card-header">
            <h3 className="card-title">System Health</h3>
            <span className={isAllHealthy ? "health-all-ok-pill" : "health-warning-pill"}>
              ● {isAllHealthy ? "All Systems Operational" : systemHealth?.overallStatus || "Standby"}
            </span>
          </div>
          <div className="subsystems-list">
            {systemHealth.subsystems && systemHealth.subsystems.length > 0 ? (
              systemHealth.subsystems.map((sub) => {
                const isOp = sub.status === "Operational";
                return (
                  <div key={sub.key} className="subsystem-row">
                    <div className="subsystem-name-group">
                      <span className={isOp ? "subsystem-check-icon" : "subsystem-standby-icon"}>
                        {isOp ? "✓" : "ℹ"}
                      </span>
                      <span className="subsystem-title">{sub.name}</span>
                    </div>
                    <span className={isOp ? "subsystem-badge-operational" : "subsystem-badge-standby"}>
                      {sub.status || "Standby"}
                    </span>
                  </div>
                );
              })
            ) : (
              <div className="card-empty-state">
                <span className="empty-state-icon">🖥️</span>
                <span className="empty-state-title">Checking System Services</span>
                <span className="empty-state-subtitle">Monitoring website, database, and infrastructure endpoints.</span>
              </div>
            )}
          </div>
          {systemHealth.formattedLastChecked && (
            <div className="health-footer-timestamp">
              Last checked: {systemHealth.formattedLastChecked}
            </div>
          )}
        </div>
      </div>

      {/* 5. ROW 3 GRID (3 Columns): Recent Activity, Revenue Overview, Quick Actions */}
      <div className="ethos-dashboard-row-3col">
        {/* Col 1: Recent Activity */}
        <div className="ethos-card recent-activity-card">
          <div className="card-header">
            <h3 className="card-title">Recent Activity</h3>
            <Link to="/admin_portal/audit-logs" className="card-view-all-link">View All</Link>
          </div>
          {recentActivity && recentActivity.length > 0 ? (
            <div className="activity-feed-list">
              {recentActivity.map((act) => (
                <div key={act.id} className="activity-item-row">
                  <span className="activity-item-icon">{getActivityIcon(act.type)}</span>
                  <div className="activity-content-col">
                    <div className="activity-action-text">{act.action}</div>
                    <div className="activity-actor-text">{act.actorNameMasked}</div>
                  </div>
                  <span className="activity-timestamp">{act.formattedTime}</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="card-empty-state">
              <span className="empty-state-icon">⚡</span>
              <span className="empty-state-title">No Recent Activity</span>
              <span className="empty-state-subtitle">Studio administrative actions and events will be logged here.</span>
            </div>
          )}
        </div>

        {/* Col 2: Revenue Overview Bar Chart */}
        <div className="ethos-card revenue-overview-card">
          <div className="card-header">
            <div>
              <h3 className="card-title">Revenue Overview</h3>
              <div className="revenue-headline-wrap">
                <span className="revenue-main-number">{revenueOverview?.formattedCurrentMonthRevenue || "₹ 0"}</span>
                {revenueOverview?.monthGrowthPercent ? (
                  <span className="revenue-growth-pill">
                    {revenueOverview.monthGrowthPercent >= 0 ? "↑" : "↓"} {Math.abs(revenueOverview.monthGrowthPercent)}% vs last month
                  </span>
                ) : (
                  <span className="revenue-neutral-pill">0% vs last month</span>
                )}
              </div>
            </div>
            <select
              className="card-select-dropdown"
              value={revenuePeriod}
              onChange={(e) => setRevenuePeriod(e.target.value)}
            >
              <option value="thisMonth">This Month</option>
              <option value="lastMonth">Last Month</option>
            </select>
          </div>

          <div className="chart-canvas-wrap">
            {revenueOverview?.weeklyBreakdown && revenueOverview.weeklyBreakdown.length > 0 ? (
              <ResponsiveContainer width="100%" height={170}>
                <BarChart data={revenueOverview.weeklyBreakdown} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                  <XAxis
                    dataKey="weekLabel"
                    axisLine={{ stroke: "#f1f5f9" }}
                    tickLine={false}
                    tick={{ fontSize: 11, fill: "#94a3b8" }}
                  />
                  <YAxis
                    axisLine={false}
                    tickLine={false}
                    tick={{ fontSize: 10, fill: "#94a3b8" }}
                    tickFormatter={(val) => `₹${(val / 1000).toFixed(0)}K`}
                  />
                  <Tooltip
                    contentStyle={{
                      background: "#ffffff",
                      borderRadius: 8,
                      boxShadow: "0 4px 12px rgba(0,0,0,0.08)",
                      border: "1px solid #e2e8f0",
                      fontSize: 12,
                    }}
                    formatter={(val) => [`₹${Number(val).toLocaleString("en-IN")}`, "Revenue"]}
                  />
                  <Bar
                    dataKey="revenueAmount"
                    fill="#6366f1"
                    radius={[4, 4, 0, 0]}
                    barSize={28}
                  />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="card-empty-state chart-empty-state">
                <span className="empty-state-icon">💳</span>
                <span className="empty-state-title">No Revenue Recorded</span>
                <span className="empty-state-subtitle">Weekly revenue distribution will show here once bookings are received.</span>
              </div>
            )}
          </div>
        </div>

        {/* Col 3: Quick Actions */}
        <div className="ethos-card quick-actions-card">
          <div className="card-header">
            <h3 className="card-title">Quick Actions</h3>
          </div>
          <div className="quick-actions-grid">
            <button
              type="button"
              className="qa-btn qa-purple"
              onClick={() => navigate("/admin_portal/workshops")}
            >
              <span className="qa-icon">+</span>
              <span className="qa-text">Create Workshop</span>
            </button>

            <button
              type="button"
              className="qa-btn qa-green"
              onClick={() => navigate("/admin_portal/bookings")}
            >
              <span className="qa-icon">📅</span>
              <span className="qa-text">View Bookings</span>
            </button>

            <button
              type="button"
              className="qa-btn qa-blue"
              onClick={() => navigate("/admin_portal/videos")}
            >
              <span className="qa-icon">📁</span>
              <span className="qa-text">Upload Media</span>
            </button>

            <button
              type="button"
              className="qa-btn qa-peach"
              onClick={() => navigate("/admin_portal/communications")}
            >
              <span className="qa-icon">🔔</span>
              <span className="qa-text">Send Notification</span>
            </button>

            <button
              type="button"
              className="qa-btn qa-cyan"
              onClick={() => navigate("/admin_portal/observability")}
            >
              <span className="qa-icon">📊</span>
              <span className="qa-text">View Reports</span>
            </button>

            <button
              type="button"
              className="qa-btn qa-slate"
              onClick={() => navigate("/admin_portal/devices")}
            >
              <span className="qa-icon">⚙️</span>
              <span className="qa-text">System Settings</span>
            </button>
          </div>
        </div>
      </div>

      {/* 6. BRANDED FOOTER */}
      <footer className="ethos-dashboard-footer">
        <div className="footer-left">
          © {new Date().getFullYear()} Ethos Dance Studio. All rights reserved.
        </div>
        <div className="footer-right">
          <Link to="#" className="footer-link">Help</Link>
          <span className="footer-sep">|</span>
          <Link to="#" className="footer-link">Privacy</Link>
          <span className="footer-sep">|</span>
          <Link to="#" className="footer-link">Terms</Link>
        </div>
      </footer>
    </div>
  );
}
