import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { adminApi } from "../../services/adminApi";
import AdminExportButton from "../../components/admin/common/AdminExportButton";
import "./AdminStudents.css";

export default function AdminStudents() {
  const [students, setStudents] = useState([]);
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Filters & Pagination
  const [search, setSearch] = useState("");
  const [profileStatus, setProfileStatus] = useState("ALL");
  const [accountStatus, setAccountStatus] = useState("ALL");
  const [packageStatus, setPackageStatus] = useState("ALL");
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  const fetchStats = async () => {
    try {
      const res = await adminApi.getStudentStats();
      setStats(res);
    } catch (err) {
      console.error("Failed to load student stats", err);
    }
  };

  const fetchStudents = async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      params.append("page", page);
      params.append("pageSize", 15);
      if (search.trim()) params.append("search", search.trim());
      if (profileStatus !== "ALL") params.append("profileStatus", profileStatus);
      if (accountStatus !== "ALL") params.append("accountStatus", accountStatus);
      if (packageStatus !== "ALL") params.append("packageStatus", packageStatus);

      const res = await adminApi.getStudents(params.toString());
      setStudents(res.items || []);
      setTotalCount(res.totalCount || 0);
      setTotalPages(Math.ceil((res.totalCount || 0) / 15) || 1);
    } catch (err) {
      setError(err.message || "Failed to load students directory.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStats();
  }, []);

  useEffect(() => {
    fetchStudents();
  }, [page, profileStatus, accountStatus, packageStatus]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    setPage(1);
    fetchStudents();
  };

  return (
    <div className="admin-students-view">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Students Directory</h1>
          <p className="admin-page-subtitle">
            Manage student profiles, dance package quotas, class attendance, and account access permissions.
          </p>
        </div>
      </div>

      {/* Operational KPI Summary Banner */}
      <div className="admin-students-stats-grid">
        <div className="student-stat-card">
          <div className="stat-icon-wrap total">👥</div>
          <div className="stat-info">
            <span className="stat-label">Total Students</span>
            <span className="stat-val">{stats ? stats.totalStudents : totalCount}</span>
          </div>
        </div>

        <div className="student-stat-card">
          <div className="stat-icon-wrap complete">✓</div>
          <div className="stat-info">
            <span className="stat-label">Complete Profiles</span>
            <span className="stat-val">{stats ? stats.completeProfiles : "—"}</span>
          </div>
        </div>

        <div className="student-stat-card">
          <div className="stat-icon-wrap incomplete">⚠️</div>
          <div className="stat-info">
            <span className="stat-label">Incomplete Profiles</span>
            <span className="stat-val">{stats ? stats.incompleteProfiles : "—"}</span>
          </div>
        </div>

        <div className="student-stat-card">
          <div className="stat-icon-wrap active">⚡</div>
          <div className="stat-info">
            <span className="stat-label">Active Accounts</span>
            <span className="stat-val">{stats ? stats.activeStudents : "—"}</span>
          </div>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="admin-students-filters">
        <form onSubmit={handleSearchSubmit} className="search-form">
          <input
            type="text"
            className="input-field"
            placeholder="Search by student name, phone, email, or customer code..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <button type="submit" className="btn-secondary">Search</button>
        </form>

        <div className="filter-selects">
          <select
            className="select-field"
            value={profileStatus}
            onChange={(e) => {
              setProfileStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="ALL">All Profile Statuses</option>
            <option value="COMPLETE">Complete Profiles</option>
            <option value="INCOMPLETE">Incomplete Profiles</option>
          </select>

          <select
            className="select-field"
            value={accountStatus}
            onChange={(e) => {
              setAccountStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="ALL">All Account Statuses</option>
            <option value="ACTIVE">Active Accounts</option>
            <option value="SUSPENDED">Suspended Accounts</option>
          </select>

          <select
            className="select-field"
            value={packageStatus}
            onChange={(e) => {
              setPackageStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="ALL">All Package Statuses</option>
            <option value="ACTIVE">Active Package</option>
            <option value="NONE">No Active Package</option>
          </select>

          <AdminExportButton
            data={students}
            filename="ethos-students-directory"
            moduleName="Student Directory"
            allowedRoles={["ADMIN"]}
          />
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* High-Contrast Table Container */}
      <div className="admin-students-table-card">
        {loading ? (
          <div className="loading-state">Loading students directory...</div>
        ) : students.length === 0 ? (
          <div className="empty-state">No students found matching your selected filters.</div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Student</th>
                <th>Contact</th>
                <th>Location</th>
                <th>Profile</th>
                <th>Package</th>
                <th>Attendance</th>
                <th>Account</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {students.map((s) => (
                <tr key={s.studentId}>
                  <td>
                    <div className="cell-student-primary">
                      <span className="student-primary-name">{s.fullName}</span>
                      <span className="student-customer-code">{s.customerCode}</span>
                    </div>
                  </td>

                  <td>
                    <div className="cell-contact-primary">
                      <span className="contact-phone">{s.phone}</span>
                      <span className="contact-email">{s.email || "No email on file"}</span>
                    </div>
                  </td>

                  <td>
                    <span className="cell-city">{s.city || "—"}</span>
                  </td>

                  <td>
                    <div className="profile-cell-wrap">
                      {s.profileCompleted ? (
                        <span className="badge-profile-completed">✓ Complete</span>
                      ) : (
                        <>
                          <span className="badge-profile-incomplete">Incomplete</span>
                          {s.missingFieldsCount > 0 && (
                            <span className="missing-summary">
                              {s.missingFieldsCount} missing ({s.missingFields.slice(0, 2).join(", ")})
                            </span>
                          )}
                        </>
                      )}
                    </div>
                  </td>

                  <td>
                    {s.activePackageName ? (
                      <span className="pkg-badge pkg-active">{s.activePackageName}</span>
                    ) : (
                      <span className="pkg-badge pkg-none">No package</span>
                    )}
                  </td>

                  <td>
                    <div className="attendance-cell">
                      {s.classesAllowed != null ? (
                        <span className="attendance-classes">
                          {s.classesUsed} / {s.classesAllowed} used
                        </span>
                      ) : (
                        <span className="attendance-classes">
                          {s.classesUsed > 0 ? `${s.classesUsed} classes` : "—"}
                        </span>
                      )}
                      {s.totalAttendanceCount > 0 && (
                        <span className="attendance-total">{s.totalAttendanceCount} logs</span>
                      )}
                    </div>
                  </td>

                  <td>
                    <span className={`status-pill ${s.isActive ? "status-active" : "status-inactive"}`}>
                      {s.isActive ? "ACTIVE" : "SUSPENDED"}
                    </span>
                  </td>

                  <td>
                    <Link
                      to={`/admin_portal/students/${s.studentId}`}
                      className="btn-view-dossier"
                    >
                      View dossier →
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {totalPages > 1 && (
          <div className="table-pagination">
            <button
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
              className="btn-secondary"
            >
              Previous
            </button>
            <span className="page-indicator">
              Page {page} of {totalPages} ({totalCount} total)
            </span>
            <button
              disabled={page >= totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="btn-secondary"
            >
              Next
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
