/**
 * useSessionAgentLaunch — resolves the RAG agent session for a live game session.
 *
 * SP1 epic #2501 (Issue #2500): the live chat panel talks to the RAG agent, which
 * requires an `agentSessionId` from `POST /game-sessions/{id}/agent/launch`.
 *
 * Issue #4154 (ADR-095 fetta 0b): il lancio NON scarica più un elenco di agenti e non passa un id.
 * Con ADR-094 l'agente è uno solo, di sistema, e lo sceglie il backend. Prima l'hook leggeva
 * `GET /games/{gameId}/agents`, che dal 2026-04-18 (#470) risponde con un oggetto mentre il client
 * validava un array; la lista era comunque vuota con l'agente di sistema inattivo, e il validator
 * del lancio bocciava l'agente di sistema per ogni gioco. L'assistente non partiva mai.
 *
 * Flow (lazy, one TanStack query — does NOT block the session render):
 *   launch(sessionId, { gameId }) → { agentSessionId }.
 *
 * The result is a discriminated status the chat panel maps to its UI:
 *   - 'no-agent'  → the backend reports `system_agent_unavailable` (no assistant configured/active)
 *   - 'launching' → launch in flight
 *   - 'ready'     → agentSessionId obtained, chat can send
 *   - 'error'     → launch failed for any other reason (panel shows error, never crashes)
 *   - 'idle'      → preconditions not met yet (no sessionId/gameId)
 *
 * AC-CHAT-NULL (review FINDING 5): every non-ready state is explicit so the panel
 * can give feedback — there is never a silent no-op where the user types and nothing
 * happens.
 */

import { useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';

// ─── Status ─────────────────────────────────────────────────────────────────

export type SessionAgentStatus = 'idle' | 'launching' | 'ready' | 'no-agent' | 'error';

export interface SessionAgentLaunchResult {
  /** Discriminated lifecycle status (see module doc). */
  readonly status: SessionAgentStatus;
  /** The launched agent session id — non-empty only when status === 'ready'. */
  readonly agentSessionId: string;
}

/** Codice che il backend usa quando l'agente di sistema manca o non è attivo (#4154). */
export const SYSTEM_AGENT_UNAVAILABLE = 'system_agent_unavailable';

// ─── Query keys ───────────────────────────────────────────────────────────────

export const sessionAgentKeys = {
  all: ['sessionAgent'] as const,
  launch: (sessionId: string) => [...sessionAgentKeys.all, 'launch', sessionId] as const,
};

/**
 * Resolve (and lazily launch) the RAG agent session for a live game session.
 *
 * @param sessionId - LiveGameSession id (path param for launch + agent chat).
 * @param gameId    - Game id from the LiveSessionDto (nullable on the aggregate).
 * @param enabled   - Gate the whole flow (e.g. disabled in visual-test builds).
 */
export function useSessionAgentLaunch(
  sessionId: string | null,
  gameId: string | null,
  enabled: boolean = true
): SessionAgentLaunchResult {
  const canResolve = enabled && !!sessionId && !!gameId;

  // The key is stable per session, so we launch at most once and reuse the cached
  // agentSessionId on re-render (lazy, non-blocking).
  const launchQuery = useQuery({
    queryKey: sessionAgentKeys.launch(sessionId ?? ''),
    queryFn: () =>
      api.agentSessions.launch(sessionId as string, {
        gameId: gameId as string,
        // C1 fix: send empty string so BE uses GameState.Initial(UserId) as default.
        // Sending '{}' caused GameState.FromJson('{}') to throw (ActivePlayer == Guid.Empty).
        initialGameStateJson: '',
      }),
    enabled: canResolve,
    staleTime: Infinity,
    retry: false,
  });

  // ── Derive discriminated status ───────────────────────────────────────────
  const status: SessionAgentStatus = useMemo(() => {
    if (!canResolve) return 'idle';
    if (launchQuery.isError) {
      const error = launchQuery.error;
      return error instanceof ApiError && error.code === SYSTEM_AGENT_UNAVAILABLE
        ? 'no-agent'
        : 'error';
    }
    if (launchQuery.isSuccess && launchQuery.data?.agentSessionId) return 'ready';
    return 'launching';
  }, [canResolve, launchQuery.isError, launchQuery.error, launchQuery.isSuccess, launchQuery.data]);

  return {
    status,
    agentSessionId: status === 'ready' ? (launchQuery.data?.agentSessionId ?? '') : '',
  };
}
