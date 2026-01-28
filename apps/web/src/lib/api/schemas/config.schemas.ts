/**
 * System Configuration API Schemas (FE-IMP-005)
 *
 * Zod schemas for validating SystemConfiguration bounded context responses.
 * Covers: Dynamic configuration, feature flags, config history, validation
 */

import { z } from 'zod';

// ========== System Configuration ==========

export const SystemConfigurationDtoSchema = z.object({
  id: z.string().uuid(),
  key: z.string().min(1),
  value: z.string(),
  valueType: z.string().min(1),
  description: z.string().nullable(),
  category: z.string().min(1),
  isActive: z.boolean(),
  requiresRestart: z.boolean(),
  environment: z.string().min(1),
  version: z.number().int().positive(),
  previousValue: z.string().nullable(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  createdByUserId: z.string().uuid(),
  updatedByUserId: z.string().uuid().nullable(),
  lastToggledAt: z.string().datetime().nullable(),
  // Tier-based feature flags (Issue #3079) - optional until backend #3073 completes
  tierFree: z.boolean().optional(),
  tierNormal: z.boolean().optional(),
  tierPremium: z.boolean().optional(),
});

export type SystemConfigurationDto = z.infer<typeof SystemConfigurationDtoSchema>;

// ========== Subscription Tiers (Issue #3079) ==========

export const SubscriptionTierSchema = z.enum(['Free', 'Normal', 'Premium']);

export type SubscriptionTier = z.infer<typeof SubscriptionTierSchema>;

export const TIER_DESCRIPTIONS: Record<SubscriptionTier, string> = {
  Free: 'Basic access with limited features',
  Normal: 'Standard subscription with most features',
  Premium: 'Full access to all features',
};

export const TIER_ORDER: SubscriptionTier[] = ['Free', 'Normal', 'Premium'];

// ========== Paged Results ==========

export const PagedResultSchema = <T extends z.ZodTypeAny>(itemSchema: T) =>
  z.object({
    items: z.array(itemSchema),
    total: z.number().int().nonnegative(),
    page: z.number().int().positive(),
    pageSize: z.number().int().positive(),
  });

export type PagedResult<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

// ========== Configuration History ==========

export const ConfigurationHistoryDtoSchema = z.object({
  id: z.string().uuid(),
  configurationId: z.string().uuid(),
  key: z.string().min(1),
  oldValue: z.string(),
  newValue: z.string(),
  version: z.number().int().positive(),
  changedAt: z.string().datetime(),
  changedByUserId: z.string().uuid(),
  changeReason: z.string().min(1),
});

export type ConfigurationHistoryDto = z.infer<typeof ConfigurationHistoryDtoSchema>;

// ========== Configuration Validation ==========

export const ConfigurationValidationResultSchema = z.object({
  isValid: z.boolean(),
  errors: z.array(z.string()),
});

export type ConfigurationValidationResult = z.infer<typeof ConfigurationValidationResultSchema>;

// ========== Configuration Export/Import ==========

export const ConfigurationExportDtoSchema = z.object({
  configurations: z.array(SystemConfigurationDtoSchema),
  exportedAt: z.string().datetime(),
  environment: z.string().min(1),
});

export type ConfigurationExportDto = z.infer<typeof ConfigurationExportDtoSchema>;

// ========== Game Library Limits (Issue #2444) ==========

export const GameLibraryLimitsDtoSchema = z.object({
  freeTierLimit: z.number().int().min(1).max(1000),
  normalTierLimit: z.number().int().min(1).max(1000),
  premiumTierLimit: z.number().int().min(1).max(1000),
  lastUpdatedAt: z.string().datetime(),
  lastUpdatedByUserId: z.string().uuid().nullable(),
});

export type GameLibraryLimitsDto = z.infer<typeof GameLibraryLimitsDtoSchema>;

export const UpdateGameLibraryLimitsRequestSchema = z.object({
  freeTierLimit: z.number().int().min(1).max(1000),
  normalTierLimit: z.number().int().min(1).max(1000),
  premiumTierLimit: z.number().int().min(1).max(1000),
});

export type UpdateGameLibraryLimitsRequest = z.infer<typeof UpdateGameLibraryLimitsRequestSchema>;

// ========== PDF Upload Limits (Issue #3078) ==========

export const PdfUploadLimitsDtoSchema = z.object({
  maxFileSizeBytes: z.number().int().min(1),
  maxPagesPerDocument: z.number().int().min(1).max(10000),
  maxDocumentsPerGame: z.number().int().min(1).max(1000),
  allowedMimeTypes: z.array(z.string().min(1)),
  lastUpdatedAt: z.string().datetime(),
  lastUpdatedByUserId: z.string().uuid().nullable(),
});

export type PdfUploadLimitsDto = z.infer<typeof PdfUploadLimitsDtoSchema>;

export const UpdatePdfUploadLimitsRequestSchema = z.object({
  maxFileSizeBytes: z.number().int().min(1),
  maxPagesPerDocument: z.number().int().min(1).max(10000),
  maxDocumentsPerGame: z.number().int().min(1).max(1000),
  allowedMimeTypes: z.array(z.string().min(1)).min(1),
});

export type UpdatePdfUploadLimitsRequest = z.infer<typeof UpdatePdfUploadLimitsRequestSchema>;
