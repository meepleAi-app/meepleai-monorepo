export function DungeonCrawlerIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Dungeon Crawler"
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
      {/* Shield */}
      <path d="M12 3 L20 6 L20 13 C20 17 16 20 12 21 C8 20 4 17 4 13 L4 6 Z" />
      {/* Sword overlapping shield */}
      <line x1="12" y1="8" x2="12" y2="17" />
      <line x1="9" y1="11" x2="15" y2="11" />
      <circle cx="12" cy="7" r="1" fill="currentColor" stroke="none" />
    </svg>
  );
}
