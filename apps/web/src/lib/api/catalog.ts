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
  return response.data.games;
}
