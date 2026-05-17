/**
 * HubCatalogView — shared shell for `/hub/<entity>` Stage 3 routes (#1166).
 *
 * Generic catalog view parameterised per entity. Renders:
 *   - Hero (entity-tinted gradient + title + subtitle + KPI bar)
 *   - Search input (300ms debounce → URL ?q=)
 *   - Filter pill bar (4 segments: all / featured / new / top, URL ?filter=)
 *   - Card grid (caller provides `renderCard` for entity-specific layout)
 *   - 4 FSM shells: loading / error / empty / filtered-empty
 *   - Optional bottom slot (StickyAccessCta for /hub/games)
 *
 * Pattern reference: discover FE (#1160) — same approach with shared atoms.
 */

'use client';

import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';

import { Search } from 'lucide-react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { cn } from '@/lib/utils';

export type HubEntity = 'game' | 'agent' | 'toolkit';
export type HubFilter = 'all' | 'featured' | 'new' | 'top';

export const HUB_FILTERS: ReadonlyArray<HubFilter> = ['all', 'featured', 'new', 'top'];

export interface HubKpi {
  readonly label: string;
  readonly value: string | number;
}

export interface HubCatalogViewLabels {
  /** Pill above title (e.g. "Hub · /hub/games · Pubblico"). */
  readonly badge: string;
  readonly title: string;
  readonly subtitle: string;
  readonly searchPlaceholder: string;
  readonly filterAriaLabel: string;
  readonly filterLabels: Readonly<Record<HubFilter, string>>;
  readonly emptyTitle: string;
  readonly emptyBody: string;
  readonly filteredEmptyTitle: string;
  readonly filteredEmptyBody: string;
  readonly errorTitle: string;
  readonly errorBody: string;
  readonly retryLabel: string;
  readonly resultsCountTemplate: string;
}

export interface HubCatalogViewProps<TItem> {
  readonly entity: HubEntity;
  readonly labels: HubCatalogViewLabels;
  /** KPI bar entries (2-4 recommended). */
  readonly kpi: ReadonlyArray<HubKpi>;
  /** Items already filtered/sorted by caller, OR raw items to be filtered client-side. */
  readonly items: ReadonlyArray<TItem>;
  /** Per-item filter predicate. Receives (item, filter, query). */
  readonly itemMatches: (item: TItem, filter: HubFilter, query: string) => boolean;
  /** Card renderer (entity-specific layout). */
  readonly renderCard: (item: TItem) => ReactNode;
  /** Stable key extractor. */
  readonly getItemKey: (item: TItem) => string;
  readonly isLoading?: boolean;
  readonly isError?: boolean;
  readonly onRetry?: () => void;
  /** Called when user commits a new search query. */
  readonly onSearchCommitted?: (q: string) => void;
  /** Called when user changes filter. */
  readonly onFilterChanged?: (from: HubFilter, to: HubFilter) => void;
  /** Optional bottom slot — used by /hub/games for StickyAccessCta. */
  readonly bottomSlot?: ReactNode;
}

const SEARCH_DEBOUNCE_MS = 300;

const ENTITY_GRADIENT: Record<HubEntity, string> = {
  game: 'linear-gradient(135deg, hsl(var(--c-game) / 0.10) 0%, hsl(var(--c-game) / 0.04) 100%)',
  agent: 'linear-gradient(135deg, hsl(var(--c-agent) / 0.10) 0%, hsl(var(--c-agent) / 0.04) 100%)',
  toolkit:
    'linear-gradient(135deg, hsl(var(--c-toolkit) / 0.10) 0%, hsl(var(--c-toolkit) / 0.04) 100%)',
};

function parseFilter(raw: string | null): HubFilter {
  if (raw && (HUB_FILTERS as ReadonlyArray<string>).includes(raw)) return raw as HubFilter;
  return 'all';
}

