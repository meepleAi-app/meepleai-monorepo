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
  mine: () => [...gameNightKeys.all, 'mine'] as const,
  detail: (id: string) => [...gameNightKeys.all, id] as const,
  rsvps: (id: string) => [...gameNightKeys.all, id, 'rsvps'] as const,
};

export function useUpcomingGameNights(enabled: boolean = true) {
  return useQuery({
    queryKey: gameNightKeys.upcoming(),
    queryFn: () => api.gameNights.getUpcoming(),
    enabled,
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
