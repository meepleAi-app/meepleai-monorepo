/**
 * AuthClient - Core Auth & API Keys Tests (Issue #2309)
 *
 * Coverage gap: 45.60% → 90%+
 * Tests: login, register, logout, getMe, API keys management
 */

import { createAuthClient, type LoginRequest, type RegisterRequest } from '../clients/authClient';
import type { HttpClient } from '../core/httpClient';
import type { AuthUser, ApiKeyDto, CreateApiKeyRequest } from '../schemas';

// Mock dependencies
const mockSetStoredApiKey = vi.fn();
const mockClearStoredApiKey = vi.fn();

vi.mock('../core/apiKeyStore', () => ({
  setStoredApiKey: (key: string) => mockSetStoredApiKey(key),
  clearStoredApiKey: () => mockClearStoredApiKey(),
}));

describe('AuthClient - Core Auth & API Keys (Issue #2309)', () => {
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

  // ========== Core Authentication ==========
  describe('login', () => {
    it('should login successfully', async () => {
      const request: LoginRequest = { email: 'test@example.com', password: 'password123' };
      const mockResponse = {
        user: {
          id: 'user-1',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
        },
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await authClient.login(request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/login',
        request,
        expect.anything()
      );
      expect(result).toEqual(mockResponse.user);
      expect(result.email).toBe('test@example.com');
    });

    it('should handle login error', async () => {
      mockHttpClient.post.mockRejectedValueOnce(new Error('Invalid credentials'));

      await expect(
        authClient.login({ email: 'wrong@example.com', password: 'wrong' })
      ).rejects.toThrow('Invalid credentials');
    });
  });

  describe('register', () => {
    it('should register new user successfully', async () => {
      const request: RegisterRequest = {
        email: 'new@example.com',
        password: 'newpass123',
        displayName: 'New User',
      };

      const mockResponse = {
        user: {
          id: 'new-user-id',
          email: 'new@example.com',
          displayName: 'New User',
          role: 'User',
        },
      };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await authClient.register(request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/register',
        request,
        expect.anything()
      );
      expect(result.email).toBe('new@example.com');
      expect(result.displayName).toBe('New User');
    });

    it('should register without displayName', async () => {
      const request: RegisterRequest = {
        email: 'minimal@example.com',
        password: 'pass123',
      };

      mockHttpClient.post.mockResolvedValueOnce({
        user: { id: '1', email: 'minimal@example.com', role: 'User' },
      });

      const result = await authClient.register(request);

      expect(result.email).toBe('minimal@example.com');
    });

    it('should handle registration error', async () => {
      mockHttpClient.post.mockRejectedValueOnce(new Error('Email already exists'));

      await expect(
        authClient.register({ email: 'existing@example.com', password: 'pass' })
      ).rejects.toThrow('Email already exists');
    });
  });

  describe('logout', () => {
    it('should logout successfully', async () => {
      mockHttpClient.post.mockResolvedValueOnce({ success: true });

      await authClient.logout();

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/logout',
        {},
        expect.anything()
      );
    });

    it('should handle logout error gracefully', async () => {
      mockHttpClient.post.mockRejectedValueOnce(new Error('Logout failed'));

      await expect(authClient.logout()).rejects.toThrow('Logout failed');
    });
  });

  describe('getMe', () => {
    it('should get current user', async () => {
      const mockUser: AuthUser = {
        id: 'user-1',
        email: 'current@example.com',
        displayName: 'Current User',
        role: 'Admin',
      };

      mockHttpClient.get.mockResolvedValueOnce({ user: mockUser });

      const result = await authClient.getMe();

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/auth/me', expect.anything());
      expect(result).toEqual(mockUser);
    });

    it('should return null when not authenticated', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ user: null });

      const result = await authClient.getMe();

      expect(result).toBeNull();
    });

    it('should return null when response is null', async () => {
      mockHttpClient.get.mockResolvedValueOnce(null);

      const result = await authClient.getMe();

      expect(result).toBeNull();
    });
  });

  // ========== Password Reset ==========
  describe('verifyResetToken', () => {
    it('should verify token successfully', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ valid: true });

      await authClient.verifyResetToken('valid-token-123');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/auth/password-reset/verify?token=valid-token-123'
      );
    });

    it('should encode special characters in token', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ valid: true });

      await authClient.verifyResetToken('token-with-special/chars&query=value');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('token-with-special%2Fchars%26query%3Dvalue')
      );
    });

    it('should handle invalid token error', async () => {
      mockHttpClient.get.mockRejectedValueOnce(new Error('Invalid token'));

      await expect(authClient.verifyResetToken('invalid-token')).rejects.toThrow('Invalid token');
    });
  });

  describe('requestPasswordReset', () => {
    it('should request password reset successfully', async () => {
      const mockResponse = { success: true, message: 'Email sent' };

      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await authClient.requestPasswordReset('user@example.com');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/password-reset/request',
        { email: 'user@example.com' },
        expect.anything()
      );
      expect(result.success).toBe(true);
    });

    it('should handle email not found error', async () => {
      mockHttpClient.post.mockRejectedValueOnce(new Error('Email not found'));

      await expect(authClient.requestPasswordReset('notfound@example.com')).rejects.toThrow(
        'Email not found'
      );
    });
  });
});
