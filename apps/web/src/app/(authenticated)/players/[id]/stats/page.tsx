/**
 * Player Stats Page - /players/[id]/stats
 *
 * Shows detailed statistics for a player using the usePlayerStatistics hook.
 * Provides per-game breakdowns of sessions played and average scores.
 *
 * @see Issue #4890
 */

'use client';

import { ArrowLeft, BarChart2, Gamepad2, Target, Trophy } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { usePlayerStatistics } from '@/hooks/queries/usePlayersFromRecords';

function StatCard({
  icon: Icon,
  label,
  value,
  color,
}: {
  icon: React.ElementType;
  label: string;
  value: string | number;
  color: string;
}) {
  return (
    <Card className={`border-l-4 ${color} shadow-sm`}>
      <CardContent className="pt-6 pb-4">
        <div className="flex items-center gap-3">
          <Icon className="h-5 w-5 text-muted-foreground shrink-0" />
          <div>
            <p className="text-sm text-muted-foreground font-nunito">{label}</p>
            <p className="text-2xl font-bold font-quicksand">{value}</p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default function PlayerStatsPage() {
  const params = useParams();
  const playerId = params?.id as string;
  const { data: stats, isLoading, error } = usePlayerStatistics();

  const playerName = decodeURIComponent(playerId).replace(/-/g, ' ');

  const totalGames = stats ? Object.keys(stats.gamePlayCounts).length : 0;
  const winRate =
    stats && stats.totalSessions > 0
      ? Math.round((stats.totalWins / stats.totalSessions) * 100)
      : 0;

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href={`/players/${playerId}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to {playerName}
          </Link>
        </Button>

        {/* Header */}
        <div className="flex items-center gap-3 mb-8">
          <BarChart2 className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Statistics</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              Performance stats for {playerName}
            </p>
          </div>
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="space-y-4">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              {[...Array(4)].map((_, i) => (
                <Skeleton key={i} className="h-24 w-full rounded-xl" />
              ))}
            </div>
            <Skeleton className="h-64 w-full rounded-xl" />
          </div>
        )}

        {/* Error */}
        {error && (
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">
              Failed to load statistics.
            </AlertDescription>
          </Alert>
        )}

        {/* Stats Content */}
        {!isLoading && !error && stats && (
          <div className="space-y-6">
            {/* Summary cards */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <StatCard
                icon={Gamepad2}
                label="Total Sessions"
                value={stats.totalSessions}
                color="border-l-[hsl(262,83%,58%)]"
              />
              <StatCard
                icon={Trophy}
                label="Total Wins"
                value={stats.totalWins}
                color="border-l-amber-400"
              />
              <StatCard
                icon={Target}
                label="Win Rate"
                value={`${winRate}%`}
                color="border-l-green-400"
              />
              <StatCard
                icon={BarChart2}
                label="Unique Games"
                value={totalGames}
                color="border-l-blue-400"
              />
            </div>

            {/* Per-game breakdown */}
            <div className="grid md:grid-cols-2 gap-6">
              <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl">Sessions by Game</CardTitle>
                </CardHeader>
                <CardContent>
                  {Object.keys(stats.gamePlayCounts).length > 0 ? (
                    <ul className="space-y-2 font-nunito">
                      {Object.entries(stats.gamePlayCounts)
                        .sort(([, a], [, b]) => b - a)
                        .map(([game, count]) => (
                          <li key={game} className="flex justify-between text-sm items-center">
                            <span className="truncate mr-2">{game}</span>
                            <span className="text-muted-foreground shrink-0">
                              {count} session{count !== 1 ? 's' : ''}
                            </span>
                          </li>
                        ))}
                    </ul>
                  ) : (
                    <Alert>
                      <AlertDescription className="font-nunito">
                        No sessions recorded.
                      </AlertDescription>
                    </Alert>
                  )}
                </CardContent>
              </Card>

              <Card className="border-l-4 border-l-[hsl(240,60%,55%)] shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl">Average Scores</CardTitle>
                </CardHeader>
                <CardContent>
                  {Object.keys(stats.averageScoresByGame).length > 0 ? (
                    <ul className="space-y-2 font-nunito">
                      {Object.entries(stats.averageScoresByGame)
                        .sort(([, a], [, b]) => b - a)
                        .map(([game, avg]) => (
                          <li key={game} className="flex justify-between text-sm items-center">
                            <span className="truncate mr-2">{game}</span>
                            <span className="text-muted-foreground shrink-0">
                              {avg.toFixed(1)} pts
                            </span>
                          </li>
                        ))}
                    </ul>
                  ) : (
                    <Alert>
                      <AlertDescription className="font-nunito">
                        No score data recorded.
                      </AlertDescription>
                    </Alert>
                  )}
                </CardContent>
              </Card>
            </div>
          </div>
        )}

        {/* No data state */}
        {!isLoading && !error && !stats && (
          <Card>
            <CardContent className="py-12 text-center text-muted-foreground font-nunito">
              No statistics available for {playerName}.
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
