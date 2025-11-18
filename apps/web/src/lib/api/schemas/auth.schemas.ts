/**
 * Authentication API Schemas (FE-IMP-005)
 *
 * Zod schemas for validating Authentication bounded context responses.
 * Covers: Login, OAuth, 2FA, Sessions, User Profile
 */

import { z } from 'zod';

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
  email: z.string().email(),
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
  qrCodeUri: z.string().url(),
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
});

export type UserProfile = z.infer<typeof UserProfileSchema>;

export const UpdateProfileResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type UpdateProfileResponse = z.infer<typeof UpdateProfileResponseSchema>;

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

// ========== User Search (Admin) ==========

export const UserSearchResultSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string().min(1),
  email: z.string().email(),
});

export type UserSearchResult = z.infer<typeof UserSearchResultSchema>;
