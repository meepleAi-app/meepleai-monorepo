import type { CSSProperties, JSX } from 'react';

import clsx from 'clsx';

import { GAMES, MIN_SELECTED, type TourGame } from './data';

export interface GameSelectStepProps {
  readonly selected: readonly string[];
  readonly onToggle: (id: string) => void;
}

function GameTile({
  game,
  selected,
  onToggle,
}: {
  readonly game: TourGame;
  readonly selected: boolean;
  readonly onToggle: (id: string) => void;
}): JSX.Element {
  const bg: CSSProperties = {
    background: `linear-gradient(160deg, hsl(${game.gradient[0]}), hsl(${game.gradient[1]}))`,
  };
  return (
    <button
      type="button"
      aria-pressed={selected}
      aria-label={`${game.title}, ${selected ? 'selezionato' : 'seleziona'}`}
      onClick={() => onToggle(game.id)}
      className={clsx(
        'group relative flex flex-col overflow-hidden rounded-xl border-2 text-left transition-transform',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2',
        selected
          ? 'border-[hsl(var(--e-game))] scale-[0.97]'
          : 'border-transparent hover:scale-[1.02]'
      )}
    >
      <div className="relative flex h-24 items-center justify-center text-4xl" style={bg}>
        <span aria-hidden="true">{game.emoji}</span>
        {selected && (
          <span
            aria-hidden="true"
            className="absolute right-1.5 top-1.5 flex h-6 w-6 items-center justify-center rounded-full bg-white text-sm font-bold text-[hsl(var(--e-game))] shadow"
          >
            ✓
          </span>
        )}
      </div>
      <div className="flex flex-col gap-0.5 bg-card px-2 py-2">
        <span className="font-quicksand text-sm font-semibold">{game.title}</span>
        <span className="font-nunito text-xs text-muted-foreground">
          {game.year} · {game.players}p
        </span>
      </div>
    </button>
  );
}

export function GameSelectStep({ selected, onToggle }: GameSelectStepProps): JSX.Element {
  const count = selected.length;
  const ready = count >= MIN_SELECTED;
  return (
    <div className="flex flex-col gap-5 px-4 py-6">
      <div className="text-center">
        <h2 className="font-quicksand text-xl font-bold">
          Quali giochi hai nella tua <span className="text-[hsl(var(--e-game))]">ludoteca</span>?
        </h2>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Seleziona almeno {MIN_SELECTED}, li useremo per personalizzare l&apos;esperienza.
        </p>
      </div>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4">
        {GAMES.map(g => (
          <GameTile key={g.id} game={g} selected={selected.includes(g.id)} onToggle={onToggle} />
        ))}
      </div>
      <p
        aria-live="polite"
        className={clsx(
          'text-center font-nunito text-sm',
          ready ? 'text-[hsl(var(--e-game))] font-semibold' : 'text-muted-foreground'
        )}
      >
        {ready
          ? `✓ ${count} giochi selezionati`
          : `${count} di ${MIN_SELECTED} selezionati — ancora ${MIN_SELECTED - count}`}
      </p>
    </div>
  );
}
