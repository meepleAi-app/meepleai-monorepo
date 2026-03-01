/**
 * EntityLink API Schemas (Issue #5142 — Epic A EntityRelationships)
 *
 * Zod schemas for EntityLink bounded context responses.
 * Covers: EntityLink DTO, create/delete requests (user + admin).
 */

import { z } from 'zod';

// ========== Enums ==========

export const MeepleEntityTypeSchema = z.enum([
  'Game',
  'Player',
  'Session',
  'Agent',
  'Document',
  'ChatSession',
  'Event',
  'Toolkit',
  'KbCard',
]);
export type MeepleEntityType = z.infer<typeof MeepleEntityTypeSchema>;

export const EntityLinkTypeSchema = z.enum([
  'ExpansionOf',
  'SequelOf',
  'Reimplements',
  'CompanionTo',
  'RelatedTo',
  'PartOf',
  'CollaboratesWith',
  'SpecializedBy',
]);
export type EntityLinkType = z.infer<typeof EntityLinkTypeSchema>;

export const EntityLinkScopeSchema = z.enum(['User', 'Shared']);
export type EntityLinkScope = z.infer<typeof EntityLinkScopeSchema>;

// ========== DTOs ==========

export const EntityLinkDtoSchema = z.object({
  id: z.string().uuid(),
  sourceEntityType: MeepleEntityTypeSchema,
  sourceEntityId: z.string().uuid(),
  targetEntityType: MeepleEntityTypeSchema,
  targetEntityId: z.string().uuid(),
  linkType: EntityLinkTypeSchema,
  isBidirectional: z.boolean(),
  scope: EntityLinkScopeSchema,
  ownerUserId: z.string().uuid(),
  metadata: z.string().nullable().optional(),
  isAdminApproved: z.boolean(),
  isBggImported: z.boolean(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  isOwner: z.boolean().optional().default(false),
  // Issue #5188: KbCard status enrichment (only present when targetEntityType=KbCard)
  kbCardStatus: z
    .object({
      fileName: z.string(),
      fileSizeBytes: z.number().nonnegative(),
      processingState: z.string(),
      progressPercentage: z.number().int().min(0).max(100),
      canRetry: z.boolean(),
      errorCategory: z.string().nullable(),
      processingError: z.string().nullable(),
    })
    .nullable()
    .optional(),
});
export type EntityLinkDto = z.infer<typeof EntityLinkDtoSchema>;

export const EntityLinkCountResponseSchema = z.object({
  count: z.number().int().min(0),
});
export type EntityLinkCountResponse = z.infer<typeof EntityLinkCountResponseSchema>;

export const ImportBggExpansionsResponseSchema = z.object({
  created: z.number().int().min(0),
});
export type ImportBggExpansionsResponse = z.infer<typeof ImportBggExpansionsResponseSchema>;

// ========== Request types ==========

export interface CreateEntityLinkRequest {
  sourceEntityType: MeepleEntityType;
  sourceEntityId: string;
  targetEntityType: MeepleEntityType;
  targetEntityId: string;
  linkType: EntityLinkType;
  metadata?: string | null;
}

export interface GetEntityLinksParams {
  entityType: MeepleEntityType;
  entityId: string;
  linkType?: EntityLinkType;
}
