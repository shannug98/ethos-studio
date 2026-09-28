import React, {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from "react";
import { Link } from "react-router-dom";
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  PieChart,
  Pie,
  Cell,
} from "recharts";
import {
  Eye,
  FileText,
  ShoppingCart,
  CreditCard,
  CheckCircle2,
  BarChart3,
  Timer,
  Smartphone,
  Laptop,
  Tablet,
  TrendingUp,
  Info,
  Sparkles,
  MapPin,
  RefreshCw,
  Globe,
  Activity,
  Layers,
} from "lucide-react";
import { adminApi } from "../../services/adminApi";
import RealWorldMap, { projectCoordinates } from "./RealWorldMap";
import "./AdminAnalytics.css";

const RANGE_OPTIONS = [
  { id: "today", label: "Today" },
  { id: "yesterday", label: "Yesterday" },
  { id: "last7days", label: "Last 7 Days" },
  { id: "last30days", label: "Last 30 Days" },
  { id: "last90days", label: "Last 90 Days" },
];

const LIVE_WINDOW_MINUTES = 15;
const LIVE_REFRESH_MS = 15000;

const numberFormat = new Intl.NumberFormat("en-IN");

function formatNumber(value) {
  return numberFormat.format(Number(value || 0));
}

function formatPercent(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return "0%";
  return `${number.toFixed(number % 1 === 0 ? 0 : 1)}%`;
}

function formatDelta(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return null;
  if (number === 0) {
    return { text: "0%", direction: "neutral" };
  }
  return {
    text: `${Math.abs(number).toFixed(Math.abs(number) % 1 === 0 ? 0 : 1)}%`,
    direction: number > 0 ? "up" : "down",
  };
}

function formatDateLabel(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return date.toLocaleDateString("en-IN", { day: "2-digit", month: "short" });
}

function formatTime(value) {
  if (!value) return "--:--";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "--:--";
  return date.toLocaleTimeString("en-IN", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  });
}

function formatDuration(seconds) {
  const value = Number(seconds);
  if (!Number.isFinite(value) || value <= 0) return "--";
  const totalMinutes = Math.floor(value / 60);
  const remainingSeconds = Math.floor(value % 60);
  if (totalMinutes >= 60) {
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return `${hours}h ${minutes}m`;
  }
  return `${totalMinutes}m ${String(remainingSeconds).padStart(2, "0")}s`;
}

function normalizeOverview(response) {
  const data = response || {};
  const funnel = data.funnel || {};

  const visitors = data.visitors ?? data.websiteVisitors ?? funnel.visitors ?? 0;
  const workshopViews = data.workshopViews ?? funnel.workshopViews ?? 0;
  const bookingStarts = data.bookingStarts ?? funnel.bookingStarts ?? 0;
  const paymentAttempts = data.paymentAttempts ?? funnel.paymentAttempts ?? 0;
  const completedBookings = data.completedBookings ?? funnel.completedBookings ?? 0;

  const visitorToWorkshopRate = funnel.workshopViewRate ?? funnel.visitorToWorkshopRate ?? (visitors > 0 ? (workshopViews / visitors) * 100 : 0);
  const workshopToBookingRate = funnel.bookingStartRate ?? funnel.workshopToBookingRate ?? (workshopViews > 0 ? (bookingStarts / workshopViews) * 100 : 0);
  const bookingToPaymentRate = funnel.paymentAttemptRate ?? funnel.bookingToPaymentRate ?? (bookingStarts > 0 ? (paymentAttempts / bookingStarts) * 100 : 0);
  const paymentToCompletionRate = funnel.completedBookingRate ?? funnel.paymentToCompletionRate ?? (paymentAttempts > 0 ? (completedBookings / paymentAttempts) * 100 : 0);
  const overallConversionRate = funnel.overallConversionRate ?? data.overallConversionRate ?? (visitors > 0 ? (completedBookings / visitors) * 100 : 0);

  return {
    visitors,
    workshopViews,
    bookingStarts,
    paymentAttempts,
    completedBookings,
    overallConversionRate: Math.min(100, Math.max(0, overallConversionRate)),
    visitorDelta: data.visitorChangePercent ?? data.visitorDelta ?? null,
    workshopViewsDelta: data.workshopViewChangePercent ?? data.workshopViewsDelta ?? null,
    bookingStartsDelta: data.bookingStartChangePercent ?? data.bookingStartsDelta ?? null,
    paymentAttemptsDelta: data.paymentAttemptChangePercent ?? data.paymentAttemptsDelta ?? null,
    completedBookingsDelta: data.completedBookingChangePercent ?? data.completedBookingsDelta ?? null,
    averageTimeToPurchaseSeconds: data.averageTimeToPurchaseSeconds ?? data.averagePurchaseTimeSeconds ?? 0,
    funnel: {
      visitors,
      workshopViews,
      bookingStarts,
      paymentAttempts,
      completedBookings,
      visitorToWorkshopRate: Math.min(100, Math.max(0, visitorToWorkshopRate)),
      workshopToBookingRate: Math.min(100, Math.max(0, workshopToBookingRate)),
      bookingToPaymentRate: Math.min(100, Math.max(0, bookingToPaymentRate)),
      paymentToCompletionRate: Math.min(100, Math.max(0, paymentToCompletionRate)),
      overallConversionRate: Math.min(100, Math.max(0, overallConversionRate)),
    },
  };
}

function normalizeTrends(response) {
  const points = response?.points || response?.trends || response || [];
  if (Array.isArray(points) && points.length > 0) {
    return points.map((item) => ({
      ...item,
      dateLabel: formatDateLabel(item.dateUtc ?? item.date ?? item.occurredAtUtc),
      visitors: item.visitors ?? item.uniqueVisitors ?? 0,
      workshopViews: item.workshopViews ?? item.views ?? 0,
      bookingStarts: item.bookingStarts ?? item.checkoutStarts ?? 0,
      paymentAttempts: item.paymentAttempts ?? 0,
      completedBookings: item.completedBookings ?? item.completed ?? item.checkoutCompletions ?? 0,
    }));
  }
  return [];
}

function normalizeWorkshops(response) {
  const items = response?.workshops || response?.items || response || [];
  if (Array.isArray(items) && items.length > 0) {
    return items.map((item, idx) => ({
      ...item,
      workshopId: item.workshopId || item.id || idx,
      workshopName: item.title || item.workshopName || item.name || "Workshop",
      imageUrl: item.imageUrl || item.posterUrl || item.thumbnailUrl || null,
      views: item.views ?? item.workshopViews ?? 0,
      bookingStarts: item.bookingStarts ?? item.checkoutStarts ?? 0,
      paymentAttempts: item.paymentAttempts ?? 0,
      completedBookings: item.completedBookings ?? item.completed ?? item.checkoutCompletions ?? 0,
      conversionRate: item.conversionRate ?? (item.views > 0 ? (item.completedBookings / item.views) * 100 : 0),
      trend: item.trend ?? "up",
    }));
  }
  return [];
}

