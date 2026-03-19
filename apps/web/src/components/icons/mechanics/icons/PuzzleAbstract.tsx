export function PuzzleAbstractIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Puzzle Abstract"
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
      {/* Puzzle piece shape */}
      <path d="M4 4 L10 4 L10 7 C10 7 11 6 12 7 C13 8 12 9 12 9 L10 9 L10 14 L7 14 C7 14 8 13 7 12 C6 11 5 12 5 12 L5 14 L4 14 Z" />
      {/* Second interlocking piece */}
      <path d="M14 10 L19 10 L19 14 L16 14 C16 14 17 15 16 16 C15 17 14 16 14 16 L14 20 L10 20 L10 17 C10 17 9 18 8 17 C7 16 8 15 8 15 L10 15 L10 10 Z" />
    </svg>
  );
}
