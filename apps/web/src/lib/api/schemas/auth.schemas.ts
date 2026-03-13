/**
 * Authentication API Schemas (FE-IMP-005)
 *
 * Zod schemas for validating Authentication bounded context responses.
 * Covers: Login, OAuth, 2FA, Sessions, User Profile
 */

import { z } from 'zod';

// ========== Core Authentication ==========

/**
 * Schema for AuthUser (simplified user data for authentication)
 */
export const AuthUserSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  displayName: z.string().nullable().optional(),
  role: z.string().min(1),
  onboardingCompleted: z.boolean().default(false),   // Issue #323
  onboardingSkipped: z.boolean().default(false),      // Issue #323
});

export type AuthUser = z.infer<typeof AuthUserSchema>;

/**
 * Schema for login response
 * Supports both normal login and 2FA challenge flow
 */
export const LoginResponseSchema = z.object({
  user: AuthUserSchema.nullable().optional(),
  requiresTwoFactor: z.boolean().default(false),
  tempSessionToken: z.string().nullable().optional(),
});

export type LoginResponse = z.infer<typeof LoginResponseSchema>;

/**
 * Schema for register response
 */
export const RegisterResponseSchema = z.object({
  user: AuthUserSchema,
});

export type RegisterResponse = z.infer<typeof RegisterResponseSchema>;

/**
 * Schema for logout response
 */
export const LogoutResponseSchema = z.object({
  ok: z.boolean().optional(),
  message: z.string().optional(),
});

export type LogoutResponse = z.infer<typeof LogoutResponseSchema>;

/**
 * Schema for current user (GET /api/v1/auth/me)
 */
export const CurrentUserResponseSchema = z.object({
  user: AuthUserSchema,
});

export type CurrentUserResponse = z.infer<typeof CurrentUserResponseSchema>;

// ========== Password Reset ==========

export const VerifyResetTokenResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string().optional(),
});

export type VerifyResetTokenResponse = z.infer<typeof VerifyResetTokenResponseSchema>;

export const RequestPasswordResetResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type RequestPasswordResetResponse = z.infer<typeof RequestPasswordResetResponseSchema>;

export const ConfirmPasswordResetResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type ConfirmPasswordResetResponse = z.infer<typeof ConfirmPasswordResetResponseSchema>;

// ========== Session Management ==========

export const SessionStatusResponseSchema = z.object({
  expiresAt: z.string().datetime(),
  lastSeenAt: z.string().datetime().nullable(),
  remainingMinutes: z.number().int().nonnegative(),
});

export type SessionStatusResponse = z.infer<typeof SessionStatusResponseSchema>;

export const UserSessionInfoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  userEmail: z.string().email(),
  createdAt: z.string().datetime(),
  expiresAt: z.string().datetime(),
  lastSeenAt: z.string().datetime().nullable(),
  revokedAt: z.string().datetime().nullable(),
  ipAddress: z.string().nullable(),
  userAgent: z.string().nullable(),
});

export type UserSessionInfo = z.infer<typeof UserSessionInfoSchema>;

export const RevokeSessionResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type RevokeSessionResponse = z.infer<typeof RevokeSessionResponseSchema>;

// ========== Two-Factor Authentication ==========

export const TotpSetupResponseSchema = z.object({
  secret: z.string().min(1),
  qrCodeUrl: z.string().url(),
  backupCodes: z.array(z.string().min(1)),
});

export type TotpSetupResponse = z.infer<typeof TotpSetupResponseSchema>;

export const TwoFactorStatusDtoSchema = z.object({
  isEnabled: z.boolean(),
  enabledAt: z.string().datetime().nullable(),
  unusedBackupCodesCount: z.number().int().nonnegative(),
});

export type TwoFactorStatusDto = z.infer<typeof TwoFactorStatusDtoSchema>;

export const Enable2FAResultSchema = z.object({
  success: z.boolean(),
  backupCodes: z.array(z.string().min(1)).nullable().optional(),
  errorMessage: z.string().nullable().optional(),
});

export type Enable2FAResult = z.infer<typeof Enable2FAResultSchema>;

export const Disable2FAResultSchema = z.object({
  success: z.boolean(),
  errorMessage: z.string().nullable().optional(),
});

export type Disable2FAResult = z.infer<typeof Disable2FAResultSchema>;

// ========== User Profile ==========

