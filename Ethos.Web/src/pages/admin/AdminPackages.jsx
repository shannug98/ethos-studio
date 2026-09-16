import React, { useState, useEffect } from "react";
import { adminApi } from "../../services/adminApi";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import AdminBadge from "../../components/admin/common/AdminBadge";
import AdminDataTable from "../../components/admin/common/AdminDataTable";
import AdminActionModal from "../../components/admin/common/AdminActionModal";
import AdminExportButton from "../../components/admin/common/AdminExportButton";
import "./AdminPackages.css";

export default function AdminPackages() {
  const [packages, setPackages] = useState([]);
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Filters & Search
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all"); // all, active, inactive
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [totalCount, setTotalCount] = useState(0);

  // Modals
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [statusModalOpen, setStatusModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);

  // Dependency data state for deactivation and delete
  const [selectedPackage, setSelectedPackage] = useState(null);
  const [dependencyData, setDependencyData] = useState(null);
  const [dependencyLoading, setDependencyLoading] = useState(false);

  // Drawer state (Batch 3)
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerPackage, setDrawerPackage] = useState(null);
  const [drawerDetail, setDrawerDetail] = useState(null);
  const [drawerActivity, setDrawerActivity] = useState([]);
  const [drawerLoading, setDrawerLoading] = useState(false);
  const [drawerError, setDrawerError] = useState(null);
  const [activityPage, setActivityPage] = useState(1);
  const [activityTotalCount, setActivityTotalCount] = useState(0);

  // Form states
  const [formData, setFormData] = useState({
    name: "",
    description: "",
    price: "",
    durationDays: 30,
    classLimit: "",
    isFeatured: false,
    featuresJson: "",
  });
  const [statusReason, setStatusReason] = useState("");
  const [modalSubmitting, setModalSubmitting] = useState(false);
  const [modalError, setModalError] = useState(null);

  const fetchPackagesAndStats = async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      params.append("page", page.toString());
      params.append("pageSize", pageSize.toString());
      if (search.trim()) params.append("search", search.trim());
      if (statusFilter === "active") params.append("isActive", "true");
      if (statusFilter === "inactive") params.append("isActive", "false");

      const [pkgsRes, statsRes] = await Promise.all([
        adminApi.getPackages(params.toString()),
        adminApi.getPackageStats(),
      ]);

      setPackages(pkgsRes.items || []);
      setTotalCount(pkgsRes.totalCount || 0);
      setStats(statsRes);
    } catch {
      setError("We couldn’t load package information. Please refresh or try again later.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPackagesAndStats();
  }, [page, statusFilter]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    setPage(1);
    fetchPackagesAndStats();
  };

  const handleOpenCreate = () => {
    setFormData({
      name: "",
      description: "",
      price: "",
      durationDays: 30,
      classLimit: "",
      isFeatured: false,
      featuresJson: "",
    });
    setModalError(null);
    setCreateModalOpen(true);
  };

  const handleOpenEdit = (pkg) => {
    setSelectedPackage(pkg);
    setFormData({
      name: pkg.name || "",
      description: pkg.description || "",
      price: pkg.price || "",
      durationDays: pkg.durationDays || 30,
      classLimit: pkg.classLimit !== null && pkg.classLimit !== undefined ? pkg.classLimit : "",
      isFeatured: !!pkg.isFeatured,
      featuresJson: pkg.featuresJson || "",
    });
    setModalError(null);
    setEditModalOpen(true);
  };

  const handleOpenStatusModal = async (pkg) => {
    setSelectedPackage(pkg);
    setStatusReason("");
    setModalError(null);
    setDependencyLoading(true);
    setStatusModalOpen(true);

    try {
      const dep = await adminApi.checkPackageDependencies(pkg.id);
      setDependencyData(dep);
    } catch {
      setDependencyData(null);
    } finally {
      setDependencyLoading(false);
    }
  };

  const handleOpenDeleteModal = async (pkg) => {
    setSelectedPackage(pkg);
    setModalError(null);
    setDependencyLoading(true);
    setDeleteModalOpen(true);

    try {
      const dep = await adminApi.checkPackageDependencies(pkg.id);
      setDependencyData(dep);
    } catch {
      setDependencyData(null);
      setModalError("We couldn’t check whether this package can be deleted.");
    } finally {
      setDependencyLoading(false);
    }
  };

  const handleOpenDrawer = async (pkg) => {
    setDrawerPackage(pkg);
    setDrawerOpen(true);
    setDrawerError(null);
    setDrawerLoading(true);
    setActivityPage(1);

    try {
      const [detailRes, activityRes] = await Promise.all([
        adminApi.getPackageDetails(pkg.id),
        adminApi.getPackageActivity(pkg.id, 1, 20),
      ]);
      setDrawerDetail(detailRes);
      setDrawerActivity(activityRes?.items || []);
      setActivityTotalCount(activityRes?.totalCount || 0);
    } catch {
      setDrawerError("We couldn’t load package details or activity.");
    } finally {
      setDrawerLoading(false);
    }
  };

  const handleDrawerRetry = async () => {
    if (!drawerPackage) return;
    setDrawerError(null);
    setDrawerLoading(true);
    try {
      const [detailRes, activityRes] = await Promise.all([
        adminApi.getPackageDetails(drawerPackage.id),
        adminApi.getPackageActivity(drawerPackage.id, activityPage, 20),
      ]);
      setDrawerDetail(detailRes);
      setDrawerActivity(activityRes?.items || []);
      setActivityTotalCount(activityRes?.totalCount || 0);
    } catch {
      setDrawerError("We couldn’t load package details or activity.");
    } finally {
      setDrawerLoading(false);
    }
  };

  const handleDrawerPageChange = async (newPage) => {
    if (!drawerPackage) return;
    setActivityPage(newPage);
    try {
      const activityRes = await adminApi.getPackageActivity(drawerPackage.id, newPage, 20);
      setDrawerActivity(activityRes?.items || []);
      setActivityTotalCount(activityRes?.totalCount || 0);
    } catch {
      setDrawerError("We couldn’t load package activity.");
    }
  };

  const handleCreateSubmit = async (e) => {
    e.preventDefault();
    if (!formData.name.trim()) {
      setModalError("Package name is required.");
      return;
    }
    const priceNum = parseFloat(formData.price);
    if (isNaN(priceNum) || priceNum <= 0) {
      setModalError("Price must be greater than zero.");
      return;
    }
    const durationNum = parseInt(formData.durationDays, 10);
    if (isNaN(durationNum) || durationNum <= 0) {
      setModalError("Duration in days must be greater than zero.");
      return;
    }

    setModalSubmitting(true);
    setModalError(null);
    try {
      const payload = {
        name: formData.name.trim(),
        description: formData.description?.trim() || null,
        price: priceNum,
        durationDays: durationNum,
        classLimit: formData.classLimit ? parseInt(formData.classLimit, 10) : null,
        isFeatured: formData.isFeatured,
        featuresJson: formData.featuresJson?.trim() || null,
      };

      await adminApi.createPackage(payload);
      setCreateModalOpen(false);
      fetchPackagesAndStats();
    } catch (err) {
      setModalError(err.message || "Failed to create package.");
    } finally {
      setModalSubmitting(false);
    }
  };

  const handleEditSubmit = async (e) => {
    e.preventDefault();
    if (!formData.name.trim()) {
      setModalError("Package name is required.");
      return;
    }
    const priceNum = parseFloat(formData.price);
    if (isNaN(priceNum) || priceNum <= 0) {
      setModalError("Price must be greater than zero.");
      return;
    }
    const durationNum = parseInt(formData.durationDays, 10);
    if (isNaN(durationNum) || durationNum <= 0) {
      setModalError("Duration in days must be greater than zero.");
      return;
    }

    setModalSubmitting(true);
    setModalError(null);
    try {
      const payload = {
        name: formData.name.trim(),
        description: formData.description?.trim() || null,
        price: priceNum,
        durationDays: durationNum,
        classLimit: formData.classLimit ? parseInt(formData.classLimit, 10) : null,
        isFeatured: formData.isFeatured,
        featuresJson: formData.featuresJson?.trim() || null,
      };

      await adminApi.updatePackage(selectedPackage.id, payload);
      setEditModalOpen(false);
      fetchPackagesAndStats();
    } catch (err) {
      setModalError(err.message || "Failed to update package.");
    } finally {
      setModalSubmitting(false);
    }
  };

  const handleStatusSubmit = async (e) => {
    e.preventDefault();
    if (!statusReason.trim()) {
      setModalError("A justification reason is mandatory.");
      return;
    }

    setModalSubmitting(true);
    setModalError(null);
    try {
      const nextStatus = !selectedPackage.isActive;
      await adminApi.updatePackageStatus(selectedPackage.id, nextStatus, statusReason.trim());
      setStatusModalOpen(false);
      fetchPackagesAndStats();
    } catch (err) {
      setModalError(err.message || "Failed to update package status.");
    } finally {
      setModalSubmitting(false);
    }
  };

  const handleDeleteSubmit = async () => {
    if (!selectedPackage) return;
    setModalSubmitting(true);
    setModalError(null);
    try {
      await adminApi.deletePackage(selectedPackage.id);
      setDeleteModalOpen(false);
      fetchPackagesAndStats();
    } catch (err) {
      setModalError(
        err.message ||
          "This package cannot be permanently deleted because existing student or payment records use it. Stop New Purchases instead."
      );
    } finally {
      setModalSubmitting(false);
    }
  };

  // Table Columns
  const columns = [
    {
      key: "name",
      label: "Package Title",
      render: (row) => (
        <div>
          <strong className="package-title">{row.name}</strong>
          {row.description && <p className="package-desc-sub">{row.description}</p>}
        </div>
      ),
    },
    {
      key: "price",
      label: "Price (₹)",
      render: (row) => (
        <span className="price-tag font-mono">
          ₹{Number(row.price).toLocaleString("en-IN")}
        </span>
      ),
    },
    {
      key: "classLimit",
      label: "Included Classes",
      render: (row) => (
        <span>
          {row.classLimit !== null && row.classLimit !== undefined
            ? `${row.classLimit} Classes`
            : "Unlimited Classes"}
        </span>
      ),
    },
    {
      key: "durationDays",
      label: "Valid For",
      render: (row) => <span>{row.durationDays} Days</span>,
    },
    {
      key: "isFeatured",
      label: "Featured Plan",
      render: (row) =>
        row.isFeatured ? (
          <AdminBadge tone="brand" dot={true}>
            Featured
          </AdminBadge>
        ) : (
          <span className="text-secondary">—</span>
        ),
    },
    {
      key: "status",
      label: "Purchase Availability",
      render: (row) => (
        <AdminBadge tone={row.isActive ? "success" : "warning"}>
          {row.isActive ? "Available for New Purchases" : "Archived / Off Sale"}
        </AdminBadge>
      ),
    },
    {
      key: "actions",
      label: "Actions",
      align: "right",
      render: (row) => (
        <div className="table-actions">
          <button
            type="button"
            className="btn btn-sm btn-secondary"
            onClick={() => handleOpenDrawer(row)}
          >
            View Details
          </button>
          <button
            type="button"
            className="btn btn-sm btn-secondary"
            onClick={() => handleOpenEdit(row)}
          >
            Edit Package
          </button>
          <button
            type="button"
            className={`btn btn-sm ${row.isActive ? "btn-warning" : "btn-primary"}`}
            onClick={() => handleOpenStatusModal(row)}
          >
            {row.isActive ? "Stop New Purchases" : "Allow New Purchases"}
          </button>
          <button
            type="button"
            className="btn btn-sm btn-danger-outline"
            onClick={() => handleOpenDeleteModal(row)}
          >
            Delete
          </button>
        </div>
      ),
    },
  ];

  return (
    <div className="admin-packages-page">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Dance Packages & Quota Pricing</h1>
          <p className="admin-page-subtitle">
            Manage studio subscription plans, class allocations, validity periods, and non-GST transparent pricing.
          </p>
        </div>
        <div className="admin-header-actions">
          <button type="button" className="btn btn-secondary" onClick={fetchPackagesAndStats}>
            Refresh
          </button>
          <button type="button" className="btn btn-primary" onClick={handleOpenCreate}>
            + Create Package
          </button>
        </div>
      </div>

      {/* 1. Catalog Status Overview (Compact 3-Card Strip) */}
      <div className="admin-kpi-grid packages-kpi-grid packages-catalog-grid">
        <AdminKpiCard
          title="Total Packages"
          value={stats ? stats.totalPackages : "—"}
          tone="neutral"
          subtitle="Total package definitions in catalog"
          icon="📦"
          className="catalog-status-card"
        />
        <AdminKpiCard
          title="Available for Purchase"
          value={stats ? stats.availableForNewPurchases : "—"}
          tone="success"
          subtitle="Open for new student purchases"
          icon="🟢"
          className="catalog-status-card"
        />
        <AdminKpiCard
          title="Not Available for Purchase"
          value={stats ? stats.notAvailableForNewPurchases : "—"}
          tone="warning"
          subtitle="Existing passes remain active"
          icon="⏸️"
          className="catalog-status-card"
        />
      </div>

      {/* 2. Operational & Revenue KPIs (3 Prominent Cards) */}
      {(() => {
        const classesAllocated = stats ? Number(stats.includedClassesAllocated || 0) : 0;
        const classesRemaining = stats ? Number(stats.includedClassesRemaining || 0) : 0;
        const classesUsed = Math.max(0, classesAllocated - classesRemaining);

        return (
          <div className="admin-kpi-grid packages-kpi-grid packages-operational-grid">
            <AdminKpiCard
              title="Packages Sold"
              value={stats ? stats.studentPackagesPurchased : "—"}
              tone="info"
              subtitle="Number of student passes issued"
              icon="🎟️"
              className="operational-kpi-card"
            />
            <AdminKpiCard
              title="Package Revenue"
              value={stats ? `₹${Number(stats.totalRevenue).toLocaleString("en-IN")}` : "—"}
              tone="success"
              subtitle="Revenue from package purchases"
              icon="💳"
              className="operational-kpi-card"
            />
            <AdminKpiCard
              title="Class Credits"
              value={stats ? stats.includedClassesAllocated : "—"}
              tone="brand"
              subtitle={
                stats
                  ? `${classesAllocated} allocated · ${classesRemaining} remaining · ${classesUsed} used`
                  : "Total credits allocated, remaining & used"
              }
              icon="🎯"
              className="operational-kpi-card"
            />
          </div>
        );
      })()}

      {/* Filter & Export Bar */}
      <div className="admin-filter-bar card-dark">
        <form onSubmit={handleSearchSubmit} className="search-form">
          <input
            type="text"
            className="input-field"
            placeholder="Search by package name or description..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <button type="submit" className="btn btn-secondary">
            Search
          </button>
        </form>

        <div className="filter-group">
          <select
            className="select-field"
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(1);
            }}
          >
            <option value="all">All Statuses</option>
            <option value="active">Active Only</option>
            <option value="inactive">Inactive Only</option>
          </select>

          <AdminExportButton
            data={packages}
            filename="ethos-dance-packages"
            moduleName="Dance Packages"
            allowedRoles={["ADMIN"]}
          />
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* Table */}
      <div className="card-dark table-wrapper">
        <AdminDataTable
          columns={columns}
          data={packages}
          keyField="id"
          loading={loading}
          emptyMessage="No packages match your search criteria."
          page={page}
          pageSize={pageSize}
          totalItems={totalCount}
          onPageChange={(p) => setPage(p)}
        />
      </div>

      {/* Create Modal */}
      <AdminActionModal
        isOpen={createModalOpen}
        title="Create New Dance Package"
        onClose={() => !modalSubmitting && setCreateModalOpen(false)}
        maxWidth="600px"
      >
        <form onSubmit={handleCreateSubmit} className="package-modal-form">
          {modalError && <div className="alert alert-danger">{modalError}</div>}

          <div className="form-group">
            <label>Package Title *</label>
            <input
              type="text"
              className="input-field"
              placeholder="e.g. 10-Class Dance Pass"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              required
            />
          </div>

          <div className="form-group">
            <label>Description</label>
            <textarea
              className="input-field textarea"
              placeholder="Package description, perks, and inclusions..."
              value={formData.description}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              rows={3}
            />
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>Price (₹) *</label>
              <input
                type="number"
                className="input-field"
                placeholder="4999"
                value={formData.price}
                onChange={(e) => setFormData({ ...formData, price: e.target.value })}
                min="1"
                step="0.01"
                required
              />
              <span className="form-hint">Non-GST compliant. Total payable amount.</span>
            </div>

            <div className="form-group">
              <label>Valid For (Days) *</label>
              <input
                type="number"
                className="input-field"
                value={formData.durationDays}
                onChange={(e) => setFormData({ ...formData, durationDays: e.target.value })}
                min="1"
                required
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>Included Classes</label>
              <input
                type="number"
                className="input-field"
                placeholder="e.g. 10 (Leave blank for unlimited)"
                value={formData.classLimit}
                onChange={(e) => setFormData({ ...formData, classLimit: e.target.value })}
                min="1"
              />
            </div>

            <div className="form-group checkbox-group">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={formData.isFeatured}
                  onChange={(e) => setFormData({ ...formData, isFeatured: e.target.checked })}
                />
                Mark as Featured Plan
              </label>
            </div>
          </div>

          <div className="modal-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setCreateModalOpen(false)}
              disabled={modalSubmitting}
            >
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" disabled={modalSubmitting}>
              {modalSubmitting ? "Creating..." : "Create Package"}
            </button>
          </div>
        </form>
      </AdminActionModal>

      {/* Edit Modal */}
      <AdminActionModal
        isOpen={editModalOpen}
        title={`Edit Package: ${selectedPackage?.name}`}
        onClose={() => !modalSubmitting && setEditModalOpen(false)}
        maxWidth="600px"
      >
        <form onSubmit={handleEditSubmit} className="package-modal-form">
          {modalError && <div className="alert alert-danger">{modalError}</div>}

          <div className="form-group">
            <label>Package Title *</label>
            <input
              type="text"
              className="input-field"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              required
            />
          </div>

          <div className="form-group">
            <label>Description</label>
            <textarea
              className="input-field textarea"
              value={formData.description}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              rows={3}
            />
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>Price (₹) *</label>
              <input
                type="number"
                className="input-field"
                value={formData.price}
                onChange={(e) => setFormData({ ...formData, price: e.target.value })}
                min="1"
                step="0.01"
                required
              />
            </div>

            <div className="form-group">
              <label>Valid For (Days) *</label>
              <input
                type="number"
                className="input-field"
                value={formData.durationDays}
                onChange={(e) => setFormData({ ...formData, durationDays: e.target.value })}
                min="1"
                required
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>Included Classes</label>
              <input
                type="number"
                className="input-field"
                value={formData.classLimit}
                onChange={(e) => setFormData({ ...formData, classLimit: e.target.value })}
                min="1"
              />
            </div>

            <div className="form-group checkbox-group">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={formData.isFeatured}
                  onChange={(e) => setFormData({ ...formData, isFeatured: e.target.checked })}
                />
                Featured Plan
              </label>
            </div>
          </div>

          <div className="modal-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setEditModalOpen(false)}
              disabled={modalSubmitting}
            >
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" disabled={modalSubmitting}>
              {modalSubmitting ? "Saving..." : "Save Changes"}
            </button>
          </div>
        </form>
      </AdminActionModal>

      {/* Status Toggle Modal (Stop / Allow New Purchases) */}
      <AdminActionModal
        isOpen={statusModalOpen}
        title={selectedPackage?.isActive ? "Stop New Purchases" : "Allow New Purchases"}
        onClose={() => !modalSubmitting && setStatusModalOpen(false)}
        maxWidth="520px"
      >
        <form onSubmit={handleStatusSubmit} className="package-modal-form">
          {modalError && <div className="alert alert-danger">{modalError}</div>}

          <p className="status-modal-explainer">
            {selectedPackage?.isActive
              ? `You are about to stop new student purchases for "${selectedPackage?.name}".`
              : `You are about to make "${selectedPackage?.name}" available again for new student purchases.`}
          </p>

          {selectedPackage?.isActive && (
            <div className="impact-warning-box">
              <span className="impact-warning-icon">🛡️</span>
              <div className="impact-warning-text">
                <strong>What happens when you stop new purchases:</strong>
                <ul>
                  <li>New students cannot select or buy this package.</li>
                  <li>Existing students keep all remaining class quotas and valid passes until expiration.</li>
                  <li>
                    {dependencyLoading ? (
                      <em>Checking affected student passes...</em>
                    ) : dependencyData ? (
                      <>Currently affects <strong>{dependencyData.recordsUsingThisPackage?.activeStudentPackages ?? 0}</strong> active student passes.</>
                    ) : (
                      <>Existing student passes remain completely safe and protected.</>
                    )}
                  </li>
                </ul>
              </div>
            </div>
          )}

          <div className="form-group">
            <label>Reason for Change (Mandatory) *</label>
            <textarea
              className="input-field textarea"
              placeholder="Provide a clear administrative reason for this change..."
              value={statusReason}
              onChange={(e) => setStatusReason(e.target.value)}
              rows={3}
              required
            />
          </div>

          <div className="modal-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setStatusModalOpen(false)}
              disabled={modalSubmitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              className={`btn ${selectedPackage?.isActive ? "btn-warning" : "btn-primary"}`}
              disabled={modalSubmitting}
            >
              {modalSubmitting
                ? "Processing..."
                : selectedPackage?.isActive
                ? "Confirm: Stop New Purchases"
                : "Confirm: Allow New Purchases"}
            </button>
          </div>
        </form>
      </AdminActionModal>

      {/* Delete / Dependency Check Modal */}
      <AdminActionModal
        isOpen={deleteModalOpen}
        title={
          dependencyLoading
            ? "Checking Package Dependencies..."
            : dependencyData?.canDelete
            ? "Permanently Delete Package"
            : "Cannot Delete: Package Is In Use"
        }
        onClose={() => !modalSubmitting && setDeleteModalOpen(false)}
        maxWidth="540px"
      >
        <div className="package-modal-form">
          {modalError && <div className="alert alert-danger">{modalError}</div>}

          {dependencyLoading ? (
            <div style={{ padding: "24px 0", textAlign: "center", color: "#667085" }}>
              Verifying student passes, class bookings, and payment records...
            </div>
          ) : dependencyData && !dependencyData.canDelete ? (
            <div>
              <p className="status-modal-explainer" style={{ color: "#b42318" }}>
                <strong>{selectedPackage?.name}</strong> cannot be deleted because historical or active records depend on it. Permanent deletion would damage audit and accounting integrity.
              </p>

              <div className="dependency-records-box">
                <h4 style={{ margin: "0 0 10px 0", fontSize: "13px", color: "#475467", textTransform: "uppercase", letterSpacing: "0.5px" }}>
                  Records Using This Package
                </h4>
                <div className="dependency-grid">
                  <div className="dependency-stat-item">
                    <span className="dependency-stat-num">
                      {dependencyData.recordsUsingThisPackage?.studentPackageRecords ?? 0}
                    </span>
                    <span className="dependency-stat-label">Student Passes Issued</span>
                  </div>
                  <div className="dependency-stat-item">
                    <span className="dependency-stat-num">
                      {dependencyData.recordsUsingThisPackage?.activeStudentPackages ?? 0}
                    </span>
                    <span className="dependency-stat-label">Active Student Passes</span>
                  </div>
                  <div className="dependency-stat-item">
                    <span className="dependency-stat-num">
                      {dependencyData.recordsUsingThisPackage?.classEnrollmentRecords ?? 0}
                    </span>
                    <span className="dependency-stat-label">Class Bookings</span>
                  </div>
                  <div className="dependency-stat-item">
                    <span className="dependency-stat-num">
                      {dependencyData.recordsUsingThisPackage?.paymentRecords ?? 0}
                    </span>
                    <span className="dependency-stat-label">Payment Invoices</span>
                  </div>
                </div>
              </div>

              <div className="impact-warning-box" style={{ marginTop: "14px" }}>
                <span className="impact-warning-icon">💡</span>
                <div className="impact-warning-text">
                  <strong>Recommended Action:</strong>
                  <p style={{ margin: "4px 0 0 0" }}>
                    Stop new purchases instead. This safely hides the package from students while keeping all existing student passes and class bookings intact.
                  </p>
                </div>
              </div>

              <div className="modal-actions" style={{ marginTop: "20px" }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setDeleteModalOpen(false)}
                >
                  Close
                </button>
                {selectedPackage?.isActive && (
                  <button
                    type="button"
                    className="btn btn-warning"
                    onClick={() => {
                      setDeleteModalOpen(false);
                      handleOpenStatusModal(selectedPackage);
                    }}
                  >
                    Stop New Purchases Instead
                  </button>
                )}
              </div>
            </div>
          ) : dependencyData && dependencyData.canDelete ? (
            <div>
              <p className="status-modal-explainer">
                Are you sure you want to permanently delete <strong>{selectedPackage?.name}</strong>?
              </p>
              <div className="alert alert-warning" style={{ fontSize: "13px" }}>
                This package has 0 student passes, 0 bookings, and 0 payment records. This action is permanent and cannot be undone.
              </div>
              <div className="modal-actions" style={{ marginTop: "20px" }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setDeleteModalOpen(false)}
                  disabled={modalSubmitting}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn btn-danger"
                  onClick={handleDeleteSubmit}
                  disabled={modalSubmitting}
                >
                  {modalSubmitting ? "Deleting..." : "Permanently Delete"}
                </button>
              </div>
            </div>
          ) : null}
        </div>
      </AdminActionModal>

      {/* Package Details & Activity History Drawer (Batch 3) */}
      {drawerOpen && (
        <div className="admin-drawer-backdrop" onClick={() => setDrawerOpen(false)}>
          <div
            className="admin-drawer"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-label="Package Details and Activity History"
          >
            {/* Drawer Header */}
            <div className="drawer-header">
              <div className="drawer-header-left">
                <div className="drawer-header-title-row">
                  <h3 className="drawer-title">{drawerPackage?.name || "Package Details"}</h3>
                  {drawerDetail && (
                    <AdminBadge tone={drawerDetail.isActive ? "success" : "warning"}>
                      {drawerDetail.isActive ? "Available for New Purchases" : "Archived / Off Sale"}
                    </AdminBadge>
                  )}
                  {drawerDetail?.isFeatured && (
                    <AdminBadge tone="brand" dot={true}>Featured Plan</AdminBadge>
                  )}
                </div>
                {drawerDetail?.description && (
                  <p className="drawer-subtitle">{drawerDetail.description}</p>
                )}
              </div>
              <button
                type="button"
                className="drawer-close-btn"
                onClick={() => setDrawerOpen(false)}
                aria-label="Close drawer"
              >
                ✕
              </button>
            </div>

            {/* Drawer Body */}
            <div className="drawer-body">
              {drawerLoading ? (
                <div style={{ textAlign: "center", padding: "40px 0", color: "#667085" }}>
                  <p>Loading package details...</p>
                </div>
              ) : drawerError ? (
                <div className="alert alert-danger" style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span>{drawerError}</span>
                  <button type="button" className="btn btn-sm btn-secondary" onClick={handleDrawerRetry}>
                    Retry
                  </button>
                </div>
              ) : (
                <>
                  {/* Package Parameters Card */}
                  <div className="drawer-card">
                    <h4 className="drawer-card-title">Package Specifications</h4>
                    <div className="drawer-param-grid">
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Price</span>
                        <span className="drawer-param-val">₹{drawerDetail ? Number(drawerDetail.price).toLocaleString("en-IN") : "—"}</span>
                      </div>
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Included Classes</span>
                        <span className="drawer-param-val">
                          {drawerDetail?.classLimit !== null && drawerDetail?.classLimit !== undefined
                            ? `${drawerDetail.classLimit} Classes`
                            : "Unlimited Classes"}
                        </span>
                      </div>
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Valid For</span>
                        <span className="drawer-param-val">{drawerDetail?.durationDays} Days</span>
                      </div>
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Created Date</span>
                        <span className="drawer-param-val">
                          {drawerDetail?.createdAt ? new Date(drawerDetail.createdAt).toLocaleDateString("en-IN", { day: "numeric", month: "short", year: "numeric" }) : "—"}
                        </span>
                      </div>
                      {drawerDetail?.updatedAt && (
                        <div className="drawer-param-item">
                          <span className="drawer-param-label">Last Updated</span>
                          <span className="drawer-param-val">
                            {new Date(drawerDetail.updatedAt).toLocaleDateString("en-IN", { day: "numeric", month: "short", year: "numeric" })}
                          </span>
                        </div>
                      )}
                    </div>

                    {/* Features List */}
                    {drawerDetail?.features && drawerDetail.features.length > 0 && (
                      <div style={{ marginTop: "14px" }}>
                        <span className="drawer-param-label" style={{ fontWeight: 600 }}>Included Benefits:</span>
                        <ul className="drawer-features-list">
                          {drawerDetail.features.map((feat, idx) => (
                            <li key={idx}>{feat}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                  </div>

                  {/* Student Passes & Usage Breakdown Card */}
                  <div className="drawer-card">
                    <h4 className="drawer-card-title">Student Usage & Activity</h4>
                    <div className="drawer-param-grid">
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Student Packages Purchased</span>
                        <span className="drawer-param-val">{drawerDetail?.studentPackagesPurchased ?? 0}</span>
                      </div>
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Active Student Passes</span>
                        <span className="drawer-param-val" style={{ color: "#027a48" }}>{drawerDetail?.activeStudentPasses ?? 0}</span>
                      </div>
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Total Classes Allocated</span>
                        <span className="drawer-param-val">{drawerDetail?.totalClassesAllocated ?? 0}</span>
                      </div>
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Total Classes Remaining</span>
                        <span className="drawer-param-val" style={{ color: "#4f46e5" }}>{drawerDetail?.totalClassesRemaining ?? 0}</span>
                      </div>
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Class Bookings</span>
                        <span className="drawer-param-val">{drawerDetail?.classEnrollmentRecordsCount ?? 0}</span>
                      </div>
                      <div className="drawer-param-item">
                        <span className="drawer-param-label">Payment Invoices</span>
                        <span className="drawer-param-val">{drawerDetail?.paymentRecordsCount ?? 0}</span>
                      </div>
                    </div>

                    {/* Consumption Percentage & Progress Bar */}
                    <div className="usage-progress-container">
                      <div className="usage-progress-labels">
                        <span>Class Consumption Rate</span>
                        <span>
                          {drawerDetail?.classConsumptionRatePercent !== null && drawerDetail?.classConsumptionRatePercent !== undefined
                            ? `${drawerDetail.classConsumptionRatePercent}% Used`
                            : "No class usage available"}
                        </span>
                      </div>
                      <div className="usage-progress-bar-track">
                        <div
                          className="usage-progress-bar-fill"
                          style={{
                            width: drawerDetail?.classConsumptionRatePercent ? `${Math.min(100, drawerDetail.classConsumptionRatePercent)}%` : "0%",
                            background: drawerDetail?.classConsumptionRatePercent && drawerDetail.classConsumptionRatePercent > 85 ? "#d92d20" : "#12b76a",
                          }}
                        />
                      </div>
                    </div>
                  </div>

                  {/* Activity History Timeline Card */}
                  <div className="drawer-card">
                    <h4 className="drawer-card-title">Activity & Audit History</h4>

                    {drawerActivity.length === 0 ? (
                      <div className="activity-empty-state">
                        No activity has been recorded for this package.
                      </div>
                    ) : (
                      <div className="activity-timeline">
                        {drawerActivity.map((act) => {
                          let badgeClass = "activity-badge-updated";
                          if (act.actionType === "PACKAGE_CREATED") badgeClass = "activity-badge-created";
                          if (act.actionType === "PACKAGE_DEACTIVATED") badgeClass = "activity-badge-deactivated";
                          if (act.actionType === "PACKAGE_ACTIVATED") badgeClass = "activity-badge-activated";
                          if (act.actionType === "PACKAGE_DELETED") badgeClass = "activity-badge-deleted";

                          return (
                            <div key={act.id} className="activity-item-card">
                              <div className="activity-item-header">
                                <span className={`activity-action-badge ${badgeClass}`}>
                                  {act.displayAction}
                                </span>
                                <span className="activity-timestamp">
                                  {new Date(act.timestamp).toLocaleString("en-IN", {
                                    day: "numeric",
                                    month: "short",
                                    year: "numeric",
                                    hour: "2-digit",
                                    minute: "2-digit",
                                  })}
                                </span>
                              </div>

                              <div className="activity-actor">
                                By: <strong>{act.adminName || "Administrator"}</strong>
                                {act.traceId && (
                                  <span style={{ fontSize: "11px", color: "#98a2b3", marginLeft: "8px" }}>
                                    (Trace: {act.traceId.substring(0, 8)}...)
                                  </span>
                                )}
                              </div>

                              {act.reason && (
                                <p className="activity-reason">
                                  "{act.reason}"
                                </p>
                              )}

                              {/* Before / After Update Diff Display */}
                              {act.updateDetails && (act.updateDetails.oldValues || act.updateDetails.newValues) && (
                                <div className="activity-diff-box">
                                  {act.updateDetails.oldValues?.price !== act.updateDetails.newValues?.price && (
                                    <div className="activity-diff-row">
                                      <span>Price:</span>
                                      <span className="activity-diff-old">₹{act.updateDetails.oldValues?.price}</span>
                                      <span className="activity-diff-arrow">→</span>
                                      <span className="activity-diff-new">₹{act.updateDetails.newValues?.price}</span>
                                    </div>
                                  )}
                                  {act.updateDetails.oldValues?.durationDays !== act.updateDetails.newValues?.durationDays && (
                                    <div className="activity-diff-row">
                                      <span>Valid For:</span>
                                      <span className="activity-diff-old">{act.updateDetails.oldValues?.durationDays} Days</span>
                                      <span className="activity-diff-arrow">→</span>
                                      <span className="activity-diff-new">{act.updateDetails.newValues?.durationDays} Days</span>
                                    </div>
                                  )}
                                  {act.updateDetails.oldValues?.classLimit !== act.updateDetails.newValues?.classLimit && (
                                    <div className="activity-diff-row">
                                      <span>Included Classes:</span>
                                      <span className="activity-diff-old">
                                        {act.updateDetails.oldValues?.classLimit !== null && act.updateDetails.oldValues?.classLimit !== undefined ? `${act.updateDetails.oldValues.classLimit} Classes` : "Unlimited"}
                                      </span>
                                      <span className="activity-diff-arrow">→</span>
                                      <span className="activity-diff-new">
                                        {act.updateDetails.newValues?.classLimit !== null && act.updateDetails.newValues?.classLimit !== undefined ? `${act.updateDetails.newValues.classLimit} Classes` : "Unlimited"}
                                      </span>
                                    </div>
                                  )}
                                  {act.updateDetails.oldValues?.name !== act.updateDetails.newValues?.name && (
                                    <div className="activity-diff-row">
                                      <span>Name:</span>
                                      <span className="activity-diff-old">{act.updateDetails.oldValues?.name}</span>
                                      <span className="activity-diff-arrow">→</span>
                                      <span className="activity-diff-new">{act.updateDetails.newValues?.name}</span>
                                    </div>
                                  )}
                                </div>
                              )}
                            </div>
                          );
                        })}

                        {/* Activity Pagination Controls */}
                        {activityTotalCount > 20 && (
                          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "12px" }}>
                            <button
                              type="button"
                              className="btn btn-sm btn-secondary"
                              disabled={activityPage <= 1}
                              onClick={() => handleDrawerPageChange(activityPage - 1)}
                            >
                              Newer Activity
                            </button>
                            <span style={{ fontSize: "12px", color: "#667085" }}>
                              Page {activityPage} of {Math.ceil(activityTotalCount / 20)}
                            </span>
                            <button
                              type="button"
                              className="btn btn-sm btn-secondary"
                              disabled={activityPage >= Math.ceil(activityTotalCount / 20)}
                              onClick={() => handleDrawerPageChange(activityPage + 1)}
                            >
                              Older Activity
                            </button>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                </>
              )}
            </div>

            {/* Drawer Footer */}
            <div className="drawer-footer">
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => setDrawerOpen(false)}
              >
                Close Drawer
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
