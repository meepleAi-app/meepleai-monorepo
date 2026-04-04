interface IconProps {
  size?: number;
  className?: string;
}

export function PlayerIcon({ size = 24, className = '' }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="currentColor"
      className={className}
      role="img"
      aria-label="Player"
    >
      <path d="M12 4a4 4 0 110 8 4 4 0 010-8zM6 20c0-3.31 2.69-6 6-6s6 2.69 6 6H6z" />
    </svg>
  );
}
