/**
 * Auth Client Tests (FE-IMP-005)
 *
 * Tests for Authentication bounded context client.
 * Covers: Login, OAuth, 2FA, Sessions, User Profile, Preferences
 */

import { createAuthClient } from '../clients/authClient';
import { HttpClient } from '../core/httpClient';
import type {
  SessionStatusResponse,
  TotpSetupResponse,
  TwoFactorStatusDto,
  UserProfile,
  UpdateProfileResponse,
  ChangePasswordResponse,
  UserPreferences,
} from '../schemas';
import { globalRequestCache } from '../core/requestCache';

describe('AuthClient', () => {
  let mockFetch: jest.Mock;
  let httpClient: HttpClient;
  let authClient: ReturnType<typeof createAuthClient>;

  beforeEach(() => {
    // Clear request cache before each test
    globalRequestCache.clear();

    mockFetch = jest.fn();
    httpClient = new HttpClient({ fetchImpl: mockFetch });
    authClient = createAuthClient({ httpClient });
  });

  describe('Session Management', () => {
    describe('getSessionStatus', () => {
      it('should get current session status', async () => {
        const mockResponse: SessionStatusResponse = {
          ExpiresAt: '2025-11-17T12:00:00Z',
          LastSeenAt: '2025-11-17T11:00:00Z',
          RemainingMinutes: 30,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers({ 'X-Correlation-Id': 'test-session-status' }),
        });

        const result = await authClient.getSessionStatus();

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/auth/session/status'),
          expect.objectContaining({ method: 'GET' })
        );
      });

      it('should return null for 401 responses', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: false,
          status: 401,
          headers: new Headers(),
        });

        const result = await authClient.getSessionStatus();

        expect(result).toBeNull();
      });
    });

    describe('extendSession', () => {
      it('should extend current session', async () => {
        const mockResponse: SessionStatusResponse = {
          ExpiresAt: '2025-11-17T13:00:00Z',
          LastSeenAt: '2025-11-17T11:00:00Z',
          RemainingMinutes: 60,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.extendSession();

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/auth/session/extend'),
          expect.objectContaining({ method: 'POST' })
        );
      });
    });
  });

  describe('Two-Factor Authentication', () => {
    describe('getTwoFactorStatus', () => {
      it('should get 2FA status', async () => {
        const mockResponse: TwoFactorStatusDto = {
          IsEnabled: true,
          EnabledAt: '2025-11-01T10:00:00Z',
          UnusedBackupCodesCount: 8,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.getTwoFactorStatus();

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/me/2fa/status'),
          expect.objectContaining({ method: 'GET' })
        );
      });

      it('should throw error if response is null', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: false,
          status: 401,
          headers: new Headers(),
        });

        await expect(authClient.getTwoFactorStatus()).rejects.toThrow(
          'Failed to get 2FA status'
        );
      });
    });

    describe('setup2FA', () => {
      it('should setup 2FA and return QR code + backup codes', async () => {
        const mockResponse: TotpSetupResponse = {
          Secret: 'JBSWY3DPEHPK3PXP',
          QrCodeUrl: 'otpauth://totp/MeepleAI:user@example.com?secret=JBSWY3DPEHPK3PXP',
          BackupCodes: [
            '12345678',
            '23456789',
            '34567890',
            '45678901',
            '56789012',
            '67890123',
            '78901234',
            '89012345',
          ],
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.setup2FA();

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/auth/2fa/setup'),
          expect.objectContaining({ method: 'POST' })
        );
      });
    });

    describe('enable2FA', () => {
      it('should enable 2FA with verification code', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => ({
            Success: true,
            BackupCodes: ['12345678', '23456789'],
            ErrorMessage: null,
          }),
          headers: new Headers(),
        });

        const result = await authClient.enable2FA('123456');

        expect(result).toEqual({
          Success: true,
          BackupCodes: ['12345678', '23456789'],
          ErrorMessage: null,
        });
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/auth/2fa/enable'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({ code: '123456' }),
          })
        );
      });
    });

    describe('verify2FA', () => {
      it('should verify 2FA code during login', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 204,
          headers: new Headers(),
        });

        await authClient.verify2FA('654321');

        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/auth/2fa/verify'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({ code: '654321' }),
          })
        );
      });
    });

    describe('disable2FA', () => {
      it('should disable 2FA with password and code', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => ({ message: '2FA disabled' }),
          headers: new Headers(),
        });

        const result = await authClient.disable2FA('password123', '123456');

        expect(result).toEqual({
          Success: true,
          ErrorMessage: null,
        });
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/auth/2fa/disable'),
          expect.objectContaining({
            method: 'POST',
            body: JSON.stringify({
              password: 'password123',
              code: '123456',
            }),
          })
        );
      });
    });
  });

  describe('User Profile', () => {
    describe('getProfile', () => {
      it('should get current user profile', async () => {
        const mockResponse: UserProfile = {
          Id: '123e4567-e89b-12d3-a456-426614174000',
          Email: 'user@meepleai.dev',
          DisplayName: 'Test User',
          Role: 'User',
          CreatedAt: '2025-01-01T00:00:00Z',
          IsTwoFactorEnabled: true,
          TwoFactorEnabledAt: '2025-11-01T10:00:00Z',
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.getProfile();

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/profile'),
          expect.objectContaining({ method: 'GET' })
        );
      });

      it('should return null for 401 responses', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: false,
          status: 401,
          headers: new Headers(),
        });

        const result = await authClient.getProfile();

        expect(result).toBeNull();
      });
    });

    describe('updateProfile', () => {
      it('should update display name', async () => {
        const mockResponse: UpdateProfileResponse = {
          ok: true,
          message: 'Profile updated successfully',
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.updateProfile({
          displayName: 'New Name',
        });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/profile'),
          expect.objectContaining({
            method: 'PUT',
            body: JSON.stringify({ displayName: 'New Name' }),
          })
        );
      });

      it('should update email', async () => {
        const mockResponse: UpdateProfileResponse = {
          ok: true,
          message: 'Profile updated successfully',
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.updateProfile({
          email: 'newemail@meepleai.dev',
        });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/profile'),
          expect.objectContaining({
            method: 'PUT',
            body: JSON.stringify({ email: 'newemail@meepleai.dev' }),
          })
        );
      });

      it('should update both display name and email', async () => {
        const mockResponse: UpdateProfileResponse = {
          ok: true,
          message: 'Profile updated successfully',
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.updateProfile({
          displayName: 'New Name',
          email: 'newemail@meepleai.dev',
        });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/profile'),
          expect.objectContaining({
            method: 'PUT',
            body: JSON.stringify({
              displayName: 'New Name',
              email: 'newemail@meepleai.dev',
            }),
          })
        );
      });
    });

    describe('changePassword', () => {
      it('should change user password', async () => {
        const mockResponse: ChangePasswordResponse = {
          ok: true,
          message: 'Password changed successfully',
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.changePassword({
          currentPassword: 'oldpass123',
          newPassword: 'newpass456',
        });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/profile/password'),
          expect.objectContaining({
            method: 'PUT',
            body: JSON.stringify({
              currentPassword: 'oldpass123',
              newPassword: 'newpass456',
            }),
          })
        );
      });
    });
  });

  describe('User Preferences', () => {
    describe('getPreferences', () => {
      it('should get user preferences', async () => {
        const mockResponse: UserPreferences = {
          language: 'en',
          emailNotifications: true,
          theme: 'dark',
          dataRetentionDays: 30,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.getPreferences();

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/preferences'),
          expect.objectContaining({ method: 'GET' })
        );
      });

      it('should return null for 401 responses', async () => {
        mockFetch.mockResolvedValueOnce({
          ok: false,
          status: 401,
          headers: new Headers(),
        });

        const result = await authClient.getPreferences();

        expect(result).toBeNull();
      });
    });

    describe('updatePreferences', () => {
      it('should update language preference', async () => {
        const mockResponse: UserPreferences = {
          language: 'it',
          emailNotifications: true,
          theme: 'dark',
          dataRetentionDays: 30,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.updatePreferences({
          language: 'it',
        });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/preferences'),
          expect.objectContaining({
            method: 'PUT',
            body: JSON.stringify({ language: 'it' }),
          })
        );
      });

      it('should update theme preference', async () => {
        const mockResponse: UserPreferences = {
          language: 'en',
          emailNotifications: true,
          theme: 'light',
          dataRetentionDays: 30,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.updatePreferences({
          theme: 'light',
        });

        expect(result).toEqual(mockResponse);
        expect(mockFetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/users/preferences'),
          expect.objectContaining({
            method: 'PUT',
            body: JSON.stringify({ theme: 'light' }),
          })
        );
      });

      it('should update email notifications', async () => {
        const mockResponse: UserPreferences = {
          language: 'en',
          emailNotifications: false,
          theme: 'dark',
          dataRetentionDays: 30,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.updatePreferences({
          emailNotifications: false,
        });

        expect(result).toEqual(mockResponse);
      });

      it('should update data retention days', async () => {
        const mockResponse: UserPreferences = {
          language: 'en',
          emailNotifications: true,
          theme: 'dark',
          dataRetentionDays: 90,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.updatePreferences({
          dataRetentionDays: 90,
        });

        expect(result).toEqual(mockResponse);
      });

      it('should update multiple preferences at once', async () => {
        const mockResponse: UserPreferences = {
          language: 'it',
          emailNotifications: false,
          theme: 'system',
          dataRetentionDays: 60,
        };

        mockFetch.mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => mockResponse,
          headers: new Headers(),
        });

        const result = await authClient.updatePreferences({
          language: 'it',
          emailNotifications: false,
          theme: 'system',
          dataRetentionDays: 60,
        });

        expect(result).toEqual(mockResponse);
      });
    });
  });
});
