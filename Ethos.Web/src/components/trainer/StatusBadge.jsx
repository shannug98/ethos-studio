export default function StatusBadge({
  status,
}) {
  if (!status) return null;

  const normalized =
    String(status)
      .replace(/([a-z])([A-Z])/g, "$1 $2")
      .replace(/_/g, " ")
      .toLowerCase();

  return (
    <span
      className={`trainer-status trainer-status--${normalized
        .replace(/\s+/g, "-")}`}
    >
      {normalized}
    </span>
  );
}
