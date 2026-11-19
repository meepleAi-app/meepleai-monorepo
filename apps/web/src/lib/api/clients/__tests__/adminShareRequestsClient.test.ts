/**
 * Admin Share Requests Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Admin review interface API client tests
 * - getAll: List pending requests with filters
 * - getById: Get review details
 * - startReview: Acquire exclusive lock
 * - releaseReview: Release lock
 * - getMyReviews: Get active reviews
 * - approve: Approve request
 * - reject: Reject with reason
 * - requestChanges: Request modifications
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createAdminShareRequestsClient } from '../adminShareRequestsClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

const mockShareRequest = {
  id: 'req-123',
  gameTitle: 'Catan',
  contributorName: 'John Doe',
  status: 'Pending',
  contributionType: 'NewGame',
  createdAt: '2024-01-15T10:00:00Z',
  documents: [],
};

describe('AdminShareRequestsClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getAll', () => {
    it('should fetch pending requests without filters', async () => {
      const mockResponse = {
        items: [mockShareRequest],
        page: 1,
        pageSize: 20,
        total: 1,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

      const client = createAdminShareRequestsClient(mockHttpClient);
      const result = await client.getAll();

      expect(result).toEqual(mockResponse);
      // #5076: pageNumber, sortBy and sortDirection are required by the backend: without them it answers 400.
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests?pageNumber=1&pageSize=20&sortBy=CreatedAt&sortDirection=Descending',
        expect.any(Object)
      );
    });

    it('should apply all filters', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        page: 2,
        pageSize: 10,
        total: 0,
      });

      const client = createAdminShareRequestsClient(mockHttpClient);
      await client.getAll({
        page: 2,
        pageSize: 10,
        status: 'InReview',
        contributionType: 'AdditionalContent',
        searchTerm: 'catan',
        sortBy: 'CreatedAt',
        sortDirection: 'Descending',
      });

      // #5076: the names the backend binds (pageNumber, type, search). It ignored page, contributionType and
      // searchTerm, and answered 400 for the missing pageNumber.
      const url = vi.mocked(mockHttpClient.get).mock.calls[0][0] as string;
      const query = Object.fromEntries(new URLSearchParams(url.split('?')[1]));
      expect(query).toEqual({
        pageNumber: '2',
        pageSize: '10',
        sortBy: 'CreatedAt',
        sortDirection: 'Descending',
        status: 'InReview',
        type: 'AdditionalContent',
        search: 'catan',
      });
    });

    it('should return empty response when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminShareRequestsClient(mockHttpClient);
      const result = await client.getAll();

      expect(result.items).toEqual([]);
      expect(result.total).toBe(0);
    });
  });

  describe('getById', () => {
    it('should get request details by ID', async () => {
      const detailedRequest = { ...mockShareRequest, history: [], documents: [] };
      vi.mocked(mockHttpClient.get).mockResolvedValue(detailedRequest);

      const client = createAdminShareRequestsClient(mockHttpClient);
      const result = await client.getById('req-123');

      expect(result).toEqual(detailedRequest);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/req-123',
        expect.any(Object)
      );
    });

    it('should throw error when request not found', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminShareRequestsClient(mockHttpClient);

      await expect(client.getById('nonexistent')).rejects.toThrow(
        'Share request nonexistent not found'
      );
    });
  });

  describe('startReview', () => {
    it('should acquire review lock', async () => {
      const mockResponse = {
        ...mockShareRequest,
        lockExpiresAt: '2024-01-15T11:00:00Z',
        lockedBy: 'admin-user',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createAdminShareRequestsClient(mockHttpClient);
      const result = await client.startReview('req-123');

      expect(result.lockExpiresAt).toBeDefined();
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/req-123/start-review',
        null,
        expect.any(Object)
      );
    });

    it('should throw error when already locked', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(
        new Error('Request is already being reviewed by another admin')
      );

      const client = createAdminShareRequestsClient(mockHttpClient);

      await expect(client.startReview('req-123')).rejects.toThrow('already being reviewed');
    });

    it('should throw error when review fails', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(null);

      const client = createAdminShareRequestsClient(mockHttpClient);

      await expect(client.startReview('req-123')).rejects.toThrow('Failed to start review');
    });
  });

  describe('releaseReview', () => {
    it('should release review lock', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createAdminShareRequestsClient(mockHttpClient);
      await client.releaseReview('req-123');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/req-123/release',
        null
      );
    });
  });

  describe('getMyReviews', () => {
    it('should get active reviews for current admin', async () => {
      const mockReviews = [
        { shareRequestId: 'req-1', gameTitle: 'Game 1', startedAt: '2024-01-15T10:00:00Z' },
        { shareRequestId: 'req-2', gameTitle: 'Game 2', startedAt: '2024-01-15T09:00:00Z' },
      ];
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockReviews);

      const client = createAdminShareRequestsClient(mockHttpClient);
      const result = await client.getMyReviews();

      expect(result).toHaveLength(2);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/my-reviews',
        expect.any(Object)
      );
    });

    it('should return empty array when no active reviews', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createAdminShareRequestsClient(mockHttpClient);
      const result = await client.getMyReviews();

      expect(result).toEqual([]);
    });
  });

  describe('approve', () => {
    it('should approve request without modifications', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createAdminShareRequestsClient(mockHttpClient);
      await client.approve('req-123');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/req-123/approve',
        {}
      );
    });

    it('should approve request with admin notes', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createAdminShareRequestsClient(mockHttpClient);
      await client.approve('req-123', { adminNotes: 'Approved with minor edits' });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/req-123/approve',
        { adminNotes: 'Approved with minor edits' }
      );
    });

    it('should handle conflict when not locked', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(
        new Error('Cannot approve: request not locked by current admin')
      );

      const client = createAdminShareRequestsClient(mockHttpClient);

      await expect(client.approve('req-123')).rejects.toThrow('not locked');
    });
  });

  // #5076: a NewGameProposal and a CoverChange are approved through approve-game-proposal.
  describe('approveGameProposal', () => {
    it('posts the action, the target and the notes, and returns the approval', async () => {
      const approval = {
        shareRequestId: '11111111-1111-4111-8111-111111111111',
        status: 'Approved',
        targetSharedGameId: '22222222-2222-4222-8222-222222222222',
        resolvedAt: '2026-11-19T10:00:00Z',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(approval);

      const client = createAdminShareRequestsClient(mockHttpClient);
      const result = await client.approveGameProposal('req-123', {
        approvalAction: 'ApproveAsVariant',
        targetSharedGameId: '22222222-2222-4222-8222-222222222222',
        adminNotes: 'Second edition',
      });

      expect(result).toEqual(approval);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/req-123/approve-game-proposal',
        {
          approvalAction: 'ApproveAsVariant',
          targetSharedGameId: '22222222-2222-4222-8222-222222222222',
          adminNotes: 'Second edition',
        },
        expect.any(Object)
      );
    });

    it('throws when the response is empty', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(null);

      const client = createAdminShareRequestsClient(mockHttpClient);

      await expect(
        client.approveGameProposal('req-123', { approvalAction: 'ApproveAsNew' })
      ).rejects.toThrow('Failed to approve the proposal');
    });
  });

  describe('reject', () => {
    it('should reject request with reason', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createAdminShareRequestsClient(mockHttpClient);
      await client.reject('req-123', {
        reason: 'Content violates community guidelines',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/req-123/reject',
        { reason: 'Content violates community guidelines' }
      );
    });
  });

  describe('requestChanges', () => {
    it('should request changes with feedback', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createAdminShareRequestsClient(mockHttpClient);
      await client.requestChanges('req-123', {
        feedback: 'Please add more detailed description and fix image quality',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/share-requests/req-123/request-changes',
        { feedback: 'Please add more detailed description and fix image quality' }
      );
    });
  });
});
