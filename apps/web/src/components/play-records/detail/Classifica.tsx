/**
 * Classifica — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Ranked player list for the play record detail view.
 * Each row: rank medal · avatar (entity-tinted by userId/playerId hue) ·
 * player name · progress bar relative to top · score.
 *
 * AC-2.5: ranked by totalScore con avatar entity-tinted (userHue), barra progresso relativa al top
 * AC-2.7 EC-1 cooperative: no winner badge
 * AC-2.9 EC-4/5 spectator: currentUserPlayerId=null → no current-user highlight
 *
 * @see mockup `admin-mockups/design_files/sp4-play-records-detail.jsx` Classifica
 */
import type { ReactElement } from 'react';

import { entityHsl } from '@/components/ui/data-display/meeple-card';
import { userHue } from '@/lib/colors/user-hue';

export interface ClassificaRow {
  readonly playerId: string;
  readonly userId: string | null;
  readonly name: string;
  readonly totalScore: number | null;
  readonly isWinner: boolean;
}

export interface ClassificaProps {
  readonly rows: ReadonlyArray<ClassificaRow>;
  readonly isCooperative: boolean;
  /** The current user's playerId for row highlighting. null = spectator. */
  readonly currentUserPlayerId?: string | null;
  readonly className?: string;
}

const RANK_MEDAL = ['🥇', '🥈', '🥉'];

function getRankLabel(index: number): string {
  return RANK_MEDAL[index] ?? String(index + 1);
}

function getInitials(name: string): string {
  return name.trim().slice(0, 2).toUpperCase();
}

export function Classifica({
  rows,
  isCooperative,
  currentUserPlayerId = null,
  className,
}: ClassificaProps): ReactElement {
  // Sort descending by totalScore; null scores go to end
  const sorted = [...rows].sort((a, b) => {
    if (a.totalScore === null && b.totalScore === null) return 0;
    if (a.totalScore === null) return 1;
    if (b.totalScore === null) return -1;
    return b.totalScore - a.totalScore;
  });

  const topScore = sorted[0]?.totalScore ?? 1;
  const effectiveTop = topScore === 0 ? 1 : topScore; // avoid div/0

  return (
    <section
      data-slot="classifica"
      className={`${className ?? ''}`}
      role="region"
      aria-label={`Classifica · ${rows.length} giocatori`}
    >
      <h2 className="mb-2 flex items-center gap-1.5 font-display text-sm font-extrabold text-foreground">
        <span aria-hidden="true">🏅</span>
        Classifica
        <span className="font-mono text-[10px] font-bold text-muted-foreground">
          · {rows.length} giocatori
        </span>
      </h2>

      <div className="overflow-hidden rounded-xl border border-border bg-card">
        {sorted.map((row, i) => {
          const isCurrentUser =
            currentUserPlayerId !== null && currentUserPlayerId === row.playerId;
          const isWinner = !isCooperative && row.isWinner;
          const hue = userHue(row.userId ?? row.playerId);
          const pct =
            row.totalScore !== null
              ? Math.round(((row.totalScore > 0 ? row.totalScore : 0) / effectiveTop) * 100)
              : 0;

          return (
            <div
              key={row.playerId}
              data-slot="classifica-row"
              data-current-user={isCurrentUser ? 'true' : undefined}
              className={`flex items-center gap-3 px-3.5 py-3 ${
                i < sorted.length - 1 ? 'border-b border-border' : ''
              } ${isWinner ? 'bg-entity-session/5' : ''} ${isCurrentUser ? 'bg-entity-player/5' : ''}`}
            >
              {/* Rank */}
              <span
                className="w-6 shrink-0 text-center font-display text-base font-extrabold"
                style={{ color: isWinner ? entityHsl('toolkit') : undefined }}
                aria-label={`Posizione ${i + 1}`}
              >
                {getRankLabel(i)}
              </span>

              {/* Avatar */}
              <div
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full font-display text-xs font-extrabold text-white" // eslint-disable-line local/no-hardcoded-color-utility -- text-white on gradient avatar bg (.e-bg pattern)
                style={{
                  background: `linear-gradient(135deg, hsl(${hue}, 70%, 62%), hsl(${hue}, 60%, 42%))`,
                  border: '2px solid var(--card)',
                }}
                aria-hidden="true"
              >
                {getInitials(row.name)}
              </div>

              {/* Name + bar */}
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-1.5 text-[13px] font-extrabold text-foreground">
                  {row.name}
                  {isWinner && !isCooperative && (
                    <span
                      data-slot="winner-badge"
                      className="rounded-full px-1.5 py-0.5 font-mono text-[8.5px] font-extrabold uppercase tracking-wider"
                      style={{
                        background: entityHsl('toolkit', 0.14),
                        color: entityHsl('toolkit'),
                      }}
                    >
                      Vincitore
                    </span>
                  )}
                </div>
                {/* Progress bar */}
                <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-muted">
                  <div
                    data-slot="score-bar"
                    className="h-full rounded-full transition-all"
                    style={{
                      width: `${pct}%`,
                      background: isWinner ? entityHsl('toolkit') : entityHsl('session'),
                    }}
                  />
                </div>
              </div>

              {/* Score */}
              <span
                className="shrink-0 font-mono text-xl font-extrabold tabular-nums"
                style={{ color: isWinner ? entityHsl('toolkit') : undefined }}
              >
                {row.totalScore !== null ? row.totalScore : '—'}
              </span>
            </div>
          );
        })}
      </div>
    </section>
  );
}
