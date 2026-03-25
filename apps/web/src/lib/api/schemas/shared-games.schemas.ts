/**
 * Shared Game Catalog API Schemas (Issue #2372, Extended #2373)
 *
 * Zod schemas for validating SharedGameCatalog bounded context responses.
 * Covers: Games CRUD, Categories, Mechanics, FAQs, Errata, Delete Workflow
 */

import { z } from 'zod';

// Import and re-export BGG types from games.schemas for convenience
import {
  BggGameDetailsSchema,
  type BggGameDetails,
  BggSearchResultSchema,
  type BggSearchResult,
} from './games.schemas';
export { BggGameDetailsSchema, type BggGameDetails, BggSearchResultSchema, type BggSearchResult };

// ========== Enums ==========

/**
 * Game publication status (matches C# GameStatus enum - Issue #2514)
 */
export const GameStatusSchema = z.enum(['Draft', 'PendingApproval', 'Published', 'Archived']);
export type GameStatus = z.infer<typeof GameStatusSchema>;

/**
 * Numeric game status (for API query params)
 * 0 = Draft, 1 = PendingApproval, 2 = Published, 3 = Archived
 */
export const GameStatusNumericSchema = z.number().int().min(0).max(3);
export type GameStatusNumeric = z.infer<typeof GameStatusNumericSchema>;

/**
 * Document type enum (Issue #2391 Sprint 1)
 */
export const SharedGameDocumentTypeSchema = z.enum(['Rulebook', 'Errata', 'Homerule']);
export type SharedGameDocumentType = z.infer<typeof SharedGameDocumentTypeSchema>;

/**
 * Agent mode enum (Issue #2391 Sprint 2)
 */
export const AgentModeSchema = z.enum(['Chat', 'Player', 'Ledger']);
export type AgentMode = z.infer<typeof AgentModeSchema>;

/**
 * LLM Provider enum (Issue #2391 Sprint 2)
 */
export const LlmProviderSchema = z.enum(['OpenRouter', 'Ollama']);
export type LlmProvider = z.infer<typeof LlmProviderSchema>;

/**
 * Numeric document type (for API storage - matches C# enum)
 */
export const SharedGameDocumentTypeNumericSchema = z.number().int().min(0).max(2);
export type SharedGameDocumentTypeNumeric = z.infer<typeof SharedGameDocumentTypeNumericSchema>;

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

// ========== FAQ & Errata (Issue #2373) ==========

/**
 * Game FAQ DTO
 */
export const GameFaqSchema = z.object({
  id: z.string().uuid(),
  question: z.string().min(1),
  answer: z.string().min(1),
  order: z.number().int().nonnegative(),
  createdAt: z.string(),
});

export type GameFaq = z.infer<typeof GameFaqSchema>;

/**
 * Game Errata DTO
 */
export const GameErrataSchema = z.object({
  id: z.string().uuid(),
  description: z.string().min(1),
  pageReference: z.string().min(1),
  publishedDate: z.string(),
  createdAt: z.string(),
});

export type GameErrata = z.infer<typeof GameErrataSchema>;

// ========== Documents (Issue #2391 Sprint 1) ==========

/**
 * Shared Game Document DTO
 */
export const SharedGameDocumentSchema = z.object({
  id: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  documentType: SharedGameDocumentTypeNumericSchema,
  version: z.string().regex(/^\d+\.\d+$/),
  isActive: z.boolean(),
  tags: z.array(z.string()).default([]),
  createdAt: z.string(),
  createdBy: z.string().uuid(),
});

export type SharedGameDocument = z.infer<typeof SharedGameDocumentSchema>;

/**
 * Game Designer DTO
 */
export const GameDesignerSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
});

export type GameDesigner = z.infer<typeof GameDesignerSchema>;

/**
 * Game Publisher DTO
 */
export const GamePublisherSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
});

export type GamePublisher = z.infer<typeof GamePublisherSchema>;

// ========== Shared Game DTOs ==========

/**
 * Shared game basic DTO (list view)
 */
export const SharedGameSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().nullable(), // 0 is valid (no BGG match)
  title: z.string().min(1),
  yearPublished: z.number().int(),
  description: z.string(),
  minPlayers: z.number().int().nonnegative(), // 0 = unknown
  maxPlayers: z.number().int().nonnegative(), // 0 = unknown
  playingTimeMinutes: z.number().int().nonnegative(), // 0 = unknown
  minAge: z.number().int().nonnegative(),
  complexityRating: z.number().nullable(),
  averageRating: z.number().nullable(),
  imageUrl: z.string().catch(''), // coerce null/missing to empty string
  thumbnailUrl: z.string().catch(''), // coerce null/missing to empty string
  status: GameStatusSchema, // Now string enum with JsonStringEnumConverter
  isRagPublic: z.boolean().default(false), // whether RAG access is public for all owners
  createdAt: z.string(), // Accept any datetime format from .NET serialization
  modifiedAt: z.string().nullable(),
});

