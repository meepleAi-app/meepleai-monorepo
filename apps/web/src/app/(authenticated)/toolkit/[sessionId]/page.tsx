'use client';

import React, { useEffect, useState } from 'react';

import { Loader2 } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';

import {
  SessionHeader,
  SessionToolLayout,
  Scoreboard,
  TurnIndicatorBar,
  TurnOrderTool,
} from '@/components/session';
import { useSessionSync } from '@/lib/hooks/useSessionSync';
import { useTurnOrder } from '@/lib/hooks/useTurnOrder';
import { useSessionStore } from '@/lib/stores/sessionStore';

/**
 * Active Session Page — Tool Rail Layout (Issues #4973, #4975)
 *
 * Layout: collapsible side rail (desktop) / bottom nav (mobile).
 * TurnIndicatorBar wired to live TurnOrder state (Issue #4975).
 * TurnOrderTool wired to useTurnOrder hook (Issue #4975).
 * Remaining tools: dice (#4974), whiteboard (#4977).
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
    pauseSession,
    resumeSession,
    finalizeSession,
    addScoreFromSSE,
    isLoading,
    error,
  } = useSessionStore();

  const [activeTool, setActiveTool] = useState<string>('scoreboard');

  // ── Turn order (Issue #4975) ─────────────────────────────────────────────────
  const {
    turnOrder,
    isLoading: isTurnLoading,
    isAdvancing,
    error: turnError,
    advance: advanceTurn,
    reset: resetTurnOrder,
  } = useTurnOrder({ sessionId });

  // Load session details on mount
  useEffect(() => {
    const load = async () => {
      try {
        await loadSession(sessionId);
      } catch (err) {
        toast.error(err instanceof Error ? err.message : 'Failed to load session');
        router.push('/toolkit');
      }
    };
    void load();
  }, [sessionId, loadSession, router]);

  // Initialize SSE connection (v1 — scores + session lifecycle)
  const { isConnected, error: sseError } = useSessionSync({
    sessionId,
    onScoreUpdate: scoreEntry => {
      addScoreFromSSE(scoreEntry);
      const participant = participants.find(p => p.id === scoreEntry.participantId);
      if (participant) {
        toast.success(`${participant.displayName}: +${scoreEntry.scoreValue}`);
      }
    },
    onPaused: () => toast.info('Session paused'),
    onResumed: () => toast.info('Session resumed'),
    onFinalized: () => toast.success('Session finalized'),
    onError: err => toast.error(`Connection error: ${err.message}`),
  });

  const handlePause = async () => {
    try {
      await pauseSession();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to pause session');
    }
  };

  const _handleResume = async () => {
    try {
      await resumeSession();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to resume session');
    }
  };

  const handleFinalize = async () => {
    if (!scoreboard) return;
    const rankedParticipants = [...participants].sort((a, b) => b.totalScore - a.totalScore);
    const ranks: Record<string, number> = {};
    rankedParticipants.forEach((p, index) => {
      ranks[p.id] = index + 1;
    });
    try {
      await finalizeSession({ ranks });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to finalize session');
    }
  };

  // Current user is a session owner if any participant marked isCurrentUser + isOwner
  const isCurrentUserOwner =
    participants.find(p => p.isCurrentUser)?.isOwner ?? false;

  // Loading state
  if (isLoading || !activeSession || !scoreboard) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-amber-600" />
      </div>
    );
  }

  // Error state
  if (error || sseError) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-6 max-w-md">
          <p className="font-semibold text-red-800 dark:text-red-200">Error</p>
          <p className="text-sm text-red-700 dark:text-red-300">{error ?? sseError?.message}</p>
        </div>
      </div>
    );
  }

  // Advance turn handler with toast
  const handleAdvanceTurn = async () => {
    try {
      const updated = await advanceTurn();
      toast.info(`Turn: ${updated.currentPlayer}`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to advance turn');
    }
  };

  // Reset turn order handler with toast
  const handleResetTurnOrder = async () => {
    try {
      await resetTurnOrder();
      toast.info('Ordine di turno reimpostato');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to reset turn order');
    }
  };

  // Compose header: SessionHeader + TurnIndicatorBar (wired to turn order, Issue #4975)
  const header = (
    <>
      <SessionHeader
        session={activeSession}
        onPause={activeSession.status === 'Active' ? handlePause : undefined}
        onFinalize={handleFinalize}
      />
      <TurnIndicatorBar
        activePlayerName={turnOrder?.currentPlayer ?? null}
        roundNumber={turnOrder?.roundNumber ?? null}
        canEndTurn={!!turnOrder && !isAdvancing}
        onEndTurn={() => void handleAdvanceTurn()}
      />
    </>
  );

  // Tool area content — rendered based on activeTool.
  const toolContent = (() => {
    switch (activeTool) {
      case 'scoreboard':
        return <Scoreboard data={scoreboard} isRealTime={isConnected} />;

      case 'turn-order':
        return (
          <TurnOrderTool
            turnOrder={turnOrder}
            isLoading={isTurnLoading}
            error={turnError}
            isHost={isCurrentUserOwner}
            onAdvanceTurn={handleAdvanceTurn}
            onResetTurnOrder={handleResetTurnOrder}
          />
        );

      case 'dice':
        return (
          <div className="flex items-center justify-center h-full min-h-[300px] text-stone-400 dark:text-stone-600 italic text-sm">
            Dice — coming in Issue #4974
          </div>
        );

      case 'whiteboard':
        return (
          <div className="flex items-center justify-center h-full min-h-[300px] text-stone-400 dark:text-stone-600 italic text-sm">
            Whiteboard — coming in Issue #4977
          </div>
        );

      default:
        return <Scoreboard data={scoreboard} isRealTime={isConnected} />;
    }
  })();

  return (
    <SessionToolLayout
      header={header}
      activeTool={activeTool}
      onToolSelect={setActiveTool}
      customTools={[]}
    >
      {toolContent}
    </SessionToolLayout>
  );
}
