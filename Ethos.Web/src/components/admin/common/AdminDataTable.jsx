import React from "react";
import "./AdminDataTable.css";

export default function AdminDataTable({
  columns = [],
  data = [],
  loading = false,
  emptyMessage = "No records found matching your filters.",
  emptyIcon = "🔍",
  page = 1,
  pageSize = 20,
  totalItems = 0,
  onPageChange,
  onRowClick,
  className = "",
}) {
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const isClickable = typeof onRowClick === "function";

  return (
    <div className={`admin-data-table-container ${className}`}>
      <div className="admin-table-wrapper" tabIndex={0} role="region" aria-label="Data Table">
        <table className="admin-data-table">
          <thead>
            <tr>
              {columns.map((col) => (
                <th
                  key={col.key || col.header || col.label}
                  style={col.width ? { width: col.width } : undefined}
                  scope="col"
                >
                  {col.header || col.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {loading ? (
              // Loading Skeleton
              Array.from({ length: Math.min(pageSize, 5) }).map((_, idx) => (
                <tr key={`skeleton-${idx}`} className="admin-row-skeleton">
                  {columns.map((col, colIdx) => (
                    <td key={`skel-col-${colIdx}`}>
                      <div className="skeleton-cell" />
                    </td>
                  ))}
                </tr>
              ))
            ) : data.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="admin-table-empty">
                  <div className="empty-content">
                    <span className="empty-icon">{emptyIcon}</span>
                    <p className="empty-text">{emptyMessage}</p>
                  </div>
                </td>
              </tr>
            ) : (
              data.map((row, rowIdx) => (
                <tr
                  key={row.id || rowIdx}
                  className={`admin-table-row ${isClickable ? "clickable" : ""}`}
                  onClick={isClickable ? () => onRowClick(row) : undefined}
                  tabIndex={isClickable ? 0 : undefined}
                  onKeyDown={
                    isClickable
                      ? (e) => {
                          if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            onRowClick(row);
                          }
                        }
                      : undefined
                  }
                >
                  {columns.map((col) => (
                    <td key={col.key || col.header}>
                      {col.render ? col.render(row) : row[col.key]}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {onPageChange && totalPages > 1 && (
        <div className="admin-table-pagination">
          <div className="pagination-info">
            Showing {(page - 1) * pageSize + 1} - {Math.min(page * pageSize, totalItems)} of {totalItems} items
          </div>
          <div className="pagination-controls">
            <button
              type="button"
              className="pagination-btn"
              disabled={page <= 1 || loading}
              onClick={() => onPageChange(page - 1)}
              aria-label="Previous page"
            >
              Previous
            </button>
            <span className="pagination-current">
              Page {page} of {totalPages}
            </span>
            <button
              type="button"
              className="pagination-btn"
              disabled={page >= totalPages || loading}
              onClick={() => onPageChange(page + 1)}
              aria-label="Next page"
            >
              Next
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
