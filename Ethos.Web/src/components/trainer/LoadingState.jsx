export default function LoadingState({
  label = "Loading...",
}) {
  return (
    <div className="trainer-loading-state">
      <div className="trainer-loader" />
      <span>{label}</span>
    </div>
  );
}
