/**
 * usePlaylists — React Query hooks for Game Night Playlists
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  AddGameToPlaylistRequest,
  CreatePlaylistRequest,
  ReorderPlaylistGamesRequest,
  UpdatePlaylistRequest,
} from '@/lib/api/schemas/playlists.schemas';

export const playlistKeys = {
  all: ['playlists'] as const,
  lists: () => [...playlistKeys.all, 'list'] as const,
  list: (page: number) => [...playlistKeys.lists(), page] as const,
  details: () => [...playlistKeys.all, 'detail'] as const,
  detail: (id: string) => [...playlistKeys.details(), id] as const,
  shared: (token: string) => [...playlistKeys.all, 'shared', token] as const,
};

export function usePlaylists(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: playlistKeys.list(page),
    queryFn: () => api.playlists.list({ page, pageSize }),
  });
}

export function usePlaylist(id: string) {
  return useQuery({
    queryKey: playlistKeys.detail(id),
    queryFn: () => api.playlists.getById(id),
    enabled: !!id,
  });
}

export function useSharedPlaylist(token: string) {
  return useQuery({
    queryKey: playlistKeys.shared(token),
    queryFn: () => api.playlists.getByShareToken(token),
    enabled: !!token,
  });
}

export function useCreatePlaylist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePlaylistRequest) => api.playlists.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.lists() });
    },
  });
}

export function useUpdatePlaylist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdatePlaylistRequest }) =>
      api.playlists.update(id, data),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: playlistKeys.lists() });
    },
  });
}

export function useDeletePlaylist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.playlists.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.lists() });
    },
  });
}

export function useAddGameToPlaylist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ playlistId, data }: { playlistId: string; data: AddGameToPlaylistRequest }) =>
      api.playlists.addGame(playlistId, data),
    onSuccess: (_data, { playlistId }) => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) });
    },
  });
}

export function useRemoveGameFromPlaylist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ playlistId, gameId }: { playlistId: string; gameId: string }) =>
      api.playlists.removeGame(playlistId, gameId),
    onSuccess: (_data, { playlistId }) => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) });
    },
  });
}

export function useReorderPlaylistGames() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ playlistId, data }: { playlistId: string; data: ReorderPlaylistGamesRequest }) =>
      api.playlists.reorderGames(playlistId, data),
    onSuccess: (_data, { playlistId }) => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) });
    },
  });
}

export function useSharePlaylist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (playlistId: string) => api.playlists.share(playlistId),
    onSuccess: (_data, playlistId) => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) });
    },
  });
}

export function useRevokePlaylistShare() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (playlistId: string) => api.playlists.revokeShare(playlistId),
    onSuccess: (_data, playlistId) => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) });
    },
  });
}
