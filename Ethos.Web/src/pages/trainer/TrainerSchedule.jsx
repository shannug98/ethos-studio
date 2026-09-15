import { useCallback, useEffect, useRef, useState } from "react";
import { trainerApi } from "../../services/trainerApi";
import LoadingState from "../../components/trainer/LoadingState";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";

const DAYS = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

const DAY_NAME_TO_INT = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6,
};

const INT_TO_DAY_NAME = {
  0: "Sunday",
  1: "Monday",
  2: "Tuesday",
  3: "Wednesday",
  4: "Thursday",
  5: "Friday",
  6: "Saturday",
};

export default function TrainerSchedule() {
  const mountedRef = useRef(true);

  const [availability, setAvailability] = useState({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError("");
      const response = await trainerApi.getAvailability();
      if (!mountedRef.current) return;

      const slots = Array.isArray(response?.data) ? response.data : (Array.isArray(response) ? response : []);
      const map = {};
      DAYS.forEach((day) => {
        map[day] = { enabled: false, startTime: "09:00", endTime: "17:00" };
      });

      slots.forEach((slot) => {
        const dayName = typeof slot.dayOfWeek === "number" ? INT_TO_DAY_NAME[slot.dayOfWeek] : slot.dayOfWeek;
        if (dayName && map[dayName]) {
          map[dayName] = {
            enabled: Boolean(slot.isAvailable),
            startTime: slot.startTime ? slot.startTime.slice(0, 5) : "09:00",
            endTime: slot.endTime ? slot.endTime.slice(0, 5) : "17:00",
          };
        }
      });

      setAvailability(map);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(getApiErrorMessage(err, "Unable to load availability."));
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    load();
    return () => {
      mountedRef.current = false;
    };
  }, [load]);

  function updateDay(day, field, value) {
    setAvailability((current) => ({
      ...current,
      [day]: {
        ...(current[day] || {}),
        [field]: value,
      },
    }));
  }

  async function save() {
    setSaving(true);
    setMessage("");
    setError("");

    try {
      const requests = DAYS.map((day) => {
        const item = availability[day] || {};
        const start = item.startTime ? (item.startTime.length === 5 ? `${item.startTime}:00` : item.startTime) : "09:00:00";
        const end = item.endTime ? (item.endTime.length === 5 ? `${item.endTime}:00` : item.endTime) : "17:00:00";
        return {
          dayOfWeek: DAY_NAME_TO_INT[day],
          isAvailable: Boolean(item.enabled),
          startTime: start,
          endTime: end,
        };
      });

      await trainerApi.updateAvailability(requests);
      if (!mountedRef.current) return;

      setMessage("Availability updated successfully.");
    } catch (err) {
      if (!mountedRef.current) return;
      setError(getApiErrorMessage(err, "Unable to save availability."));
    } finally {
      if (mountedRef.current) {
        setSaving(false);
      }
    }
  }

  if (loading) {
    return <LoadingState label="Loading availability..." />;
  }

  return (
    <div className="trainer-page">
      <div className="trainer-page-header">
        <span className="trainer-eyebrow">
          YOUR RHYTHM
        </span>

        <h2>Availability</h2>

        <p>
          Tell Ethos when you're available to teach.
        </p>
      </div>

      {message && (
        <div className="trainer-success" role="status" aria-live="polite">
          {message}
        </div>
      )}

      {error && (
        <div className="trainer-alert error" role="alert">
          {error}
        </div>
      )}

      <div className="trainer-schedule-list">
        {DAYS.map((day) => {
          const item = availability[day] || {};

          return (
            <div className="trainer-schedule-row" key={day}>
              <div>
                <strong>{day}</strong>
                <span>
                  {item.enabled ? "Available" : "Unavailable"}
                </span>
              </div>

              <label className="trainer-toggle">
                <input
                  type="checkbox"
                  checked={Boolean(item.enabled)}
                  onChange={(event) =>
                    updateDay(day, "enabled", event.target.checked)
                  }
                />
                <span />
              </label>

              <input
                type="time"
                disabled={!item.enabled}
                value={item.startTime || ""}
                onChange={(event) =>
                  updateDay(day, "startTime", event.target.value)
                }
              />

              <span className="trainer-time-divider">—</span>

              <input
                type="time"
                disabled={!item.enabled}
                value={item.endTime || ""}
                onChange={(event) =>
                  updateDay(day, "endTime", event.target.value)
                }
              />
            </div>
          );
        })}
      </div>

      <button
        type="button"
        className="trainer-primary-button"
        onClick={save}
        disabled={saving}
      >
        {saving ? "SAVING..." : "SAVE AVAILABILITY"}
      </button>
    </div>
  );
}
