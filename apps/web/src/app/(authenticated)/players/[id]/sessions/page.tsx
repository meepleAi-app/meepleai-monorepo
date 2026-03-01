/**
 * Player Sessions Page - /players/[id]/sessions
 *
 * Shows game sessions involving this player.
 * Fetches session history and displays a filterable list.
 *
 * @see Issue #4890
 */

'use client';

import { useState, useEffect } from 'react';

import { ArrowLeft, Calendar, Clock, Trophy } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api, type GameSessionDto } from '@/lib/api';
import { cn } from '@/lib/utils';

const STATUS_COLORS: Record<string, string> = {
  Completed: 'bg-green-100 text-green-800 border-green-200',
  InProgress: 'bg-blue-100 text-blue-800 border-blue-200',
  Paused: 'bg-amber-100 text-amber-800 border-amber-200',
  Abandoned: 'bg-red-100 text-red-800 border-red-200',
  Setup: 'bg-gray-100 text-gray-800 border-gray-200',
};

function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes}m`;
  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(iso));
}

export default function PlayerSessionsPage() {
  const params = useParams();
  const playerId = params?.id as string;
  const playerName = decodeURIComponent(playerId).replace(/-/g, ' ');

  const [sessions, setSessions] = useState<GameSessionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!playerId) return;
    setLoading(true);
    api.sessions
      .getHistory({ limit: 50 })
      .then((res) => {
        // Filter sessions where this player participated
        const playerSessions = res.sessions.filter((s) =>
          s.players.some(
            (p) => p.playerName.toLowerCase() === playerName.toLowerCase()
          )
        );
        setSessions(playerSessions);
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Failed to load sessions');
      })
      .finally(() => setLoading(false));
  }, [playerId, playerName]);

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
          <Calendar className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Sessions</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              Game sessions involving {playerName}
            </p>
          </div>
          {!loading && sessions.length > 0 && (
            <Badge variant="secondary" className="ml-auto">
              {sessions.length}
            </Badge>
          )}
        </div>

        {/* Loading */}
        {loading && (
          <div className="space-y-4">
            {[...Array(4)].map((_, i) => (
              <Skeleton key={i} className="h-24 w-full rounded-xl" />
            ))}
          </div>
        )}

        {/* Error */}
        {error && !loading && (
          <Card>
            <CardContent className="py-8 text-center">
              <p className="text-destructive font-nunito">{error}</p>
            </CardContent>
          </Card>
        )}

        {/* Sessions List */}
        {!loading && !error && (
          <>
            {sessions.length === 0 ? (
              <Card>
                <CardContent className="py-12 text-center text-muted-foreground font-nunito">
                  No sessions found for {playerName}.
                </CardContent>
              </Card>
            ) : (
              <div className="space-y-3">
                {sessions.map((session) => {
                  const playerEntry = session.players.find(
                    (p) => p.playerName.toLowerCase() === playerName.toLowerCase()
                  );
                  const isWinner = session.winnerName?.toLowerCase() === playerName.toLowerCase();

                  return (
                    <Link key={session.id} href={`/sessions/${session.id}`}>
                      <Card className="hover:shadow-md transition-shadow cursor-pointer border border-border/60">
                        <CardHeader className="pb-2">
                          <div className="flex items-start justify-between gap-3">
                            <CardTitle className="font-quicksand text-base flex items-center gap-2">
                              Session
                              {isWinner && (
                                <Trophy className="h-4 w-4 text-amber-500" />
                              )}
                            </CardTitle>
                            <Badge
                              variant="outline"
                              className={cn(
                                'text-xs shrink-0',
                                STATUS_COLORS[session.status]
                              )}
                            >
                              {session.status}
                            </Badge>
                          </div>
                          <CardDescription className="font-nunito flex items-center gap-4 flex-wrap">
                            <span className="flex items-center gap-1">
                              <Calendar className="h-3 w-3" />
                              {formatDate(session.startedAt)}
                            </span>
                            <span className="flex items-center gap-1">
                              <Clock className="h-3 w-3" />
                              {formatDuration(session.durationMinutes)}
                            </span>
                            <span>{session.playerCount} player{session.playerCount !== 1 ? 's' : ''}</span>
                          </CardDescription>
                        </CardHeader>
                        {(session.winnerName ?? playerEntry?.color) && (
                          <CardContent className="pt-0 font-nunito text-sm text-muted-foreground">
                            {session.winnerName && (
                              <span>
                                Winner: <span className="font-medium text-foreground">{session.winnerName}</span>
                              </span>
                            )}
                          </CardContent>
                        )}
                      </Card>
                    </Link>
                  );
                })}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