const PAYMENT_COLOR_MAP = {
  successful: "#10B981",
  success: "#10B981",
  paid: "#10B981",
  failed: "#EF4444",
  failure: "#EF4444",
  abandoned: "#F59E0B",
  pending: "#F59E0B",
  initiated: "#3B82F6",
};

function normalizePayments(response) {
  if (!response) return [];

  // If response is already an array of breakdown items:
  if (Array.isArray(response)) {
    return response.map((item, idx) => ({
      label: item.label || item.name || item.status || "Other",
      count: item.count ?? item.total ?? 0,
      percentage: item.percentage ?? item.percent ?? 0,
      color: item.color || PAYMENT_COLOR_MAP[(item.label || item.name || item.status || "").toLowerCase()] || (idx === 0 ? "#10B981" : idx === 1 ? "#EF4444" : "#F59E0B"),
    }));
  }

  // If response has an array property:
  const items = response?.outcomes || response?.payments || response?.items;
  if (Array.isArray(items) && items.length > 0) {
    return items.map((item, idx) => ({
      label: item.label || item.name || item.status || "Other",
      count: item.count ?? item.total ?? 0,
      percentage: item.percentage ?? item.percent ?? 0,
      color: item.color || PAYMENT_COLOR_MAP[(item.label || item.name || item.status || "").toLowerCase()] || (idx === 0 ? "#10B981" : idx === 1 ? "#EF4444" : "#F59E0B"),
    }));
  }

  // If response is PaymentOutcomesDto object:
  const successful = Number(response.successful ?? response.paid ?? 0);
  const failed = Number(response.failed ?? 0);
  const unresolved = Number(response.unresolved ?? response.pending ?? 0);
  const cancelled = Number(response.cancelled ?? 0);
  const refunded = Number(response.refunded ?? 0);
  const total = Number(response.totalAttempts ?? (successful + failed + unresolved + cancelled + refunded));

  if (total === 0 && successful === 0) {
    return [];
  }

  const result = [];
  if (successful > 0) {
    result.push({
      label: "Successful",
      count: successful,
      percentage: response.successfulPercent ?? (total > 0 ? Math.round((successful / total) * 100) : 100),
      color: "#10B981",
    });
  }
  if (failed > 0) {
    result.push({
      label: "Failed",
      count: failed,
      percentage: response.failedPercent ?? (total > 0 ? Math.round((failed / total) * 100) : 0),
      color: "#EF4444",
    });
  }
  if (unresolved > 0) {
    result.push({
      label: "Pending",
      count: unresolved,
      percentage: response.unresolvedPercent ?? (total > 0 ? Math.round((unresolved / total) * 100) : 0),
      color: "#F59E0B",
    });
  }
  if (cancelled > 0) {
    result.push({
      label: "Cancelled",
      count: cancelled,
      percentage: response.cancelledPercent ?? (total > 0 ? Math.round((cancelled / total) * 100) : 0),
      color: "#94A3B8",
    });
  }
  if (refunded > 0) {
    result.push({
      label: "Refunded",
      count: refunded,
      percentage: response.refundedPercent ?? (total > 0 ? Math.round((refunded / total) * 100) : 0),
      color: "#8B5CF6",
    });
  }

  return result;
}

const CHANNEL_COLOR_MAP = {
  instagram: "#D49A3D",
  "google search": "#3B82F6",
  google: "#3B82F6",
  direct: "#60A5FA",
  facebook: "#EC4899",
  whatsapp: "#10B981",
  youtube: "#EF4444",
  twitter: "#0EA5E9",
  x: "#0EA5E9",
  referral: "#8B5CF6",
  other: "#94A3B8",
  others: "#94A3B8",
};

const DEFAULT_CHANNEL_COLORS = ["#D49A3D", "#3B82F6", "#60A5FA", "#EC4899", "#10B981", "#8B5CF6", "#94A3B8"];

function normalizeTrafficSources(response) {
  const sources = response?.sources || response?.trafficSources || response || [];
  if (Array.isArray(sources) && sources.length > 0) {
    return sources.map((item, idx) => {
      const name = item.channel || item.source || item.name || "Direct";
      const key = name.toLowerCase();
      const color = CHANNEL_COLOR_MAP[key] || DEFAULT_CHANNEL_COLORS[idx % DEFAULT_CHANNEL_COLORS.length];
      return {
        name,
        value: item.visitors ?? item.count ?? item.value ?? 0,
        percent: formatPercent(item.percentage ?? item.percent ?? 0),
        rawPercent: item.percentage ?? item.percent ?? 0,
        color,
      };
    });
  }
  return [];
}

function normalizeDevices(response) {
  let mobile = { count: 0, percentage: 0 };
  let desktop = { count: 0, percentage: 0 };
  let tablet = { count: 0, percentage: 0 };

  const list = Array.isArray(response?.devices)
    ? response.devices
    : Array.isArray(response)
    ? response
    : null;

  if (list && list.length > 0) {
    for (const item of list) {
      const dName = (item.device || item.name || "").toLowerCase();
      const pct = Math.round(item.percentage ?? item.percent ?? 0);
      const cnt = item.count ?? 0;
      if (dName.includes("mob")) mobile = { count: cnt, percentage: pct };
      else if (dName.includes("desk")) desktop = { count: cnt, percentage: pct };
      else if (dName.includes("tab")) tablet = { count: cnt, percentage: pct };
    }
  } else {
    const obj = response?.devices || response?.breakdown || response || {};
    mobile = {
      count: obj.mobile?.count ?? 0,
      percentage: Math.round(obj.mobile?.percentage ?? obj.mobilePercent ?? 0),
    };
    desktop = {
      count: obj.desktop?.count ?? 0,
      percentage: Math.round(obj.desktop?.percentage ?? obj.desktopPercent ?? 0),
    };
    tablet = {
      count: obj.tablet?.count ?? 0,
      percentage: Math.round(obj.tablet?.percentage ?? obj.tabletPercent ?? 0),
    };
  }

  return { mobile, desktop, tablet };
}



const LOCATION_COLORS = ["#D49A3D", "#F97316", "#3B82F6", "#EC4899", "#8B5CF6", "#10B981", "#94A3B8"];

