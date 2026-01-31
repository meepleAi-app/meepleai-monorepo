/**
 * AuthClient - Edge Cases for Branch Coverage (Issue #2309)
 *
 * Target: Branch coverage 62.5% → 90%+
 * Tests: Error paths, null handling, edge cases
 */

import { createAuthClient } from '../authClient';
import type { HttpClient } from '../../core/httpClient';

describe('authClient - Edge Cases (Issue #2309)', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let authClient: ReturnType<typeof createAuthClient>;

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn(),
      postFile: vi.fn(),
    } as any;

    authClient = createAuthClient({ httpClient: mockHttpClient });
  });

  describe('Null Response Handling', () => {
    it('should handle null from getTwoFactorStatus by throwing', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      await expect(authClient.getTwoFactorStatus()).rejects.toThrow('Failed to get 2FA status');
    });

    it('should return empty array from getUserSessions on null', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await authClient.getUserSessions();

      expect(result).toEqual([]);
    });

    it('should return empty array from searchUsers on null', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await authClient.searchUsers('query');

      expect(result).toEqual([]);
    });
  });

  describe('Error Propagation', () => {
    it('should propagate network errors from login', async () => {
      mockHttpClient.post.mockRejectedValueOnce(new Error('Network timeout'));

      await expect(
        authClient.login({ email: 'test@example.com', password: 'pass' })
      ).rejects.toThrow('Network timeout');
    });

    it('should propagate validation errors from register', async () => {
      mockHttpClient.post.mockRejectedValueOnce(new Error('Email already exists'));

      await expect(
        authClient.register({ email: 'existing@example.com', password: 'pass' })
      ).rejects.toThrow('Email already exists');
    });

    it('should propagate errors from updateProfile', async () => {
      mockHttpClient.put.mockRejectedValueOnce(new Error('Profile update failed'));

      await expect(authClient.updateProfile({ displayName: 'New' })).rejects.toThrow(
        'Profile update failed'
      );
    });

    it('should propagate errors from changePassword', async () => {
      mockHttpClient.put.mockRejectedValueOnce(new Error('Current password incorrect'));

      await expect(
        authClient.changePassword({ currentPassword: 'wrong', newPassword: 'new' })
      ).rejects.toThrow('Current password incorrect');
    });

    it('should propagate errors from updatePreferences', async () => {
      mockHttpClient.put.mockRejectedValueOnce(new Error('Preferences update failed'));

      await expect(authClient.updatePreferences({ language: 'en' })).rejects.toThrow(
        'Preferences update failed'
      );
    });
  });
});
