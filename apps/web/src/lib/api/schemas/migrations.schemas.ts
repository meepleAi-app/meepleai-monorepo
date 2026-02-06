/**
 * Proposal Migration API Schemas
 *
 * Zod schemas for post-approval migration choices.
 * Issue #3669: Phase 8 - Frontend Integration
 */

import { z } from 'zod';

// Migration action choices
export const MigrationActionSchema = z.enum(['KeepPrivate', 'MigrateToShared']);
export type MigrationAction = z.infer<typeof MigrationActionSchema>;

// Pending migration DTO
export const PendingMigrationDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  shareRequestId: z.string().uuid(),
  privateGameId: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  gameTitle: z.string(),
  approvedAt: z.string().datetime(),
  expiresAt: z.string().datetime(),
  sharedGameUrl: z.string().url().nullable().optional(),
});

export type PendingMigrationDto = z.infer<typeof PendingMigrationDtoSchema>;

// Migration choice request
export const MigrationChoiceRequestSchema = z.object({
  action: MigrationActionSchema,
});

export type MigrationChoiceRequest = z.infer<typeof MigrationChoiceRequestSchema>;

// Migration choice response
export const MigrationChoiceResponseSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  migrationId: z.string().uuid(),
  action: MigrationActionSchema,
});

export type MigrationChoiceResponse = z.infer<typeof MigrationChoiceResponseSchema>;
