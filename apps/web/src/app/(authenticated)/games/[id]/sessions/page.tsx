/**
 * Game Sessions Page - /games/[id]/sessions
 *
 * Shows all play sessions for a game using api.games.getSessions(id).
 * Displays date, players, duration, winner per session.
 *
 * @see Issue #4889
 */

'use client';

import { useEffect, useState } from 'react';

import { ArrowLeft, Clock, Gamepad2, Trophy, Users } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

function SessionRow({ session }: { session: GameSessionDto }) {
  const date = new Date(session.startedAt).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });

  return (
    <li className="flex flex-col sm:flex-row sm:items-center gap-3 p-4 rounded-lg hover:bg-muted/40 transition-colors border border-border/50">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 mb-1">
          <span className="text-sm font-medium font-quicksand">{date}</span>
          <Badge
            variant={session.status === 'Completed' ? 'default' : 'secondary'}
            className="text-xs font-nunito"
          >
            {session.status}
          </Badge>
        </div>
        <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground font-nunito">
          <span className="flex items-center gap-1">
            <Users className="h-3 w-3" />
            {session.playerCount} player{session.playerCount !== 1 ? 's' : ''}
          </span>
          {session.durationMinutes > 0 && (
            <span className="flex items-center gap-1">
              <Clock className="h-3 w-3" />
              {session.durationMinutes} min
            </span>
          )}
          {session.winnerName && (
            <span className="flex items-center gap-1 text-amber-600">
              <Trophy className="h-3 w-3" />
              {session.winnerName}
            </span>
          )}
        </div>
      </div>
      <Button asChild variant="ghost" size="sm" className="font-nunito shrink-0">
        <Link href={`/sessions/${session.id}`}>View</Link>
      </Button>
    </li>
  );
}

export default function GameSessionsPage() {
  const params = useParams();
  const gameId = params?.id as string;

  const [sessions, setSessions] = useState<GameSessionDto[] | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    if (!gameId) return;
    setIsLoading(true);
    api.games
      .getSessions(gameId)
      .then(data => setSessions(data))
      .catch(err => setError(err instanceof Error ? err : new Error(String(err))))
      .finally(() => setIsLoading(false));
  }, [gameId]);

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href={`/library/games/${gameId}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Game
          </Link>
        </Button>

        {/* Header */}
        <div className="flex items-center gap-3 mb-8">
          <Gamepad2 className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Sessions</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              All play sessions for this game
            </p>
          </div>
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <Skeleton key={i} className="h-20 w-full rounded-xl" />
            ))}
          </div>
        )}

        {/* Error */}
        {error && (
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">
              Failed to load sessions: {error.message}
            </AlertDescription>
          </Alert>
        )}

        {/* Content */}
        {!isLoading && !error && sessions !== null && (
          <>
            {sessions.length === 0 ? (
              <Card>
                <CardContent className="py-12 text-center text-muted-foreground font-nunito">
                  No sessions recorded for this game yet.
                </CardContent>
              </Card>
            ) : (
              <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl">
                    {sessions.length} Session{sessions.length !== 1 ? 's' : ''}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-2" role="list">
                    {sessions.map(session => (
                      <SessionRow key={session.id} session={session} />
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
