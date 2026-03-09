/**
 * Gaming Hub Dashboard API Client - Issue #4582
 * Epic #4575: Gaming Hub Dashboard - Phase 2
 *
 * TypeScript client for dashboard endpoints with type-safety
 */

import { HttpClient } from './core/httpClient';
import { UserAiUsageDtoSchema, type UserAiUsageDto } from './schemas/ai-usage.schemas';

const httpClient = new HttpClient();

// ============================================================================
// Type Definitions (matching backend DTOs)
// ============================================================================

export interface UserStatsDto {
  totalGames: number;
  monthlyPlays: number;
  monthlyPlaysChange: number; // Percentage: +15 or -10
  weeklyPlayTime: string; // TimeSpan format "HH:MM:SS"
  monthlyFavorites: number;
}

export interface SessionSummaryDto {
  id: string;
  gameName: string;
  gameImageUrl?: string;
  sessionDate: string; // ISO 8601
  playerCount: number;
  duration?: string; // TimeSpan format "HH:MM:SS" or null
  averageScore?: number;
  winnerName?: string;
}

export interface UserGameDto {
  id: string;
  title: string;
  publisher?: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  averageRating?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playingTimeMinutes?: number;
  complexityRating?: number;
  playCount: number;
  lastPlayed?: string; // ISO 8601 or null
  isOwned: boolean;
  inWishlist: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface GetUserGamesParams {
  category?: string;
  sort?: 'alphabetical' | 'lastPlayed' | 'rating' | 'playCount';
  page?: number;
  pageSize?: number;
}

// ============================================================================
// API Client
// ============================================================================

/**
 * Dashboard API client for Gaming Hub
 * Epic #4575, Issues #4578-#4580
 */
export const dashboardClient = {
  /**
   * Get user dashboard statistics
   * Issue #4578
   */
  async getUserStats(): Promise<UserStatsDto> {
    const response = await httpClient.get<UserStatsDto>('/api/v1/users/me/stats');
    if (!response) throw new Error('Failed to fetch user stats');
    return response;
  },

  /**
   * Get recent gaming sessions
   * Issue #4579
   * @param limit Number of sessions to return (default: 3, max: 20)
   */
  async getRecentSessions(limit = 3): Promise<SessionSummaryDto[]> {
    const response = await httpClient.get<SessionSummaryDto[]>(
      `/api/v1/sessions/recent?limit=${limit}`
    );
    if (!response) throw new Error('Failed to fetch recent sessions');
    return response;
  },

  /**
   * Get user's game collection with filters and pagination
   * Issue #4580
   * @param params Filter, sort, and pagination options
   */
  async getUserGames(params: GetUserGamesParams = {}): Promise<PagedResult<UserGameDto>> {
    const queryParams = new URLSearchParams();

    if (params.category && params.category !== 'all') {
      queryParams.append('category', params.category);
    }
    if (params.sort) {
      queryParams.append('sort', params.sort);
    }
    if (params.page) {
      queryParams.append('page', params.page.toString());
    }
    if (params.pageSize) {
      queryParams.append('pageSize', params.pageSize.toString());
    }

    const queryString = queryParams.toString();
    const url = queryString
      ? `/api/v1/users/me/games?${queryString}`
      : '/api/v1/users/me/games';

    const response = await httpClient.get<PagedResult<UserGameDto>>(url);
    if (!response) throw new Error('Failed to fetch user games');
    return response;
  },

  /**
   * Get current user's AI usage statistics
   * Issue #5484: Editor self-service AI usage
   * @param days Lookback period (default: 30)
   */
  async getMyAiUsage(days = 30): Promise<UserAiUsageDto> {
    const response = await httpClient.get<UserAiUsageDto>(
      `/api/v1/users/me/ai-usage?days=${days}`,
      UserAiUsageDtoSchema
    );
    if (!response) throw new Error('Failed to fetch AI usage');
    return response;
  },
};
