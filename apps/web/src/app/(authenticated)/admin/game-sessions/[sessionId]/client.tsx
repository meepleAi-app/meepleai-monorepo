/**
 * Game Session Detail Client - Issue #3948
 *
 * Displays detailed information about a specific game session:
 * - Session metadata (game, date, duration, status)
 * - Player list with scores
 * - Session timeline/notes
 * - Actions: view game, view players
 */

'use client';

import { ArrowLeft, PlayCircle, Calendar, Clock, Users, MapPin, FileText } from 'lucide-react';
import Link from 'next/link';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { usePlayRecord } from '@/lib/hooks/use-play-records';

interface GameSessionDetailClientProps {
  sessionId: string;
}

// Format ISO 8601 duration
function formatDuration(isoDuration: string | null): string {
  if (!isoDuration) return 'N/A';
  // eslint-disable-next-line security/detect-unsafe-regex
  const match = isoDuration.match(/PT(?:(\d+)H)?(?:(\d+)M)?/);
  if (!match) return isoDuration;
  const hours = match[1] ? `${match[1]}h` : '';
  const minutes = match[2] ? `${match[2]}m` : '';
  return [hours, minutes].filter(Boolean).join(' ') || '0m';
}

export function GameSessionDetailClient({ sessionId }: GameSessionDetailClientProps) {
  const { user, loading: authLoading } = useAuthUser();
  const { data: playRecord, isLoading, error } = usePlayRecord(sessionId);

  if (isLoading) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="flex justify-center items-center min-h-[400px]">
          <Spinner size="lg" />
        </div>
      </AdminAuthGuard>
    );
  }

  if (error || !playRecord) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="container mx-auto p-6 max-w-4xl">
          <Alert variant="destructive">
            <AlertDescription>Failed to load session details. Session may not exist.</AlertDescription>
          </Alert>
          <Link href="/admin/game-sessions">
            <Button variant="outline" className="mt-4">
              <ArrowLeft className="h-4 w-4 mr-2" />
              Back to Sessions
            </Button>
          </Link>
        </div>
      </AdminAuthGuard>
    );
  }

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-4xl space-y-6">
        {/* Header */}
        <div className="flex items-center gap-4">
          <Link href="/admin/game-sessions">
            <Button variant="ghost" size="sm">
              <ArrowLeft className="h-4 w-4 mr-1" />
              Back
            </Button>
          </Link>
          <div className="flex-1">
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <PlayCircle className="h-6 w-6" />
              {playRecord.gameName}
            </h1>
            <p className="text-muted-foreground">Session {sessionId.slice(0, 8)}...</p>
          </div>
          <Badge variant={playRecord.status === 'InProgress' ? 'default' : 'secondary'}>
            {playRecord.status}
          </Badge>
        </div>

        {/* Session Info Card */}
        <Card>
          <CardHeader>
            <CardTitle>Session Information</CardTitle>
          </CardHeader>
          <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="flex items-center gap-3">
              <Calendar className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="text-sm font-medium">Date</p>
                <p className="text-sm text-muted-foreground">
                  {new Date(playRecord.sessionDate).toLocaleDateString('en-US', {
                    weekday: 'short',
                    month: 'short',
                    day: 'numeric',
                    year: 'numeric',
                  })}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <Clock className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="text-sm font-medium">Duration</p>
                <p className="text-sm text-muted-foreground">{formatDuration(playRecord.duration)}</p>
              </div>
            </div>

            <div className="flex items-center gap-3">
              <Users className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="text-sm font-medium">Players</p>
                <p className="text-sm text-muted-foreground">{playRecord.players.length} players</p>
              </div>
            </div>

            {playRecord.location && (
              <div className="flex items-center gap-3">
                <MapPin className="h-5 w-5 text-muted-foreground" />
                <div>
                  <p className="text-sm font-medium">Location</p>
                  <p className="text-sm text-muted-foreground">{playRecord.location}</p>
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Players & Scores */}
        <Card>
          <CardHeader>
            <CardTitle>Players & Scores</CardTitle>
            <CardDescription>{playRecord.players.length} players participated</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Player</TableHead>
                  <TableHead>User ID</TableHead>
                  {playRecord.scoringConfig.enabledDimensions.map((dim: string) => (
                    <TableHead key={dim}>{dim}</TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {playRecord.players.map((player) => (
                  <TableRow key={player.id}>
                    <TableCell className="font-medium">{player.displayName}</TableCell>
                    <TableCell>
                      {player.userId ? (
                        <Link
                          href={`/admin/users/${player.userId}`}
                          className="text-primary hover:underline"
                        >
                          {player.userId.slice(0, 8)}...
                        </Link>
                      ) : (
                        <span className="text-muted-foreground">Guest</span>
                      )}
                    </TableCell>
                    {playRecord.scoringConfig.enabledDimensions.map((dim: string) => {
                      const score = player.scores.find((s) => s.dimension === dim);
                      return (
                        <TableCell key={dim}>
                          {score ? (
                            <>
                              {score.value}
                              {score.unit && <span className="text-muted-foreground ml-1">{score.unit}</span>}
                            </>
                          ) : (
                            <span className="text-muted-foreground">-</span>
                          )}
                        </TableCell>
                      );
                    })}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        {/* Notes */}
        {playRecord.notes && (
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <FileText className="h-5 w-5" />
                Notes
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm whitespace-pre-wrap">{playRecord.notes}</p>
            </CardContent>
          </Card>
        )}

        {/* Timeline */}
        <Card>
          <CardHeader>
            <CardTitle>Session Timeline</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex items-center gap-3 text-sm">
              <Badge variant="outline">Created</Badge>
              <span className="text-muted-foreground">
                {new Date(playRecord.createdAt).toLocaleString('en-US')}
              </span>
            </div>

            {playRecord.startTime && (
              <div className="flex items-center gap-3 text-sm">
                <Badge variant="default">Started</Badge>
                <span className="text-muted-foreground">
                  {new Date(playRecord.startTime).toLocaleString('en-US')}
                </span>
              </div>
            )}

            {playRecord.endTime && (
              <div className="flex items-center gap-3 text-sm">
                <Badge variant="secondary">Completed</Badge>
                <span className="text-muted-foreground">
                  {new Date(playRecord.endTime).toLocaleString('en-US')}
                </span>
              </div>
            )}

            {playRecord.updatedAt && playRecord.updatedAt !== playRecord.createdAt && (
              <div className="flex items-center gap-3 text-sm">
                <Badge variant="outline">Last Updated</Badge>
                <span className="text-muted-foreground">
                  {new Date(playRecord.updatedAt).toLocaleString('en-US')}
                </span>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </AdminAuthGuard>
  );
}
