/**
 * SessionsFilters — Wave D.1 v2 component (Issue #735).
 *
 * Mapped from `admin-mockups/design_files/sp4-sessions-index.jsx` (SessionFilters).
 *
 * Pure component: all i18n strings injected via `labels` — no `useTranslation`.
 *
 * Layout (mobile-first):
 *   - Search input: full-width, search icon, aria-label
 *   - Status pill bar: 4 chips (All/Active/Completed/Abandoned) with WAI-ARIA
 *     tablist role, roving tabindex, ArrowLeft/Right keyboard nav (via
 *     useTablistKeyboardNav hook from Wave A.6 PR #623).
 *   - View toggle: List / Grid 2-button group, aria-pressed states
 *
 * WCAG:
 *   - Active pill: bg-[hsla(240,60%,55%,0.14)] text-[hsl(240,60%,45%)] — l45 = 6.8:1 vs white
 *   - Focus rings on all interactive elements
 */

'use client';

import { type ChangeEvent, type ReactElement, useMemo } from 'react';

import clsx from 'clsx';
import { LayoutGrid, List, Search } from 'lucide-react';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import type { SessionStatusFilter, SessionViewMode } from '@/lib/sessions/sessions-filters';

export interface SessionsFiltersLabels {
  readonly statusAll: string;
  readonly statusActive: string;
  readonly statusCompleted: string;
  readonly statusAbandoned: string;
  readonly searchPlaceholder: string;
  readonly viewList: string;
  readonly viewGrid: string;
  readonly statusGroupLabel: string;
  readonly viewToggleLabel: string;
  readonly searchAriaLabel: string;
}

export interface SessionsFiltersProps {
  readonly statusFilter: SessionStatusFilter;
  readonly onStatusChange: (next: SessionStatusFilter) => void;
  readonly view: SessionViewMode;
  readonly onViewChange: (next: SessionViewMode) => void;
  readonly search: string;
  readonly onSearchChange: (next: string) => void;
  readonly counts?: Partial<Record<SessionStatusFilter, number>>;
  readonly labels: SessionsFiltersLabels;
  readonly className?: string;
}

const STATUS_ORDER: ReadonlyArray<SessionStatusFilter> = [
  'all',
  'active',
  'completed',
  'abandoned',
];

export function SessionsFilters({
  statusFilter,
  onStatusChange,
  view,
  onViewChange,
  search,
  onSearchChange,
  counts,
  labels,
  className,
}: SessionsFiltersProps): ReactElement {
  const orderedKeys = useMemo(() => STATUS_ORDER, []);
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<SessionStatusFilter>({
    orderedKeys,
    onChange: onStatusChange,
  });

  const statusLabels: Record<SessionStatusFilter, string> = {
    all: labels.statusAll,
    active: labels.statusActive,
    completed: labels.statusCompleted,
    abandoned: labels.statusAbandoned,
  };

  return (
    <div
      data-slot="sessions-filters"
      className={clsx(
        'sticky top-0 z-10 mx-auto max-w-[1280px] px-4 sm:px-8',
        'flex flex-col gap-3 py-3',
        'border-b border-border/60 bg-background/90 backdrop-blur-sm',
        className
      )}
    >
      {/* Search + view toggle row */}
      <div className="flex items-center gap-3">
        {/* Search input */}
        <div className="relative flex-1">
          <Search
            aria-hidden="true"
            className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
          />
          <input
            type="search"
            value={search}
            onChange={(e: ChangeEvent<HTMLInputElement>) => onSearchChange(e.target.value)}
            placeholder={labels.searchPlaceholder}
            aria-label={labels.searchAriaLabel}
            data-slot="sessions-filters-search"
            className={clsx(
              'w-full rounded-xl border border-input bg-card py-2 pl-9 pr-4 text-sm',
              'text-foreground placeholder:text-muted-foreground',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              'sm:max-w-xs'
            )}
          />
        </div>

        {/* View toggle */}
        <div
          role="group"
          aria-label={labels.viewToggleLabel}
          className="inline-flex overflow-hidden rounded-lg border border-border bg-card"
        >
          <button
            type="button"
            aria-pressed={view === 'list'}
            aria-label={labels.viewList}
            onClick={() => onViewChange('list')}
            className={clsx(
              'flex h-9 w-9 items-center justify-center transition-colors',
              view === 'list'
                ? 'bg-entity-session/14 text-entity-session'
                : 'text-muted-foreground hover:bg-muted',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset'
            )}
            data-slot="sessions-view-list"
          >
            <List className="h-4 w-4" aria-hidden="true" />
          </button>
          <button
            type="button"
            aria-pressed={view === 'grid'}
            aria-label={labels.viewGrid}
            onClick={() => onViewChange('grid')}
            className={clsx(
              'flex h-9 w-9 items-center justify-center transition-colors',
              view === 'grid'
                ? 'bg-entity-session/14 text-entity-session'
                : 'text-muted-foreground hover:bg-muted',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset'
            )}
            data-slot="sessions-view-grid"
          >
            <LayoutGrid className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
      </div>

      {/* Status pill bar */}
      <div
        role="tablist"
        aria-label={labels.statusGroupLabel}
        aria-orientation="horizontal"
        className="flex items-center gap-1.5 overflow-x-auto scrollbar-none"
        id="sessions-status-tablist"
      >
        {STATUS_ORDER.map(status => {
          const isActive = statusFilter === status;
          const count = counts?.[status];
          return (
            <button
              key={status}
              type="button"
              role="tab"
              aria-selected={isActive}
              aria-controls="sessions-content"
              tabIndex={isActive ? 0 : -1}
              ref={node => {
                if (node) tabRefs.current.set(status, node);
                else tabRefs.current.delete(status);
              }}
              onKeyDown={e => handleKeyDown(e, status)}
              onClick={() => onStatusChange(status)}
              data-slot="sessions-filter-chip"
              data-status={status}
              className={clsx(
                'inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-semibold transition-colors whitespace-nowrap',
                isActive
                  ? 'bg-entity-session/14 text-entity-session ring-1 ring-entity-session/30'
                  : 'bg-slate-50 text-slate-700 hover:bg-slate-100',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
            >
              {statusLabels[status]}
              {count !== undefined && (
                <span
                  className={clsx(
                    'rounded-full px-1.5 py-px font-mono text-[9px] font-extrabold',
                    isActive ? 'bg-entity-session text-white' : 'bg-muted text-muted-foreground'
                  )}
                >
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
}
