'use client';

import { useCallback, useRef, useState } from 'react';

import { createSession, finalizeSession, goLive } from '@/lib/api/clients/gameSessionsClient';
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
  // Issue #3217: tracks the draft created but not yet promoted to live. If go-live fails, the
  // draft would otherwise be orphaned — an Active tracking session counting against the global
  // session quota (SessionQuotaService, Free tier = 3). Keeping the ref lets a same-game retry
  // REUSE the existing draft (go-live is idempotent/self-healing since #3218) instead of piling
  // up new drafts, so at most ONE pending draft exists at any time.
  const pendingDraftRef = useRef<{ sessionId: string; gameId: string } | null>(null);

  // Core start routine WITHOUT the double-tap guard. Both public entry points (startGame,
  // startNextGame) acquire the shared isStartingRef guard exactly once, then delegate here —
  // this keeps ONE guard consistent across the two and avoids the self-deadlock that would
  // occur if startNextGame set the ref and then called a self-guarding startGame.
  const startGameCore = useCallback(
    async (payload: StartGamePayload) => {
      setIsStarting(true);
      setError(null);
      try {
        // Resolve the sessionId to promote WITHOUT accumulating orphan drafts.
        let sessionId: string;
        const pending = pendingDraftRef.current;

        if (pending && pending.gameId === payload.gameId) {
          // Same-game retry: a previous attempt already created this draft but go-live failed.
          // Reuse it (go-live self-heals per #3218) — do NOT create a duplicate draft.
          sessionId = pending.sessionId;
        } else {
          // A draft for a DIFFERENT game is lingering — best-effort compensate it so it doesn't
          // count against the quota, then fall through to create a fresh draft.
          if (pending) {
            try {
              await finalizeSession(pending.sessionId);
            } catch {
              // best-effort compensation — swallow; the draft can still be reaped server-side
            }
            pendingDraftRef.current = null;
          }

          // Epic #3188 Slice 3 (D5): create now yields a DRAFT (Pending). startGame's intent is
          // to START PLAYING, so promote the fresh draft to live via the go-live sub-resource.
          const response = await createSession({
            gameNightId,
            gameId: payload.gameId,
            participants: payload.participants.map(p => ({
              displayName: p.displayName,
              userId: p.userId,
              isGuest: p.isGuest,
            })),
          });
          sessionId = response.sessionId;
          pendingDraftRef.current = { sessionId, gameId: payload.gameId };
        }

        await goLive(sessionId);

        // Success: the draft is now live — it is no longer "pending". Clear the ref so a later
        // start doesn't mistake this (now-live) session for an abandoned draft.
        pendingDraftRef.current = null;

        startSession({
          sessionId,
          gameId: payload.gameId,
          gameTitle: payload.gameTitle,
          participants: payload.participants,
        });
      } catch (err: unknown) {
        // Keep pendingDraftRef so a same-game retry reuses the draft instead of creating a new one.
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

  const startGame = useCallback(
    async (payload: StartGamePayload) => {
      // Synchronous ref-based guard against double-tap (state updates are async). Previously
      // startGame lacked this, so a double-tap created multiple orphan drafts (issue #3217).
      if (isStartingRef.current) return;
      isStartingRef.current = true;
      try {
        await startGameCore(payload);
      } finally {
        isStartingRef.current = false;
      }
    },
    [startGameCore]
  );

  const startNextGame = useCallback(
    async (payload: StartGamePayload) => {
      // Same shared guard as startGame; acquire it here and delegate to startGameCore (NOT
      // startGame) so the guard isn't re-checked from within — that would self-deadlock.
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

        // 3. Avvia nuovo gioco (senza riacquisire il guard condiviso)
        await startGameCore(payload);
      } finally {
        isStartingRef.current = false;
      }
    },
    [reset, startGameCore]
  );

  return { completedGames, isStarting, error, startGame, startNextGame };
}