export type SharedGame = z.infer<typeof SharedGameSchema>;

/**
 * Shared game detail DTO (single game view with full info)
 * Issue #2373 Phase 4: Extended with FAQs, Errata, Designers, Publishers, Categories, Mechanics
 */
export const SharedGameDetailSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().nullable(), // 0 is valid (no BGG match)
  title: z.string().min(1),
  yearPublished: z.number().int(),
  description: z.string(),
  minPlayers: z.number().int().nonnegative(), // 0 = unknown
  maxPlayers: z.number().int().nonnegative(), // 0 = unknown
  playingTimeMinutes: z.number().int().nonnegative(), // 0 = unknown
  minAge: z.number().int().nonnegative(),
  complexityRating: z.number().nullable(),
  averageRating: z.number().nullable(),
  imageUrl: z.string().catch(''), // coerce null/missing to empty string
  thumbnailUrl: z.string().catch(''), // coerce null/missing to empty string
  rules: GameRulesSchema.nullable(),
  status: GameStatusSchema, // Now string enum with JsonStringEnumConverter
  createdBy: z.string().uuid(),
  modifiedBy: z.string().uuid().nullable(),
  createdAt: z.string(), // Accept any datetime format from .NET serialization
  modifiedAt: z.string().nullable(),
  // Extended fields (Issue #2373)
  faqs: z.array(GameFaqSchema),
  erratas: z.array(GameErrataSchema),
  designers: z.array(GameDesignerSchema),
  publishers: z.array(GamePublisherSchema),
  categories: z.array(GameCategorySchema),
  mechanics: z.array(GameMechanicSchema),
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
  createdAt: z.string(),
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
  categories: z.array(z.string()).optional().default([]),
  mechanics: z.array(z.string()).optional().default([]),
  designers: z.array(z.string()).optional().default([]),
  publishers: z.array(z.string()).optional().default([]),
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

/**
 * Reject publication request body (Issue #2514)
 */
export const RejectPublicationRequestSchema = z.object({
  reason: z.string().min(10, 'Reason must be at least 10 characters').max(500),
});

export type RejectPublicationRequest = z.infer<typeof RejectPublicationRequestSchema>;

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
  publishedDate: z.string(),
});

export type AddErrataRequest = z.infer<typeof AddErrataRequestSchema>;

/**
 * Update errata request
 */
export const UpdateErrataRequestSchema = z.object({
  description: z.string().min(1).max(2000),
  pageReference: z.string().min(1).max(100),
  publishedDate: z.string(),
});

export type UpdateErrataRequest = z.infer<typeof UpdateErrataRequestSchema>;

// ========== Document Requests (Issue #2391 Sprint 1) ==========

/**
 * Add document to shared game request
 */
export const AddDocumentRequestSchema = z.object({
  pdfDocumentId: z.string().uuid(),
  documentType: SharedGameDocumentTypeNumericSchema,
  version: z.string().regex(/^\d+\.\d+$/),
  tags: z.array(z.string()).max(10).optional().nullable(),
  setAsActive: z.boolean(),
});

export type AddDocumentRequest = z.infer<typeof AddDocumentRequestSchema>;

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

// ========== Bulk Import (JSON Import) ==========

/**
 * Bulk game import DTO for JSON import
 * Used for batch importing games from JSON files
 */
export const BulkGameImportDtoSchema = z.object({
  bggId: z.number().int().positive().optional(),
  title: z.string().min(1).optional(),
  yearPublished: z.number().int().min(1900).max(2100).optional(),
  description: z.string().optional(),
  minPlayers: z.number().int().min(1).max(100).optional(),
  maxPlayers: z.number().int().min(1).max(100).optional(),
  playingTimeMinutes: z.number().int().min(1).max(10000).optional(),
  minAge: z.number().int().min(0).max(100).optional(),
  complexityRating: z.number().min(0).max(5).optional(),
  averageRating: z.number().min(0).max(10).optional(),
  imageUrl: z.string().url().optional(),
  thumbnailUrl: z.string().url().optional(),
});

export type BulkGameImportDto = z.infer<typeof BulkGameImportDtoSchema>;

/**
 * Bulk import result schema
 * Returns summary of import operation
 */
