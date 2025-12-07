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
});

export type AuthUser = z.infer<typeof AuthUserSchema>;

/**
 * Schema for login response
 */
export const LoginResponseSchema = z.object({
  user: AuthUserSchema,
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
  ExpiresAt: z.string().datetime(),
  LastSeenAt: z.string().datetime().nullable(),
  RemainingMinutes: z.number().int().nonnegative(),
});

export type SessionStatusResponse = z.infer<typeof SessionStatusResponseSchema>;

export const UserSessionInfoSchema = z.object({
  Id: z.string().uuid(),
  UserId: z.string().uuid(),
  UserEmail: z.string().email(),
  CreatedAt: z.string().datetime(),
  ExpiresAt: z.string().datetime(),
  LastSeenAt: z.string().datetime().nullable(),
  RevokedAt: z.string().datetime().nullable(),
  IpAddress: z.string().nullable(),
  UserAgent: z.string().nullable(),
});

export type UserSessionInfo = z.infer<typeof UserSessionInfoSchema>;

export const RevokeSessionResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type RevokeSessionResponse = z.infer<typeof RevokeSessionResponseSchema>;

// ========== Two-Factor Authentication ==========

export const TotpSetupResponseSchema = z.object({
  Secret: z.string().min(1),
  QrCodeUrl: z.string().url(),
  BackupCodes: z.array(z.string().min(1)),
});

export type TotpSetupResponse = z.infer<typeof TotpSetupResponseSchema>;

export const TwoFactorStatusDtoSchema = z.object({
  IsEnabled: z.boolean(),
  EnabledAt: z.string().datetime().nullable(),
  UnusedBackupCodesCount: z.number().int().nonnegative(),
});

export type TwoFactorStatusDto = z.infer<typeof TwoFactorStatusDtoSchema>;

export const Enable2FAResultSchema = z.object({
  Success: z.boolean(),
  BackupCodes: z.array(z.string().min(1)).nullable().optional(),
  ErrorMessage: z.string().nullable().optional(),
});

export type Enable2FAResult = z.infer<typeof Enable2FAResultSchema>;

export const Disable2FAResultSchema = z.object({
  Success: z.boolean(),
  ErrorMessage: z.string().nullable().optional(),
});

export type Disable2FAResult = z.infer<typeof Disable2FAResultSchema>;

// ========== User Profile ==========

export const UserProfileSchema = z.object({
  Id: z.string().uuid(),
  Email: z.string().email(),
  DisplayName: z.string().min(1),
  Role: z.string().min(1),
  CreatedAt: z.string().datetime(),
  IsTwoFactorEnabled: z.boolean(),
  TwoFactorEnabledAt: z.string().datetime().nullable(),
  // User preferences (included in profile after Issue #1675)
  Language: z.string().min(1),
  Theme: z.string().min(1),
  EmailNotifications: z.boolean(),
  DataRetentionDays: z.number().int().positive(),
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

// ========== API Key Authentication ==========

export const ApiKeyLoginResponseSchema = z.object({
  user: UserProfileSchema,
  message: z.string(),
});

export type ApiKeyLoginResponse = z.infer<typeof ApiKeyLoginResponseSchema>;

// ========== User Search (Admin) ==========

export const UserSearchResultSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string().min(1),
  email: z.string().email(),
});

export type UserSearchResult = z.infer<typeof UserSearchResultSchema>;
