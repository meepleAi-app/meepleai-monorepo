'use client';

import { useCallback, useState } from 'react';

import { createSession, finalizeSession } from '@/lib/api/clients/gameSessionsClient';
import { useSessionStore } from '@/stores/session/store';
import type { SessionParticipant } from '@/stores/session/types';

interface StartGamePayload {
  gameId: string;
  gameTitle: string;
  participants: SessionParticipant[];
}

interface UseGameNightOrchestrator {
  /** Sessioni completate nella serata */
  completedGames: Array<{ gameTitle: string; sessionId: string }>;
  isStarting: boolean;
  error: string | null;
  /** Avvia il primo gioco della serata */
  startGame: (payload: StartGamePayload) => Promise<void>;
  /** Finalizza il gioco corrente e ne inizia uno nuovo */
  startNextGame: (payload: StartGamePayload) => Promise<void>;
}

export function useGameNightOrchestrator(gameNightId: string): UseGameNightOrchestrator {
  // Granular selectors — avoids re-renders on unrelated store changes
  const sessionId = useSessionStore(s => s.sessionId);
  const gameTitle = useSessionStore(s => s.gameTitle);
  const startSession = useSessionStore(s => s.startSession);
  const reset = useSessionStore(s => s.reset);

  const [completedGames, setCompletedGames] = useState<
    Array<{ gameTitle: string; sessionId: string }>
  >([]);
  const [isStarting, setIsStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const startGame = useCallback(
    async (payload: StartGamePayload) => {
      setIsStarting(true);
      setError(null);
      try {
        const response = await createSession({
          gameNightId,
          gameId: payload.gameId,
          participants: payload.participants.map(p => ({
            displayName: p.displayName,
            userId: p.userId,
            isGuest: p.isGuest,
          })),
        });

        startSession({
          sessionId: response.sessionId,
          gameId: payload.gameId,
          gameTitle: payload.gameTitle,
          participants: payload.participants,
        });
      } catch (err: unknown) {
        const status =
          (err as { status?: number; statusCode?: number })?.status ??
          (err as { status?: number; statusCode?: number })?.statusCode;
        if (status === 409) {
          setError(
            'Una partita è già attiva per questa serata. Finalizzala prima di iniziarne una nuova.'
          );
        } else {
          setError('Impossibile avviare la partita. Riprova.');
        }
        throw err;
      } finally {
        setIsStarting(false);
      }
    },
    [gameNightId, startSession]
  );

  const startNextGame = useCallback(
    async (payload: StartGamePayload) => {
      // 1. Finalizza sessione corrente (best-effort)
      if (sessionId) {
        try {
          await finalizeSession(sessionId);
          if (gameTitle) {
            setCompletedGames(prev => [...prev, { gameTitle, sessionId }]);
          }
        } catch {
          // Continua comunque — la finalizzazione può essere ritentata
        }
      }

      // 2. Reset store per il nuovo gioco
      reset();

      // 3. Avvia nuovo gioco
      await startGame(payload);
    },
    [sessionId, gameTitle, reset, startGame]
  );

  return { completedGames, isStarting, error, startGame, startNextGame };
}
