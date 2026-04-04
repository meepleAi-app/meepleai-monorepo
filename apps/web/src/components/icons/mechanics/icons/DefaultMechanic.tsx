export function DefaultMechanicIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Board Game"
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
      {/* Meeple shape — generic board game piece */}
      <circle cx="12" cy="5" r="2.5" />
      {/* Body */}
      <path d="M7 10 C7 8.5 8.5 8 10 8.5 L12 9 L14 8.5 C15.5 8 17 8.5 17 10 L16 15 L14 14 L14 19 C14 19.5 13.5 20 13 20 L11 20 C10.5 20 10 19.5 10 19 L10 14 L8 15 Z" />
    </svg>
  );
}
