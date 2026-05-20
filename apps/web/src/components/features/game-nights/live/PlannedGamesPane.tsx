import type { JSX } from 'react';

export type PlannedGameStatus = 'completed' | 'inprogress' | 'upcoming';

export interface PlannedGameWinner {
  readonly name: string;
  readonly initials: string;
  readonly color: number;
}

export interface PlannedGame {
  readonly id: string;
  readonly title: string;
  readonly publisher?: string;
  readonly emoji?: string;
  readonly cover?: readonly [string, string];
  readonly status: PlannedGameStatus;
  readonly order: number;
  readonly actual?: string;
  readonly estimated?: string;
  readonly score?: string;
  readonly winner?: PlannedGameWinner;
}

export interface PlannedGamesPaneProps {
  readonly games: ReadonlyArray<PlannedGame>;
  readonly compact?: boolean;
  readonly title?: string;
  readonly playOrderLocked?: boolean;
  readonly onAddGame?: () => void;
  readonly addGameDisabledLabel?: string;
  readonly className?: string;
}

const STATUS_ACCENT: Record<PlannedGameStatus, string> = {
  completed: 'hsl(var(--c-toolkit))',
  inprogress: 'hsl(var(--c-session))',
  upcoming: 'hsl(var(--c-game))',
};

export function PlannedGamesPane({
  games,
  compact = false,
  title = 'Planned',
  playOrderLocked = true,
  onAddGame,
  addGameDisabledLabel = 'Aggiungi disponibile fuori live',
  className,
}: PlannedGamesPaneProps): JSX.Element {
  const canAdd = Boolean(onAddGame);

  return (
    <aside
      aria-label="Planned games"
      className={[
        'flex flex-col bg-card min-w-0 shrink-0',
        compact ? 'w-full' : 'w-[280px] border-r border-border',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <div className="flex items-center justify-between border-b border-border-light px-3.5 py-2.5">
        <div className="flex items-center gap-1.5">
          <span aria-hidden="true">🎲</span>
          <span className="font-mono text-[10.5px] font-extrabold uppercase tracking-wider text-muted-foreground">
            {title} ({games.length})
          </span>
        </div>
        {playOrderLocked ? (
          <span
            title="Riordino disabilitato in live mode"
            className="rounded-pill border border-border bg-muted px-1.5 py-0.5 font-mono text-[9px] font-bold text-muted-foreground"
          >
            🔒 PlayOrder
          </span>
        ) : null}
      </div>

      <ul className="flex flex-1 flex-col gap-2 overflow-y-auto p-2.5 m-0 list-none min-h-0">
        {games.length === 0 ? (
          <li className="font-mono text-xs text-muted-foreground text-center py-6">
            Nessun gioco pianificato
          </li>
        ) : (
          games.map(game => <PlannedGameRow key={game.id} game={game} />)
        )}
      </ul>

      <div className="border-t border-border-light p-3">
        <button
          type="button"
          onClick={onAddGame}
          disabled={!canAdd}
          title={canAdd ? undefined : addGameDisabledLabel}
          className={[
            'w-full rounded-md border border-dashed font-display text-[11.5px] font-bold py-2',
            canAdd
              ? 'cursor-pointer border-border-strong text-foreground hover:bg-muted'
              : 'cursor-not-allowed border-border-strong text-muted-foreground opacity-70',
          ].join(' ')}
        >
          + Aggiungi gioco
        </button>
      </div>
    </aside>
  );
}

function PlannedGameRow({ game }: { readonly game: PlannedGame }): JSX.Element {
  const isCompleted = game.status === 'completed';
  const isInProgress = game.status === 'inprogress';
  const isUpcoming = game.status === 'upcoming';
  const accent = STATUS_ACCENT[game.status];

  return (
    <li
      data-status={game.status}
      className={[
        'relative rounded-md px-3 py-2.5 border-l-4',
        isInProgress
          ? 'bg-entity-session/[0.08] border border-entity-session/35'
          : 'bg-card border border-border',
        isCompleted ? 'opacity-80' : '',
      ]
        .filter(Boolean)
        .join(' ')}
      style={{ borderLeftColor: accent }}
    >
      <div className="flex items-center gap-2 mb-1.5">
        <span
          aria-hidden="true"
          className="inline-flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-sm text-base"
          style={
            game.cover
              ? { background: `linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})` }
              : { background: 'var(--bg-muted)' }
          }
        >
          {game.emoji ?? '🎲'}
        </span>
        <div className="min-w-0 flex-1">
          <div className="truncate font-display text-[12.5px] font-extrabold text-foreground">
            {game.title}
          </div>
          {game.publisher ? (
            <div className="font-mono text-[9.5px] font-bold text-muted-foreground">
              #{game.order} · {game.publisher}
            </div>
          ) : (
            <div className="font-mono text-[9.5px] font-bold text-muted-foreground">
              #{game.order}
            </div>
          )}
        </div>
        <span
          aria-label={`Status: ${game.status}`}
          className="relative inline-flex h-[22px] w-[22px] shrink-0 items-center justify-center rounded-full font-display text-[11px] font-extrabold"
          style={{ background: accent, color: '#fff' }}
        >
          {isCompleted ? '✓' : null}
          {isInProgress ? (
            <>
              <span className="text-[10px]">●</span>
              <span
                aria-hidden="true"
                className="motion-safe:animate-ping absolute -inset-[3px] rounded-full border-2 opacity-40"
                style={{ borderColor: accent }}
              />
            </>
          ) : null}
          {isUpcoming ? <span className="text-[10px]">⏳</span> : null}
        </span>
      </div>

      {/* Status line */}
      <div
        className="flex items-center gap-1.5 font-mono text-[10px] font-bold tabular-nums"
        style={{ color: accent }}
      >
        {isCompleted ? (
          <>
            <span>FINITO</span>
            <span className="text-muted-foreground">·</span>
            <span className="text-foreground/70">{game.actual ?? '—'}</span>
          </>
        ) : null}
        {isInProgress ? (
          <>
            <span
              aria-hidden="true"
              className="motion-safe:animate-pulse inline-block h-1.5 w-1.5 rounded-full"
              style={{ background: accent }}
            />
            <span>LIVE</span>
            {game.actual ? (
              <>
                <span className="text-muted-foreground">·</span>
                <span className="text-foreground/70">{game.actual}</span>
              </>
            ) : null}
          </>
        ) : null}
        {isUpcoming ? (
          <>
            <span>UPCOMING</span>
            {game.estimated ? (
              <>
                <span className="text-muted-foreground">·</span>
                <span className="text-foreground/70">est. {game.estimated}</span>
              </>
            ) : null}
          </>
        ) : null}
      </div>

      {/* Winner banner */}
      {isCompleted && game.winner ? (
        <div className="mt-1.5 flex items-center gap-1.5 rounded-sm border border-entity-event/30 bg-entity-event/10 px-2 py-1.5">
          <span
            aria-label={`Winner ${game.winner.initials}`}
            className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full font-display text-[9px] font-extrabold"
            style={{
              background: `hsl(${game.winner.color}, 60%, 55%)`,
              color: '#fff',
            }}
          >
            {game.winner.initials}
          </span>
          <div className="min-w-0 flex-1">
            <div className="truncate font-display text-[11px] font-extrabold text-entity-event">
              🏆 {game.winner.name} ha vinto
            </div>
            {game.score ? (
              <div className="font-mono text-[9px] font-bold tabular-nums text-muted-foreground">
                {game.score}
              </div>
            ) : null}
          </div>
        </div>
      ) : null}
    </li>
  );
}
