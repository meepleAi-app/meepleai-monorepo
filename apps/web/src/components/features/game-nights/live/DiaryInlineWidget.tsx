import type { JSX } from 'react';

import { CrossGameDiaryTimeline } from '@/components/features/game-nights/live/CrossGameDiaryTimeline';
import type {
  DiaryEvent,
  DiaryGameRef,
  DiaryPlayerRef,
} from '@/components/features/game-nights/live/CrossGameDiaryTimeline';

export interface DiaryInlineWidgetProps {
  readonly events: ReadonlyArray<DiaryEvent>;
  readonly games: ReadonlyArray<DiaryGameRef>;
  readonly players: ReadonlyArray<DiaryPlayerRef>;
  readonly width?: number;
  readonly height?: number;
  readonly embedded?: boolean;
  readonly sessionsCount?: number;
  readonly isLive?: boolean;
  readonly maxEvents?: number;
  readonly onAddNote?: () => void;
  readonly onOpenFullTimeline?: () => void;
  readonly className?: string;
}

export function DiaryInlineWidget({
  events,
  games,
  players,
  width = 320,
  height = 400,
  embedded = false,
  sessionsCount,
  isLive = false,
  maxEvents = 7,
  onAddNote,
  onOpenFullTimeline,
  className,
}: DiaryInlineWidgetProps): JSX.Element {
  const visibleEvents = maxEvents > 0 ? events.slice(-maxEvents) : events;
  const derivedSessionsCount =
    sessionsCount ??
    new Set(events.map(e => e.gameId).filter((id): id is string => id !== null)).size;

  return (
    <div
      className={[
        'flex flex-col overflow-hidden',
        'rounded-lg border bg-card border-entity-event/30',
        embedded ? 'shadow-md ring-4 ring-entity-event/[0.06]' : '',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
      style={
        embedded
          ? { width, height, maxWidth: width }
          : { width: '100%', height: '100%', maxWidth: width }
      }
    >
      {/* Header */}
      <div className="flex items-center gap-1.5 px-3 py-2 bg-entity-event/[0.06] border-b border-entity-event/20">
        <span aria-hidden="true" className="text-[13px] leading-none">
          📜
        </span>
        <div className="min-w-0 flex-1">
          <span className="font-mono text-[9.5px] font-extrabold uppercase tracking-wider text-entity-event block">
            Diary cross-game
          </span>
          <span className="font-mono text-[9.5px] font-bold text-muted-foreground mt-px block">
            {events.length} {events.length === 1 ? 'evento' : 'eventi'} · {derivedSessionsCount}{' '}
            {derivedSessionsCount === 1 ? 'session' : 'session'}
          </span>
        </div>
        {isLive ? (
          <span
            aria-label="Live"
            className={[
              'inline-flex items-center gap-1 rounded-pill px-1.5 py-0.5',
              'bg-entity-session/15 text-entity-session',
              'font-mono text-[8.5px] font-extrabold uppercase tracking-wider',
            ].join(' ')}
          >
            <span
              aria-hidden="true"
              className="motion-safe:animate-pulse inline-block h-1.5 w-1.5 rounded-full bg-entity-session"
            />
            Live
          </span>
        ) : null}
      </div>

      {/* Body — scrollable timeline */}
      <div className="relative flex-1 overflow-y-auto px-3 py-2.5">
        <div
          aria-hidden="true"
          className="absolute left-4 top-2.5 bottom-2.5 w-0.5"
          style={{
            background:
              'linear-gradient(180deg, hsl(var(--c-event) / 0.3), hsl(var(--c-event) / 0.05))',
          }}
        />
        <CrossGameDiaryTimeline events={visibleEvents} games={games} players={players} compact />
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between border-t border-border px-2.5 py-1.5">
        {onAddNote ? (
          <button
            type="button"
            onClick={onAddNote}
            className={[
              'rounded-pill px-2 py-0.5 cursor-pointer',
              'bg-entity-event/10 text-entity-event border border-entity-event/30',
              'font-mono text-[9px] font-extrabold uppercase tracking-wider',
            ].join(' ')}
          >
            + Annota
          </button>
        ) : (
          <span />
        )}
        {onOpenFullTimeline ? (
          <button
            type="button"
            onClick={onOpenFullTimeline}
            className="font-mono text-[9px] font-bold text-muted-foreground cursor-pointer bg-transparent border-0"
          >
            Apri timeline ↗
          </button>
        ) : (
          <span className="font-mono text-[9px] font-bold text-muted-foreground">
            Apri timeline ↗
          </span>
        )}
      </div>
    </div>
  );
}
