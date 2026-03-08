/**
 * User Library API Schemas
 *
 * Zod schemas for validating user game library responses.
 * User library management for personal game collections.
 *
 * Updated: Issue #2868 - Added UpdateGameStateRequest schema
 */

import { z } from 'zod';

// Game state types for library filtering (Issue #2866)
// Valid enum values - strict parsing
const VALID_GAME_STATES = ['Nuovo', 'InPrestito', 'Wishlist', 'Owned'] as const;
export const GameStateTypeSchema = z.enum(VALID_GAME_STATES);
export type GameStateType = z.infer<typeof GameStateTypeSchema>;

// Defensive schema that falls back to 'Owned' for unknown values (prevents API breaking on new states)
export const GameStateTypeWithFallbackSchema = z.string().transform(val => {
  if (VALID_GAME_STATES.includes(val as GameStateType)) {
    return val as GameStateType;
  }
  // Log unknown state for debugging, fallback to 'Owned'
  console.warn(`Unknown GameStateType received: "${val}", falling back to "Owned"`);
  return 'Owned' as GameStateType;
});

// User library entry DTO matching backend contract
// Issue #4998: Replaced hasPdfDocuments with KB-aware fields
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
  currentState: GameStateTypeWithFallbackSchema,
  stateChangedAt: z.string().datetime().nullable().optional(),
  stateNotes: z.string().nullable().optional(),
  hasKb: z.boolean().default(false), // true if >= 1 PDF fully indexed in RAG
  kbCardCount: z.number().int().nonnegative().default(0), // total PDF documents linked
  kbIndexedCount: z.number().int().nonnegative().default(0), // PDFs with ProcessingState.Ready
  kbProcessingCount: z.number().int().nonnegative().default(0), // PDFs currently in pipeline
  agentIsOwned: z.boolean().default(true), // always true in library context
  minPlayers: z.number().int().nullable().optional(),
  maxPlayers: z.number().int().nullable().optional(),
  playingTimeMinutes: z.number().int().nullable().optional(),
  complexityRating: z.number().nullable().optional(),
  averageRating: z.number().nullable().optional(),
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

// Batch game status schemas (Issue #4581)
export const GameStatusSimpleSchema = z.object({
  inLibrary: z.boolean(),
  isFavorite: z.boolean(),
  isOwned: z.boolean(),
});

export type GameStatusSimple = z.infer<typeof GameStatusSimpleSchema>;

export const BatchGameStatusResponseSchema = z.object({
  results: z.record(z.string().uuid(), GameStatusSimpleSchema),
  totalChecked: z.number().int().nonnegative(),
});

export type BatchGameStatusResponse = z.infer<typeof BatchGameStatusResponseSchema>;

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

// Update game state request (Issue #2868)
export const UpdateGameStateRequestSchema = z.object({
  newState: GameStateTypeSchema,
  stateNotes: z.string().max(500).nullable().optional(),
});

export type UpdateGameStateRequest = z.infer<typeof UpdateGameStateRequestSchema>;

// Query parameters for getting library
export interface GetUserLibraryParams {
  page?: number;
  pageSize?: number;
  favoritesOnly?: boolean;
  stateFilter?: GameStateType[];
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

// ========================================
// Library Share Link Schemas (Issue #2614)
// ========================================

// Library share link response DTO
export const LibraryShareLinkSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  shareToken: z.string().length(32),
  shareUrl: z.string().url(),
  privacyLevel: z.enum(['public', 'unlisted']),
  includeNotes: z.boolean(),
  createdAt: z.string().datetime(),
  expiresAt: z.string().datetime().nullable(),
  revokedAt: z.string().datetime().nullable(),
  viewCount: z.number().int().nonnegative(),
  lastAccessedAt: z.string().datetime().nullable(),
  isActive: z.boolean(),
});

export type LibraryShareLink = z.infer<typeof LibraryShareLinkSchema>;

// Create share link request
export const CreateLibraryShareLinkRequestSchema = z.object({
  privacyLevel: z.enum(['public', 'unlisted']).default('unlisted'),
  includeNotes: z.boolean().default(false),
  expiresAt: z.string().datetime().nullable().optional(),
});

export type CreateLibraryShareLinkRequest = z.infer<typeof CreateLibraryShareLinkRequestSchema>;

