/**
 * SplitViewLayout — Tablet Split-View Component
 * Issue #16 from mobile-first-ux-epic.md
 *
 * Provides a responsive list + detail side-by-side layout:
 *   - Mobile (<lg): stacked — list or detail shown via selection state
 *   - Tablet landscape (lg+): side-by-side with resizable panels
 *
 * Usage:
 * ```tsx
 * <SplitViewLayout
 *   list={<GamesList onSelect={setId} />}
 *   detail={selectedId ? <GameDetail id={selectedId} /> : undefined}
 *   onBack={() => setSelectedId(null)}
 *   emptyDetail={<p>Select a game</p>}
 * />
 * ```
 */

'use client';

import { ArrowLeft } from 'lucide-react';

import { cn } from '@/lib/utils';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface SplitViewLayoutProps {
  /** The list panel content */
  list: React.ReactNode;
  /** The detail panel content (null/undefined = nothing selected) */
  detail?: React.ReactNode;
  /** Callback to clear selection and show list on mobile */
  onBack?: () => void;
  /** Placeholder shown when no detail is selected (desktop only) */
  emptyDetail?: React.ReactNode;
  /** List panel width ratio (default: 2/5 on desktop) */
  listRatio?: 'narrow' | 'balanced' | 'wide';
  /** Additional className for the root container */
  className?: string;
  /** Whether to show back button on mobile detail view */
  showBackButton?: boolean;
  /** Accessible label for the list panel */
  listLabel?: string;
  /** Accessible label for the detail panel */
  detailLabel?: string;
}

// ─── SplitViewLayout ─────────────────────────────────────────────────────────

export function SplitViewLayout({
  list,
  detail,
  onBack,
  emptyDetail,
  listRatio = 'balanced',
  className,
  showBackButton = true,
  listLabel = 'List',
  detailLabel = 'Detail',
}: SplitViewLayoutProps) {
  const hasDetail = detail !== undefined && detail !== null;

  const listWidthClass = {
    narrow: 'lg:w-1/3',
    balanced: 'lg:w-2/5',
    wide: 'lg:w-1/2',
  }[listRatio];

  const detailWidthClass = {
    narrow: 'lg:w-2/3',
    balanced: 'lg:w-3/5',
    wide: 'lg:w-1/2',
  }[listRatio];

  return (
    <div
      className={cn('flex flex-col lg:flex-row lg:gap-0 min-h-0 flex-1', className)}
      data-testid="split-view-layout"
    >
      {/* List Panel */}
      <div
        className={cn(
          // Mobile: full width, hidden when detail is selected
          hasDetail ? 'hidden lg:flex' : 'flex',
          // Desktop: fixed width with scroll
          'flex-col',
          listWidthClass,
          'lg:border-r lg:border-border/60',
          'overflow-y-auto'
        )}
        role="region"
        aria-label={listLabel}
        data-testid="split-view-list"
      >
        {list}
      </div>

      {/* Detail Panel */}
      <div
        className={cn(
          // Mobile: full width, only shown when detail is selected
          hasDetail ? 'flex' : 'hidden lg:flex',
          // Desktop: fill remaining width
          'flex-col flex-1',
          detailWidthClass,
          'overflow-y-auto'
        )}
        role="region"
        aria-label={detailLabel}
        data-testid="split-view-detail"
      >
        {/* Mobile back button */}
        {hasDetail && showBackButton && (
          <button
            type="button"
            onClick={onBack}
            className={cn(
              'lg:hidden',
              'flex items-center gap-2 px-4 py-3',
              'text-sm font-medium text-foreground/70',
              'hover:text-foreground',
              'border-b border-border/40',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
            aria-label="Back to list"
            data-testid="split-view-back"
          >
            <ArrowLeft className="h-4 w-4" />
            <span>Indietro</span>
          </button>
        )}

        {hasDetail
          ? detail
          : emptyDetail && (
              <div
                className="flex-1 flex items-center justify-center text-muted-foreground"
                data-testid="split-view-empty"
              >
                {emptyDetail}
              </div>
            )}
      </div>
    </div>
  );
}
