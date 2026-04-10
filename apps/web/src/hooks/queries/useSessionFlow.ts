/**
 * useSessionFlow — React Query hooks for Session Flow v2.1 endpoints
 * Plan 2 Task 5 — GameNight diary + complete actions
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { DiaryQueryParams } from '@/lib/api/session-flow/types';

import { gameNightKeys } from './useGameNights';

// ─── Query Keys ─────────────────────────────────────────────────────────────

export const sessionFlowKeys = {
  all: ['session-flow'] as const,
  gameNightDiary: (gameNightId: string) =>
    [...sessionFlowKeys.all, 'game-night-diary', gameNightId] as const,
  sessionDiary: (sessionId: string) =>
    [...sessionFlowKeys.all, 'session-diary', sessionId] as const,
  currentSession: () => [...sessionFlowKeys.all, 'current-session'] as const,
  kbReadiness: (gameId: string) => [...sessionFlowKeys.all, 'kb-readiness', gameId] as const,
};

// ─── Queries ────────────────────────────────────────────────────────────────

/**
 * Fetch the UNION diary for a whole game night (all sessions combined).
 */
export function useGameNightDiaryQuery(gameNightId: string, params?: DiaryQueryParams) {
  return useQuery({
    queryKey: sessionFlowKeys.gameNightDiary(gameNightId),
    queryFn: () => api.sessionFlow.getGameNightDiary(gameNightId, params),
    enabled: !!gameNightId,
  });
}

/**
 * Fetch the diary for a single session.
 */
export function useSessionDiaryQuery(sessionId: string, params?: DiaryQueryParams) {
  return useQuery({
    queryKey: sessionFlowKeys.sessionDiary(sessionId),
    queryFn: () => api.sessionFlow.getSessionDiary(sessionId, params),
    enabled: !!sessionId,
  });
}

/**
 * Fetch the caller's current active/paused session (orphan recovery).
 */
export function useCurrentSessionQuery(enabled = true) {
  return useQuery({
    queryKey: sessionFlowKeys.currentSession(),
    queryFn: () => api.sessionFlow.getCurrentSession(),
    enabled,
  });
}

/**
 * Probe KB readiness for a game.
 */
export function useKbReadinessQuery(gameId: string) {
  return useQuery({
    queryKey: sessionFlowKeys.kbReadiness(gameId),
    queryFn: () => api.sessionFlow.getKbReadiness(gameId),
    enabled: !!gameId,
  });
}

// ─── Mutations ──────────────────────────────────────────────────────────────

/**
 * Complete a game night — cascade-finalize all sessions.
 * Invalidates both session-flow diary and game-night detail queries.
 */
export function useCompleteGameNight() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (gameNightId: string) => api.sessionFlow.completeGameNight(gameNightId),
    onSuccess: (_data, gameNightId) => {
      queryClient.invalidateQueries({ queryKey: sessionFlowKeys.gameNightDiary(gameNightId) });
      queryClient.invalidateQueries({ queryKey: gameNightKeys.detail(gameNightId) });
      queryClient.invalidateQueries({ queryKey: gameNightKeys.all });
    },
  });
}
