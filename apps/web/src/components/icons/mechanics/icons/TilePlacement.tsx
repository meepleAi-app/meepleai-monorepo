export function TilePlacementIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Tile Placement"
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
      {/* 2x2 grid of tiles with one being placed */}
      <rect x="3" y="3" width="8" height="8" rx="1" />
      <rect x="13" y="3" width="8" height="8" rx="1" />
      <rect x="3" y="13" width="8" height="8" rx="1" />
      {/* Tile being placed (dashed) */}
      <rect x="13" y="13" width="8" height="8" rx="1" strokeDasharray="3 2" />
      {/* Arrow indicating placement */}
      <line x1="17" y1="15" x2="17" y2="19" />
      <polyline points="15,17 17,19 19,17" />
    </svg>
  );
}
