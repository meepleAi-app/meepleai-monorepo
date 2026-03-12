'use client';

/**
 * LiveSessionView — Main orchestrator for the live game session play view.
 *
 * Mobile-first layout:
 *   Header → Scoreboard → Quick Actions → Chat Widget
 *
 * Desktop (lg+): 3-column via LiveSessionLayout —
 *   Left panel (scoreboard + actions) | Center (activity feed) | Right panel (chat)
 *
 * Integrates with:
 * - useSessionStore (Zustand) for session state
 * - useSessionSync (SSE) for real-time updates
 * - LiveSessionLayout for desktop 3-column layout
 * - useQuickViewStore for session-contextual quick view
 * - Sheet (slide-over) for rules explainer
 * - ScoreInput for quick score entry
 *
 * Issue #5587 — Live Game Session UI
 */

import { useState, useCallback, useEffect, useRef, useMemo } from 'react';

import { Loader2 } from 'lucide-react';

import { toScoreboardData } from '@/components/session/adapters';
import { LiveSessionLayout } from '@/components/session/LiveSessionLayout';
import { ScoreInput } from '@/components/session/ScoreInput';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/navigation/sheet';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { useGameAgents } from '@/hooks/queries/useGameAgents';
import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { useResponsive } from '@/hooks/useResponsive';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { useSessionSync } from '@/lib/hooks/useSessionSync';
import { useSessionStore } from '@/lib/stores/sessionStore';
import { useQuickViewStore } from '@/store/quick-view';

import { LiveScoreboard, type LiveScoreboardPlayer } from './LiveScoreboard';
import { QuickActions } from './QuickActions';
import { SaveCompleteDialog } from './SaveCompleteDialog';
import { ScoreAssistant } from './ScoreAssistant';
import { SessionChatWidget, type ChatMessage } from './SessionChatWidget';
import { SessionHeader } from './SessionHeader';

// ============================================================================
// Helpers
// ============================================================================

function mapSessionStatus(status: string): 'Active' | 'Paused' | 'Finalized' {
  const map: Record<string, 'Active' | 'Paused' | 'Finalized'> = {
    Created: 'Active',
    Setup: 'Active',
    InProgress: 'Active',
    Paused: 'Paused',
    Completed: 'Finalized',
  };
  return map[status] ?? 'Active';
}

function mapToScoreboardPlayers(session: LiveSessionDto): LiveScoreboardPlayer[] {
  return session.players.map(p => ({
    id: p.id,
    displayName: p.displayName,
    totalScore: p.totalScore,
    avatarColor:
      p.color === 'Red'
        ? '#ef4444'
        : p.color === 'Blue'
          ? '#3b82f6'
          : p.color === 'Green'
            ? '#22c55e'
            : p.color === 'Yellow'
              ? '#eab308'
              : p.color === 'Purple'
                ? '#a855f7'
                : p.color === 'Orange'
                  ? '#f97316'
                  : p.color === 'Pink'
                    ? '#ec4899'
                    : p.color === 'Teal'
                      ? '#14b8a6'
                      : p.color === 'White'
                        ? '#f5f5f5'
                        : p.color === 'Black'
                          ? '#1f2937'
                          : '#9ca3af',
    isCurrentUser: false,
  }));
}

// ============================================================================
// Component
// ============================================================================

export interface LiveSessionViewProps {
  sessionId: string;
}

