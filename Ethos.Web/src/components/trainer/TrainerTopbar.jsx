import { useEffect, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { Bell } from "lucide-react";

import { useAuth } from "../../context/AuthContext";
import { trainerApi } from "../../services/trainerApi";
import { trainerStateSync } from "../../services/trainerStateSync";
import { getMediaUrl } from "../../utils/mediaUrl";

const titles = [
  {
    match: (pathname) =>
      pathname === "/trainer/dashboard",
    title: "Trainer Studio",
  },
  {
    match: (pathname) =>
      pathname === "/trainer/profile",
    title: "Your Profile",
  },
  {
    match: (pathname) =>
      pathname === "/trainer/workshops" ||
      pathname.startsWith("/trainer/workshops/"),
    title: "Your Workshops",
  },
  {
    match: (pathname) =>
      pathname === "/trainer/schedule",
    title: "Availability",
  },
  {
    match: (pathname) =>
      pathname === "/trainer/performance",
    title: "Performance",
  },
  {
    match: (pathname) =>
      pathname === "/trainer/tier" ||
      pathname.startsWith("/trainer/tier/"),
    title: "Your Tier",
  },
  {
    match: (pathname) =>
      pathname === "/trainer/notifications",
    title: "Notifications",
  },
];

export default function TrainerTopbar() {
  const location = useLocation();
  const { user } = useAuth();
  const [photoUrl, setPhotoUrl] = useState(null);
  const [unreadCount, setUnreadCount] = useState(0);

  useEffect(() => {
    let mounted = true;

    trainerApi.getProfile()
      .then((data) => {
        if (mounted && data?.profilePhotoUrl) {
          setPhotoUrl(getMediaUrl(data.profilePhotoUrl));
        }
      })
      .catch(() => {});

    trainerApi.getUnreadNotificationCount()
      .then((res) => {
        if (mounted && typeof res?.unreadCount === "number") {
          setUnreadCount(res.unreadCount);
        }
      })
      .catch(() => {});

    const unsubPhoto = trainerStateSync.onPhotoUpdated((url) => {
      if (mounted && url) {
        setPhotoUrl(getMediaUrl(url));
      }
    });

    const unsubUnread = trainerStateSync.onUnreadCount((count) => {
      if (mounted) {
        setUnreadCount(count);
      }
    });

    return () => {
      mounted = false;
      unsubPhoto();
      unsubUnread();
    };
  }, []);

  const title =
    titles.find((item) =>
      item.match(location.pathname)
    )?.title || "Trainer Studio";

  const firstName =
    user?.firstName ||
    user?.name?.split(" ")[0] ||
    user?.fullName?.split(" ")[0] ||
    "Trainer";

  return (
    <header className="trainer-topbar">
      <div>
        <span className="trainer-eyebrow">
          ETHOS DANCE STUDIO
        </span>

        <h1>{title}</h1>
      </div>

      <div className="trainer-topbar-right">
        <Link
          to="/trainer/notifications"
          className="trainer-topbar-bell"
          aria-label={unreadCount > 0 ? `Notifications (${unreadCount} unread)` : "Notifications"}
        >
          <Bell size={18} />
          {unreadCount > 0 && <span className="trainer-topbar-bell-dot" />}
        </Link>

        <div className="trainer-greeting">
          Welcome back, <strong>{firstName}</strong>
        </div>

        <div
          className="trainer-topbar-avatar"
          aria-label={`${firstName} profile`}
          style={{ overflow: "hidden" }}
        >
          {photoUrl ? (
            <img
              src={photoUrl}
              alt={firstName}
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
            />
          ) : (
            firstName.charAt(0).toUpperCase()
          )}
        </div>
      </div>
    </header>
  );
}
