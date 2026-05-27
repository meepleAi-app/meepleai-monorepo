/**
 * CrossEntityFilters — Phase 2a (#1605). Chip row above the hub grid.
 *
 * In the `games` tab the STATO chip group is FUNCTIONAL: it carries the
 * game-state filters that the retired `loaned`/`kb` tabs used to provide
 * (Owned / Wishlist / InPrestito + a with-KB toggle), so no access regresses
 * when the 3 game-state tabs collapse into one `games` tab. For all other tabs
 * the component renders nothing — search + sort live as globals in the hub
 * toolbar.
 *
 * Controlled component: the parent (`LibraryHub`) owns `gameStateFilter`.
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
  className,
}: CrossEntityFiltersProps): ReactElement | null {
  const { t } = useTranslation();

  if (tab !== 'games') return null;

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
    </div>
  );
}
