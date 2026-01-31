'use client';

import React, { useEffect, useState } from 'react';

import { Loader2 } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';

import {
  SessionHeader,
  ParticipantCard,
  ScoreInput,
  Scoreboard,
  type SyncStatus,
} from '@/components/session';
import { useSessionSync } from '@/lib/hooks/useSessionSync';
import { useSessionStore } from '@/lib/stores/sessionStore';

/**
 * Active Session Page
 *
 * Features:
 * - Real-time SSE sync for score updates
 * - 3-column responsive layout
 * - Optimistic UI for score submissions
 * - Session lifecycle actions (pause, resume, finalize)
 */
export default function ActiveSessionPage() {
  const params = useParams();
  const router = useRouter();
  const sessionId = params?.sessionId as string;

  if (!sessionId) {
    throw new Error('Session ID is required');
  }

  const {
    activeSession,
    scoreboard,
    participants,
    loadSession,
    updateScore,
    pauseSession,
    resumeSession,
    finalizeSession,
    addScoreFromSSE,
    isLoading,
    error,
  } = useSessionStore();

  const [syncStatus, setSyncStatus] = useState<SyncStatus>('idle');

  // Load session details on mount
  useEffect(() => {
    const load = async () => {
      try {
        await loadSession(sessionId);
      } catch (err) {
        toast.error(err instanceof Error ? err.message : 'Failed to load session');

        // Redirect to toolkit home if session not found
        router.push('/toolkit');
      }
    };

    void load();
  }, [sessionId, loadSession, router]);

  // Initialize SSE connection
  const { isConnected, error: sseError } = useSessionSync({
    sessionId,
    onScoreUpdate: scoreEntry => {
      // Add score to store from SSE event
      addScoreFromSSE(scoreEntry);

      // Show toast notification
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
      // Note: Redirect handled by handleFinalize, not here to avoid duplicates
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
      await updateScore(data);

      setSyncStatus('synced');
      setTimeout(() => setSyncStatus('idle'), 2000);
    } catch (err) {
      setSyncStatus('error');
      toast.error(err instanceof Error ? err.message : 'Failed to update score');
    }
  };

  /**
   * Handle pause session
   */
  const handlePause = async () => {
    try {
      await pauseSession();
      // Toast shown by SSE callback to avoid duplicates
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to pause session');
    }
  };

  /**
   * Handle resume session
   */
  const _handleResume = async () => {
    try {
      await resumeSession();
      // Toast shown by SSE callback to avoid duplicates
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to resume session');
    }
  };

  /**
   * Handle finalize session
   */
  const handleFinalize = async () => {
    if (!scoreboard) return;

    // Calculate ranks based on total scores
    const rankedParticipants = [...participants].sort((a, b) => b.totalScore - a.totalScore);
    const ranks: Record<string, number> = {};
    rankedParticipants.forEach((p, index) => {
      ranks[p.id] = index + 1;
    });

    try {
      await finalizeSession({ ranks });
      // Toast shown by SSE callback to avoid duplicates
      // SSE callback also handles redirect after finalization
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to finalize session');
    }
  };

  // Loading state
  if (isLoading || !activeSession || !scoreboard) {
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
          <p className="text-sm text-red-700 dark:text-red-300">
            {error || sseError?.message}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Session Header */}
      <SessionHeader
        session={activeSession}
        onPause={activeSession.status === 'Active' ? handlePause : undefined}
        onFinalize={handleFinalize}
      />

      {/* Main Content */}
      <div className="container mx-auto px-4 py-6">
        <div className="grid lg:grid-cols-3 gap-6">
          {/* Left: Participants */}
          <div className="lg:col-span-1 space-y-4">
            <h2 className="text-lg font-semibold mb-4">Participants</h2>
            {participants.map(participant => (
              <ParticipantCard key={participant.id} participant={participant} />
            ))}
          </div>

          {/* Center/Right: Scoreboard */}
          <div className="lg:col-span-2">
            <Scoreboard data={scoreboard} isRealTime={isConnected} />
          </div>
        </div>
      </div>

      {/* Sticky Bottom: Score Input */}
      <div className="fixed bottom-0 left-0 right-0 bg-white dark:bg-gray-800 border-t border-gray-200 dark:border-gray-700 shadow-lg">
        <div className="container mx-auto px-4 py-4">
          <ScoreInput
            participants={participants}
            rounds={scoreboard.rounds}
            categories={scoreboard.categories}
            onSubmit={handleScoreSubmit}
            syncStatus={syncStatus}
          />
        </div>
      </div>
    </div>
  );
}
