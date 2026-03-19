export function WorkerPlacementIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Worker Placement"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
    >
      {/* Meeple-like person */}
      <circle cx="12" cy="5" r="2" />
      <path d="M12 7 L12 14" />
      <path d="M8 10 L16 10" />
      <path d="M12 14 L9 20" />
      <path d="M12 14 L15 20" />
      {/* Placement arrow */}
      <path d="M17 17 L20 17 L20 20 L17 20" strokeDasharray="1 1" />
    </svg>
  );
}
