/**
 * Scoreboard Tab — /sessions/[id] (default route)
 *
 * Renders SessionHeader + LiveIndicator + Scoreboard with real-time SSE updates.
 *
 * Issue #5041 — Sessions Redesign Phase 2
 */

'use client';

import { useCallback, use } from 'react';

import { Loader2 } from 'lucide-react';

import { LiveIndicator, Scoreboard, SessionHeader } from '@/components/session';
import { toScoreboardData, toSession } from '@/components/session/adapters';
import type { LiveSessionStatus } from '@/lib/api/schemas/live-sessions.schemas';
import { useSessionSync } from '@/lib/hooks/useSessionSync';
import { useSessionStore } from '@/lib/stores/sessionStore';

interface SessionPageProps {
  params: Promise<{ id: string }>;
}

export default function SessionScoreboardPage({ params }: SessionPageProps) {
  const { id } = use(params);

  const activeSession = useSessionStore(s => s.activeSession);
  const scores = useSessionStore(s => s.scores);
  const isLoading = useSessionStore(s => s.isLoading);
  const error = useSessionStore(s => s.error);
  const loadScores = useSessionStore(s => s.loadScores);
  const pauseSession = useSessionStore(s => s.pauseSession);
  const resumeSession = useSessionStore(s => s.resumeSession);
  const completeSession = useSessionStore(s => s.completeSession);
  const handleSessionUpdate = useSessionStore(s => s.handleSessionUpdate);

  // SSE callbacks — refresh scores on update, handle lifecycle events
  const onScoreUpdate = useCallback(() => {
    loadScores();
  }, [loadScores]);

  const onPaused = useCallback(() => {
    if (activeSession) {
      handleSessionUpdate({
        ...activeSession,
        status: 'Paused' as LiveSessionStatus,
      });
    }
  }, [activeSession, handleSessionUpdate]);

  const onResumed = useCallback(() => {
    if (activeSession) {
      handleSessionUpdate({
        ...activeSession,
        status: 'InProgress' as LiveSessionStatus,
      });
    }
  }, [activeSession, handleSessionUpdate]);

  const onFinalized = useCallback(() => {
    if (activeSession) {
      handleSessionUpdate({
        ...activeSession,
        status: 'Completed' as LiveSessionStatus,
      });
    }
  }, [activeSession, handleSessionUpdate]);

  // Connect SSE
  const { isConnected } = useSessionSync({
    sessionId: id,
    onScoreUpdate,
    onPaused,
    onResumed,
    onFinalized,
  });

  // Loading state
  if (isLoading && !activeSession) {
    return (
      <div className="flex items-center justify-center py-20">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // Error state
  if (error && !activeSession) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 py-20 text-center">
        <p className="text-sm text-destructive">{error}</p>
        <p className="text-xs text-muted-foreground">Impossibile caricare la sessione</p>
      </div>
    );
  }

  // No session loaded yet
  if (!activeSession) return null;

  const session = toSession(activeSession);
  const scoreboardData = toScoreboardData(activeSession, scores, null);
  const currentRound = Math.max(1, ...scores.map(s => s.round), 0);

  const handlePause = () => {
    if (activeSession.status === 'Paused') {
      resumeSession();
    } else {
      pauseSession();
    }
  };

  return (
    <div className="pb-32">
      <SessionHeader session={session} onPause={handlePause} onFinalize={completeSession} />

      <LiveIndicator
        startedAt={activeSession.startedAt}
        currentRound={currentRound}
        isConnected={isConnected}
      />

      <Scoreboard data={scoreboardData} variant="full" isRealTime />
    </div>
  );
}
