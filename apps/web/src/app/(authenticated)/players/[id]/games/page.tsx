/**
 * Player Games Page - /players/[id]/games
 *
 * Shows the games this player has participated in, derived from player statistics.
 * Uses usePlayerStatistics hook which provides game play counts per game.
 *
 * @see Issue #4890
 */

'use client';

import { ArrowLeft, Gamepad2, Trophy } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { usePlayerStatistics } from '@/hooks/queries/usePlayersFromRecords';

export default function PlayerGamesPage() {
  const params = useParams();
  const playerId = params?.id as string;
  const { data: stats, isLoading, error } = usePlayerStatistics();

  const playerName = decodeURIComponent(playerId).replace(/-/g, ' ');

  // Build sorted game list from play counts
  const games = stats
    ? Object.entries(stats.gamePlayCounts)
        .map(([name, count]) => ({
          name,
          count,
          avgScore: stats.averageScoresByGame[name] ?? null,
        }))
        .sort((a, b) => b.count - a.count)
    : [];

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
          <Gamepad2 className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Games Played</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              All games {playerName} has participated in
            </p>
          </div>
        </div>

        {/* Content */}
        {isLoading && (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <Skeleton key={i} className="h-16 w-full rounded-xl" />
            ))}
          </div>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">
              Failed to load game statistics.
            </AlertDescription>
          </Alert>
        )}

        {!isLoading && !error && (
          <>
            {games.length === 0 ? (
              <Card>
                <CardContent className="py-12 text-center text-muted-foreground font-nunito">
                  No games recorded yet.
                </CardContent>
              </Card>
            ) : (
              <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl flex items-center gap-2">
                    <Trophy className="h-5 w-5 text-primary" />
                    {games.length} Game{games.length !== 1 ? 's' : ''}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-3 font-nunito" role="list">
                    {games.map((game, index) => (
                      <li
                        key={game.name}
                        className="flex items-center gap-4 p-3 rounded-lg hover:bg-muted/40 transition-colors"
                      >
                        <span className="text-lg font-bold text-muted-foreground w-7 text-center shrink-0">
                          #{index + 1}
                        </span>
                        <div className="flex-1 min-w-0">
                          <p className="font-medium truncate">{game.name}</p>
                          <p className="text-xs text-muted-foreground">
                            {game.count} session{game.count !== 1 ? 's' : ''}
                            {game.avgScore !== null && (
                              <span> · avg {game.avgScore.toFixed(1)} pts</span>
                            )}
                          </p>
                        </div>
                        <div className="flex items-center gap-1 shrink-0 text-sm text-muted-foreground">
                          <Gamepad2 className="h-4 w-4" />
                          <span>{game.count}</span>
                        </div>
                      </li>
                    ))}
                  </ul>
                </CardContent>
              </Card>
            )}
          </>
        )}
      </div>
    </div>
  );
}
