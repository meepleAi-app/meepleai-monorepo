/**
 * Invitation API Schemas (Issue #132)
 *
 * Zod schemas for user invitation system — admin invitation management
 * and public invitation acceptance/validation endpoints.
 */

import { z } from 'zod';

// ──────────────────────────────────────────────
// Enums
// ──────────────────────────────────────────────

export const InvitationStatusSchema = z.enum(['Pending', 'Accepted', 'Expired']);

// ──────────────────────────────────────────────
// Core DTOs
// ──────────────────────────────────────────────

export const InvitationDtoSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  role: z.string(),
  status: InvitationStatusSchema,
  expiresAt: z.string(),
  createdAt: z.string(),
  acceptedAt: z.string().nullable(),
  invitedByUserId: z.string().uuid(),
});

export const InvitationStatsSchema = z.object({
  pending: z.number().int(),
  accepted: z.number().int(),
  expired: z.number().int(),
  total: z.number().int(),
});

// ──────────────────────────────────────────────
// Bulk Invite
// ──────────────────────────────────────────────

export const BulkInviteFailureSchema = z.object({
  email: z.string(),
  error: z.string(),
});

export const BulkInviteResponseSchema = z.object({
  successful: z.array(InvitationDtoSchema),
  failed: z.array(BulkInviteFailureSchema),
});

// ──────────────────────────────────────────────
// Token Validation
// ──────────────────────────────────────────────

export const TokenValidationSchema = z.object({
  valid: z.boolean(),
  role: z.string().nullable(),
  expiresAt: z.string().nullable(),
});

// ──────────────────────────────────────────────
// Paginated Invitations List
// ──────────────────────────────────────────────

export const GetInvitationsResponseSchema = z.object({
  items: z.array(InvitationDtoSchema),
  totalCount: z.number().int(),
  page: z.number().int(),
  pageSize: z.number().int(),
});

// ──────────────────────────────────────────────
// Accept Invitation Response
// ──────────────────────────────────────────────

export const AcceptInvitationResponseSchema = z.object({
  user: z.object({
    id: z.string().uuid(),
    email: z.string(),
    displayName: z.string().nullable(),
    role: z.string(),
    tier: z.string().nullable(),
    createdAt: z.string(),
    isTwoFactorEnabled: z.boolean(),
    twoFactorEnabledAt: z.string().nullable(),
    level: z.number().int(),
    experiencePoints: z.number().int(),
    emailVerified: z.boolean(),
    emailVerifiedAt: z.string().nullable(),
    verificationGracePeriodEndsAt: z.string().nullable(),
  }),
  sessionToken: z.string(),
  expiresAt: z.string(),
});

// ──────────────────────────────────────────────
// Type Exports
// ──────────────────────────────────────────────

export type InvitationStatus = z.infer<typeof InvitationStatusSchema>;
export type InvitationDto = z.infer<typeof InvitationDtoSchema>;
export type InvitationStats = z.infer<typeof InvitationStatsSchema>;
export type BulkInviteFailure = z.infer<typeof BulkInviteFailureSchema>;
export type BulkInviteResponse = z.infer<typeof BulkInviteResponseSchema>;
export type TokenValidation = z.infer<typeof TokenValidationSchema>;
export type GetInvitationsResponse = z.infer<typeof GetInvitationsResponseSchema>;
export type AcceptInvitationResponse = z.infer<typeof AcceptInvitationResponseSchema>;
