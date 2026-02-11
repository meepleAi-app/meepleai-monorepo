/**
 * Catalog API Client - Issue #3921
 *
 * Client for `/api/v1/catalog/trending` endpoint
 *
 * @see Issue #3918 - Catalog Trending Analytics Service (Backend)
 * @see Issue #3921 - Frontend: Catalog Trending Widget
 */

import type { TrendingGame } from '@/components/dashboard/CatalogTrending';

// ============================================================================
// Backend API Response Types
// ============================================================================

interface TrendingGameApiResponse {
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

// ============================================================================
// Mock Data (Temporary - until backend is accessible)
// ============================================================================

const MOCK_TRENDING: TrendingGame[] = [
  { id: 'game-1', name: 'Ark Nova', trend: 25, rank: 1, previousRank: 2, imageUrl: null },
  { id: 'game-2', name: 'Wingspan', trend: 18, rank: 2, previousRank: 3, imageUrl: null },
  { id: 'game-3', name: 'Dune: Imperium', trend: 12, rank: 3, previousRank: 1, imageUrl: null },
  { id: 'game-4', name: 'Earth', trend: 8, rank: 4, previousRank: 6, imageUrl: null },
  { id: 'game-5', name: 'Cascadia', trend: 5, rank: 5, previousRank: 4, imageUrl: null },
];

// ============================================================================
// Configuration
// ============================================================================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

/**
 * Mock data toggle for development
 * Set `NEXT_PUBLIC_USE_MOCK_CATALOG=false` in `.env.local` to use real API
 */
const USE_MOCK_DATA = process.env.NEXT_PUBLIC_USE_MOCK_CATALOG !== 'false';

// ============================================================================
// API Client
// ============================================================================

/**
 * Fetch top trending games from catalog
 *
 * @param limit - Number of games to fetch (default: 5)
 * @returns Promise<TrendingGame[]>
 */
export async function fetchCatalogTrending(limit = 5): Promise<TrendingGame[]> {
  if (USE_MOCK_DATA) {
    await new Promise((resolve) => setTimeout(resolve, 200));
    return MOCK_TRENDING.slice(0, limit);
  }

  const response = await fetch(
    `${API_BASE_URL}/api/v1/catalog/trending?limit=${limit}`,
    {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
    }
  );

  if (!response.ok) {
    throw new Error(`Catalog trending API error: ${response.status} ${response.statusText}`);
  }

  const data: TrendingGameApiResponse[] = await response.json();
  return mapApiResponse(data);
}

// ============================================================================
// Response Mapper
// ============================================================================

function mapApiResponse(data: TrendingGameApiResponse[]): TrendingGame[] {
  return data.map((item) => ({
    id: item.gameId,
    name: item.title,
    trend: Math.round(item.score),
    rank: item.rank,
    previousRank: item.rank,
    imageUrl: item.thumbnailUrl,
  }));
}