function normalizeLocations(response) {
  const items = response?.locations || response?.items || response || [];
  if (Array.isArray(items) && items.length > 0) {
    return items.map((loc, idx) => ({
      country: loc.country || "India",
      region: loc.region || "",
      city: loc.city || loc.locationName || loc.name || "Unknown",
      locationName: loc.locationName || loc.city || "Unknown",
      visitorCount: loc.visitorCount ?? loc.count ?? loc.visitors ?? 0,
      count: loc.visitorCount ?? loc.count ?? loc.visitors ?? 0,
      percentage: loc.percentage ?? loc.percent ?? 0,
      rawPercent: loc.percentage ?? loc.percent ?? 0,
      percent: formatPercent(loc.percentage ?? loc.percent ?? 0),
      latitude: loc.latitude ?? 17.3850,
      longitude: loc.longitude ?? 78.4867,
      color: loc.color || LOCATION_COLORS[idx % LOCATION_COLORS.length],
    }));
  }
  return [];
}

function normalizeActivity(response) {
  const items = response?.activities || response?.events || response?.items || response || [];
  if (Array.isArray(items) && items.length > 0) {
    return items.map((item, index) => ({
      id: item.id || index,
      type: item.activityType || item.type || item.eventType || "view",
      title: item.description || item.title || item.message || "User activity recorded",
      workshopName: item.workshopTitle || item.workshopName || null,
      occurredAtUtc: item.occurredAtUtc || item.timestamp || item.createdAtUtc || new Date().toISOString(),
      code: item.code || null,
    }));
  }
  return [];
}

// Mini Sparkline SVG Generator from real Trend Points
function MiniSparkline({ points = [], dataKey = "visitors", color = "#D49A3D" }) {
  const pathD = useMemo(() => {
    if (!points || points.length < 2) {
      return "M2 18 Q 30 18, 58 18";
    }
    const values = points.map((p) => Number(p[dataKey] || 0));
    const min = Math.min(...values);
    const max = Math.max(...values, 1);
    const range = max - min || 1;

    const width = 56;
    const height = 18;
    const padding = 2;

    const coords = values.map((val, idx) => {
      const x = padding + (idx / (values.length - 1)) * (width - 2 * padding);
      const y = height + padding - ((val - min) / range) * height;
      return [x, y];
    });

    return coords.reduce((acc, [x, y], idx) => {
      if (idx === 0) return `M ${x.toFixed(1)} ${y.toFixed(1)}`;
      return `${acc} L ${x.toFixed(1)} ${y.toFixed(1)}`;
    }, "");
  }, [points, dataKey]);

  return (
    <svg className="li-sparkline-svg" viewBox="0 0 60 24" fill="none" preserveAspectRatio="none">
      <path
        d={pathD}
        stroke={color}
        strokeWidth="2.2"
        strokeLinecap="round"
        strokeLinejoin="round"
        fill="none"
      />
    </svg>
  );
}

