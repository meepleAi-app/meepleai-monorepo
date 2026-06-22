/**
 * PlayerTopGamesCard — /players/[id] v2 component (issue #1546, #1485 follow-up).
 *
 * Mapped from `admin-mockups/design_files/sp4-player-detail.jsx:528-592`
 * (function `TopGamesCard`). Ranked top-N games card with optional per-game
 * win rate, mirroring the sibling pattern from `PlayerLeaderboardCard`.
 *
 * Pure component: all i18n strings injected via `labels`. No hooks.
 *
 * Renders:
 *   - Title row
 *   - Ranked rows (rank-1 = trophy 🏆, rank-2+ = #N)
 *   - Each row: game title + plays/wins subtitle + optional win rate %
 *   - Empty state when items is empty
 *
 * Data source: `PlayerStatistics.mostPlayedGames` (post-#1663 Phase 2) joined
 * with `winByGame` for per-game wins. When `winCount` is null (BE rollout not
 * yet on env), the win-rate badge gracefully hides — never renders "0%" for
 * "no data".
 *
 * WCAG:
 *   - Trophy/rank glyphs are aria-hidden (decorative).
 *   - Rank exposed to AT via sr-only span (ARIA 1.2: no aria-label on generic div).
 *   - Win-rate badge: numeric value + sr-only "WIN RATE" suffix label.
 *   - Empty state uses role="status" for polite announcement.
 *
 * Refs #1485 gap report § 2.1, #1546.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

/**
 * Top-N game entry — the public input contract of `PlayerTopGamesCard`.
 *
 * Derived upstream from `PlayerStatistics.mostPlayedGames` (priority,
 * post-#1663 Phase 2) with `winByGame` joined for `winCount`. Falls back
 * to `gamePlayCounts` when the optional Phase 2 fields are not yet rolled
 * out on the env. `winCount === null` ⇒ "no data" (badge hidden);
 * `winCount === 0` ⇒ valid datum "played but never won" (renders "0%").
 *
 * This type is the single source of truth for the component contract; the
 * fixture re-uses it via import.
 */
export interface TopGameItem {
  readonly gameId: string | null;
  readonly gameName: string;
  readonly playCount: number;
  /** Per-game wins; `null` when BE hasn't shipped/joined per-game wins. */
  readonly winCount: number | null;
}

export interface PlayerTopGamesCardLabels {
  readonly title: string;
  /** Template: "{count} plays" — substitute {count} with playCount. */
  readonly playsLabel: string;
  /** Template: "{count} plays · {wins} wins" — substitute {count} and {wins}. */
  readonly playsLabelWithWins: string;
  /** Short tag rendered above the win-rate %; e.g. "WIN RATE". */
  readonly winRateLabel: string;
  /** Template: "Rank {rank}" — substitute {rank} with numeric value (1-based). */
  readonly rankAriaLabel: string;
  /** Shown when items is empty. */
  readonly empty: string;
}

export interface PlayerTopGamesCardProps {
  readonly items: ReadonlyArray<TopGameItem>;
  /** Cap the rendered list. Default: 5 (mockup spec). */
  readonly maxItems?: number;
  readonly labels: PlayerTopGamesCardLabels;
  readonly className?: string;
}

/** Substitutes named placeholders in a label template. */
function interpolate(template: string, values: Record<string, string>): string {
  return Object.entries(values).reduce((acc, [key, val]) => acc.replace(`{${key}}`, val), template);
}

/**
 * Sorts top-games desc by playCount and caps to `maxItems`. The BE may already
 * return sorted data; this is defense-in-depth so the component is correct
 * regardless of caller invariants.
 */
function takeTopN(items: ReadonlyArray<TopGameItem>, maxItems: number): ReadonlyArray<TopGameItem> {
  const sorted = [...items].sort((a, b) => b.playCount - a.playCount);
  return sorted.slice(0, maxItems);
}

interface RankIndicatorProps {
  readonly rank: number; // 1-based
}

function RankIndicator({ rank }: RankIndicatorProps): ReactElement {
  if (rank === 1) {
    return (
      <span
        aria-hidden="true"
        data-slot="player-detail-top-games-rank-trophy"
        className="w-6 text-center font-mono text-[13px] font-extrabold leading-none text-amber-700 dark:text-amber-400"
      >
        🏆
      </span>
    );
  }
  return (
    <span
      aria-hidden="true"
      data-slot="player-detail-top-games-rank"
      className="w-6 text-center font-mono text-[11px] font-extrabold leading-none text-muted-foreground"
    >
      #{rank}
    </span>
  );
}

export function PlayerTopGamesCard({
  items,
  maxItems = 5,
  labels,
  className,
}: PlayerTopGamesCardProps): ReactElement {
  const visible = takeTopN(items, maxItems);

  return (
    <div
      data-slot="player-detail-top-games"
      className={clsx(
        'flex flex-col gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm',
        className
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between gap-2">
        <h3 className="font-display text-[15px] font-extrabold text-foreground">{labels.title}</h3>
      </div>

      {visible.length === 0 ? (
        <p
          data-slot="player-detail-top-games-empty"
          role="status"
          className="text-sm text-muted-foreground"
        >
          {labels.empty}
        </p>
      ) : (
        <ul data-slot="player-detail-top-games-list" className="flex flex-col">
          {visible.map((item, idx) => {
            const rank = idx + 1;
            const hasWins = item.winCount !== null && item.winCount >= 0 && item.playCount > 0;
            const winRatePct = hasWins
              ? Math.round(((item.winCount ?? 0) / item.playCount) * 100)
              : null;

            const subtitle = hasWins
              ? interpolate(labels.playsLabelWithWins, {
                  count: String(item.playCount),
                  wins: String(item.winCount ?? 0),
                })
              : interpolate(labels.playsLabel, { count: String(item.playCount) });

            // Stable key: gameId when present, otherwise gameName + rank to dedupe
            // entries that share a name (extremely unlikely but defensive).
            const key = item.gameId ?? `${item.gameName}-${rank}`;

            return (
              <li
                key={key}
                data-slot="player-detail-top-games-item"
                className={clsx(
                  'flex items-center gap-3 py-2.5',
                  idx < visible.length - 1 && 'border-b border-border/50'
                )}
              >
                <span className="sr-only">
                  {interpolate(labels.rankAriaLabel, { rank: String(rank) })}
                </span>
                <RankIndicator rank={rank} />
                <div className="flex min-w-0 flex-1 flex-col">
                  <span className="truncate font-display text-[13px] font-extrabold text-foreground">
                    {item.gameName}
                  </span>
                  <span className="font-mono text-[10px] font-semibold text-muted-foreground">
                    {subtitle}
                  </span>
                </div>
                {winRatePct !== null ? (
                  <div
                    data-slot="player-detail-top-games-win-rate"
                    className="flex min-w-[3.5rem] flex-col items-end"
                  >
                    <span
                      aria-hidden="true"
                      className="font-display text-base font-extrabold leading-none tabular-nums text-emerald-700 dark:text-emerald-400"
                    >
                      {winRatePct}%
                    </span>
                    <span
                      aria-hidden="true"
                      className="mt-0.5 font-mono text-[9px] font-bold uppercase tracking-wider text-muted-foreground"
                    >
                      {labels.winRateLabel}
                    </span>
                    <span className="sr-only">
                      {labels.winRateLabel}: {winRatePct}%
                    </span>
                  </div>
                ) : null}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
