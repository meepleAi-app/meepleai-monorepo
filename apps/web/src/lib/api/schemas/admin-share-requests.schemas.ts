import { z } from 'zod';
import { ShareRequestStatusSchema, ContributionTypeSchema } from './share-requests.schemas';

/**
 * Admin Share Request API Schemas
 * Issue #2745: Frontend - Admin Review Interface
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 */

// Enums
export const ShareRequestSortFieldSchema = z.enum([
  'CreatedAt',
  'GameTitle',
  'ContributorName',
  'Status',
]);

export type ShareRequestSortField = z.infer<typeof ShareRequestSortFieldSchema>;

export const SortDirectionSchema = z.enum(['Ascending', 'Descending']);

export type SortDirection = z.infer<typeof SortDirectionSchema>;

export const ShareRequestHistoryActionSchema = z.enum([
  'Created',
  'DocumentsUpdated',
  'ReviewStarted',
  'ReviewReleased',
  'ChangesRequested',
  'Resubmitted',
  'Approved',
  'Rejected',
  'Withdrawn',
]);

export type ShareRequestHistoryAction = z.infer<typeof ShareRequestHistoryActionSchema>;

// Badge DTO
export const BadgeDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  iconUrl: z.string().nullable(),
  awardedAt: z.string().datetime(),
});

export type BadgeDto = z.infer<typeof BadgeDtoSchema>;

// Contributor Profile DTO
export const ContributorProfileDtoSchema = z.object({
  userId: z.string().uuid(),
  userName: z.string(),
  avatarUrl: z.string().nullable(),
  joinedAt: z.string().datetime(),
  totalContributions: z.number().int().nonnegative(),
  approvedContributions: z.number().int().nonnegative(),
  approvalRate: z.number().min(0).max(1),
  badges: z.array(BadgeDtoSchema),
});

export type ContributorProfileDto = z.infer<typeof ContributorProfileDtoSchema>;

// Document Preview DTO
export const DocumentPreviewDtoSchema = z.object({
  documentId: z.string().uuid(),
  fileName: z.string(),
  contentType: z.string(),
  fileSize: z.number().int().positive(),
  previewUrl: z.string().nullable(),
  pageCount: z.number().int().positive().nullable(),
});

export type DocumentPreviewDto = z.infer<typeof DocumentPreviewDtoSchema>;

// Game Details DTO
export const GameDetailsDtoSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  description: z.string().nullable(),
  thumbnailUrl: z.string().nullable(),
  bggId: z.number().int().positive().nullable(),
  minPlayers: z.number().int().positive().nullable(),
  maxPlayers: z.number().int().positive().nullable(),
  playingTime: z.number().int().positive().nullable(),
  complexity: z.number().min(0).max(5).nullable(),
  categories: z.array(z.string()),
  mechanisms: z.array(z.string()),
});

export type GameDetailsDto = z.infer<typeof GameDetailsDtoSchema>;

// Lock Status DTO
export const LockStatusDtoSchema = z.object({
  isLocked: z.boolean(),
  isLockedByCurrentAdmin: z.boolean(),
  lockedByAdminId: z.string().uuid().nullable(),
  lockedByAdminName: z.string().nullable(),
  lockExpiresAt: z.string().datetime().nullable(),
});

export type LockStatusDto = z.infer<typeof LockStatusDtoSchema>;

// Share Request History Entry DTO
export const ShareRequestHistoryEntryDtoSchema = z.object({
  timestamp: z.string().datetime(),
  action: ShareRequestHistoryActionSchema,
  actorId: z.string().uuid().nullable(),
  actorName: z.string().nullable(),
  details: z.string().nullable(),
});

export type ShareRequestHistoryEntryDto = z.infer<typeof ShareRequestHistoryEntryDtoSchema>;

// Admin Share Request DTO (for queue list)
export const AdminShareRequestDtoSchema = z.object({
  id: z.string().uuid(),
  status: ShareRequestStatusSchema,
  contributionType: ContributionTypeSchema,

  // Game preview
  sourceGameId: z.string().uuid(),
  gameTitle: z.string(),
  gameThumbnailUrl: z.string().nullable(),
  bggId: z.number().int().positive().nullable(),

  // Contributor info
  userId: z.string().uuid(),
  userName: z.string(),
  userAvatarUrl: z.string().nullable(),
  userTotalContributions: z.number().int().nonnegative(),

  // Request details
  userNotes: z.string().nullable(),
  attachedDocumentCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime(),

  // Lock info
  isInReview: z.boolean(),
  reviewingAdminId: z.string().uuid().nullable(),
  reviewingAdminName: z.string().nullable(),
  reviewStartedAt: z.string().datetime().nullable(),

  // For additional content
  targetSharedGameId: z.string().uuid().nullable(),
  targetSharedGameTitle: z.string().nullable(),
});

