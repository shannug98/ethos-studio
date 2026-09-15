import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import { useTrainerPermissions } from "../../hooks/useTrainerPermissions";
import { TRAINER_PERMISSIONS } from "../../constants/trainerPermissions";
import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import EmptyState from "../../components/trainer/EmptyState";
import StatusBadge from "../../components/trainer/StatusBadge";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import "./TrainerWorkshops.css";

function formatDate(value) {
  if (!value) return "—";

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
}

function formatTime(value) {
  if (!value) return "—";

  const parts = value.split(":");
  const hours = Number(parts[0]);
  const minutes = Number(parts[1] || 0);

  if (Number.isNaN(hours)) return value;

  const date = new Date();
  date.setHours(hours, minutes, 0, 0);

  return date.toLocaleTimeString("en-IN", {
    hour: "numeric",
    minute: "2-digit",
  });
}

function formatPrice(value) {
  if (value === null || value === undefined) return "—";

  return `₹${Number(value).toLocaleString("en-IN")}`;
}

export default function TrainerWorkshops() {
  const mountedRef = useRef(true);
  const { hasPermission } = useTrainerPermissions();
  const canCreate = hasPermission(TRAINER_PERMISSIONS.CREATE_WORKSHOP);

  const [workshops, setWorkshops] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadWorkshops = useCallback(async () => {
    try {
      setLoading(true);
      setError("");

      const response = await trainerApi.getWorkshops();
      if (!mountedRef.current) return;

      const data = Array.isArray(response?.data) ? response.data : (Array.isArray(response) ? response : []);
      setWorkshops(data);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(
        getApiErrorMessage(err, "We couldn't load your workshops.")
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    loadWorkshops();
    return () => {
      mountedRef.current = false;
    };
  }, [loadWorkshops]);

  if (loading) {
    return <LoadingState label="Loading your workshops..." />;
  }

  if (error && workshops.length === 0) {
    return (
      <ErrorState
        title="We couldn't load your workshops"
        description={error}
        action={
          <button
            type="button"
            className="trainer-primary-button"
            onClick={loadWorkshops}
          >
            TRY AGAIN
          </button>
        }
      />
    );
  }

  return (
    <main className="trainer-workshops-page">
      <div className="workshops-shell">
        <header className="workshops-heading">
          <div>
            <span className="trainer-section-eyebrow">
              TRAINER STUDIO / WORKSHOPS
            </span>

            <h1>
              Your <em>workshops.</em>
            </h1>

            <p>
              Create, refine and submit your workshops for the Ethos approval
              process.
            </p>
          </div>

          {canCreate && (
            <Link
              to="/trainer/workshops/create"
              className="trainer-primary-button"
            >
              CREATE WORKSHOP
            </Link>
          )}
        </header>

        {error && (
          <div className="trainer-alert error" role="alert">
            <div>
              <strong>WORKSHOPS UNAVAILABLE</strong>
              <span>{error}</span>
            </div>

            <button type="button" onClick={loadWorkshops}>TRY AGAIN</button>
          </div>
        )}

        {!error && workshops.length === 0 && (
          <EmptyState
            title="Build your first experience."
            description="Create a workshop and submit it to Ethos for approval."
            action={
              canCreate ? (
                <Link
                  to="/trainer/workshops/create"
                  className="trainer-primary-button"
                >
                  CREATE YOUR FIRST WORKSHOP
                </Link>
              ) : null
            }
          />
        )}

        {!error && workshops.length > 0 && (
          <section className="trainer-workshop-list">
            {workshops.map((workshop, index) => {
              return (
                <article
                  className="trainer-workshop-card"
                  key={workshop.id}
                >
                  <div className="workshop-card-index">
                    {String(index + 1).padStart(2, "0")}
                  </div>

                  <div className="workshop-card-image">
                    {workshop.imageUrl ? (
                      <img
                        src={workshop.imageUrl}
                        alt={workshop.title}
                      />
                    ) : (
                      <div className="workshop-image-fallback">
                        ETHOS
                      </div>
                    )}
                  </div>

                  <div className="workshop-card-content">
                    <div className="workshop-card-topline">
                      <span>
                        {workshop.danceStyle || "DANCE"}
                      </span>

                      <StatusBadge status={workshop.status} />
                    </div>

                    <h2>{workshop.title}</h2>

                    <p className="workshop-card-description">
                      {workshop.description}
                    </p>

                    <div className="workshop-meta">
                      <div>
                        <small>DATE</small>
                        <strong>
                          {formatDate(workshop.workshopDate)}
                        </strong>
                      </div>

                      <div>
                        <small>TIME</small>
                        <strong>
                          {formatTime(workshop.startTime)} —{" "}
                          {formatTime(workshop.endTime)}
                        </strong>
                      </div>

                      <div>
                        <small>LEVEL</small>
                        <strong>{workshop.level || "—"}</strong>
                      </div>

                      <div>
                        <small>VENUE</small>
                        <strong>{workshop.venue || "—"}</strong>
                      </div>
                    </div>

                    <div className="workshop-card-bottom">
                      <div className="workshop-capacity">
                        <span>
                          {workshop.bookedCount || 0}/
                          {workshop.capacity || 0} booked
                        </span>

                        <div className="capacity-track">
                          <div
                            style={{
                              width: `${
                                workshop.capacity
                                  ? Math.min(
                                      100,
                                      ((workshop.bookedCount || 0) /
                                        workshop.capacity) *
                                        100
                                    )
                                  : 0
                              }%`,
                            }}
                          />
                        </div>
                      </div>

                      <div className="workshop-card-actions">
                        <span className="workshop-price">
                          {formatPrice(workshop.price)}
                        </span>

                        <Link
                          to={`/trainer/workshops/${workshop.id}`}
                          className="workshop-view-link"
                        >
                          VIEW WORKSHOP →
                        </Link>
                      </div>
                    </div>
                  </div>
                </article>
              );
            })}
          </section>
        )}
      </div>
    </main>
  );
}
