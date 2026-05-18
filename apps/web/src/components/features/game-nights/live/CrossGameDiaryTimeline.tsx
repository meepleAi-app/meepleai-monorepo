import type { JSX } from 'react';
import { useMemo } from 'react';

export type DiaryEventKind = 'turn' | 'score' | 'custom' | 'end' | 'system';

export interface DiaryEvent {
  readonly id: string;
  readonly time: string;
  readonly gameId: string | null;
  readonly kind: DiaryEventKind;
  readonly icon: string;
  readonly actors: ReadonlyArray<string>;
  readonly text: string;
}

export interface DiaryGameRef {
  readonly id: string;
  readonly title: string;
  readonly emoji: string;
}

export interface DiaryPlayerRef {
  readonly id: string;
  readonly initials: string;
  readonly color: number;
}

export type DiaryFilter = 'all' | 'turn' | 'score' | 'custom';

export interface CrossGameDiaryTimelineProps {
  readonly events: ReadonlyArray<DiaryEvent>;
  readonly games: ReadonlyArray<DiaryGameRef>;
  readonly players: ReadonlyArray<DiaryPlayerRef>;
  readonly filter?: DiaryFilter;
  readonly onFilterChange?: (filter: DiaryFilter) => void;
  readonly compact?: boolean;
  readonly className?: string;
}

const FILTERS: ReadonlyArray<{ id: DiaryFilter; label: string }> = [
  { id: 'all', label: 'Tutti' },
  { id: 'turn', label: 'Turn' },
  { id: 'score', label: 'Score' },
  { id: 'custom', label: 'Custom' },
];

const KIND_TO_ENTITY: Record<DiaryEventKind, string> = {
  turn: 'session',
  score: 'player',
  custom: 'chat',
  end: 'event',
  system: 'toolkit',
};

export function CrossGameDiaryTimeline({
  events,
  games,
  players,
  filter = 'all',
  onFilterChange,
  compact = false,
  className,
}: CrossGameDiaryTimelineProps): JSX.Element {
  const gamesById = useMemo(() => Object.fromEntries(games.map(g => [g.id, g])), [games]);
  const playersById = useMemo(() => Object.fromEntries(players.map(p => [p.id, p])), [players]);

  const filtered = useMemo(
    () => (filter === 'all' ? events : events.filter(e => e.kind === filter)),
    [events, filter]
  );

  return (
    <div className={['flex flex-col gap-2', className ?? ''].filter(Boolean).join(' ')}>
      {onFilterChange ? (
        <div role="tablist" aria-label="Filtro eventi diary" className="flex gap-1.5">
          {FILTERS.map(f => {
            const active = filter === f.id;
            return (
              <button
                key={f.id}
                role="tab"
                aria-selected={active}
                type="button"
                onClick={() => onFilterChange(f.id)}
                className={[
                  'rounded-pill px-2.5 py-1 font-mono text-[9.5px] font-extrabold uppercase tracking-wider cursor-pointer',
                  active
                    ? 'bg-entity-event/15 text-entity-event border border-entity-event/40'
                    : 'bg-transparent text-muted-foreground border border-border',
                ].join(' ')}
              >
                {f.label}
              </button>
            );
          })}
        </div>
      ) : null}

      <div
        role="log"
        aria-live="polite"
        aria-label="Timeline diary cross-game"
        className={['relative flex flex-col', compact ? 'gap-1.5' : 'gap-2'].join(' ')}
      >
        {filtered.length === 0 ? (
          <div className="font-mono text-xs text-muted-foreground py-4 text-center">
            Nessun evento ancora · Inizia a giocare!
          </div>
        ) : (
          filtered.map((event, i) => {
            const prevEvent = i > 0 ? filtered[i - 1] : null;
            const currentGame = event.gameId ? (gamesById[event.gameId] ?? null) : null;
            const prevGame = prevEvent?.gameId ? (gamesById[prevEvent.gameId] ?? null) : null;
            const gameChanged = currentGame !== null && currentGame.id !== prevGame?.id;
            return (
              <DiaryEventRow
                key={event.id}
                event={event}
                gameTitle={gameChanged ? currentGame.title : null}
                gameEmoji={gameChanged ? currentGame.emoji : null}
                actorPlayers={event.actors
                  .map(a => playersById[a])
                  .filter((p): p is DiaryPlayerRef => Boolean(p))}
                compact={compact}
              />
            );
          })
        )}
      </div>
    </div>
  );
}