export function HubCatalogView<TItem>({
  entity,
  labels,
  kpi,
  items,
  itemMatches,
  renderCard,
  getItemKey,
  isLoading = false,
  isError = false,
  onRetry,
  onSearchCommitted,
  onFilterChanged,
  bottomSlot,
}: HubCatalogViewProps<TItem>) {
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  // ── URL state SSOT ────────────────────────────────────────────────────────
  const initialQ = searchParams.get('q') ?? '';
  const initialFilter = parseFilter(searchParams.get('filter'));

  const [q, setQ] = useState(initialQ);
  const [filter, setFilter] = useState<HubFilter>(initialFilter);
  const idleTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    setQ(searchParams.get('q') ?? '');
    setFilter(parseFilter(searchParams.get('filter')));
  }, [searchParams]);

  useEffect(
    () => () => {
      if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
    },
    []
  );

  // ── URL update helper ────────────────────────────────────────────────────
  const updateUrl = useCallback(
    (next: { q?: string; filter?: HubFilter }) => {
      const params = new URLSearchParams(searchParams.toString());
      const nextQ = next.q !== undefined ? next.q : q;
      const nextFilter = next.filter !== undefined ? next.filter : filter;
      if (nextQ) params.set('q', nextQ);
      else params.delete('q');
      if (nextFilter && nextFilter !== 'all') params.set('filter', nextFilter);
      else params.delete('filter');
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
    },
    [searchParams, q, filter, pathname, router]
  );

  // ── Search handlers (debounced) ──────────────────────────────────────────
  const handleSearchChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const next = event.target.value;
      setQ(next);
      if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
      idleTimerRef.current = setTimeout(() => {
        updateUrl({ q: next });
        onSearchCommitted?.(next);
      }, SEARCH_DEBOUNCE_MS);
    },
    [updateUrl, onSearchCommitted]
  );

  const handleSearchKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLInputElement>) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
        updateUrl({ q });
        onSearchCommitted?.(q);
      }
    },
    [q, updateUrl, onSearchCommitted]
  );

  const handleFilterClick = useCallback(
    (next: HubFilter) => {
      const previous = filter;
      setFilter(next);
      updateUrl({ filter: next });
      onFilterChanged?.(previous, next);
    },
    [filter, updateUrl, onFilterChanged]
  );

  // ── Client-side filtering ────────────────────────────────────────────────
  const filtered = useMemo(
    () => items.filter(item => itemMatches(item, filter, q)),
    [items, itemMatches, filter, q]
  );

  const hasQuery = q.length > 0;
  const hasFilter = filter !== 'all';
  const isFilteredCriteriaApplied = hasQuery || hasFilter;

  return (
    <main
      data-slot="hub-catalog-view"
      data-entity={entity}
      className="mx-auto flex w-full max-w-6xl flex-col gap-5 px-4 py-4 sm:px-6 sm:py-6"
    >
      {/* Hero */}
      <header
        data-slot="hub-catalog-hero"
        className="relative overflow-hidden rounded-2xl px-5 py-6 sm:px-7 sm:py-8"
        style={{
          background: ENTITY_GRADIENT[entity],
          border: '1px solid var(--border-light, rgba(180, 130, 80, 0.1))',
        }}
      >
        <div className="flex flex-col gap-3">
          <span className="inline-flex w-fit items-center gap-1.5 rounded-full bg-muted/60 px-2.5 py-1 font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
            {labels.badge}
          </span>
          <h1
            className="font-bold font-[Quicksand] text-2xl sm:text-3xl tracking-tight text-foreground"
            style={{ lineHeight: 1.1 }}
          >
            {labels.title}
          </h1>
          <p className="max-w-2xl text-sm text-muted-foreground" style={{ lineHeight: 1.55 }}>
            {labels.subtitle}
          </p>

          {/* KPI bar */}
          {kpi.length > 0 && (
            <dl className="mt-2 flex flex-wrap gap-4 sm:gap-6">
              {kpi.map(k => (
                <div key={k.label} className="flex flex-col">
                  <dt className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
                    {k.label}
                  </dt>
                  <dd className="font-bold font-[Quicksand] text-lg text-foreground tabular-nums">
                    {k.value}
                  </dd>
                </div>
              ))}
            </dl>
          )}

          {/* Search */}
          <div className="relative mt-2 max-w-md">
            <Search
              size={16}
              className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"
              aria-hidden="true"
            />
            <input
              type="search"
              data-slot="hub-search-input"
              value={q}
              onChange={handleSearchChange}
              onKeyDown={handleSearchKeyDown}
              placeholder={labels.searchPlaceholder}
              aria-label={labels.searchPlaceholder}
              className="h-10 w-full rounded-2xl border border-border bg-card pl-10 pr-4 text-sm outline-none transition-shadow focus-visible:ring-2 focus-visible:ring-foreground/20 placeholder:text-muted-foreground"
            />
          </div>

          {/* Filter pill bar */}
          <div
            role="toolbar"
            aria-label={labels.filterAriaLabel}
            data-slot="hub-filter-pillbar"
            className="flex flex-wrap gap-2"
          >
            {HUB_FILTERS.map(f => {
              const isActive = f === filter;
              return (
                <button
                  key={f}
                  type="button"
                  data-filter={f}
                  aria-pressed={isActive}
                  onClick={() => handleFilterClick(f)}
                  className={cn(
                    'inline-flex shrink-0 items-center rounded-2xl border px-3 py-1 text-xs font-bold font-[Quicksand] transition-colors',
                    isActive
                      ? 'border-transparent bg-foreground text-background'
                      : 'border-border bg-card text-muted-foreground hover:border-border-strong hover:text-foreground'
                  )}
                >
                  {labels.filterLabels[f]}
                </button>
              );
            })}
          </div>
        </div>
      </header>

      {/* Body — FSM */}
      {isError && (
        <div
          data-slot="hub-error"
          role="alert"
          className="flex flex-col items-center gap-3 rounded-xl border border-destructive/30 bg-destructive/10 py-10 text-center"
        >
          <span className="text-3xl" aria-hidden="true">
            ⚠️
          </span>
          <h2 className="font-bold font-[Quicksand] text-base text-foreground">
            {labels.errorTitle}
          </h2>
          <p className="max-w-md text-sm text-muted-foreground">{labels.errorBody}</p>
          {onRetry && (
            <button
              type="button"
              onClick={onRetry}
              className="mt-1 inline-flex items-center rounded-lg bg-foreground px-4 py-2 font-bold font-[Quicksand] text-xs text-background"
            >
              {labels.retryLabel}
            </button>
          )}
        </div>
      )}

      {!isError && isLoading && (
        <div
          data-slot="hub-loading"
          role="status"
          aria-label="Loading"
          className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4"
        >
          {Array.from({ length: 8 }).map((_, i) => (
            <div
              key={i}
              data-slot="hub-card-skeleton"
              className="h-[220px] animate-pulse rounded-xl border border-border bg-muted/40"
            />
          ))}
        </div>
      )}

      {!isError && !isLoading && items.length === 0 && (
        <div
          data-slot="hub-empty"
          className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-border bg-muted/30 py-12 text-center"
        >
          <span className="text-4xl" aria-hidden="true">
            📭
          </span>
          <h2 className="font-bold font-[Quicksand] text-base text-foreground">
            {labels.emptyTitle}
          </h2>
          <p className="max-w-md text-sm text-muted-foreground">{labels.emptyBody}</p>
        </div>
      )}

      {!isError && !isLoading && items.length > 0 && filtered.length === 0 && (
        <div
          data-slot="hub-filtered-empty"
          className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-border bg-muted/30 py-12 text-center"
        >
          <span className="text-4xl" aria-hidden="true">
            🔍
          </span>
          <h2 className="font-bold font-[Quicksand] text-base text-foreground">
            {labels.filteredEmptyTitle}
          </h2>
          <p className="max-w-md text-sm text-muted-foreground">{labels.filteredEmptyBody}</p>
        </div>
      )}

      {!isError && !isLoading && filtered.length > 0 && (
        <>
          <div className="flex items-center justify-between">
            <p className="font-mono text-xs text-muted-foreground">
              {labels.resultsCountTemplate
                .replace('{filtered}', String(filtered.length))
                .replace('{total}', String(items.length))}
            </p>
          </div>
          <div
            data-slot="hub-card-grid"
            data-filter-active={isFilteredCriteriaApplied || undefined}
            className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4"
          >
            {filtered.map(item => (
              <div key={getItemKey(item)} data-slot="hub-card-cell">
                {renderCard(item)}
              </div>
            ))}
          </div>
        </>
      )}

      {bottomSlot}
    </main>
  );
}
