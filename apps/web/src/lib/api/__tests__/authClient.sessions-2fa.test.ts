/**
 * Auth Client Tests - Session Management & 2FA (FE-IMP-005)
 *
 * Tests for session management and two-factor authentication features.
 * Split from authClient.test.ts for better organization.
 */

import { createAuthClient } from '../clients/authClient';
import { HttpClient } from '../core/httpClient';
import type { SessionStatusResponse, TotpSetupResponse, TwoFactorStatusDto } from '../schemas';
import { globalRequestCache } from '../core/requestCache';

describe('AuthClient - Sessions & 2FA', () => {
  let mockFetch: Mock;
  let httpClient: HttpClient;
  let authClient: ReturnType<typeof createAuthClient>;

  beforeEach(() => {
    // Clear request cache before each test
    globalRequestCache.clear();

    mockFetch = vi.fn();
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

        await expect(authClient.getTwoFactorStatus()).rejects.toThrow('Failed to get 2FA status');
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
          json: async () => ({
            Success: true,
            ErrorMessage: null,
          }),
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
});
