import type { JSX } from 'react';

export interface PerGameRecapPlayer {
  readonly id: string;
  readonly name: string;
  readonly initials: string;
  readonly color: number;
  readonly score?: number;
}

export interface PerGameRecapGame {
  readonly id: string;
  readonly sessionId?: string;
  readonly title: string;
  readonly emoji?: string;
  readonly cover?: readonly [string, string];
  readonly order: number;
  readonly duration: string;
  readonly eventsCount: number;
  readonly coopMode?: boolean;
  readonly coopOutcome?: string;
  readonly winner?: PerGameRecapPlayer & { readonly score: number };
  readonly topScores?: ReadonlyArray<PerGameRecapPlayer>;
}

export interface PerGameRecapRowProps {
  readonly game: PerGameRecapGame;
  readonly mobile?: boolean;
  readonly onJumpToSession?: (sessionId: string) => void;
  readonly className?: string;
}

export function PerGameRecapRow({
  game,
  mobile = false,
  onJumpToSession,
  className,
}: PerGameRecapRowProps): JSX.Element {
  const isCoop = Boolean(game.coopMode);
  const accentClass = isCoop ? 'border-l-entity-toolkit' : 'border-l-entity-event';

  return (
    <article
      data-coop={isCoop ? 'true' : 'false'}
      className={[
        'rounded-lg border border-border bg-card border-l-4',
        accentClass,
        mobile ? 'flex flex-col gap-2.5 p-3' : 'grid items-center gap-4 px-4 py-3.5',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
      style={mobile ? undefined : { gridTemplateColumns: '90px 1fr auto' }}
    >
      {/* Cover */}
      <div
        aria-hidden="true"
        className={[
          'relative flex items-center justify-center rounded-md',
          mobile ? 'h-[110px] text-[44px]' : 'h-[70px] text-3xl',
        ].join(' ')}
        style={
          game.cover
            ? { background: `linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})` }
            : { background: 'var(--bg-muted)' }
        }
      >
        <span
          className="absolute left-1.5 top-1.5 rounded-pill px-1.5 py-px font-mono text-[9px] font-extrabold tracking-wider"
          style={{ background: 'rgba(0,0,0,0.55)', color: '#fff' }}
        >
          #{game.order}
        </span>
        <span className="drop-shadow-lg">{game.emoji ?? '🎲'}</span>
      </div>

      {/* Info column */}
      <div className="flex min-w-0 flex-col gap-1.5">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="m-0 font-display text-base font-extrabold tracking-tight text-foreground">
            {game.title}
          </h3>
          {isCoop ? (
            <span className="inline-flex items-center gap-1 rounded-pill border border-entity-toolkit/30 bg-entity-toolkit/15 px-1.5 py-px font-mono text-[9px] font-extrabold uppercase tracking-wider text-entity-toolkit">
              <span aria-hidden="true">🤝</span>
              Co-op
            </span>
          ) : null}
          <span className="font-mono text-[9.5px] font-extrabold uppercase tracking-wider text-muted-foreground">
            ⏱ {game.duration} · {game.eventsCount} {game.eventsCount === 1 ? 'evento' : 'eventi'}
          </span>
        </div>

        {/* Winner line */}
        <div className="flex flex-wrap items-center gap-2">
          {isCoop ? (
            <>
              <span className="inline-flex items-center gap-1.5 rounded-pill border border-entity-toolkit/30 bg-entity-toolkit/15 px-2 py-0.5 font-display text-[11px] font-extrabold text-entity-toolkit">
                <span aria-hidden="true">🏆</span>
                Team coop ha vinto
              </span>
              {game.coopOutcome ? (
                <span className="font-mono text-[10.5px] font-bold text-foreground/70">
                  {game.coopOutcome}
                </span>
              ) : null}
            </>
          ) : game.winner ? (
            <>
              <span
                aria-label={`Winner ${game.winner.initials}`}
                className="inline-flex h-[22px] w-[22px] shrink-0 items-center justify-center rounded-full border-2 font-display text-[9px] font-extrabold"
                style={{
                  background: `hsl(${game.winner.color}, 60%, 55%)`,
                  color: '#fff',
                  borderColor: 'hsl(var(--c-event) / 0.4)' as string,
                }}
              >
                {game.winner.initials}
              </span>
              <span className="font-display text-[12.5px] font-extrabold text-foreground">
                🏆 {game.winner.name}
              </span>
              <span className="font-mono text-[11px] font-extrabold tabular-nums text-entity-event">
                {game.winner.score}pt
              </span>
              {game.topScores && game.topScores.length > 0 ? (
                <div className="ml-1 flex gap-1">
                  {game.topScores.slice(0, 3).map(p => (
                    <span
                      key={p.id}
                      aria-label={`Top ${p.initials}`}
                      className="inline-flex h-[18px] items-center gap-1 rounded-pill border border-border bg-muted px-1.5 font-mono text-[9px] font-bold tabular-nums text-foreground/70"
                    >
                      {p.initials}
                      {p.score !== undefined ? ` ${p.score}` : ''}
                    </span>
                  ))}
                </div>
              ) : null}
            </>
          ) : (
            <span className="font-mono text-[10.5px] font-bold text-muted-foreground">
              Nessun winner registrato
            </span>
          )}
        </div>
      </div>

      {/* CTA */}
      {(() => {
        const sessionId = game.sessionId;
        if (!sessionId || !onJumpToSession) return null;
        return (
          <button
            type="button"
            onClick={() => onJumpToSession(sessionId)}
            className={[
              'shrink-0 rounded-md border border-entity-session/30 bg-entity-session/10 font-display font-extrabold text-entity-session cursor-pointer',
              mobile ? 'w-full px-3 py-2 text-[12px]' : 'px-3 py-2 text-[12.5px]',
            ].join(' ')}
          >
            Vai a session →
          </button>
        );
      })()}
    </article>
  );
}
