/**
 * Shared Game Catalog API Schemas (Issue #2372)
 *
 * Zod schemas for validating SharedGameCatalog bounded context responses.
 * Covers: Games CRUD, Categories, Mechanics, FAQs, Errata, Delete Workflow
 */

import { z } from 'zod';

// ========== Enums ==========

/**
 * Game publication status (matches C# GameStatus enum)
 */
export const GameStatusSchema = z.enum(['Draft', 'Published', 'Archived']);
export type GameStatus = z.infer<typeof GameStatusSchema>;

/**
 * Numeric game status (for API query params)
 */
export const GameStatusNumericSchema = z.number().int().min(0).max(2);
export type GameStatusNumeric = z.infer<typeof GameStatusNumericSchema>;

// ========== Reference Data ==========

/**
 * Game category DTO
 */
export const GameCategorySchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  slug: z.string().min(1),
});

export type GameCategory = z.infer<typeof GameCategorySchema>;

/**
 * Game mechanic DTO
 */
export const GameMechanicSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  slug: z.string().min(1),
});

export type GameMechanic = z.infer<typeof GameMechanicSchema>;

// ========== Game Rules ==========

/**
 * Game rules DTO
 */
export const GameRulesSchema = z.object({
  content: z.string(),
  language: z.string(),
});

export type GameRules = z.infer<typeof GameRulesSchema>;

// ========== Shared Game DTOs ==========

/**
 * Shared game basic DTO (list view)
 */
export const SharedGameSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().positive().nullable(),
  title: z.string().min(1),
  yearPublished: z.number().int(),
  description: z.string(),
  minPlayers: z.number().int().positive(),
  maxPlayers: z.number().int().positive(),
  playingTimeMinutes: z.number().int().positive(),
  minAge: z.number().int().nonnegative(),
  complexityRating: z.number().nullable(),
  averageRating: z.number().nullable(),
  imageUrl: z.string(),
  thumbnailUrl: z.string(),
  status: GameStatusNumericSchema,
  createdAt: z.string().datetime(),
  modifiedAt: z.string().datetime().nullable(),
});

export type SharedGame = z.infer<typeof SharedGameSchema>;

/**
 * Shared game detail DTO (single game view with full info)
 */
export const SharedGameDetailSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().positive().nullable(),
  title: z.string().min(1),
  yearPublished: z.number().int(),
  description: z.string(),
  minPlayers: z.number().int().positive(),
  maxPlayers: z.number().int().positive(),
  playingTimeMinutes: z.number().int().positive(),
  minAge: z.number().int().nonnegative(),
  complexityRating: z.number().nullable(),
  averageRating: z.number().nullable(),
  imageUrl: z.string(),
  thumbnailUrl: z.string(),
  rules: GameRulesSchema.nullable(),
  status: GameStatusNumericSchema,
  createdBy: z.string().uuid(),
  modifiedBy: z.string().uuid().nullable(),
  createdAt: z.string().datetime(),
  modifiedAt: z.string().datetime().nullable(),
});

export type SharedGameDetail = z.infer<typeof SharedGameDetailSchema>;

// ========== Paginated Results ==========

/**
 * Paginated shared games response
 */
