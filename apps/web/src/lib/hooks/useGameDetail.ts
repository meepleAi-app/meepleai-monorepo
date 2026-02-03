/**
 * Game Detail Hook (Issue #2832)
 *
 * React Query + Zustand integration for game detail page with optimistic updates
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { useGameDetailStore } from '@/lib/stores/useGameDetailStore';

export const GAME_DETAIL_QUERY_KEY = (gameId: string) => ['game-detail', gameId] as const;

export interface GameDetail {
  id: string;
  userId: string;
  gameId: string;
  gameTitle: string;
  gamePublisher: string | null;
  gameYearPublished: number | null;
  gameDescription: string | null;
  gameIconUrl: string | null;
  gameImageUrl: string | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  playTimeMinutes: number | null;
  complexityRating: number | null;
  averageRating: number | null;
  addedAt: string;
  notes: string | null;
  isFavorite: boolean;
  currentState: 'Nuovo' | 'InPrestito' | 'Wishlist' | 'Owned';
  stateChangedAt: string | null;
  stateNotes: string | null;
  isAvailableForPlay: boolean;
  timesPlayed: number;
  lastPlayed: string | null;
  winRate: string | null;
  avgDuration: string | null;
  recentSessions?: GameSession[];
  checklist?: ChecklistItem[];
  customAgentConfig?: unknown;
  customPdf?: unknown;
}

export interface GameSession {
  id: string;
  playedAt: string;
  durationMinutes: number;
  durationFormatted: string;
  didWin: boolean | null;
  players: string | null;
  notes: string | null;
}

export interface ChecklistItem {
  id: string;
  description: string;
  order: number;
  isCompleted: boolean;
  additionalInfo: string | null;
}

export interface RecordSessionPayload {
  playedAt: Date;
  durationMinutes: number;
  didWin?: boolean;
  players?: string;
  notes?: string;
}

export interface UpdateStatePayload {
  newState: 'Nuovo' | 'InPrestito' | 'Wishlist' | 'Owned';
  stateNotes?: string;
}

/**
 * Hook for fetching game detail
 */
export function useGameDetail(gameId: string) {
  const setGameId = useGameDetailStore((s) => s.setGameId);
  const setCurrentState = useGameDetailStore((s) => s.setCurrentState);

  return useQuery<GameDetail, Error>({
    queryKey: GAME_DETAIL_QUERY_KEY(gameId),
    queryFn: async () => {
      const response = await fetch(`/api/v1/library/games/${gameId}`);
      if (!response.ok) throw new Error('Failed to fetch game detail');
      const result = await response.json() as GameDetail;
      setGameId(gameId);
      setCurrentState(result.currentState);
      return result;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes (matches backend cache)
    retry: 2,
  });
}

/**
 * Hook for updating game state
 */
export function useUpdateGameState(gameId: string) {
  const queryClient = useQueryClient();
  const setIsUpdatingState = useGameDetailStore((s) => s.setIsUpdatingState);
  const setCurrentState = useGameDetailStore((s) => s.setCurrentState);
  const setError = useGameDetailStore((s) => s.setError);

  return useMutation({
    mutationFn: async (payload: UpdateStatePayload) => {
      const response = await fetch(`/api/v1/library/games/${gameId}/state`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!response.ok) throw new Error('Failed to update game state');
    },
    onMutate: async (payload) => {
      setIsUpdatingState(true);

      // Optimistic update
      await queryClient.cancelQueries({ queryKey: GAME_DETAIL_QUERY_KEY(gameId) });
      const previousData = queryClient.getQueryData<GameDetail>(GAME_DETAIL_QUERY_KEY(gameId));

      if (previousData) {
        queryClient.setQueryData<GameDetail>(GAME_DETAIL_QUERY_KEY(gameId), {
          ...previousData,
          currentState: payload.newState,
          stateNotes: payload.stateNotes ?? previousData.stateNotes,
        });
      }

      setCurrentState(payload.newState);

      return { previousData };
    },
    // TODO: Re-implement error handling with React Query v5 patterns
    // onError removed - use mutation.error in component
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GAME_DETAIL_QUERY_KEY(gameId) });
      setIsUpdatingState(false);
      setError(null);
    },
  });
}

/**
 * Hook for recording game session
 */
export function useRecordGameSession(gameId: string) {
  const queryClient = useQueryClient();
  const setIsRecordingSession = useGameDetailStore((s) => s.setIsRecordingSession);
  const setOptimisticSessionId = useGameDetailStore((s) => s.setOptimisticSessionId);
  const setError = useGameDetailStore((s) => s.setError);

  return useMutation({
    mutationFn: async (payload: RecordSessionPayload) => {
      const response = await fetch(`/api/v1/library/games/${gameId}/sessions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...payload,
          playedAt: payload.playedAt.toISOString(),
        }),
      });
      if (!response.ok) throw new Error('Failed to record session');
      const result = await response.json() as { sessionId: string };
      return result.sessionId;
    },
    onMutate: async () => {
      setIsRecordingSession(true);
      const optimisticId = `optimistic-${Date.now()}`;
      setOptimisticSessionId(optimisticId);

      // Cancel ongoing queries
      await queryClient.cancelQueries({ queryKey: GAME_DETAIL_QUERY_KEY(gameId) });

      return { optimisticId };
    },
    onSuccess: (sessionId) => {
      // Invalidate to refetch with updated stats
      queryClient.invalidateQueries({ queryKey: GAME_DETAIL_QUERY_KEY(gameId) });
      setIsRecordingSession(false);
      setOptimisticSessionId(null);
      setError(null);

      return sessionId;
    },
  });
}
