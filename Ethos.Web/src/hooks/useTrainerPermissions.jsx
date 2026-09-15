import { createContext, useContext, useEffect, useState, useMemo, useCallback } from "react";
import { trainerApi } from "../services/trainerApi";
import { TRAINER_PERMISSIONS } from "../constants/trainerPermissions";
import { useAuth } from "../context/AuthContext";

const TrainerPermissionsContext = createContext(null);

export function TrainerPermissionsProvider({ children }) {
  const { token, isTrainer } = useAuth();
  const [permissions, setPermissions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchPermissions = useCallback(async () => {
    if (!token || !isTrainer) {
      setPermissions([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await trainerApi.getPermissions();
      setPermissions(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load permissions");
      setPermissions([]);
    } finally {
      setLoading(false);
    }
  }, [token, isTrainer]);

  useEffect(() => {
    fetchPermissions();
  }, [fetchPermissions]);

  const permissionMap = useMemo(() => {
    const map = new Map();
    (permissions || []).forEach((item) => {
      if (item && item.code) {
        map.set(item.code, Boolean(item.isAllowed));
      }
    });
    return map;
  }, [permissions]);

  const hasPermission = useCallback(
    (code) => {
      if (!code) return false;
      if (!permissionMap.has(code)) {
        return false;
      }
      return Boolean(permissionMap.get(code));
    },
    [permissionMap]
  );

  const canRequestUpgrade = useMemo(() => {
    return (
      hasPermission(TRAINER_PERMISSIONS.REQUEST_UPGRADE) ||
      hasPermission(TRAINER_PERMISSIONS.REQUEST_TIER_UPGRADE)
    );
  }, [hasPermission]);

  const value = useMemo(
    () => ({
      permissions,
      loading,
      error,
      hasPermission,
      canRequestUpgrade,
      refetchPermissions: fetchPermissions,
    }),
    [permissions, loading, error, hasPermission, canRequestUpgrade, fetchPermissions]
  );

  return (
    <TrainerPermissionsContext.Provider value={value}>
      {children}
    </TrainerPermissionsContext.Provider>
  );
}

export function useTrainerPermissions() {
  const context = useContext(TrainerPermissionsContext);

  if (!context) {
    return {
      permissions: [],
      loading: false,
      error: null,
      hasPermission: () => true,
      canRequestUpgrade: true,
      refetchPermissions: () => {},
    };
  }

  return context;
}
