/**
 * useLiveSession - TanStack Query loader for the canonical LiveGameSession aggregate.
 *
 * ADR-083 Fase 1 (Issue #2501): the live session surface (`/sessions/[id]/live`,
 * `SessionLiveView`) must load `LiveGameSession` (`/api/v1/live-sessions/{id}` →
 * `LiveSessionDto`), the aggregate the wizards actually create. The previous loader
 * (`useSession` → `api.sessions.getById` → the empty `GameSession` shell) returned a
 * 404 for funnel-created sessions, so the surface only ever ran on visual fixtures.
 *
 * Mirrors the `useSession` contract (`GameSessionDto | null`) so the FSM in
 * SessionLiveView keeps the not-found path: `api.liveSessions.getSession` throws
 * "Session not found" on a 404, which we map to `null` here.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

/** Query key factory for live-session queries (distinct from `sessionsKeys`). */
export const liveSessionKeys = {
  all: ['liveSessions'] as const,
  detail: (id: string) => [...liveSessionKeys.all, 'detail', id] as const,
};

/** Sentinel thrown by `api.liveSessions.getSession` when the session does not exist. */
const NOT_FOUND_MESSAGE = 'Session not found';

/**
 * Hook to fetch a single live session by ID.
 *
 * @param sessionId - LiveGameSession ID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with the LiveSessionDto, or null when the session is not found
 */
export function useLiveSession(
  sessionId: string,
  enabled: boolean = true
): UseQueryResult<LiveSessionDto | null, Error> {
  return useQuery({
    queryKey: liveSessionKeys.detail(sessionId),
    queryFn: async (): Promise<LiveSessionDto | null> => {
      try {
        return await api.liveSessions.getSession(sessionId);
      } catch (err) {
        // Preserve the not-found FSM contract: a missing session is `null`, not an error.
        // A real HTTP 404 surfaces as a NotFoundError (ApiError, statusCode 404) whose
        // message comes from the backend body; the 401→null client path makes
        // getSession throw Error('Session not found'). Both map to the not-found shell.
        if (err instanceof ApiError && err.statusCode === 404) return null;
        if (err instanceof Error && err.message === NOT_FOUND_MESSAGE) return null;
        throw err;
      }
    },
    enabled: enabled && !!sessionId,
    staleTime: 30 * 1000,
  });
}
