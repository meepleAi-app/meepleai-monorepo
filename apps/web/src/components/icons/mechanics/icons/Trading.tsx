export function TradingIcon({ size = 24, className = '' }: { size?: number; className?: string }) {
  return (
    <svg
      role="img"
      aria-label="Trading"
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
      {/* Exchange arrows */}
      <path d="M7 16 L3 12 L7 8" />
      <line x1="3" y1="12" x2="21" y2="12" />
      <path d="M17 8 L21 12 L17 16" />
      {/* Small coins/goods */}
      <circle cx="10" cy="8" r="1.5" />
      <circle cx="14" cy="16" r="1.5" />
    </svg>
  );
}
