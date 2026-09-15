import { useEffect, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { Bell, Menu } from "lucide-react";

import { useAuth } from "../../context/AuthContext";
import { studentApi } from "../../services/studentApi";
import { studentStateSync } from "../../services/studentStateSync";
import StudentAvatar from "./StudentAvatar";

const pageTitles = [
  { match: (p) => p === "/student/dashboard" || p === "/student", title: "Student Studio" },
  { match: (p) => p.startsWith("/student/classes"), title: "My Classes & Schedule" },
  { match: (p) => p.startsWith("/student/workshops") && p !== "/student/my-workshops", title: "Studio Workshops" },
  { match: (p) => p.startsWith("/student/my-workshops"), title: "My Bookings & Passes" },
  { match: (p) => p.startsWith("/student/packages") || p === "/packages", title: "My Membership Packages" },
  { match: (p) => p.startsWith("/student/feedback"), title: "Learning & Reviews" },
  { match: (p) => p.startsWith("/student/profile"), title: "Student Profile" },
  { match: (p) => p.startsWith("/student/notifications"), title: "Notifications" },
];

export default function StudentTopbar({ onToggleMobile }) {
  const location = useLocation();
  const { user } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);
  const [photoUrl, setPhotoUrl] = useState(user?.profilePhotoUrl || null);
  const [studentName, setStudentName] = useState(
    user?.fullName || user?.name || user?.firstName || "Student"
  );

  useEffect(() => {
    let mounted = true;

    studentApi
      .getUnreadNotificationCount()
      .then((res) => {
        if (mounted && typeof res?.unreadCount === "number") {
          setUnreadCount(res.unreadCount);
        }
      })
      .catch(() => {});

    studentApi
      .getProfile()
      .then((data) => {
        if (mounted && data) {
          if (data.profilePhotoUrl) setPhotoUrl(data.profilePhotoUrl);
          if (data.fullName) setStudentName(data.fullName);
        }
      })
      .catch(() => {});

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

  const currentTitle =
    pageTitles.find((item) => item.match(location.pathname))?.title ||
    "Student Studio";

  const firstName = studentName.split(" ")[0] || "Student";

  return (
    <header className="student-topbar">
      <div className="student-topbar-left">
        <button
          type="button"
          className="student-mobile-toggle"
          onClick={onToggleMobile}
          aria-label="Open Navigation Menu"
        >
          <Menu size={20} />
        </button>

        <div>
          <span className="student-topbar-eyebrow">ETHOS DANCE STUDIO</span>
          <h1 className="student-topbar-title">{currentTitle}</h1>
        </div>
      </div>

      <div className="student-topbar-right">
        <div className="student-membership-chip">
          <span className="student-membership-dot" />
          <span>Active Membership</span>
        </div>

        <Link
          to="/student/notifications"
          className="student-topbar-bell"
          aria-label="Notifications"
        >
          <Bell size={18} />
          {unreadCount > 0 && <span className="student-topbar-bell-badge" />}
        </Link>

        <Link to="/student/profile" className="student-topbar-profile-link" style={{ display: "flex", alignItems: "center", gap: "10px", textDecoration: "none", color: "inherit" }}>
          <StudentAvatar
            photoUrl={photoUrl}
            name={studentName}
            size={32}
          />
          <div className="student-topbar-greeting">
            Welcome, <strong>{firstName}</strong>
          </div>
        </Link>
      </div>
    </header>
  );
}
