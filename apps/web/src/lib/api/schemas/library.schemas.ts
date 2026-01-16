/**
 * User Library API Schemas
 *
 * Zod schemas for validating user game library responses.
 * User library management for personal game collections.
 */

import { z } from 'zod';

// User library entry DTO matching backend contract
export const UserLibraryEntrySchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  gameId: z.string().uuid(),
  gameTitle: z.string(),
  gamePublisher: z.string().nullable().optional(),
  gameYearPublished: z.number().nullable().optional(),
  gameIconUrl: z.string().nullable().optional(),
  gameImageUrl: z.string().nullable().optional(),
  addedAt: z.string().datetime(),
  notes: z.string().nullable().optional(),
  isFavorite: z.boolean(),
});

export type UserLibraryEntry = z.infer<typeof UserLibraryEntrySchema>;

// Library statistics DTO
export const UserLibraryStatsSchema = z.object({
  totalGames: z.number().int().nonnegative(),
  favoriteGames: z.number().int().nonnegative(),
  oldestAddedAt: z.string().datetime().nullable().optional(),
  newestAddedAt: z.string().datetime().nullable().optional(),
});

export type UserLibraryStats = z.infer<typeof UserLibraryStatsSchema>;

// Paginated library response
export const PaginatedLibraryResponseSchema = z.object({
  items: z.array(UserLibraryEntrySchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  totalCount: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
  hasNextPage: z.boolean(),
  hasPreviousPage: z.boolean(),
});

export type PaginatedLibraryResponse = z.infer<typeof PaginatedLibraryResponseSchema>;

// Game in library status
export const GameInLibraryStatusSchema = z.object({
  inLibrary: z.boolean(),
  isFavorite: z.boolean(),
});

export type GameInLibraryStatus = z.infer<typeof GameInLibraryStatusSchema>;

// Request schemas for mutations
export const AddGameToLibraryRequestSchema = z.object({
  notes: z.string().max(500).nullable().optional(),
  isFavorite: z.boolean().optional().default(false),
});

export type AddGameToLibraryRequest = z.infer<typeof AddGameToLibraryRequestSchema>;

export const UpdateLibraryEntryRequestSchema = z.object({
  notes: z.string().max(500).nullable().optional(),
  isFavorite: z.boolean().nullable().optional(),
});

export type UpdateLibraryEntryRequest = z.infer<typeof UpdateLibraryEntryRequestSchema>;

// Query parameters for getting library
export interface GetUserLibraryParams {
  page?: number;
  pageSize?: number;
  favoritesOnly?: boolean;
  sortBy?: 'addedAt' | 'title' | 'favorite';
  sortDescending?: boolean;
}

// Library quota response - tier-based limits
export const LibraryQuotaResponseSchema = z.object({
  currentCount: z.number().int().nonnegative(),
  maxAllowed: z.number().int().positive(),
  userTier: z.enum(['free', 'normal', 'premium']),
  remainingSlots: z.number().int().nonnegative(),
  percentageUsed: z.number().min(0).max(100),
});

export type LibraryQuotaResponse = z.infer<typeof LibraryQuotaResponseSchema>;
