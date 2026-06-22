import { apiClient } from './client';

export interface TrendingGame {
  rank: number;
  gameId: string;
  title: string;
  thumbnailUrl: string | null;
  score: number;
  searchCount: number;
  viewCount: number;
  libraryAddCount: number;
  playCount: number;
  /**
   * Issue #2290 — mirrors `SharedGameDto.hasKnowledgeBase`. True when the
   * game has at least one indexed KB. Drives the "KB" / AI-Ready badge on
   * the Discover Row 1 card (Block B of #2289).
   */
  hasKnowledgeBase: boolean;
}

export async function getTrendingGames(limit = 10): Promise<TrendingGame[]> {
  const response = await apiClient.get<TrendingGame[]>(`/api/v1/catalog/trending?limit=${limit}`);
  return response ?? [];
}

export async function fetchCatalogTrending(limit = 5): Promise<TrendingGame[]> {
  return getTrendingGames(limit);
}
