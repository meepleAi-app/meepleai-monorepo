/**
 * useGameNights — React Query hooks for Game Nights
 * Issue #33 — P3 Game Night Frontend
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  CreateGameNightInput,
  RsvpStatus,
  UpdateGameNightInput,
} from '@/lib/api/schemas/game-nights.schemas';

export const gameNightKeys = {
  all: ['game-nights'] as const,
  upcoming: () => [...gameNightKeys.all, 'upcoming'] as const,
  // F20 #1974: dashboard "Recenti" key; `limit` belongs to the key so
  // different page sizes don't collide on the React Query cache.
  completed: (limit?: number) => [...gameNightKeys.all, 'completed', limit ?? 5] as const,
  mine: () => [...gameNightKeys.all, 'mine'] as const,
  detail: (id: string) => [...gameNightKeys.all, id] as const,
  rsvps: (id: string) => [...gameNightKeys.all, id, 'rsvps'] as const,
  voteTally: (id: string) => [...gameNightKeys.all, id, 'vote-tally'] as const,
  summary: (id: string) => [...gameNightKeys.all, id, 'summary'] as const,
};

export function useUpcomingGameNights(
  options: { enabled?: boolean; retry?: number | boolean } = {}
) {
  const { enabled = true, retry } = options;
  return useQuery({
    queryKey: gameNightKeys.upcoming(),
    queryFn: () => api.gameNights.getUpcoming(),
    enabled,
    ...(retry !== undefined ? { retry } : {}),
  });
}

/**
 * F20 #1974 (audit 2026-06-07): dashboard "Recenti" slot — recently
 * completed game nights for the Asse C P2 dashboard surface.
 */
export function useCompletedGameNights(
  options: { limit?: number; enabled?: boolean; retry?: number | boolean } = {}
) {
  const { limit, enabled = true, retry } = options;
  return useQuery({
    queryKey: gameNightKeys.completed(limit),
    queryFn: () => api.gameNights.getCompleted(limit),
    enabled,
    ...(retry !== undefined ? { retry } : {}),
  });
}

export function useMyGameNights() {
  return useQuery({
    queryKey: gameNightKeys.mine(),
    queryFn: () => api.gameNights.getMine(),
  });
}

export function useGameNight(id: string) {
  return useQuery({
    queryKey: gameNightKeys.detail(id),
    queryFn: () => api.gameNights.getById(id),
    enabled: !!id,
  });
}

export function useGameNightRsvps(id: string) {
  return useQuery({
    queryKey: gameNightKeys.rsvps(id),
    queryFn: () => api.gameNights.getRsvps(id),
    enabled: !!id,
  });
}

export function useCreateGameNight() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateGameNightInput) => api.gameNights.create(input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.all });
    },
  });
}

export function useUpdateGameNight() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateGameNightInput }) =>
      api.gameNights.update(id, input),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: gameNightKeys.all });
    },
  });
}

export function usePublishGameNight() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.gameNights.publish(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.all });
    },
  });
}

export function useCancelGameNight() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.gameNights.cancel(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.all });
    },
  });
}

export function useRsvpGameNight() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, response }: { id: string; response: RsvpStatus }) =>
      api.gameNights.rsvp(id, response),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: gameNightKeys.rsvps(id) });
      queryClient.invalidateQueries({ queryKey: gameNightKeys.all });
    },
  });
}

// ── Candidate voting (approval model) — Issue #2700 ──────────────────────

export function useGameNightVoteTally(id: string, options: { enabled?: boolean } = {}) {
  const { enabled = true } = options;
  return useQuery({
    queryKey: gameNightKeys.voteTally(id),
    queryFn: () => api.gameNights.getVoteTally(id),
    enabled: enabled && !!id,
  });
}

export function useCastGameNightVote() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, candidateGameId }: { id: string; candidateGameId: string }) =>
      api.gameNights.castVote(id, candidateGameId),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.voteTally(id) });
    },
  });
}

export function useRetractGameNightVote() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, candidateGameId }: { id: string; candidateGameId: string }) =>
      api.gameNights.retractVote(id, candidateGameId),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.voteTally(id) });
    },
  });
}

export function useResolveGameNightVotingTie() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, winningCandidateGameId }: { id: string; winningCandidateGameId: string }) =>
      api.gameNights.resolveVotingTie(id, winningCandidateGameId),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.voteTally(id) });
    },
  });
}

// ── Summary + share-token + archive — Issue #2702 ────────────────────────

export function useGameNightSummary(id: string, options: { enabled?: boolean } = {}) {
  const { enabled = true } = options;
  return useQuery({
    queryKey: gameNightKeys.summary(id),
    queryFn: () => api.gameNights.getSummary(id),
    enabled: enabled && !!id,
  });
}

export function useSharedGameNightSummary(token: string) {
  return useQuery({
    queryKey: [...gameNightKeys.all, 'shared', token] as const,
    queryFn: () => api.gameNights.getSharedSummary(token),
    enabled: !!token,
    retry: false,
  });
}

export function useGenerateGameNightShareToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.gameNights.generateShareToken(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.summary(id) });
    },
  });
}

export function useSetGameNightArchived() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) =>
      api.gameNights.setArchived(id, archived),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: gameNightKeys.summary(id) });
    },
  });
}
