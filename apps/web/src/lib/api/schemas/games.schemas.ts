/**
 * Games & Sessions API Schemas (FE-IMP-005)
 *
 * Zod schemas for validating GameManagement bounded context responses.
 * Covers: Games CRUD, Sessions, BoardGameGeek integration
 */

import { z } from 'zod';

// ========== Game Entity ==========

export const GameSchema = z.object({
  id: z.string().uuid(),
  title: z.string().min(1),
  publisher: z.string().nullable(),
  yearPublished: z.number().int().nullable(),
  minPlayers: z.number().int().positive().nullable(),
  maxPlayers: z.number().int().positive().nullable(),
  minPlayTimeMinutes: z.number().int().positive().nullable(),
  maxPlayTimeMinutes: z.number().int().positive().nullable(),
  bggId: z.number().int().positive().nullable(),
  createdAt: z.string().datetime(),
});

export type Game = z.infer<typeof GameSchema>;

export const PaginatedGamesResponseSchema = z.object({
  games: z.array(GameSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  totalPages: z.number().int().nonnegative(),
});

export type PaginatedGamesResponse = z.infer<typeof PaginatedGamesResponseSchema>;

// ========== Game Sessions ==========

export const SessionPlayerDtoSchema = z.object({
  playerName: z.string().min(1),
  playerOrder: z.number().int().nonnegative(),
  color: z.string().nullable(),
});

export type SessionPlayerDto = z.infer<typeof SessionPlayerDtoSchema>;

export const GameSessionDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  status: z.string().min(1),
  startedAt: z.string().datetime(),
  completedAt: z.string().datetime().nullable(),
  playerCount: z.number().int().positive(),
  players: z.array(SessionPlayerDtoSchema),
  winnerName: z.string().nullable(),
  notes: z.string().nullable(),
  durationMinutes: z.number().int().nonnegative(),
});

export type GameSessionDto = z.infer<typeof GameSessionDtoSchema>;

export const PaginatedSessionsResponseSchema = z.object({
  sessions: z.array(GameSessionDtoSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

export type PaginatedSessionsResponse = z.infer<typeof PaginatedSessionsResponseSchema>;

// ========== BoardGameGeek API ==========

export const BggSearchResultSchema = z.object({
  bggId: z.number().int().positive(),
  name: z.string().min(1),
  yearPublished: z.number().int().nullable(),
  thumbnailUrl: z.string().url().nullable(),
  type: z.string().min(1),
});

export type BggSearchResult = z.infer<typeof BggSearchResultSchema>;

export const BggSearchResponseSchema = z.object({
  results: z.array(BggSearchResultSchema),
});

export type BggSearchResponse = z.infer<typeof BggSearchResponseSchema>;

export const BggGameDetailsSchema = z.object({
  bggId: z.number().int().positive(),
  name: z.string().min(1),
  description: z.string().nullable(),
  yearPublished: z.number().int().nullable(),
  minPlayers: z.number().int().positive().nullable(),
  maxPlayers: z.number().int().positive().nullable(),
  playingTime: z.number().int().positive().nullable(),
  minPlayTime: z.number().int().positive().nullable(),
  maxPlayTime: z.number().int().positive().nullable(),
  minAge: z.number().int().positive().nullable(),
  averageRating: z.number().nullable(),
  bayesAverageRating: z.number().nullable(),
  usersRated: z.number().int().nonnegative().nullable(),
  averageWeight: z.number().nullable(),
  thumbnailUrl: z.string().url().nullable(),
  imageUrl: z.string().url().nullable(),
  categories: z.array(z.string()),
  mechanics: z.array(z.string()),
  designers: z.array(z.string()),
  publishers: z.array(z.string()),
});

export type BggGameDetails = z.infer<typeof BggGameDetailsSchema>;
