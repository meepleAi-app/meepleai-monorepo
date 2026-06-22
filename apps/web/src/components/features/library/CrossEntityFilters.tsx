/**
 * CrossEntityFilters — Phase 2a (#1605) + Phase 3b (#1593) + Task 1.4 SP4 re-skin (#1585-followup).
 *
 * Two-row composition (mockup `admin-mockups/design_files/sp4-library-desktop.jsx:218-310`):
 *   Row 1: glass-blur search input + `/` keyboard hint badge (global focus shortcut).
 *   Row 2: scrollable filter-chip strip (STATO/GIOCO/DATA/SORT visual stubs) →
 *          existing STATO functional buttons (games tab only) → divider →
 *          "Filtri avanzati" CTA → spacer → view-toggle radiogroup.
 *
 * The component now hosts the search + view-toggle that previously lived in
 * `LibraryHub`'s `<div data-slot="library-toolbar">` block. The root carries
 * the same `data-slot="library-toolbar"` so existing test selectors and visual
 * regressions remain stable.
 *
 * `/` keyboard shortcut focuses the search input from anywhere on the page,
 * but ignores keystrokes while the user is already typing in an editable
 * element (input/textarea/select/contenteditable).
 *
 * In the `games` tab the STATO chip group is FUNCTIONAL (Owned / Wishlist /
 * InPrestito + with-KB toggle). The 4 mockup chips (STATO/GIOCO/DATA/SORT)
 * are visual stubs — SORT shows the current sort label as a follow-up hook
 * for a future popover.
 *
 * Controlled component: the parent (`LibraryHub`) owns `gameStateFilter`,
 * `search`, `view`, `sortKey`, and the drawer state.
 */

'use client';

import { useEffect, useRef, type ReactElement } from 'react';

import clsx from 'clsx';

import { useTranslation } from '@/hooks/useTranslation';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import type { HybridHubTab } from '@/lib/library/hybrid-hub.derive';
import type { LibrarySortKey } from '@/lib/library/library-filters';

import type { LibraryViewMode } from './LibraryHybridGrid';

export interface GameStateFilter {
  readonly states: ReadonlyArray<GameStateType>;
  readonly withKb: boolean;
}

export interface CrossEntityFiltersProps {
  readonly tab: HybridHubTab;
  readonly gameStateFilter: GameStateFilter;
  readonly onGameStateFilterChange: (next: GameStateFilter) => void;
  /** Phase 3b: opens the AdvancedFiltersDrawer. When undefined, the chip is hidden. */
  readonly onMoreFilters?: () => void;
  /** Phase 3b: number of active drawer filters to badge on the chip (0 = no badge). */
  readonly activeFiltersCount?: number;
  /** Task 1.4: search input (was in LibraryHub toolbar). */
  readonly search: string;
  readonly onSearchChange: (next: string) => void;
  /** Task 1.4: view toggle (was in LibraryHub toolbar). */
  readonly view: LibraryViewMode;
  readonly onViewChange: (next: LibraryViewMode) => void;
  /** Task 1.4: sort key — used to populate the SORT chip label. Mockup chip is non-interactive for now. */
  readonly sortKey?: LibrarySortKey;
  /** Compact mode: reduce vertical padding (used in mobile/dense layouts). */
  readonly compact?: boolean;
  readonly className?: string;
}

const STATE_CHIPS: ReadonlyArray<{ value: GameStateType; i18nKey: string }> = [
  { value: 'Owned', i18nKey: 'pages.library.filters.stato.owned' },
  { value: 'Wishlist', i18nKey: 'pages.library.filters.stato.wishlist' },
  { value: 'InPrestito', i18nKey: 'pages.library.filters.stato.loaned' },
];

const VIEW_OPTIONS: ReadonlyArray<{ id: LibraryViewMode; icon: string; labelKey: string }> = [
  { id: 'grid', icon: '▦', labelKey: 'pages.library.view.grid' },
  { id: 'list', icon: '☰', labelKey: 'pages.library.view.list' },
  { id: 'compact', icon: '≡', labelKey: 'pages.library.view.compact' },
];

const SORT_LABEL_KEY: Record<LibrarySortKey, string> = {
  recent: 'pages.library.sort.recent',
  title: 'pages.library.sort.title',
  rating: 'pages.library.sort.rating',
  state: 'pages.library.sort.state',
};

