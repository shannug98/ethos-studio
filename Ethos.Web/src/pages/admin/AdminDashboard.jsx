import React, { useState, useEffect } from "react";
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

  // 1. Data States
  const [loading, setLoading] = useState(true);
  const [trendRange, setTrendRange] = useState("last6months");
  const [revenuePeriod, setRevenuePeriod] = useState("thisMonth");

  const [summary, setSummary] = useState({
    totalBookings: 428,
    bookingsGrowthPercent: 12,
    totalRevenue: 324580,
    revenueGrowthPercent: 18,
    upcomingWorkshopsCount: 8,
    workshopsThisWeekCount: 2,
    unreadMessagesCount: 3,
    messagesGrowthPercent: -40,
    pendingActionsCount: 6,
    failedPaymentsCount: 3,
  });

  const [trends, setTrends] = useState({
    range: "last6months",
    dataPoints: [
      { label: "Jan", bookingsCount: 25, revenueAmount: 120000 },
      { label: "Feb", bookingsCount: 38, revenueAmount: 175000 },
      { label: "Mar", bookingsCount: 52, revenueAmount: 210000 },
      { label: "Apr", bookingsCount: 64, revenueAmount: 260000 },
      { label: "May", bookingsCount: 72, revenueAmount: 290000 },
      { label: "Jun", bookingsCount: 85, revenueAmount: 324580 },
    ],
  });

  const [workshopStatus, setWorkshopStatus] = useState({
    publishedCount: 14,
    scheduledCount: 5,
    draftCount: 3,
    completedCount: 2,
    archivedCount: 0,
    cancelledCount: 0,
    totalCount: 24,
  });

  const [priorities, setPriorities] = useState([
    { id: "1", type: "WORKSHOPS_AWAITING", count: 2, title: "2 workshops awaiting publication", subtitle: "Review and publish →", actionUrl: "/admin_portal/workshops", severity: "WARNING" },
    { id: "2", type: "FAILED_PAYMENTS", count: 3, title: "3 failed payments", subtitle: "Check and follow up →", actionUrl: "/admin_portal/payments", severity: "DANGER" },
    { id: "3", type: "UNREAD_MESSAGES", count: 3, title: "3 unread contact messages", subtitle: "Respond to enquiries →", actionUrl: "/admin_portal/communications", severity: "WARNING" },
    { id: "4", type: "MEDIA_PENDING", count: 1, title: "1 media item pending review", subtitle: "Approve or reject →", actionUrl: "/admin_portal/videos", severity: "INFO" },
    { id: "5", type: "NEW_REGISTRATIONS", count: 1, title: "1 new user registration", subtitle: "Review user details →", actionUrl: "/admin_portal/users", severity: "INFO" },
  ]);

  const [recentBookings, setRecentBookings] = useState([
    { bookingId: "b1", customerNameMasked: "Aarav Mehta", workshopTitle: "Hip Hop Intensive", formattedDate: "24 Jun 2025", formattedAmount: "₹ 2,500", paymentStatus: "Paid" },
    { bookingId: "b2", customerNameMasked: "Priya Sharma", workshopTitle: "Contemporary Flow", formattedDate: "23 Jun 2025", formattedAmount: "₹ 1,800", paymentStatus: "Paid" },
    { bookingId: "b3", customerNameMasked: "Rohan Kapoor", workshopTitle: "Kids Dance Camp", formattedDate: "23 Jun 2025", formattedAmount: "₹ 3,000", paymentStatus: "Pending" },
    { bookingId: "b4", customerNameMasked: "Sneha Iyer", workshopTitle: "Bharatanatyam Basics", formattedDate: "22 Jun 2025", formattedAmount: "₹ 2,000", paymentStatus: "Paid" },
    { bookingId: "b5", customerNameMasked: "Kunal Desai", workshopTitle: "Advanced Choreography", formattedDate: "22 Jun 2025", formattedAmount: "₹ 2,800", paymentStatus: "Failed" },
  ]);

  const [upcomingWorkshops, setUpcomingWorkshops] = useState([
    { workshopId: "w1", title: "Hip Hop Intensive", formattedDate: "Sat, 28 Jun 2025 · 10:00 AM", bookedSeats: 32, capacity: 40, occupancyPercentage: 80.0, thumbnailUrl: "/images/classes/hiphop.jpg" },
    { workshopId: "w2", title: "Contemporary Flow", formattedDate: "Sun, 29 Jun 2025 · 11:00 AM", bookedSeats: 18, capacity: 30, occupancyPercentage: 60.0, thumbnailUrl: "/images/classes/contemporary.jpg" },
    { workshopId: "w3", title: "Kids Dance Camp", formattedDate: "Sat, 05 Jul 2025 · 09:00 AM", bookedSeats: 12, capacity: 20, occupancyPercentage: 60.0, thumbnailUrl: "/images/classes/kids.jpg" },
    { workshopId: "w4", title: "Bollywood Beats", formattedDate: "Sun, 06 Jul 2025 · 05:00 PM", bookedSeats: 25, capacity: 30, occupancyPercentage: 83.3, thumbnailUrl: "/images/classes/bollywood.jpg" },
  ]);

  const [systemHealth, setSystemHealth] = useState({
    overallStatus: "Operational",
    formattedLastChecked: "24 Jun 2025, 10:42 AM",
    subsystems: [
      { key: "website_api", name: "Website & API", status: "Operational" },
      { key: "database", name: "Database", status: "Operational" },
      { key: "payment_provider", name: "Payment Provider", status: "Operational" },
      { key: "whatsapp_provider", name: "WhatsApp Provider", status: "Operational" },
      { key: "storage", name: "Storage", status: "Operational" },
    ],
  });

  const [recentActivity, setRecentActivity] = useState([
    { id: "a1", action: "Workshop published: Contemporary Flow", actorNameMasked: "by admin@ethos.com", formattedTime: "10:15 AM", type: "WORKSHOP" },
    { id: "a2", action: "Payment confirmed: ₹ 2,500", actorNameMasked: "by system", formattedTime: "09:48 AM", type: "PAYMENT" },
    { id: "a3", action: "New message received", actorNameMasked: "from neha.k@example.com", formattedTime: "09:20 AM", type: "MESSAGE" },
    { id: "a4", action: "Media uploaded: workshop-banner.jpg", actorNameMasked: "by admin@ethos.com", formattedTime: "08:55 AM", type: "MEDIA" },
    { id: "a5", action: "User registered: arav@abc.com", actorNameMasked: "by system", formattedTime: "08:30 AM", type: "USER" },
  ]);

  const [revenueOverview, setRevenueOverview] = useState({
    formattedCurrentMonthRevenue: "₹ 3,24,580",
    monthGrowthPercent: 18,
    weeklyBreakdown: [
      { weekLabel: "Week 1", revenueAmount: 48000 },
      { weekLabel: "Week 2", revenueAmount: 65000 },
      { weekLabel: "Week 3", revenueAmount: 98000 },
      { weekLabel: "Week 4", revenueAmount: 113580 },
    ],
  });

  // 2. Fetch Initial Bounded Data
  useEffect(() => {
    let isMounted = true;
    setLoading(true);

    Promise.allSettled([
      adminApi.getDashboardSummary(),
      adminApi.getDashboardTrends(trendRange),
      adminApi.getWorkshopStatusDonut(),
      adminApi.getDashboardPriorities(),
      adminApi.getRecentBookings(5),
      adminApi.getUpcomingWorkshops(4),
      adminApi.getSystemHealth(),
      adminApi.getRecentActivity(5),
      adminApi.getRevenueOverview(),
    ]).then((results) => {
      if (!isMounted) return;

      if (results[0].status === "fulfilled" && results[0].value) {
        setSummary(results[0].value);
      }
      if (results[1].status === "fulfilled" && results[1].value) {
        setTrends(results[1].value);
      }
      if (results[2].status === "fulfilled" && results[2].value) {
        setWorkshopStatus(results[2].value);
      }
      if (results[3].status === "fulfilled" && results[3].value?.items) {
        setPriorities(results[3].value.items);
      }
      if (results[4].status === "fulfilled" && Array.isArray(results[4].value)) {
        setRecentBookings(results[4].value);
      }
      if (results[5].status === "fulfilled" && Array.isArray(results[5].value)) {
        setUpcomingWorkshops(results[5].value);
      }
      if (results[6].status === "fulfilled" && results[6].value) {
        setSystemHealth(results[6].value);
      }
      if (results[7].status === "fulfilled" && Array.isArray(results[7].value)) {
        setRecentActivity(results[7].value);
      }
      if (results[8].status === "fulfilled" && results[8].value) {
        setRevenueOverview(results[8].value);
      }

      setLoading(false);
    });

    return () => {
      isMounted = false;
    };
  }, []);

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
    { name: "Published", value: workshopStatus.publishedCount || 14, color: WORKSHOP_COLORS.Published },
    { name: "Scheduled", value: workshopStatus.scheduledCount || 5, color: WORKSHOP_COLORS.Scheduled },
    { name: "Draft", value: workshopStatus.draftCount || 3, color: WORKSHOP_COLORS.Draft },
    { name: "Completed", value: workshopStatus.completedCount || 2, color: WORKSHOP_COLORS.Completed },
    { name: "Archived", value: workshopStatus.archivedCount || 0, color: WORKSHOP_COLORS.Archived },
  ].filter((item) => item.value > 0);

  const totalWorkshopsCount = workshopStatus.totalCount || 24;

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
            <div className="kpi-value">{summary.totalBookings?.toLocaleString() || 428}</div>
            <div className="kpi-subtext growth-up">
              <span className="subtext-arrow">↑</span> {summary.bookingsGrowthPercent || 12}% vs last month
            </div>
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
            <div className="kpi-value">₹ {summary.totalRevenue?.toLocaleString("en-IN") || "3,24,580"}</div>
            <div className="kpi-subtext growth-up">
              <span className="subtext-arrow">↑</span> {summary.revenueGrowthPercent || 18}% vs last month
            </div>
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
            <div className="kpi-value">{summary.upcomingWorkshopsCount || 8}</div>
            <div className="kpi-subtext normal">
              {summary.workshopsThisWeekCount || 2} this week
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
            <div className="kpi-value">{summary.unreadMessagesCount || 3}</div>
            <div className="kpi-subtext growth-down">
              <span className="subtext-arrow">↓</span> {Math.abs(summary.messagesGrowthPercent || 40)}% vs last week
            </div>
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
            <div className="kpi-value">{summary.pendingActionsCount || 6}</div>
            <div className="kpi-subtext attention">
              Needs attention
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
          </div>
        </div>

        {/* Col 2: Workshop Status Donut Chart */}
        <div className="ethos-card workshop-status-card">
          <div className="card-header">
            <h3 className="card-title">Workshop Status</h3>
          </div>
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
                <span className="legend-count">{workshopStatus.publishedCount || 14}</span>
              </div>
              <div className="legend-row">
                <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Scheduled }}></span>
                <span className="legend-name">Scheduled</span>
                <span className="legend-count">{workshopStatus.scheduledCount || 5}</span>
              </div>
              <div className="legend-row">
                <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Draft }}></span>
                <span className="legend-name">Draft</span>
                <span className="legend-count">{workshopStatus.draftCount || 3}</span>
              </div>
              <div className="legend-row">
                <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Completed }}></span>
                <span className="legend-name">Completed</span>
                <span className="legend-count">{workshopStatus.completedCount || 2}</span>
              </div>
              <div className="legend-row">
                <span className="legend-dot" style={{ background: WORKSHOP_COLORS.Archived }}></span>
                <span className="legend-name">Archived</span>
                <span className="legend-count">{workshopStatus.archivedCount || 0}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Col 3: Today's Priorities */}
        <div className="ethos-card priorities-card">
          <div className="card-header">
            <h3 className="card-title">Today's Priorities</h3>
            <Link to="/admin_portal/incidents" className="card-view-all-link">View All</Link>
          </div>
          <div className="priorities-list">
            {priorities.map((item) => (
              <div
                key={item.id}
                className="priority-item-row"
                onClick={() => navigate(item.actionUrl)}
                role="button"
                tabIndex={0}
              >
                <div className={`priority-icon-pill ${item.severity.toLowerCase()}`}>
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
                      <span className={`status-pill ${b.paymentStatus.toLowerCase()}`}>
                        {b.paymentStatus}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Col 2: Upcoming Workshops */}
        <div className="ethos-card upcoming-workshops-card">
          <div className="card-header">
            <h3 className="card-title">Upcoming Workshops</h3>
            <Link to="/admin_portal/workshops" className="card-view-all-link">View All</Link>
          </div>
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
                      style={{ width: `${Math.min(100, w.occupancyPercentage || 75)}%` }}
                    ></div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Col 3: System Health */}
        <div className="ethos-card system-health-card">
          <div className="card-header">
            <h3 className="card-title">System Health</h3>
            <span className="health-all-ok-pill">● All Systems Operational</span>
          </div>
          <div className="subsystems-list">
            {systemHealth.subsystems?.map((sub) => (
              <div key={sub.key} className="subsystem-row">
                <div className="subsystem-name-group">
                  <span className="subsystem-check-icon">✓</span>
                  <span className="subsystem-title">{sub.name}</span>
                </div>
                <span className="subsystem-badge-operational">{sub.status || "Operational"}</span>
              </div>
            ))}
          </div>
          <div className="health-footer-timestamp">
            Last checked: {systemHealth.formattedLastChecked || "24 Jun 2025, 10:42 AM"}
          </div>
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
        </div>

        {/* Col 2: Revenue Overview Bar Chart */}
        <div className="ethos-card revenue-overview-card">
          <div className="card-header">
            <div>
              <h3 className="card-title">Revenue Overview</h3>
              <div className="revenue-headline-wrap">
                <span className="revenue-main-number">{revenueOverview.formattedCurrentMonthRevenue || "₹ 3,24,580"}</span>
                <span className="revenue-growth-pill">↑ {revenueOverview.monthGrowthPercent || 18}% vs last month</span>
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
