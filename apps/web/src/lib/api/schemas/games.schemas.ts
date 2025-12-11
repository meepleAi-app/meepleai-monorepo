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
  // Issue #1830: UI-003 GameCard enhancements
  imageUrl: z.string().url().nullable().optional(),
  description: z.string().nullable().optional(),
  faqCount: z.number().int().nonnegative().nullable().optional(),
  averageRating: z.number().nullable().optional(),
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

// Simple array response (used by getAll())
export const GamesArrayResponseSchema = z.array(GameSchema);
export type GamesArrayResponse = z.infer<typeof GamesArrayResponseSchema>;

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

// ========== RuleSpec Management ==========

/**
 * Rule Atom DTO Schema (Issue #1977)
 * Matches RuleAtomDto from backend GameManagement context
 */
export const RuleAtomSchema = z.object({
  id: z.string(),
  text: z.string(),
  section: z
    .string()
    .nullish()
    .transform(val => val ?? null),
  page: z
    .string()
    .nullish()
    .transform(val => val ?? null),
  line: z
    .string()
    .nullish()
    .transform(val => val ?? null),
});

export type RuleAtom = z.infer<typeof RuleAtomSchema>;

/**
 * RuleSpec DTO Schema (Issue #1977)
 * Matches RuleSpecDto from backend GameManagement context
 */
export const RuleSpecSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  version: z.string(),
  createdAt: z.string().datetime(),
  createdByUserId: z.string().uuid().nullable(),
  parentVersionId: z.string().uuid().nullable(),
  atoms: z.array(RuleAtomSchema),
});

export type RuleSpec = z.infer<typeof RuleSpecSchema>;

/**
 * RuleSpec Version DTO Schema (Issue #1977)
 * Matches RuleSpecVersionDto from backend GetVersionHistoryQuery
 */
export const RuleSpecVersionSchema = z.object({
  version: z.string(),
  createdAt: z.string().datetime(),
  atomCount: z.number().int().nonnegative(),
  createdByUserName: z.string().nullable(),
});

export type RuleSpecVersion = z.infer<typeof RuleSpecVersionSchema>;

/**
 * RuleSpec History DTO Schema (Issue #1977)
 * Matches RuleSpecHistoryDto from backend GetVersionHistoryQuery
 */
export const RuleSpecHistorySchema = z.object({
  gameId: z.string().uuid(),
  versions: z.array(RuleSpecVersionSchema),
  totalVersions: z.number().int().nonnegative(),
});

export type RuleSpecHistory = z.infer<typeof RuleSpecHistorySchema>;

/**
 * Version Node DTO Schema (Issue #1977)
 * Matches VersionNodeDto from backend GetVersionTimelineQuery
 */
export const VersionNodeSchema = z.object({
  version: z.string(),
  createdAt: z.string().datetime(),
  parentVersion: z.string().nullable(),
  createdByUserName: z.string().nullable(),
  atomCount: z.number().int().nonnegative(),
  isActive: z.boolean(),
});

export type VersionNode = z.infer<typeof VersionNodeSchema>;

/**
 * Version Timeline DTO Schema (Issue #1977)
 * Matches VersionTimelineDto from backend GetVersionTimelineQuery
 */
export const VersionTimelineSchema = z.object({
  gameId: z.string().uuid(),
  versions: z.array(VersionNodeSchema),
  totalVersions: z.number().int().nonnegative(),
  authors: z.array(z.string()).optional(),
});

export type VersionTimeline = z.infer<typeof VersionTimelineSchema>;

/**
 * Field Change DTO Schema (Issue #1977)
 * Represents a field-level change in rule atom
 */
export const FieldChangeSchema = z.object({
  fieldName: z.string(),
  oldValue: z.string().nullable(),
  newValue: z.string().nullable(),
});

export type FieldChange = z.infer<typeof FieldChangeSchema>;

/**
 * Change Type Enum (Issue #1977)
 * Matches ChangeType from backend
 */
export const ChangeTypeSchema = z.enum(['Added', 'Modified', 'Deleted', 'Unchanged']);
export type ChangeType = z.infer<typeof ChangeTypeSchema>;

/**
 * Rule Atom Change DTO Schema (Issue #1977)
 * Represents a single atom change in diff
 */
export const RuleAtomChangeSchema = z.object({
  type: ChangeTypeSchema,
  oldAtom: z.string().nullable(),
  newAtom: z.string().nullable(),
  oldValue: RuleAtomSchema.nullable(),
  newValue: RuleAtomSchema.nullable(),
  fieldChanges: z.array(FieldChangeSchema).nullable(),
});

export type RuleAtomChange = z.infer<typeof RuleAtomChangeSchema>;

/**
 * Diff Summary DTO Schema (Issue #1977)
 * Summary statistics for diff operation
 */
export const DiffSummarySchema = z.object({
  totalChanges: z.number().int().nonnegative(),
  added: z.number().int().nonnegative(),
  modified: z.number().int().nonnegative(),
  deleted: z.number().int().nonnegative(),
  unchanged: z.number().int().nonnegative(),
});

export type DiffSummary = z.infer<typeof DiffSummarySchema>;

/**
 * RuleSpec Diff DTO Schema (Issue #1977)
 * Matches RuleSpecDiff from backend Models
 */
export const RuleSpecDiffSchema = z.object({
  gameId: z.string().uuid(),
  fromVersion: z.string(),
  toVersion: z.string(),
  fromCreatedAt: z.string().datetime(),
  toCreatedAt: z.string().datetime(),
  summary: DiffSummarySchema,
  changes: z.array(RuleAtomChangeSchema),
});

export type RuleSpecDiff = z.infer<typeof RuleSpecDiffSchema>;
