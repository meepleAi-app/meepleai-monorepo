'use client';

import { type ReactElement } from 'react';

import type { LiveSessionDto, LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { WINGSPAN_CATEGORIES } from './wingspan-state';

export interface WingspanCategoryBreakdownProps {
  readonly players: ReadonlyArray<LiveSessionPlayerDto>;
  readonly roundScores: LiveSessionDto['roundScores'];
  readonly categoryLabels: Record<string, string>;
  readonly heading: string;
}

function sumCategory(
  roundScores: LiveSessionDto['roundScores'],
  playerId: string,
  categoryId: string
): number {
  return roundScores
    .filter(rs => rs.playerId === playerId && rs.dimension === categoryId)
    .reduce((sum, rs) => sum + rs.value, 0);
}

export function WingspanCategoryBreakdown({
  players,
  roundScores,
  categoryLabels,
  heading,
}: WingspanCategoryBreakdownProps): ReactElement {
  return (
    <section data-slot="wingspan-breakdown" className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {heading}
      </h3>
      <ul role="list" className="flex flex-col gap-1">
        {players.map(player => (
          <li key={player.id} className="flex flex-col gap-1 rounded-lg bg-card px-2 py-1.5">
            <span className="text-xs font-semibold text-foreground">{player.displayName}</span>
            <span className="flex flex-wrap gap-x-3 gap-y-0.5">
              {WINGSPAN_CATEGORIES.map(cat => (
                <span
                  key={cat.id}
                  data-player={player.id}
                  data-category={cat.id}
                  className="inline-flex items-center gap-1 text-xs text-muted-foreground"
                  title={categoryLabels[cat.id] ?? cat.id}
                >
                  <span aria-hidden="true">{cat.emoji}</span>
                  <span className="font-semibold tabular-nums text-foreground">
                    {sumCategory(roundScores, player.id, cat.id)}
                  </span>
                </span>
              ))}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
