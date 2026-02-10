/**
 * Play Record Details Page
 *
 * Displays full session details with players and scores.
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { ArrowLeft, Edit, Play, CheckCircle, Clock, MapPin, User } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { PlayerManager } from '@/components/play-records/PlayerManager';
import { ScoringInterface } from '@/components/play-records/ScoringInterface';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import {
  usePlayRecord,
  useAddPlayer,
  useRecordScore,
  useStartRecord,
  useCompleteRecord,
} from '@/lib/hooks/use-play-records';

export default function PlayRecordDetailsPage() {
  const params = useParams();
  const router = useRouter();

  const recordId = typeof params?.id === 'string' ? params.id : '';

  const { data: record, isLoading, error } = usePlayRecord(recordId);
  const addPlayer = useAddPlayer(recordId);
  const recordScore = useRecordScore(recordId);
  const startRecord = useStartRecord(recordId);
  const completeRecord = useCompleteRecord(recordId);

  const handleAddPlayer = async (player: { userId?: string; displayName: string }) => {
    try {
      await addPlayer.mutateAsync(player);
      toast.success('Player Added', { description: `${player.displayName} added to session` });
    } catch (error) {
      toast.error('Error', {
        description: error instanceof Error ? error.message : 'Failed to add player',
      });
    }
  };

  const handleRecordScore = async (playerId: string, dimension: string, value: number) => {
    await recordScore.mutateAsync({ playerId, dimension, value });
  };

  const handleStart = async () => {
    try {
      await startRecord.mutateAsync();
      toast.success('Session Started', { description: 'Session is now in progress' });
    } catch (error) {
      toast.error('Error', {
        description: error instanceof Error ? error.message : 'Failed to start session',
      });
    }
  };

  const handleComplete = async () => {
    try {
      await completeRecord.mutateAsync();
      toast.success('Session Completed', { description: 'Session has been marked as completed' });
    } catch (error) {
      toast.error('Error', {
        description: error instanceof Error ? error.message : 'Failed to complete session',
      });
    }
  };

  if (isLoading) {
    return (
      <div className="container mx-auto p-6">
        <div className="h-8 bg-muted animate-pulse rounded w-48 mb-6" />
        <div className="grid gap-4 md:grid-cols-2">
          <div className="h-64 bg-muted animate-pulse rounded" />
          <div className="h-64 bg-muted animate-pulse rounded" />
        </div>
      </div>
    );
  }

  if (error || !record) {
    return (
      <div className="container mx-auto p-6">
        <Alert variant="destructive">
          <AlertDescription>
            {error instanceof Error ? error.message : 'Play record not found'}
          </AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={() => router.push('/play-records')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back to History
        </Button>
      </div>
    );
  }

  const canEdit = record.status !== 'Archived';
  const canStart = record.status === 'Planned';
  const canComplete = record.status === 'InProgress';

  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => router.push('/play-records')}
            aria-label="Back to history"
          >
            <ArrowLeft className="w-5 h-5" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{record.gameName}</h1>
            <p className="text-muted-foreground mt-1">
              {new Date(record.sessionDate).toLocaleDateString('en-US', {
                weekday: 'long',
                month: 'long',
                day: 'numeric',
                year: 'numeric',
              })}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {canStart && (
            <Button onClick={handleStart} disabled={startRecord.isPending}>
              <Play className="w-4 h-4 mr-2" />
              Start Session
            </Button>
          )}
          {canComplete && (
            <Button onClick={handleComplete} disabled={completeRecord.isPending} variant="default">
              <CheckCircle className="w-4 h-4 mr-2" />
              Complete Session
            </Button>
          )}
          {canEdit && (
            <Button variant="outline" onClick={() => router.push(`/play-records/${recordId}/edit`)}>
              <Edit className="w-4 h-4 mr-2" />
              Edit
            </Button>
          )}
        </div>
      </div>

      {/* Session Info Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <Clock className="w-4 h-4" />
              Status
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Badge variant={record.status === 'Completed' ? 'default' : 'secondary'}>
              {record.status}
            </Badge>
            {record.duration && (
              <p className="text-sm text-muted-foreground mt-2">
                Duration: {record.duration}
              </p>
            )}
          </CardContent>
        </Card>

        {record.location && (
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium flex items-center gap-2">
                <MapPin className="w-4 h-4" />
                Location
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm">{record.location}</p>
            </CardContent>
          </Card>
        )}

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-medium flex items-center gap-2">
              <User className="w-4 h-4" />
              Players
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{record.players.length}</p>
          </CardContent>
        </Card>
      </div>

      {/* Notes */}
      {record.notes && (
        <Card>
          <CardHeader>
            <CardTitle>Notes</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm whitespace-pre-wrap">{record.notes}</p>
          </CardContent>
        </Card>
      )}

      {/* Players */}
      <Card>
        <CardHeader>
          <CardTitle>Players</CardTitle>
        </CardHeader>
        <CardContent>
          <PlayerManager
            players={record.players}
            onAddPlayer={handleAddPlayer}
            recordId={recordId}
            isAddingPlayer={addPlayer.isPending}
          />
        </CardContent>
      </Card>

      {/* Scoring */}
      <Card>
        <CardHeader>
          <CardTitle>Scores</CardTitle>
        </CardHeader>
        <CardContent>
          <ScoringInterface
            players={record.players}
            scoringConfig={record.scoringConfig}
            onRecordScore={handleRecordScore}
            isRecording={recordScore.isPending}
          />
        </CardContent>
      </Card>
    </div>
  );
}
