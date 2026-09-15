import React, { useState } from "react";
import "./AdminExportButton.css";

export default function AdminExportButton({
  data = [],
  filename = "ethos_export",
  disabled = false,
  className = "",
}) {
  const [isOpen, setIsOpen] = useState(false);

  // Sanitize and redact sensitive fields (secrets, tokens, hashes, passwords, and ensure zero GST)
  const sanitizeRecord = (record) => {
    const sanitized = {};
    for (const [key, value] of Object.entries(record)) {
      const lowerKey = key.toLowerCase();
      // Block sensitive auth data
      if (
        lowerKey.includes("password") ||
        lowerKey.includes("token") ||
        lowerKey.includes("secret") ||
        lowerKey.includes("hash") ||
        lowerKey.includes("otp") ||
        lowerKey.includes("salt")
      ) {
        continue;
      }
      // Guarantee zero GST / tax fields
      if (
        lowerKey.includes("gst") ||
        lowerKey.includes("cgst") ||
        lowerKey.includes("sgst") ||
        lowerKey.includes("igst") ||
        lowerKey.includes("tax")
      ) {
        continue;
      }
      // Stringify nested objects
      if (value !== null && typeof value === "object") {
        sanitized[key] = JSON.stringify(value);
      } else {
        sanitized[key] = value;
      }
    }
    return sanitized;
  };

  const exportCsv = () => {
    if (!data || data.length === 0) return;
    const boundedData = data.slice(0, 10000).map(sanitizeRecord);
    const headers = Object.keys(boundedData[0]);

    const csvRows = [
      headers.join(","),
      ...boundedData.map((row) =>
        headers
          .map((fieldName) => {
            const val = row[fieldName] !== undefined && row[fieldName] !== null ? String(row[fieldName]) : "";
            // Escape double quotes
            const escaped = val.replace(/"/g, '""');
            return `"${escaped}"`;
          })
          .join(",")
      ),
    ];

    const blob = new Blob([csvRows.join("\r\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", `${filename}_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setIsOpen(false);
  };

  const exportJson = () => {
    if (!data || data.length === 0) return;
    const boundedData = data.slice(0, 10000).map(sanitizeRecord);
    const blob = new Blob([JSON.stringify(boundedData, null, 2)], {
      type: "application/json",
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", `${filename}_${new Date().toISOString().slice(0, 10)}.json`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setIsOpen(false);
  };

  return (
    <div className={`admin-export-dropdown ${className}`}>
      <button
        type="button"
        className="admin-export-trigger"
        onClick={() => setIsOpen(!isOpen)}
        disabled={disabled || !data || data.length === 0}
        aria-haspopup="true"
        aria-expanded={isOpen}
      >
        <span>📥 Export</span>
        <span className="dropdown-chevron">▾</span>
      </button>

      {isOpen && (
        <>
          <div className="admin-export-overlay" onClick={() => setIsOpen(false)} />
          <div className="admin-export-menu" role="menu">
            <button
              type="button"
              className="admin-export-item"
              onClick={exportCsv}
              role="menuitem"
            >
              📄 Export as CSV
            </button>
            <button
              type="button"
              className="admin-export-item"
              onClick={exportJson}
              role="menuitem"
            >
              📦 Export as JSON
            </button>
          </div>
        </>
      )}
    </div>
  );
}
