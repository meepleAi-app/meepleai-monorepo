/**
 * Authentication Client (FE-IMP-005)
 *
 * Modular client for Authentication bounded context.
 * Covers: Sessions, 2FA, User Profile, Preferences
 */

import type { HttpClient } from '../core/httpClient';
import { setStoredApiKey, clearStoredApiKey } from '../core/apiKeyStore';
import {
  SessionStatusResponseSchema,
  UserSessionInfoSchema,
  RevokeSessionResponseSchema,
  TotpSetupResponseSchema,
  TwoFactorStatusDtoSchema,
  Enable2FAResultSchema,
  Disable2FAResultSchema,
  UserProfileSchema,
  UpdateProfileResponseSchema,
  ChangePasswordResponseSchema,
  UserPreferencesSchema,
  ApiKeyLoginResponseSchema,
  type SessionStatusResponse,
  type UserSessionInfo,
  type RevokeSessionResponse,
  type TotpSetupResponse,
  type TwoFactorStatusDto,
  type Enable2FAResult,
  type Disable2FAResult,
  type UserProfile,
  type UpdateProfileResponse,
  type ChangePasswordResponse,
  type UserPreferences,
  type ApiKeyLoginResponse,
} from '../schemas';

export interface CreateAuthClientParams {
  httpClient: HttpClient;
}

export interface UpdateProfileRequest {
  displayName?: string | null;
  email?: string | null;
}

export interface UpdatePreferencesRequest {
  language?: string;
  emailNotifications?: boolean;
  theme?: 'light' | 'dark' | 'system';
  dataRetentionDays?: number;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

/**
 * Authentication API client with Zod validation
 */
export function createAuthClient({ httpClient }: CreateAuthClientParams) {
  return {
    // ========== API Key Authentication ==========

    /**
     * Validate API key and store it for header-based auth
     */
    async loginWithApiKey(apiKey: string): Promise<ApiKeyLoginResponse> {
      const response = await httpClient.post('/api/v1/auth/apikey/login', { apiKey }, ApiKeyLoginResponseSchema);
      setStoredApiKey(apiKey.trim());
      return response;
    },

    /**
     * Logout API key authentication (clears stored key)
     */
    async logoutApiKey(): Promise<{ ok: boolean; message: string }> {
      const response = await httpClient.post<{ ok: boolean; message: string }>('/api/v1/auth/apikey/logout', {});
      clearStoredApiKey();
      return response;
    },

    // ========== Session Management ==========

    /**
     * Get current session status
     */
    async getSessionStatus(): Promise<SessionStatusResponse | null> {
      return httpClient.get(
        '/api/v1/auth/session/status',
        SessionStatusResponseSchema
      );
    },

    /**
     * Extend current session
     */
    async extendSession(): Promise<SessionStatusResponse> {
      return httpClient.post(
        '/api/v1/auth/session/extend',
        {},
        SessionStatusResponseSchema
      );
    },

    /**
     * Get all active sessions for current user
     */
    async getUserSessions(): Promise<UserSessionInfo[]> {
      const response = await httpClient.get(
        '/api/v1/users/me/sessions',
        UserSessionInfoSchema.array()
      );
      return response || [];
    },

    /**
     * Revoke a specific session
     * @param sessionId Session ID (GUID format)
     */
    async revokeSession(sessionId: string): Promise<RevokeSessionResponse> {
      return httpClient.post(
        `/api/v1/auth/sessions/${encodeURIComponent(sessionId)}/revoke`,
        {},
        RevokeSessionResponseSchema
      );
    },

    // ========== Two-Factor Authentication ==========

    /**
     * Get 2FA status for current user
     */
    async getTwoFactorStatus(): Promise<TwoFactorStatusDto> {
      const response = await httpClient.get(
        '/api/v1/users/me/2fa/status',
        TwoFactorStatusDtoSchema
      );
      if (!response) {
        throw new Error('Failed to get 2FA status');
      }
      return response;
    },

    /**
     * Setup 2FA (get QR code and backup codes)
     */
    async setup2FA(): Promise<TotpSetupResponse> {
      return httpClient.post(
        '/api/v1/auth/2fa/setup',
        {},
        TotpSetupResponseSchema
      );
    },

    /**
     * Enable 2FA with verification code
     */
    async enable2FA(code: string): Promise<Enable2FAResult> {
      const response = await httpClient.post<{ message: string; backupCodes?: string[] }>(
        '/api/v1/auth/2fa/enable',
        { code }
      );

      // Backend returns 204 No Content or { message, backupCodes }
      return {
        success: true,
        backupCodes: response?.backupCodes || null,
        errorMessage: null,
      };
    },

    /**
     * Verify 2FA code during login
     */
    async verify2FA(code: string): Promise<void> {
      await httpClient.post<void>('/api/v1/auth/2fa/verify', { code });
    },

    /**
     * Disable 2FA
     */
    async disable2FA(password: string, code: string): Promise<Disable2FAResult> {
      const response = await httpClient.post<{ message: string }>(
        '/api/v1/auth/2fa/disable',
        { password, code }
      );

      return {
        success: true,
        errorMessage: null,
      };
    },

    // ========== User Profile ==========

    /**
     * Get current user profile
     */
    async getProfile(): Promise<UserProfile | null> {
      return httpClient.get('/api/v1/users/profile', UserProfileSchema);
    },

    /**
     * Update user profile
     */
    async updateProfile(
      payload: UpdateProfileRequest
    ): Promise<UpdateProfileResponse> {
      return httpClient.put(
        '/api/v1/users/profile',
        payload,
        UpdateProfileResponseSchema
      );
    },

    /**
     * Change user password
     */
    async changePassword(
      request: ChangePasswordRequest
    ): Promise<ChangePasswordResponse> {
      return httpClient.put(
        '/api/v1/users/profile/password',
        request,
        ChangePasswordResponseSchema
      );
    },

    // ========== User Preferences ==========

    /**
     * Get user preferences
     */
    async getPreferences(): Promise<UserPreferences | null> {
      return httpClient.get('/api/v1/users/preferences', UserPreferencesSchema);
    },

    /**
     * Update user preferences
     */
    async updatePreferences(
      payload: UpdatePreferencesRequest
    ): Promise<UserPreferences> {
      return httpClient.put(
        '/api/v1/users/preferences',
        payload,
        UserPreferencesSchema
      );
    },
  };
}

export type AuthClient = ReturnType<typeof createAuthClient>;
