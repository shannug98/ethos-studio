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
  CartesianGrid,
  Tooltip,
  Legend,
} from "recharts";

import "./AdminDashboardCharts.css";

const PRIMARY_INDIGO = "#4f46e5";
const ACCENT_CYAN = "#06b6d4";
const SUCCESS_EMERALD = "#10b981";
const PURPLE = "#8b5cf6";
const TEXT_MUTED = "#64748b";

const PIE_COLORS = [
  "#4f46e5",
  "#06b6d4",
  "#10b981",
  "#f59e0b",
  "#94a3b8",
];

function asArray(value) {
  if (Array.isArray(value)) {
    return value;
  }

  if (!value || typeof value !== "object") {
    return [];
  }

  if (Array.isArray(value.series)) {
    return value.series;
  }

  if (Array.isArray(value.Series)) {
    return value.Series;
  }

  if (Array.isArray(value.items)) {
    return value.items;
  }

  if (Array.isArray(value.data)) {
    return value.data;
  }

  if (Array.isArray(value.result)) {
    return value.result;
  }

  if (Array.isArray(value.records)) {
    return value.records;
  }

  if (Array.isArray(value.rows)) {
    return value.rows;
  }

  /*
   * Some APIs return an object keyed by date:
   *
   * {
   *   "2026-09-05": { ... },
   *   "2026-09-06": { ... }
   * }
   *
   * Convert that shape into an array.
   */
  const objectValues = Object.entries(value);

  if (
    objectValues.length > 0 &&
    objectValues.every(
      ([key, item]) =>
        typeof item === "object" &&
        item !== null &&
        !Array.isArray(item)
    )
  ) {
    return objectValues.map(([key, item]) => ({
      date: item.date ?? item.day ?? key,
      ...item,
    }));
  }

  return [];
}

function getNumber(item, keys) {
  for (const key of keys) {
    const value = item?.[key];

    if (value !== undefined && value !== null) {
      const number = Number(value);

      if (Number.isFinite(number)) {
        return number;
      }
    }
  }

  return 0;
}

function formatCurrency(value) {
  return "₹" + Number(value || 0).toLocaleString("en-IN");
}

function formatDateLabel(value) {
  if (!value) {
    return "";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return String(value).slice(0, 10);
  }

  return date.toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
  });
}

function ChartCard({ title, subtitle, children }) {
  return (
    <section className="admin-chart-card">
      <div className="admin-chart-card-header">
        <div>
          <h3>{title}</h3>

          {subtitle && <p>{subtitle}</p>}
        </div>
      </div>

      <div className="admin-chart-content">{children}</div>
    </section>
  );
}