export const BulkImportResultSchema = z.object({
  successCount: z.number().int().nonnegative(),
  failureCount: z.number().int().nonnegative(),
  errors: z.array(z.string()),
  importedGameIds: z.array(z.string().uuid()),
});

export type BulkImportResult = z.infer<typeof BulkImportResultSchema>;

/**
 * Batch approval result schema
 * Issue #3350: Batch Approval/Rejection for Games
 * Returns summary of batch operation
 */
export const BatchApprovalResultSchema = z.object({
  successCount: z.number().int().nonnegative(),
  failureCount: z.number().int().nonnegative(),
  errors: z.array(z.string()),
});

export type BatchApprovalResult = z.infer<typeof BatchApprovalResultSchema>;

// ========== BGG Import/Update Flow (Admin Add from BGG) ==========

// Note: BggSearchResultSchema, BggSearchResult, BggGameDetailsSchema, and BggGameDetails
// are imported from games.schemas.ts and re-exported at the top of this file

/**
 * BGG Duplicate Check Result schema
 * Returns both existing game data and fresh BGG data for diff comparison
 */
export const BggDuplicateCheckResultSchema = z.object({
  isDuplicate: z.boolean(),
  existingGameId: z.string().uuid().nullable(),
  existingGame: SharedGameDetailSchema.nullable(),
  bggData: BggGameDetailsSchema.nullable(),
});

export type BggDuplicateCheckResult = z.infer<typeof BggDuplicateCheckResultSchema>;

/**
 * Update from BGG request body
 */
export interface UpdateFromBggRequest {
  bggId: number;
  fieldsToUpdate?: string[];
}

/**
 * Valid fields for selective BGG update
 */
export const BggUpdatableFields = [
  'title',
  'description',
  'yearPublished',
  'minPlayers',
  'maxPlayers',
  'playingTime',
  'minAge',
  'complexityRating',
  'averageRating',
  'imageUrl',
  'thumbnailUrl',
  'designers',
  'publishers',
  'categories',
  'mechanics',
] as const;

export type BggUpdatableField = (typeof BggUpdatableFields)[number];

// ========== Rulebook Analysis (Issue #5454) ==========

/**
 * Victory conditions DTO
 */
export const VictoryConditionsDtoSchema = z.object({
  primary: z.string(),
  alternatives: z.array(z.string()),
  isPointBased: z.boolean(),
  targetPoints: z.number().nullable(),
});

export type VictoryConditionsDto = z.infer<typeof VictoryConditionsDtoSchema>;

/**
 * Resource DTO
 */
export const ResourceDtoSchema = z.object({
  name: z.string(),
  type: z.string(),
  usage: z.string(),
  isLimited: z.boolean(),
});

export type ResourceDto = z.infer<typeof ResourceDtoSchema>;

/**
 * Game phase DTO
 */
export const GamePhaseDtoSchema = z.object({
  name: z.string(),
  description: z.string(),
  order: z.number().int(),
  isOptional: z.boolean(),
});

export type GamePhaseDto = z.infer<typeof GamePhaseDtoSchema>;

/**
 * Key concept DTO (glossary)
 */
export const KeyConceptDtoSchema = z.object({
  term: z.string(),
  definition: z.string(),
  category: z.string(),
});

export type KeyConceptDto = z.infer<typeof KeyConceptDtoSchema>;

/**
 * Generated FAQ DTO
 */
export const GeneratedFaqDtoSchema = z.object({
  question: z.string(),
  answer: z.string(),
  sourceSection: z.string(),
  confidence: z.number(),
  tags: z.array(z.string()),
});

export type GeneratedFaqDto = z.infer<typeof GeneratedFaqDtoSchema>;

/**
 * Rulebook analysis DTO — full structured analysis result
 */
export const RulebookAnalysisDtoSchema = z.object({
  id: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  gameTitle: z.string(),
  summary: z.string(),
  keyMechanics: z.array(z.string()),
  victoryConditions: VictoryConditionsDtoSchema.nullable(),
  resources: z.array(ResourceDtoSchema),
  gamePhases: z.array(GamePhaseDtoSchema),
  commonQuestions: z.array(z.string()),
  confidenceScore: z.number(),
  version: z.number().int(),
  isActive: z.boolean(),
  source: z.string().uuid(),
  analyzedAt: z.string(),
  createdBy: z.string().uuid(),
  keyConcepts: z.array(KeyConceptDtoSchema),
  generatedFaqs: z.array(GeneratedFaqDtoSchema),
  gameStateSchemaJson: z.string().nullable(),
  completionStatus: z.string(),
  missingSections: z.array(z.string()).nullable(),
});

export type RulebookAnalysisDto = z.infer<typeof RulebookAnalysisDtoSchema>;
