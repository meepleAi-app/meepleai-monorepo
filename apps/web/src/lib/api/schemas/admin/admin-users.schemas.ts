/**
 * Admin User Management Schemas
 *
 * User CRUD, activity timeline, sessions, and API key management.
 */

import { z } from 'zod';

import { ApiKeyDtoSchema } from '../auth.schemas';

// ========== User Management ==========

export const AdminUserSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  displayName: z.string().min(1),
  role: z.string().min(1),
  tier: z.string().optional().default('Free'), // Issue #3698: User tier
  tokenUsage: z.number().int().optional().default(0), // Issue #3698: Tokens used
  tokenLimit: z.number().int().optional().default(10_000), // Issue #3698: Monthly limit
  createdAt: z.string().datetime(),
  lastSeenAt: z.string().datetime().nullable().optional(),
  isTwoFactorEnabled: z.boolean().optional(),
  isSuspended: z.boolean().optional().default(false),
  suspendReason: z.string().nullable().optional(),
});

export type AdminUser = z.infer<typeof AdminUserSchema>;

export const SuspendUserRequestSchema = z.object({
  reason: z.string().optional(),
});

export type SuspendUserRequest = z.infer<typeof SuspendUserRequestSchema>;

export const CreateUserRequestSchema = z.object({
  email: z.string().email(),
  password: z.string().min(8),
  displayName: z.string().min(1),
  role: z.string().min(1),
});

export type CreateUserRequest = z.infer<typeof CreateUserRequestSchema>;

export const UpdateUserRequestSchema = z.object({
  displayName: z.string().min(1).optional(),
  role: z.string().min(1).optional(),
});

export type UpdateUserRequest = z.infer<typeof UpdateUserRequestSchema>;

export const AdminUserResponseSchema = z.object({
  user: AdminUserSchema,
  message: z.string().optional(),
});

export type AdminUserResponse = z.infer<typeof AdminUserResponseSchema>;

export const DeleteUserResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type DeleteUserResponse = z.infer<typeof DeleteUserResponseSchema>;

// ========== User Activity Timeline (Issue #911) ==========

export const UserActivityDtoSchema = z.object({
  id: z.string().uuid(),
  action: z.string().min(1),
  resource: z.string().min(1),
  resourceId: z.string().nullable().optional(),
  result: z.string().min(1),
  details: z.string().nullable().optional(),
  createdAt: z.string().datetime(),
  ipAddress: z.string().nullable().optional(),
});

export type UserActivityDto = z.infer<typeof UserActivityDtoSchema>;

export const GetUserActivityResultSchema = z.object({
  activities: z.array(UserActivityDtoSchema),
  totalCount: z.number().int().min(0),
});

export type GetUserActivityResult = z.infer<typeof GetUserActivityResultSchema>;

export interface UserActivityFilters {
  actionFilter?: string;
  resourceFilter?: string;
  startDate?: Date;
  endDate?: Date;
  limit?: number;
}

// ========== Admin Sessions ==========

export const AdminSessionInfoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  userEmail: z.string().email(),
  createdAt: z.string().datetime(),
  expiresAt: z.string().datetime(),
  lastSeenAt: z.string().datetime().nullable().optional(),
  revokedAt: z.string().datetime().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
});

export type AdminSessionInfo = z.infer<typeof AdminSessionInfoSchema>;

export interface GetAdminSessionsParams {
  limit?: number;
  userId?: string;
}

// ========== API Key Management (Issue #908) ==========

// Re-export ApiKeyDto from auth.schemas to avoid duplication
export type { ApiKeyDto } from '../auth.schemas';
export { ApiKeyDtoSchema } from '../auth.schemas';

export const ApiKeyUsageStatsDtoSchema = z.object({
  keyId: z.string().uuid(),
  totalUsageCount: z.number().int().nonnegative(),
  lastUsedAt: z.string().datetime().nullable().optional(),
  usageCountLast24Hours: z.number().int().nonnegative(),
  usageCountLast7Days: z.number().int().nonnegative(),
  usageCountLast30Days: z.number().int().nonnegative(),
  averageRequestsPerDay: z.number().nonnegative(),
});

export type ApiKeyUsageStatsDto = z.infer<typeof ApiKeyUsageStatsDtoSchema>;

export const ApiKeyWithStatsDtoSchema = z.object({
  apiKey: ApiKeyDtoSchema,
  usageStats: ApiKeyUsageStatsDtoSchema,
});

export type ApiKeyWithStatsDto = z.infer<typeof ApiKeyWithStatsDtoSchema>;

// Re-export CreateApiKey schemas from auth.schemas to avoid duplication
export type { CreateApiKeyRequest, CreateApiKeyResponse } from '../auth.schemas';
export { CreateApiKeyRequestSchema, CreateApiKeyResponseSchema } from '../auth.schemas';

export const UpdateApiKeyRequestSchema = z.object({
  keyName: z.string().min(1).optional(),
  scopes: z.string().min(1).optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

export type UpdateApiKeyRequest = z.infer<typeof UpdateApiKeyRequestSchema>;

export const GetAllApiKeysWithStatsResponseSchema = z.object({
  keys: z.array(ApiKeyWithStatsDtoSchema),
  count: z.number().int().nonnegative(),
  filters: z.object({
    userId: z.string().uuid().nullable().optional(),
    includeRevoked: z.boolean(),
  }),
});

export type GetAllApiKeysWithStatsResponse = z.infer<typeof GetAllApiKeysWithStatsResponseSchema>;

export const BulkImportApiKeysResultSchema = z.object({
  successCount: z.number().int().nonnegative(),
  failedCount: z.number().int().nonnegative(),
  errors: z.array(z.string()),
});

export type BulkImportApiKeysResult = z.infer<typeof BulkImportApiKeysResultSchema>;
