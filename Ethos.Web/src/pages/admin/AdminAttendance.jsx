import React, { useState, useEffect } from "react";
import { adminApi } from "../../services/adminApi";
import "./AdminAttendance.css";

const ATTENDANCE_STATUS = {
  1: { label: "Present", color: "present" },
  2: { label: "Absent", color: "absent" },
  3: { label: "Late", color: "late" },
  4: { label: "Excused", color: "excused" },
};

export default function AdminAttendance() {
  const [activeTab, setActiveTab] = useState("classes"); // 'classes' | 'workshops'
  
  // Class Sessions State
  const [sessions, setSessions] = useState([]);
  const [selectedSession, setSelectedSession] = useState(null);
  const [roster, setRoster] = useState(null);
  const [studentStatuses, setStudentStatuses] = useState({}); // studentProfileId -> { status, notes }
  const [savingAttendance, setSavingAttendance] = useState(false);
  const [confirmAllPresentModal, setConfirmAllPresentModal] = useState(false);

  // Workshops State
  const [workshops, setWorkshops] = useState([]);
  const [selectedWorkshop, setSelectedWorkshop] = useState(null);
  const [workshopRegistrations, setWorkshopRegistrations] = useState([]);

  // Attendance Lifecycle & Notification State
  const [attendanceAlert, setAttendanceAlert] = useState(null);
  const [markingAttendeeId, setMarkingAttendeeId] = useState(null);
  const [confirmNoShowTarget, setConfirmNoShowTarget] = useState(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Date / Time formatting helpers to prevent 'Invalid Date'
  const formatDisplayDate = (dateVal) => {
    if (!dateVal) return "—";
    const d = new Date(dateVal);
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleDateString("en-US", {
      weekday: "short",
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  };

  const formatTimeRange = (startTime, endTime) => {
    if (!startTime) return "";
    const s = typeof startTime === "string" ? startTime.substring(0, 5) : "";
    const e = typeof endTime === "string" ? endTime.substring(0, 5) : "";
    return e ? `${s} – ${e}` : s;
  };

  // Load Sessions or Workshops
  const loadInitialData = async () => {
    setLoading(true);
    setError(null);
    setAttendanceAlert(null);
    try {
      if (activeTab === "classes") {
        const data = await adminApi.getAttendanceSessions();
        const items = Array.isArray(data) ? data : (data?.items || []);
        setSessions(items);
        if (items.length > 0 && !selectedSession) {
          selectSession(items[0]);
        }
      } else {
        const data = await adminApi.getWorkshops();
        const items = Array.isArray(data) ? data : (data?.items || []);
        setWorkshops(items);
        if (items.length > 0 && !selectedWorkshop) {
          selectWorkshop(items[0]);
        }
      }
    } catch (err) {
      setError(err.message || "Failed to load attendance directory.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadInitialData();
  }, [activeTab]);

  const selectSession = async (session) => {
    setSelectedSession(session);
    setLoading(true);
    try {
      const rosterData = await adminApi.getSessionRoster(session.id);
      setRoster(rosterData);
      const initialMap = {};
      rosterData.students.forEach((s) => {
        initialMap[s.studentProfileId] = {
          status: s.attendanceStatus || 1, // Default to Present if unset
          notes: s.notes || "",
        };
      });
      setStudentStatuses(initialMap);
    } catch (err) {
      alert(err.message || "Failed to load session roster.");
    } finally {
      setLoading(false);
    }
  };

  const selectWorkshop = async (ws) => {
    setSelectedWorkshop(ws);
    setLoading(true);
    setAttendanceAlert(null);
    try {
      const data = await adminApi.getWorkshopRegistrations(ws.id);
      setWorkshopRegistrations(data?.items || []);
    } catch (err) {
      alert(err.message || "Failed to load workshop roster.");
    } finally {
      setLoading(false);
    }
  };

  const updateStudentStatus = (studentProfileId, status) => {
    setStudentStatuses((prev) => ({
      ...prev,
      [studentProfileId]: {
        ...prev[studentProfileId],
        status,
      },
    }));
  };

  const updateStudentNote = (studentProfileId, notes) => {
    setStudentStatuses((prev) => ({
      ...prev,
      [studentProfileId]: {
        ...prev[studentProfileId],
        notes,
      },
    }));
  };

  const handleMarkAllPresent = () => {
    setConfirmAllPresentModal(true);
  };

  const executeMarkAllPresent = () => {
    if (!roster) return;
    const updated = { ...studentStatuses };
    roster.students.forEach((s) => {
      updated[s.studentProfileId] = {
        ...updated[s.studentProfileId],
        status: 1, // Present
      };
    });
    setStudentStatuses(updated);
    setConfirmAllPresentModal(false);
  };

  const handleSaveAttendance = async () => {
    if (!selectedSession || !roster) return;
    setSavingAttendance(true);
    try {
      const records = roster.students.map((s) => ({
        studentProfileId: s.studentProfileId,
        status: studentStatuses[s.studentProfileId]?.status || 1,
        notes: studentStatuses[s.studentProfileId]?.notes || null,
      }));

      await adminApi.markSessionAttendance(selectedSession.id, records);
      alert("Attendance records saved successfully.");
      // Refresh roster
      selectSession(selectedSession);
    } catch (err) {
      alert(err.message || "Failed to save attendance.");
    } finally {
      setSavingAttendance(false);
    }
  };

  // Workshop Attendance Handlers
  const handleCheckIn = async (reg) => {
    if (!selectedWorkshop) return;
    const rowId = reg.bookingId || reg.studentId;
    setMarkingAttendeeId(rowId);
    setAttendanceAlert(null);
    try {
      await adminApi.markWorkshopAttendance(selectedWorkshop.id, reg.studentId, 4, reg.bookingId);
      setAttendanceAlert({
        type: "success",
        message: `✓ Successfully checked in ${reg.studentName || "Attendee"} (Status: ATTENDED).`,
      });
      // Immediate row refresh
      const data = await adminApi.getWorkshopRegistrations(selectedWorkshop.id);
      setWorkshopRegistrations(data?.items || []);
    } catch (err) {
      setAttendanceAlert({
        type: "error",
        message: err.message || "Failed to check in attendee.",
      });
    } finally {
      setMarkingAttendeeId(null);
    }
  };

  const openNoShowModal = (reg) => {
    setAttendanceAlert(null);
    setConfirmNoShowTarget(reg);
  };

  const executeNoShow = async () => {
    if (!selectedWorkshop || !confirmNoShowTarget) return;
    const reg = confirmNoShowTarget;
    const rowId = reg.bookingId || reg.studentId;
    setMarkingAttendeeId(rowId);
    try {
      await adminApi.markWorkshopAttendance(selectedWorkshop.id, reg.studentId, 5, reg.bookingId);
      setAttendanceAlert({
        type: "success",
        message: `✓ Marked ${reg.studentName || "Attendee"} as NO-SHOW.`,
      });
      setConfirmNoShowTarget(null);
      // Immediate row refresh
      const data = await adminApi.getWorkshopRegistrations(selectedWorkshop.id);
      setWorkshopRegistrations(data?.items || []);
    } catch (err) {
      setAttendanceAlert({
        type: "error",
        message: err.message || "Failed to mark attendee as no-show.",
      });
    } finally {
      setMarkingAttendeeId(null);
    }
  };

  const handleUndoCheckIn = async (reg) => {
    if (!selectedWorkshop) return;
    const rowId = reg.bookingId || reg.studentId;
    setMarkingAttendeeId(rowId);
    setAttendanceAlert(null);
    try {
      await adminApi.markWorkshopAttendance(selectedWorkshop.id, reg.studentId, 2, reg.bookingId);
      setAttendanceAlert({
        type: "success",
        message: `✓ Reverted check-in for ${reg.studentName || "Attendee"} to CONFIRMED.`,
      });
      // Immediate row refresh
      const data = await adminApi.getWorkshopRegistrations(selectedWorkshop.id);
      setWorkshopRegistrations(data?.items || []);
    } catch (err) {
      setAttendanceAlert({
        type: "error",
        message: err.message || "Failed to revert check-in.",
      });
    } finally {
      setMarkingAttendeeId(null);
    }
  };

  return (
    <div className="admin-attendance-container">
      <div className="attendance-header">
        <div>
          <h1>Attendance Tracking & Before/After Audit</h1>
          <p className="subtitle">
            Class session check-in rosters, bulk attendance reconciliation, and workshop attendance.
          </p>
        </div>
        <button className="admin-btn secondary" onClick={loadInitialData}>
          Refresh
        </button>
      </div>

      <div className="attendance-tabs">
        <button
          className={`tab-btn ${activeTab === "classes" ? "active" : ""}`}
          onClick={() => setActiveTab("classes")}
        >
          Class Sessions Roster
        </button>
        <button
          className={`tab-btn ${activeTab === "workshops" ? "active" : ""}`}
          onClick={() => setActiveTab("workshops")}
        >
          Workshop Check-in
        </button>
      </div>

      {loading && !roster && <div className="loading-state">Loading attendance context...</div>}
      {error && <div className="error-state">Error: {error}</div>}

      {/* Class Sessions Tab */}
      {!error && activeTab === "classes" && (
        <div className="attendance-grid">
          {/* Sidebar list of sessions */}
          <div className="session-sidebar">
            <h3>Scheduled Sessions</h3>
            <div className="session-list">
              {sessions.length === 0 ? (
                <div className="text-muted p-3">No active sessions available.</div>
              ) : (
                sessions.map((s) => (
                  <div
                    key={s.id}
                    className={`session-card ${selectedSession?.id === s.id ? "selected" : ""}`}
                    onClick={() => selectSession(s)}
                  >
                    <div className="session-title">{s.danceClassName}</div>
                    <div className="session-meta">
                      {formatDisplayDate(s.sessionDate)} • {formatTimeRange(s.startTime, s.endTime)}
                    </div>
                    <div className="session-room">
                      Room: {s.studioRoom || "Main Studio"} | Attended: {s.attendedCount} / {s.enrolledCount}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Roster & Attendance Marking */}
          <div className="roster-main">
            {selectedSession && roster ? (
              <div className="roster-card">
                <div className="roster-header">
                  <div>
                    <h2>{roster.danceClassName} — Roster</h2>
                    <p className="roster-sub">
                      Date: {formatDisplayDate(roster.sessionDate)} | Room: {roster.studioRoom || "Main Studio"} | Enrolled: {roster.students.length}
                    </p>
                  </div>
                  <div className="roster-actions">
                    <button
                      className="admin-btn secondary"
                      onClick={handleMarkAllPresent}
                    >
                      ✓ Mark All Present
                    </button>
                    <button
                      className="admin-btn primary"
                      disabled={savingAttendance}
                      onClick={handleSaveAttendance}
                    >
                      {savingAttendance ? "Saving..." : "Save Attendance"}
                    </button>
                  </div>
                </div>

                <div className="roster-table-wrapper">
                  <table className="admin-table">
                    <thead>
                      <tr>
                        <th>Student</th>
                        <th>Status</th>
                        <th>Last Updated</th>
                        <th>Notes / Observation</th>
                      </tr>
                    </thead>
                    <tbody>
                      {roster.students.length === 0 ? (
                        <tr>
                          <td colSpan="4" className="empty-row">
                            No students currently enrolled in this session.
                          </td>
                        </tr>
                      ) : (
                        roster.students.map((st) => {
                          const currentStatus = studentStatuses[st.studentProfileId]?.status || 1;
                          const currentNote = studentStatuses[st.studentProfileId]?.notes || "";
                          return (
                            <tr key={st.studentProfileId}>
                              <td>
                                <strong>{st.studentName}</strong>
                                <div className="sub-text">{st.studentPhone} • {st.customerCode}</div>
                              </td>
                              <td>
                                <div className="status-button-group">
                                  {[1, 2, 3, 4].map((code) => (
                                    <button
                                      key={code}
                                      type="button"
                                      className={`status-chip ${code === currentStatus ? `active ${ATTENDANCE_STATUS[code].color}` : ""}`}
                                      onClick={() => updateStudentStatus(st.studentProfileId, code)}
                                    >
                                      {ATTENDANCE_STATUS[code].label}
                                    </button>
                                  ))}
                                </div>
                              </td>
                              <td>
                                {st.markedAt ? (
                                  <div className="sub-text">
                                    {new Date(st.markedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                                    <br />
                                    by {st.markedByAdminName || "Admin"}
                                  </div>
                                ) : (
                                  <span className="text-muted">Unrecorded</span>
                                )}
                              </td>
                              <td>
                                <input
                                  type="text"
                                  placeholder="e.g. Arrived 10m late"
                                  className="table-input"
                                  value={currentNote}
                                  onChange={(e) => updateStudentNote(st.studentProfileId, e.target.value)}
                                />
                              </td>
                            </tr>
                          );
                        })
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : (
              <div className="no-selection">
                Select a class session from the sidebar to view and mark attendance roster.
              </div>
            )}
          </div>
        </div>
      )}

      {/* Workshops Tab */}
      {!error && activeTab === "workshops" && (
        <div className="attendance-grid">
          {/* Sidebar list of workshops */}
          <div className="session-sidebar">
            <h3>Workshops</h3>
            <div className="session-list">
              {workshops.length === 0 ? (
                <div className="text-muted p-3">No workshops available.</div>
              ) : (
                workshops.map((ws) => (
                  <div
                    key={ws.id}
                    className={`session-card ${selectedWorkshop?.id === ws.id ? "selected" : ""}`}
                    onClick={() => selectWorkshop(ws)}
                  >
                    <div className="session-title">{ws.title}</div>
                    <div className="session-meta">
                      {formatDisplayDate(ws.workshopDate || ws.date || ws.scheduledDate)} • {ws.trainerName}
                    </div>
                    <div className="session-room">
                      Capacity: {ws.capacity} | Enrolled: {ws.bookedCount ?? ws.currentRegistrations ?? 0}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Workshop Registrations & Check-In */}
          <div className="roster-main">
            {selectedWorkshop ? (
              <div className="roster-card">
                <div className="roster-header">
                  <div>
                    <h2>{selectedWorkshop.title} — Attendee Check-In</h2>
                    <p className="roster-sub">
                      Date: {formatDisplayDate(selectedWorkshop.workshopDate || selectedWorkshop.date || selectedWorkshop.scheduledDate)}
                      {selectedWorkshop.startTime ? ` (${formatTimeRange(selectedWorkshop.startTime, selectedWorkshop.endTime)})` : ""}
                      {" "}| Trainer: {selectedWorkshop.trainerName} | Capacity: {selectedWorkshop.capacity} | Enrolled: {workshopRegistrations.length}
                    </p>
                  </div>
                </div>

                {/* Status Feedback Alert Banner */}
                {attendanceAlert && (
                  <div className={`attendance-alert ${attendanceAlert.type}`}>
                    <span>{attendanceAlert.message}</span>
                    <button
                      type="button"
                      className="attendance-alert-close"
                      onClick={() => setAttendanceAlert(null)}
                    >
                      ×
                    </button>
                  </div>
                )}

                <div className="roster-table-wrapper">
                  <table className="admin-table">
                    <thead>
                      <tr>
                        <th>Attendee</th>
                        <th>Contact</th>
                        <th>Booking Ref</th>
                        <th>Payment</th>
                        <th>Attendance Status</th>
                        <th>Check-in Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {workshopRegistrations.length === 0 ? (
                        <tr>
                          <td colSpan="6" className="empty-row">
                            No registered attendees found for this workshop.
                          </td>
                        </tr>
                      ) : (
                        workshopRegistrations.map((reg) => {
                          const normStatus = (reg.status || "").toUpperCase();
                          const isAttended = normStatus === "ATTENDED";
                          const isNoShow = normStatus === "NOSHOW" || normStatus === "NO_SHOW";
                          const isCancelled = normStatus === "CANCELLED";
                          const isCompleted = normStatus === "COMPLETED";
                          const isRowLoading = markingAttendeeId === (reg.bookingId || reg.studentId);
                          const isGuest = reg.isGuest || reg.attendeeType === "Workshop Attendee";

                          return (
                            <tr key={reg.bookingId || reg.studentId}>
                              {/* Attendee Name & Role Tag */}
                              <td>
                                <strong>{reg.studentName}</strong>
                                <div className="sub-text">
                                  <span className={`status-tag ${isGuest ? "pending" : "confirmed"}`}>
                                    {isGuest ? "Workshop Attendee" : "ETHOS Student"}
                                  </span>
                                  {" "}• {reg.customerCode || (isGuest ? "GUEST" : "—")}
                                </div>
                              </td>

                              {/* Contact */}
                              <td>
                                <div className="sub-text">📞 {reg.studentPhone || "—"}</div>
                                {reg.studentEmail && (
                                  <div className="sub-text">✉️ {reg.studentEmail}</div>
                                )}
                              </td>

                              {/* Booking Reference */}
                              <td>
                                <div className="mono-code">
                                  {reg.bookingReference || `BK-${(reg.bookingId || "").slice(0, 8).toUpperCase()}`}
                                </div>
                                <div className="sub-text">
                                  {reg.bookedAt ? new Date(reg.bookedAt).toLocaleDateString() : "—"}
                                </div>
                              </td>

                              {/* Payment Status */}
                              <td>
                                <span className={`status-tag ${reg.paymentStatus?.toLowerCase().includes("paid") || reg.status === "Confirmed" || isAttended ? "active" : "pending"}`}>
                                  {reg.paymentStatus || "Paid"}
                                </span>
                              </td>

                              {/* Attendance / Booking Status */}
                              <td>
                                <span className={`status-tag ${isAttended ? "attended" : isNoShow ? "noshow" : isCancelled ? "cancelled" : "confirmed"}`}>
                                  {reg.status}
                                </span>
                              </td>

                              {/* Action Buttons with Strict Lifecycle Transitions */}
                              <td>
                                {isAttended && (
                                  <div className="action-buttons-cell">
                                    <span className="checked-in-badge">✓ Checked In</span>
                                    <button
                                      type="button"
                                      className="undo-action-btn"
                                      disabled={isRowLoading}
                                      onClick={() => handleUndoCheckIn(reg)}
                                      title="Revert check-in back to confirmed"
                                    >
                                      {isRowLoading ? "Reverting..." : "Undo Check-In"}
                                    </button>
                                  </div>
                                )}

                                {isNoShow && (
                                  <div className="action-buttons-cell">
                                    <span className="no-show-badge">✕ No-Show</span>
                                    <button
                                      type="button"
                                      className="correct-action-btn"
                                      disabled={isRowLoading}
                                      onClick={() => handleCheckIn(reg)}
                                      title="Correct attendee status to Attended"
                                    >
                                      {isRowLoading ? "Updating..." : "Mark Attended"}
                                    </button>
                                  </div>
                                )}

                                {isCancelled && (
                                  <span className="text-muted">No actions (Cancelled)</span>
                                )}

                                {isCompleted && (
                                  <span className="text-muted">Completed</span>
                                )}

                                {!isAttended && !isNoShow && !isCancelled && !isCompleted && (
                                  <div className="action-buttons-cell">
                                    <button
                                      type="button"
                                      className="admin-btn primary small"
                                      disabled={isRowLoading}
                                      onClick={() => handleCheckIn(reg)}
                                    >
                                      {isRowLoading ? "Checking In..." : "✓ Check In (Attended)"}
                                    </button>
                                    <button
                                      type="button"
                                      className="admin-btn secondary small"
                                      disabled={isRowLoading}
                                      onClick={() => openNoShowModal(reg)}
                                    >
                                      No-Show
                                    </button>
                                  </div>
                                )}
                              </td>
                            </tr>
                          );
                        })
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : (
              <div className="no-selection">
                Select a workshop from the sidebar to review and check-in attendees.
              </div>
            )}
          </div>
        </div>
      )}

      {/* Confirmation Modal for No-Show */}
      {confirmNoShowTarget && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>Confirm No-Show</h3>
            <p className="modal-description">
              Mark <strong>{confirmNoShowTarget.studentName}</strong> as <strong>No-Show</strong>? This will update attendance records and may affect workshop reporting.
            </p>
            <div className="modal-actions">
              <button
                type="button"
                className="admin-btn secondary"
                disabled={markingAttendeeId === (confirmNoShowTarget.bookingId || confirmNoShowTarget.studentId)}
                onClick={() => setConfirmNoShowTarget(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="admin-btn danger"
                disabled={markingAttendeeId === (confirmNoShowTarget.bookingId || confirmNoShowTarget.studentId)}
                onClick={executeNoShow}
              >
                {markingAttendeeId === (confirmNoShowTarget.bookingId || confirmNoShowTarget.studentId)
                  ? "Updating..."
                  : "Confirm No-Show"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Confirmation Modal for Mark All Present */}
      {confirmAllPresentModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>Confirm Bulk Action</h3>
            <p className="modal-description">
              Are you sure you want to mark all students in this roster as <strong>Present</strong>? Any unrecorded or absent statuses will be staged as Present until you save changes.
            </p>
            <div className="modal-actions">
              <button
                type="button"
                className="admin-btn secondary"
                onClick={() => setConfirmAllPresentModal(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="admin-btn primary"
                onClick={executeMarkAllPresent}
              >
                Confirm Mark All Present
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
