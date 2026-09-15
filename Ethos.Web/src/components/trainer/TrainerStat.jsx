export default function TrainerStat({
  icon: Icon,
  label,
  value,
  detail,
}) {
  return (
    <article className="trainer-dashboard-stat">
      <div className="trainer-stat-header">
        {Icon && (
          <div className="trainer-stat-icon-box">
            <Icon size={16} />
          </div>
        )}
        <span className="trainer-stat-label">{label}</span>
      </div>

      <strong className="trainer-stat-value">{value ?? "—"}</strong>

      {detail && (
        <small className="trainer-stat-detail">{detail}</small>
      )}
    </article>
  );
}
