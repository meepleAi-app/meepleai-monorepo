/**
 * Game Sessions Tab Component
 *
 * Displays session history and statistics for a game.
 * Receives session data as props from parent (game detail page fetches via useGameSessions).
 *
 * Issue M4: Show session history in game detail
 */

'use client';

import React from 'react';

import { Users, Clock, Calendar, Trophy, PlayCircle } from 'lucide-react';

import { EmptyStateCard } from '@/components/features/common/EmptyStateCard';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

// ============================================================================
// Types
// ============================================================================

interface GameSessionsTabProps {
  gameId: string;
  sessions: GameSessionDto[];
  isLoading?: boolean;
}

// ============================================================================
// Helpers
// ============================================================================

function getStatusBadge(status: string) {
  const statusLower = status.toLowerCase();
  switch (statusLower) {
    case 'inprogress':
    case 'in progress':
      return (
        <Badge variant="default" className="bg-blue-600">
          In Corso
        </Badge>
      );
    case 'completed':
      return (
        <Badge variant="default" className="bg-green-600">
          Completata
        </Badge>
      );
    case 'paused':
      return <Badge variant="secondary">In Pausa</Badge>;
    case 'abandoned':
      return <Badge variant="destructive">Abbandonata</Badge>;
    case 'setup':
      return <Badge variant="outline">Setup</Badge>;
    default:
      return <Badge variant="outline">{status}</Badge>;
  }
}

function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
}

// ============================================================================
// Component
// ============================================================================

