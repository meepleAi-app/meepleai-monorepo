/**
 * Invitations Client Tests - Issue #132
 *
 * Coverage: All invitationsClient methods with mocked httpClient
 * - sendInvitation (admin)
 * - bulkSendInvitations (admin, raw fetch)
 * - resendInvitation (admin)
 * - getInvitations (admin)
 * - getInvitationStats (admin)
 * - validateInvitationToken (public)
 * - acceptInvitation (public)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createInvitationsClient } from '../invitationsClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

describe('InvitationsClient - Issue #132', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal('fetch', vi.fn());
  });

  describe('sendInvitation', () => {
    it('should send a single invitation', async () => {
      const mockResponse = {
        id: '00000000-0000-0000-0000-000000000001',
        email: 'test@example.com',
        role: 'Player',
        status: 'Pending',
        expiresAt: '2026-03-18T00:00:00Z',
        createdAt: '2026-03-11T00:00:00Z',
        acceptedAt: null,
        invitedByUserId: '00000000-0000-0000-0000-000000000099',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.sendInvitation('test@example.com', 'Player');

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/users/invite',
        { email: 'test@example.com', role: 'Player' },
        expect.any(Object)
      );
    });

    it('should propagate errors from httpClient', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(
        new Error('Conflict: email already invited')
      );

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      await expect(client.sendInvitation('dup@example.com', 'Player')).rejects.toThrow(
        'Conflict: email already invited'
      );
    });
  });

  describe('bulkSendInvitations', () => {
    it('should send CSV via multipart form-data and parse response', async () => {
      const bulkResponse = {
        successful: [
          {
            id: 'a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d',
            email: 'a@test.com',
            role: 'Player',
            status: 'Pending',
            expiresAt: '2026-03-18T00:00:00Z',
            createdAt: '2026-03-11T00:00:00Z',
            acceptedAt: null,
            invitedByUserId: 'f1e2d3c4-b5a6-4978-8a6b-5c4d3e2f1a0b',
          },
        ],
        failed: [{ email: 'bad', error: 'Invalid email' }],
      };

      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(bulkResponse),
      } as Response);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.bulkSendInvitations('email,role\na@test.com,Player\nbad,Player');

      expect(result.successful).toHaveLength(1);
      expect(result.failed).toHaveLength(1);
      expect(result.failed[0].email).toBe('bad');
    });

    it('should throw on non-ok response with detail message', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: false,
        status: 400,
        json: () => Promise.resolve({ detail: 'CSV is empty' }),
      } as unknown as Response);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      await expect(client.bulkSendInvitations('')).rejects.toThrow('CSV is empty');
    });
  });

  describe('resendInvitation', () => {
    it('should call resend endpoint with invitation id', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      await client.resendInvitation('inv-uuid-123');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/users/invitations/inv-uuid-123/resend'
      );
    });
  });

  describe('getInvitations', () => {
    it('should fetch invitations with query params', async () => {
      const mockResponse = {
        items: [],
        totalCount: 0,
        page: 2,
        pageSize: 10,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.getInvitations({ page: 2, pageSize: 10, status: 'Pending' });

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('page=2'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('status=Pending'),
        expect.any(Object)
      );
    });

    it('should return default response when httpClient returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.getInvitations();

      expect(result).toEqual({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    });
  });

  describe('getInvitationStats', () => {
    it('should fetch invitation stats', async () => {
      const mockStats = { pending: 5, accepted: 10, expired: 2, total: 17 };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockStats);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.getInvitationStats();

      expect(result).toEqual(mockStats);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/users/invitations/stats',
        expect.any(Object)
      );
    });

    it('should return default stats when httpClient returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.getInvitationStats();

      expect(result).toEqual({ pending: 0, accepted: 0, expired: 0, total: 0 });
    });
  });

  describe('validateInvitationToken', () => {
    it('should validate a valid token', async () => {
      const mockValidation = {
        valid: true,
        role: 'Player',
        expiresAt: '2026-03-18T00:00:00Z',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockValidation);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.validateInvitationToken('abc-token-123');

      expect(result.valid).toBe(true);
      expect(result.role).toBe('Player');
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/validate-invitation',
        { token: 'abc-token-123' },
        expect.any(Object)
      );
    });

    it('should return invalid for expired token', async () => {
      const mockValidation = {
        valid: false,
        role: null,
        expiresAt: null,
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockValidation);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.validateInvitationToken('expired-token');

      expect(result.valid).toBe(false);
    });
  });

  describe('acceptInvitation', () => {
    it('should accept invitation and return user + session', async () => {
      const mockResponse = {
        user: {
          id: '00000000-0000-0000-0000-000000000001',
          email: 'new@example.com',
          displayName: null,
          role: 'Player',
          tier: null,
          createdAt: '2026-03-11T00:00:00Z',
          isTwoFactorEnabled: false,
          twoFactorEnabledAt: null,
          level: 1,
          experiencePoints: 0,
          emailVerified: true,
          emailVerifiedAt: '2026-03-11T00:00:00Z',
          verificationGracePeriodEndsAt: null,
        },
        sessionToken: 'jwt-session-token-xyz',
        expiresAt: '2026-03-12T00:00:00Z',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      const result = await client.acceptInvitation('abc-token', 'P@ssw0rd!', 'P@ssw0rd!');

      expect(result.user.email).toBe('new@example.com');
      expect(result.sessionToken).toBe('jwt-session-token-xyz');
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/auth/accept-invitation',
        { token: 'abc-token', password: 'P@ssw0rd!', confirmPassword: 'P@ssw0rd!' },
        expect.any(Object)
      );
    });

    it('should propagate error on invalid token', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(new Error('Invitation not found'));

      const client = createInvitationsClient({ httpClient: mockHttpClient });
      await expect(client.acceptInvitation('bad-token', 'P@ss', 'P@ss')).rejects.toThrow(
        'Invitation not found'
      );
    });
  });
});
