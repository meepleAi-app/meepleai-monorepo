/**
 * Share Requests Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: User share requests API client tests
 * - getUserShareRequests: Get paginated list of user's share requests
 * - getShareRequestById: Get a specific share request
 * - createShareRequest: Create a new share request
 * - getRateLimitStatus: Get current rate limit status
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createShareRequestsClient } from '../shareRequestsClient';
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
  gameId: 'game-456',
  gameTitle: 'My Custom Game',
  status: 'Pending',
  contributionType: 'NewGame',
  createdAt: '2024-01-15T10:00:00Z',
  updatedAt: '2024-01-15T10:00:00Z',
  adminFeedback: null,
};

describe('ShareRequestsClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getUserShareRequests', () => {
    it('should fetch user share requests without filters', async () => {
      const mockResponse = {
        items: [mockShareRequest],
        page: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.getUserShareRequests();

      expect(result).toEqual(mockResponse);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/share-requests',
        expect.any(Object)
      );
    });

    it('should apply pagination parameters', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        page: 2,
        pageSize: 20,
        totalCount: 25,
        totalPages: 2,
        hasNextPage: false,
        hasPreviousPage: true,
      });

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      await client.getUserShareRequests({ page: 2, pageSize: 20 });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('page=2'),
        expect.any(Object)
      );
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('pageSize=20'),
        expect.any(Object)
      );
    });

    it('should filter by status', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        page: 1,
        pageSize: 10,
        totalCount: 0,
        totalPages: 0,
        hasNextPage: false,
        hasPreviousPage: false,
      });

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      await client.getUserShareRequests({ status: 'Approved' });

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        expect.stringContaining('status=Approved'),
        expect.any(Object)
      );
    });

    it('should return empty response when API returns null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.getUserShareRequests();

      expect(result.items).toEqual([]);
      expect(result.totalCount).toBe(0);
    });

    it('should handle multiple status types', async () => {
      const pendingRequests = [
        { ...mockShareRequest, status: 'Pending' },
        { ...mockShareRequest, id: 'req-124', status: 'InReview' },
      ];
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: pendingRequests,
        page: 1,
        pageSize: 10,
        totalCount: 2,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      });

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.getUserShareRequests();

      expect(result.items).toHaveLength(2);
    });
  });

  describe('getShareRequestById', () => {
    it('should fetch share request by ID', async () => {
      const detailedRequest = {
        ...mockShareRequest,
        documents: [{ id: 'doc-1', name: 'rulebook.pdf' }],
        history: [{ action: 'Created', timestamp: '2024-01-15T10:00:00Z' }],
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(detailedRequest);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.getShareRequestById('req-123');

      expect(result).toEqual(detailedRequest);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/share-requests/req-123',
        expect.any(Object)
      );
    });

    it('should throw error when request not found', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });

      await expect(client.getShareRequestById('nonexistent')).rejects.toThrow(
        'Share request not found: nonexistent'
      );
    });

    it('should handle request with admin feedback', async () => {
      const requestWithFeedback = {
        ...mockShareRequest,
        status: 'ChangesRequested',
        adminFeedback: 'Please add more detailed description',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(requestWithFeedback);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.getShareRequestById('req-123');

      expect(result.adminFeedback).toBe('Please add more detailed description');
    });
  });

  describe('createShareRequest', () => {
    it('should create a new share request', async () => {
      const mockResponse = {
        id: 'new-req-123',
        message: 'Share request created successfully',
      };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.createShareRequest({
        libraryGameId: 'lib-game-123',
        contributionType: 'NewGame',
        notes: 'This is a great game I want to share',
      });

      expect(result.id).toBe('new-req-123');
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/share-requests',
        expect.objectContaining({
          libraryGameId: 'lib-game-123',
          contributionType: 'NewGame',
        }),
        expect.any(Object)
      );
    });

    it('should create request for additional content', async () => {
      const mockResponse = { id: 'req-456', message: 'Created' };
      vi.mocked(mockHttpClient.post).mockResolvedValue(mockResponse);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      await client.createShareRequest({
        libraryGameId: 'lib-game-789',
        contributionType: 'AdditionalContent',
        targetGameId: 'existing-game-123',
        notes: 'Adding FAQ and errata',
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/share-requests',
        expect.objectContaining({
          contributionType: 'AdditionalContent',
          targetGameId: 'existing-game-123',
        }),
        expect.any(Object)
      );
    });

    it('should throw error when creation fails', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(null);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });

      await expect(
        client.createShareRequest({
          libraryGameId: 'lib-game-123',
          contributionType: 'NewGame',
        })
      ).rejects.toThrow('Failed to create share request');
    });

    it('should handle rate limit exceeded', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(
        new Error('Rate limit exceeded. Maximum 5 requests per month.')
      );

      const client = createShareRequestsClient({ httpClient: mockHttpClient });

      await expect(
        client.createShareRequest({
          libraryGameId: 'lib-game-123',
          contributionType: 'NewGame',
        })
      ).rejects.toThrow('Rate limit exceeded');
    });
  });

  describe('getRateLimitStatus', () => {
    it('should get current rate limit status', async () => {
      const mockStatus = {
        currentCount: 2,
        maxAllowed: 5,
        resetDate: '2024-02-01T00:00:00Z',
        remainingRequests: 3,
        isLimited: false,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockStatus);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.getRateLimitStatus();

      expect(result).toEqual(mockStatus);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/share-requests/rate-limit',
        expect.any(Object)
      );
    });

    it('should handle user at rate limit', async () => {
      const mockStatus = {
        currentCount: 5,
        maxAllowed: 5,
        resetDate: '2024-02-01T00:00:00Z',
        remainingRequests: 0,
        isLimited: true,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockStatus);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.getRateLimitStatus();

      expect(result.isLimited).toBe(true);
      expect(result.remainingRequests).toBe(0);
    });

    it('should handle premium user with higher limits', async () => {
      const mockStatus = {
        currentCount: 8,
        maxAllowed: 20,
        resetDate: '2024-02-01T00:00:00Z',
        remainingRequests: 12,
        isLimited: false,
        userTier: 'Premium',
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockStatus);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });
      const result = await client.getRateLimitStatus();

      expect(result.maxAllowed).toBe(20);
    });

    it('should throw error when status fetch fails', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createShareRequestsClient({ httpClient: mockHttpClient });

      await expect(client.getRateLimitStatus()).rejects.toThrow(
        'Failed to get rate limit status'
      );
    });
  });
});
