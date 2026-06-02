/**
 * GamesTabPanel — content for the "Games" tab on /players/[id] (Issue #1113).
 *
 * MVP: ranked list of gamePlayCounts entries + CTA to /players/[id]/games.
 * Each entry is a simple <li> row; follow-up issue may upgrade to MeepleCard
 * grid once a richer per-game shape lands.
 */

'use client';

import type { JSX } from 'react';
import { useMemo } from 'react';

import Link from 'next/link';

export interface GamesTabPanelLabels {
  readonly title: string;
  readonly viewAll: string;
  readonly empty: string;
  readonly playsSuffix: string;
}

export interface GamesTabPanelProps {
  readonly playerId: string;
  readonly gamePlayCounts: Readonly<Record<string, number>>;
  readonly labels: GamesTabPanelLabels;
}

export function GamesTabPanel({
  playerId,
  gamePlayCounts,
  labels,
}: GamesTabPanelProps): JSX.Element {
  const ranked = useMemo(
    () =>
      Object.entries(gamePlayCounts)
        .filter(([, count]) => count > 0)
        .sort(([, a], [, b]) => b - a),
    [gamePlayCounts]
  );

  const isEmpty = ranked.length === 0;

  return (
    <div
      data-slot="games-tab-panel"
      className="mx-auto w-full max-w-4xl px-4 sm:px-8 flex flex-col gap-4"
    >
      <h2 className="text-lg font-semibold">{labels.title}</h2>
      {isEmpty ? (
        <p className="text-muted-foreground">{labels.empty}</p>
      ) : (
        <>
          <ul className="flex flex-col gap-2">
            {ranked.map(([name, count]) => (
              <li
                key={name}
                className="flex items-center justify-between rounded-md border border-border px-3 py-2"
              >
                <span className="font-medium">{name}</span>
                <span className="text-muted-foreground tabular-nums">
                  {count} {labels.playsSuffix}
                </span>
              </li>
            ))}
          </ul>
          <Link
            href={`/players/${playerId}/games`}
            className="text-sm font-semibold text-entity-game-text hover:underline"
          >
            {labels.viewAll}
          </Link>
        </>
      )}
    </div>
  );
}
