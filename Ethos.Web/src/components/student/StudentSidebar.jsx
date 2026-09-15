import { NavLink, useNavigate, useLocation } from "react-router-dom";
import { useEffect, useState } from "react";
import {
  Home,
  Calendar,
  Sparkles,
  Ticket,
  Package,
  Award,
  Bell,
  User,
  LogOut,
} from "lucide-react";

import { useAuth } from "../../context/AuthContext";
import { studentApi } from "../../services/studentApi";
import { studentStateSync } from "../../services/studentStateSync";
import StudentAvatar from "./StudentAvatar";
import ethosEmblem from "../../assets/brand/ethos-emblem.png";

const navigation = [
  {
    label: "Overview",
    path: "/student/dashboard",
    icon: Home,
    match: (pathname) => pathname === "/student/dashboard" || pathname === "/student",
  },
  {
    label: "My Classes",
    path: "/student/classes",
    icon: Calendar,
    match: (pathname) => pathname.startsWith("/student/classes"),
  },
  {
    label: "Workshops",
    path: "/student/workshops",
    icon: Sparkles,
    match: (pathname) => pathname.startsWith("/student/workshops") && pathname !== "/student/my-workshops",
  },
  {
    label: "My Bookings",
    path: "/student/my-workshops",
    icon: Ticket,
    match: (pathname) => pathname.startsWith("/student/my-workshops"),
  },
  {
    label: "My Packages",
    path: "/student/packages",
    icon: Package,
    match: (pathname) => pathname.startsWith("/student/packages") || pathname === "/packages",
  },
  {
    label: "Learning & Reviews",
    path: "/student/feedback",
    icon: Award,
    match: (pathname) => pathname.startsWith("/student/feedback"),
  },
  {
    label: "Notifications",
    path: "/student/notifications",
    icon: Bell,
    hasBadge: true,
    match: (pathname) => pathname.startsWith("/student/notifications"),
  },
  {
    label: "My Profile",
    path: "/student/profile",
    icon: User,
    match: (pathname) => pathname.startsWith("/student/profile"),
  },
];

export default function StudentSidebar({ isOpen, onClose }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [photoUrl, setPhotoUrl] = useState(user?.profilePhotoUrl || null);
  const [studentName, setStudentName] = useState(
    user?.fullName || user?.name || user?.firstName || "Student"
  );
  const [unreadCount, setUnreadCount] = useState(0);

  useEffect(() => {
    let mounted = true;

    // Load initial profile and unread count
    studentApi
      .getProfile()
      .then((data) => {
        if (mounted && data) {
          if (data.profilePhotoUrl) setPhotoUrl(data.profilePhotoUrl);
          if (data.fullName) setStudentName(data.fullName);
        }
      })
      .catch(() => {});

    studentApi
      .getUnreadNotificationCount()
      .then((res) => {
        if (mounted && typeof res?.unreadCount === "number") {
          setUnreadCount(res.unreadCount);
        }
      })
      .catch(() => {});

    // Subscribe to live state updates
    const unsubUnread = studentStateSync.onUnreadCount((count) => {
      if (mounted) setUnreadCount(count);
    });

    const unsubProfile = studentStateSync.onProfileUpdated((profile) => {
      if (mounted && profile) {
        if (profile.profilePhotoUrl !== undefined) setPhotoUrl(profile.profilePhotoUrl);
        if (profile.fullName) setStudentName(profile.fullName);
      }
    });

    const unsubPhoto = studentStateSync.onPhotoUpdated((url) => {
      if (mounted && url !== undefined) {
        setPhotoUrl(url);
      }
    });

    return () => {
      mounted = false;
      unsubUnread();
      unsubProfile();
      unsubPhoto();
    };
  }, []);

  const handleSignOut = () => {
    logout();
    navigate("/student/login", { replace: true });
  };

  return (
    <>
      {isOpen && (
        <div
          className="student-sidebar-overlay"
          onClick={onClose}
          aria-hidden="true"
        />
      )}

      <aside className={`student-sidebar ${isOpen ? "open" : ""}`}>
        {/* BRAND */}
        <NavLink to="/student/dashboard" className="student-brand" onClick={onClose}>
          <div className="student-brand-emblem">
            <img src={ethosEmblem} alt="Ethos" />
          </div>
          <div className="student-brand-text">
            <strong>ETHOS STUDIO</strong>
            <small>STUDENT PORTAL</small>
          </div>
        </NavLink>

        {/* PROFILE IDENTITY CARD */}
        <div className="student-sidebar-profile">
          <StudentAvatar
            photoUrl={photoUrl}
            name={studentName}
            size={36}
            clickable
          />
          <NavLink
            to="/student/profile"
            className="student-sidebar-meta"
            onClick={onClose}
            style={{ textDecoration: "none", color: "inherit", flex: 1, overflow: "hidden" }}
          >
            <strong>{studentName}</strong>
            <span>Active Student</span>
          </NavLink>
        </div>

        {/* NAVIGATION */}
        <nav className="student-navigation" aria-label="Student Navigation">
          {navigation.map((item) => {
            const Icon = item.icon;
            const isActive = item.match(location.pathname);

            return (
              <NavLink
                key={item.path}
                to={item.path}
                className={`student-nav-item ${isActive ? "active" : ""}`}
                onClick={onClose}
              >
                <Icon size={18} />
                <span>{item.label}</span>
                {item.hasBadge && unreadCount > 0 && (
                  <span className="student-nav-badge">{unreadCount}</span>
                )}
              </NavLink>
            );
          })}
        </nav>

        {/* SIGN OUT */}
        <div className="student-sidebar-footer">
          <button
            type="button"
            className="student-signout-btn"
            onClick={handleSignOut}
          >
            <LogOut size={16} />
            <span>SIGN OUT</span>
          </button>
        </div>
      </aside>
    </>
  );
}