export const PagedSharedGamesSchema = z.object({
  items: z.array(SharedGameSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

export type PagedSharedGames = z.infer<typeof PagedSharedGamesSchema>;

// ========== Delete Workflow ==========

/**
 * Delete request DTO
 */
export const DeleteRequestSchema = z.object({
  id: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  gameTitle: z.string(),
  requestedBy: z.string().uuid(),
  reason: z.string(),
  createdAt: z.string().datetime(),
});

export type DeleteRequest = z.infer<typeof DeleteRequestSchema>;

/**
 * Paginated delete requests response
 */
export const PagedDeleteRequestsSchema = z.object({
  items: z.array(DeleteRequestSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

export type PagedDeleteRequests = z.infer<typeof PagedDeleteRequestsSchema>;

// ========== Request Models ==========

/**
 * Create shared game request
 */
export const CreateSharedGameRequestSchema = z.object({
  title: z.string().min(1).max(255),
  yearPublished: z.number().int().min(1900).max(2100),
  description: z.string().min(1),
  minPlayers: z.number().int().min(1).max(100),
  maxPlayers: z.number().int().min(1).max(100),
  playingTimeMinutes: z.number().int().min(1).max(10000),
  minAge: z.number().int().min(0).max(100),
  complexityRating: z.number().min(0).max(5).nullable().optional(),
  averageRating: z.number().min(0).max(10).nullable().optional(),
  imageUrl: z.string().url(),
  thumbnailUrl: z.string().url(),
  rules: z
    .object({
      content: z.string().min(1),
      language: z.string().min(1).max(10),
    })
    .nullable()
    .optional(),
  bggId: z.number().int().positive().nullable().optional(),
});

export type CreateSharedGameRequest = z.infer<typeof CreateSharedGameRequestSchema>;

/**
 * Update shared game request
 */
export const UpdateSharedGameRequestSchema = z.object({
  title: z.string().min(1).max(255),
  yearPublished: z.number().int().min(1900).max(2100),
  description: z.string().min(1),
  minPlayers: z.number().int().min(1).max(100),
  maxPlayers: z.number().int().min(1).max(100),
  playingTimeMinutes: z.number().int().min(1).max(10000),
  minAge: z.number().int().min(0).max(100),
  complexityRating: z.number().min(0).max(5).nullable().optional(),
  averageRating: z.number().min(0).max(10).nullable().optional(),
  imageUrl: z.string().url(),
  thumbnailUrl: z.string().url(),
  rules: z
    .object({
      content: z.string().min(1),
      language: z.string().min(1).max(10),
    })
    .nullable()
    .optional(),
});

export type UpdateSharedGameRequest = z.infer<typeof UpdateSharedGameRequestSchema>;

/**
 * Delete request body (reason required for editors)
 */
export const DeleteSharedGameRequestSchema = z.object({
  reason: z.string().min(1).optional(),
});

export type DeleteSharedGameRequestBody = z.infer<typeof DeleteSharedGameRequestSchema>;

/**
 * Approve delete request body
 */
export const ApproveDeleteRequestBodySchema = z.object({
  comment: z.string().optional(),
});

export type ApproveDeleteRequestBody = z.infer<typeof ApproveDeleteRequestBodySchema>;

/**
 * Reject delete request body
 */
export const RejectDeleteRequestBodySchema = z.object({
  reason: z.string().min(1),
});

export type RejectDeleteRequestBody = z.infer<typeof RejectDeleteRequestBodySchema>;

// ========== FAQ ==========

/**
 * Add FAQ request
 */
export const AddFaqRequestSchema = z.object({
  question: z.string().min(1).max(500),
  answer: z.string().min(1).max(5000),
  order: z.number().int().nonnegative(),
});

export type AddFaqRequest = z.infer<typeof AddFaqRequestSchema>;

/**
 * Update FAQ request
 */
export const UpdateFaqRequestSchema = z.object({
  question: z.string().min(1).max(500),
  answer: z.string().min(1).max(5000),
  order: z.number().int().nonnegative(),
});

export type UpdateFaqRequest = z.infer<typeof UpdateFaqRequestSchema>;

// ========== Errata ==========

/**
 * Add errata request
 */
export const AddErrataRequestSchema = z.object({
  description: z.string().min(1).max(2000),
  pageReference: z.string().min(1).max(100),
  publishedDate: z.string().datetime(),
});

export type AddErrataRequest = z.infer<typeof AddErrataRequestSchema>;

/**
 * Update errata request
 */
export const UpdateErrataRequestSchema = z.object({
  description: z.string().min(1).max(2000),
  pageReference: z.string().min(1).max(100),
  publishedDate: z.string().datetime(),
});

export type UpdateErrataRequest = z.infer<typeof UpdateErrataRequestSchema>;

// ========== Search Params ==========

/**
 * Search shared games query parameters
 */
export const SearchSharedGamesParamsSchema = z.object({
  searchTerm: z.string().optional(),
  categoryIds: z.string().optional(), // comma-separated GUIDs
  mechanicIds: z.string().optional(), // comma-separated GUIDs
  minPlayers: z.number().int().positive().optional(),
  maxPlayers: z.number().int().positive().optional(),
  maxPlayingTime: z.number().int().positive().optional(),
  status: GameStatusNumericSchema.optional(),
  page: z.number().int().positive().optional(),
  pageSize: z.number().int().positive().optional(),
  sortBy: z.string().optional(),
  sortDescending: z.boolean().optional(),
});

export type SearchSharedGamesParams = z.infer<typeof SearchSharedGamesParamsSchema>;

// ========== Response Helpers ==========

/**
 * Created response (POST returns { id: uuid })
 */
export const CreatedResponseSchema = z.object({
  id: z.string().uuid(),
});

export type CreatedResponse = z.infer<typeof CreatedResponseSchema>;

/**
 * Delete request accepted response
 */
export const DeleteRequestAcceptedSchema = z.object({
  requestId: z.string().uuid(),
  message: z.string(),
});

export type DeleteRequestAccepted = z.infer<typeof DeleteRequestAcceptedSchema>;
