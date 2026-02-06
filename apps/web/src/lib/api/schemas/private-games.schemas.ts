/**
 * Private Games API Schemas
 *
 * Zod schemas for private game management.
 * Issue #3669: Phase 8 - Frontend Integration
 */

import { z } from 'zod';

// Private game source enum
export const PrivateGameSourceSchema = z.enum(['Manual', 'Bgg']);
export type PrivateGameSource = z.infer<typeof PrivateGameSourceSchema>;

// Private game DTO from backend
export const PrivateGameDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  source: PrivateGameSourceSchema,
  bggId: z.number().int().positive().nullable().optional(),
  title: z.string(),
  minPlayers: z.number().int().positive(),
  maxPlayers: z.number().int().positive(),
  yearPublished: z.number().int().nullable().optional(),
  description: z.string().nullable().optional(),
  playingTimeMinutes: z.number().int().positive().nullable().optional(),
  minAge: z.number().int().positive().nullable().optional(),
  complexityRating: z.number().min(0).max(5).nullable().optional(),
  imageUrl: z.string().url().nullable().optional(),
  thumbnailUrl: z.string().url().nullable().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export type PrivateGameDto = z.infer<typeof PrivateGameDtoSchema>;

// Add private game request (matches backend AddPrivateGameRequest)
export const AddPrivateGameRequestSchema = z.object({
  source: PrivateGameSourceSchema,
  bggId: z.number().int().positive().nullable().optional(),
  title: z.string().min(1).max(200),
  minPlayers: z.number().int().min(1).max(99),
  maxPlayers: z.number().int().min(1).max(99),
  yearPublished: z.number().int().min(1900).max(2100).nullable().optional(),
  description: z.string().max(5000).nullable().optional(),
  playingTimeMinutes: z.number().int().min(1).max(10000).nullable().optional(),
  minAge: z.number().int().min(0).max(99).nullable().optional(),
  complexityRating: z.number().min(0).max(5).nullable().optional(),
  imageUrl: z.string().url().nullable().optional(),
  thumbnailUrl: z.string().url().nullable().optional(),
}).refine((data) => data.maxPlayers >= data.minPlayers, {
  message: 'Max players must be greater than or equal to min players',
  path: ['maxPlayers'],
});

export type AddPrivateGameRequest = z.infer<typeof AddPrivateGameRequestSchema>;

// Update private game request (matches backend UpdatePrivateGameRequest)
export const UpdatePrivateGameRequestSchema = z.object({
  title: z.string().min(1).max(200),
  minPlayers: z.number().int().min(1).max(99),
  maxPlayers: z.number().int().min(1).max(99),
  yearPublished: z.number().int().min(1900).max(2100).nullable().optional(),
  description: z.string().max(5000).nullable().optional(),
  playingTimeMinutes: z.number().int().min(1).max(10000).nullable().optional(),
  minAge: z.number().int().min(0).max(99).nullable().optional(),
  complexityRating: z.number().min(0).max(5).nullable().optional(),
  imageUrl: z.string().url().nullable().optional(),
}).refine((data) => data.maxPlayers >= data.minPlayers, {
  message: 'Max players must be greater than or equal to min players',
  path: ['maxPlayers'],
});

export type UpdatePrivateGameRequest = z.infer<typeof UpdatePrivateGameRequestSchema>;

// Paginated private games response
export const PaginatedPrivateGamesResponseSchema = z.object({
  items: z.array(PrivateGameDtoSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  totalCount: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
  hasNextPage: z.boolean(),
  hasPreviousPage: z.boolean(),
});

export type PaginatedPrivateGamesResponse = z.infer<typeof PaginatedPrivateGamesResponseSchema>;

// Get private games query params
export interface GetPrivateGamesParams {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: 'title' | 'createdAt' | 'updatedAt';
  sortDirection?: 'asc' | 'desc';
}
