/**
 * Playlists API Client
 * Gap Closure — Playlists
 */

import {
  GameNightPlaylistDtoSchema,
  PaginatedPlaylistsResponseSchema,
  ShareLinkResponseSchema,
  type GameNightPlaylistDto,
  type PaginatedPlaylistsResponse,
  type CreatePlaylistRequest,
  type UpdatePlaylistRequest,
  type AddGameToPlaylistRequest,
  type ReorderPlaylistGamesRequest,
  type ShareLinkResponse,
} from '../schemas/playlists.schemas';

import type { HttpClient } from '../core/httpClient';

export interface PlaylistsClient {
  list(params?: { page?: number; pageSize?: number }): Promise<PaginatedPlaylistsResponse>;
  getById(id: string): Promise<GameNightPlaylistDto>;
  create(data: CreatePlaylistRequest): Promise<GameNightPlaylistDto>;
  update(id: string, data: UpdatePlaylistRequest): Promise<GameNightPlaylistDto>;
  delete(id: string): Promise<void>;
  addGame(id: string, data: AddGameToPlaylistRequest): Promise<GameNightPlaylistDto>;
  removeGame(id: string, gameId: string): Promise<void>;
  reorderGames(id: string, data: ReorderPlaylistGamesRequest): Promise<GameNightPlaylistDto>;
  share(id: string): Promise<ShareLinkResponse>;
  revokeShare(id: string): Promise<void>;
  getByShareToken(token: string): Promise<GameNightPlaylistDto>;
}

export function createPlaylistsClient({ httpClient }: { httpClient: HttpClient }): PlaylistsClient {
  return {
    async list(params) {
      const query = new URLSearchParams();
      if (params?.page !== undefined) query.set('page', String(params.page));
      if (params?.pageSize !== undefined) query.set('pageSize', String(params.pageSize));
      const qs = query.toString();
      const url = qs ? `/api/v1/playlists?${qs}` : '/api/v1/playlists';
      const data = await httpClient.get<PaginatedPlaylistsResponse>(url);
      return PaginatedPlaylistsResponseSchema.parse(data);
    },

    async getById(id) {
      const data = await httpClient.get<GameNightPlaylistDto>(`/api/v1/playlists/${id}`);
      return GameNightPlaylistDtoSchema.parse(data);
    },

    async create(input) {
      const data = await httpClient.post<GameNightPlaylistDto>('/api/v1/playlists', input);
      return GameNightPlaylistDtoSchema.parse(data);
    },

    async update(id, input) {
      const data = await httpClient.put<GameNightPlaylistDto>(`/api/v1/playlists/${id}`, input);
      return GameNightPlaylistDtoSchema.parse(data);
    },

    async delete(id) {
      await httpClient.delete(`/api/v1/playlists/${id}`);
    },

    async addGame(id, input) {
      const data = await httpClient.post<GameNightPlaylistDto>(
        `/api/v1/playlists/${id}/games`,
        input
      );
      return GameNightPlaylistDtoSchema.parse(data);
    },

    async removeGame(id, gameId) {
      await httpClient.delete(`/api/v1/playlists/${id}/games/${gameId}`);
    },

    async reorderGames(id, input) {
      const data = await httpClient.put<GameNightPlaylistDto>(
        `/api/v1/playlists/${id}/games/reorder`,
        input
      );
      return GameNightPlaylistDtoSchema.parse(data);
    },

    async share(id) {
      const data = await httpClient.post<ShareLinkResponse>(`/api/v1/playlists/${id}/share`, {});
      return ShareLinkResponseSchema.parse(data);
    },

    async revokeShare(id) {
      await httpClient.delete(`/api/v1/playlists/${id}/share`);
    },

    async getByShareToken(token) {
      const data = await httpClient.get<GameNightPlaylistDto>(`/api/v1/playlists/shared/${token}`);
      return GameNightPlaylistDtoSchema.parse(data);
    },
  };
}
