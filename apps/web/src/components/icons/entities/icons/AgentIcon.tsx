interface IconProps {
  size?: number;
  className?: string;
}

export function AgentIcon({ size = 24, className = '' }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="currentColor"
      className={className}
      role="img"
      aria-label="Agent"
    >
      <path d="M7 2v11h3v9l7-12h-4l4-8H7z" />
    </svg>
  );
}