// Circular Donut Progress Ring
function CircularProgress({
  percent = 0,
  color = "#D49A3D",
  size = 42,
  strokeWidth = 4,
  trackColor = "rgba(255, 255, 255, 0.12)",
  textColor = "#ffffff",
}) {
  const clamped = Math.max(0, Math.min(100, Number(percent) || 0));
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const strokeDashoffset = circumference - (clamped / 100) * circumference;

  return (
    <div className="circular-progress-wrap" style={{ width: size, height: size }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={trackColor}
          strokeWidth={strokeWidth}
          fill="none"
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={color}
          strokeWidth={strokeWidth}
          strokeDasharray={circumference}
          strokeDashoffset={strokeDashoffset}
          strokeLinecap="round"
          fill="none"
          transform={`rotate(-90 ${size / 2} ${size / 2})`}
        />
      </svg>
      <span
        className="circular-progress-text"
        style={{
          color: textColor,
          fontSize: size > 50 ? "13px" : "9.5px",
          fontWeight: 800,
        }}
      >
        {Math.round(clamped)}%
      </span>
    </div>
  );
}

// Interactive Custom Tooltip for Stacked Trends
function CustomTrendsTooltip({ active, payload, label }) {
  if (active && payload && payload.length) {
    return (
      <div className="li-trends-tooltip-card">
        <div className="tooltip-date-header">{label}</div>
        <div className="tooltip-metrics-list">
          {payload.map((entry, index) => (
            <div key={index} className="tooltip-metric-row">
              <span className="tooltip-dot" style={{ backgroundColor: entry.color }} />
              <span className="tooltip-name">{entry.name}</span>
              <strong className="tooltip-val">{formatNumber(entry.value)}</strong>
            </div>
          ))}
        </div>
      </div>
    );
  }
  return null;
}

// Interactive Custom Tooltip for Live Activity Bar Chart
function CustomLiveActivityTooltip({ active, payload }) {
  if (active && payload && payload.length) {
    const data = payload[0].payload;
    return (
      <div className="li-trends-tooltip-card" style={{ minWidth: "140px" }}>
        <div className="tooltip-date-header">
          {data.isNow ? "Current Window (Now)" : `${data.label} (${data.formattedTime})`}
        </div>
        <div className="tooltip-metrics-list">
          <div className="tooltip-metric-row">
            <span className="tooltip-dot" style={{ backgroundColor: data.isNow ? "#EA580C" : "#D49A3D" }} />
            <span className="tooltip-name">Active Visitors</span>
            <strong className="tooltip-val">{formatNumber(data.count)}</strong>
          </div>
        </div>
      </div>
    );
  }
  return null;
}

export default function AdminAnalytics() {
  const [range, setRange] = useState("last7days");
  const [workshopId, setWorkshopId] = useState("");
  const [overview, setOverview] = useState(null);
  const [trends, setTrends] = useState([]);
  const [workshops, setWorkshops] = useState([]);
  const [availableWorkshops, setAvailableWorkshops] = useState([]);
  const [payments, setPayments] = useState([]);
  const [trafficSources, setTrafficSources] = useState([]);
  const [devices, setDevices] = useState({ mobile: { percentage: 0 }, desktop: { percentage: 0 }, tablet: { percentage: 0 } });
  const [locations, setLocations] = useState([]);
  const [hoveredLocation, setHoveredLocation] = useState(null);
  const [live, setLive] = useState({ activeUsers: 0, windowMinutes: LIVE_WINDOW_MINUTES });
  const [activities, setActivities] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    let isMounted = true;
    adminApi.getWorkshops()
      .then((res) => {
        if (!isMounted) return;
        const list = res?.workshops || res?.items || res || [];
        if (Array.isArray(list)) {
          setAvailableWorkshops(list);
        }
      })
      .catch(() => {});
    return () => {
      isMounted = false;
    };
  }, []);

  const loadDashboard = useCallback(
    async ({ silent = false } = {}) => {
      if (silent) {
        setRefreshing(true);
      } else {
        setLoading(true);
      }
      setError(null);

      try {
        const [
          overviewRes,
          trendsRes,
          workshopsRes,
          paymentsRes,
          liveRes,
          activityRes,
          trafficRes,
          devicesRes,
          locationsRes,
        ] = await Promise.allSettled([
          adminApi.getAdminInsightsOverview(range, workshopId),
          adminApi.getAdminInsightsTrends(range, workshopId),
          adminApi.getAdminInsightsWorkshops(range, 10),
          adminApi.getAdminInsightsPayments(range, workshopId),
          adminApi.getAdminInsightsLive(LIVE_WINDOW_MINUTES, workshopId),
          adminApi.getAdminInsightsActivity(20, workshopId),
          adminApi.getAdminInsightsTrafficSources(range, workshopId),
          adminApi.getAdminInsightsDevices(range, workshopId),
          adminApi.getAdminInsightsLocations(range, workshopId),
        ]);

        const normalizedOverviewData = normalizeOverview(overviewRes.status === "fulfilled" ? overviewRes.value : null);
        setOverview(normalizedOverviewData);
        setTrends(normalizeTrends(trendsRes.status === "fulfilled" ? trendsRes.value : null));
        setWorkshops(normalizeWorkshops(workshopsRes.status === "fulfilled" ? workshopsRes.value : null));

        const paymentsPayload = paymentsRes.status === "fulfilled" && paymentsRes.value
          ? paymentsRes.value
          : overviewRes.status === "fulfilled" && overviewRes.value?.paymentOutcomes
          ? overviewRes.value.paymentOutcomes
          : null;

        let normalizedPayments = normalizePayments(paymentsPayload);
        if (normalizedPayments.length === 0 && (normalizedOverviewData.completedBookings > 0 || normalizedOverviewData.paymentAttempts > 0)) {
          const succ = normalizedOverviewData.completedBookings || normalizedOverviewData.paymentAttempts;
          normalizedPayments = [
            {
              label: "Successful",
              count: succ,
              percentage: 100,
              color: "#10B981",
            },
          ];
        }
        setPayments(normalizedPayments);

        setTrafficSources(normalizeTrafficSources(trafficRes.status === "fulfilled" ? trafficRes.value : null));
        setDevices(normalizeDevices(devicesRes.status === "fulfilled" ? devicesRes.value : null));
        setLocations(normalizeLocations(locationsRes.status === "fulfilled" ? locationsRes.value : null));

        setLive({
          activeUsers: liveRes.status === "fulfilled" && liveRes.value?.liveUserCount != null ? Number(liveRes.value.liveUserCount) : 0,
          windowMinutes: LIVE_WINDOW_MINUTES,
        });
        setActivities(normalizeActivity(activityRes.status === "fulfilled" ? activityRes.value : null));
      } catch (err) {
        setError(err?.message || "Unable to load Live Insights.");
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [range, workshopId]
  );

  useEffect(() => {
    loadDashboard();
  }, [loadDashboard]);

  useEffect(() => {
    const timer = window.setInterval(() => {
      loadDashboard({ silent: true });
    }, LIVE_REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [loadDashboard]);

  const normalizedOverview = overview || normalizeOverview(null);
  const funnel = normalizedOverview.funnel;

  const liveTimeline = useMemo(() => {
    const totalMinutes = live.windowMinutes || 15;
    const bucketCount = 6;
    const now = new Date();
    const activeTotal = Number(live.activeUsers) || 0;

    return Array.from({ length: bucketCount }).map((_, index) => {
      const minutesAgo = Math.round(((bucketCount - 1 - index) / (bucketCount - 1)) * totalMinutes);
      const bucketTime = new Date(now.getTime() - minutesAgo * 60 * 1000);
      const formattedTime = bucketTime.toLocaleTimeString("en-IN", {
        hour: "2-digit",
        minute: "2-digit",
        hour12: true,
      });

      const label = index === bucketCount - 1 ? "Now" : `-${minutesAgo}m`;
      // Truthful telemetry: Current active users reflect in the active time window
      const count = index === bucketCount - 1 ? activeTotal : 0;

      return {
        label,
        minutesAgo,
        formattedTime,
        count,
        isNow: index === bucketCount - 1,
      };
    });
  }, [live.activeUsers, live.windowMinutes]);

  const totalTrafficVisitors = useMemo(() => {
    return trafficSources.reduce((sum, item) => sum + (Number(item.value) || 0), 0) || normalizedOverview.visitors;
  }, [trafficSources, normalizedOverview.visitors]);

  const effectiveLocations = useMemo(() => {
    return locations || [];
  }, [locations]);

  return (
    <div className="ethos-insights-luxury-page">
      {/* =========================================================
          TOP CONTROL BAR (Range Selector & Real-Time Status)
          ========================================================= */}
      <div className="li-top-control-bar">
        <div className="li-title-area">
          <h1 className="li-main-title">Live Insights</h1>
          <div className="li-telemetry-pill">
            <span className="li-telemetry-dot" />
            <span>REAL-TIME TELEMETRY</span>
          </div>
        </div>

        <div className="li-controls-group">
          {/* Workshop Selector Dropdown */}
          <select
            className="li-workshop-select"
            value={workshopId}
            onChange={(e) => setWorkshopId(e.target.value)}
            aria-label="Filter by Workshop"
          >
            <option value="">All Workshops</option>
            {availableWorkshops.map((ws) => (
              <option key={ws.id || ws.workshopId} value={ws.id || ws.workshopId}>
                {ws.title || ws.name || ws.workshopName}
              </option>
            ))}
          </select>

          <div className="li-range-pill-group">
            {RANGE_OPTIONS.map((opt) => (
              <button
                key={opt.id}
                type="button"
                className={`li-range-tab ${range === opt.id ? "active" : ""}`}
                onClick={() => setRange(opt.id)}
              >
                {opt.label}
              </button>
            ))}
          </div>

          <button
            type="button"
            className={`li-refresh-button ${refreshing ? "is-refreshing" : ""}`}
            onClick={() => loadDashboard({ silent: true })}
            title="Refresh Insights"
          >
            <RefreshCw size={14} />
            <span>Refresh</span>
          </button>
        </div>
      </div>

      {/* =========================================================
          1. TOP 5 KPI CARDS ROW WITH REAL SPARKLINES
          ========================================================= */}
      <section className="li-kpi-luxury-row">
        {/* Card 1: Website Visitors */}
        <div className="li-kpi-card-luxury">
          <div className="li-kpi-header-row">
            <div className="li-kpi-icon-badge amber-badge">
              <Eye size={18} />
            </div>
            <MiniSparkline points={trends} dataKey="visitors" color="#D49A3D" />
          </div>
          <span className="li-kpi-label">Website Visitors</span>
          <div className="li-kpi-number">{formatNumber(normalizedOverview.visitors)}</div>
          <div className="li-kpi-subtext">
            {normalizedOverview.visitorDelta != null ? (
              <span className={`li-delta ${normalizedOverview.visitorDelta >= 0 ? "up" : "down"}`}>
                {normalizedOverview.visitorDelta >= 0 ? "↑" : "↓"} {Math.abs(normalizedOverview.visitorDelta)}%
              </span>
            ) : (
              <span className="li-subtext-muted">Unique visitors</span>
            )}
            <span className="li-subtext-muted">in selected period</span>
          </div>
        </div>

        {/* Card 2: Workshop Views */}
        <div className="li-kpi-card-luxury">
          <div className="li-kpi-header-row">
            <div className="li-kpi-icon-badge coral-badge">
              <FileText size={18} />
            </div>
            <MiniSparkline points={trends} dataKey="workshopViews" color="#F97316" />
          </div>
          <span className="li-kpi-label">Workshop Views</span>
          <div className="li-kpi-number">{formatNumber(normalizedOverview.workshopViews)}</div>
          <div className="li-kpi-subtext">
            <span className="li-delta up">{formatPercent(funnel.visitorToWorkshopRate)}</span>
            <span className="li-subtext-muted">of total visitors</span>
          </div>
        </div>

        {/* Card 3: Started Booking */}
        <div className="li-kpi-card-luxury">
          <div className="li-kpi-header-row">
            <div className="li-kpi-icon-badge rose-badge">
              <ShoppingCart size={18} />
            </div>
            <MiniSparkline points={trends} dataKey="bookingStarts" color="#F43F5E" />
          </div>
          <span className="li-kpi-label">Started Booking</span>
          <div className="li-kpi-number">{formatNumber(normalizedOverview.bookingStarts)}</div>
          <div className="li-kpi-subtext">
            <span className="li-delta up">{formatPercent(funnel.workshopToBookingRate)}</span>
            <span className="li-subtext-muted">of workshop viewers</span>
          </div>
        </div>

        {/* Card 4: Reached Payment */}
        <div className="li-kpi-card-luxury">
          <div className="li-kpi-header-row">
            <div className="li-kpi-icon-badge gold-badge">
              <CreditCard size={18} />
            </div>
            <MiniSparkline points={trends} dataKey="paymentAttempts" color="#EAB308" />
          </div>
          <span className="li-kpi-label">Reached Payment</span>
          <div className="li-kpi-number">{formatNumber(normalizedOverview.paymentAttempts)}</div>
          <div className="li-kpi-subtext">
            <span className="li-delta up">{formatPercent(funnel.bookingToPaymentRate)}</span>
            <span className="li-subtext-muted">of booking starters</span>
          </div>
        </div>

        {/* Card 5: Completed Bookings */}
        <div className="li-kpi-card-luxury">
          <div className="li-kpi-header-row">
            <div className="li-kpi-icon-badge emerald-badge">
              <CheckCircle2 size={18} />
            </div>
            <MiniSparkline points={trends} dataKey="completedBookings" color="#10B981" />
          </div>
          <span className="li-kpi-label">Completed Bookings</span>
          <div className="li-kpi-number">{formatNumber(normalizedOverview.completedBookings)}</div>
          <div className="li-kpi-subtext">
            <span className="li-delta up">{formatPercent(funnel.paymentToCompletionRate)}</span>
            <span className="li-subtext-muted">of payment attempts</span>
          </div>
        </div>
      </section>

      {/* =========================================================
          2. MAIN SECTION: 3D CONVERSION FUNNEL + TRENDS & LIVE ACTIVITY
          ========================================================= */}
      <section className="li-hero-insights-grid">
        {/* LEFT: 3D LUMINOUS CONVERSION FUNNEL (Obsidian Glass Card) */}
        <div className="li-grand-funnel-card">
          <div className="funnel-card-header">
            <div className="funnel-title-wrap">
              <h2>Conversion Funnel</h2>
              <Info size={15} className="funnel-info-icon" title="Monotonic Unique Visitor Retention Across Funnel" />
            </div>
            <div className="funnel-filter-pill">
              <span>Unique Visitors</span>
            </div>
          </div>

          <div className="funnel-luminous-container">
            {/* Ambient Backlight Glow */}
            <div className="funnel-back-glow" />

            {/* Funnel Rings Flow */}
            <div className="funnel-tiers-stack">
              {/* Tier 1: Website Visitors */}
              <div className="funnel-tier-band tier-gold">
                <div className="funnel-tier-body">
                  <div className="funnel-dancer-silhouette tier-bg-gold" />
                  <div className="funnel-tier-content">
                    <span className="tier-count">{formatNumber(funnel.visitors)}</span>
                    <span className="tier-name">Website Visitors</span>
                  </div>
                </div>
                <div className="funnel-tier-metric">
                  <CircularProgress percent={funnel.visitors > 0 ? 100 : 0} color="#D49A3D" size={40} />
                  <div className="metric-details">
                    <strong>{funnel.visitors > 0 ? "100%" : "0%"}</strong>
                    <span>Total visitors</span>
                  </div>
                </div>
              </div>

              {/* Tier 2: Workshop Page Views */}
              <div className="funnel-tier-band tier-bronze">
                <div className="funnel-tier-body">
                  <div className="funnel-dancer-silhouette tier-bg-bronze" />
                  <div className="funnel-tier-content">
                    <span className="tier-count">{formatNumber(funnel.workshopViews)}</span>
                    <span className="tier-name">Workshop Page Views</span>
                  </div>
                </div>
                <div className="funnel-tier-metric">
                  <CircularProgress percent={funnel.visitorToWorkshopRate} color="#E879F9" size={40} />
                  <div className="metric-details">
                    <strong>{formatPercent(funnel.visitorToWorkshopRate)}</strong>
                    <span>of visitors</span>
                  </div>
                </div>
              </div>

              {/* Tier 3: Started Booking */}
              <div className="funnel-tier-band tier-rose">
                <div className="funnel-tier-body">
                  <div className="funnel-dancer-silhouette tier-bg-rose" />
                  <div className="funnel-tier-content">
                    <span className="tier-count">{formatNumber(funnel.bookingStarts)}</span>
                    <span className="tier-name">Started Booking</span>
                  </div>
                </div>
                <div className="funnel-tier-metric">
                  <CircularProgress percent={funnel.workshopToBookingRate} color="#F43F5E" size={40} />
                  <div className="metric-details">
                    <strong>{formatPercent(funnel.workshopToBookingRate)}</strong>
                    <span>of workshop views</span>
                  </div>
                </div>
              </div>

              {/* Tier 4: Reached Payment */}
              <div className="funnel-tier-band tier-violet">
                <div className="funnel-tier-body">
                  <div className="funnel-dancer-silhouette tier-bg-violet" />
                  <div className="funnel-tier-content">
                    <span className="tier-count">{formatNumber(funnel.paymentAttempts)}</span>
                    <span className="tier-name">Reached Payment</span>
                  </div>
                </div>
                <div className="funnel-tier-metric">
                  <CircularProgress percent={funnel.bookingToPaymentRate} color="#A855F7" size={40} />
                  <div className="metric-details">
                    <strong>{formatPercent(funnel.bookingToPaymentRate)}</strong>
                    <span>of booking starts</span>
                  </div>
                </div>
              </div>

              {/* Tier 5: Completed Purchase */}
              <div className="funnel-tier-band tier-teal">
                <div className="funnel-tier-body">
                  <div className="funnel-dancer-silhouette tier-bg-teal" />
                  <div className="funnel-tier-content">
                    <span className="tier-count">{formatNumber(funnel.completedBookings)}</span>
                    <span className="tier-name">Completed Purchase</span>
                  </div>
                </div>
                <div className="funnel-tier-metric">
                  <CircularProgress percent={funnel.paymentToCompletionRate} color="#14B8A6" size={40} />
                  <div className="metric-details">
                    <strong>{formatPercent(funnel.paymentToCompletionRate)}</strong>
                    <span>of payment attempts</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Glowing Golden Apex Flare */}
            <div className="funnel-apex-flare" />
          </div>

          {/* Funnel Bottom KPIs */}
          <div className="funnel-bottom-kpis">
            <div className="funnel-kpi-col">
              <div className="funnel-kpi-icon-pill">
                <BarChart3 size={16} />
              </div>
              <div>
                <span className="kpi-micro-title">Overall Conversion Rate</span>
                <div className="kpi-main-val">
                  <strong>{formatPercent(funnel.overallConversionRate)}</strong>
                  <span className="kpi-val-delta">visitor-to-booking</span>
                </div>
              </div>
            </div>

            <div className="funnel-kpi-col">
              <div className="funnel-kpi-icon-pill">
                <Timer size={16} />
              </div>
              <div>
                <span className="kpi-micro-title">Average Time to Purchase</span>
                <div className="kpi-main-val">
                  <strong>{formatDuration(normalizedOverview.averageTimeToPurchaseSeconds)}</strong>
                  <span className="kpi-val-delta">session-to-success</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* RIGHT COLUMN: TRENDS + SPLIT (LIVE ACTIVITY & LOCATIONS) */}
        <div className="li-right-column-stack">
          {/* Top: Visitor & Conversion Trends */}
          <div className="li-trends-luxury-card">
            <div className="li-card-header-luxury">
              <h2>Visitor &amp; Conversion Trends</h2>
              <div className="trends-filter-dropdown">
                <span>{RANGE_OPTIONS.find((r) => r.id === range)?.label || "Selected Period"}</span>
              </div>
            </div>

            {/* Trends Legend Header */}
            <div className="trends-legend-luxury">
              <div className="legend-item"><span className="dot dot-visitors" /> Website Visitors</div>
              <div className="legend-item"><span className="dot dot-views" /> Workshop Views</div>
              <div className="legend-item"><span className="dot dot-booking" /> Booking Starts</div>
              <div className="legend-item"><span className="dot dot-payment" /> Payment Attempts</div>
              <div className="legend-item"><span className="dot dot-purchases" /> Purchases</div>
            </div>

            {/* Stacked Glowing Area Chart */}
            <div className="trends-chart-container">
              {trends.length > 0 ? (
                <ResponsiveContainer width="100%" height={260}>
                  <AreaChart data={trends} margin={{ top: 12, right: 10, left: -20, bottom: 0 }}>
                    <defs>
                      <linearGradient id="gradVisitors" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#D49A3D" stopOpacity={0.45} />
                        <stop offset="95%" stopColor="#D49A3D" stopOpacity={0.0} />
                      </linearGradient>
                      <linearGradient id="gradViews" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#F43F5E" stopOpacity={0.4} />
                        <stop offset="95%" stopColor="#F43F5E" stopOpacity={0.0} />
                      </linearGradient>
                      <linearGradient id="gradStarts" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#8B5CF6" stopOpacity={0.4} />
                        <stop offset="95%" stopColor="#8B5CF6" stopOpacity={0.0} />
                      </linearGradient>
                      <linearGradient id="gradPayments" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#38BDF8" stopOpacity={0.4} />
                        <stop offset="95%" stopColor="#38BDF8" stopOpacity={0.0} />
                      </linearGradient>
                      <linearGradient id="gradPurchases" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#10B981" stopOpacity={0.45} />
                        <stop offset="95%" stopColor="#10B981" stopOpacity={0.0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid stroke="#EFEBE4" strokeDasharray="3 3" vertical={false} />
                    <XAxis dataKey="dateLabel" tick={{ fontSize: 11, fill: "#8C827A" }} axisLine={false} tickLine={false} />
                    <YAxis tick={{ fontSize: 11, fill: "#8C827A" }} axisLine={false} tickLine={false} />
                    <Tooltip content={<CustomTrendsTooltip />} />
                    <Area type="monotone" dataKey="visitors" name="Visitors" stroke="#D49A3D" strokeWidth={2.5} fill="url(#gradVisitors)" />
                    <Area type="monotone" dataKey="workshopViews" name="Workshop Views" stroke="#F43F5E" strokeWidth={2} fill="url(#gradViews)" />
                    <Area type="monotone" dataKey="bookingStarts" name="Booking Starts" stroke="#8B5CF6" strokeWidth={2} fill="url(#gradStarts)" />
                    <Area type="monotone" dataKey="paymentAttempts" name="Payment Attempts" stroke="#38BDF8" strokeWidth={2} fill="url(#gradPayments)" />
                    <Area type="monotone" dataKey="completedBookings" name="Purchases" stroke="#10B981" strokeWidth={2.5} fill="url(#gradPurchases)" />
                  </AreaChart>
                </ResponsiveContainer>
              ) : (
                <div className="li-empty-state-luxury">
                  <div className="li-empty-state-icon">
                    <Activity size={18} />
                  </div>
                  <span className="li-empty-state-text">No trend telemetry recorded yet</span>
                  <span className="li-empty-state-sub">Visitor and booking activity for this date range will display here in real time.</span>
                </div>
              )}
            </div>
          </div>

          {/* Top Locations Card (Full Width) */}
          <div className="li-locations-luxury-card">
            <div className="li-locations-card-header">
              <h3>Top Locations</h3>
              <span className="li-locations-range-badge">
                {RANGE_OPTIONS.find((r) => r.id === range)?.label || "Selected Period"}
              </span>
            </div>

            <div className="li-locations-side-by-side">
              <div className="li-locations-map-pane">
                <RealWorldMap
                  locations={effectiveLocations}
                  hoveredLocation={hoveredLocation}
                  onHoverLocation={setHoveredLocation}
                />
              </div>

              <div className="li-locations-list-pane">
                <div className="locations-breakdown-col">
                  {effectiveLocations.length > 0 ? (
                    <div className="locations-list">
                      {effectiveLocations.map((loc, idx) => {
                        const isLocActive = hoveredLocation?.city?.toLowerCase() === loc.city?.toLowerCase();
                        return (
                          <div
                            key={idx}
                            className={`location-item-row ${isLocActive ? "is-active" : ""}`}
                            title={`${loc.city}, ${loc.country}: ${formatNumber(loc.visitorCount || loc.count)} visitors`}
                            onMouseEnter={() => {
                              const coords = projectCoordinates(loc.longitude || 78.4867, loc.latitude || 17.3850);
                              setHoveredLocation({ ...loc, x: coords.x, y: coords.y });
                            }}
                            onMouseLeave={() => setHoveredLocation(null)}
                          >
                            <div className="loc-info-wrap">
                              <span className="loc-dot" style={{ backgroundColor: loc.color }} />
                              <div style={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
                                <span className="loc-name">{loc.locationName || loc.city}</span>
                                {loc.country && loc.country !== loc.city && (
                                  <span style={{ fontSize: "9.5px", color: "#8C827A", fontWeight: 500 }}>{loc.country}</span>
                                )}
                              </div>
                            </div>
                            <span className="loc-percent-badge">{loc.percent}</span>
                          </div>
                        );
                      })}
                    </div>
                  ) : (
                    <div className="li-empty-state-luxury" style={{ padding: "8px 4px" }}>
                      <span className="li-empty-state-text" style={{ fontSize: "11.5px" }}>No visitor location telemetry recorded for this period</span>
                      <span className="li-empty-state-sub" style={{ fontSize: "10.5px" }}>Regional telemetry aggregates in real time as unique visitors access the platform.</span>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* =========================================================
          3. 4-CARD MODULAR ROW: LIVE ACTIVITY, DEVICES, PAYMENTS, BRAND
          ========================================================= */}
      <section className="li-four-cards-row">
        {/* Card 1: Live Activity */}
        <div className="li-modular-card">
          <div className="modular-header">
            <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
              <h3 style={{ margin: 0 }}>Live Activity</h3>
              <div className="live-pulse-badge">
                <span className="pulse-dot" />
                <span>LIVE</span>
              </div>
            </div>
            <div className="card-dropdown-sm">
              <span>Telemetry</span>
            </div>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            <div className="live-user-stat">
              <span className="live-stat-number">{formatNumber(live.activeUsers)}</span>
              <span className="live-stat-sub">Active users right now (15m window)</span>
            </div>

            {/* Live Activity Telemetry Bar Chart with X & Y Axis */}
            <div style={{ width: "100%", height: "90px", marginTop: "4px" }}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={liveTimeline} margin={{ top: 6, right: 6, left: -24, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="2 2" stroke="#EFEBE4" vertical={false} />
                  <XAxis
                    dataKey="label"
                    tick={{ fontSize: 9, fill: "#8C827A", fontWeight: 600 }}
                    axisLine={{ stroke: "#E6DEC5" }}
                    tickLine={false}
                  />
                  <YAxis
                    allowDecimals={false}
                    tick={{ fontSize: 9, fill: "#8C827A" }}
                    axisLine={false}
                    tickLine={false}
                    width={28}
                  />
                  <Tooltip content={<CustomLiveActivityTooltip />} cursor={{ fill: "rgba(212, 154, 61, 0.08)" }} />
                  <Bar dataKey="count" radius={[3, 3, 0, 0]} maxBarSize={22}>
                    {liveTimeline.map((entry, idx) => (
                      <Cell key={`live-bar-${idx}`} fill={entry.isNow ? "#EA580C" : "#D49A3D"} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>

        {/* Card 2: Device Breakdown */}
        <div className="li-modular-card">
          <div className="modular-header">
            <h3>Device Breakdown</h3>
            <div className="card-dropdown-sm">
              <span>Client User-Agent</span>
            </div>
          </div>

          <div className="device-gauges-row">
            {/* Mobile */}
            <div className="device-gauge-item">
              <div className="gauge-circle-outer">
                <CircularProgress
                  percent={devices.mobile.percentage}
                  color="#D49A3D"
                  size={64}
                  strokeWidth={5.5}
                  trackColor="#EAE4D9"
                  textColor="#1E1810"
                />
              </div>
              <div className="device-label-row">
                <Smartphone size={14} className="device-icon" />
                <span>Mobile</span>
              </div>
            </div>

            {/* Desktop */}
            <div className="device-gauge-item">
              <div className="gauge-circle-outer">
                <CircularProgress
                  percent={devices.desktop.percentage}
                  color="#60A5FA"
                  size={64}
                  strokeWidth={5.5}
                  trackColor="#EAE4D9"
                  textColor="#1E1810"
                />
              </div>
              <div className="device-label-row">
                <Laptop size={14} className="device-icon" />
                <span>Desktop</span>
              </div>
            </div>

            {/* Tablet */}
            <div className="device-gauge-item">
              <div className="gauge-circle-outer">
                <CircularProgress
                  percent={devices.tablet.percentage}
                  color="#F43F5E"
                  size={64}
                  strokeWidth={5.5}
                  trackColor="#EAE4D9"
                  textColor="#1E1810"
                />
              </div>
              <div className="device-label-row">
                <Tablet size={14} className="device-icon" />
                <span>Tablet</span>
              </div>
            </div>
          </div>
        </div>

        {/* Card 3: Payment Outcomes */}
        <div className="li-modular-card">
          <div className="modular-header">
            <h3>Payment Outcomes</h3>
            <div className="card-dropdown-sm">
              <span>Database Ledger</span>
            </div>
          </div>

          {payments.length > 0 ? (
            <div className="donut-center-layout">
              <div className="donut-canvas-wrap">
                <ResponsiveContainer width={120} height={120}>
                  <PieChart>
                    <Pie data={payments} dataKey="count" innerRadius={42} outerRadius={58} stroke="none" paddingAngle={3}>
                      {payments.map((entry, idx) => (
                        <Cell key={idx} fill={entry.color} />
                      ))}
                    </Pie>
                  </PieChart>
                </ResponsiveContainer>
                <div className="donut-center-text">
                  <strong>{formatNumber(payments.reduce((sum, p) => sum + (Number(p.count) || 0), 0) || normalizedOverview.paymentAttempts || normalizedOverview.completedBookings || 0)}</strong>
                  <span>{(payments.reduce((sum, p) => sum + (Number(p.count) || 0), 0) || normalizedOverview.paymentAttempts || normalizedOverview.completedBookings || 0) === 1 ? "Attempt" : "Attempts"}</span>
                </div>
              </div>

              <div className="donut-legend-col">
                {payments.map((item, idx) => (
                  <div key={idx} className="donut-legend-row">
                    <span className="legend-dot" style={{ backgroundColor: item.color }} />
                    <span className="legend-label">{item.label}</span>
                    <span className="legend-count">{item.count}</span>
                    <span className="legend-val">{formatPercent(item.percentage)}</span>
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <div className="li-empty-state-luxury">
              <div className="li-empty-state-icon">
                <CreditCard size={18} />
              </div>
              <span className="li-empty-state-text">No payment attempts</span>
              <span className="li-empty-state-sub">Razorpay transactions and checkouts will show here.</span>
            </div>
          )}
        </div>

        {/* Card 4: Brand Showcase / Inspiration Card */}
        <div className="li-brand-showcase-card">
          <div className="brand-card-overlay" />
          <div className="brand-card-content">
            <Sparkles size={20} className="brand-sparkle-icon" />
            <blockquote className="brand-quote">
              “Turn Interest Into Movement”
            </blockquote>
            <span className="brand-sub">ETHOS DANCE STUDIO</span>
          </div>
        </div>
      </section>

      {/* =========================================================
          4. BOTTOM INTELLIGENCE ROW: WORKSHOPS TABLE & LIVE FEED
          ========================================================= */}
      <section className="li-bottom-intelligence-grid">
        {/* Left: Top Workshops by Interest */}
        <div className="li-workshops-table-card">
          <div className="li-card-header-luxury">
            <h2>Top Workshops by Interest</h2>
            <Link to="/admin_portal/workshops" className="view-all-workshops-link">
              View All Workshops →
            </Link>
          </div>

          <div className="workshops-table-scroll-container">
            {workshops.length > 0 ? (
              <table className="li-luxury-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Workshop</th>
                    <th>Page Views</th>
                    <th>Booking Starts</th>
                    <th>Reached Payment</th>
                    <th>Completed</th>
                    <th>Conversion Rate</th>
                    <th>Trend</th>
                  </tr>
                </thead>
                <tbody>
                  {workshops.map((w, idx) => (
                    <tr key={w.workshopId || idx}>
                      <td className="rank-cell">{idx + 1}</td>
                      <td className="workshop-info-cell">
                        {w.imageUrl ? (
                          <img
                            src={w.imageUrl}
                            alt={w.workshopName}
                            className="workshop-avatar-img"
                            onError={(e) => {
                              e.currentTarget.style.display = "none";
                              if (e.currentTarget.nextElementSibling) {
                                e.currentTarget.nextElementSibling.style.display = "flex";
                              }
                            }}
                          />
                        ) : null}
                        <div
                          className="workshop-avatar-thumb"
                          style={{ display: w.imageUrl ? "none" : "flex" }}
                        >
                          🩰
                        </div>
                        <span className="workshop-name-text">{w.workshopName}</span>
                      </td>
                      <td>{formatNumber(w.views)}</td>
                      <td>{formatNumber(w.bookingStarts)}</td>
                      <td>{formatNumber(w.paymentAttempts)}</td>
                      <td className="completed-bold-cell">{formatNumber(w.completedBookings)}</td>
                      <td>
                        <span className="conversion-rate-pill">{formatPercent(w.conversionRate)}</span>
                      </td>
                      <td>
                        <span className="trend-arrow-icon">↗</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <div className="li-empty-state-luxury">
                <div className="li-empty-state-icon">
                  <Layers size={18} />
                </div>
                <span className="li-empty-state-text">No workshop engagement recorded</span>
                <span className="li-empty-state-sub">As users browse workshops and buy passes, rankings will appear here.</span>
              </div>
            )}
          </div>
        </div>

        {/* Right: Recent Activity Feed */}
        <div className="li-activity-feed-card">
          <div className="li-card-header-luxury">
            <h2>Recent Activity Feed</h2>
            <div className="card-dropdown-sm">
              <span>Telemetry Stream</span>
            </div>
          </div>

          <div className="activity-feed-stream">
            {activities.length > 0 ? (
              activities.map((act) => {
                const isPurchase = act.type?.toLowerCase().includes("purchase") || act.title?.toLowerCase().includes("completed");
                const isPayment = act.type?.toLowerCase().includes("payment") || act.title?.toLowerCase().includes("payment");
                const isBooking = act.type?.toLowerCase().includes("booking") || act.title?.toLowerCase().includes("started");

                let icon = <Eye size={14} className="act-icon-green" />;
                let badgeBg = "#DCFCE7";

                if (isPurchase) {
                  icon = <CheckCircle2 size={14} className="act-icon-emerald" />;
                  badgeBg = "#D1FAE5";
                } else if (isPayment) {
                  icon = <CreditCard size={14} className="act-icon-coral" />;
                  badgeBg = "#FEE2E2";
                } else if (isBooking) {
                  icon = <ShoppingCart size={14} className="act-icon-amber" />;
                  badgeBg = "#FEF3C7";
                }

                return (
                  <div key={act.id} className="activity-feed-item">
                    <div className="act-icon-circle" style={{ background: badgeBg }}>
                      {icon}
                    </div>
                    <span className="act-timestamp">{formatTime(act.occurredAtUtc)}</span>
                    <div className="act-description-wrap">
                      <span className="act-title">{act.title}</span>
                      {act.code && <span className="act-code-badge">{act.code}</span>}
                    </div>
                  </div>
                );
              })
            ) : (
              <div className="li-empty-state-luxury">
                <div className="li-empty-state-icon">
                  <Activity size={18} />
                </div>
                <span className="li-empty-state-text">No recent activity</span>
                <span className="li-empty-state-sub">Live events, page views, and booking checkout actions will appear here in real time.</span>
              </div>
            )}
          </div>
        </div>
      </section>
    </div>
  );
}
