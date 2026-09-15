export default function ErrorState({
  title = "Something went wrong",
  description = "We couldn't complete your request. Please try again.",
  action,
}) {
  return (
    <div className="trainer-error-state">
      <div className="trainer-error-symbol" aria-hidden="true">
        !
      </div>

      <h3>{title}</h3>

      <p>{description}</p>

      {action}
    </div>
  );
}
