import { Outlet } from "react-router-dom";
import { useTrainerPermissions } from "../../hooks/useTrainerPermissions";

export default function TrainerPermissionRoute({ permission, children }) {
  const { loading, hasPermission } = useTrainerPermissions();

  if (loading) {
    return (
      <div className="trainer-loading-screen">
        <div className="trainer-loader" />
        <span>Verifying permissions...</span>
      </div>
    );
  }

  if (permission && !hasPermission(permission)) {
    return (
      <div
        className="trainer-permission-denied"
        style={{
          padding: "4rem 2rem",
          textAlign: "center",
          color: "#fff",
          maxWidth: "500px",
          margin: "4rem auto",
          background: "rgba(255, 255, 255, 0.03)",
          border: "1px solid rgba(255, 255, 255, 0.08)",
          borderRadius: "8px",
        }}
      >
        <div style={{ fontSize: "2.5rem", marginBottom: "1rem", color: "#e26d5c" }}>⊘</div>
        <h2 style={{ fontSize: "1.25rem", letterSpacing: "1px", margin: "0 0 0.5rem 0" }}>
          ACCESS RESTRICTED
        </h2>
        <p style={{ color: "#aaa", fontSize: "0.9rem", lineHeight: "1.5" }}>
          Your trainer profile or tier permissions do not allow access to this feature.
        </p>
      </div>
    );
  }

  return children ? children : <Outlet />;
}
