interface IconProps {
  size?: number;
  className?: string;
}

export function EventIcon({ size = 24, className = '' }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="currentColor"
      className={className}
      role="img"
      aria-label="Event"
    >
      <path d="M19 3h-1V1h-2v2H8V1H6v2H5a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2V5a2 2 0 00-2-2zm0 16H5V8h14v11zm-7-5l2.12 1.26-.56-2.42L15.5 11h-2.49L12 8.8 10.99 11H8.5l1.94 1.84-.56 2.42L12 14z" />
    </svg>
  );
}
