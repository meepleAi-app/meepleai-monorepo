export function AreaControlIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Area Control"
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
      {/* Flag on a pole representing territory control */}
      <line x1="4" y1="22" x2="4" y2="4" />
      <path d="M4 4 L16 7 L4 10 Z" />
      {/* Territory boundary lines */}
      <path d="M8 16 L18 16 L20 20 L6 20 Z" strokeDasharray="2 2" />
    </svg>
  );
}
