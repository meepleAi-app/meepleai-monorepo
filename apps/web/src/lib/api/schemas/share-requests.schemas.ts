import { z } from 'zod';

/**
 * Share Request API Schemas
 * Issue #2743: Frontend - UI Condivisione da Libreria
 */

// Enums
export const ShareRequestStatusSchema = z.enum([
  'Pending',
  'InReview',
  'ChangesRequested',
  'Approved',
  'Rejected',
  'Withdrawn',
]);

export type ShareRequestStatus = z.infer<typeof ShareRequestStatusSchema>;

export const ContributionTypeSchema = z.enum(['NewGame', 'AdditionalContent']);

export type ContributionType = z.infer<typeof ContributionTypeSchema>;

// Rate Limit Status DTO
export const RateLimitStatusDtoSchema = z.object({
  currentPendingCount: z.number().int().nonnegative(),
  maxPendingAllowed: z.number().int().positive(),
  currentMonthlyCount: z.number().int().nonnegative(),
  maxMonthlyAllowed: z.number().int().positive(),
  isInCooldown: z.boolean(),
  cooldownEndsAt: z.string().datetime().nullable(),
  monthResetAt: z.string().datetime(),
});

export type RateLimitStatusDto = z.infer<typeof RateLimitStatusDtoSchema>;

// User Share Request DTO
export const UserShareRequestDtoSchema = z.object({
  id: z.string().uuid(),
  sourceGameId: z.string().uuid(),
  gameTitle: z.string(),
  gameThumbnailUrl: z.string().nullable(),
  status: ShareRequestStatusSchema,
  contributionType: ContributionTypeSchema,
  userNotes: z.string().nullable(),
  adminFeedback: z.string().nullable(),
  attachedDocumentCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime(),
  resolvedAt: z.string().datetime().nullable(),
  resultingSharedGameId: z.string().uuid().nullable(),
});

export type UserShareRequestDto = z.infer<typeof UserShareRequestDtoSchema>;

// Create Share Request Command (Request)
export const CreateShareRequestCommandSchema = z.object({
  sourceGameId: z.string().uuid(),
  notes: z.string().max(2000).nullable().optional(),
  attachedDocumentIds: z.array(z.string().uuid()).optional().default([]),
});

export type CreateShareRequestCommand = z.infer<typeof CreateShareRequestCommandSchema>;

// Create Share Request Response
export const CreateShareRequestResponseSchema = z.object({
  shareRequestId: z.string().uuid(),
  status: ShareRequestStatusSchema,
  contributionType: ContributionTypeSchema,
  createdAt: z.string().datetime(),
});

export type CreateShareRequestResponse = z.infer<typeof CreateShareRequestResponseSchema>;

// Paginated Share Requests Response
// Backend PagedResult<T> returns: { items, total, page, pageSize }
export const PaginatedShareRequestsResponseSchema = z.object({
  items: z.array(UserShareRequestDtoSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  total: z.number().int().nonnegative(),
});

export type PaginatedShareRequestsResponse = z.infer<typeof PaginatedShareRequestsResponseSchema>;
