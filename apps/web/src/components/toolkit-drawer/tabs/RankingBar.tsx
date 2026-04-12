'use client';

/**
 * RankingBar — Live ranking showing top players with medals.
 */

import React from 'react';

export interface RankingEntry {
  id: string;
  name: string;
  color: string;
  total: number;
}

export interface RankingBarProps {
  entries: RankingEntry[];
}

const MEDALS = ['🥇', '🥈', '🥉'];

export function RankingBar({ entries }: RankingBarProps) {
  if (entries.length === 0) return null;

  const sorted = [...entries].sort((a, b) => b.total - a.total);

  return (
    <div
      className="flex flex-wrap items-center justify-center gap-2 text-xs"
      data-testid="ranking-bar"
    >
      {sorted.slice(0, 3).map((entry, idx) => (
        <div key={entry.id} className="flex items-center gap-1">
          <span>{MEDALS[idx]}</span>
          <span className="font-medium" style={{ color: entry.color }}>
            {entry.name}
          </span>
          <span className="font-bold text-gray-800">{entry.total}</span>
          {idx < Math.min(2, sorted.length - 1) && <span className="ml-1 text-gray-300">·</span>}
        </div>
      ))}
    </div>
  );
}
