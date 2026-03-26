export function NarrativeRpgIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Narrative RPG"
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
      {/* Open book */}
      <path d="M2 6 C2 5 3 4 4 4 L11 4 L11 20 L4 20 C3 20 2 19 2 18 Z" />
      <path d="M22 6 C22 5 21 4 20 4 L13 4 L13 20 L20 20 C21 20 22 19 22 18 Z" />
      <line x1="11" y1="4" x2="13" y2="4" />
      <line x1="11" y1="20" x2="13" y2="20" />
      {/* Text lines on left page */}
      <line x1="5" y1="8" x2="10" y2="8" />
      <line x1="5" y1="11" x2="10" y2="11" />
      <line x1="5" y1="14" x2="8" y2="14" />
    </svg>
  );
}
