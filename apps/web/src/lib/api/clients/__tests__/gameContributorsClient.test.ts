/**
 * Game Contributors Client Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Game contributors API client tests
 * - getGameContributors: Get all contributors for a shared game
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createGameContributorsClient } from '../gameContributorsClient';
import type { HttpClient } from '../../core/httpClient';

// Valid UUIDs for Zod validation (variant bits must be 8/9/a/b)
const MOCK_USER_ID_1 = 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa';
const MOCK_USER_ID_2 = 'bbbbbbbb-bbbb-4bbb-abbb-bbbbbbbbbbbb';
const MOCK_USER_ID_3 = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
const MOCK_BADGE_ID_1 = '11111111-1111-4111-a111-111111111111';
const MOCK_BADGE_ID_2 = '22222222-2222-4222-a222-222222222222';
const MOCK_BADGE_ID_3 = '33333333-3333-4333-a333-333333333333';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

const mockContributor = {
  userId: MOCK_USER_ID_1,
  userName: 'JohnDoe',
  avatarUrl: '/avatars/john.jpg',
  isPrimaryContributor: true,
  contributionCount: 15,
  firstContributionAt: '2023-06-15T00:00:00Z',
  topBadges: [
    {
      id: MOCK_BADGE_ID_1,
      name: 'Top Contributor',
      tier: 'Gold' as const,
      iconUrl: '/badges/gold.svg',
    },
  ],
};

describe('GameContributorsClient - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getGameContributors', () => {
    it('should fetch contributors for a game', async () => {
      const mockResponse = { contributors: [mockContributor] };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockResponse);

      const client = createGameContributorsClient({ httpClient: mockHttpClient });
      const result = await client.getGameContributors('game-123');

      expect(result).toEqual([mockContributor]);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/shared-games/game-123/contributors'
      );
    });

    it('should return multiple contributors sorted by contribution count', async () => {
      const contributors = [
        { ...mockContributor, userId: MOCK_USER_ID_1, contributionCount: 50 },
        { ...mockContributor, userId: MOCK_USER_ID_2, contributionCount: 30, isPrimaryContributor: false },
        { ...mockContributor, userId: MOCK_USER_ID_3, contributionCount: 10, isPrimaryContributor: false },
      ];
      vi.mocked(mockHttpClient.get).mockResolvedValue({ contributors });

      const client = createGameContributorsClient({ httpClient: mockHttpClient });
      const result = await client.getGameContributors('game-456');

      expect(result).toHaveLength(3);
      expect(result[0].contributionCount).toBe(50);
    });

    it('should return empty array for game with no contributors', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue({ contributors: [] });

      const client = createGameContributorsClient({ httpClient: mockHttpClient });
      const result = await client.getGameContributors('new-game');

      expect(result).toEqual([]);
    });

    it('should handle contributor with no badges', async () => {
      const contributorNoBadges = {
        ...mockContributor,
        topBadges: [],
        isPrimaryContributor: false,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        contributors: [contributorNoBadges],
      });

      const client = createGameContributorsClient({ httpClient: mockHttpClient });
      const result = await client.getGameContributors('game-789');

      expect(result[0].topBadges).toEqual([]);
    });

    it('should handle contributor with multiple badges', async () => {
      const contributorMultiBadges = {
        ...mockContributor,
        topBadges: [
          { id: MOCK_BADGE_ID_1, name: 'Veteran', tier: 'Platinum' as const, iconUrl: '/badges/veteran.svg' },
          { id: MOCK_BADGE_ID_2, name: 'Helper', tier: 'Silver' as const, iconUrl: '/badges/helper.svg' },
          { id: MOCK_BADGE_ID_3, name: 'First Post', tier: 'Bronze' as const, iconUrl: null },
        ],
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        contributors: [contributorMultiBadges],
      });

      const client = createGameContributorsClient({ httpClient: mockHttpClient });
      const result = await client.getGameContributors('game-abc');

      expect(result[0].topBadges).toHaveLength(3);
    });

    it('should handle API error', async () => {
      vi.mocked(mockHttpClient.get).mockRejectedValue(new Error('Game not found'));

      const client = createGameContributorsClient({ httpClient: mockHttpClient });

      await expect(client.getGameContributors('nonexistent')).rejects.toThrow(
        'Game not found'
      );
    });

    it('should handle network error', async () => {
      vi.mocked(mockHttpClient.get).mockRejectedValue(new Error('Network error'));

      const client = createGameContributorsClient({ httpClient: mockHttpClient });

      await expect(client.getGameContributors('game-123')).rejects.toThrow(
        'Network error'
      );
    });

    it('should handle contributor with null avatar', async () => {
      const contributorNoAvatar = { ...mockContributor, avatarUrl: null };
      vi.mocked(mockHttpClient.get).mockResolvedValue({
        contributors: [contributorNoAvatar],
      });

      const client = createGameContributorsClient({ httpClient: mockHttpClient });
      const result = await client.getGameContributors('game-xyz');

      expect(result[0].avatarUrl).toBeNull();
    });
  });
});
