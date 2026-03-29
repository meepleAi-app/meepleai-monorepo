/**
 * PlayModeMobile — Full mobile play experience with 4-tab layout
 *
 * Phase 5: Game Night — Task 4
 *
 * Tabs:
 *   Gioco    — timer, QuickToolBar, tool sheet
 *   Punteggi — LiveScoreboard, tap→ScoreNumpad
 *   Chiedi   — CTA to chat
 *   Giocatori — player list
 *
 * Uses SessionBottomNav for tab navigation and MobileHeader for the header.
 */

'use client';

import { useState, useEffect, useCallback, useRef } from 'react';

import { Timer, MessageCircle, Crown, LogOut } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { LiveScoreboard } from '@/components/game-night/LiveScoreboard';
import type { LiveScoreboardPlayer } from '@/components/game-night/LiveScoreboard';
import { QuickToolBar } from '@/components/session/QuickToolBar';
import type { ToolId } from '@/components/session/QuickToolBar';
import { ScoreNumpad } from '@/components/session/ScoreNumpad';
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { SessionBottomNav, type SessionTab } from '@/components/ui/navigation/SessionBottomNav';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { PLAYER_COLOR_HEX } from '@/lib/constants/player-colors';
import { useSessionSync } from '@/lib/domain-hooks/useSessionSync';
import { useSessionStore } from '@/lib/stores/sessionStore';
import { cn } from '@/lib/utils';

// ========== Props ==========

interface PlayModeMobileProps {
  sessionId: string;
}

// ========== Component ==========

