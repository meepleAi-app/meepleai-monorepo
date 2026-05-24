/**
 * GameDetailLeaderboard - Wave C follow-up (Issue #1468)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (Leaderboard, lines 759-812).
 * Tracking: epic #1475; design-handoff PILOT_GAP_REPORT § 2.3.
 *
 * Pure presentational leaderboard. Ranks registered players by wins. The caller owns the
 * `useGameLeaderboard` hook (#1467) and passes `entries` already fetched, plus a `hueFor`
 * mapper (e.g. `userHue`, #1470) for avatar colors. Loading/error states are the caller's
 * responsibility (mirror `GameDetailSpecsCard` / `GameDetailKpiCards`).
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { GameLeaderboardEntry } from '@/lib/api/schemas';

export interface GameDetailLeaderboardLabels {
  readonly plays: string;
  readonly avgScore: string;
  readonly wins: string;
  readonly empty: string;
}

export interface GameDetailLeaderboardProps {
  readonly entries: ReadonlyArray<GameLeaderboardEntry>;
  /** Localized card title, resolved upstream (caller-side i18n). */
  readonly title: string;
  /** Localized labels, resolved upstream. */
  readonly labels: GameDetailLeaderboardLabels;
  /** Maps a playerId to an HSL hue (0..360) for the avatar. Caller injects userHue (#1470); omitted => neutral. */
  readonly hueFor?: (playerId: string) => number;
  /** Max rows rendered (default 10). */
  readonly maxItems?: number;
  readonly className?: string;
}

const EM_DASH = '—';

export function GameDetailLeaderboard(props: GameDetailLeaderboardProps): ReactElement {
  const { entries, title, labels, hueFor, maxItems = 10, className } = props;
  const rows = entries.slice(0, maxItems);

  return (
    <section
      data-slot="game-detail-leaderboard"
      className={clsx('rounded-2xl border border-border bg-card p-[18px] shadow-sm', className)}
    >
      <h3 className="mb-3.5 font-display text-[15px] font-extrabold text-foreground">{title}</h3>

      {rows.length === 0 ? (
        <p
          data-slot="game-detail-leaderboard-empty"
          className="py-6 text-center font-mono text-[11px] text-muted-foreground"
        >
          {labels.empty}
        </p>
      ) : (
        <ol role="list" aria-label={title} className="flex flex-col">
          {rows.map((e, i) => {
            const hue = hueFor?.(e.playerId);
            const isFirst = i === 0;
            return (
              <li
                key={e.playerId}
                role="listitem"
                data-slot="game-detail-leaderboard-row"
                className={clsx(
                  'flex items-center gap-3 py-2.5',
                  i < rows.length - 1 && 'border-b border-border'
                )}
              >
                <span
                  className={clsx(
                    'w-[22px] shrink-0 text-center font-mono text-[13px] font-extrabold',
                    isFirst ? 'text-entity-agent' : 'text-muted-foreground'
                  )}
                >
                  {isFirst ? '🏆' : `#${i + 1}`}
                </span>

                <span
                  data-slot="game-detail-leaderboard-avatar"
                  aria-hidden="true"
                  className={clsx(
                    'inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full font-display text-[11px] font-extrabold',
                    hue === undefined ? 'bg-muted text-muted-foreground' : 'text-white'
                  )}
                  style={hue === undefined ? undefined : { background: `hsl(${hue}, 60%, 55%)` }}
                >
                  {e.initials}
                </span>

                <div className="min-w-0 flex-1">
                  <div className="truncate font-display text-[13px] font-extrabold text-foreground">
                    {e.displayName}
                  </div>
                  <div className="font-mono text-[10px] font-semibold text-muted-foreground">
                    {e.plays} {labels.plays} · {labels.avgScore}{' '}
                    <span>{e.avgScore === null ? EM_DASH : e.avgScore}</span>
                  </div>
                </div>

                <div className="flex flex-col items-end">
                  <span className="font-display text-[16px] font-extrabold leading-none tabular-nums text-entity-player">
                    {e.wins}
                  </span>
                  <span className="mt-0.5 font-mono text-[9px] font-bold uppercase tracking-[0.06em] text-muted-foreground">
                    {labels.wins}
                  </span>
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
