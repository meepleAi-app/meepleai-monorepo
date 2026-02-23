'use client';

import React, { Suspense, useEffect, useRef } from 'react';

import { Hash, Loader2 } from 'lucide-react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { toast } from 'sonner';

import {
  CounterTool,
  DiceRoller,
  SessionHeader,
  SessionToolLayout,
  Scoreboard,
  TurnIndicatorBar,
  TurnOrderTool,
  WhiteboardTool,
} from '@/components/session';
import type { CounterToolConfig, Participant } from '@/components/session/types';
import { useCounterTool } from '@/lib/hooks/useCounterTool';
import { useDiceRoller } from '@/lib/hooks/useDiceRoller';
import { useGameToolkit } from '@/lib/hooks/useGameToolkit';
import { useSessionSync } from '@/lib/hooks/useSessionSync';
import { useTurnOrder } from '@/lib/hooks/useTurnOrder';
import { useWhiteboardTool } from '@/lib/hooks/useWhiteboardTool';
import { useSessionStore } from '@/lib/stores/sessionStore';
import type { ToolId } from '@/lib/stores/sessionStore';
import type { CounterToolDto } from '@/lib/types/gameToolkit';
import { resolveSessionTools } from '@/lib/utils/resolveSessionTools';

/**
 * Active Session Page — Tool Rail Layout (Issues #4973, #4974, #4975, #4977, #4976)
 *
 * Wires all 4 base toolkit tools + custom GameToolkit tools:
 *  🔄 Turn Order  → TurnOrderTool + useTurnOrder
 *  🎲 Dice        → DiceRoller + useDiceRoller
 *  🖊️ Whiteboard  → WhiteboardTool + useWhiteboardTool
 *  📊 Scoreboard  → Scoreboard (data from sessionStore)
 *
 * Custom toolkit tools (Issue #4976):
 *  - Fetches published GameToolkit via useGameToolkit(session.gameId)
 *  - Override flags hide base tools; custom tools appear in "Custom" section
 *  - Counter tools use CounterToolContent sub-component (encapsulates hook)
 *
 * Active tool persisted in URL query param `?tool=<toolId>` (Issue #4974).
 * `sessionStore.activeTool` mirrors the URL for cross-component access.
 */

// ── CounterToolContent ─────────────────────────────────────────────────────────

/**
 * Wrapper component that owns the useCounterTool hook, allowing it to be
 * rendered from the switch statement without violating Rules of Hooks.
 */
function CounterToolContent({
  sessionId,
  toolDto,
  participants,
  currentUserId,
}: {
  sessionId: string;
  toolDto: CounterToolDto;
  participants: Participant[];
  currentUserId: string;
}) {
  const config: CounterToolConfig = {
    name: toolDto.name,
    minValue: toolDto.minValue,
    maxValue: toolDto.maxValue,
    defaultValue: toolDto.defaultValue,
    isPerPlayer: toolDto.isPerPlayer,
    icon: toolDto.icon ?? null,
    color: toolDto.color ?? null,
  };

  const { state, isPending, error, applyChange } = useCounterTool({
    sessionId,
    toolName: toolDto.name,
  });

  return (
    <CounterTool
      config={config}
      counterState={state}
      participants={participants}
      currentUserId={currentUserId}
      isPending={isPending}
      error={error}
      onApplyChange={applyChange}
    />
  );
}

// ── CustomToolPlaceholder ─────────────────────────────────────────────────────

/** Shown for custom tool types not yet fully implemented (card, timer, dice). */
function CustomToolPlaceholder({ label }: { label: string }) {
  return (
    <div className="flex flex-col items-center justify-center h-full gap-3 text-stone-400 dark:text-stone-500">
      <Hash className="w-10 h-10" aria-hidden="true" />
      <p className="text-sm font-medium">{label}</p>
      <p className="text-xs">Custom tool — coming soon</p>
    </div>
  );
}

// ── ActiveSessionPage ─────────────────────────────────────────────────────────

