import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { Link } from "react-router-dom";
import {
  Calendar,
  Users,
  Star,
  BarChart3,
  ArrowRight,
  User,
  Bell,
  Image as ImageIcon,
} from "lucide-react";

import { trainerApi } from "../../services/trainerApi";
import { useAuth } from "../../context/AuthContext";

import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import StatusBadge from "../../components/trainer/StatusBadge";
import TrainerStat from "../../components/trainer/TrainerStat";

import { useTrainerPermissions } from "../../hooks/useTrainerPermissions";
import { TRAINER_PERMISSIONS } from "../../constants/trainerPermissions";

import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import { getMediaUrl } from "../../utils/mediaUrl";

export default function TrainerDashboard() {
  const { user } = useAuth();
  const { hasPermission } = useTrainerPermissions();

  const [dashboard, setDashboard] = useState(null);
  const [tier, setTier] = useState(null);
  const [gallery, setGallery] = useState([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const loadDashboard = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const [dashboardData, tierData, galleryData] = await Promise.all([
        trainerApi.getDashboard(),
        trainerApi.getTier(),
        trainerApi.getGallery().catch(() => []),
      ]);

      if (!mountedRef.current) return;

      setDashboard(dashboardData);
      setTier(tierData);
      setGallery(Array.isArray(galleryData) ? galleryData : []);
    } catch (err) {
      if (!mountedRef.current) return;

      setError(
        getApiErrorMessage(
          err,
          "Unable to load your trainer dashboard."
        )
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    loadDashboard();
  }, [loadDashboard]);

  const trainerName = useMemo(() => {
    const raw =
      dashboard?.trainer?.fullName ||
      user?.fullName ||
      user?.name ||
      user?.displayName ||
      "";

    if (!raw || !raw.trim()) {
      return "Trainer";
    }

    const firstWord = raw.trim().split(" ")[0];
    return firstWord.charAt(0).toUpperCase() + firstWord.slice(1).toLowerCase();
  }, [user, dashboard]);

  const initials = useMemo(() => {
    const name = dashboard?.trainer?.fullName || user?.fullName || user?.name || "";
    if (!name.trim()) return "E";
    return name
      .trim()
      .split(" ")
      .map((part) => part[0])
      .filter(Boolean)
      .slice(0, 2)
      .join("")
      .toUpperCase();
  }, [user, dashboard]);

  const photoUrl = getMediaUrl(dashboard?.trainer?.profilePhotoUrl);

  const upcomingWorkshops = dashboard?.upcomingWorkshops || [];

  const workshopCount =
    dashboard?.upcomingWorkshopCount ??
    dashboard?.totalWorkshops ??
    0;

  const studentCount =
    dashboard?.studentCount ??
    dashboard?.totalStudents ??
    0;

  const rating =
    dashboard?.averageFeedbackRating != null
      ? Number(dashboard.averageFeedbackRating).toFixed(1)
      : dashboard?.averageRating ?? "—";

  const attendance =
    dashboard?.attendancePercentage != null
      ? `${dashboard.attendancePercentage}%`
      : "—";

  const canViewWorkshops = hasPermission(
    TRAINER_PERMISSIONS.VIEW_WORKSHOPS
  );

  const canCreateWorkshop = hasPermission(
    TRAINER_PERMISSIONS.CREATE_WORKSHOP
  );

  const canViewPerformance = hasPermission(
    TRAINER_PERMISSIONS.VIEW_PERFORMANCE
  );

  const canViewProfile = hasPermission(
    TRAINER_PERMISSIONS.VIEW_PROFILE
  );

  if (loading) {
    return <LoadingState label="Preparing your studio..." />;
  }

  if (error) {
    return (
      <div className="trainer-dashboard-page">
        <ErrorState
          title="Unable to load your studio"
          description={error}
          action={
            <button
              type="button"
              className="trainer-primary-button trainer-primary-button--small"
              onClick={loadDashboard}
            >
              TRY AGAIN
            </button>
          }
        />
      </div>
    );
  }

  const tierTitle =
    tier?.name || tier?.tierName || dashboard?.trainer?.tier || "Silver Trainer";

  return (
    <div className="trainer-dashboard-page">
      {/* REFINED EDITORIAL HERO */}
      <section className="trainer-dashboard-hero">
        <div className="trainer-dashboard-hero-content">
          <span className="trainer-eyebrow">ETHOS TRAINER STUDIO</span>

          <h1>
            Good day,
            <br />
            <em>{trainerName}.</em>
          </h1>

          <p>
            Your workshops, students and creative growth — all in one space.
            <br />
            Teach. Create. Inspire.
          </p>
        </div>

        {/* ELEGANT PROFILE PHOTO & TIER CARD */}
        <div className="trainer-dashboard-profile-box">
          <div className="trainer-dashboard-photo-container">
            {photoUrl ? (
              <img
                src={photoUrl}
                alt={trainerName}
                className="trainer-dashboard-photo"
              />
            ) : (
              <div className="trainer-dashboard-photo-fallback">
                <span>{initials}</span>
              </div>
            )}
          </div>

          <div className="trainer-dashboard-tier-info">
            <span className="tier-kicker">ACTIVE TIER</span>
            <strong className="tier-name">{tierTitle}</strong>
            {hasPermission(TRAINER_PERMISSIONS.VIEW_TIER) && (
              <Link to="/trainer/tier" className="tier-link">
                VIEW DETAILS →
              </Link>
            )}
          </div>
        </div>
      </section>

      {/* STATS */}
      <section
        className="trainer-dashboard-stats"
        aria-label="Trainer statistics"
      >
        <TrainerStat
          icon={Calendar}
          label="WORKSHOPS HOSTED"
          value={workshopCount}
          detail="Created to date"
        />

        <TrainerStat
          icon={Users}
          label="STUDENTS TAUGHT"
          value={studentCount}
          detail="Across your workshops"
        />

        <TrainerStat
          icon={Star}
          label="COMMUNITY RATING"
          value={rating}
          detail="Based on student feedback"
        />

        <TrainerStat
          icon={BarChart3}
          label="AVERAGE ATTENDANCE"
          value={attendance}
          detail="Across all sessions"
        />
      </section>

      {/* MAIN CONTENT */}
      <section className="trainer-dashboard-main">
        {/* UPCOMING WORKSHOPS */}
        <div className="trainer-dashboard-workshops">
          <div className="trainer-dashboard-section-heading">
            <div>
              <h2>UPCOMING WORKSHOPS</h2>
              <p className="trainer-dashboard-subtitle">Your next workshop starts here.</p>
            </div>

            {canViewWorkshops && (
              <Link
                to="/trainer/workshops"
                className="trainer-dashboard-link"
              >
                VIEW ALL →
              </Link>
            )}
          </div>

          {upcomingWorkshops.length > 0 ? (
            <div className="trainer-dashboard-workshop-list">
              {upcomingWorkshops.map((workshop) => {
                const workshopId =
                  workshop.id || workshop.workshopId;

                return (
                  <Link
                    key={workshopId}
                    to={`/trainer/workshops/${workshopId}`}
                    className="trainer-dashboard-workshop"
                  >
                    <div className="trainer-dashboard-workshop-date">
                      <span>
                        {formatWorkshopDate(
                          workshop.date || workshop.startDate
                        )}
                      </span>
                    </div>

                    <div className="trainer-dashboard-workshop-info">
                      <strong>
                        {workshop.title ||
                          workshop.name ||
                          "Untitled workshop"}
                      </strong>

                      <span>
                        {formatWorkshopTime(
                          workshop.date || workshop.startDate
                        )}
                      </span>
                    </div>

                    <StatusBadge
                      status={workshop.status || "Scheduled"}
                    />

                    <span
                      className="trainer-dashboard-arrow"
                      aria-hidden="true"
                    >
                      →
                    </span>
                  </Link>
                );
              })}
            </div>
          ) : (
            <div className="trainer-dashboard-empty">
              <div
                className="trainer-dashboard-empty-mark"
                aria-hidden="true"
              >
                <Calendar size={22} />
              </div>

              <div className="trainer-dashboard-empty-text">
                <h3>No upcoming workshops yet.</h3>
                <p>
                  Create your next workshop and bring your movement to the Ethos community.
                </p>
              </div>

              {canCreateWorkshop && (
                <Link
                  to="/trainer/workshops/create"
                  className="trainer-primary-button trainer-primary-button--small"
                >
                  CREATE WORKSHOP →
                </Link>
              )}
            </div>
          )}
        </div>

        {/* YOUR NEXT MOVES */}
        <aside className="trainer-dashboard-actions">
          <h2>YOUR NEXT MOVES</h2>
          <p className="trainer-dashboard-subtitle">
            Everything important is just a step away.
          </p>

          <div className="trainer-dashboard-action-list">
            {canCreateWorkshop && (
              <Link to="/trainer/workshops/create" className="trainer-dashboard-action-item">
                <div className="trainer-action-icon-box">
                  <Calendar size={16} />
                </div>
                <div className="trainer-action-text">
                  <strong>Create workshop</strong>
                  <small>Share your knowledge and inspire students</small>
                </div>
                <ArrowRight size={16} className="trainer-action-arrow" />
              </Link>
            )}

            {canViewPerformance && (
              <Link to="/trainer/performance" className="trainer-dashboard-action-item">
                <div className="trainer-action-icon-box">
                  <BarChart3 size={16} />
                </div>
                <div className="trainer-action-text">
                  <strong>View performance</strong>
                  <small>Check your feedback and growth</small>
                </div>
                <ArrowRight size={16} className="trainer-action-arrow" />
              </Link>
            )}

            {canViewProfile && (
              <Link to="/trainer/profile" className="trainer-dashboard-action-item">
                <div className="trainer-action-icon-box">
                  <User size={16} />
                </div>
                <div className="trainer-action-text">
                  <strong>Edit profile</strong>
                  <small>Keep your information up to date</small>
                </div>
                <ArrowRight size={16} className="trainer-action-arrow" />
              </Link>
            )}

            {hasPermission(TRAINER_PERMISSIONS.VIEW_NOTIFICATIONS) && (
              <Link to="/trainer/notifications" className="trainer-dashboard-action-item">
                <div className="trainer-action-icon-box">
                  <Bell size={16} />
                </div>
                <div className="trainer-action-text">
                  <strong>View notifications</strong>
                  <small>Stay updated with the latest updates</small>
                </div>
                <ArrowRight size={16} className="trainer-action-arrow" />
              </Link>
            )}
          </div>
        </aside>
      </section>

      {/* CONDITIONAL PORTFOLIO GALLERY STRIP (Only renders if photos exist) */}
      {gallery.length > 0 && (
        <section className="trainer-dashboard-gallery-strip">
          <div className="trainer-dashboard-section-heading">
            <div>
              <h2>YOUR MOVEMENT</h2>
              <p className="trainer-dashboard-subtitle">A glimpse from your portfolio gallery.</p>
            </div>

            {canViewProfile && (
              <Link to="/trainer/profile" className="trainer-dashboard-link">
                VIEW FULL PORTFOLIO →
              </Link>
            )}
          </div>

          <div className="trainer-dashboard-gallery-grid">
            {gallery.slice(0, 4).map((img) => (
              <div key={img.id} className="trainer-dashboard-gallery-item">
                <img
                  src={trainerApi.getGalleryImageUrl(img.id)}
                  alt={img.fileName || "Trainer portfolio"}
                  loading="lazy"
                />
              </div>
            ))}
          </div>
        </section>
      )}

      {/* GROWTH */}
      {canViewPerformance && (
        <section className="trainer-dashboard-growth">
          <div>
            <span className="trainer-eyebrow">PERFORMANCE</span>

            <h2>
              Your work leaves
              <br />
              an impression.
            </h2>
          </div>

          <div className="trainer-dashboard-growth-copy">
            <p>
              Track your community feedback, workshop performance and
              progression through the Trainer Studio.
            </p>

            <Link
              to="/trainer/performance"
              className="trainer-outline-button"
            >
              EXPLORE PERFORMANCE
            </Link>
          </div>
        </section>
      )}
    </div>
  );
}

function formatWorkshopDate(value) {
  if (!value) {
    return "DATE TBC";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return date
    .toLocaleDateString("en-IN", {
      day: "2-digit",
      month: "short",
      year: "numeric",
    })
    .toUpperCase();
}

function formatWorkshopTime(value) {
  if (!value) {
    return "TIME TBC";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return date.toLocaleTimeString("en-IN", {
    hour: "numeric",
    minute: "2-digit",
  });
}
