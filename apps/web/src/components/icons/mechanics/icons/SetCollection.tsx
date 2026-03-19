export function SetCollectionIcon({
  size = 24,
  className = '',
}: {
  size?: number;
  className?: string;
}) {
  return (
    <svg
      role="img"
      aria-label="Set Collection"
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
      {/* Fanning cards */}
      <rect x="2" y="8" width="10" height="14" rx="1" transform="rotate(-15 7 15)" />
      <rect x="7" y="7" width="10" height="14" rx="1" />
      <rect x="12" y="8" width="10" height="14" rx="1" transform="rotate(15 17 15)" />
      {/* Star/checkmark indicating collected set */}
      <path
        d="M12 3 L13 5 L15 5 L13.5 6.5 L14 9 L12 7.5 L10 9 L10.5 6.5 L9 5 L11 5 Z"
        fill="currentColor"
        stroke="none"
      />
    </svg>
  );
}
