/**
 * Playlists Zod Schemas
 * Gap Closure — Playlists
 */

import { z } from 'zod';

export const PlaylistGameDtoSchema = z.object({
  sharedGameId: z.string().uuid(),
  position: z.number(),
  addedAt: z.string(),
});
export type PlaylistGameDto = z.infer<typeof PlaylistGameDtoSchema>;

export const GameNightPlaylistDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  scheduledDate: z.string().nullable(),
  creatorUserId: z.string().uuid(),
  isShared: z.boolean(),
  shareToken: z.string().nullable(),
  games: z.array(PlaylistGameDtoSchema),
  createdAt: z.string(),
  updatedAt: z.string(),
});
export type GameNightPlaylistDto = z.infer<typeof GameNightPlaylistDtoSchema>;

export const PaginatedPlaylistsResponseSchema = z.object({
  playlists: z.array(GameNightPlaylistDtoSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
  totalPages: z.number(),
});
export type PaginatedPlaylistsResponse = z.infer<typeof PaginatedPlaylistsResponseSchema>;

export const CreatePlaylistRequestSchema = z.object({
  name: z.string(),
  scheduledDate: z.string().optional(),
});
export type CreatePlaylistRequest = z.infer<typeof CreatePlaylistRequestSchema>;

export const UpdatePlaylistRequestSchema = z.object({
  name: z.string().optional(),
  scheduledDate: z.string().optional(),
});
export type UpdatePlaylistRequest = z.infer<typeof UpdatePlaylistRequestSchema>;

export const AddGameToPlaylistRequestSchema = z.object({
  sharedGameId: z.string().uuid(),
  position: z.number(),
});
export type AddGameToPlaylistRequest = z.infer<typeof AddGameToPlaylistRequestSchema>;

export const ReorderPlaylistGamesRequestSchema = z.object({
  orderedGameIds: z.array(z.string().uuid()),
});
export type ReorderPlaylistGamesRequest = z.infer<typeof ReorderPlaylistGamesRequestSchema>;

export const ShareLinkResponseSchema = z.object({
  shareToken: z.string(),
  shareUrl: z.string(),
});
export type ShareLinkResponse = z.infer<typeof ShareLinkResponseSchema>;
