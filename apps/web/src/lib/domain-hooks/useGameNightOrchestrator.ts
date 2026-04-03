'use client';

import { useCallback, useRef, useState } from 'react';

import { createSession, finalizeSession } from '@/lib/api/clients/gameSessionsClient';
import { ConflictError } from '@/lib/api/core/errors';
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
  const startSession = useSessionStore(s => s.startSession);
  const reset = useSessionStore(s => s.reset);

  const [completedGames, setCompletedGames] = useState<
    Array<{ gameTitle: string; sessionId: string }>
  >([]);
  const [isStarting, setIsStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isStartingRef = useRef(false);

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
        if (err instanceof ConflictError) {
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
      // Synchronous ref-based guard against double-tap (state updates are async)
      if (isStartingRef.current) return;
      isStartingRef.current = true;

      try {
        // Read latest state atomically to avoid stale closure issues
        // (SSE or other tabs may have updated the store between renders)
        const currentState = useSessionStore.getState();
        const currentSessionId = currentState.sessionId;
        const currentGameTitle = currentState.gameTitle;

        // 1. Finalizza sessione corrente (best-effort)
        if (currentSessionId) {
          try {
            await finalizeSession(currentSessionId);
            if (currentGameTitle) {
              setCompletedGames(prev => [
                ...prev,
                { gameTitle: currentGameTitle, sessionId: currentSessionId },
              ]);
            }
          } catch {
            // Continua comunque — la finalizzazione può essere ritentata
          }
        }

        // 2. Reset store per il nuovo gioco
        reset();

        // 3. Avvia nuovo gioco
        await startGame(payload);
      } finally {
        isStartingRef.current = false;
      }
    },
    [reset, startGame]
  );

  return { completedGames, isStarting, error, startGame, startNextGame };
}
