export function CooperativeIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Cooperative"
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
      {/* Handshake - two hands clasping */}
      <path d="M4 12 C4 10 6 9 8 10 L12 12 L16 10 C18 9 20 10 20 12" />
      <path d="M8 10 L8 7 C8 6 9 5 10 5 L12 5" />
      <path d="M16 10 L16 7 C16 6 15 5 14 5 L12 5" />
      <path d="M4 12 L4 15 C4 16 5 17 6 17 L18 17 C19 17 20 16 20 15 L20 12" />
    </svg>
  );
}
