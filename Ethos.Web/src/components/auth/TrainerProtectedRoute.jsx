import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";

export default function TrainerProtectedRoute() {
  const {
    loading,
    token,
    isTrainer,
  } = useAuth();

  const location = useLocation();

  if (loading) {
    return (
      <div className="trainer-loading-screen">
        <div className="trainer-loader" />
        <span>Loading your studio</span>
      </div>
    );
  }

  if (!token) {
    const returnUrl = `${location.pathname}${location.search}${location.hash}`;

    return (
      <Navigate
        to={`/trainer/login?returnUrl=${encodeURIComponent(returnUrl)}`}
        replace
      />
    );
  }

  if (!isTrainer) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
