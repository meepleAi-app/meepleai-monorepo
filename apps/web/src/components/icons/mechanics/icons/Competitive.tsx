export function CompetitiveIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Competitive"
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
      {/* Crossed swords */}
      <line x1="5" y1="5" x2="19" y2="19" />
      <line x1="19" y1="5" x2="5" y2="19" />
      <line x1="5" y1="8" x2="8" y2="5" />
      <line x1="16" y1="19" x2="19" y2="16" />
      <circle cx="12" cy="12" r="2" />
    </svg>
  );
}
