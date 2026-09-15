import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";

import { authApi } from "../services/authApi";
import { registerUnauthorizedHandler, resetUnauthorizedGuard } from "../services/apiClient";
import { studentDashboardCache } from "../services/studentDashboardCache";
import { studentStateSync } from "../services/studentStateSync";


const AuthContext = createContext(null);

const TOKEN_KEY = "ethos_access_token";
const USER_KEY = "ethos_user";

function decodeJwt(token) {
  try {
    const payload = token.split(".")[1];

    if (!payload) return null;

    const normalized = payload
      .replace(/-/g, "+")
      .replace(/_/g, "/");

    return JSON.parse(
      decodeURIComponent(
        atob(normalized)
          .split("")
          .map(
            (char) =>
              "%" +
              ("00" + char.charCodeAt(0).toString(16)).slice(-2)
          )
          .join("")
      )
    );
  } catch {
    return null;
  }
}

function getRoles(user, token) {
  if (Array.isArray(user?.roles)) {
    return user.roles;
  }

  const payload = token ? decodeJwt(token) : null;

  const roles =
    payload?.roles ||
    payload?.role ||
    payload?.["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ||
    [];

  return Array.isArray(roles)
    ? roles
    : roles
      ? [roles]
      : [];
}

export function AuthProvider({ children }) {
  const [token, setToken] = useState(
    () => localStorage.getItem(TOKEN_KEY)
  );

  const [user, setUser] = useState(() => {
    try {
      const stored = localStorage.getItem(USER_KEY);
      return stored ? JSON.parse(stored) : null;
    } catch {
      return null;
    }
  });

  const [loading, setLoading] = useState(Boolean(token));
  function clearSession() {

    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);

    resetUnauthorizedGuard();
    studentDashboardCache.clear();
    studentStateSync.clearPhoto();
    setToken(null);
    setUser(null);
  }


  useEffect(() => {
    registerUnauthorizedHandler(() => {
      clearSession();
    });

    return () => {
      registerUnauthorizedHandler(null);
    };
  }, []);

  useEffect(() => {
    let active = true;

    async function restoreSession() {
      if (!token) {
        setLoading(false);
        return;
      }

      try {
        const currentUser = await authApi.me();

        if (!active) return;

        setUser(currentUser);
        localStorage.setItem(
          USER_KEY,
          JSON.stringify(currentUser)
        );
      } catch {
        if (!active) return;

        clearSession();
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    restoreSession();

    return () => {
      active = false;
    };
  }, []);

  async function loginWithPassword(phone, password) {
    const result = await authApi.login(phone, password);
    return result;
  }

  async function loginWithOtp(phoneNumber, otp, purpose = null) {
    const result = await authApi.verifyOtp(
      phoneNumber,
      otp,
      purpose
    );

    const accessToken =
      result?.accessToken ||
      result?.token;

    if (!accessToken) {
      throw new Error(
        "Authentication succeeded but no access token was returned."
      );
    }

    const currentUser =
      result?.user || {
        roles: getRoles(result?.user, accessToken),
      };

    localStorage.setItem(
      TOKEN_KEY,
      accessToken
    );

    localStorage.setItem(
      USER_KEY,
      JSON.stringify(currentUser)
    );

    resetUnauthorizedGuard();
    setToken(accessToken);
    setUser(currentUser);

    return {
      token: accessToken,
      user: currentUser,
    };
  }

  async function changePassword(currentPassword, newPassword, confirmNewPassword) {
    return await authApi.changePassword(
      currentPassword,
      newPassword,
      confirmNewPassword
    );
  }

  function logout() {
    clearSession();
  }

  const roles = useMemo(
    () => getRoles(user, token),
    [user, token]
  );

  const isTrainer = roles.includes("TRAINER");
  const isStudent = roles.includes("STUDENT");

  const value = {
    token,
    user,
    roles,
    isTrainer,
    isStudent,
    loading,
    loginWithPassword,
    loginWithOtp,
    verifyOtp: loginWithOtp,
    changePassword,
    logout,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error(
      "useAuth must be used inside AuthProvider"
    );
  }

  return context;
}
