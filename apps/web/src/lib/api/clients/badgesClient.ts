/**
 * Badges API Client (Issue #2747)
 *
 * API client for badge system and gamification features.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

import { type HttpClient } from '../core/httpClient';
import {
  MyBadgesResponseSchema,
  LeaderboardResponseSchema,
  ToggleBadgeDisplayRequestSchema,
  type UserBadgeDto,
  type LeaderboardEntryDto,
  type LeaderboardPeriod,
} from '../schemas/badges.schemas';

export interface CreateBadgesClientParams {
  httpClient: HttpClient;
}

/**
 * I valori del filtro UI non coincidono con l'enum `LeaderboardPeriod` del backend
 * (`Week` | `Month` | `AllTime`): `ThisWeek`/`ThisMonth` non fanno binding e la richiesta
 * viene rifiutata. La traduzione sta qui, al confine con l'API, per non toccare i valori
 * che LeaderboardTable usa nei suoi controlli (#3836).
 */
const PERIOD_TO_BACKEND: Record<LeaderboardPeriod, string> = {
  ThisWeek: 'Week',
  ThisMonth: 'Month',
  AllTime: 'AllTime',
};

/**
 * Badges API client with Zod validation
 *
 * @example
 * ```typescript
 * const client = createBadgesClient({ httpClient });
 *
 * // Get my badges
 * const badges = await client.getMyBadges();
 *
 * // Get leaderboard
 * const leaderboard = await client.getLeaderboard('AllTime');
 *
 * // Toggle badge visibility
 * await client.toggleBadgeDisplay('badge-uuid', true);
 * ```
 */
export function createBadgesClient({ httpClient }: CreateBadgesClientParams) {
  return {
    /**
     * Get all badges earned by current user
     *
     * @returns Array of user badges
     */
    async getMyBadges(): Promise<UserBadgeDto[]> {
      const response = await httpClient.get('/api/v1/users/me/badges');

      return MyBadgesResponseSchema.parse(response);
    },

    /**
     * Get leaderboard for specified period
     *
     * @param period - Time period filter (ThisWeek, ThisMonth, AllTime)
     * @returns Leaderboard entries sorted by rank
     */
    async getLeaderboard(period: LeaderboardPeriod): Promise<LeaderboardEntryDto[]> {
      const response = await httpClient.get(
        `/api/v1/badges/leaderboard?period=${PERIOD_TO_BACKEND[period]}`
      );

      return LeaderboardResponseSchema.parse(response);
    },

    /**
     * Toggle badge visibility on user profile
     *
     * @param badgeId - Badge UUID
     * @param isDisplayed - Whether to display badge
     */
    async toggleBadgeDisplay(badgeId: string, isDisplayed: boolean): Promise<void> {
      const request = ToggleBadgeDisplayRequestSchema.parse({ isDisplayed });

      await httpClient.put(`/api/v1/users/me/badges/${badgeId}/display`, request);
    },
  };
}

export type BadgesClient = ReturnType<typeof createBadgesClient>;
