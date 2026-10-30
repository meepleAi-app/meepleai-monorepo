'use client';

/**
 * useStartNextGame — #2633 WS1 DEC-10.
 *
 * Mutation that starts the next planned game (POST /game-nights/{id}/sessions). On success it
 * invalidates the night-live query so the badge/current-game re-derive from the read model
 * (single source of truth — no optimistic flip). A 409 with code MAX_LIVE_SESSIONS_EXCEEDED is
 * the server-authoritative signal that a session is already live; the view discriminates via
 * `isMaxLiveBlockedError` to mount the blocked modal.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { ApiError, ConflictError, ForbiddenError } from '@/lib/api/core/errors';
import type { StartGameNightSessionResult } from '@/lib/api/schemas/game-nights.schemas';

import { gameNightLiveKeys } from './useGameNightLive';
import { nightLiveDiaryKeys } from './useNightLiveDiary';

export const MAX_LIVE_SESSIONS_EXCEEDED = 'MAX_LIVE_SESSIONS_EXCEEDED';

/** DEC-10: true iff the error is the max-1-live 409 (→ blocked modal, not a generic toast). */
export function isMaxLiveBlockedError(error: unknown): boolean {
  return error instanceof ConflictError && error.code === MAX_LIVE_SESSIONS_EXCEEDED;
}

/**
 * Code of the 422 that `CreateSessionCommandHandler` throws when the game has no rulebook indexed yet
 * (Session Flow v2.1 T4). #4729: nothing showed it, so starting the game just did nothing.
 */
export const KB_NOT_READY = 'kb_not_ready';

/** True iff the start was refused because the game has no ready rulebook. */
export function isKbNotReadyError(error: unknown): boolean {
  return error instanceof ApiError && error.code === KB_NOT_READY;
}

/** #4729: the 422 a user can fix says how. */
export const KB_NOT_READY_MESSAGE =
  'Questo gioco non ha ancora un regolamento pronto: caricalo dalla scheda del gioco, attendi la fine dell’indicizzazione e riprova.';

/**
 * #4752: what to tell the user when starting a game of the night is refused. «Aggiungi partita» used to
 * fail on a route the backend does not have and said nothing: every refusal now has a word.
 */
export function startGameErrorMessage(error: unknown): string {
  if (isMaxLiveBlockedError(error)) {
    return 'C’è già una partita in corso: completala prima di avviarne un’altra.';
  }
  if (isKbNotReadyError(error)) {
    return KB_NOT_READY_MESSAGE;
  }
  if (error instanceof ForbiddenError) {
    return 'Solo l’organizzatore può avviare una partita.';
  }
  // Quota di sessioni, serata che non ammette partite, tetto di partite: il backend li rimappa in un 409 con
  // il proprio testo, in inglese e senza un codice che il client conosca. Un nuovo tentativo non può riuscire,
  // quindi niente «Riprova».
  if (error instanceof ConflictError) return conflictMessage(error.message);
  return 'Impossibile avviare la partita. Riprova.';
}

const GENERIC_CONFLICT_MESSAGE = 'Non è possibile avviare la partita in questo momento.';

/**
 * Le frasi italiane dei rifiuti noti del backend. Il testo del server NON si mostra mai grezzo: il catch di
 * `StartGameNightSessionCommandHandler` trasforma QUALUNQUE `InvalidOperationException` (anche di EF o Npgsql)
 * in un 409 con `ex.Message`, quindi un testo interno potrebbe arrivare all'utente. Ciò che non si riconosce
 * dice la frase generica. I pattern sono ancorati all'inizio del messaggio.
 */
function conflictMessage(serverMessage: string): string {
  const text = serverMessage.trim();

  if (/^Concurrent session limit reached/i.test(text)) {
    return 'Hai raggiunto il numero massimo di sessioni attive del tuo piano: concludine una prima di avviarne un’altra.';
  }

  const atMost = /^A game night can have at most (\d+) sessions?\b/i.exec(text);
  if (atMost) return `Una serata può avere al massimo ${atMost[1]} partite.`;

  const status = /^Cannot add sessions to a (\w+) game night/i.exec(text);
  switch (status?.[1].toLowerCase()) {
    case 'finalized':
    case 'completed':
      return 'La serata è già conclusa: non si possono aggiungere altre partite.';
    case 'cancelled':
      return 'La serata è stata annullata: non si possono aggiungere altre partite.';
    case 'draft':
      return 'La serata non è ancora pubblicata: pubblicala prima di avviare una partita.';
    default:
      return GENERIC_CONFLICT_MESSAGE;
  }
}

export interface StartNextGameVars {
  readonly gameId: string;
  readonly gameTitle: string;
}

export interface UseStartNextGameOptions {
  /**
   * What to do once the game has started. It runs at HOOK level on purpose: a callback passed to `mutate()`
   * is dropped when the mutation is `reset()` or the component unmounts while the request is pending
   * (#4752: closing the picker dialog mid-request started the game on the server and did nothing here).
   */
  readonly onSuccess?: (result: StartGameNightSessionResult) => void;
}

export function useStartNextGame(
  gameNightId: string,
  options?: UseStartNextGameOptions
): UseMutationResult<StartGameNightSessionResult, Error, StartNextGameVars> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ gameId, gameTitle }: StartNextGameVars) =>
      api.gameNights.startNextGame(gameNightId, gameId, gameTitle),
    onSuccess: result => {
      // Refetch BOTH reads: the live sessions gain the new InProgress row AND the diary gains
      // its game_started event — invalidating only the live key would leave the diary mis-grouped
      // until its own staleTime elapsed (#2633 C2 must-fix #6, the two-query race).
      void queryClient.invalidateQueries({ queryKey: gameNightLiveKeys.detail(gameNightId) });
      void queryClient.invalidateQueries({ queryKey: nightLiveDiaryKeys.detail(gameNightId) });
      options?.onSuccess?.(result);
    },
    retry: false,
  });
}
