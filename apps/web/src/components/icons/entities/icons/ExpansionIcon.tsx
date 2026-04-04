interface IconProps {
  size?: number;
  className?: string;
}

export function ExpansionIcon({ size = 24, className = '' }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="currentColor"
      className={className}
      role="img"
      aria-label="Expansion"
    >
      <path d="M20.5 11H19V7c0-1.1-.9-2-2-2h-4V3.5a2.5 2.5 0 00-5 0V5H4c-1.1 0-2 .9-2 2v3.8h1.5a2.5 2.5 0 010 5H2V19c0 1.1.9 2 2 2h3.8v-1.5a2.5 2.5 0 015 0V21H16c1.1 0 2-.9 2-2v-4h1.5a2.5 2.5 0 000-5z" />
    </svg>
  );
}