interface FilterChipProps {
  readonly label: string;
  readonly value: string;
  readonly hasValue?: boolean;
  readonly onClick?: () => void;
}

function FilterChip({ label, value, hasValue = false, onClick }: FilterChipProps): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className={clsx(
        'inline-flex flex-shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-md px-3 py-1.5 font-display text-[12px] font-bold',
        hasValue
          ? 'border border-entity-game/30 bg-entity-game/10 text-entity-game'
          : 'border border-border bg-card text-muted-foreground'
      )}
    >
      <span
        className={clsx(
          'font-mono text-[9px] font-extrabold uppercase tracking-[0.06em]',
          hasValue ? 'text-entity-game' : 'text-muted-foreground'
        )}
      >
        {label}
      </span>
      <span>{value}</span>
      <span aria-hidden="true" className="text-[10px] opacity-60">
        ▾
      </span>
    </button>
  );
}

export function CrossEntityFilters({
  tab,
  gameStateFilter,
  onGameStateFilterChange,
  onMoreFilters,
  activeFiltersCount = 0,
  search,
  onSearchChange,
  view,
  onViewChange,
  sortKey = 'recent',
  compact = false,
  className,
}: CrossEntityFiltersProps): ReactElement {
  const { t } = useTranslation();
  const searchRef = useRef<HTMLInputElement>(null);

  // Global "/" shortcut — focus the search input unless the user is already
  // typing somewhere editable. Skips browser-reserved combos (Ctrl+/, Cmd+/,
  // Alt+/) and uses `instanceof HTMLElement` for safer type narrowing than
  // the previous `as HTMLElement | null` cast.
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key !== '/') return;
      // Skip when any modifier is held — Ctrl+/ / Cmd+/ / Alt+/ are
      // browser/devtools shortcuts the page should not hijack.
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      // Skip when the event originates from an editable element.
      if (e.target instanceof HTMLElement) {
        const tag = e.target.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
        if (e.target.isContentEditable) return;
      }
      e.preventDefault();
      searchRef.current?.focus();
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, []);

  const showStato = tab === 'games';
  // R4: hide the drawer chip on the 'all' tab (no single entity scope).
  const showMoreFilters = onMoreFilters !== undefined && tab !== 'all';
  const moreFiltersActive = activeFiltersCount > 0;

  return (
    <div
      data-slot="library-toolbar"
      data-testid="cross-entity-filters-stato"
      className={clsx(
        'flex flex-col gap-2 border-b border-border/60 bg-background/80 backdrop-blur-md',
        compact ? 'px-4 py-2.5' : 'px-8 py-3',
        className
      )}
    >
      {/* Row 1 — Search input + / hint */}
      <div className="relative">
        <span
          aria-hidden="true"
          className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-sm text-muted-foreground"
        >
          ⌕
        </span>
        <input
          ref={searchRef}
          type="search"
          value={search}
          onChange={e => onSearchChange(e.target.value)}
          placeholder={t('pages.library.filters.search.placeholder')}
          aria-label={t('pages.library.filters.search.ariaLabel')}
          data-slot="library-search-input"
          className="w-full rounded-md border border-border bg-card py-2.5 pl-8 pr-10 font-body text-[13px] text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
        <kbd
          aria-label={t('pages.library.filters.search.keyboardHintAriaLabel')}
          className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 rounded border border-border bg-muted px-1.5 py-px font-mono text-[10px] font-bold text-muted-foreground"
        >
          /
        </kbd>
      </div>

      {/* Row 2 — Filter chips + advanced CTA + view toggle */}
      <div className="flex items-center gap-1.5 overflow-x-auto" style={{ scrollbarWidth: 'none' }}>
        {/* Mockup-conformant filter chips. STATO/DATA are non-interactive
            placeholders; SORT reflects current sortKey label as a hook for a
            future popover.
            Issue #2186: the GIOCO chip was removed — it was a visual stub
            duplicating the LibraryTabs entity scope (Tutti / Giochi / Agenti /
            KB / Sessioni / Chat) and contributed to the triple-filter overlap
            reported in the US-10 audit. Sort/Ordina now lives as its own
            visual chip surface here, matching the locked decision A. */}
        <FilterChip
          label={t('pages.library.filters.chips.stato')}
          value={t('pages.library.filters.chips.value.all')}
        />
        <FilterChip
          label={t('pages.library.filters.chips.data')}
          value={t('pages.library.filters.chips.value.always')}
        />
        <FilterChip
          label={t('pages.library.filters.chips.sort')}
          value={t(SORT_LABEL_KEY[sortKey])}
          hasValue
        />

        {/* Functional STATO chip group — preserves Phase 2a behaviour for the games tab. */}
        {showStato ? (
          <div className="flex flex-shrink-0 items-center gap-2 pl-2">
            {STATE_CHIPS.map(chip => {
              const active = gameStateFilter.states.includes(chip.value);
              return (
                <button
                  key={chip.value}
                  type="button"
                  aria-pressed={active}
                  onClick={() => {
                    const has = gameStateFilter.states.includes(chip.value);
                    const states = has
                      ? gameStateFilter.states.filter(s => s !== chip.value)
                      : [...gameStateFilter.states, chip.value];
                    onGameStateFilterChange({ ...gameStateFilter, states });
                  }}
                  className={clsx(
                    'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
                    active
                      ? 'border-primary bg-primary/10 text-foreground'
                      : 'border-border bg-background text-muted-foreground hover:text-foreground'
                  )}
                >
                  {t(chip.i18nKey)}
                </button>
              );
            })}
            <button
              type="button"
              aria-pressed={gameStateFilter.withKb}
              onClick={() =>
                onGameStateFilterChange({
                  ...gameStateFilter,
                  withKb: !gameStateFilter.withKb,
                })
              }
              className={clsx(
                'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
                gameStateFilter.withKb
                  ? 'border-primary bg-primary/10 text-foreground'
                  : 'border-border bg-background text-muted-foreground hover:text-foreground'
              )}
            >
              {t('pages.library.filters.stato.withKb')}
            </button>
          </div>
        ) : null}

        {showMoreFilters ? (
          <>
            <span aria-hidden="true" className="mx-1 h-[22px] w-px flex-shrink-0 bg-border" />
            <button
              type="button"
              data-testid="cross-entity-filters-more"
              onClick={onMoreFilters}
              className={clsx(
                'inline-flex flex-shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-md px-3 py-1.5 font-display text-[12px] font-extrabold',
                moreFiltersActive
                  ? 'border border-entity-agent/40 bg-entity-agent/10 text-entity-agent'
                  : 'border border-border-strong bg-card text-foreground'
              )}
            >
              <span aria-hidden="true">⚙</span>
              {t('pages.library.filters.advanced.label')}
              {moreFiltersActive ? (
                <span className="rounded-full bg-entity-agent px-1.5 py-px font-mono text-[9px] font-extrabold text-white">
                  {activeFiltersCount}
                </span>
              ) : (
                ''
              )}
              {/* Preserve legacy text "({n})" for tests + non-visual readers when active. */}
              {moreFiltersActive ? (
                <span className="sr-only">{` (${activeFiltersCount})`}</span>
              ) : null}
            </button>
          </>
        ) : null}

        {/* Spacer pushes the view-toggle to the right end of the row. */}
        <div className="flex-1" />

        {/* View toggle — ARIA radiogroup, keyboard-navigable. */}
        <div
          role="radiogroup"
          aria-label={t('pages.library.view.ariaLabel')}
          data-slot="library-view-toggle"
          className="inline-flex flex-shrink-0 overflow-hidden rounded-md border border-border bg-card"
        >
          {VIEW_OPTIONS.map(opt => {
            const active = view === opt.id;
            return (
              <button
                key={opt.id}
                type="button"
                role="radio"
                aria-checked={active}
                aria-label={t(opt.labelKey)}
                onClick={() => onViewChange(opt.id)}
                data-view-mode={opt.id}
                className={clsx(
                  'cursor-pointer border-0 px-2.5 py-1.5 font-display text-[13px] font-bold',
                  active
                    ? 'bg-entity-game/12 text-entity-game'
                    : 'bg-transparent text-muted-foreground'
                )}
              >
                <span aria-hidden="true">{opt.icon}</span>
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
