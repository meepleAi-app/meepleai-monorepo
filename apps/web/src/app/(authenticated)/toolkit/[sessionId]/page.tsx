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
  type SyncStatus,
} from '@/components/session';
import { useSessionSync } from '@/lib/hooks/useSessionSync';
import { useSessionStore } from '@/lib/stores/sessionStore';

/**
 * Active Session Page — Tool Rail Layout (Issue #4973)
 *
 * New layout: collapsible side rail (desktop) / bottom nav (mobile).
 * Tool area renders the active tool (scoreboard default until #4974).
 * TurnIndicatorBar is a placeholder until #4975 wires TurnOrder state.
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
  const [activeTool, setActiveTool] = useState<string>('scoreboard');

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

  // Initialize SSE connection
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

  // Compose header: SessionHeader + TurnIndicatorBar
  const header = (
    <>
      <SessionHeader
        session={activeSession}
        onPause={activeSession.status === 'Active' ? handlePause : undefined}
        onFinalize={handleFinalize}
      />
      {/* Turn indicator sub-line — wired to TurnOrder in #4975 */}
      <TurnIndicatorBar
        activePlayerName={null}
        roundNumber={null}
        canEndTurn={false}
      />
    </>
  );

  /**
   * Tool area content — rendered based on activeTool.
   * Remaining tools wired in Issue #4974 (Base Toolkit Integration).
   * syncStatus passed to Scoreboard via unused param until #4974.
   * @ts-expect-error syncStatus used by ScoreInput (removed in this redesign)
   */
  void syncStatus;

  const toolContent = (() => {
    switch (activeTool) {
      case 'scoreboard':
        return <Scoreboard data={scoreboard} isRealTime={isConnected} />;
      case 'turn-order':
        return (
          <div className="flex items-center justify-center h-full min-h-[300px] text-stone-400 dark:text-stone-600 italic text-sm">
            Turn Order tool — coming in Issue #4975
          </div>
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
