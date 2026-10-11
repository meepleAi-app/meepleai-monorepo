/**
 * GameCandidatesPicker — Step 4 ("Cosa") component.
 * Issue #950 W3 Components. Spec §5 (C5) + §12 Scenario 6 (decide-at-group).
 *
 * Pure component: receives the library game list and emits selection intent.
 * With `onSearchChange` it also shows a search box; the search itself runs on the
 * server, in the caller (#4187).
 */

'use client';

import { useId, type ReactElement } from 'react';

export interface GameCandidatesPickerLabels {
  readonly label: string;
  readonly decideAtGroupLabel: string;
  readonly decideAtGroupHelper: string;
  readonly selectedCount: (count: number) => string;
  readonly libraryHeader: string;
  readonly libraryEmpty: string;
  readonly selectGame: (name: string) => string;
  readonly deselectGame: (name: string) => string;
  /** Accessible name of the search box (#4187). */
  readonly searchLabel?: string;
  /** Shown when a search finds nothing, instead of "your library is empty" (#4187). */
  readonly searchEmpty?: (query: string) => string;
}

export interface GameCandidateOption {
  readonly id: string;
  readonly title: string;
  readonly imageUrl?: string | null;
  readonly minPlayers?: number | null;
  readonly maxPlayers?: number | null;
  readonly playingTimeMinutes?: number | null;
}

export interface GameCandidatesPickerProps {
  readonly games: readonly GameCandidateOption[];
  readonly selected: readonly string[];
  readonly decideAtGroup: boolean;
  readonly onToggleGame: (gameId: string) => void;
  readonly onToggleDecideAtGroup: () => void;
  readonly labels: GameCandidatesPickerLabels;
  /** Current search text. */
  readonly search?: string;
  /** When set, the picker shows a search box and forwards what the user types. */
  readonly onSearchChange?: (value: string) => void;
}

export function GameCandidatesPicker({
  games,
  selected,
  decideAtGroup,
  onToggleGame,
  onToggleDecideAtGroup,
  labels,
  search = '',
  onSearchChange,
}: GameCandidatesPickerProps): ReactElement {
  const toggleId = useId();
  const searchId = useId();
  const selectedSet = new Set(selected);
  const trimmedSearch = search.trim();
  const emptyMessage =
    trimmedSearch && labels.searchEmpty ? labels.searchEmpty(trimmedSearch) : labels.libraryEmpty;

  return (
    <section
      data-slot="game-night-create-step4"
      aria-labelledby={`${toggleId}-label`}
      className="flex flex-col gap-4"
    >
      <p id={`${toggleId}-label`} className="text-sm font-medium text-foreground">
        {labels.label}
      </p>

      <label
        htmlFor={toggleId}
        className="flex items-start gap-3 rounded-md border border-border bg-card p-3"
        data-slot="game-night-create-step4-decide-toggle"
      >
        <input
          id={toggleId}
          type="checkbox"
          checked={decideAtGroup}
          onChange={onToggleDecideAtGroup}
          className="mt-1"
        />
        <span className="flex flex-col">
          <span className="text-sm font-medium text-foreground">{labels.decideAtGroupLabel}</span>
          <span className="text-xs text-muted-foreground">{labels.decideAtGroupHelper}</span>
        </span>
      </label>

      <div
        data-slot="game-night-create-step4-library"
        // `inert` removes the subtree from the accessibility tree AND from
        // axe's color-contrast scan (#1700 release CI), unlike aria-hidden
        // alone. Without this, the opacity-50 dimming on already-muted text
        // dropped contrast below WCAG AA 4.5:1 even though the subtree is
        // visually marked as inactive.
        {...(decideAtGroup ? { inert: true } : {})}
        className={decideAtGroup ? 'opacity-50 pointer-events-none' : ''}
      >
        <p className="text-sm font-medium text-foreground">{labels.libraryHeader}</p>
        <p className="text-xs text-muted-foreground">{labels.selectedCount(selected.length)}</p>

        {onSearchChange && (
          <input
            id={searchId}
            type="search"
            value={search}
            onChange={e => onSearchChange(e.target.value)}
            aria-label={labels.searchLabel ?? labels.libraryHeader}
            placeholder={labels.searchLabel}
            autoComplete="off"
            data-slot="game-night-create-step4-search"
            className="mt-2 w-full rounded-md border border-border bg-card px-3 py-2 text-sm text-foreground"
          />
        )}

        {games.length === 0 ? (
          <p className="mt-2 text-xs text-muted-foreground">{emptyMessage}</p>
        ) : (
          <ul
            role="list"
            className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3"
            data-slot="game-night-create-step4-grid"
          >
            {games.map(game => {
              const isSelected = selectedSet.has(game.id);
              const ariaLabel = isSelected
                ? labels.deselectGame(game.title)
                : labels.selectGame(game.title);
              return (
                <li key={game.id}>
                  <button
                    type="button"
                    onClick={() => onToggleGame(game.id)}
                    aria-pressed={isSelected}
                    aria-label={ariaLabel}
                    data-slot={`game-night-create-step4-game-${game.id}`}
                    className={
                      isSelected
                        ? 'flex w-full flex-col gap-2 rounded-md border border-primary bg-primary/10 p-3 text-left'
                        : 'flex w-full flex-col gap-2 rounded-md border border-border bg-card p-3 text-left hover:border-primary'
                    }
                  >
                    <span className="text-sm font-medium text-foreground">{game.title}</span>
                    {(game.minPlayers || game.playingTimeMinutes) && (
                      <span className="text-xs text-muted-foreground">
                        {game.minPlayers && game.maxPlayers
                          ? `${game.minPlayers}–${game.maxPlayers} 👤`
                          : null}
                        {game.minPlayers && game.maxPlayers && game.playingTimeMinutes
                          ? ' · '
                          : null}
                        {game.playingTimeMinutes ? `${game.playingTimeMinutes}'` : null}
                      </span>
                    )}
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </section>
  );
}