// Update share link request
export const UpdateLibraryShareLinkRequestSchema = z.object({
  privacyLevel: z.enum(['public', 'unlisted']).optional(),
  includeNotes: z.boolean().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

export type UpdateLibraryShareLinkRequest = z.infer<typeof UpdateLibraryShareLinkRequestSchema>;

// Shared library public view response
export const SharedLibraryGameSchema = z.object({
  gameId: z.string().uuid(),
  title: z.string(),
  publisher: z.string().nullable(),
  yearPublished: z.number().nullable(),
  iconUrl: z.string().nullable(),
  imageUrl: z.string().nullable(),
  isFavorite: z.boolean(),
  notes: z.string().nullable(),
  addedAt: z.string().datetime(),
});

export type SharedLibraryGame = z.infer<typeof SharedLibraryGameSchema>;

export const SharedLibrarySchema = z.object({
  ownerDisplayName: z.string(),
  games: z.array(SharedLibraryGameSchema),
  totalGames: z.number().int().nonnegative(),
  favoritesCount: z.number().int().nonnegative(),
  privacyLevel: z.enum(['public', 'unlisted']),
  sharedAt: z.string().datetime(),
});

export type SharedLibrary = z.infer<typeof SharedLibrarySchema>;

// ========================================
// Game Detail Schemas (Issue #3513)
// ========================================

// Library game session DTO for recent sessions (distinct from games.schemas GameSessionDto)
export const LibraryGameSessionSchema = z.object({
  id: z.string().uuid(),
  playedAt: z.string().datetime(),
  durationMinutes: z.number().int().nonnegative(),
  durationFormatted: z.string(),
  didWin: z.boolean().nullable(),
  players: z.string().nullable(),
  notes: z.string().nullable(),
});

export type LibraryGameSession = z.infer<typeof LibraryGameSessionSchema>;

// Checklist item DTO
export const LibraryChecklistItemSchema = z.object({
  id: z.string().uuid(),
  description: z.string(),
  order: z.number().int(),
  isCompleted: z.boolean(),
  additionalInfo: z.string().nullable(),
});

export type LibraryChecklistItem = z.infer<typeof LibraryChecklistItemSchema>;

// Custom PDF DTO
export const LibraryCustomPdfSchema = z.object({
  id: z.string().uuid(),
  pdfUrl: z.string(),
  originalFileName: z.string(),
  fileSizeBytes: z.number(),
  uploadedAt: z.string().datetime(),
});

export type LibraryCustomPdf = z.infer<typeof LibraryCustomPdfSchema>;

// Comprehensive game detail DTO (GET /library/games/{gameId})
export const GameDetailDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  gameId: z.string().uuid(),

  // Game metadata
  gameTitle: z.string(),
  gamePublisher: z.string().nullable(),
  gameYearPublished: z.number().nullable(),
  gameDescription: z.string().nullable(),
  gameIconUrl: z.string().nullable(),
  gameImageUrl: z.string().nullable(),
  minPlayers: z.number().nullable(),
  maxPlayers: z.number().nullable(),
  playTimeMinutes: z.number().nullable(),
  complexityRating: z.number().nullable(),
  averageRating: z.number().nullable(),

  // Library metadata
  addedAt: z.string().datetime(),
  notes: z.string().nullable(),
  isFavorite: z.boolean(),

  // Game state
  currentState: GameStateTypeWithFallbackSchema,
  stateChangedAt: z.string().datetime().nullable(),
  stateNotes: z.string().nullable(),
  isAvailableForPlay: z.boolean(),

  // Statistics
  timesPlayed: z.number().int().nonnegative(),
  lastPlayed: z.string().datetime().nullable(),
  winRate: z.string().nullable(),
  avgDuration: z.string().nullable(),

  // Optional extended data
  recentSessions: z.array(LibraryGameSessionSchema).nullable().optional(),
  checklist: z.array(LibraryChecklistItemSchema).nullable().optional(),
  customAgentConfig: z.any().nullable().optional(), // AgentConfigDto
  customPdf: LibraryCustomPdfSchema.nullable().optional(),
});

export type GameDetailDto = z.infer<typeof GameDetailDtoSchema>;

// ========================================
// Game Labels Schemas (Issue #3512)
// ========================================

/**
 * Label DTO matching backend LabelDto contract.
 * Labels can be predefined (system) or custom (user-created).
 */
export const LabelDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string().max(50),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'Must be valid hex color'),
  isPredefined: z.boolean(),
  createdAt: z.string().datetime(),
});

export type LabelDto = z.infer<typeof LabelDtoSchema>;

/**
 * Request to create a custom label.
 */
export const CreateCustomLabelRequestSchema = z.object({
  name: z.string().min(1).max(50),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'Must be valid hex color'),
});

export type CreateCustomLabelRequest = z.infer<typeof CreateCustomLabelRequestSchema>;

/**
 * Predefined label constants matching backend PredefinedLabels.cs
 */
export const PREDEFINED_LABELS = {
  FAMILY: { id: 'a1b2c3d4-1111-1111-1111-111111111111', name: 'Family', color: '#22c55e' },
  STRATEGY: { id: 'a1b2c3d4-2222-2222-2222-222222222222', name: 'Strategy', color: '#3b82f6' },
  PARTY: { id: 'a1b2c3d4-3333-3333-3333-333333333333', name: 'Party', color: '#f59e0b' },
  COOPERATIVE: {
    id: 'a1b2c3d4-4444-4444-4444-444444444444',
    name: 'Cooperative',
    color: '#8b5cf6',
  },
  SOLO: { id: 'a1b2c3d4-5555-5555-5555-555555555555', name: 'Solo', color: '#ec4899' },
  FILLER: { id: 'a1b2c3d4-6666-6666-6666-666666666666', name: 'Filler', color: '#6b7280' },
  CLASSIC: { id: 'a1b2c3d4-7777-7777-7777-777777777777', name: 'Classic', color: '#78716c' },
  KIDS: { id: 'a1b2c3d4-8888-8888-8888-888888888888', name: 'Kids', color: '#14b8a6' },
} as const;