export function PlayModeMobile({ sessionId }: PlayModeMobileProps) {
  const router = useRouter();

  // Session store
  const session = useSessionStore(s => s.activeSession);
  const scores = useSessionStore(s => s.scores);
  const isLoading = useSessionStore(s => s.isLoading);
  const loadSession = useSessionStore(s => s.loadSession);
  const recordScore = useSessionStore(s => s.recordScore);
  const completeSession = useSessionStore(s => s.completeSession);

  // Tabs
  const [activeTab, setActiveTab] = useState<SessionTab>('game');

  // Tool sheet
  const [activeTool, setActiveTool] = useState<ToolId | null>(null);
  const [toolSheetOpen, setToolSheetOpen] = useState(false);

  // Score numpad
  const [scoreTarget, setScoreTarget] = useState<{
    playerId: string;
    playerName: string;
    currentScore: number;
  } | null>(null);

  // End session confirm
  const [showEndConfirm, setShowEndConfirm] = useState(false);
  const [sessionEnded, setSessionEnded] = useState(false);

  // Timer
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // ── Load session on mount ───────────────────────────────────────────────────

  useEffect(() => {
    if (!session || session.id !== sessionId) {
      loadSession(sessionId).catch(() => {
        // error is set in the store
      });
    }
  }, [sessionId, session, loadSession]);

  // ── SSE sync ────────────────────────────────────────────────────────────────

  useSessionSync({ sessionId });

  // ── Timer ───────────────────────────────────────────────────────────────────

  useEffect(() => {
    if (session?.startedAt) {
      const startTime = new Date(session.startedAt).getTime();
      const tick = () => {
        setElapsedSeconds(Math.floor((Date.now() - startTime) / 1000));
      };
      tick();
      timerRef.current = setInterval(tick, 1000);
    } else {
      // Start from mount if no startedAt
      const mountTime = Date.now();
      timerRef.current = setInterval(() => {
        setElapsedSeconds(Math.floor((Date.now() - mountTime) / 1000));
      }, 1000);
    }

    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
    };
  }, [session?.startedAt]);

  const formatTime = (seconds: number) => {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
    return `${m}:${String(s).padStart(2, '0')}`;
  };

  // ── Tool selection ──────────────────────────────────────────────────────────

  const handleSelectTool = useCallback((tool: ToolId) => {
    setActiveTool(tool);
    setToolSheetOpen(true);
  }, []);

  // ── Score submission ────────────────────────────────────────────────────────

  const handleScoreSubmit = useCallback(
    async (value: number) => {
      if (!scoreTarget) return;
      try {
        await recordScore({
          playerId: scoreTarget.playerId,
          round: 1,
          dimension: 'points',
          value,
        });
      } catch {
        // error in store
      }
      setScoreTarget(null);
    },
    [scoreTarget, recordScore]
  );

  // ── End session ─────────────────────────────────────────────────────────────

  const handleEndSession = useCallback(async () => {
    try {
      await completeSession();
      setSessionEnded(true);
      setShowEndConfirm(false);
    } catch {
      // error in store
    }
  }, [completeSession]);

  // ── Derived data ────────────────────────────────────────────────────────────

  const players = session?.players ?? [];

  const scoreboardPlayers: LiveScoreboardPlayer[] = players
    .filter(p => p.isActive)
    .map(p => {
      // Sum scores for this player
      const totalScore = scores
        .filter(s => s.playerId === p.id)
        .reduce((sum, s) => sum + s.value, 0);

      return {
        id: p.id,
        displayName: p.displayName,
        totalScore: p.totalScore + totalScore,
        avatarColor: PLAYER_COLOR_HEX[p.color] ?? '#6b7280',
      };
    });

  // ── Loading state ───────────────────────────────────────────────────────────

  if (isLoading && !session) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-sm text-muted-foreground">Caricamento sessione...</p>
      </div>
    );
  }

  // ── Session Ended Summary ───────────────────────────────────────────────────

  if (sessionEnded || session?.status === 'Completed') {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center p-6 space-y-6">
        <div className="text-center space-y-2">
          <h1 className="text-2xl font-bold font-quicksand">Partita completata!</h1>
          <p className="text-sm text-muted-foreground">{session?.gameName ?? 'Sessione'}</p>
        </div>

        <div className="w-full max-w-sm">
          <LiveScoreboard players={scoreboardPlayers} className="w-full" />
        </div>

        <GradientButton fullWidth size="lg" onClick={() => router.push('/sessions')}>
          Torna alle sessioni
        </GradientButton>
      </div>
    );
  }

  // ── Main play view ──────────────────────────────────────────────────────────

  return (
    <div className="flex flex-col min-h-screen pb-[var(--size-session-bottom-nav,56px)]">
      {/* Header */}
      <MobileHeader
        title={session?.gameName ?? 'Sessione'}
        subtitle={`Turno ${(session?.currentTurnIndex ?? 0) + 1}`}
        onBack={() => router.back()}
        rightActions={
          <button
            onClick={() => setShowEndConfirm(true)}
            className="flex items-center gap-1 text-xs font-medium text-red-400 hover:text-red-300 px-2 py-1 rounded-lg hover:bg-white/5"
          >
            <LogOut className="h-3.5 w-3.5" />
            Termina
          </button>
        }
      />

      {/* Tab content */}
      <div className="flex-1 overflow-y-auto">
        {/* ——— Tab: Gioco ——— */}
        {activeTab === 'game' && (
          <div className="p-4 space-y-6">
            {/* Timer */}
            <div className="flex flex-col items-center gap-1">
              <Timer className="h-5 w-5 text-amber-500" />
              <span className="text-3xl font-mono font-bold tabular-nums text-[var(--gaming-text-primary,white)]">
                {formatTime(elapsedSeconds)}
              </span>
              <span className="text-xs text-muted-foreground">Tempo di gioco</span>
            </div>

            {/* Quick tools */}
            <QuickToolBar activeTool={activeTool} onSelectTool={handleSelectTool} />

            {/* Scoreboard summary */}
            <div>
              <h3 className="text-sm font-semibold mb-2 text-[var(--gaming-text-secondary,#ccc)]">
                Classifica
              </h3>
              <LiveScoreboard players={scoreboardPlayers} isRealTime />
            </div>
          </div>
        )}

        {/* ——— Tab: Punteggi ——— */}
        {activeTab === 'scores' && (
          <div className="p-4 space-y-4">
            <h3 className="text-sm font-semibold text-[var(--gaming-text-secondary,#ccc)]">
              Tocca un giocatore per aggiornare il punteggio
            </h3>
            <div className="space-y-2">
              {scoreboardPlayers.map(player => (
                <button
                  key={player.id}
                  onClick={() =>
                    setScoreTarget({
                      playerId: player.id,
                      playerName: player.displayName,
                      currentScore: player.totalScore,
                    })
                  }
                  className={cn(
                    'flex items-center gap-3 w-full rounded-xl px-4 py-3 text-left transition-colors',
                    'bg-white/5 hover:bg-white/10 border border-white/10'
                  )}
                >
                  <div
                    className="h-8 w-8 rounded-lg flex items-center justify-center text-xs font-bold text-white"
                    style={{ backgroundColor: player.avatarColor }}
                  >
                    {player.displayName.slice(0, 2).toUpperCase()}
                  </div>
                  <span className="flex-1 font-medium text-sm">{player.displayName}</span>
                  <span className="font-mono text-lg font-bold tabular-nums">
                    {player.totalScore}
                  </span>
                </button>
              ))}
            </div>
          </div>
        )}

        {/* ——— Tab: Chiedi ——— */}
        {activeTab === 'chat' && (
          <div className="flex flex-col items-center justify-center p-8 space-y-6 min-h-[60vh]">
            <div className="h-20 w-20 rounded-2xl bg-amber-500/10 flex items-center justify-center">
              <MessageCircle className="h-10 w-10 text-amber-500" />
            </div>
            <div className="text-center space-y-2">
              <h3 className="text-lg font-bold font-quicksand">Chiedi all&apos;assistente AI</h3>
              <p className="text-sm text-muted-foreground max-w-xs">
                Hai dubbi sulle regole? Chiedi al nostro assistente e ottieni risposte immediate.
              </p>
            </div>
            <Link
              href={session?.gameId ? `/chat?gameId=${session.gameId}` : '/chat'}
              className="w-full max-w-xs"
            >
              <GradientButton fullWidth size="lg">
                Apri Chat AI
              </GradientButton>
            </Link>
          </div>
        )}

        {/* ——— Tab: Giocatori ——— */}
        {activeTab === 'players' && (
          <div className="p-4 space-y-4">
            <h3 className="text-sm font-semibold text-[var(--gaming-text-secondary,#ccc)]">
              Giocatori ({players.filter(p => p.isActive).length})
            </h3>
            <div className="space-y-2">
              {players
                .filter(p => p.isActive)
                .map((player, index) => {
                  const isTurn = session?.currentTurnPlayerId === player.id;
                  const playerScore = scoreboardPlayers.find(s => s.id === player.id);
                  return (
                    <div
                      key={player.id}
                      className={cn(
                        'flex items-center gap-3 rounded-xl px-4 py-3 border transition-all',
                        isTurn
                          ? 'border-amber-500/50 bg-amber-500/10'
                          : 'border-white/10 bg-white/5'
                      )}
                    >
                      {/* Avatar */}
                      <div
                        className="h-10 w-10 rounded-lg flex items-center justify-center text-sm font-bold text-white shadow-sm"
                        style={{ backgroundColor: PLAYER_COLOR_HEX[player.color] ?? '#6b7280' }}
                      >
                        {player.displayName.slice(0, 2).toUpperCase()}
                      </div>

                      {/* Info */}
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-1.5">
                          <span className="font-semibold text-sm truncate">
                            {player.displayName}
                          </span>
                          {player.role === 'Host' && (
                            <Crown className="h-3.5 w-3.5 text-amber-500 shrink-0" />
                          )}
                          {isTurn && (
                            <span className="text-[10px] font-medium text-amber-400 bg-amber-500/20 px-1.5 py-0.5 rounded">
                              TURNO
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-muted-foreground">
                          #{index + 1} &middot; {player.color}
                        </p>
                      </div>

                      {/* Score */}
                      <div className="text-right">
                        <span className="font-mono text-lg font-bold tabular-nums">
                          {playerScore?.totalScore ?? player.totalScore}
                        </span>
                        <span className="text-xs text-muted-foreground ml-1">pts</span>
                      </div>
                    </div>
                  );
                })}
            </div>
          </div>
        )}
      </div>

      {/* Bottom nav */}
      <SessionBottomNav activeTab={activeTab} onTabChange={setActiveTab} />

      {/* Tool BottomSheet */}
      <BottomSheet
        open={toolSheetOpen}
        onOpenChange={setToolSheetOpen}
        title={activeTool ? activeTool.charAt(0).toUpperCase() + activeTool.slice(1) : 'Strumento'}
      >
        <div className="flex items-center justify-center py-12">
          <p className="text-sm text-muted-foreground">
            Strumento: <span className="font-semibold">{activeTool}</span>
          </p>
        </div>
      </BottomSheet>

      {/* Score Numpad BottomSheet */}
      <BottomSheet
        open={!!scoreTarget}
        onOpenChange={open => {
          if (!open) setScoreTarget(null);
        }}
        title="Aggiorna punteggio"
      >
        {scoreTarget && (
          <ScoreNumpad
            playerName={scoreTarget.playerName}
            currentScore={scoreTarget.currentScore}
            onSubmit={handleScoreSubmit}
            onClose={() => setScoreTarget(null)}
          />
        )}
      </BottomSheet>

      {/* End Session Confirm BottomSheet */}
      <BottomSheet open={showEndConfirm} onOpenChange={setShowEndConfirm} title="Termina partita">
        <div className="space-y-4 py-4">
          <p className="text-sm text-muted-foreground text-center">
            Sei sicuro di voler terminare la partita? I punteggi verranno salvati.
          </p>
          <div className="flex gap-3">
            <button
              onClick={() => setShowEndConfirm(false)}
              className="flex-1 rounded-xl bg-white/10 py-3 text-sm font-medium hover:bg-white/20"
            >
              Annulla
            </button>
            <button
              onClick={handleEndSession}
              className="flex-1 rounded-xl bg-red-500 py-3 text-sm font-medium text-white hover:bg-red-600"
            >
              Termina
            </button>
          </div>
        </div>
      </BottomSheet>
    </div>
  );
}
