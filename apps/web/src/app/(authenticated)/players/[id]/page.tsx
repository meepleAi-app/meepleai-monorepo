/**
 * Player Detail Page - /players/[id]
 *
 * Shows player details with navigation footer linking to Sessions and Games.
 * Uses MeepleCard entity=player with hero variant.
 *
 * @see Issue #4693
 */

'use client';

import { use } from 'react';

import { ArrowLeft, Gamepad2, Trophy } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useEntityNavigation } from '@/hooks/useEntityNavigation';
import { usePlayerStatistics } from '@/hooks/queries/usePlayersFromRecords';

export default function PlayerDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id: playerId } = use(params);
  const { data: stats, isLoading } = usePlayerStatistics();
  const navigationLinks = useEntityNavigation('player', { id: playerId });

  // Decode player name from URL slug
  const playerName = decodeURIComponent(playerId).replace(/-/g, ' ');

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-7xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href="/players">
            <ArrowLeft className="mr-2 h-4 w-4" /> Torna ai Giocatori
          </Link>
        </Button>

        {/* Hero Card */}
        <section className="mb-8 flex justify-center">
          <MeepleCard
            entity="player"
            variant="hero"
            title={playerName}
            subtitle="Giocatore"
            metadata={
              stats
                ? [
                    { icon: Gamepad2, value: `${stats.totalSessions} sessioni` },
                    { icon: Trophy, value: `${stats.totalWins} vittorie` },
                  ]
                : []
            }
            loading={isLoading}
            navigateTo={navigationLinks}
          />
        </section>

        {/* Stats Cards */}
        {stats && (
          <div className="grid md:grid-cols-2 gap-6 max-w-4xl mx-auto">
            <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-lg">
              <CardHeader>
                <CardTitle className="font-quicksand text-xl">
                  Giochi Preferiti
                </CardTitle>
              </CardHeader>
              <CardContent>
                {Object.keys(stats.gamePlayCounts).length > 0 ? (
                  <ul className="space-y-2 font-nunito">
                    {Object.entries(stats.gamePlayCounts)
                      .sort(([, a], [, b]) => b - a)
                      .slice(0, 10)
                      .map(([game, count]) => (
                        <li
                          key={game}
                          className="flex justify-between text-sm"
                        >
                          <span>{game}</span>
                          <span className="text-muted-foreground">
                            {count} partite
                          </span>
                        </li>
                      ))}
                  </ul>
                ) : (
                  <Alert>
                    <AlertDescription className="font-nunito">
                      Nessuna partita registrata.
                    </AlertDescription>
                  </Alert>
                )}
              </CardContent>
            </Card>

            <Card className="border-l-4 border-l-[hsl(240,60%,55%)] shadow-lg">
              <CardHeader>
                <CardTitle className="font-quicksand text-xl">
                  Punteggi Medi
                </CardTitle>
              </CardHeader>
              <CardContent>
                {Object.keys(stats.averageScoresByGame).length > 0 ? (
                  <ul className="space-y-2 font-nunito">
                    {Object.entries(stats.averageScoresByGame)
                      .sort(([, a], [, b]) => b - a)
                      .slice(0, 10)
                      .map(([game, avg]) => (
                        <li
                          key={game}
                          className="flex justify-between text-sm"
                        >
                          <span>{game}</span>
                          <span className="text-muted-foreground">
                            {avg.toFixed(1)} pts
                          </span>
                        </li>
                      ))}
                  </ul>
                ) : (
                  <Alert>
                    <AlertDescription className="font-nunito">
                      Nessun punteggio registrato.
                    </AlertDescription>
                  </Alert>
                )}
              </CardContent>
            </Card>
          </div>
        )}
      </div>
    </div>
  );
}
