/**
 * AuthClient - Remaining Methods Tests (Issue #2309)
 *
 * Coverage gap: 58.24% → 90%
 * Tests: Sessions, Profile, Preferences, API Keys CRUD
 */

import { createAuthClient } from '../clients/authClient';
import type { HttpClient } from '../core/httpClient';
import type {
  UserSessionInfo,
  UserProfile,
  UserPreferences,
  ApiKeyDto,
  CreateApiKeyRequest,
} from '../schemas';

describe('AuthClient - Remaining Methods (Issue #2309)', () => {
  let mockHttpClient: Mocked<HttpClient>;
  let authClient: ReturnType<typeof createAuthClient>;

  beforeEach(() => {
    vi.clearAllMocks();

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

  // ========== Session Management ==========
  describe('getUserSessions', () => {
    it('should get all user sessions', async () => {
      const mockSessions: UserSessionInfo[] = [
        {
          sessionId: 'session-1',
          createdAt: '2024-01-01',
          expiresAt: '2024-01-02',
          ipAddress: '192.168.1.1',
          userAgent: 'Mozilla/5.0',
          isCurrent: true,
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockSessions);

      const result = await authClient.getUserSessions();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/users/me/sessions',
        expect.anything()
      );
      expect(result).toEqual(mockSessions);
    });

    it('should return empty array when null response', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await authClient.getUserSessions();

      expect(result).toEqual([]);
    });
  });

  describe('revokeSession', () => {
    it('should revoke specific session', async () => {
      const mockResponse = { success: true, message: 'Session revoked' };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await authClient.revokeSession('session-123');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/sessions/session-123/revoke',
        {},
        expect.anything()
      );
      expect(result.success).toBe(true);
    });

    it('should encode session ID in URL', async () => {
      mockHttpClient.post.mockResolvedValueOnce({ success: true, message: 'OK' });

      await authClient.revokeSession('session-with/special');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        expect.stringContaining('session-with%2Fspecial'),
        {},
        expect.anything()
      );
    });
  });

  // ========== 2FA Methods ==========
  describe('setup2FA', () => {
    it('should setup 2FA', async () => {
      const mockSetup = {
        qrCodeUrl: 'data:image/png;base64,xxx',
        manualEntryKey: 'ABCD1234',
        backupCodes: ['code1', 'code2'],
      };

      mockHttpClient.post.mockResolvedValueOnce(mockSetup);

      const result = await authClient.setup2FA();

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/2fa/setup',
        {},
        expect.anything()
      );
      expect(result.qrCodeUrl).toBeDefined();
      expect(result.backupCodes).toHaveLength(2);
    });
  });

  describe('enable2FA', () => {
    it('should enable 2FA with code', async () => {
      const mockResult = { success: true, message: '2FA enabled' };

      mockHttpClient.post.mockResolvedValueOnce(mockResult);

      const result = await authClient.enable2FA('123456');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/2fa/enable',
        { code: '123456' },
        expect.anything()
      );
      expect(result.success).toBe(true);
    });
  });

  describe('verify2FA', () => {
    it('should verify 2FA code', async () => {
      mockHttpClient.post.mockResolvedValueOnce(undefined);

      await authClient.verify2FA('654321');

      expect(mockHttpClient.post).toHaveBeenCalledWith('/api/v1/auth/2fa/verify', {
        code: '654321',
      });
    });
  });

  describe('disable2FA', () => {
    it('should disable 2FA with password and code', async () => {
      const mockResult = { success: true, message: '2FA disabled' };

      mockHttpClient.post.mockResolvedValueOnce(mockResult);

      const result = await authClient.disable2FA('mypassword', '123456');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/2fa/disable',
        { password: 'mypassword', code: '123456' },
        expect.anything()
      );
      expect(result.success).toBe(true);
    });
  });

  // ========== User Profile ==========
  describe('getProfile', () => {
    it('should get user profile', async () => {
      const mockProfile: UserProfile = {
        id: 'user-1',
        email: 'user@example.com',
        displayName: 'User Name',
        role: 'User',
        createdAt: '2024-01-01',
        twoFactorEnabled: false,
      };

      mockHttpClient.get.mockResolvedValueOnce(mockProfile);

      const result = await authClient.getProfile();

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/users/profile', expect.anything());
      expect(result).toEqual(mockProfile);
    });

    it('should return null when profile not found', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await authClient.getProfile();

      expect(result).toBeNull();
    });
  });

  describe('updateProfile', () => {
    it('should update profile successfully', async () => {
      const updates = { displayName: 'New Name', email: 'newemail@example.com' };
      const mockResponse = {
        success: true,
        message: 'Profile updated',
        user: { id: '1', displayName: 'New Name' },
      };

      mockHttpClient.put.mockResolvedValueOnce(mockResponse);

      const result = await authClient.updateProfile(updates);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/users/profile',
        updates,
        expect.anything()
      );
      expect(result.success).toBe(true);
    });

    it('should update with partial payload', async () => {
      mockHttpClient.put.mockResolvedValueOnce({ success: true, message: 'OK', user: {} });

      await authClient.updateProfile({ displayName: 'Only Name' });

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/users/profile',
        { displayName: 'Only Name' },
        expect.anything()
      );
    });
  });

  describe('changePassword', () => {
    it('should change password successfully', async () => {
      const request = { currentPassword: 'old123', newPassword: 'new456' };
      const mockResponse = { success: true, message: 'Password changed' };

      mockHttpClient.put.mockResolvedValueOnce(mockResponse);

      const result = await authClient.changePassword(request);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/users/profile/password',
        request,
        expect.anything()
      );
      expect(result.success).toBe(true);
    });
  });

  describe('getPreferences', () => {
    it('should get user preferences', async () => {
      const mockPrefs: UserPreferences = {
        language: 'it',
        emailNotifications: true,
        theme: 'dark',
        dataRetentionDays: 90,
      };

      mockHttpClient.get.mockResolvedValueOnce(mockPrefs);

      const result = await authClient.getPreferences();

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/users/preferences',
        expect.anything()
      );
      expect(result).toEqual(mockPrefs);
    });

    it('should return null when preferences not set', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await authClient.getPreferences();

      expect(result).toBeNull();
    });
  });

  describe('updatePreferences', () => {
    it('should update preferences successfully', async () => {
      const updates = { language: 'en', theme: 'light' as const, emailNotifications: false };
      const mockProfile: UserProfile = {
        id: 'user-1',
        email: 'user@example.com',
        displayName: 'User',
        role: 'User',
        createdAt: '2024-01-01',
        twoFactorEnabled: false,
      };

      mockHttpClient.put.mockResolvedValueOnce(mockProfile);

      const result = await authClient.updatePreferences(updates);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/users/preferences',
        updates,
        expect.anything()
      );
      expect(result).toBeDefined();
    });
  });

  // ========== API Keys & User Operations ==========
  describe('listApiKeys', () => {
    it('should list API keys', async () => {
      const mockKeys: ApiKeyDto[] = [
        {
          id: 'key-1',
          name: 'Key 1',
          keyPrefix: 'mpl_',
          scopes: [],
          createdAt: '2024-01-01',
          expiresAt: null,
          lastUsedAt: null,
          isActive: true,
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce({ apiKeys: mockKeys });

      const result = await authClient.listApiKeys();

      expect(result.apiKeys).toHaveLength(1);
    });
  });

  describe('getApiKey', () => {
    it('should get API key by ID', async () => {
      const mockKey: ApiKeyDto = {
        id: 'key-1',
        name: 'My Key',
        keyPrefix: 'mpl_',
        scopes: [],
        createdAt: '2024-01-01',
        expiresAt: null,
        lastUsedAt: null,
        isActive: true,
      };

      mockHttpClient.get.mockResolvedValueOnce(mockKey);

      const result = await authClient.getApiKey('key-1');

      expect(result).toEqual(mockKey);
    });
  });

  describe('searchUsers', () => {
    it('should search users', async () => {
      mockHttpClient.get.mockResolvedValueOnce([
        { id: '1', email: 'user@example.com', displayName: 'User', role: 'User' },
      ]);

      const result = await authClient.searchUsers('user');

      expect(result).toHaveLength(1);
    });
  });

  describe('getMyActivity', () => {
    it('should get user activity', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ activities: [], total: 0 });

      const result = await authClient.getMyActivity();

      expect(result.activities).toEqual([]);
    });
  });
});
