import { Navigate, Outlet } from "react-router-dom";
import { isAdminAuthenticated } from "../../services/adminApi";

export default function AdminProtectedRoute({ children }) {
  if (!isAdminAuthenticated()) {
    return <Navigate to="/admin_portal/login" replace />;
  }

  return children ? children : <Outlet />;
}
