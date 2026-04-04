export function DeckBuildingIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Deck Building"
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
      {/* Stacked cards */}
      <rect x="5" y="10" width="12" height="10" rx="1" />
      <rect x="7" y="7" width="12" height="10" rx="1" />
      <rect x="9" y="4" width="12" height="10" rx="1" />
    </svg>
  );
}
