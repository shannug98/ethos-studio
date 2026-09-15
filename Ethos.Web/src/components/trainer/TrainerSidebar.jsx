import { NavLink } from "react-router-dom";
import { useEffect, useState } from "react";
import {
  Home,
  User,
  Calendar,
  TrendingUp,
  Gem,
  Bell,
  LogOut,
} from "lucide-react";

import { useAuth } from "../../context/AuthContext";
import { useTrainerPermissions } from "../../hooks/useTrainerPermissions";
import { TRAINER_PERMISSIONS } from "../../constants/trainerPermissions";
import { trainerApi } from "../../services/trainerApi";
import { trainerStateSync } from "../../services/trainerStateSync";
import { getMediaUrl } from "../../utils/mediaUrl";
import ethosEmblem from "../../assets/brand/ethos-emblem.png";

const navigation = [
  {
    label: "Overview",
    path: "/trainer/dashboard",
    icon: Home,
    permission: TRAINER_PERMISSIONS.VIEW_DASHBOARD,
  },
  {
    label: "My Profile",
    path: "/trainer/profile",
    icon: User,
    permission: TRAINER_PERMISSIONS.VIEW_PROFILE,
  },
  {
    label: "Workshops",
    path: "/trainer/workshops",
    icon: Calendar,
    permission: TRAINER_PERMISSIONS.VIEW_WORKSHOPS,
  },
  {
    label: "Performance",
    path: "/trainer/performance",
    icon: TrendingUp,
    permission: TRAINER_PERMISSIONS.VIEW_PERFORMANCE,
  },
  {
    label: "My Tier",
    path: "/trainer/tier",
    icon: Gem,
    permission: TRAINER_PERMISSIONS.VIEW_TIER,
  },
  {
    label: "Notifications",
    path: "/trainer/notifications",
    icon: Bell,
    permission: TRAINER_PERMISSIONS.VIEW_NOTIFICATIONS,
  },
];

export default function TrainerSidebar() {
  const [open, setOpen] = useState(false);
  const [photoUrl, setPhotoUrl] = useState(null);

  const { user, logout } = useAuth();
  const { hasPermission } = useTrainerPermissions();

  useEffect(() => {
    let mounted = true;
    trainerApi.getProfile()
      .then((data) => {
        if (mounted && data?.profilePhotoUrl) {
          setPhotoUrl(getMediaUrl(data.profilePhotoUrl));
        }
      })
      .catch(() => {});
    const unsubPhoto = trainerStateSync.onPhotoUpdated((url) => {
      if (mounted && url) {
        setPhotoUrl(getMediaUrl(url));
      }
    });

    return () => {
      mounted = false;
      unsubPhoto();
    };
  }, []);

  const displayName =
    user?.name ||
    user?.fullName ||
    user?.firstName ||
    "Trainer";

  const visibleNav = navigation.filter(
    (item) =>
      !item.permission ||
      hasPermission(item.permission)
  );

  useEffect(() => {
    if (!open) return;

    function handleKeyDown(event) {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;

    function handleResize() {
      if (window.innerWidth > 950) {
        setOpen(false);
      }
    }

    window.addEventListener("resize", handleResize);

    return () => {
      window.removeEventListener("resize", handleResize);
    };
  }, [open]);

  return (
    <>
      <button
        type="button"
        className="trainer-mobile-trigger"
        onClick={() => setOpen((value) => !value)}
        aria-label={
          open
            ? "Close trainer navigation"
            : "Open trainer navigation"
        }
        aria-expanded={open}
        aria-controls="trainer-sidebar"
      >
        <span />
        <span />
        <span />
      </button>

      {open && (
        <button
          type="button"
          className="trainer-sidebar-overlay"
          aria-label="Close trainer navigation"
          onClick={() => setOpen(false)}
        />
      )}

      <aside
        id="trainer-sidebar"
        className={`trainer-sidebar ${
          open ? "trainer-sidebar--open" : ""
        }`}
        aria-label="Trainer navigation"
      >
        <div className="trainer-brand">
          <div className="trainer-brand-mark" style={{ overflow: "hidden", padding: "4px" }}>
            <img
              src={ethosEmblem}
              alt="Ethos Emblem"
              style={{ width: "100%", height: "100%", objectFit: "contain" }}
            />
          </div>

          <div>
            <strong>ETHOS</strong>
            <small>TRAINER STUDIO</small>
          </div>
        </div>

        <div className="trainer-sidebar-profile">
          <div className="trainer-avatar" style={{ overflow: "hidden" }}>
            {photoUrl ? (
              <img
                src={photoUrl}
                alt={displayName}
                style={{ width: "100%", height: "100%", objectFit: "cover" }}
              />
            ) : (
              displayName.charAt(0).toUpperCase()
            )}
          </div>

          <div>
            <strong>{displayName}</strong>
            <span>Trainer</span>
          </div>
        </div>

        <nav
          className="trainer-navigation"
          aria-label="Trainer portal sections"
        >
          {visibleNav.map((item) => {
            const IconComponent = item.icon;
            return (
              <NavLink
                key={item.path + item.label}
                to={item.path}
                end={item.path === "/trainer/dashboard"}
                onClick={() => setOpen(false)}
                className={({ isActive }) =>
                  `trainer-nav-link ${
                    isActive ? "trainer-nav-link--active" : ""
                  }`
                }
              >
                <span className="trainer-nav-icon" aria-hidden="true">
                  <IconComponent size={16} />
                </span>

                <span>{item.label}</span>
              </NavLink>
            );
          })}
        </nav>

        <div className="trainer-sidebar-bottom">
          <button
            type="button"
            className="trainer-logout"
            onClick={logout}
          >
            <span className="trainer-nav-icon" aria-hidden="true">
              <LogOut size={16} />
            </span>
            Sign out
          </button>
        </div>
      </aside>
    </>
  );
}

