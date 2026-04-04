/**
 * PlayerStatsCard
 *
 * AgentMemory — Task 25
 *
 * Shows a player's stats for a specific game (or all games).
 * Fetches data from the agent-memory player stats API.
 */

'use client';

import { useEffect, useState } from 'react';

import { BarChart3, Loader2, Trophy } from 'lucide-react';

import type { PlayerMemoryDto, PlayerGameStatsDto } from '@/lib/api/clients/agentMemoryClient';
import { useApiClient } from '@/lib/api/context';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface PlayerStatsCardProps {
  userId: string;
  gameId?: string;
}

// ─── Stat Row ────────────────────────────────────────────────────────────────

function StatRow({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-xs text-gray-500 font-nunito">{label}</span>
      <span className="font-quicksand font-semibold text-sm text-gray-900">{value}</span>
    </div>
  );
}

// ─── Component ───────────────────────────────────────────────────────────────

export function PlayerStatsCard({ userId, gameId }: PlayerStatsCardProps) {
  const api = useApiClient();
  const [stats, setStats] = useState<PlayerMemoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function fetchStats() {
      setLoading(true);
      setError(null);
      try {
        const data = await api.agentMemory.getMyStats();
        if (!cancelled) {
          setStats(data);
        }
      } catch (_err) {
        if (!cancelled) {
          setError('Failed to load player stats');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    fetchStats();
    return () => {
      cancelled = true;
    };
  }, [api, userId]);

  // Aggregate game stats, optionally filtered by gameId
  const aggregated = aggregateStats(stats, gameId);

  if (loading) {
    return (
      <div
        className="rounded-xl border border-white/40 bg-white/70 backdrop-blur-md shadow-sm p-4 flex items-center justify-center gap-2"
        data-testid="player-stats-card"
      >
        <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
        <span className="text-sm text-gray-500 font-nunito">Loading stats...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div
        className="rounded-xl border border-red-200 bg-red-50/70 backdrop-blur-md shadow-sm p-4"
        data-testid="player-stats-card"
      >
        <p className="text-sm text-red-600 font-nunito">{error}</p>
      </div>
    );
  }

  return (
    <div
      className="rounded-xl border border-white/40 bg-white/70 backdrop-blur-md shadow-sm p-4 space-y-3"
      data-testid="player-stats-card"
    >
      {/* Header */}
      <div className="flex items-center gap-2">
        <BarChart3 className="h-4 w-4 text-amber-600" />
        <h3 className="font-quicksand font-semibold text-sm text-gray-900">
          {gameId ? 'Game Stats' : 'Overall Stats'}
        </h3>
      </div>

      {aggregated.totalPlayed === 0 ? (
        <p className="text-sm text-gray-500 font-nunito italic">No games played yet</p>
      ) : (
        <div className="space-y-2">
          {/* Win/Loss */}
          <div className="flex gap-3">
            <div className="flex items-center gap-1.5 rounded-lg bg-green-50 px-3 py-1.5">
              <Trophy className="h-3.5 w-3.5 text-green-600" />
              <span className="font-quicksand font-bold text-green-700">{aggregated.wins}</span>
              <span className="text-xs text-green-600 font-nunito">wins</span>
            </div>
            <div className="flex items-center gap-1.5 rounded-lg bg-red-50 px-3 py-1.5">
              <span className="font-quicksand font-bold text-red-700">{aggregated.losses}</span>
              <span className="text-xs text-red-600 font-nunito">losses</span>
            </div>
          </div>

          {/* Details */}
          <div className="space-y-1.5 pt-1">
            <StatRow label="Total games" value={aggregated.totalPlayed} />
            {aggregated.bestScore !== null && (
              <StatRow label="Best score" value={aggregated.bestScore} />
            )}
            {aggregated.totalPlayed > 0 && (
              <StatRow
                label="Win rate"
                value={`${Math.round((aggregated.wins / aggregated.totalPlayed) * 100)}%`}
              />
            )}
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

interface AggregatedStats {
  wins: number;
  losses: number;
  totalPlayed: number;
  bestScore: number | null;
}

function aggregateStats(playerMemories: PlayerMemoryDto[], gameId?: string): AggregatedStats {
  const allGameStats: PlayerGameStatsDto[] = playerMemories.flatMap(pm => pm.gameStats);

  const filtered = gameId ? allGameStats.filter(gs => gs.gameId === gameId) : allGameStats;

  if (filtered.length === 0) {
    return { wins: 0, losses: 0, totalPlayed: 0, bestScore: null };
  }

  let wins = 0;
  let losses = 0;
  let totalPlayed = 0;
  let bestScore: number | null = null;

  for (const gs of filtered) {
    wins += gs.wins;
    losses += gs.losses;
    totalPlayed += gs.totalPlayed;
    if (gs.bestScore !== null) {
      bestScore = bestScore === null ? gs.bestScore : Math.max(bestScore, gs.bestScore);
    }
  }

  return { wins, losses, totalPlayed, bestScore };
}