export type AdminShareRequestDto = z.infer<typeof AdminShareRequestDtoSchema>;

// Share Request Details DTO (for detail page)
export const ShareRequestDetailsDtoSchema = z.object({
  id: z.string().uuid(),
  status: ShareRequestStatusSchema,
  contributionType: ContributionTypeSchema,

  // Full game details
  sourceGame: GameDetailsDtoSchema,

  // If additional content, show existing game for comparison
  targetSharedGame: GameDetailsDtoSchema.nullable(),

  // Contributor details
  contributor: ContributorProfileDtoSchema,

  // Request content
  userNotes: z.string().nullable(),
  attachedDocuments: z.array(DocumentPreviewDtoSchema),

  // History
  history: z.array(ShareRequestHistoryEntryDtoSchema),

  // Lock status
  lockStatus: LockStatusDtoSchema,

  // Timestamps
  createdAt: z.string().datetime(),
  resolvedAt: z.string().datetime().nullable(),
});

export type ShareRequestDetailsDto = z.infer<typeof ShareRequestDetailsDtoSchema>;

// Active Review DTO (for "My Reviews" page)
export const ActiveReviewDtoSchema = z.object({
  shareRequestId: z.string().uuid(),
  sourceGameId: z.string().uuid(),
  gameTitle: z.string(),
  contributorId: z.string().uuid(),
  contributorName: z.string(),
  reviewStartedAt: z.string().datetime(),
  reviewLockExpiresAt: z.string().datetime(),
  status: ShareRequestStatusSchema,
});

export type ActiveReviewDto = z.infer<typeof ActiveReviewDtoSchema>;

// Paginated Admin Share Requests Response
export const PaginatedAdminShareRequestsResponseSchema = z.object({
  items: z.array(AdminShareRequestDtoSchema),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  totalCount: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
  hasNextPage: z.boolean(),
  hasPreviousPage: z.boolean(),
});

export type PaginatedAdminShareRequestsResponse = z.infer<
  typeof PaginatedAdminShareRequestsResponseSchema
>;

// Start Review Response
export const StartReviewResponseSchema = z.object({
  shareRequestId: z.string().uuid(),
  lockExpiresAt: z.string().datetime(),
  requestDetails: ShareRequestDetailsDtoSchema,
});

export type StartReviewResponse = z.infer<typeof StartReviewResponseSchema>;

// Request/Command Schemas

// Approve Request Data
export const ApproveRequestDataSchema = z.object({
  modifiedTitle: z.string().min(1).max(255).optional(),
  modifiedDescription: z.string().max(5000).optional(),
  documentsToInclude: z.array(z.string().uuid()).optional(),
  adminNotes: z.string().max(2000).optional(),
});

export type ApproveRequestData = z.infer<typeof ApproveRequestDataSchema>;

// Reject Request Data
export const RejectRequestDataSchema = z.object({
  reason: z.string().min(10, 'Rejection reason must be at least 10 characters').max(2000),
});

export type RejectRequestData = z.infer<typeof RejectRequestDataSchema>;

// Request Changes Data
export const RequestChangesDataSchema = z.object({
  feedback: z.string().min(10, 'Feedback must be at least 10 characters').max(2000),
});

export type RequestChangesData = z.infer<typeof RequestChangesDataSchema>;

// Query Parameters for List

export const GetAdminShareRequestsParamsSchema = z.object({
  page: z.number().int().positive().optional().default(1),
  pageSize: z.number().int().positive().max(100).optional().default(20),
  status: ShareRequestStatusSchema.optional(),
  contributionType: ContributionTypeSchema.optional(),
  searchTerm: z.string().max(255).optional(),
  sortBy: ShareRequestSortFieldSchema.optional().default('CreatedAt'),
  sortDirection: SortDirectionSchema.optional().default('Descending'),
});

export type GetAdminShareRequestsParams = z.infer<typeof GetAdminShareRequestsParamsSchema>;
