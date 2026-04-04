'use client';

import { memo } from 'react';

import { cn } from '@/lib/utils';

interface RankingPlayer {
  name: string;
  score: number;
  position: number;
  isLeader?: boolean;
}

interface RankingBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'ranking';
    players: RankingPlayer[];
  };
}

export const RankingBlock = memo(function RankingBlock({
  title,
  entityColor,
  data,
}: RankingBlockProps) {
  const { players } = data;

  return (
    <div className="flex flex-col gap-2">
      {title && (
        <>
          <h4
            className="text-xs font-semibold uppercase tracking-wide"
            style={{ color: `hsl(${entityColor})` }}
          >
            {title}
          </h4>
          <div className="h-px w-full" style={{ backgroundColor: `hsl(${entityColor} / 0.2)` }} />
        </>
      )}

      {players.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">No data yet</p>
      ) : (
        <ol className="flex flex-col gap-1">
          {players.map(player => (
            <li
              key={player.position}
              className={cn(
                'flex items-center gap-2 rounded px-1.5 py-1 text-xs',
                player.isLeader && 'font-semibold'
              )}
              style={
                player.isLeader
                  ? { backgroundColor: `hsl(${entityColor} / 0.12)`, color: `hsl(${entityColor})` }
                  : undefined
              }
            >
              <span className="w-4 shrink-0 text-center tabular-nums text-muted-foreground">
                {player.position}
              </span>
              <span className="flex-1 truncate">{player.name}</span>
              <span className="shrink-0 tabular-nums">{player.score}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
});

RankingBlock.displayName = 'RankingBlock';
