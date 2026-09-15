export default function SuccessState({
  title = "Success",
  description,
  action,
}) {
  return (
    <div className="trainer-success-state">
      <div className="trainer-success-symbol" aria-hidden="true">
        ✓
      </div>

      <h3>{title}</h3>

      {description && (
        <p>{description}</p>
      )}

      {action}
    </div>
  );
}
