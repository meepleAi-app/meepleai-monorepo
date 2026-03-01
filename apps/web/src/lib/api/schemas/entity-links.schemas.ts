/**
 * EntityLinks API Schemas
 *
 * Zod schemas for EntityLink domain (Epic A — EntityRelationships backend).
 * Issue #5129 C — Card Navigation Graph Completion
 *
 * Enum values match C# JsonStringEnumConverter (PascalCase) output.
 */

import { z } from 'zod';

// ========== EntityLinkType Enum ==========

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

// ========== MeepleEntityType Enum (EntityLink domain) ==========

export const LinkEntityTypeSchema = z.enum([
  'Game',
  'Player',
  'Session',
  'Agent',
  'Document',
  'ChatSession',
  'Event',
  'Toolkit',
  'KbCard', // Issue #5191: KbCard entity type for PDF document links
]);

export type LinkEntityType = z.infer<typeof LinkEntityTypeSchema>;

// ========== EntityLink Scope ==========

export const EntityLinkScopeSchema = z.enum(['User', 'Shared']);

export type EntityLinkScope = z.infer<typeof EntityLinkScopeSchema>;

// ========== EntityLinkDto ==========

export const EntityLinkDtoSchema = z.object({
  id: z.string().uuid(),
  sourceEntityType: LinkEntityTypeSchema,
  sourceEntityId: z.string().uuid(),
  targetEntityType: LinkEntityTypeSchema,
  targetEntityId: z.string().uuid(),
  linkType: EntityLinkTypeSchema,
  isBidirectional: z.boolean(),
  scope: EntityLinkScopeSchema,
  ownerUserId: z.string().uuid(),
  metadata: z.string().nullable(),
  isAdminApproved: z.boolean(),
  isBggImported: z.boolean(),
  createdAt: z.string(),
  updatedAt: z.string(),
  isOwner: z.boolean().default(false),
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

// ========== CreateEntityLink Request ==========

export const CreateEntityLinkRequestSchema = z.object({
  sourceEntityType: LinkEntityTypeSchema,
  sourceEntityId: z.string().uuid(),
  targetEntityType: LinkEntityTypeSchema,
  targetEntityId: z.string().uuid(),
  linkType: EntityLinkTypeSchema,
  metadata: z.string().nullable().optional(),
});

export type CreateEntityLinkRequest = z.infer<typeof CreateEntityLinkRequestSchema>;

// ========== EntityLinkCount Response ==========

export const EntityLinkCountResponseSchema = z.object({
  count: z.number().int().nonnegative(),
});

export type EntityLinkCountResponse = z.infer<typeof EntityLinkCountResponseSchema>;