interface DiaryEventRowProps {
  readonly event: DiaryEvent;
  readonly gameTitle: string | null;
  readonly gameEmoji: string | null;
  readonly actorPlayers: ReadonlyArray<DiaryPlayerRef>;
  readonly compact: boolean;
}

function DiaryEventRow({
  event,
  gameTitle,
  gameEmoji,
  actorPlayers,
  compact,
}: DiaryEventRowProps): JSX.Element {
  const entityKey = KIND_TO_ENTITY[event.kind];
  const isEnd = event.kind === 'end';
  const isSystem = event.kind === 'system';

  return (
    <>
      {gameTitle ? (
        <div
          aria-hidden="true"
          className={['flex items-center gap-1.5', compact ? 'py-0.5' : 'py-1'].join(' ')}
        >
          <span
            className="flex-1 h-px"
            style={{
              background:
                'linear-gradient(90deg, transparent, hsl(var(--c-event) / 0.4), transparent)',
            }}
          />
          <span className="font-mono text-[9px] font-extrabold uppercase tracking-wider text-entity-event inline-flex items-center gap-1">
            <span>{gameEmoji}</span>
            {gameTitle}
          </span>
          <span
            className="flex-1 h-px"
            style={{
              background:
                'linear-gradient(90deg, transparent, hsl(var(--c-event) / 0.4), transparent)',
            }}
          />
        </div>
      ) : null}
      <div className="relative flex gap-2.5 pl-1">
        <div
          aria-hidden="true"
          className="z-10 flex h-[22px] w-[22px] shrink-0 items-center justify-center rounded-full text-[10px]"
          style={{
            background: `hsl(var(--c-${entityKey}) / 0.14)`,
            border: `2px solid hsl(var(--c-${entityKey}) / 0.4)`,
          }}
        >
          <span aria-hidden="true">{event.icon}</span>
        </div>
        <div
          className={[
            'min-w-0 flex-1 rounded-sm px-2.5 py-1.5',
            isEnd
              ? 'bg-entity-event/10 border-l-2 border-l-entity-event'
              : isSystem
                ? 'bg-entity-toolkit/[0.06] border-l-2 border-l-entity-toolkit'
                : 'bg-muted border-l-2 border-l-transparent',
          ].join(' ')}
        >
          <div className="flex flex-wrap items-center gap-1.5 mb-px">
            <span
              className="font-mono text-[9.5px] font-extrabold uppercase tracking-wider tabular-nums"
              style={{ color: `hsl(var(--c-${entityKey}))` }}
            >
              {event.time}
            </span>
            {actorPlayers.length > 0 ? (
              <div className="flex gap-px">
                {actorPlayers.map(player => (
                  <span
                    key={player.id}
                    aria-label={`Player ${player.initials}`}
                    className="inline-flex h-[14px] w-[14px] items-center justify-center rounded-full border-2 font-display text-[7px] font-extrabold"
                    style={{
                      background: `hsl(${player.color}, 60%, 55%)`,
                      borderColor: 'var(--bg-muted)' as string,
                      color: '#fff',
                    }}
                  >
                    {player.initials}
                  </span>
                ))}
              </div>
            ) : null}
          </div>
          <div className="font-body text-[11.5px] font-semibold leading-snug text-foreground">
            {event.text}
          </div>
        </div>
      </div>
    </>
  );
}
