import { apiClient } from "./client";

export interface TrendingGame {
  gameId: string;
  title: string;
  trendScore: number;
  percentageChange: number | null;
  imageUrl?: string;
}

export async function getTrendingGames(period: "week" | "month" = "week"): Promise<TrendingGame[]> {
  const response = await apiClient.get<{ games: TrendingGame[] }>(`/catalog/trending?period=${period}`);
  if (!response) return [];
  return response.games;
}

export async function fetchCatalogTrending(limit = 5): Promise<TrendingGame[]> {
  const games = await getTrendingGames('week');
  return games.slice(0, limit);
}
