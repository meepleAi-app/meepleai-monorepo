'use client';

/**
 * LiveScoreboard — Sorted scores with leader highlight and quick score update.
 *
 * Compact version of the full Scoreboard, optimized for the live play view.
 * Participants sorted by score descending; leader gets a crown icon.
 *
 * Issue #5587 — Live Game Session UI
 */

import { useState, useEffect, useRef } from 'react';

import { Crown, TrendingUp, TrendingDown, Minus } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface LiveScoreboardPlayer {
  id: string;
  displayName: string;
  totalScore: number;
  avatarColor: string;
  isCurrentUser?: boolean;
}

export interface LiveScoreboardProps {
  players: LiveScoreboardPlayer[];
  /** Enable real-time score change animations */
  isRealTime?: boolean;
  className?: string;
}

export function LiveScoreboard({ players, isRealTime = false, className }: LiveScoreboardProps) {
  const [animatingIds, setAnimatingIds] = useState<Set<string>>(new Set());
  const prevScoresRef = useRef<Map<string, number>>(new Map());

  // Track score changes for animation
  useEffect(() => {
    if (!isRealTime) return;

    const newAnimating = new Set<string>();
    players.forEach(p => {
      const prev = prevScoresRef.current.get(p.id);
      if (prev !== undefined && prev !== p.totalScore) {
        newAnimating.add(p.id);
        setTimeout(() => {
          setAnimatingIds(s => {
            const next = new Set(s);
            next.delete(p.id);
            return next;
          });
        }, 1000);
      }
      prevScoresRef.current.set(p.id, p.totalScore);
    });

    if (newAnimating.size > 0) {
      setAnimatingIds(newAnimating);
    }
  }, [players, isRealTime]);

  const sorted = [...players].sort((a, b) => b.totalScore - a.totalScore);

  const getScoreTrend = (playerId: string): 'up' | 'down' | 'neutral' => {
    const prev = prevScoresRef.current.get(playerId);
    const current = sorted.find(p => p.id === playerId)?.totalScore ?? 0;
    if (prev === undefined) return 'neutral';
    if (current > prev) return 'up';
    if (current < prev) return 'down';
    return 'neutral';
  };

  const trendIcons = {
    up: (
      <TrendingUp className="h-3 w-3 text-emerald-600 dark:text-emerald-400" aria-hidden="true" />
    ),
    down: <TrendingDown className="h-3 w-3 text-red-600 dark:text-red-400" aria-hidden="true" />,
    neutral: <Minus className="h-3 w-3 text-slate-400" aria-hidden="true" />,
  };

  return (
    <section
      className={cn('space-y-1.5', className)}
      aria-label="Classifica"
      data-testid="live-scoreboard"
    >
      {sorted.map((player, index) => {
        const isLeader = index === 0 && player.totalScore > 0;
        const trend = getScoreTrend(player.id);
        const isAnimating = animatingIds.has(player.id);

        return (
          <div
            key={player.id}
            data-testid={`scoreboard-row-${player.id}`}
            className={cn(
              'flex items-center gap-3 rounded-xl px-3 py-2.5 transition-all',
              isLeader
                ? 'bg-amber-100/80 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/40 shadow-sm'
                : 'bg-white/60 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-700/40',
              player.isCurrentUser && !isLeader && 'ring-1 ring-amber-400/40',
              isAnimating && 'animate-pulse'
            )}
          >
            {/* Avatar */}
            <div
              className="h-8 w-8 flex-shrink-0 rounded-lg flex items-center justify-center text-xs font-bold text-white shadow-sm ring-1 ring-black/10"
              style={{
                background: `linear-gradient(135deg, ${player.avatarColor} 0%, ${player.avatarColor}dd 100%)`,
              }}
            >
              {player.displayName
                .replace(/\s*\(io\)\s*/i, '')
                .split(' ')
                .map(n => n[0])
                .join('')
                .toUpperCase()
                .slice(0, 2)}
            </div>

            {/* Name + trend */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-1.5">
                <span className="font-semibold text-sm text-slate-900 dark:text-slate-100 truncate">
                  {player.displayName}
                </span>
                {isLeader && (
                  <Crown className="h-3.5 w-3.5 text-amber-500 flex-shrink-0" aria-label="Leader" />
                )}
              </div>
            </div>

            {/* Score + trend icon */}
            <div className="flex items-center gap-1.5 flex-shrink-0">
              {/* eslint-disable-next-line security/detect-object-injection -- trend is typed union */}
              {isRealTime && trendIcons[trend]}
              <span
                className={cn(
                  'font-mono text-base font-black tabular-nums',
                  isLeader
                    ? 'text-amber-900 dark:text-amber-400'
                    : 'text-slate-700 dark:text-slate-300'
                )}
              >
                {player.totalScore}
              </span>
              <span className="text-xs text-slate-400">pts</span>
            </div>
          </div>
        );
      })}
    </section>
  );
}
