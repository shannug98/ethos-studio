import React, { useState, useEffect } from "react";
import { adminApi } from "../../services/adminApi";
import "./AdminBookings.css";

export default function AdminBookings() {
  const [activeTab, setActiveTab] = useState("classes"); // 'classes' | 'workshops'
  const [classEnrollments, setClassEnrollments] = useState([]);
  const [workshopBookings, setWorkshopBookings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");

  // Cancellation Modal
  const [cancelModal, setCancelModal] = useState({
    open: false,
    type: "class", // 'class' | 'workshop'
    booking: null,
    reason: "",
    submitting: false,
  });

  // Manual Enrollment Modal
  const [manualModal, setManualModal] = useState({
    open: false,
    studentProfileId: "",
    danceClassId: "",
    studentPackageId: "",
    allowPackageBypass: false,
    bypassReason: "",
    submitting: false,
  });

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      if (activeTab === "classes") {
        const queryParams = [];
        if (searchTerm) queryParams.push(`search=${encodeURIComponent(searchTerm)}`);
        if (statusFilter !== "all") queryParams.push(`status=${encodeURIComponent(statusFilter)}`);
        const query = queryParams.length > 0 ? queryParams.join("&") : "";
        const data = await adminApi.getClassEnrollments(query);
        setClassEnrollments(data?.items || []);
      } else {
        const queryParams = [];
        if (searchTerm) queryParams.push(`search=${encodeURIComponent(searchTerm)}`);
        if (statusFilter !== "all") queryParams.push(`status=${encodeURIComponent(statusFilter)}`);
        const query = queryParams.length > 0 ? queryParams.join("&") : "";
        const data = await adminApi.getWorkshopBookings(query);
        setWorkshopBookings(data?.items || []);
      }
    } catch (err) {
      setError(err.message || "Failed to load bookings ledger.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [activeTab, statusFilter]);

  const handleSearch = (e) => {
    e.preventDefault();
    loadData();
  };

  const openCancelModal = (type, booking) => {
    setCancelModal({
      open: true,
      type,
      booking,
      reason: "",
      submitting: false,
    });
  };

  const handleConfirmCancel = async () => {
    if (!cancelModal.reason.trim()) {
      alert("A cancellation reason is strictly mandatory.");
      return;
    }
    setCancelModal((prev) => ({ ...prev, submitting: true }));
    try {
      if (cancelModal.type === "class") {
        await adminApi.cancelClassEnrollment(cancelModal.booking.id, cancelModal.reason.trim());
      } else {
        await adminApi.cancelWorkshopBooking(cancelModal.booking.bookingId, cancelModal.reason.trim());
      }
      setCancelModal({ open: false, type: "class", booking: null, reason: "", submitting: false });
      loadData();
    } catch (err) {
      alert(err.message || "Cancellation failed.");
      setCancelModal((prev) => ({ ...prev, submitting: false }));
    }
  };

  const handleManualEnrollSubmit = async (e) => {
    e.preventDefault();
    if (!manualModal.studentProfileId.trim() || !manualModal.danceClassId.trim()) {
      alert("Student ID and Dance Class ID are required.");
      return;
    }
    if (manualModal.allowPackageBypass && !manualModal.bypassReason.trim()) {
      alert("A reason is strictly mandatory when bypassing package quota.");
      return;
    }

    setManualModal((prev) => ({ ...prev, submitting: true }));
    try {
      await adminApi.manualClassEnrollment({
        studentProfileId: manualModal.studentProfileId.trim(),
        danceClassId: manualModal.danceClassId.trim(),
        studentPackageId: manualModal.studentPackageId.trim() || null,
        allowPackageBypass: manualModal.allowPackageBypass,
        bypassReason: manualModal.bypassReason.trim() || null,
      });
      setManualModal({
        open: false,
        studentProfileId: "",
        danceClassId: "",
        studentPackageId: "",
        allowPackageBypass: false,
        bypassReason: "",
        submitting: false,
      });
      loadData();
    } catch (err) {
      alert(err.message || "Manual enrollment failed.");
      setManualModal((prev) => ({ ...prev, submitting: false }));
    }
  };

  return (
    <div className="admin-bookings-container">
      <div className="bookings-header">
        <div>
          <h1>Bookings Ledger & Quota Control</h1>
          <p className="subtitle">
            Oversee class enrollments, workshop bookings, atomic quota returns, and manual overrides.
          </p>
        </div>
        <div className="header-actions">
          {activeTab === "classes" && (
            <button
              className="admin-btn primary"
              onClick={() => setManualModal((prev) => ({ ...prev, open: true }))}
            >
              + Manual Enrollment
            </button>
          )}
          <button className="admin-btn secondary" onClick={loadData}>
            Refresh
          </button>
        </div>
      </div>

      <div className="bookings-tabs">
        <button
          className={`tab-btn ${activeTab === "classes" ? "active" : ""}`}
          onClick={() => {
            setActiveTab("classes");
            setStatusFilter("all");
          }}
        >
          Class Enrollments
        </button>
        <button
          className={`tab-btn ${activeTab === "workshops" ? "active" : ""}`}
          onClick={() => {
            setActiveTab("workshops");
            setStatusFilter("all");
          }}
        >
          Workshop Bookings
        </button>
      </div>

      <div className="filters-bar">
        <form onSubmit={handleSearch} className="search-box">
          <input
            type="text"
            placeholder={activeTab === "classes" ? "Search by student, phone, or class..." : "Search by student or workshop..."}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          <button type="submit" className="admin-btn secondary">Search</button>
        </form>

        <div className="filter-select-group">
          <label>Status:</label>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
          >
            <option value="all">All Statuses</option>
            {activeTab === "classes" ? (
              <>
                <option value="Active">Active</option>
                <option value="Cancelled">Cancelled</option>
                <option value="Completed">Completed</option>
              </>
            ) : (
              <>
                <option value="Confirmed">Confirmed</option>
                <option value="Cancelled">Cancelled</option>
                <option value="Attended">Attended</option>
              </>
            )}
          </select>
        </div>
      </div>

      {loading && <div className="loading-state">Loading ledger entries...</div>}
      {error && <div className="error-state">Error: {error}</div>}

      {!loading && !error && activeTab === "classes" && (
        <div className="table-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Enrollment ID</th>
                <th>Student</th>
                <th>Dance Class</th>
                <th>Package Applied</th>
                <th>Enrolled Date</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {classEnrollments.length === 0 ? (
                <tr>
                  <td colSpan="7" className="empty-row">No class enrollments found.</td>
                </tr>
              ) : (
                classEnrollments.map((enr) => (
                  <tr key={enr.id}>
                    <td>
                      <span className="mono-code">{enr.id.substring(0, 8)}...</span>
                    </td>
                    <td>
                      <strong>{enr.studentName}</strong>
                      <div className="sub-text">{enr.studentPhone} • {enr.studentCustomerCode}</div>
                    </td>
                    <td>
                      <span className="font-medium">{enr.danceClassName}</span>
                    </td>
                    <td>
                      {enr.packageName ? (
                        <span className="badge badge-package">{enr.packageName}</span>
                      ) : (
                        <span className="text-muted">Direct / Manual</span>
                      )}
                    </td>
                    <td>{new Date(enr.enrollmentDate).toLocaleDateString()}</td>
                    <td>
                      <span className={`status-tag ${enr.statusName?.toLowerCase() || "active"}`}>
                        {enr.statusName || "Active"}
                      </span>
                    </td>
                    <td>
                      {enr.statusName !== "Cancelled" && (
                        <button
                          className="action-btn-danger"
                          onClick={() => openCancelModal("class", enr)}
                        >
                          Cancel
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {!loading && !error && activeTab === "workshops" && (
        <div className="table-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Booking ID</th>
                <th>Student</th>
                <th>Workshop Title</th>
                <th>Booked At</th>
                <th>Payment Ref</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {workshopBookings.length === 0 ? (
                <tr>
                  <td colSpan="7" className="empty-row">No workshop bookings found.</td>
                </tr>
              ) : (
                workshopBookings.map((b) => (
                  <tr key={b.bookingId}>
                    <td>
                      <span className="mono-code">{b.bookingId.substring(0, 8)}...</span>
                    </td>
                    <td>
                      <strong>{b.studentName}</strong>
                      <div className="sub-text">{b.studentPhone}</div>
                    </td>
                    <td>
                      <span className="font-medium">{b.workshopTitle}</span>
                    </td>
                    <td>{new Date(b.bookedAt).toLocaleDateString()}</td>
                    <td>
                      {b.paymentTransactionId ? (
                        <span className="mono-code">{b.paymentTransactionId.substring(0, 8)}...</span>
                      ) : (
                        <span className="text-muted">None</span>
                      )}
                    </td>
                    <td>
                      <span className={`status-tag ${b.status?.toLowerCase() || "confirmed"}`}>
                        {b.status}
                      </span>
                    </td>
                    <td>
                      {b.status !== "Cancelled" && (
                        <button
                          className="action-btn-danger"
                          onClick={() => openCancelModal("workshop", b)}
                        >
                          Cancel
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Cancellation Modal */}
      {cancelModal.open && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>Confirm Booking Cancellation</h3>
            <p className="modal-description">
              Cancelling this {cancelModal.type === "class" ? "class enrollment" : "workshop booking"} will execute an atomic quota return (if applicable) and log an administrative audit event.
            </p>

            <div className="form-group">
              <label>Cancellation Reason <span className="req">*</span></label>
              <textarea
                placeholder="Enter mandatory administrative cancellation reason..."
                rows="3"
                value={cancelModal.reason}
                onChange={(e) => setCancelModal((prev) => ({ ...prev, reason: e.target.value }))}
              />
            </div>

            <div className="modal-actions">
              <button
                className="admin-btn secondary"
                disabled={cancelModal.submitting}
                onClick={() => setCancelModal({ open: false, type: "class", booking: null, reason: "", submitting: false })}
              >
                Dismiss
              </button>
              <button
                className="admin-btn danger"
                disabled={cancelModal.submitting}
                onClick={handleConfirmCancel}
              >
                {cancelModal.submitting ? "Cancelling..." : "Confirm Cancellation"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Manual Enrollment Modal */}
      {manualModal.open && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>Manual Class Enrollment</h3>
            <p className="modal-description">
              Enroll a student directly into a dance class. Quotas can be checked or explicitly bypassed with an audited reason.
            </p>

            <form onSubmit={handleManualEnrollSubmit}>
              <div className="form-group">
                <label>Student Profile ID <span className="req">*</span></label>
                <input
                  type="text"
                  placeholder="UUID of Student Profile"
                  value={manualModal.studentProfileId}
                  onChange={(e) => setManualModal((prev) => ({ ...prev, studentProfileId: e.target.value }))}
                  required
                />
              </div>

              <div className="form-group">
                <label>Dance Class ID <span className="req">*</span></label>
                <input
                  type="text"
                  placeholder="UUID of Dance Class"
                  value={manualModal.danceClassId}
                  onChange={(e) => setManualModal((prev) => ({ ...prev, danceClassId: e.target.value }))}
                  required
                />
              </div>

              <div className="form-group">
                <label>Student Package ID (Optional)</label>
                <input
                  type="text"
                  placeholder="UUID of Active Student Package"
                  value={manualModal.studentPackageId}
                  onChange={(e) => setManualModal((prev) => ({ ...prev, studentPackageId: e.target.value }))}
                />
              </div>

              <div className="form-group checkbox-group">
                <label>
                  <input
                    type="checkbox"
                    checked={manualModal.allowPackageBypass}
                    onChange={(e) => setManualModal((prev) => ({ ...prev, allowPackageBypass: e.target.checked }))}
                  />
                  <span>Allow Package Quota Bypass (Administrative Override)</span>
                </label>
              </div>

              {manualModal.allowPackageBypass && (
                <div className="form-group">
                  <label>Bypass Reason <span className="req">*</span></label>
                  <textarea
                    placeholder="Provide mandatory business reason for bypassing student package quota..."
                    rows="2"
                    value={manualModal.bypassReason}
                    onChange={(e) => setManualModal((prev) => ({ ...prev, bypassReason: e.target.value }))}
                    required
                  />
                </div>
              )}

              <div className="modal-actions">
                <button
                  type="button"
                  className="admin-btn secondary"
                  disabled={manualModal.submitting}
                  onClick={() => setManualModal({ open: false, studentProfileId: "", danceClassId: "", studentPackageId: "", allowPackageBypass: false, bypassReason: "", submitting: false })}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="admin-btn primary"
                  disabled={manualModal.submitting}
                >
                  {manualModal.submitting ? "Enrolling..." : "Enroll Student"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
