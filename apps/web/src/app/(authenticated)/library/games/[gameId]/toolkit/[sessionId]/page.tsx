'use client';

import React, { useEffect, useState } from 'react';

import { Loader2 } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';

import {
  SessionHeader,
  MeepleParticipantCard,
  ScoreInput,
  Scoreboard,
  type SyncStatus,
} from '@/components/session';
import type { ScoreboardData } from '@/components/session/types';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { getGameTemplateByName } from '@/lib/config/game-templates';
import { useSessionSync } from '@/lib/domain-hooks/useSessionSync';
import { useSessionStore } from '@/lib/stores/session-store';

/**
 * Game-Specific Active Session Page
 *
 * Features:
 * - Reuses Generic Toolkit components
 * - Auto-loads game template (rounds, categories)
 * - Real-time SSE sync
 * - Game-specific scoring rules display
 */
export default function GameSpecificSessionPage() {
  const params = useParams();
  const router = useRouter();
  const gameId = params?.gameId as string;
  const sessionId = params?.sessionId as string;

  if (!gameId || !sessionId) {
    throw new Error('Game ID and Session ID are required');
  }

  const {
    activeSession,
    scores,
    loadSession,
    recordScore,
    pauseSession,
    resumeSession: _resumeSession,
    completeSession,
    handleScoreUpdate,
    isLoading,
    error,
  } = useSessionStore();

  const { data: currentUser } = useCurrentUser();
  const [syncStatus, setSyncStatus] = useState<SyncStatus>('idle');
  const [gameName, setGameName] = useState<string>('');

  // ── Derived data ──────────────────────────────────────────────────────────

  const participants = (activeSession?.players ?? []).map(p => ({
    id: p.id,
    displayName: p.displayName,
    isOwner: p.role === 'Host',
    isCurrentUser: p.userId !== null && p.userId === currentUser?.id,
    avatarColor: p.color,
    totalScore: p.totalScore,
    rank: p.currentRank,
  }));

  const derivedScoreboard: ScoreboardData = {
    participants,
    scores: scores.map(s => ({
      id: `${s.playerId}-${s.round}-${s.dimension}`,
      participantId: s.playerId,
      roundNumber: s.round,
      category: s.dimension,
      scoreValue: s.value,
      timestamp: new Date(s.recordedAt),
      createdBy: s.playerId,
    })),
    rounds: [...new Set(scores.map(s => s.round))].sort((a, b) => a - b),
    categories: [...new Set(scores.map(s => s.dimension))],
  };

  // Load session details
  useEffect(() => {
    const load = async () => {
      try {
        await loadSession(sessionId);

        // Fetch game name for template lookup
        const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
        const gameResponse = await fetch(`${baseUrl}/api/v1/games/${gameId}`, {
          credentials: 'include',
        });

        if (gameResponse.ok) {
          const gameData = await gameResponse.json();
          setGameName(gameData.title ?? gameData.name ?? '');
        }
      } catch (err) {
        toast.error(err instanceof Error ? err.message : 'Failed to load session');
        router.push(`/library/games/${gameId}`);
      }
    };

    void load();
  }, [sessionId, gameId, loadSession, router]);

  // Get game template
  const template = gameName ? getGameTemplateByName(gameName) : null;

  // Initialize SSE
  const { isConnected, error: sseError } = useSessionSync({
    sessionId,
    onScoreUpdate: scoreEntry => {
      handleScoreUpdate({
        playerId: scoreEntry.participantId,
        round: scoreEntry.roundNumber ?? 1,
        dimension: scoreEntry.category ?? 'score',
        value: scoreEntry.scoreValue,
        unit: null,
        recordedAt: scoreEntry.timestamp.toISOString(),
      });

      const participant = participants.find(p => p.id === scoreEntry.participantId);
      if (participant) {
        toast.success(`${participant.displayName}: +${scoreEntry.scoreValue}`);
      }
    },
    onPaused: () => {
      toast.info('Session paused');
    },
    onResumed: () => {
      toast.info('Session resumed');
    },
    onFinalized: () => {
      toast.success('Session finalized');

      setTimeout(() => {
        router.push(`/library/games/${gameId}`);
      }, 2000);
    },
    onError: err => {
      toast.error(`Connection error: ${err.message}`);
    },
  });

  /**
   * Handle score submission
   */
  const handleScoreSubmit = async (data: {
    participantId: string;
    roundNumber: number | null;
    category: string | null;
    scoreValue: number;
  }) => {
    setSyncStatus('saving');

    try {
      await recordScore({
        playerId: data.participantId,
        round: data.roundNumber ?? 1,
        dimension: data.category ?? 'score',
        value: data.scoreValue,
      });
      setSyncStatus('synced');
      setTimeout(() => setSyncStatus('idle'), 2000);
    } catch (err) {
      setSyncStatus('error');
      toast.error(err instanceof Error ? err.message : 'Failed to update score');
    }
  };

  /**
   * Handle pause
   */
  const handlePause = async () => {
    try {
      await pauseSession();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to pause session');
    }
  };

  /**
   * Handle finalize
   */
  const handleFinalize = async () => {
    try {
      await completeSession();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to finalize session');
    }
  };

  // Loading state
  if (isLoading || !activeSession) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
      </div>
    );
  }

  // Error state
  if (error || sseError) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-6 max-w-md">
          <p className="font-semibold text-red-800 dark:text-red-200">Error</p>
          <p className="text-sm text-red-700 dark:text-red-300">{error || sseError?.message}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Session Header */}
      <SessionHeader
        session={activeSession as unknown as import('@/components/session/types').Session}
        onPause={activeSession.status === 'InProgress' ? handlePause : undefined}
        onFinalize={handleFinalize}
      />

      {/* Main Content */}
      <div className="container mx-auto px-4 py-6">
        <div className="grid lg:grid-cols-3 gap-6">
          {/* Left: Participants + Template Info */}
          <div className="lg:col-span-1 space-y-4">
            <h2 className="text-lg font-semibold mb-4">Participants</h2>
            {participants.map(participant => (
              <MeepleParticipantCard key={participant.id} participant={participant} />
            ))}

            {/* Template Info */}
            {template && (
              <Card className="mt-6">
                <CardHeader>
                  <CardTitle className="text-sm">Scoring Rules</CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-gray-600 dark:text-gray-400">
                    {template.scoringRules}
                  </p>
                </CardContent>
              </Card>
            )}
          </div>

          {/* Right: Scoreboard */}
          <div className="lg:col-span-2">
            <Scoreboard data={derivedScoreboard} isRealTime={isConnected} />
          </div>
        </div>
      </div>

      {/* Sticky Bottom: Score Input with Template */}
      <div className="fixed bottom-0 left-0 right-0 bg-white dark:bg-gray-800 border-t border-gray-200 dark:border-gray-700 shadow-lg">
        <div className="container mx-auto px-4 py-4">
          <ScoreInput
            participants={participants}
            rounds={template?.rounds || derivedScoreboard.rounds}
            categories={template?.categories || derivedScoreboard.categories}
            onSubmit={handleScoreSubmit}
            syncStatus={syncStatus}
          />
        </div>
      </div>
    </div>
  );
}
