'use client';

import type { BreadcrumbEntry } from '../DashboardEngine';

interface SheetBreadcrumbProps {
  entries: BreadcrumbEntry[];
  onNavigate: (index: number) => void;
}

/**
 * Breadcrumb trail inside the SessionSheet for card-link navigation.
 */
export function SheetBreadcrumb({ entries, onNavigate }: SheetBreadcrumbProps) {
  if (entries.length <= 1) return null;

  return (
    <nav
      data-testid="sheet-breadcrumb"
      aria-label="Sheet breadcrumb"
      className="flex items-center gap-1 text-xs text-muted-foreground mb-3 flex-wrap"
    >
      {entries.map((entry, i) => (
        <span key={entry.context} className="flex items-center gap-1">
          {i > 0 && <span className="opacity-40">›</span>}
          {i === entries.length - 1 ? (
            <span data-testid={`breadcrumb-entry-${i}`} className="font-medium text-foreground">
              {entry.label}
            </span>
          ) : (
            <button
              type="button"
              data-testid={`breadcrumb-entry-${i}`}
              onClick={() => onNavigate(i)}
              className="hover:text-foreground transition-colors underline-offset-2 hover:underline"
            >
              {entry.label}
            </button>
          )}
        </span>
      ))}
    </nav>
  );
}
