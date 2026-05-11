/**
 * AgentFilters — Wave B.2 v2 component (Issue #634).
 *
 * Mapped from `admin-mockups/design_files/sp4-agents-index.jsx` (AgentFilters).
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-2-agents.md §3.2
 *
 * Pure component (mirror Wave B.1 GamesFiltersInline): all i18n strings
 * injected via `labels`. Internal state limited to the search input mirror
 * (`localQuery`) so the 300ms debounce stays trailing-edge correct while the
 * orchestrator (AgentsLibraryView) remains source of truth for `query`.
 *
 * Differs from GamesFiltersInline:
 *   - NO view toggle (B.2 spec drops grid/list switcher per spec-panel
 *     resolution B-2; agents always render in CSS-grid auto-fit).
 *   - Status keys are agent-specific: `all | attivo | in-setup | archiviato`.
 *   - Sort keys are agent-specific: `recent | alpha | used`.
 *
 * WAI-ARIA contract:
 *   - Search: <input type="search"> with sr-only <label>; clear button is
 *     immediate (does NOT pass through the debounce).
 *   - Status: role="tablist" + 4 role="tab" with aria-selected. Roving
 *     tabindex driven by `useTablistKeyboardNav` (orientation horizontal,
 *     wrap), automatic activation (focus = onChange same tick).
 *   - Sort: native <select> with associated <label>.
 */

'use client';

import { useEffect, useRef, useState, type ChangeEvent, type ReactElement } from 'react';

import clsx from 'clsx';

import { useDebounce } from '@/hooks/useDebounce';
import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import type { AgentsSortKey, AgentsStatusKey } from '@/lib/agents/library-filters';

export type { AgentsSortKey, AgentsStatusKey };

export interface AgentFiltersLabels {
  readonly search: {
    readonly placeholder: string;
    readonly ariaLabel: string;
    readonly clearAriaLabel: string;
  };
  readonly status: {
    readonly label: string;
    readonly options: Readonly<Record<AgentsStatusKey, string>>;
  };
  readonly sort: {
    readonly label: string;
    readonly options: Readonly<Record<AgentsSortKey, string>>;
  };
  readonly resultCount: (count: number) => string;
}

export interface AgentFiltersProps {
  readonly labels: AgentFiltersLabels;
  readonly query: string;
  readonly onQueryChange: (next: string) => void;
  readonly status: AgentsStatusKey;
  readonly onStatusChange: (next: AgentsStatusKey) => void;
  readonly sort: AgentsSortKey;
  readonly onSortChange: (next: AgentsSortKey) => void;
  readonly resultCount: number;
  readonly compact?: boolean;
  readonly className?: string;
}

const STATUS_KEYS: readonly AgentsStatusKey[] = ['all', 'attivo', 'in-setup', 'archiviato'];
const SORT_KEYS: readonly AgentsSortKey[] = ['recent', 'alpha', 'used'];

const SEARCH_DEBOUNCE_MS = 300;

export function AgentFilters({
  labels,
  query,
  onQueryChange,
  status,
  onStatusChange,
  sort,
  onSortChange,
  resultCount,
  compact: _compact = false,
  className,
}: AgentFiltersProps): ReactElement {
  const [localQuery, setLocalQuery] = useState(query);
  const debouncedQuery = useDebounce(localQuery, SEARCH_DEBOUNCE_MS);
  const lastFiredRef = useRef(query);

  useEffect(() => {
    if (query !== lastFiredRef.current) {
      lastFiredRef.current = query;
      setLocalQuery(query);
    }
  }, [query]);

  useEffect(() => {
    if (debouncedQuery !== lastFiredRef.current) {
      lastFiredRef.current = debouncedQuery;
      onQueryChange(debouncedQuery);
    }
  }, [debouncedQuery, onQueryChange]);

  const handleClearSearch = () => {
    setLocalQuery('');
    lastFiredRef.current = '';
    onQueryChange('');
  };

  const { tabRefs: statusTabRefs, handleKeyDown: handleStatusKeyDown } =
    useTablistKeyboardNav<AgentsStatusKey>({
      orderedKeys: STATUS_KEYS,
      onChange: onStatusChange,
      orientation: 'horizontal',
    });

  return (
    <section
      data-slot="agents-filters"
      className={clsx(
        'flex flex-col gap-4 rounded-2xl border border-border bg-card/80 p-4 shadow-sm sm:p-5',
        'sticky top-16 z-10 backdrop-blur supports-[backdrop-filter]:bg-card/70',
        className
      )}
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <label className="relative flex w-full max-w-xl items-center">
          <span className="sr-only">{labels.search.ariaLabel}</span>
          <input
            type="search"
            value={localQuery}
            onChange={(event: ChangeEvent<HTMLInputElement>) => setLocalQuery(event.target.value)}
            placeholder={labels.search.placeholder}
            className={clsx(
              'h-10 w-full rounded-full border border-border bg-background px-4 pr-10 text-sm',
              'placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
            data-slot="agents-filters-search-input"
          />
          {localQuery.length > 0 ? (
            <button
              type="button"
              aria-label={labels.search.clearAriaLabel}
              onClick={handleClearSearch}
              className={clsx(
                'absolute right-2 top-1/2 inline-flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded-full',
                'text-muted-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
              data-slot="agents-filters-search-clear"
            >
              ×
            </button>
          ) : null}
        </label>

        <span
          className="text-xs font-medium text-muted-foreground sm:text-sm"
          data-slot="agents-filters-result-count"
        >
          {labels.resultCount(resultCount)}
        </span>
      </div>

      <div
        className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center sm:gap-4"
        data-slot="agents-filters-controls"
      >
        <div
          role="tablist"
          aria-label={labels.status.label}
          className="inline-flex items-center gap-1 rounded-full border border-border bg-muted/40 p-1"
          data-slot="agents-filters-status-tablist"
        >
          {STATUS_KEYS.map(key => {
            const selected = key === status;
            return (
              <button
                key={key}
                ref={node => {
                  if (node) statusTabRefs.current.set(key, node);
                  else statusTabRefs.current.delete(key);
                }}
                type="button"
                role="tab"
                id={`agents-filters-status-${key}`}
                aria-selected={selected}
                tabIndex={selected ? 0 : -1}
                onClick={() => onStatusChange(key)}
                onKeyDown={event => handleStatusKeyDown(event, key)}
                className={clsx(
                  'inline-flex h-8 items-center rounded-full px-3 text-xs font-medium transition-colors sm:text-sm',
                  selected
                    ? 'bg-primary text-primary-foreground shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'
                )}
                data-slot="agents-filters-status-tab"
                data-status={key}
              >
                {labels.status.options[key]}
              </button>
            );
          })}
        </div>

        <label
          className="flex items-center gap-2 text-xs font-medium text-muted-foreground sm:text-sm"
          data-slot="agents-filters-sort"
        >
          <span>{labels.sort.label}</span>
          <select
            value={sort}
            onChange={(event: ChangeEvent<HTMLSelectElement>) =>
              onSortChange(event.target.value as AgentsSortKey)
            }
            className={clsx(
              'h-8 rounded-full border border-border bg-background px-3 text-xs sm:text-sm',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
          >
            {SORT_KEYS.map(key => (
              <option key={key} value={key}>
                {labels.sort.options[key]}
              </option>
            ))}
          </select>
        </label>
      </div>
    </section>
  );
}