export const UserProfileSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  displayName: z.string().min(1),
  role: z.string().min(1),
  createdAt: z.string().datetime(),
  isTwoFactorEnabled: z.boolean(),
  twoFactorEnabledAt: z.string().datetime().nullable(),
  // User preferences (included in profile after Issue #1675)
  language: z.string().min(1),
  theme: z.string().min(1),
  emailNotifications: z.boolean(),
  dataRetentionDays: z.number().int().positive(),
  // Avatar URL (Issue #2882)
  avatarUrl: z.string().url().nullable().optional(),
});

export type UserProfile = z.infer<typeof UserProfileSchema>;

export const UpdateProfileResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type UpdateProfileResponse = z.infer<typeof UpdateProfileResponseSchema>;

/**
 * Schema for avatar upload response (Issue #2882)
 */
export const UploadAvatarResponseSchema = z.object({
  ok: z.boolean(),
  avatarUrl: z.string().url(),
  message: z.string().optional(),
});

export type UploadAvatarResponse = z.infer<typeof UploadAvatarResponseSchema>;

export const ChangePasswordResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type ChangePasswordResponse = z.infer<typeof ChangePasswordResponseSchema>;

// ========== User Preferences ==========

export const UserPreferencesSchema = z.object({
  language: z.string().min(1),
  emailNotifications: z.boolean(),
  theme: z.enum(['light', 'dark', 'system']),
  dataRetentionDays: z.number().int().positive(),
});

export type UserPreferences = z.infer<typeof UserPreferencesSchema>;

// ========== API Key Authentication ==========

export const ApiKeyLoginResponseSchema = z.object({
  user: UserProfileSchema,
  message: z.string(),
});

export type ApiKeyLoginResponse = z.infer<typeof ApiKeyLoginResponseSchema>;

// ========== API Key Management (Issue #909) ==========

/**
 * Schema for API Key DTO
 */
export const ApiKeyDtoSchema = z.object({
  id: z.string().uuid(),
  keyName: z.string().min(1),
  keyPrefix: z.string().min(1),
  scopes: z.string().min(1),
  createdAt: z.string().datetime(),
  expiresAt: z.string().datetime().nullable(),
  lastUsedAt: z.string().datetime().nullable(),
  isActive: z.boolean(),
});

export type ApiKeyDto = z.infer<typeof ApiKeyDtoSchema>;

/**
 * Schema for Create API Key Request
 */
export const CreateApiKeyRequestSchema = z.object({
  keyName: z.string().min(3).max(100),
  scopes: z.string().min(1),
  expiresAt: z.string().datetime().nullable().optional(),
  metadata: z.string().nullable().optional(),
});

export type CreateApiKeyRequest = z.infer<typeof CreateApiKeyRequestSchema>;

/**
 * Schema for Create API Key Response
 * Includes the plaintext key (only shown once)
 */
export const CreateApiKeyResponseSchema = z.object({
  id: z.string().uuid(),
  keyName: z.string().min(1),
  keyPrefix: z.string().min(1),
  plaintextKey: z.string().min(1),
  scopes: z.string().min(1),
  createdAt: z.string().datetime(),
  expiresAt: z.string().datetime().nullable(),
});

export type CreateApiKeyResponse = z.infer<typeof CreateApiKeyResponseSchema>;

/**
 * Schema for List API Keys Response
 */
export const ListApiKeysResponseSchema = z.object({
  items: z.array(ApiKeyDtoSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

export type ListApiKeysResponse = z.infer<typeof ListApiKeysResponseSchema>;

// ========== User Search (Admin) ==========

export const UserSearchResultSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string().min(1),
  email: z.string().email(),
});

export type UserSearchResult = z.infer<typeof UserSearchResultSchema>;

// ========== Revoke All Sessions (Issue #2056) ==========

/**
 * Schema for RevokeAllSessions request payload
 */
export const RevokeAllSessionsRequestSchema = z.object({
  includeCurrentSession: z.boolean().default(false),
  password: z.string().nullable().optional(),
});

export type RevokeAllSessionsRequest = z.infer<typeof RevokeAllSessionsRequestSchema>;

/**
 * Schema for RevokeAllSessions response
 */
export const RevokeAllSessionsResponseSchema = z.object({
  ok: z.boolean(),
  revokedCount: z.number().int().nonnegative(),
  currentSessionRevoked: z.boolean(),
  message: z.string(),
});

export type RevokeAllSessionsResponse = z.infer<typeof RevokeAllSessionsResponseSchema>;
