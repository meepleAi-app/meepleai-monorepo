export function SocialDeductionIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Social Deduction"
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
      {/* Eye / mask shape */}
      <path d="M2 12 C6 6 18 6 22 12 C18 18 6 18 2 12 Z" />
      <circle cx="12" cy="12" r="3" />
      <circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" />
      {/* Mask tie strings */}
      <line x1="2" y1="12" x2="0" y2="10" />
      <line x1="22" y1="12" x2="24" y2="10" />
    </svg>
  );
}
