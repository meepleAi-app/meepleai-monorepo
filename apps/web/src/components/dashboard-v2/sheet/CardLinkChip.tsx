'use client';

import type { SheetContext } from '../DashboardEngine';

// ---------------------------------------------------------------------------
// CardLinkChip — placeholder until Task 6 (SessionSheet) provides full impl
// ---------------------------------------------------------------------------

interface CardLinkChipProps {
  label: string;
  target: SheetContext;
  onClick: (target: SheetContext) => void;
}

export function CardLinkChip({ label, target, onClick }: CardLinkChipProps) {
  return (
    <button
      type="button"
      onClick={() => onClick(target)}
      className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground shadow-sm transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      {label}
    </button>
  );
}
