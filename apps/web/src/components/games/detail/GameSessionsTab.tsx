/**
 * Game Sessions Tab Component
 *
 * Displays active sessions, session history, and statistics
 */

import React from 'react';
import { Game, GameSessionDto } from '@/lib/api';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Separator } from '@/components/ui/separator';
import { Users, Clock, Calendar, Trophy, PlayCircle, AlertCircle } from 'lucide-react';

interface GameSessionsTabProps {
  game: Game;
  sessions: GameSessionDto[];
}

function getStatusBadge(status: string) {
  const statusLower = status.toLowerCase();
  switch (statusLower) {
    case 'inprogress':
    case 'in progress':
      return <Badge variant="default" className="bg-blue-600">In Progress</Badge>;
    case 'completed':
      return <Badge variant="default" className="bg-green-600">Completed</Badge>;
    case 'paused':
      return <Badge variant="secondary">Paused</Badge>;
    case 'abandoned':
      return <Badge variant="destructive">Abandoned</Badge>;
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

export function GameSessionsTab({ game, sessions }: GameSessionsTabProps) {
  const activeSessions = sessions.filter(s =>
    ['inprogress', 'in progress', 'paused', 'setup'].includes(s.status.toLowerCase())
  );
  const completedSessions = sessions.filter(s => s.status.toLowerCase() === 'completed');

  // Calculate statistics
  const totalSessions = sessions.length;
  const completedCount = completedSessions.length;
  const averageDuration = completedSessions.length > 0
    ? Math.round(completedSessions.reduce((sum, s) => sum + s.durationMinutes, 0) / completedSessions.length)
    : 0;

  return (
    <div className="space-y-6">
      {/* Statistics Card */}
      <Card>
        <CardHeader>
          <CardTitle>Session Statistics</CardTitle>
          <CardDescription>Overview of your play sessions</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="flex flex-col items-center p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
              <PlayCircle className="h-5 w-5 text-muted-foreground mb-2" />
              <div className="text-2xl font-bold">{totalSessions}</div>
              <div className="text-xs text-muted-foreground">Total Sessions</div>
            </div>

            <div className="flex flex-col items-center p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
              <Trophy className="h-5 w-5 text-green-500 mb-2" />
              <div className="text-2xl font-bold">{completedCount}</div>
              <div className="text-xs text-muted-foreground">Completed</div>
            </div>

            <div className="flex flex-col items-center p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
              <Clock className="h-5 w-5 text-blue-500 mb-2" />
              <div className="text-2xl font-bold">{activeSessions.length}</div>
              <div className="text-xs text-muted-foreground">Active</div>
            </div>

            <div className="flex flex-col items-center p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
              <Clock className="h-5 w-5 text-muted-foreground mb-2" />
              <div className="text-2xl font-bold">{averageDuration > 0 ? formatDuration(averageDuration) : '—'}</div>
              <div className="text-xs text-muted-foreground">Avg. Duration</div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Active Sessions Card */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>Active Sessions</span>
            <Badge variant="secondary">{activeSessions.length}</Badge>
          </CardTitle>
          <CardDescription>
            Currently in-progress or paused game sessions
          </CardDescription>
        </CardHeader>
        <CardContent>
          {activeSessions.length === 0 ? (
            <Alert>
              <PlayCircle className="h-4 w-4" />
              <AlertDescription>
                No active sessions. Start a new session to begin tracking your gameplay!
              </AlertDescription>
            </Alert>
          ) : (
            <div className="space-y-3">
              {activeSessions.map((session) => (
                <div
                  key={session.id}
                  className="p-4 border border-slate-200 dark:border-slate-700 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors"
                >
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-2">
                      {getStatusBadge(session.status)}
                      <span className="text-sm text-muted-foreground">
                        Started {new Date(session.startedAt).toLocaleDateString()}
                      </span>
                    </div>
                    <Button size="sm" variant="outline" disabled>
                      View Details
                    </Button>
                  </div>

                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <div className="flex items-center gap-2">
                      <Users className="h-4 w-4 text-muted-foreground" />
                      <span>{session.playerCount} players</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Clock className="h-4 w-4 text-muted-foreground" />
                      <span>{formatDuration(session.durationMinutes)}</span>
                    </div>
                  </div>

                  {session.players.length > 0 && (
                    <div className="mt-3">
                      <div className="text-xs text-muted-foreground mb-2">Players:</div>
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
          )}
        </CardContent>
      </Card>

      {/* Session History Card */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>Session History</span>
            <Badge variant="secondary">{completedSessions.length} completed</Badge>
          </CardTitle>
          <CardDescription>
            Past gameplay sessions
          </CardDescription>
        </CardHeader>
        <CardContent>
          {completedSessions.length === 0 ? (
            <Alert>
              <Calendar className="h-4 w-4" />
              <AlertDescription>
                No completed sessions yet. Complete a session to see it here!
              </AlertDescription>
            </Alert>
          ) : (
            <div className="space-y-3">
              {completedSessions.slice(0, 10).map((session) => (
                <div
                  key={session.id}
                  className="p-4 border border-slate-200 dark:border-slate-700 rounded-lg"
                >
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-2">
                      {getStatusBadge(session.status)}
                      <span className="text-sm text-muted-foreground">
                        {new Date(session.completedAt || session.startedAt).toLocaleDateString()}
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
                      <span>{session.playerCount} players</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Clock className="h-4 w-4 text-muted-foreground" />
                      <span>{formatDuration(session.durationMinutes)}</span>
                    </div>
                  </div>

                  {session.players.length > 0 && (
                    <div className="mt-3">
                      <div className="text-xs text-muted-foreground mb-2">Players:</div>
                      <div className="flex flex-wrap gap-2">
                        {session.players.map((player, idx) => (
                          <Badge
                            key={idx}
                            variant={player.playerName === session.winnerName ? "default" : "outline"}
                          >
                            {player.playerName}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  )}

                  {session.notes && (
                    <div className="mt-3 text-sm text-muted-foreground">
                      <span className="font-medium">Notes:</span> {session.notes}
                    </div>
                  )}
                </div>
              ))}

              {completedSessions.length > 10 && (
                <div className="text-center">
                  <Button variant="outline" size="sm" disabled>
                    Load More ({completedSessions.length - 10} more sessions)
                  </Button>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Start New Session Button */}
      <Card>
        <CardHeader>
          <CardTitle>Start New Session</CardTitle>
          <CardDescription>
            Track a new gameplay session for this game
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Session tracking functionality coming soon.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    </div>
  );
}
