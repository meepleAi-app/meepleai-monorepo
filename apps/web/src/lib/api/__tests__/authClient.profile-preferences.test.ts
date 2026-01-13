/**
 * Auth Client Tests - User Profile & Preferences (FE-IMP-005)
 *
 * Tests for user profile management and preferences features.
 * Split from authClient.test.ts for better organization.
 */

import { createAuthClient } from '../clients/authClient';
import { HttpClient } from '../core/httpClient';
import type {
  UserProfile,
  UpdateProfileResponse,
  ChangePasswordResponse,
  UserPreferences,
} from '../schemas';
import { globalRequestCache } from '../core/requestCache';

describe('AuthClient - Profile & Preferences', () => {
  let mockFetch: Mock;
  let httpClient: HttpClient;
  let authClient: ReturnType<typeof createAuthClient>;
  const mockUserId = '123e4567-e89b-12d3-a456-426614174000';

  beforeEach(() => {
    // Clear request cache before each test
    globalRequestCache.clear();

    mockFetch = vi.fn();
    httpClient = new HttpClient({ fetchImpl: mockFetch });
    authClient = createAuthClient({ httpClient });
  });

  describe('User Profile', () => {
    describe('getProfile', () => {
      it('should get current user profile', async () => {
        const mockResponse: UserProfile = {
          id: mockUserId,
          email: 'user@meepleai.dev',
          displayName: 'Test User',
          role: 'User',
          createdAt: '2025-01-01T00:00:00Z',
          isTwoFactorEnabled: true,
          twoFactorEnabledAt: '2025-11-01T10:00:00Z',
          language: 'en',
          theme: 'dark',
          emailNotifications: true,
          dataRetentionDays: 30,
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
        const mockResponse: UserProfile = {
          id: mockUserId,
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
          createdAt: new Date().toISOString(),
          isTwoFactorEnabled: false,
          twoFactorEnabledAt: null,
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
        const mockResponse: UserProfile = {
          id: mockUserId,
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
          createdAt: new Date().toISOString(),
          isTwoFactorEnabled: false,
          twoFactorEnabledAt: null,
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
        const mockResponse: UserProfile = {
          id: mockUserId,
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
          createdAt: new Date().toISOString(),
          isTwoFactorEnabled: false,
          twoFactorEnabledAt: null,
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
        const mockResponse: UserProfile = {
          id: mockUserId,
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
          createdAt: new Date().toISOString(),
          isTwoFactorEnabled: false,
          twoFactorEnabledAt: null,
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
        const mockResponse: UserProfile = {
          id: mockUserId,
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
          createdAt: new Date().toISOString(),
          isTwoFactorEnabled: false,
          twoFactorEnabledAt: null,
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
