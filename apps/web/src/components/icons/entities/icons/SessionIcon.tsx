interface IconProps {
  size?: number;
  className?: string;
}

export function SessionIcon({ size = 24, className = '' }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="currentColor"
      className={className}
      role="img"
      aria-label="Session"
    >
      <path d="M6 2h12v2l-4 4v4l4 4v2H6v-2l4-4v-4L6 4V2z" />
    </svg>
  );
}
