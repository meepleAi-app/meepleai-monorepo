/**
 * Badges Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Badge system API client tests
 * - getMyBadges: Fetch user's earned badges
 * - getLeaderboard: Fetch period-based leaderboard
 * - toggleBadgeDisplay: Toggle badge visibility on profile
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createBadgesClient } from '../badgesClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

// Valid UUIDs for Zod validation (variant bits must be 8/9/a/b)
const MOCK_BADGE_ID_1 = '11111111-1111-4111-a111-111111111111';
const MOCK_BADGE_ID_2 = '22222222-2222-4222-a222-222222222222';
const MOCK_USER_ID_1 = 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa';
const MOCK_USER_ID_2 = 'bbbbbbbb-bbbb-4bbb-abbb-bbbbbbbbbbbb';

describe('BadgesClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getMyBadges', () => {
    it('should fetch user badges successfully', async () => {
      const mockBadges = {
        badges: [
          {
            id: MOCK_BADGE_ID_1,
            name: 'First Contribution',
            description: 'Made your first contribution',
            tier: 'Bronze',
            iconUrl: '/badges/first-contrib.svg',
            earnedAt: '2024-01-15T10:30:00Z',
            isDisplayed: true,
            category: 'contribution',
          },
          {
            id: MOCK_BADGE_ID_2,
            name: 'Expert Contributor',
            description: 'Made 100 contributions',
            tier: 'Gold',
            iconUrl: '/badges/expert.svg',
            earnedAt: '2024-02-20T14:45:00Z',
            isDisplayed: false,
            category: 'contribution',
          },
        ],
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockBadges);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getMyBadges();

      expect(result).toEqual(mockBadges.badges);
      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/badges/my-badges');
    });

    it('should return empty array when no badges', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ badges: [] });

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getMyBadges();

      expect(result).toEqual([]);
    });

    it('should handle API error', async () => {
      vi.mocked(mockHttpClient.get).mockRejectedValue(new Error('Network error'));

      const client = createBadgesClient({ httpClient: mockHttpClient });

      await expect(client.getMyBadges()).rejects.toThrow('Network error');
    });
  });

  describe('getLeaderboard', () => {
    const mockLeaderboard = {
      period: 'AllTime' as const,
      items: [
        {
          userId: MOCK_USER_ID_1,
          userName: 'TopContributor',
          avatarUrl: '/avatars/user1.jpg',
          contributionCount: 250,
          topBadges: [
            {
              id: MOCK_BADGE_ID_1,
              name: 'Diamond Contributor',
              description: '500+ contributions',
              tier: 'Diamond' as const,
              iconUrl: '/badges/diamond.svg',
              earnedAt: '2024-01-01T00:00:00Z',
              isDisplayed: true,
            },
          ],
          rank: 1,
        },
        {
          userId: MOCK_USER_ID_2,
          userName: 'ActiveUser',
          avatarUrl: null,
          contributionCount: 150,
          topBadges: [],
          rank: 2,
        },
      ],
    };

    it('should fetch AllTime leaderboard', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockLeaderboard);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getLeaderboard('AllTime');

      expect(result).toEqual(mockLeaderboard.items);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/badges/leaderboard?period=AllTime'
      );
    });

    it('should fetch ThisWeek leaderboard', async () => {
      const weeklyLeaderboard = { ...mockLeaderboard, period: 'ThisWeek' as const };
      vi.mocked(mockHttpClient.get).mockResolvedValue(weeklyLeaderboard);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getLeaderboard('ThisWeek');

      expect(result).toEqual(weeklyLeaderboard.items);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/badges/leaderboard?period=ThisWeek'
      );
    });

    it('should fetch ThisMonth leaderboard', async () => {
      const monthlyLeaderboard = { ...mockLeaderboard, period: 'ThisMonth' as const };
      vi.mocked(mockHttpClient.get).mockResolvedValue(monthlyLeaderboard);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getLeaderboard('ThisMonth');

      expect(result).toEqual(monthlyLeaderboard.items);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/badges/leaderboard?period=ThisMonth'
      );
    });

    it('should return empty array when no leaderboard entries', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ period: 'AllTime' as const, items: [] });

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getLeaderboard('AllTime');

      expect(result).toEqual([]);
    });
  });

  describe('toggleBadgeDisplay', () => {
    it('should enable badge display', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.toggleBadgeDisplay('badge-123', true);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/badges/badge-123/display',
        { isDisplayed: true }
      );
    });

    it('should disable badge display', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.toggleBadgeDisplay('badge-456', false);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/badges/badge-456/display',
        { isDisplayed: false }
      );
    });

    it('should handle invalid badge ID gracefully', async () => {
      vi.mocked(mockHttpClient.put).mockRejectedValue(new Error('Badge not found'));

      const client = createBadgesClient({ httpClient: mockHttpClient });

      await expect(client.toggleBadgeDisplay('invalid-id', true)).rejects.toThrow(
        'Badge not found'
      );
    });

    it('should encode special characters in badge ID', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.toggleBadgeDisplay('badge/special', true);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/badges/badge/special/display',
        { isDisplayed: true }
      );
    });
  });
});
