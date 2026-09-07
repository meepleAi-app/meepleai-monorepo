/**
 * Badges Client Tests — contratto allineato al backend (#3836).
 *
 * Le fixture riproducono la forma REALE delle risposte, derivata dai record C#:
 *   UserBadgeDto        apps/api/.../SharedGameCatalog/Application/DTOs/UserBadgeDto.cs
 *   LeaderboardEntryDto apps/api/.../SharedGameCatalog/Application/DTOs/LeaderboardEntryDto.cs
 *   rotte               apps/api/.../Routing/SharedGameCatalog/SharedGameCatalogBadgeEndpoints.cs
 *
 * La versione precedente di questo file mockava `{ badges: [...] }` e asseriva
 * `/api/v1/badges/my-badges`: entrambi inventati, nessuno dei due esiste nel backend.
 * Gli 11 test passavano bloccando il difetto invece di trovarlo. Quando tocchi queste
 * fixture, aggiornale leggendo i record C#, non lo schema Zod che stai validando.
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

// UUID validi per Zod (i bit di variante devono essere 8/9/a/b)
const MOCK_BADGE_ID_1 = '11111111-1111-4111-a111-111111111111';
const MOCK_BADGE_ID_2 = '22222222-2222-4222-a222-222222222222';
const MOCK_USER_ID_1 = 'aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa';
const MOCK_USER_ID_2 = 'bbbbbbbb-bbbb-4bbb-abbb-bbbbbbbbbbbb';

/** Forma di UserBadgeDto come serializzato dal backend (camelCase, enum come stringa). */
const apiBadge = {
  id: MOCK_BADGE_ID_1,
  code: 'FIRST_CONTRIBUTION',
  name: 'First Contribution',
  description: 'Made your first contribution',
  iconUrl: '/badges/first-contrib.svg',
  tier: 'Bronze',
  earnedAt: '2024-01-15T10:30:00Z',
  isDisplayed: true,
};

/** `IconUrl` è `string?` nel record C#: il caso null deve passare. */
const apiBadgeWithoutIcon = {
  id: MOCK_BADGE_ID_2,
  code: 'EXPERT_CONTRIBUTOR',
  name: 'Expert Contributor',
  description: 'Made 100 contributions',
  iconUrl: null,
  tier: 'Gold',
  earnedAt: '2024-02-20T14:45:00Z',
  isDisplayed: false,
};

const apiLeaderboardEntry = {
  rank: 1,
  userId: MOCK_USER_ID_1,
  userName: 'TopContributor',
  avatarUrl: '/avatars/user1.jpg',
  contributionCount: 250,
  badgeCount: 12,
  highestBadgeTier: 'Diamond',
  topBadges: [apiBadge],
};

const apiLeaderboardEntry2 = {
  rank: 2,
  userId: MOCK_USER_ID_2,
  userName: 'ActiveUser',
  avatarUrl: null,
  contributionCount: 150,
  badgeCount: 3,
  highestBadgeTier: 'Silver',
  topBadges: [],
};

describe('BadgesClient — contratto backend (#3836)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getMyBadges', () => {
    it('chiama la rotta esposta dal backend, /users/me/badges', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([apiBadge]);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.getMyBadges();

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/users/me/badges');
    });

    it('accetta la lista nuda che il backend restituisce', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([apiBadge, apiBadgeWithoutIcon]);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getMyBadges();

      expect(result).toHaveLength(2);
      expect(result[0].code).toBe('FIRST_CONTRIBUTION');
    });

    it('accetta un badge con iconUrl null (IconUrl è string? nel record C#)', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([apiBadgeWithoutIcon]);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getMyBadges();

      expect(result[0].iconUrl).toBeNull();
    });

    it('restituisce una lista vuota quando non ci sono badge', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createBadgesClient({ httpClient: mockHttpClient });

      expect(await client.getMyBadges()).toEqual([]);
    });

    it('propaga gli errori di rete', async () => {
      vi.mocked(mockHttpClient.get).mockRejectedValue(new Error('Network error'));

      const client = createBadgesClient({ httpClient: mockHttpClient });

      await expect(client.getMyBadges()).rejects.toThrow('Network error');
    });
  });

  describe('getLeaderboard', () => {
    it('accetta la lista nuda che il backend restituisce', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([apiLeaderboardEntry, apiLeaderboardEntry2]);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      const result = await client.getLeaderboard('AllTime');

      expect(result).toHaveLength(2);
      expect(result[0].badgeCount).toBe(12);
      expect(result[0].highestBadgeTier).toBe('Diamond');
    });

    it('traduce ThisWeek nel valore Week dell enum backend', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.getLeaderboard('ThisWeek');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/badges/leaderboard?period=Week');
    });

    it('traduce ThisMonth nel valore Month dell enum backend', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.getLeaderboard('ThisMonth');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/badges/leaderboard?period=Month');
    });

    it('lascia AllTime invariato, unico valore comune ai due enum', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.getLeaderboard('AllTime');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/badges/leaderboard?period=AllTime');
    });

    it('restituisce una lista vuota quando la classifica è vuota', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue([]);

      const client = createBadgesClient({ httpClient: mockHttpClient });

      expect(await client.getLeaderboard('AllTime')).toEqual([]);
    });
  });

  describe('toggleBadgeDisplay', () => {
    it('chiama la rotta esposta dal backend, /users/me/badges/{id}/display', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.toggleBadgeDisplay(MOCK_BADGE_ID_1, true);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        `/api/v1/users/me/badges/${MOCK_BADGE_ID_1}/display`,
        { isDisplayed: true }
      );
    });

    it('inoltra isDisplayed false', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(undefined);

      const client = createBadgesClient({ httpClient: mockHttpClient });
      await client.toggleBadgeDisplay(MOCK_BADGE_ID_2, false);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        `/api/v1/users/me/badges/${MOCK_BADGE_ID_2}/display`,
        { isDisplayed: false }
      );
    });

    it('propaga il 404 su badge inesistente', async () => {
      vi.mocked(mockHttpClient.put).mockRejectedValue(new Error('Badge not found'));

      const client = createBadgesClient({ httpClient: mockHttpClient });

      await expect(client.toggleBadgeDisplay(MOCK_BADGE_ID_1, true)).rejects.toThrow(
        'Badge not found'
      );
    });
  });
});
