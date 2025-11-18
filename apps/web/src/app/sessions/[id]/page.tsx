/**
 * SPRINT-4: Session Details Page (Issue #1134)
 *
 * Displays full details of a game session with timeline and actions.
 * Features:
 * - Full session information
 * - Player list with colors
 * - Timeline: start → pauses → end
 * - Game information
 * - Actions (if active): Pause/Resume/End
 * - Back navigation
 * - WCAG 2.1 AA accessibility compliance
 */

'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { api, GameSessionDto, Game } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Separator } from '@/components/ui/separator';
import { Avatar } from '@/components/ui/avatar';
import { ErrorDisplay } from '@/components/ErrorDisplay';
import { categorizeError } from '@/lib/errorUtils';

/**
 * Session status badge component
 */
function SessionStatusBadge({ status }: { status: string }) {
  const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
    InProgress: 'default',
    Paused: 'secondary',
    Completed: 'default',
    Abandoned: 'destructive',
    Setup: 'outline'
  };

  return (
    <Badge variant={variants[status] || 'outline'} aria-label={`Session status: ${status}`}>
      {status}
    </Badge>
  );
}

/**
 * Player list component
 */
function PlayerList({ players }: { players: GameSessionDto['players'] }) {
  if (!players || players.length === 0) {
    return <p className="text-muted-foreground">No players recorded</p>;
  }

  return (
    <div className="space-y-3">
      {players.map((player, index) => (
        <div key={index} className="flex items-center gap-3">
          <Avatar
            className="h-10 w-10 flex items-center justify-center font-semibold"
            style={{ backgroundColor: player.color || '#666' }}
            aria-label={`Player ${player.playerName} color: ${player.color || 'default'}`}
          >
            <span className="text-white text-sm">{player.playerName.charAt(0).toUpperCase()}</span>
          </Avatar>
          <div>
            <p className="font-medium">{player.playerName}</p>
            <p className="text-sm text-muted-foreground">
              Player {player.playerOrder}
              {player.color && (
                <span className="ml-2">• Color: {player.color}</span>
              )}
            </p>
          </div>
        </div>
      ))}
    </div>
  );
}

/**
 * Session timeline component
 */
function SessionTimeline({ session }: { session: GameSessionDto }) {
  const events = [
    {
      label: 'Session Started',
      timestamp: session.startedAt,
      icon: '▶️'
    }
  ];

  if (session.status === 'Paused') {
    events.push({
      label: 'Session Paused',
      timestamp: new Date().toISOString(), // Would come from backend in real implementation
      icon: '⏸️'
    });
  }

  if (session.completedAt) {
    events.push({
      label: session.status === 'Completed' ? 'Session Completed' : 'Session Abandoned',
      timestamp: session.completedAt,
      icon: session.status === 'Completed' ? '✅' : '❌'
    });
  }

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    }).format(date);
  };

  return (
    <div className="space-y-4">
      {events.map((event, index) => (
        <div key={index} className="flex gap-4">
          <div className="flex flex-col items-center">
            <div className="text-2xl" role="img" aria-label={event.label}>
              {event.icon}
            </div>
            {index < events.length - 1 && (
              <div className="w-0.5 h-12 bg-muted" aria-hidden="true" />
            )}
          </div>
          <div className="flex-1 pb-4">
            <p className="font-medium">{event.label}</p>
            <p className="text-sm text-muted-foreground">
              {formatTimestamp(event.timestamp)}
            </p>
          </div>
        </div>
      ))}
    </div>
  );
}

/**
 * Session Details Page
 */
