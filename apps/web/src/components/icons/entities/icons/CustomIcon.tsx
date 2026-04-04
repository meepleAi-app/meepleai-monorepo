interface IconProps {
  size?: number;
  className?: string;
}

export function CustomIcon({ size = 24, className = '' }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="currentColor"
      className={className}
      role="img"
      aria-label="Custom"
    >
      <path d="M12 1L9 9l-8 3 8 3 3 8 3-8 8-3-8-3z" />
    </svg>
  );
}
