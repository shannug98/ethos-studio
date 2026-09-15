import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";

export default function StudentProtectedRoute() {
  const {
    loading,
    token,
    isStudent,
  } = useAuth();

  const location = useLocation();

  if (loading) {
    return (
      <div
        className="student-loading-screen"
        style={{
          minHeight: "100vh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          background: "#0d0c0b",
          color: "#f7f0e9",
          fontFamily: "var(--font-sans, Arial, sans-serif)",
          fontSize: "13px",
          letterSpacing: "0.1em",
          textTransform: "uppercase"
        }}
      >
        <span>Loading your student portal...</span>
      </div>
    );
  }

  if (!token) {
    const returnUrl = `${location.pathname}${location.search}${location.hash}`;
    return (
      <Navigate
        to={`/student/login?returnUrl=${encodeURIComponent(returnUrl)}`}
        replace
      />
    );
  }

  if (!isStudent) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