export default function SessionDetailsPage() {
  const router = useRouter();
  const params = useParams();
  const id = params.id as string;
  const [session, setSession] = useState<GameSessionDto | null>(null);
  const [game, setGame] = useState<Game | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  /**
   * Fetch session details from API
   */
  const fetchSession = async () => {
    if (!id || typeof id !== 'string') return;

    try {
      setLoading(true);
      setError(null);

      const sessionData = await api.sessions.getById(id);
      if (!sessionData) {
        setError('Session not found');
        return;
      }

      setSession(sessionData);

      // Fetch game details
      const gameData = await api.games.getById(sessionData.gameId);
      setGame(gameData);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load session details');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Initialize component
   */
  useEffect(() => {
    if (id) {
      fetchSession();
    }
  }, [id]);

  /**
   * Handle pause session action
   */
  const handlePause = async () => {
    if (!session) return;

    try {
      setActionLoading(true);
      await api.sessions.pause(session.id);
      await fetchSession(); // Refresh session data
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to pause session');
    } finally {
      setActionLoading(false);
    }
  };

  /**
   * Handle resume session action
   */
  const handleResume = async () => {
    if (!session) return;

    try {
      setActionLoading(true);
      await api.sessions.resume(session.id);
      await fetchSession(); // Refresh session data
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to resume session');
    } finally {
      setActionLoading(false);
    }
  };

  /**
   * Handle end session action
   */
  const handleEnd = async () => {
    if (!session) return;

    if (!confirm('Are you sure you want to end this session? This action cannot be undone.')) {
      return;
    }

    try {
      setActionLoading(true);
      await api.sessions.end(session.id);
      router.push('/sessions');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to end session');
      setActionLoading(false);
    }
  };

  /**
   * Format duration for display
   */
  const formatDuration = (minutes: number): string => {
    if (minutes < 60) return `${minutes} minutes`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours} hour${hours !== 1 ? 's' : ''} ${mins} minute${mins !== 1 ? 's' : ''}`;
  };

  // Loading state
  if (loading) {
    return (
      <div className="container mx-auto py-8 px-4">
        <Skeleton className="h-8 w-64 mb-4" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  // Error state
  if (error && !session) {
    return (
      <div className="container mx-auto py-8 px-4">
        <ErrorDisplay
          error={categorizeError(new Error(error))}
          onRetry={fetchSession}
          onDismiss={() => router.push('/sessions')}
          showTechnicalDetails={process.env.NODE_ENV === 'development'}
        />
      </div>
    );
  }

  // No session found
  if (!session) {
    return (
      <div className="container mx-auto py-8 px-4">
        <p className="text-muted-foreground mb-4">Session not found</p>
        <Button onClick={() => router.push('/sessions')}>
          Back to Sessions
        </Button>
      </div>
    );
  }

  const isActive = session.status === 'InProgress' || session.status === 'Paused';
  const canPause = session.status === 'InProgress';
  const canResume = session.status === 'Paused';

  return (
    <div className="container mx-auto py-8 px-4">
      {/* Header */}
      <div className="mb-6">
        <Link href="/sessions" className="text-sm text-muted-foreground hover:text-foreground mb-2 inline-block">
          ← Back to Sessions
        </Link>
        <div className="flex justify-between items-start">
          <div>
            <h1 className="text-3xl font-bold mb-2">Session Details</h1>
            <div className="flex gap-2 items-center">
              <SessionStatusBadge status={session.status} />
              {session.winnerName && (
                <Badge variant="outline">Winner: {session.winnerName}</Badge>
              )}
            </div>
          </div>

          {/* Action buttons */}
          {isActive && (
            <div className="flex gap-2">
              {canPause && (
                <Button
                  variant="outline"
                  onClick={handlePause}
                  disabled={actionLoading}
                  aria-label="Pause session"
                >
                  Pause
                </Button>
              )}
              {canResume && (
                <Button
                  variant="outline"
                  onClick={handleResume}
                  disabled={actionLoading}
                  aria-label="Resume session"
                >
                  Resume
                </Button>
              )}
              <Button
                variant="destructive"
                onClick={handleEnd}
                disabled={actionLoading}
                aria-label="End session"
              >
                End Session
              </Button>
            </div>
          )}
        </div>
      </div>

      {/* Error Display */}
      {error && (
        <ErrorDisplay
          error={categorizeError(new Error(error))}
          onRetry={fetchSession}
          showTechnicalDetails={process.env.NODE_ENV === 'development'}
        />
      )}

      {/* Content Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Game Information */}
        <Card>
          <CardHeader>
            <CardTitle>Game Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <p className="text-sm text-muted-foreground">Game Title</p>
              <p className="font-medium text-lg">{game?.title || 'Unknown Game'}</p>
            </div>
            {game?.publisher && (
              <div>
                <p className="text-sm text-muted-foreground">Publisher</p>
                <p className="font-medium">{game.publisher}</p>
              </div>
            )}
            {game?.yearPublished && (
              <div>
                <p className="text-sm text-muted-foreground">Year Published</p>
                <p className="font-medium">{game.yearPublished}</p>
              </div>
            )}
            <Separator />
            <div>
              <p className="text-sm text-muted-foreground">Duration</p>
              <p className="font-medium">{formatDuration(session.durationMinutes)}</p>
            </div>
            {session.notes && (
              <div>
                <p className="text-sm text-muted-foreground">Notes</p>
                <p className="font-medium">{session.notes}</p>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Players */}
        <Card>
          <CardHeader>
            <CardTitle>Players ({session.playerCount})</CardTitle>
          </CardHeader>
          <CardContent>
            <PlayerList players={session.players} />
          </CardContent>
        </Card>

        {/* Timeline */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Session Timeline</CardTitle>
            <CardDescription>
              Track the progression of this game session
            </CardDescription>
          </CardHeader>
          <CardContent>
            <SessionTimeline session={session} />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
