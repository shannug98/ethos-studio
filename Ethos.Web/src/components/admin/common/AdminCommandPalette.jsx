import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { getAdminUser, adminApi } from "../../../services/adminApi";
import { ADMIN_MODULE_REGISTRY } from "../../../constants/adminRouteRegistry";
import "./AdminCommandPalette.css";

export default function AdminCommandPalette({ isOpen, onClose, onThemeChange, currentTheme }) {
  const [query, setQuery] = useState("");
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [dataResults, setDataResults] = useState([]);
  const [isSearchingBackend, setIsSearchingBackend] = useState(false);

  const inputRef = useRef(null);
  const debounceTimerRef = useRef(null);
  const navigate = useNavigate();
  const adminUser = getAdminUser();
  const permissions = adminUser?.permissions || [];
  const hasFullAccess = adminUser?.roles?.includes("ADMIN");

  const hasPerm = (perm) => hasFullAccess || permissions.includes(perm);

  // Level 1: Instant page and navigation commands
  const navCommands = ADMIN_MODULE_REGISTRY
    .filter((m) => m.isNavigable)
    .map((m) => ({
      id: `nav-${m.id}`,
      title: m.label,
      subtitle: m.description,
      category: "Pages & Modules",
      icon: m.icon,
      action: () => navigate(m.path),
      available: !m.requiredPermission || hasPerm(m.requiredPermission),
    }));

  const themeCommands = [
    {
      id: "theme-obsidian",
      title: "Switch Theme: Obsidian Dark",
      category: "Theme & Display",
      icon: "🌑",
      action: () => onThemeChange("obsidian"),
      available: true,
      active: currentTheme === "obsidian",
    },
    {
      id: "theme-light",
      title: "Switch Theme: Clean Light",
      category: "Theme & Display",
      icon: "☀️",
      action: () => onThemeChange("light"),
      available: true,
      active: currentTheme === "light",
    },
    {
      id: "theme-contrast",
      title: "Switch Theme: High-Contrast Accessibility",
      category: "Theme & Display",
      icon: "👁️",
      action: () => onThemeChange("contrast"),
      available: true,
      active: currentTheme === "contrast",
    },
  ];

  const localCommands = [...navCommands, ...themeCommands];

  const filteredLocal = localCommands.filter(
    (cmd) =>
      cmd.available &&
      (cmd.title.toLowerCase().includes(query.toLowerCase()) ||
        cmd.category.toLowerCase().includes(query.toLowerCase()) ||
        (cmd.subtitle && cmd.subtitle.toLowerCase().includes(query.toLowerCase())))
  );

  // Level 2: Debounced live backend data search (Students, Trainers, Workshops)
  useEffect(() => {
    if (!query || query.trim().length < 2) {
      setDataResults([]);
      setIsSearchingBackend(false);
      return;
    }

    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }

    debounceTimerRef.current = setTimeout(async () => {
      setIsSearchingBackend(true);
      const q = query.trim();

      try {
        const results = [];

        // Parallel targeted lookups
        const [studentsRes, trainersRes, workshopsRes] = await Promise.allSettled([
          adminApi.getStudents(`search=${encodeURIComponent(q)}&pageSize=4`),
          adminApi.getTrainers(`search=${encodeURIComponent(q)}&pageSize=4`),
          adminApi.getWorkshops(`search=${encodeURIComponent(q)}&pageSize=4`),
        ]);

        if (studentsRes.status === "fulfilled" && Array.isArray(studentsRes.value?.items)) {
          studentsRes.value.items.forEach((s) => {
            results.push({
              id: `student-${s.id}`,
              title: s.fullName || s.email || "Student",
              subtitle: `Student · ${s.phone || s.customerCode || "Registered"}`,
              category: "Students",
              icon: "👥",
              action: () => navigate(`/admin_portal/students/${s.id}`),
            });
          });
        }

        if (trainersRes.status === "fulfilled" && Array.isArray(trainersRes.value?.items)) {
          trainersRes.value.items.forEach((t) => {
            results.push({
              id: `trainer-${t.id}`,
              title: t.fullName || "Trainer",
              subtitle: `Trainer · Tier: ${t.tierName || "Standard"}`,
              category: "Trainers",
              icon: "👤",
              action: () => navigate(`/admin_portal/trainers/${t.id}`),
            });
          });
        }

        if (workshopsRes.status === "fulfilled" && Array.isArray(workshopsRes.value?.items)) {
          workshopsRes.value.items.forEach((w) => {
            results.push({
              id: `workshop-${w.id}`,
              title: w.title || "Workshop",
              subtitle: `Workshop · ${w.danceStyle || "General"}`,
              category: "Workshops",
              icon: "🎪",
              action: () => navigate(`/admin_portal/workshops`),
            });
          });
        }

        setDataResults(results);
      } catch (err) {
        console.warn("Backend search lookup error:", err);
      } finally {
        setIsSearchingBackend(false);
      }
    }, 280);

    return () => {
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, [query, navigate]);

  const allItems = [...filteredLocal, ...dataResults];

  useEffect(() => {
    if (isOpen) {
      setQuery("");
      setSelectedIndex(0);
      setDataResults([]);
      setTimeout(() => inputRef.current?.focus(), 50);

      const handleGlobalKeyDown = (e) => {
        if (e.key === "Escape") {
          e.preventDefault();
          onClose();
        }
      };
      window.addEventListener("keydown", handleGlobalKeyDown);
      return () => window.removeEventListener("keydown", handleGlobalKeyDown);
    }
  }, [isOpen, onClose]);

  useEffect(() => {
    setSelectedIndex(0);
  }, [query, dataResults.length]);

  const handleKeyDown = (e) => {
    if (e.key === "Escape") {
      e.preventDefault();
      onClose();
    } else if (e.key === "ArrowDown") {
      e.preventDefault();
      setSelectedIndex((prev) => (prev + 1) % Math.max(1, allItems.length));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setSelectedIndex((prev) =>
        prev <= 0 ? allItems.length - 1 : prev - 1
      );
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (allItems[selectedIndex]) {
        allItems[selectedIndex].action();
        onClose();
      }
    }
  };

  if (!isOpen) return null;

  return (
    <div className="admin-palette-backdrop" onClick={onClose}>
      <div
        className="admin-palette-container"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label="Search & Commands"
      >
        <div className="admin-palette-search">
          <span className="palette-search-icon">🔍</span>
          <input
            ref={inputRef}
            type="text"
            className="palette-search-input"
            placeholder="Search students, trainers, workshops, bookings, payments, or pages..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
          />
          {isSearchingBackend && <span className="palette-search-spinner" title="Searching records...">⏳</span>}
          <kbd className="palette-kbd">ESC</kbd>
        </div>

        <div className="admin-palette-results">
          {allItems.length === 0 ? (
            <div className="palette-empty">
              {isSearchingBackend ? "Searching studio records..." : "No matching pages, commands, or records found."}
            </div>
          ) : (
            allItems.map((item, idx) => (
              <div
                key={item.id}
                className={`palette-item ${idx === selectedIndex ? "selected" : ""} ${
                  item.active ? "active" : ""
                }`}
                onClick={() => {
                  item.action();
                  onClose();
                }}
                onMouseEnter={() => setSelectedIndex(idx)}
              >
                <span className="palette-item-icon">{item.icon}</span>
                <div className="palette-item-text">
                  <div className="palette-item-title-row">
                    <span className="palette-item-title">{item.title}</span>
                    <span className="palette-item-category-pill">{item.category}</span>
                  </div>
                  {item.subtitle && <span className="palette-item-category">{item.subtitle}</span>}
                </div>
                {item.active && <span className="palette-active-tag">Active</span>}
              </div>
            ))
          )}
        </div>

        <div className="admin-palette-footer">
          <span>Navigate with <kbd className="palette-kbd">↑</kbd> <kbd className="palette-kbd">↓</kbd> · Select with <kbd className="palette-kbd">↵ Enter</kbd> · Close with <kbd className="palette-kbd">Esc</kbd></span>
        </div>
      </div>
    </div>
  );
}
