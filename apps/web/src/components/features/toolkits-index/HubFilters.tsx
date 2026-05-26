/**
 * HubFilters — sticky filter bar for `/toolkits` hub.
 *
 * Wave 4 (#1480). Maps the mockup HubFilters (sp4-hub-toolkits.jsx:298-384).
 * Search input + 4 status tabs (all/featured/new/top) + sort dropdown
 * (popular/rating/title/uses) + result count. Pure presentational, state and
 * labels injected from the orchestrator (no internal debounce; orchestrator
 * owns that).
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export type HubFiltersStatus = 'all' | 'featured' | 'new' | 'top';
export type HubFiltersSort = 'popular' | 'rating' | 'title' | 'uses';

const STATUS_ORDER: readonly HubFiltersStatus[] = ['all', 'featured', 'new', 'top'] as const;
const SORT_ORDER: readonly HubFiltersSort[] = ['popular', 'rating', 'title', 'uses'] as const;

export interface HubFiltersLabels {
  readonly searchPlaceholder: string;
  readonly searchClearAriaLabel: string;
  readonly statusTablistAriaLabel: string;
  readonly statusOptions: Readonly<Record<HubFiltersStatus, string>>;
  readonly sortLabel: string;
  readonly sortOptions: Readonly<Record<HubFiltersSort, string>>;
  /** Template with `{count}` placeholder, e.g. `"{count} toolkit"`. */
  readonly countTemplate: string;
}

export interface HubFiltersProps {
  readonly query: string;
  readonly onQueryChange: (query: string) => void;
  readonly status: HubFiltersStatus;
  readonly onStatusChange: (status: HubFiltersStatus) => void;
  readonly sort: HubFiltersSort;
  readonly onSortChange: (sort: HubFiltersSort) => void;
  readonly count: number;
  readonly labels: HubFiltersLabels;
  readonly compact?: boolean;
  readonly className?: string;
}

function interpolateCount(template: string, count: number): string {
  return template.replace('{count}', String(count));
}

export function HubFilters({
  query,
  onQueryChange,
  status,
  onStatusChange,
  sort,
  onSortChange,
  count,
  labels,
  compact = false,
  className,
}: HubFiltersProps): JSX.Element {
  return (
    <div
      data-slot="toolkits-index-filters"
      className={clsx(
        'sticky top-0 z-10 flex flex-col items-stretch gap-2.5 border-b border-border bg-background/80 px-4 py-3 backdrop-blur-md',
        'sm:flex-row sm:items-center sm:gap-3 sm:px-8 sm:py-3.5',
        className
      )}
    >
      {/* Search input */}
      <label className="flex flex-1 items-center gap-2 rounded-md border border-border bg-card px-3 py-2 sm:max-w-sm">
        <span aria-hidden="true" className="text-muted-foreground">
          🔍
        </span>
        <input
          type="text"
          value={query}
          onChange={e => onQueryChange(e.target.value)}
          placeholder={labels.searchPlaceholder}
          className="min-w-0 flex-1 border-0 bg-transparent text-sm text-foreground outline-none placeholder:text-muted-foreground"
        />
        {query.length > 0 && (
          <button
            type="button"
            onClick={() => onQueryChange('')}
            aria-label={labels.searchClearAriaLabel}
            className="border-0 bg-transparent p-0 text-sm text-muted-foreground"
          >
            ✕
          </button>
        )}
      </label>

      {/* Status tabs */}
      <div
        role="tablist"
        aria-label={labels.statusTablistAriaLabel}
        className="inline-flex shrink-0 rounded-md bg-muted p-0.5"
      >
        {STATUS_ORDER.map(opt => {
          const active = status === opt;
          return (
            <button
              key={opt}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => onStatusChange(opt)}
              className={clsx(
                'rounded font-bold font-[Quicksand] whitespace-nowrap transition-all',
                compact ? 'px-2 py-1.5 text-[11px]' : 'px-3 py-1.5 text-xs',
                active
                  ? 'bg-card text-entity-toolkit shadow-sm'
                  : 'bg-transparent text-muted-foreground'
              )}
            >
              {labels.statusOptions[opt]}
            </button>
          );
        })}
      </div>

      {/* Spacer */}
      <div className="hidden sm:block sm:flex-1" />

      {/* Sort dropdown */}
      <label className="inline-flex items-center gap-1.5 font-mono text-[11px] font-semibold text-muted-foreground">
        <span className="uppercase tracking-wider">{labels.sortLabel}</span>
        <select
          aria-label={labels.sortLabel}
          value={sort}
          onChange={e => onSortChange(e.target.value as HubFiltersSort)}
          className="rounded border border-border bg-card px-2.5 py-1.5 font-[Quicksand] text-xs font-bold text-foreground"
        >
          {SORT_ORDER.map(opt => (
            <option key={opt} value={opt}>
              {labels.sortOptions[opt]}
            </option>
          ))}
        </select>
      </label>

      {/* Count label */}
      <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
        {interpolateCount(labels.countTemplate, count)}
      </span>
    </div>
  );
}
