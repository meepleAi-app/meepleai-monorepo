/**
 * useSessionAgentLaunch — resolves the RAG agent session for a live game session.
 *
 * SP1 epic #2501 (Issue #2500): the live chat panel talks to the RAG agent, which
 * requires an `agentSessionId` from `POST /game-sessions/{id}/agent/launch`. The
 * launch needs an `agentDefinitionId`, which is exactly `AgentDto.id` returned by
 * `GET /games/{gameId}/agents` (verified: `AgentDto.id` IS the AgentDefinition id).
 *
 * Flow (lazy, two dependent TanStack queries — does NOT block the session render):
 *   1. getAgents(gameId) → pick first active agent (`isActive`) else first agent.
 *   2. launch(sessionId, { agentDefinitionId, agentId, gameId }) → { agentSessionId }.
 *
 * The result is a discriminated status the chat panel maps to its UI:
 *   - 'no-agent'  → getAgents resolved empty (no assistant available for this game)
 *   - 'launching' → getAgents/launch in flight
 *   - 'ready'     → agentSessionId obtained, chat can send
 *   - 'error'     → getAgents OR launch failed (panel shows error, never crashes)
 *   - 'idle'      → preconditions not met yet (no sessionId/gameId)
 *
 * AC-CHAT-NULL (review FINDING 5): every non-ready state is explicit so the panel
 * can give feedback — there is never a silent no-op where the user types and nothing
 * happens.
 */

import { useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { AgentDto } from '@/lib/api/schemas';

// ─── Status ─────────────────────────────────────────────────────────────────

export type SessionAgentStatus = 'idle' | 'launching' | 'ready' | 'no-agent' | 'error';

export interface SessionAgentLaunchResult {
  /** Discriminated lifecycle status (see module doc). */
  readonly status: SessionAgentStatus;
  /** The launched agent session id — non-empty only when status === 'ready'. */
  readonly agentSessionId: string;
}

// ─── Query keys ───────────────────────────────────────────────────────────────

export const sessionAgentKeys = {
  all: ['sessionAgent'] as const,
  agents: (gameId: string) => [...sessionAgentKeys.all, 'agents', gameId] as const,
  launch: (sessionId: string, agentDefinitionId: string) =>
    [...sessionAgentKeys.all, 'launch', sessionId, agentDefinitionId] as const,
};

/** Pick the first active agent, falling back to the first agent of any state. */
function pickAgent(agents: ReadonlyArray<AgentDto>): AgentDto | undefined {
  return agents.find(a => a.isActive) ?? agents[0];
}

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

  // ── Step 1: list agents for the game ──────────────────────────────────────
  const agentsQuery = useQuery<AgentDto[], Error>({
    queryKey: sessionAgentKeys.agents(gameId ?? ''),
    queryFn: () => api.games.getAgents(gameId as string),
    enabled: canResolve,
    staleTime: 60_000,
    retry: false,
  });

  const agent = useMemo(
    () => (agentsQuery.data ? pickAgent(agentsQuery.data) : undefined),
    [agentsQuery.data]
  );

  // ── Step 2: launch the agent session once an agent is resolved ────────────
  // Dependent query: only runs after agents resolved to a concrete agent. The
  // key is stable per (session, agentDefinition) so we launch at most once and
  // reuse the cached agentSessionId on re-render (lazy, non-blocking).
  const launchQuery = useQuery({
    queryKey: sessionAgentKeys.launch(sessionId ?? '', agent?.id ?? ''),
    queryFn: () =>
      api.agentSessions.launch(sessionId as string, {
        agentDefinitionId: agent!.id,
        // FE schema requires agentId; the BE ignores it. AgentDto.id IS the
        // AgentDefinition id, so the same value is correct for both fields.
        agentId: agent!.id,
        gameId: gameId as string,
        initialGameStateJson: '{}',
      }),
    enabled: canResolve && agent != null,
    staleTime: Infinity,
    retry: false,
  });

  // ── Derive discriminated status ───────────────────────────────────────────
  const status: SessionAgentStatus = useMemo(() => {
    if (!canResolve) return 'idle';
    if (agentsQuery.isError) return 'error';
    if (agentsQuery.isSuccess && agent == null) return 'no-agent';
    if (launchQuery.isError) return 'error';
    if (launchQuery.isSuccess && launchQuery.data?.agentSessionId) return 'ready';
    return 'launching';
  }, [
    canResolve,
    agent,
    agentsQuery.isError,
    agentsQuery.isSuccess,
    launchQuery.isError,
    launchQuery.isSuccess,
    launchQuery.data,
  ]);

  return {
    status,
    agentSessionId: status === 'ready' ? (launchQuery.data?.agentSessionId ?? '') : '',
  };
}
