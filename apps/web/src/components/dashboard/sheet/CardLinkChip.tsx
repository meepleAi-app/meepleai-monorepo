'use client';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type AccentColor = 'cyan' | 'emerald' | 'violet' | 'primary';

interface CardLinkChipProps {
  icon: React.ReactNode;
  label: string;
  description?: string;
  accentColor?: AccentColor;
  onClick: () => void;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const ACCENT_GRADIENTS: Record<AccentColor, string> = {
  cyan: 'from-cyan-500/20 to-cyan-500/5 border-cyan-500/30 hover:from-cyan-500/30',
  emerald: 'from-emerald-500/20 to-emerald-500/5 border-emerald-500/30 hover:from-emerald-500/30',
  violet: 'from-violet-500/20 to-violet-500/5 border-violet-500/30 hover:from-violet-500/30',
  primary: 'from-primary/20 to-primary/5 border-primary/30 hover:from-primary/30',
};

// ---------------------------------------------------------------------------
// CardLinkChip
// ---------------------------------------------------------------------------

/**
 * Clickable pill-shaped chip with gradient background.
 *
 * Layout: [icon] [label + optional description] [→]
 *
 * Color variants: cyan, emerald, violet, primary.
 */
export function CardLinkChip({
  icon,
  label,
  description,
  accentColor = 'primary',
  onClick,
}: CardLinkChipProps) {
  const gradient = ACCENT_GRADIENTS[accentColor];

  return (
    <button
      type="button"
      data-testid="card-link-chip"
      onClick={onClick}
      className={`flex w-full items-center gap-3 rounded-2xl border bg-gradient-to-r px-4 py-3 text-left transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${gradient}`}
    >
      {/* Icon */}
      <span
        data-testid="card-link-chip-icon"
        className="flex-shrink-0 text-lg leading-none"
        aria-hidden="true"
      >
        {icon}
      </span>

      {/* Text */}
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="text-sm font-medium text-foreground">{label}</span>
        {description && (
          <span data-testid="card-link-chip-description" className="text-xs text-muted-foreground">
            {description}
          </span>
        )}
      </span>

      {/* Arrow */}
      <span aria-hidden="true" className="flex-shrink-0 text-muted-foreground">
        →
      </span>
    </button>
  );
}
