/**
 * Email Verification Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Email verification API client tests
 * - verifyEmail: Verify email with token
 * - resendVerificationEmail: Resend verification email
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createEmailVerificationClient } from '../emailVerificationClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

describe('EmailVerificationClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('verifyEmail', () => {
    it('should verify email with valid token', async () => {
      const mockResponse = {
        ok: true,
        message: 'Email verified successfully',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createEmailVerificationClient({ httpClient: mockHttpClient });
      const result = await client.verifyEmail('valid-token-123');

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/email/verify',
        { token: 'valid-token-123' },
        expect.any(Object)
      );
    });

    it('should handle invalid token error', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(
        new Error('Token is invalid or expired')
      );

      const client = createEmailVerificationClient({ httpClient: mockHttpClient });

      await expect(client.verifyEmail('invalid-token')).rejects.toThrow(
        'Token is invalid or expired'
      );
    });

    it('should handle already verified email', async () => {
      const mockResponse = {
        ok: false,
        message: 'Email is already verified',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createEmailVerificationClient({ httpClient: mockHttpClient });
      const result = await client.verifyEmail('used-token');

      expect(result.ok).toBe(false);
      expect(result.message).toContain('already verified');
    });

    it('should handle network error', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(new Error('Network error'));

      const client = createEmailVerificationClient({ httpClient: mockHttpClient });

      await expect(client.verifyEmail('any-token')).rejects.toThrow('Network error');
    });
  });

  describe('resendVerificationEmail', () => {
    it('should resend verification email successfully', async () => {
      const mockResponse = {
        ok: true,
        message: 'Verification email sent',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createEmailVerificationClient({ httpClient: mockHttpClient });
      const result = await client.resendVerificationEmail('user@example.com');

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/email/resend',
        { email: 'user@example.com' },
        expect.any(Object)
      );
    });

    it('should handle rate limit error', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(
        new Error('Rate limited. Please try again in 1 minute.')
      );

      const client = createEmailVerificationClient({ httpClient: mockHttpClient });

      await expect(client.resendVerificationEmail('user@example.com')).rejects.toThrow(
        'Rate limited'
      );
    });

    it('should handle non-existent email', async () => {
      const mockResponse = {
        ok: false,
        message: 'No account found with this email',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createEmailVerificationClient({ httpClient: mockHttpClient });
      const result = await client.resendVerificationEmail('nonexistent@example.com');

      expect(result.ok).toBe(false);
    });

    it('should handle already verified email gracefully', async () => {
      const mockResponse = {
        ok: true,
        message: 'Email is already verified. No action needed.',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createEmailVerificationClient({ httpClient: mockHttpClient });
      const result = await client.resendVerificationEmail('verified@example.com');

      expect(result.ok).toBe(true);
      expect(result.message).toContain('already verified');
    });
  });
});
