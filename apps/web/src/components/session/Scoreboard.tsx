'use client';

import React, { useState, useEffect, useRef } from 'react';

import { TrendingUp, TrendingDown, Minus, Trophy } from 'lucide-react';

import { MeepleParticipantCard } from './MeepleParticipantCard';
import { ScoreboardData } from './types';

interface ScoreboardProps {
  data: ScoreboardData;
  isRealTime?: boolean;
  variant?: 'full' | 'compact';
}

export function Scoreboard({ data, isRealTime = false, variant = 'full' }: ScoreboardProps) {
  const [animatingScores, setAnimatingScores] = useState<Set<string>>(new Set());
  const prevScoresRef = useRef<Map<string, number>>(new Map());

  // Track score changes for animations
  useEffect(() => {
    if (!isRealTime) return;

    const newAnimating = new Set<string>();
    data.participants.forEach(p => {
      const prevScore = prevScoresRef.current.get(p.id);
      if (prevScore !== undefined && prevScore !== p.totalScore) {
        newAnimating.add(p.id);
        setTimeout(() => {
          setAnimatingScores(prev => {
            const next = new Set(prev);
            next.delete(p.id);
            return next;
          });
        }, 1000);
      }
      prevScoresRef.current.set(p.id, p.totalScore);
    });

    if (newAnimating.size > 0) {
      setAnimatingScores(newAnimating);
    }
  }, [data.participants, isRealTime]);

  const getScoresByParticipant = (participantId: string, roundNum?: number) => {
    return data.scores
      .filter(
        s =>
          s.participantId === participantId &&
          (roundNum === undefined || s.roundNumber === roundNum)
      )
      .reduce((sum, s) => sum + s.scoreValue, 0);
  };

  const getScoreTrend = (participantId: string): 'up' | 'down' | 'neutral' => {
    const allScores = data.scores
      .filter(s => s.participantId === participantId)
      .sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());

    if (allScores.length < 2) return 'neutral';

    const recent = allScores.slice(-2);
    if (recent[1].scoreValue > recent[0].scoreValue) return 'up';
    if (recent[1].scoreValue < recent[0].scoreValue) return 'down';
    return 'neutral';
  };

  const trendIcons = {
    up: <TrendingUp className="h-3 w-3 text-emerald-600 dark:text-emerald-400" />,
    down: <TrendingDown className="h-3 w-3 text-red-600 dark:text-red-400" />,
    neutral: <Minus className="h-3 w-3 text-slate-400" />,
  };

  if (variant === 'compact') {
    return (
      <div className="space-y-2">
        {data.participants
          .sort((a, b) => (b.rank ?? 999) - (a.rank ?? 999))
          .map(participant => (
            <MeepleParticipantCard
              key={participant.id}
              participant={participant}
              variant="compact"
            />
          ))}
      </div>
    );
  }

  // Full scoreboard with round breakdown
  return (
    <div className="space-y-6">
      {/* Leader Podium - Top 3 */}
      {data.participants.filter(p => p.rank && p.rank <= 3).length > 0 && (
        <div className="rounded-2xl border border-amber-200 dark:border-amber-900/30 bg-gradient-to-br from-amber-50 to-orange-50 dark:from-amber-950/20 dark:to-orange-950/10 p-6 shadow-xl shadow-amber-900/10">
          {/* Wood texture overlay */}
          <div className="absolute inset-0 opacity-[0.03] dark:opacity-[0.05] rounded-2xl bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZmlsdGVyIGlkPSJub2lzZSI+PGZlVHVyYnVsZW5jZSB0eXBlPSJmcmFjdGFsTm9pc2UiIGJhc2VGcmVxdWVuY3k9IjAuOCIgbnVtT2N0YXZlcz0iNCIgc3RpdGNoVGlsZXM9InN0aXRjaCIvPjwvZmlsdGVyPjxyZWN0IHdpZHRoPSIxMDAlIiBoZWlnaHQ9IjEwMCUiIGZpbHRlcj0idXJsKCNub2lzZSkiIG9wYWNpdHk9IjAuNSIvPjwvc3ZnPg==')] pointer-events-none" />

          <div className="relative flex items-center gap-2 mb-4">
            <Trophy className="h-5 w-5 text-amber-600 dark:text-amber-400" />
            <h2 className="font-bold text-lg text-slate-900 dark:text-amber-50 tracking-tight">
              Current Leaders
            </h2>
          </div>

          <div className="relative grid gap-3 sm:grid-cols-3">
            {data.participants
              .filter(p => p.rank && p.rank <= 3)
              .sort((a, b) => (a.rank ?? 999) - (b.rank ?? 999))
              .map(participant => (
                <MeepleParticipantCard
                  key={participant.id}
                  participant={participant}
                  variant="full"
                />
              ))}
          </div>
        </div>
      )}

      {/* Detailed Score Table */}
      {data.rounds.length > 0 && (
        <div className="overflow-hidden rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/50 shadow-lg">
          {/* Table Header */}
          <div className="border-b border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/80 px-4 py-3">
            <h3 className="font-bold text-sm uppercase tracking-wider text-slate-600 dark:text-slate-400">
              Score Breakdown
            </h3>
          </div>

          {/* Scrollable Table */}
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="border-b border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/50">
                <tr>
                  <th className="sticky left-0 z-10 bg-slate-50 dark:bg-slate-800/50 px-4 py-3 text-left text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
                    Player
                  </th>
                  {data.rounds.map(round => (
                    <th
                      key={round}
                      className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400 whitespace-nowrap"
                    >
                      R{round}
                    </th>
                  ))}
                  {data.categories.map(category => (
                    <th
                      key={category}
                      className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400 whitespace-nowrap"
                    >
                      {category}
                    </th>
                  ))}
                  <th className="sticky right-0 z-10 bg-gradient-to-l from-amber-50 to-transparent dark:from-slate-800/50 px-4 py-3 text-right text-xs font-bold uppercase tracking-wider text-amber-700 dark:text-amber-400">
                    Total
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-700/50">
                {data.participants
                  .sort((a, b) => b.totalScore - a.totalScore)
                  .map(participant => {
                    const trend = getScoreTrend(participant.id);
                    const isAnimating = animatingScores.has(participant.id);

                    return (
                      <tr
                        key={participant.id}
                        className={`transition-colors hover:bg-slate-50 dark:hover:bg-slate-800/30 ${
                          participant.isCurrentUser ? 'bg-amber-50/30 dark:bg-amber-950/10' : ''
                        } ${isAnimating ? 'animate-pulse' : ''}`}
                      >
                        {/* Player Name - Sticky */}
                        <td className="sticky left-0 z-10 bg-white dark:bg-slate-800/50 px-4 py-3">
                          <div className="flex items-center gap-2">
                            <div
                              className="h-8 w-8 flex items-center justify-center rounded-md text-xs font-bold text-white shadow-sm ring-1 ring-black/10"
                              style={{
                                background: `linear-gradient(135deg, ${participant.avatarColor} 0%, ${participant.avatarColor}dd 100%)`,
                              }}
                            >
                              {participant.displayName
                                .replace(/\s*\(io\)\s*/i, '')
                                .split(' ')
                                .map(n => n[0])
                                .join('')
                                .toUpperCase()
                                .slice(0, 2)}
                            </div>
                            <div className="min-w-0">
                              <div className="font-semibold text-sm text-slate-900 dark:text-slate-100 truncate">
                                {participant.displayName}
                              </div>
                              <div className="flex items-center gap-1 text-xs text-slate-500 dark:text-slate-400">
                                {/* eslint-disable-next-line security/detect-object-injection -- trend is typed TrendType from computed value */}
                                {trendIcons[trend]}
                                <span>Rank #{participant.rank}</span>
                              </div>
                            </div>
                          </div>
                        </td>

                        {/* Round Scores */}
                        {data.rounds.map(round => {
                          const roundScore = getScoresByParticipant(participant.id, round);
                          return (
                            <td key={round} className="px-4 py-3 text-center">
                              <span className="font-mono text-sm font-semibold tabular-nums text-slate-700 dark:text-slate-300">
                                {roundScore > 0 ? `+${roundScore}` : roundScore || '-'}
                              </span>
                            </td>
                          );
                        })}

                        {/* Category Scores */}
                        {data.categories.map(category => {
                          const categoryScores = data.scores
                            .filter(
                              s => s.participantId === participant.id && s.category === category
                            )
                            .reduce((sum, s) => sum + s.scoreValue, 0);
                          return (
                            <td key={category} className="px-4 py-3 text-center">
                              <span className="font-mono text-sm font-semibold tabular-nums text-slate-700 dark:text-slate-300">
                                {categoryScores || '-'}
                              </span>
                            </td>
                          );
                        })}

                        {/* Total Score - Sticky */}
                        <td className="sticky right-0 z-10 bg-gradient-to-l from-amber-50 to-white dark:from-slate-800/80 dark:to-slate-800/50 px-4 py-3 text-right">
                          <div className="inline-flex items-center gap-2 rounded-lg bg-amber-100 dark:bg-amber-950/30 px-3 py-1.5">
                            <span className="font-mono text-lg font-black tabular-nums text-amber-900 dark:text-amber-400">
                              {participant.totalScore.toLocaleString()}
                            </span>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Other Participants (Rank > 3) */}
      {data.participants.filter(p => !p.rank || p.rank > 3).length > 0 && (
        <div className="space-y-2">
          <h3 className="font-bold text-sm uppercase tracking-wider text-slate-600 dark:text-slate-400 px-1">
            Other Players
          </h3>
          {data.participants
            .filter(p => !p.rank || p.rank > 3)
            .sort((a, b) => b.totalScore - a.totalScore)
            .map(participant => (
              <MeepleParticipantCard
                key={participant.id}
                participant={participant}
                variant="compact"
              />
            ))}
        </div>
      )}

      {/* Real-time indicator */}
      {isRealTime && (
        <div className="flex items-center justify-center gap-2 text-xs text-slate-500 dark:text-slate-400">
          <span className="relative flex h-2 w-2">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500" />
          </span>
          <span className="font-medium">Live updates enabled</span>
        </div>
      )}
    </div>
  );
}
