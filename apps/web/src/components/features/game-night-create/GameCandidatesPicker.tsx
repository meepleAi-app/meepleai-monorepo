/**
 * GameCandidatesPicker — Step 4 ("Cosa") component.
 * Issue #950 W3 Components. Spec §5 (C5) + §12 Scenario 6 (decide-at-group).
 *
 * Pure component: receives the library game list and emits selection intent.
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
}

export function GameCandidatesPicker({
  games,
  selected,
  decideAtGroup,
  onToggleGame,
  onToggleDecideAtGroup,
  labels,
}: GameCandidatesPickerProps): ReactElement {
  const toggleId = useId();
  const selectedSet = new Set(selected);

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
        aria-hidden={decideAtGroup}
        className={decideAtGroup ? 'opacity-50 pointer-events-none' : ''}
      >
        <p className="text-sm font-medium text-foreground">{labels.libraryHeader}</p>
        <p className="text-xs text-muted-foreground">{labels.selectedCount(selected.length)}</p>

        {games.length === 0 ? (
          <p className="mt-2 text-xs text-muted-foreground">{labels.libraryEmpty}</p>
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
