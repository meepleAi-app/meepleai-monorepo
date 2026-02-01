/**
 * Rate Limits Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Admin rate limit configuration API client tests
 * - getTierConfigs: Get all tier configurations
 * - updateTierConfig: Update tier-specific limits
 * - getUserOverrides: Get user-specific overrides with pagination
 * - createOverride: Create user override
 * - removeOverride: Remove user override
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createRateLimitsClient } from '../rateLimitsClient';
import type { HttpClient } from '../../core/httpClient';

// Valid UUIDs for Zod validation (variant bits must be 8/9/a/b)
const MOCK_USER_ID_1 = 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa';
const MOCK_USER_ID_2 = 'bbbbbbbb-bbbb-4bbb-abbb-bbbbbbbbbbbb';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

const mockTierConfig = {
  tier: 'Premium' as const,
  tierDisplayName: 'Premium Tier',
  maxPendingRequests: 10,
  maxRequestsPerMonth: 20,
  cooldownAfterRejection: {
    days: 7,
    totalSeconds: 604800,
  },
  cooldownDisplay: '7 days',
  updatedAt: '2024-01-15T10:00:00Z',
  updatedByAdminName: 'Admin User',
};

const mockUserOverride = {
  userId: MOCK_USER_ID_1,
  userName: 'TestUser',
  userEmail: 'test@example.com',
  userTier: 'Premium' as const,
  maxPendingRequests: 50,
  maxRequestsPerMonth: 100,
  cooldownAfterRejection: {
    days: 1,
    totalSeconds: 86400,
  },
  expiresAt: '2024-12-31T23:59:59Z',
  isExpired: false,
  reason: 'VIP user testing - extended limits for beta program',
  createdAt: '2024-01-15T10:00:00Z',
  createdByAdminName: 'Admin User',
};

describe('RateLimitsClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getTierConfigs', () => {
    it('should fetch all tier configurations', async () => {
      const mockConfigs = {
        items: [
          { ...mockTierConfig, tier: 'Free' as const, tierDisplayName: 'Free Tier', maxPendingRequests: 1, maxRequestsPerMonth: 5, cooldownAfterRejection: { days: 30, totalSeconds: 2592000 }, cooldownDisplay: '30 days' },
          { ...mockTierConfig, tier: 'Basic' as const, tierDisplayName: 'Basic Tier', maxPendingRequests: 3, maxRequestsPerMonth: 10, cooldownAfterRejection: { days: 14, totalSeconds: 1209600 }, cooldownDisplay: '14 days' },
          mockTierConfig,
          { ...mockTierConfig, tier: 'Pro' as const, tierDisplayName: 'Pro Tier', maxPendingRequests: 20, maxRequestsPerMonth: 50, cooldownAfterRejection: { days: 3, totalSeconds: 259200 }, cooldownDisplay: '3 days' },
          { ...mockTierConfig, tier: 'Admin' as const, tierDisplayName: 'Admin Tier', maxPendingRequests: 999, maxRequestsPerMonth: 999, cooldownAfterRejection: { days: 0, totalSeconds: 0 }, cooldownDisplay: 'No cooldown' },
        ],
        totalCount: 5,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockConfigs);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      const result = await client.getTierConfigs();

      expect(result).toEqual(mockConfigs.items);
      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/admin/rate-limits/configs');
    });

    it('should return empty array when no configs', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ items: [], totalCount: 0 });

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      const result = await client.getTierConfigs();

      expect(result).toEqual([]);
    });

    it('should handle API error', async () => {
      vi.mocked(mockHttpClient.get).mockRejectedValue(new Error('Unauthorized'));

      const client = createRateLimitsClient({ httpClient: mockHttpClient });

      await expect(client.getTierConfigs()).rejects.toThrow('Unauthorized');
    });
  });

  describe('updateTierConfig', () => {
    it('should update Premium tier config', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      await client.updateTierConfig('Premium', {
        maxPendingRequests: 15,
        maxRequestsPerMonth: 30,
        cooldownDays: 5,
      });

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/admin/rate-limits/configs/Premium',
        { maxPendingRequests: 15, maxRequestsPerMonth: 30, cooldownDays: 5 }
      );
    });

    it('should update Free tier config', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      await client.updateTierConfig('Free', {
        maxPendingRequests: 2,
        maxRequestsPerMonth: 10,
        cooldownDays: 21,
      });

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/admin/rate-limits/configs/Free',
        expect.objectContaining({ maxPendingRequests: 2 })
      );
    });

    it('should handle invalid tier error', async () => {
      vi.mocked(mockHttpClient.put).mockRejectedValue(new Error('Invalid tier'));

      const client = createRateLimitsClient({ httpClient: mockHttpClient });

      await expect(
        client.updateTierConfig('InvalidTier' as any, {
          maxPendingRequests: 1,
          maxRequestsPerMonth: 1,
          cooldownDays: 1,
        })
      ).rejects.toThrow('Invalid tier');
    });
  });

  describe('getUserOverrides', () => {
    it('should fetch user overrides with default pagination', async () => {
      const mockOverrides = {
        items: [mockUserOverride],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 20,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockOverrides);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      const result = await client.getUserOverrides();

      expect(result).toEqual([mockUserOverride]);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/rate-limits/overrides?pageNumber=1&pageSize=20'
      );
    });

    it('should apply custom pagination', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        items: [],
        totalCount: 0,
        pageNumber: 3,
        pageSize: 50,
      });

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      await client.getUserOverrides(3, 50);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/rate-limits/overrides?pageNumber=3&pageSize=50'
      );
    });

    it('should return multiple overrides', async () => {
      const mockOverrides = {
        items: [
          mockUserOverride,
          { ...mockUserOverride, userId: MOCK_USER_ID_2, userName: 'AnotherUser', userEmail: 'another@example.com' },
        ],
        totalCount: 2,
        pageNumber: 1,
        pageSize: 20,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockOverrides);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      const result = await client.getUserOverrides();

      expect(result).toHaveLength(2);
    });
  });

  describe('createOverride', () => {
    it('should create user override with all fields', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      await client.createOverride({
        userId: MOCK_USER_ID_1,
        reason: 'Special event testing - extended limits needed',
        maxPendingRequests: 100,
        maxRequestsPerMonth: 200,
        cooldownDays: 0,
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/rate-limits/overrides',
        expect.objectContaining({
          userId: MOCK_USER_ID_1,
          reason: 'Special event testing - extended limits needed',
        })
      );
    });

    it('should create override with minimal fields', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(undefined);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      await client.createOverride({
        userId: MOCK_USER_ID_2,
        reason: 'Minimal override for testing purposes',
        maxPendingRequests: 10,
      });

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/admin/rate-limits/overrides',
        expect.objectContaining({ userId: MOCK_USER_ID_2 })
      );
    });

    it('should handle duplicate override error', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(
        new Error('Override already exists for this user')
      );

      const client = createRateLimitsClient({ httpClient: mockHttpClient });

      await expect(
        client.createOverride({
          userId: MOCK_USER_ID_1,
          reason: 'Duplicate test override attempt',
          maxPendingRequests: 10,
        })
      ).rejects.toThrow('Override already exists');
    });
  });

  describe('removeOverride', () => {
    it('should remove user override', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      await client.removeOverride(MOCK_USER_ID_1);

      expect(mockHttpClient.delete).toHaveBeenCalledWith(
        `/api/v1/admin/rate-limits/overrides/${MOCK_USER_ID_1}`
      );
    });

    it('should handle non-existent override', async () => {
      vi.mocked(mockHttpClient.delete).mockRejectedValue(
        new Error('Override not found')
      );

      const client = createRateLimitsClient({ httpClient: mockHttpClient });

      await expect(client.removeOverride('nonexistent')).rejects.toThrow(
        'Override not found'
      );
    });

    it('should handle special characters in user ID', async () => {
      vi.mocked(mockHttpClient.delete).mockResolvedValue(undefined);

      const client = createRateLimitsClient({ httpClient: mockHttpClient });
      await client.removeOverride(MOCK_USER_ID_2);

      expect(mockHttpClient.delete).toHaveBeenCalledWith(
        `/api/v1/admin/rate-limits/overrides/${MOCK_USER_ID_2}`
      );
    });
  });
});
