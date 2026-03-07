'use client';

import { useQuery } from '@tanstack/react-query';
import {
  BarChart3,
  Clock,
  Gamepad2,
  Trophy,
  TrendingUp,
  Loader2,
} from 'lucide-react';
import { api } from '@/lib/api';

export default function SessionStatsPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['session-statistics'],
    queryFn: () => api.sessionStatistics.getStatistics(12),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-20">
        <Loader2 className="h-8 w-8 animate-spin text-amber-600" />
      </div>
    );
  }

  if (!data) {
    return (
      <div className="px-6 py-10 text-center text-muted-foreground">
        <BarChart3 className="mx-auto mb-4 h-12 w-12 opacity-40" />
        <p>No session data available yet. Play some games to see stats!</p>
      </div>
    );
  }

  const maxMonthly = Math.max(
    ...data.monthlyActivity.map((m) => m.sessionCount),
    1,
  );

  return (
    <div className="space-y-8 p-6">
      <div>
        <h1 className="font-quicksand text-2xl font-bold">Session Analytics</h1>
        <p className="text-sm text-muted-foreground">
          Your gaming activity over the last 12 months
        </p>
      </div>

      {/* KPI Cards */}
      <div
        className="grid grid-cols-1 gap-4 sm:grid-cols-3"
        data-testid="kpi-cards"
      >
        <div className="rounded-xl border bg-white/70 p-5 backdrop-blur-md">
          <div className="flex items-center gap-3">
            <Gamepad2 className="h-5 w-5 text-amber-600" />
            <span className="text-sm text-muted-foreground">
              Total Sessions
            </span>
          </div>
          <p className="mt-2 text-3xl font-bold" data-testid="total-sessions">
            {data.totalSessions}
          </p>
        </div>

        <div className="rounded-xl border bg-white/70 p-5 backdrop-blur-md">
          <div className="flex items-center gap-3">
            <Trophy className="h-5 w-5 text-amber-600" />
            <span className="text-sm text-muted-foreground">
              Games Played
            </span>
          </div>
          <p className="mt-2 text-3xl font-bold" data-testid="total-games">
            {data.totalGamesPlayed}
          </p>
        </div>

        <div className="rounded-xl border bg-white/70 p-5 backdrop-blur-md">
          <div className="flex items-center gap-3">
            <Clock className="h-5 w-5 text-amber-600" />
            <span className="text-sm text-muted-foreground">
              Avg Duration
            </span>
          </div>
          <p className="mt-2 text-3xl font-bold" data-testid="avg-duration">
            {data.averageSessionDuration}
          </p>
        </div>
      </div>

      {/* Most Played Games */}
      {data.mostPlayedGames.length > 0 && (
        <div>
          <h2 className="mb-3 font-quicksand text-lg font-semibold">
            Most Played Games
          </h2>
          <div
            className="space-y-2"
            data-testid="most-played-list"
          >
            {data.mostPlayedGames.map((game) => (
              <div
                key={game.gameId}
                className="flex items-center justify-between rounded-lg border bg-white/50 px-4 py-3"
              >
                <span className="font-medium">{game.gameName}</span>
                <span className="rounded-full bg-amber-100 px-3 py-1 text-sm font-semibold text-amber-900">
                  {game.playCount} plays
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Monthly Activity */}
      {data.monthlyActivity.length > 0 && (
        <div>
          <h2 className="mb-3 font-quicksand text-lg font-semibold">
            Monthly Activity
          </h2>
          <div
            className="flex items-end gap-2"
            data-testid="monthly-chart"
            style={{ height: 160 }}
          >
            {data.monthlyActivity.map((month) => (
              <div
                key={month.month}
                className="flex flex-1 flex-col items-center gap-1"
              >
                <span className="text-xs font-medium">
                  {month.sessionCount}
                </span>
                <div
                  className="w-full rounded-t bg-amber-500"
                  style={{
                    height: `${(month.sessionCount / maxMonthly) * 120}px`,
                    minHeight: 4,
                  }}
                />
                <span className="text-[10px] text-muted-foreground">
                  {month.month.slice(5)}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Recent Score Trends */}
      {data.recentScoreTrends.length > 0 && (
        <div>
          <h2 className="mb-3 font-quicksand text-lg font-semibold">
            Recent Scores
          </h2>
          <div className="space-y-1" data-testid="score-trends">
            {data.recentScoreTrends.slice(0, 10).map((score, i) => (
              <div
                key={`${score.date}-${i}`}
                className="flex items-center justify-between rounded border bg-white/50 px-4 py-2 text-sm"
              >
                <span className="flex items-center gap-2">
                  <TrendingUp className="h-3 w-3 text-emerald-600" />
                  {score.gameName}
                </span>
                <span className="font-mono font-semibold">
                  {score.finalScore}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