export default function AdminDashboardCharts({
  activity,
  overview,
}) {
  const activityRows = asArray(activity);

  const normalizedActivity = activityRows.map((item, index) => ({
    label: formatDateLabel(
      item.date ??
        item.day ??
        item.period ??
        item.bucket ??
        item.timestamp ??
        ("Day " + (index + 1))
    ),

    adminActions: getNumber(item, [
      "adminActions",
      "adminActionCount",
      "adminActionTotal",
      "actions",
      "actionCount",
    ]),

    workshopBookings: getNumber(item, [
      "workshopBookings",
      "workshopBookingCount",
      "bookingCount",
      "bookings",
    ]),

    workshops: getNumber(item, [
      "workshops",
      "Workshops",
      "workshopCount",
      "createdWorkshops",
    ]),

    revenue: getNumber(item, [
      "revenue",
      "totalRevenue",
      "paymentRevenue",
      "revenueAmount",
    ]),
  }));

  const overviewObject =
    overview && typeof overview === "object"
      ? overview
      : {};

  const userDistribution = [
    {
      name: "Students",
      value: getNumber(overviewObject, [
        "students",
        "studentCount",
        "totalStudents",
      ]),
    },
    {
      name: "Trainers",
      value: getNumber(overviewObject, [
        "trainers",
        "trainerCount",
        "totalTrainers",
      ]),
    },
    {
      name: "Admins",
      value: getNumber(overviewObject, [
        "admins",
        "adminCount",
        "totalAdmins",
      ]),
    },
    {
      name: "Other",
      value: getNumber(overviewObject, [
        "otherUsers",
        "otherUserCount",
        "totalOtherUsers",
      ]),
    },
  ].filter((item) => item.value > 0);

  const chartData =
    normalizedActivity.length > 0
      ? normalizedActivity
      : [
          {
            label: "No data",
            adminActions: 0,
            workshops: 0,
            workshopBookings: 0,
            revenue: 0,
          },
        ];

  return (
    <div className="admin-dashboard-chart-grid">
      <ChartCard
        title="Platform Activity Trend"
        subtitle="Administrative actions, workshops, bookings, and revenue"
      >
        <ResponsiveContainer width="100%" height={285}>
          <LineChart
            data={chartData}
            margin={{
              top: 12,
              right: 12,
              left: 0,
              bottom: 8,
            }}
          >
            <CartesianGrid
              stroke="#e5e7eb"
              strokeDasharray="3 3"
            />

            <XAxis
              dataKey="label"
              tick={{
                fill: "#6b7280",
                fontSize: 11,
              }}
              axisLine={{
                stroke: "#d1d5db",
              }}
              tickLine={false}
            />

            <YAxis
              yAxisId="count"
              tick={{
                fill: "#6b7280",
                fontSize: 11,
              }}
              axisLine={false}
              tickLine={false}
            />

            <YAxis
              yAxisId="revenue"
              orientation="right"
              tick={{
                fill: "#6b7280",
                fontSize: 11,
              }}
              axisLine={false}
              tickLine={false}
              tickFormatter={(value) => "₹" + value}
            />

            <Tooltip
              contentStyle={{
                border: "1px solid #e5e7eb",
                borderRadius: 10,
                background: "#ffffff",
                color: "#111827",
              }}
              formatter={(value, name) => {
                if (name === "Revenue") {
                  return [formatCurrency(value), name];
                }

                return [value, name];
              }}
            />

            <Legend
              wrapperStyle={{
                fontSize: 11,
                paddingTop: 10,
              }}
            />

            <Line
              yAxisId="count"
              type="monotone"
              dataKey="adminActions"
              name="Admin Actions"
              stroke={PRIMARY_INDIGO}
              strokeWidth={2.5}
              dot={{
                r: 3,
                fill: PRIMARY_INDIGO,
              }}
              activeDot={{
                r: 6,
              }}
            />

            <Line
              yAxisId="count"
              type="monotone"
              dataKey="workshops"
              name="Workshops"
              stroke={PURPLE}
              strokeWidth={2.5}
              dot={{
                r: 3,
                fill: PURPLE,
              }}
              activeDot={{
                r: 5,
              }}
            />

            <Line
              yAxisId="count"
              type="monotone"
              dataKey="workshopBookings"
              name="Workshop Bookings"
              stroke={ACCENT_CYAN}
              strokeWidth={2}
              dot={{
                r: 3,
                fill: ACCENT_CYAN,
              }}
            />

            <Line
              yAxisId="revenue"
              type="monotone"
              dataKey="revenue"
              name="Revenue"
              stroke={SUCCESS_EMERALD}
              strokeWidth={2}
              strokeDasharray="5 5"
              dot={{
                r: 3,
                fill: SUCCESS_EMERALD,
              }}
            />
          </LineChart>
        </ResponsiveContainer>
      </ChartCard>

      <ChartCard
        title="User Distribution"
        subtitle="Current platform account composition"
      >
        {userDistribution.length > 0 ? (
          <ResponsiveContainer width="100%" height={285}>
            <PieChart>
              <Pie
                data={userDistribution}
                dataKey="value"
                nameKey="name"
                cx="42%"
                cy="50%"
                innerRadius={65}
                outerRadius={100}
                paddingAngle={2}
                labelLine={false}
              >
                {userDistribution.map((entry, index) => (
                  <Cell
                    key={entry.name + "-" + index}
                    fill={PIE_COLORS[index % PIE_COLORS.length]}
                  />
                ))}
              </Pie>

              <Tooltip
                contentStyle={{
                  border: "1px solid #e5e7eb",
                  borderRadius: 10,
                  background: "#ffffff",
                  color: "#111827",
                }}
              />

              <Legend
                layout="vertical"
                verticalAlign="middle"
                align="right"
                wrapperStyle={{
                  fontSize: 11,
                  lineHeight: "22px",
                }}
              />
            </PieChart>
          </ResponsiveContainer>
        ) : (
          <div className="admin-chart-empty">
            User distribution data is not available.
          </div>
        )}
      </ChartCard>

      <ChartCard
        title="Revenue Overview"
        subtitle="Daily revenue from the selected period"
      >
        <ResponsiveContainer width="100%" height={285}>
          <BarChart
            data={chartData}
            margin={{
              top: 12,
              right: 12,
              left: 0,
              bottom: 8,
            }}
          >
            <CartesianGrid
              stroke="#e5e7eb"
              strokeDasharray="3 3"
            />

            <XAxis
              dataKey="label"
              tick={{
                fill: "#6b7280",
                fontSize: 11,
              }}
              axisLine={{
                stroke: "#d1d5db",
              }}
              tickLine={false}
            />

            <YAxis
              tick={{
                fill: "#6b7280",
                fontSize: 11,
              }}
              axisLine={false}
              tickLine={false}
              tickFormatter={(value) => "₹" + value}
            />

            <Tooltip
              contentStyle={{
                border: "1px solid #e5e7eb",
                borderRadius: 10,
                background: "#ffffff",
                color: "#111827",
              }}
              formatter={(value) => [
                formatCurrency(value),
                "Revenue",
              ]}
            />

            <Bar
              dataKey="revenue"
              name="Revenue"
              fill={PRIMARY_INDIGO}
              radius={[6, 6, 0, 0]}
              maxBarSize={34}
            />
          </BarChart>
        </ResponsiveContainer>
      </ChartCard>
    </div>
  );
}