export function LiveSessionView({ sessionId }: LiveSessionViewProps) {
  // ----- Store -----
  const activeSession = useSessionStore(s => s.activeSession);
  const scores = useSessionStore(s => s.scores);
  const isLoading = useSessionStore(s => s.isLoading);
  const error = useSessionStore(s => s.error);
  const loadScores = useSessionStore(s => s.loadScores);
  const _pauseSession = useSessionStore(s => s.pauseSession);
  const resumeSession = useSessionStore(s => s.resumeSession);
  const handleSessionUpdate = useSessionStore(s => s.handleSessionUpdate);

  // ----- Responsive -----
  const { isDesktop } = useResponsive();

  // ----- QuickView session context -----
  useEffect(() => {
    if (activeSession?.gameId) {
      useQuickViewStore.getState().openForSession(sessionId, activeSession.gameId);
    }
    return () => {
      useQuickViewStore.getState().close();
    };
  }, [sessionId, activeSession?.gameId]);

  // ----- Agent chat (SSE streaming) -----
  const gameId = activeSession?.gameId ?? null;
  const { data: agents } = useGameAgents({ gameId, enabled: !!gameId });
  const agentId = agents?.[0]?.id ?? null;

  const chatThreadIdRef = useRef<string | null>(null);
  const [sentMessages, setSentMessages] = useState<ChatMessage[]>([]);

  const { state: agentState, sendMessage: sendAgentMessage } = useAgentChatStream({
    onComplete: (_answer, metadata) => {
      // Persist the chatThreadId for follow-up messages in the same thread
      if (metadata.chatThreadId) {
        chatThreadIdRef.current = metadata.chatThreadId;
      }
    },
  });

  // Build the full message list: sent user messages + current streaming response
  const chatMessages = useMemo<ChatMessage[]>(() => {
    const msgs = [...sentMessages];
    // Add the current (or completed) assistant response if present
    if (agentState.currentAnswer) {
      msgs.push({
        id: `assistant-${msgs.length}`,
        role: 'assistant',
        content: agentState.currentAnswer,
        timestamp: new Date(),
      });
    }
    return msgs;
  }, [sentMessages, agentState.currentAnswer]);

  const isChatStreaming = agentState.isStreaming;

  // ----- Local UI state -----
  const [rulesOpen, setRulesOpen] = useState(false);
  const [arbiterOpen, setArbiterOpen] = useState(false);
  const [scoresOpen, setScoresOpen] = useState(false);
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);

  // ----- SSE connection -----
  const onScoreUpdate = useCallback(() => {
    loadScores();
  }, [loadScores]);

  const onPaused = useCallback(() => {
    const session = useSessionStore.getState().activeSession;
    if (session) {
      handleSessionUpdate({ ...session, status: 'Paused' });
    }
  }, [handleSessionUpdate]);

  const onResumed = useCallback(() => {
    const session = useSessionStore.getState().activeSession;
    if (session) {
      handleSessionUpdate({ ...session, status: 'InProgress' });
    }
  }, [handleSessionUpdate]);

  const { isConnected } = useSessionSync({
    sessionId,
    onScoreUpdate,
    onPaused,
    onResumed,
  });

  // ----- Handlers -----
  const handleTogglePause = useCallback(() => {
    if (!activeSession) return;
    if (activeSession.status === 'Paused') {
      resumeSession();
    } else {
      setSaveDialogOpen(true);
    }
  }, [activeSession, resumeSession]);

  const handleSaveComplete = useCallback(() => {
    // Session is already paused by the save-complete endpoint
    const session = useSessionStore.getState().activeSession;
    if (session) {
      handleSessionUpdate({ ...session, status: 'Paused' });
    }
    setSaveDialogOpen(false);
  }, [handleSessionUpdate]);

  const handleChatSend = useCallback(
    (message: string) => {
      if (!agentId) return;

      // Snapshot the previous assistant answer (if any) before the hook resets
      if (agentState.currentAnswer) {
        setSentMessages(prev => [
          ...prev,
          {
            id: `assistant-${Date.now() - 1}`,
            role: 'assistant' as const,
            content: agentState.currentAnswer,
            timestamp: new Date(),
          },
        ]);
      }

      // Add the user message to history
      const userMsg: ChatMessage = {
        id: `user-${Date.now()}`,
        role: 'user',
        content: message,
        timestamp: new Date(),
      };
      setSentMessages(prev => [...prev, userMsg]);

      // Send via SSE stream
      sendAgentMessage(agentId, message, chatThreadIdRef.current ?? undefined);
    },
    [agentId, agentState.currentAnswer, sendAgentMessage]
  );

  const handleScoreSubmit = useCallback(
    async (data: {
      participantId: string;
      roundNumber: number | null;
      category: string | null;
      scoreValue: number;
    }) => {
      if (!activeSession) return;

      const { recordScore } = useSessionStore.getState();
      await recordScore({
        playerId: data.participantId,
        round: data.roundNumber ?? 1,
        dimension: data.category ?? 'default',
        value: data.scoreValue,
      });
    },
    [activeSession]
  );

  // ----- Loading / Error states -----
  if (isLoading && !activeSession) {
    return (
      <div className="flex items-center justify-center py-20" data-testid="live-session-loading">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error && !activeSession) {
    return (
      <div
        className="flex flex-col items-center justify-center gap-2 py-20 text-center"
        data-testid="live-session-error"
      >
        <p className="text-sm text-destructive">{error}</p>
        <p className="text-xs text-muted-foreground">Impossibile caricare la sessione</p>
      </div>
    );
  }

  if (!activeSession) return null;

  // ----- Derived data -----
  const isPaused = activeSession.status === 'Paused';
  const sessionStatus = mapSessionStatus(activeSession.status);
  const turnNumber = activeSession.currentTurnIndex + 1;
  const scoreboardPlayers = mapToScoreboardPlayers(activeSession);
  const scoreboardData = toScoreboardData(activeSession, scores, null);
  const roundNumbers = scores.map(s => s.round);
  const currentRound = roundNumbers.length > 0 ? Math.max(1, ...roundNumbers) : 1;

  // ----- Shared sub-sections (used in both layouts) -----

  const connectionIndicator = (
    <div className="flex items-center justify-center py-2">
      {isConnected ? (
        <span className="inline-flex items-center gap-1.5 text-xs font-medium text-emerald-600 dark:text-emerald-400">
          <span className="relative flex h-2 w-2">
            <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
            <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500" />
          </span>
          Live
        </span>
      ) : (
        <span className="inline-flex items-center gap-1.5 text-xs font-medium text-red-600 dark:text-red-400">
          <span className="h-2 w-2 rounded-full bg-red-500" />
          Disconnesso
        </span>
      )}
    </div>
  );

  const scoreboardSection = (
    <div className="space-y-4 p-4">
      <LiveScoreboard players={scoreboardPlayers} isRealTime={isConnected} />
      <ScoreAssistant sessionId={sessionId} onScoreRecorded={loadScores} />
      <QuickActions
        isPaused={isPaused}
        isLoading={isLoading}
        onOpenRules={() => setRulesOpen(true)}
        onAskArbiter={() => setArbiterOpen(true)}
        onTogglePause={handleTogglePause}
        onOpenScores={() => setScoresOpen(true)}
      />
    </div>
  );

  const chatSection = (
    <div className="p-4 h-full">
      <SessionChatWidget
        messages={chatMessages}
        isStreaming={isChatStreaming}
        onSend={handleChatSend}
        defaultExpanded={isDesktop}
      />
    </div>
  );

  const overlays = (
    <>
      {/* Rules slide-over (Sheet) */}
      <Sheet open={rulesOpen} onOpenChange={setRulesOpen}>
        <SheetContent side="right" className="w-full sm:max-w-md overflow-y-auto">
          <SheetHeader>
            <SheetTitle>Regole del gioco</SheetTitle>
            <SheetDescription>
              Consulta le regole di {activeSession.gameName || 'questo gioco'}.
            </SheetDescription>
          </SheetHeader>
          <div className="mt-6 text-sm text-slate-600 dark:text-slate-400">
            <p>
              Il modulo regole verrà collegato alla Knowledge Base nella prossima iterazione. Per
              ora puoi usare la chat per porre domande.
            </p>
          </div>
        </SheetContent>
      </Sheet>

      {/* Arbiter modal */}
      <Dialog open={arbiterOpen} onOpenChange={setArbiterOpen}>
        <DialogContent className="max-w-sm" data-testid="arbiter-dialog">
          <DialogTitle>Chiedi all&apos;arbitro</DialogTitle>
          <div className="space-y-3 pt-2">
            <p className="text-sm text-muted-foreground">
              Descrivi la situazione e l&apos;arbitro AI analizzerà le regole per dare un verdetto.
            </p>
            <p className="text-xs text-slate-400">Funzione in arrivo nella prossima iterazione.</p>
          </div>
        </DialogContent>
      </Dialog>

      {/* Score input sheet */}
      <Sheet open={scoresOpen} onOpenChange={setScoresOpen}>
        <SheetContent side="bottom" className="max-h-[80vh] overflow-y-auto rounded-t-2xl">
          <SheetHeader>
            <SheetTitle>Aggiorna punteggi</SheetTitle>
            <SheetDescription>Inserisci i punti per un giocatore.</SheetDescription>
          </SheetHeader>
          <div className="mt-4">
            <ScoreInput
              participants={scoreboardData.participants}
              rounds={scoreboardData.rounds.length > 0 ? scoreboardData.rounds : [currentRound]}
              categories={scoreboardData.categories}
              currentRound={currentRound}
              onSubmit={handleScoreSubmit}
            />
          </div>
        </SheetContent>
      </Sheet>

      {/* Save complete dialog (replaces PauseSessionDialog) */}
      <SaveCompleteDialog
        open={saveDialogOpen}
        onOpenChange={setSaveDialogOpen}
        sessionId={sessionId}
        onSaveComplete={handleSaveComplete}
      />
    </>
  );

  // ----- Desktop layout (lg+): 3-column via LiveSessionLayout -----
  if (isDesktop) {
    return (
      <div className="flex flex-col bg-gradient-to-b from-amber-50/30 to-white dark:from-slate-950 dark:to-slate-900">
        {/* Header */}
        <SessionHeader
          gameName={activeSession.gameName || 'Sessione di gioco'}
          turnNumber={turnNumber}
          currentPhase={null}
          status={sessionStatus}
        />

        {/* 3-column layout */}
        <LiveSessionLayout
          leftPanel={scoreboardSection}
          centerContent={
            <div className="flex flex-col h-full overflow-auto">
              {connectionIndicator}
              {/* Activity feed placeholder — will be wired in a follow-up issue */}
              <div
                className="flex-1 flex items-center justify-center text-sm text-muted-foreground"
                data-testid="activity-feed-placeholder"
              >
                Feed attività in arrivo
              </div>
            </div>
          }
          rightPanel={chatSection}
        />

        {overlays}
      </div>
    );
  }

  // ----- Mobile layout: stacked -----
  return (
    <div className="min-h-screen bg-gradient-to-b from-amber-50/30 to-white dark:from-slate-950 dark:to-slate-900">
      {/* Header */}
      <SessionHeader
        gameName={activeSession.gameName || 'Sessione di gioco'}
        turnNumber={turnNumber}
        currentPhase={null}
        status={sessionStatus}
      />

      {connectionIndicator}

      {/* Main content: stacked on mobile */}
      <div className="px-4 pb-8 space-y-4">
        <LiveScoreboard players={scoreboardPlayers} isRealTime={isConnected} />
        <ScoreAssistant sessionId={sessionId} onScoreRecorded={loadScores} />
        <QuickActions
          isPaused={isPaused}
          isLoading={isLoading}
          onOpenRules={() => setRulesOpen(true)}
          onAskArbiter={() => setArbiterOpen(true)}
          onTogglePause={handleTogglePause}
          onOpenScores={() => setScoresOpen(true)}
        />
        <SessionChatWidget
          messages={chatMessages}
          isStreaming={isChatStreaming}
          onSend={handleChatSend}
          defaultExpanded={false}
        />
      </div>

      {overlays}
    </div>
  );
}
