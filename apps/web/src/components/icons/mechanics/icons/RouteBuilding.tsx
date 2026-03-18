export function RouteBuildingIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Route Building"
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
      {/* Connected path / train track style */}
      <circle cx="4" cy="12" r="2" />
      <circle cx="12" cy="5" r="2" />
      <circle cx="20" cy="12" r="2" />
      <circle cx="12" cy="19" r="2" />
      {/* Connecting lines */}
      <line x1="6" y1="12" x2="10" y2="7" />
      <line x1="14" y1="7" x2="18" y2="11" />
      <line x1="18" y1="13" x2="14" y2="17" />
      <line x1="10" y1="17" x2="6" y2="13" />
      {/* Cross ties (train track) */}
      <line x1="7.5" y1="9" x2="9" y2="10.5" />
      <line x1="15" y1="9" x2="16.5" y2="10.5" />
    </svg>
  );
}
