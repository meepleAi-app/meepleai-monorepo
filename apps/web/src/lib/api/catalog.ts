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
}

export async function getTrendingGames(limit = 10): Promise<TrendingGame[]> {
  const response = await apiClient.get<TrendingGame[]>(`/catalog/trending?limit=${limit}`);
  return response ?? [];
}

export async function fetchCatalogTrending(limit = 5): Promise<TrendingGame[]> {
  return getTrendingGames(limit);
}