export function GameSessionsTab({
  gameId: _gameId,
  sessions = [],
  isLoading = false,
}: GameSessionsTabProps) {
  if (isLoading) {
    return (
      <div className="space-y-6">
        <Card>
          <CardHeader>
            <Skeleton className="h-6 w-48" />
            <Skeleton className="h-4 w-64 mt-2" />
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              {Array.from({ length: 4 }).map((_, i) => (
                <Skeleton key={i} className="h-24 w-full rounded-lg" />
              ))}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <Skeleton className="h-6 w-48" />
          </CardHeader>
          <CardContent className="space-y-3">
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-20 w-full" />
          </CardContent>
        </Card>
      </div>
    );
  }

  if (sessions.length === 0) {
    return (
      <EmptyStateCard
        title="Nessuna partita"
        description="Nessuna partita registrata per questo gioco"
        ctaLabel="Inizia una Partita"
        onCtaClick={() => {
          // Navigate to play tab or session creation
          window.location.href = '/play';
        }}
        icon={PlayCircle}
        entityColor="220 70% 50%" // blue for sessions
      />
    );
  }

  const activeSessions = sessions.filter(s =>
    ['inprogress', 'in progress', 'paused', 'setup'].includes(s.status.toLowerCase())
  );
  const completedSessions = sessions.filter(s => s.status.toLowerCase() === 'completed');

  // Statistics
  const totalSessions = sessions.length;
  const completedCount = completedSessions.length;
  const averageDuration =
    completedSessions.length > 0
      ? Math.round(
          completedSessions.reduce((sum, s) => sum + s.durationMinutes, 0) /
            completedSessions.length
        )
      : 0;

  return (
    <div className="space-y-6">
      {/* Statistics Card */}
      <Card>
        <CardHeader>
          <CardTitle>Statistiche Partite</CardTitle>
          <CardDescription>Panoramica delle tue sessioni di gioco</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="flex flex-col items-center p-4 bg-muted dark:bg-card rounded-lg">
              <PlayCircle className="h-5 w-5 text-muted-foreground mb-2" />
              <div className="text-2xl font-bold">{totalSessions}</div>
              <div className="text-xs text-muted-foreground">Totale</div>
            </div>

            <div className="flex flex-col items-center p-4 bg-muted dark:bg-card rounded-lg">
              <Trophy className="h-5 w-5 text-green-500 mb-2" />
              <div className="text-2xl font-bold">{completedCount}</div>
              <div className="text-xs text-muted-foreground">Completate</div>
            </div>

            <div className="flex flex-col items-center p-4 bg-muted dark:bg-card rounded-lg">
              <Clock className="h-5 w-5 text-blue-500 mb-2" />
              <div className="text-2xl font-bold">{activeSessions.length}</div>
              <div className="text-xs text-muted-foreground">Attive</div>
            </div>

            <div className="flex flex-col items-center p-4 bg-muted dark:bg-card rounded-lg">
              <Clock className="h-5 w-5 text-muted-foreground mb-2" />
              <div className="text-2xl font-bold">
                {averageDuration > 0 ? formatDuration(averageDuration) : '\u2014'}
              </div>
              <div className="text-xs text-muted-foreground">Durata Media</div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Active Sessions */}
      {activeSessions.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              <span>Partite Attive</span>
              <Badge variant="secondary">{activeSessions.length}</Badge>
            </CardTitle>
            <CardDescription>Sessioni in corso o in pausa</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {activeSessions.map(session => (
                <div
                  key={session.id}
                  className="p-4 border border-border/50 dark:border-border/30 rounded-lg hover:bg-muted/50 dark:hover:bg-muted/30 transition-colors"
                >
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-2">
                      {getStatusBadge(session.status)}
                      <span className="text-sm text-muted-foreground">
                        Iniziata il {new Date(session.startedAt).toLocaleDateString('it-IT')}
                      </span>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <div className="flex items-center gap-2">
                      <Users className="h-4 w-4 text-muted-foreground" />
                      <span>{session.playerCount} giocatori</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Clock className="h-4 w-4 text-muted-foreground" />
                      <span>{formatDuration(session.durationMinutes)}</span>
                    </div>
                  </div>

                  {session.players.length > 0 && (
                    <div className="mt-3">
                      <div className="text-xs text-muted-foreground mb-2">Giocatori:</div>
                      <div className="flex flex-wrap gap-2">
                        {session.players.map((player, idx) => (
                          <Badge key={idx} variant="outline">
                            {player.playerName}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Session History */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>Storico Partite</span>
            <Badge variant="secondary">{completedSessions.length} completate</Badge>
          </CardTitle>
          <CardDescription>Partite concluse</CardDescription>
        </CardHeader>
        <CardContent>
          {completedSessions.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-8 text-center">
              <Calendar className="h-8 w-8 text-muted-foreground/50 mb-3" />
              <p className="text-sm text-muted-foreground">
                Nessuna partita completata. Concludi una sessione per vederla qui!
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              {completedSessions.slice(0, 10).map(session => (
                <div
                  key={session.id}
                  className="p-4 border border-border/50 dark:border-border/30 rounded-lg"
                >
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-2">
                      {getStatusBadge(session.status)}
                      <span className="text-sm text-muted-foreground">
                        {new Date(session.completedAt || session.startedAt).toLocaleDateString(
                          'it-IT'
                        )}
                      </span>
                    </div>
                    {session.winnerName && (
                      <div className="flex items-center gap-2 text-sm">
                        <Trophy className="h-4 w-4 text-yellow-500" />
                        <span className="font-medium">{session.winnerName}</span>
                      </div>
                    )}
                  </div>

                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <div className="flex items-center gap-2">
                      <Users className="h-4 w-4 text-muted-foreground" />
                      <span>{session.playerCount} giocatori</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Clock className="h-4 w-4 text-muted-foreground" />
                      <span>{formatDuration(session.durationMinutes)}</span>
                    </div>
                  </div>

                  {session.players.length > 0 && (
                    <div className="mt-3">
                      <div className="text-xs text-muted-foreground mb-2">Giocatori:</div>
                      <div className="flex flex-wrap gap-2">
                        {session.players.map((player, idx) => (
                          <Badge
                            key={idx}
                            variant={
                              player.playerName === session.winnerName ? 'default' : 'outline'
                            }
                          >
                            {player.playerName}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  )}

                  {session.notes && (
                    <div className="mt-3 text-sm text-muted-foreground">
                      <span className="font-medium">Note:</span> {session.notes}
                    </div>
                  )}
                </div>
              ))}

              {completedSessions.length > 10 && (
                <div className="text-center pt-2">
                  <p className="text-sm text-muted-foreground">
                    e altre {completedSessions.length - 10} partite
                  </p>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
