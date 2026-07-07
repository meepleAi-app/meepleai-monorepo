/**
 * useSessionFlow — React Query hooks for Session Flow v2.1 endpoints
 * Plan 2 Task 5 — GameNight diary + complete actions
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { DiaryQueryParams } from '@/lib/api/session-flow/types';
import { gameNightLiveKeys } from '@/lib/game-nights/hooks/useGameNightLive';
import { gamebookCampaignKeys } from '@/lib/gamebook/hooks/useGamebookCampaign';

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
 * Complete a game night — cascade-finalize all sessions (in-progress → completed).
 *
 * Invalidates the session-flow diary + game-night list/detail queries AND (SI-3 #2634) the
 * night-live read + the whole gamebook campaign family, so every surface reflecting the night's
 * status refetches: the night-live hub strip AND the cross-surface "Serata" spine strip on the
 * gamebook play page. The spine is keyed by campaignId (unknown at finalize time), hence the
 * broad `gamebookCampaignKeys.all` family invalidation rather than a targeted key.
 */
export function useCompleteGameNight() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (gameNightId: string) => api.sessionFlow.completeGameNight(gameNightId),
    onSuccess: (_data, gameNightId) => {
      queryClient.invalidateQueries({ queryKey: sessionFlowKeys.gameNightDiary(gameNightId) });
      queryClient.invalidateQueries({ queryKey: gameNightKeys.detail(gameNightId) });
      queryClient.invalidateQueries({ queryKey: gameNightKeys.all });
      // SI-3: the night-live read (the strip transitions in-progress → completed) …
      queryClient.invalidateQueries({ queryKey: gameNightLiveKeys.detail(gameNightId) });
      // … and every gamebook campaign/spine (cross-surface Serata strip refetch).
      queryClient.invalidateQueries({ queryKey: gamebookCampaignKeys.all });
    },
  });
}
