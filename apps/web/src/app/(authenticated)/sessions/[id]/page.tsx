/**
 * Scoreboard Tab — /sessions/[id] (default route)
 *
 * Renders SessionHeader + LiveIndicator + Scoreboard with real-time SSE updates.
 * Also renders RelatedEntitiesSection for entity link connections on this session.
 *
 * Issue #5041 — Sessions Redesign Phase 2
 */

'use client';

import { useCallback, use } from 'react';

import { Loader2 } from 'lucide-react';

import { LiveIndicator, Scoreboard, SessionHeader } from '@/components/session';
import { toScoreboardData, toSession } from '@/components/session/adapters';
import { RelatedEntitiesSection } from '@/components/ui/data-display/entity-link/related-entities-section';
import type { LiveSessionStatus } from '@/lib/api/schemas/live-sessions.schemas';
import { useSessionSync } from '@/lib/domain-hooks/useSessionSync';
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

  // SSE callbacks — use getState() to avoid stale closures and SSE reconnection cascades
  const onScoreUpdate = useCallback(() => {
    loadScores();
  }, [loadScores]);

  const onPaused = useCallback(() => {
    const session = useSessionStore.getState().activeSession;
    if (session) {
      handleSessionUpdate({ ...session, status: 'Paused' as LiveSessionStatus });
    }
  }, [handleSessionUpdate]);

  const onResumed = useCallback(() => {
    const session = useSessionStore.getState().activeSession;
    if (session) {
      handleSessionUpdate({ ...session, status: 'InProgress' as LiveSessionStatus });
    }
  }, [handleSessionUpdate]);

  const onFinalized = useCallback(() => {
    const session = useSessionStore.getState().activeSession;
    if (session) {
      handleSessionUpdate({ ...session, status: 'Completed' as LiveSessionStatus });
    }
  }, [handleSessionUpdate]);

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
  const roundNumbers = scores.map(s => s.round);
  const currentRound = roundNumbers.length > 0 ? Math.max(1, ...roundNumbers) : 1;

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

      <RelatedEntitiesSection entityType="Session" entityId={id} />
    </div>
  );
}
