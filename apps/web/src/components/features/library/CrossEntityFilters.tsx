/**
 * CrossEntityFilters — Phase 2a (#1605) + Phase 3b (#1593). Chip row above the hub grid.
 *
 * In the `games` tab the STATO chip group is FUNCTIONAL (Owned / Wishlist /
 * InPrestito + with-KB toggle). Phase 3b adds a "Più filtri" chip (on every tab
 * except `all`) that opens the AdvancedFiltersDrawer via `onMoreFilters`.
 *
 * Controlled component: the parent (`LibraryHub`) owns `gameStateFilter` + the
 * drawer state.
 */

'use client';

import { type ReactElement } from 'react';

import clsx from 'clsx';

import { useTranslation } from '@/hooks/useTranslation';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import type { HybridHubTab } from '@/lib/library/hybrid-hub.derive';

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
  readonly className?: string;
}

const STATE_CHIPS: ReadonlyArray<{ value: GameStateType; i18nKey: string }> = [
  { value: 'Owned', i18nKey: 'pages.library.filters.stato.owned' },
  { value: 'Wishlist', i18nKey: 'pages.library.filters.stato.wishlist' },
  { value: 'InPrestito', i18nKey: 'pages.library.filters.stato.loaned' },
];

export function CrossEntityFilters({
  tab,
  gameStateFilter,
  onGameStateFilterChange,
  onMoreFilters,
  activeFiltersCount = 0,
  className,
}: CrossEntityFiltersProps): ReactElement | null {
  const { t } = useTranslation();

  const showStato = tab === 'games';
  // R4: hide the drawer chip on the 'all' tab (no single entity scope).
  const showMoreFilters = onMoreFilters !== undefined && tab !== 'all';

  if (!showStato && !showMoreFilters) return null;

  const toggleState = (value: GameStateType) => {
    const has = gameStateFilter.states.includes(value);
    const states = has
      ? gameStateFilter.states.filter(s => s !== value)
      : [...gameStateFilter.states, value];
    onGameStateFilterChange({ ...gameStateFilter, states });
  };

  return (
    <div
      data-slot="cross-entity-filters"
      data-testid="cross-entity-filters-stato"
      className={clsx('flex flex-wrap items-center gap-2', className)}
    >
      {showStato ? (
        <>
          <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {t('pages.library.filters.stato.label')}
          </span>
          {STATE_CHIPS.map(chip => {
            const active = gameStateFilter.states.includes(chip.value);
            return (
              <button
                key={chip.value}
                type="button"
                aria-pressed={active}
                onClick={() => toggleState(chip.value)}
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
              onGameStateFilterChange({ ...gameStateFilter, withKb: !gameStateFilter.withKb })
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
        </>
      ) : null}

      {showMoreFilters ? (
        <button
          type="button"
          data-testid="cross-entity-filters-more"
          onClick={onMoreFilters}
          className={clsx(
            'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
            activeFiltersCount > 0
              ? 'border-primary bg-primary/10 text-foreground'
              : 'border-border bg-background text-muted-foreground hover:text-foreground'
          )}
        >
          {t('pages.library.filters.title')}
          {activeFiltersCount > 0 ? ` (${activeFiltersCount})` : ''}
        </button>
      ) : null}
    </div>
  );
}