function ActiveSessionPageContent() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
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
    activeTool,
    setActiveTool,
  } = useSessionStore();

  // ── URL ↔ store sync ──────────────────────────────────────────────────────

  // On mount: read URL param and initialise store (one-way URL → store).
  const syncedRef = useRef(false);
  useEffect(() => {
    if (syncedRef.current) return;
    syncedRef.current = true;
    const urlTool = searchParams.get('tool');
    if (urlTool) {
      setActiveTool(urlTool as ToolId);
    }
  }, [searchParams, setActiveTool]);

  /** Update both the store and the URL when the user switches tools. */
  const handleToolSelect = (toolId: string) => {
    setActiveTool(toolId as ToolId);
    const next = new URLSearchParams(searchParams.toString());
    next.set('tool', toolId);
    router.replace(`?${next.toString()}`, { scroll: false });
  };

  // ── Game Toolkit (Issue #4976) ────────────────────────────────────────────

  const { toolkit } = useGameToolkit(activeSession?.gameId);
  const { visibleBaseToolIds, customTools } = resolveSessionTools(toolkit);

  // ── Turn order (Issue #4975) ─────────────────────────────────────────────

  const {
    turnOrder,
    isLoading: isTurnLoading,
    isAdvancing,
    error: turnError,
    advance: advanceTurn,
    reset: resetTurnOrder,
  } = useTurnOrder({ sessionId });

  // ── Dice roller (Issue #4974) ─────────────────────────────────────────────

  const currentParticipant = participants.find(p => p.isCurrentUser);
  const diceRoller = useDiceRoller({
    sessionId,
    participantId: currentParticipant?.id ?? '',
    participantName: currentParticipant?.displayName ?? '',
    onRollReceived: (roll) => {
      if (!roll.participantId || roll.participantId !== currentParticipant?.id) {
        toast.info(`${roll.participantName}: ${roll.formula} = ${roll.total}`);
      }
    },
  });

  // ── Whiteboard (Issue #4977) ──────────────────────────────────────────────

  const whiteboard = useWhiteboardTool({ sessionId });

  // ── Session lifecycle ─────────────────────────────────────────────────────

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

  // ── Turn-order action handlers ────────────────────────────────────────────

  const handleAdvanceTurn = async () => {
    try {
      const updated = await advanceTurn();
      toast.info(`Turn: ${updated.currentPlayer}`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to advance turn');
    }
  };

  const handleResetTurnOrder = async () => {
    try {
      await resetTurnOrder();
      toast.info('Ordine di turno reimpostato');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to reset turn order');
    }
  };

  // ── Header composition ────────────────────────────────────────────────────

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

  // ── Tool area ─────────────────────────────────────────────────────────────

  const toolContent = (() => {
    // ── Base tools (only rendered when not overridden) ──────────────────────
    if (activeTool === 'scoreboard' && visibleBaseToolIds.has('scoreboard')) {
      return <Scoreboard data={scoreboard} isRealTime={isConnected} />;
    }

    if (activeTool === 'turn-order' && visibleBaseToolIds.has('turn-order')) {
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
    }

    if (activeTool === 'dice' && visibleBaseToolIds.has('dice')) {
      return (
        <DiceRoller
          sessionId={sessionId}
          participantId={currentParticipant?.id ?? ''}
          participantName={currentParticipant?.displayName ?? ''}
          onRoll={diceRoller.roll}
          rollHistory={diceRoller.rollHistory}
          disabled={diceRoller.isRolling}
        />
      );
    }

    if (activeTool === 'whiteboard') {
      // Whiteboard is NEVER overridable
      return (
        <WhiteboardTool
          whiteboardState={whiteboard.whiteboardState}
          isPending={whiteboard.isPending}
          error={whiteboard.error}
          onStrokesChange={whiteboard.saveStrokes}
          onStructuredChange={whiteboard.saveStructured}
          onClear={whiteboard.clear}
        />
      );
    }

    // ── Custom toolkit tools (Issue #4976) ──────────────────────────────────
    if (toolkit && activeTool.startsWith('custom-')) {
      // Counter tools
      if (activeTool.startsWith('custom-counter-')) {
        const idx = parseInt(activeTool.replace('custom-counter-', ''), 10);
        const toolDto = toolkit.counterTools[idx];
        if (toolDto) {
          return (
            <CounterToolContent
              sessionId={sessionId}
              toolDto={toolDto}
              participants={participants}
              currentUserId={currentParticipant?.id ?? ''}
            />
          );
        }
      }

      // Dice, card, timer — placeholder for now
      const customTool = customTools.find(t => t.id === activeTool);
      if (customTool) {
        return <CustomToolPlaceholder label={customTool.label} />;
      }
    }

    // Fallback to scoreboard
    return <Scoreboard data={scoreboard} isRealTime={isConnected} />;
  })();

  return (
    <SessionToolLayout
      header={header}
      activeTool={activeTool}
      onToolSelect={handleToolSelect}
      customTools={customTools}
    >
      {toolContent}
    </SessionToolLayout>
  );
}

export default function ActiveSessionPage() {
  return (
    <Suspense>
      <ActiveSessionPageContent />
    </Suspense>
  );
}
