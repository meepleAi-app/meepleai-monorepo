/**
 * Session Players Page - /sessions/[id]/players
 *
 * Lists all players participating in a specific session.
 * Fetches session data via api.sessions.getById and renders player cards.
 *
 * @see Issue #4891
 */

'use client';

import { useState, useEffect } from 'react';

import { ArrowLeft, Users } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Avatar } from '@/components/ui/data-display/avatar';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api, type GameSessionDto } from '@/lib/api';

export default function SessionPlayersPage() {
  const params = useParams();
  const id = params?.id as string;
  const [session, setSession] = useState<GameSessionDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    api.sessions
      .getById(id)
      .then((data) => {
        setSession(data);
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Failed to load session');
      })
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <div className="container mx-auto py-8 px-4 max-w-4xl">
        <Skeleton className="h-8 w-48 mb-6" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {[...Array(4)].map((_, i) => (
            <Skeleton key={i} className="h-24 w-full rounded-xl" />
          ))}
        </div>
      </div>
    );
  }

  if (error || !session) {
    return (
      <div className="container mx-auto py-8 px-4 max-w-4xl">
        <p className="text-destructive mb-4">{error ?? 'Session not found'}</p>
        <Button asChild variant="ghost">
          <Link href={`/sessions/${id}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Session
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 px-4 max-w-4xl">
      {/* Header */}
      <div className="mb-6">
        <Button asChild variant="ghost" className="mb-4 font-nunito">
          <Link href={`/sessions/${id}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Session
          </Link>
        </Button>
        <div className="flex items-center gap-3">
          <Users className="h-6 w-6 text-primary" />
          <h1 className="text-3xl font-bold font-quicksand">Players</h1>
          <Badge variant="secondary">{session.playerCount}</Badge>
        </div>
        {session.winnerName && (
          <p className="mt-2 text-sm text-muted-foreground font-nunito">
            Winner: <span className="font-semibold text-foreground">{session.winnerName}</span>
          </p>
        )}
      </div>

      {/* Players Grid */}
      {session.players.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center text-muted-foreground font-nunito">
            No players recorded for this session.
          </CardContent>
        </Card>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {session.players.map((player, index) => {
            const isWinner = player.playerName === session.winnerName;
            return (
              <Card
                key={index}
                className="border border-border/60 shadow-sm hover:shadow-md transition-shadow"
              >
                <CardHeader className="pb-3">
                  <CardTitle className="flex items-center gap-3 font-quicksand">
                    <Avatar
                      className="h-10 w-10 flex items-center justify-center font-bold shrink-0"
                      style={{ backgroundColor: player.color ?? '#6366f1' }}
                      aria-label={`${player.playerName} avatar`}
                    >
                      <span className="text-white text-sm">
                        {player.playerName.charAt(0).toUpperCase()}
                      </span>
                    </Avatar>
                    <span className="truncate">{player.playerName}</span>
                    {isWinner && (
                      <Badge className="ml-auto shrink-0 bg-amber-100 text-amber-900 border-amber-300">
                        Winner
                      </Badge>
                    )}
                  </CardTitle>
                </CardHeader>
                <CardContent className="pt-0 font-nunito text-sm text-muted-foreground space-y-1">
                  <p>Play order: #{player.playerOrder}</p>
                  {player.color && (
                    <div className="flex items-center gap-2">
                      <span
                        className="inline-block w-3 h-3 rounded-full border border-border"
                        style={{ backgroundColor: player.color }}
                        aria-hidden="true"
                      />
                      <span>{player.color}</span>
                    </div>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
