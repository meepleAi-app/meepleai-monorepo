/**
 * GuestSessionView — Read-only session view for QR code joiners
 *
 * Phase 5: Game Night — Task 5
 *
 * Loads session by code via public API, shows:
 * - Game name and session info
 * - Read-only LiveScoreboard
 * - "Non serve registrazione" message
 *
 * No auth required.
 */

'use client';

import { useEffect, useState, useCallback } from 'react';

import { Loader2, Users, Trophy, Gamepad2, Clock } from 'lucide-react';

import { LiveScoreboard } from '@/components/game-night/LiveScoreboard';
import type { LiveScoreboardPlayer } from '@/components/game-night/LiveScoreboard';
import { Button } from '@/components/ui/primitives/button';
import { GlassCard } from '@/components/ui/surfaces/GlassCard';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

// ========== Color map ==========

const PLAYER_COLOR_HEX: Record<string, string> = {
  Red: '#ef4444',
  Blue: '#3b82f6',
  Green: '#22c55e',
  Yellow: '#eab308',
  Purple: '#a855f7',
  Orange: '#f97316',
  White: '#e2e8f0',
  Black: '#1f2937',
  Pink: '#ec4899',
  Teal: '#14b8a6',
};

// ========== Types ==========

type ViewState = 'loading' | 'loaded' | 'error';

// ========== Props ==========

export interface GuestSessionViewProps {
  code: string;
}

// ========== Component ==========

export function GuestSessionView({ code }: GuestSessionViewProps) {
  const [viewState, setViewState] = useState<ViewState>('loading');
  const [session, setSession] = useState<LiveSessionDto | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const loadSession = useCallback(async () => {
    setViewState('loading');
    setErrorMessage(null);

    try {
      const res = await fetch(`/api/v1/live-sessions/code/${encodeURIComponent(code)}`);
      if (!res.ok) {
        setErrorMessage('Sessione non trovata o codice non valido.');
        setViewState('error');
        return;
      }

      const data = (await res.json()) as LiveSessionDto;
      setSession(data);
      setViewState('loaded');
    } catch {
      setErrorMessage('Errore di connessione. Riprova tra qualche secondo.');
      setViewState('error');
    }
  }, [code]);

  useEffect(() => {
    loadSession();
  }, [loadSession]);

  // ── Loading ───────────────────────────────────────────────────────────────────

  if (viewState === 'loading') {
    return (
      <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 dark:from-slate-900 dark:to-slate-800 flex items-center justify-center p-4">
        <div className="flex flex-col items-center gap-3">
          <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
          <p className="font-nunito text-gray-600 dark:text-gray-400">Caricamento sessione...</p>
        </div>
      </main>
    );
  }

  // ── Error ─────────────────────────────────────────────────────────────────────

  if (viewState === 'error') {
    return (
      <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 dark:from-slate-900 dark:to-slate-800 flex items-center justify-center p-4">
        <div className="max-w-sm w-full text-center space-y-4">
          <div className="h-16 w-16 mx-auto rounded-2xl bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
            <Users className="h-8 w-8 text-red-500" />
          </div>
          <h1 className="text-xl font-quicksand font-bold text-gray-900 dark:text-gray-100">
            Sessione non trovata
          </h1>
          <p className="text-sm font-nunito text-gray-600 dark:text-gray-400">
            {errorMessage ?? 'Il codice potrebbe essere scaduto o non valido.'}
          </p>
          <Button onClick={() => loadSession()} variant="outline" className="w-full">
            Riprova
          </Button>
        </div>
      </main>
    );
  }

  // ── Loaded ────────────────────────────────────────────────────────────────────

  if (!session) return null;

  const activePlayers = session.players.filter(p => p.isActive);

  const scoreboardPlayers: LiveScoreboardPlayer[] = activePlayers.map(p => ({
    id: p.id,
    displayName: p.displayName,
    totalScore: p.totalScore,
    avatarColor: PLAYER_COLOR_HEX[p.color] ?? '#6b7280',
  }));

  const statusLabels: Record<string, string> = {
    Created: 'In preparazione',
    Setup: 'In preparazione',
    InProgress: 'In corso',
    Paused: 'In pausa',
    Completed: 'Completata',
  };

  const statusLabel = statusLabels[session.status] ?? session.status;

  return (
    <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 dark:from-slate-900 dark:to-slate-800 p-4">
      <div className="max-w-sm mx-auto space-y-4">
        {/* Header */}
        <div className="text-center pt-4 pb-2 space-y-1">
          <div className="h-14 w-14 mx-auto rounded-2xl bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center mb-3">
            <Gamepad2 className="h-7 w-7 text-amber-600 dark:text-amber-400" />
          </div>
          <h1 className="text-2xl font-quicksand font-bold text-gray-900 dark:text-gray-100">
            {session.gameName}
          </h1>
          <div className="flex items-center justify-center gap-2 text-sm text-gray-500 dark:text-gray-400">
            <span className="inline-flex items-center gap-1 bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400 px-2 py-0.5 rounded-full text-xs font-medium">
              <Clock className="h-3 w-3" />
              {statusLabel}
            </span>
            <span className="text-xs">
              Codice: <span className="font-mono font-bold">{session.sessionCode}</span>
            </span>
          </div>
        </div>

        {/* Scoreboard */}
        <GlassCard entity="session" className="p-4">
          <div className="flex items-center gap-2 mb-3">
            <Trophy className="h-4 w-4 text-amber-500" />
            <h2 className="font-semibold text-sm">Classifica</h2>
          </div>
          {scoreboardPlayers.length > 0 ? (
            <LiveScoreboard players={scoreboardPlayers} isRealTime />
          ) : (
            <p className="text-sm text-muted-foreground text-center py-4">
              Nessun giocatore ancora
            </p>
          )}
        </GlassCard>

        {/* Players info */}
        <GlassCard entity="session" className="p-4">
          <div className="flex items-center gap-2 mb-3">
            <Users className="h-4 w-4 text-amber-500" />
            <h2 className="font-semibold text-sm">Giocatori ({activePlayers.length})</h2>
          </div>
          <div className="flex flex-wrap gap-2">
            {activePlayers.map(player => (
              <div
                key={player.id}
                className="flex items-center gap-1.5 rounded-full bg-white/60 dark:bg-white/10 px-3 py-1"
              >
                <div
                  className="h-3 w-3 rounded-full"
                  style={{ backgroundColor: PLAYER_COLOR_HEX[player.color] ?? '#6b7280' }}
                />
                <span className="text-xs font-medium">{player.displayName}</span>
              </div>
            ))}
          </div>
        </GlassCard>

        {/* No registration message */}
        <div className="text-center py-4">
          <p className="text-xs text-gray-400 dark:text-gray-500">
            Non serve registrazione per visualizzare questa partita.
          </p>
        </div>
      </div>
    </main>
  );
}
