/**
 * useCarouselGames - TanStack Query hooks for GameCarousel component
 *
 * Issue #3586: GC-001 — Game Carousel API Integration
 * Epic: #3585 — GameCarousel Integration & Production Readiness
 *
 * Provides data fetching with automatic caching, loading states,
 * and error handling for the GameCarousel component.
 *
 * Features:
 * - Multiple data sources: featured, trending, category, user-library
 * - Automatic data transformation to CarouselGame format
 * - Loading states with skeleton support
 * - Error handling with automatic retry
 * - React Query caching (stale-while-revalidate)
 */

import { useQuery, UseQueryResult } from '@tanstack/react-query';
import { Clock, Users } from 'lucide-react';

import { api } from '@/lib/api';
import type {
  PagedSharedGames,
  SharedGame,
  SearchSharedGamesParams,
} from '@/lib/api/schemas/shared-games.schemas';
import type { PaginatedLibraryResponse, UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import type { CarouselGame, CarouselSortValue } from '@/components/ui/data-display/game-carousel';

// ============================================================================
// Types & Interfaces
// ============================================================================

/**
 * Carousel data source types
 */
export type CarouselSource = 'featured' | 'trending' | 'category' | 'user-library';

/**
 * Options for useCarouselGames hook
 */
export interface UseCarouselGamesOptions {
  /** Data source for the carousel */
  source: CarouselSource;
  /** Category ID for 'category' source */
  categoryId?: string;
  /** Maximum number of games to display (default: 10) */
  limit?: number;
  /** Sort order for games (default depends on source) */
  sort?: CarouselSortValue;
}

/**
 * Result from the carousel games hook with additional metadata
 */
export interface CarouselGamesResult {
  /** Transformed games ready for carousel display */
  games: CarouselGame[];
  /** Total count of available games */
  total: number;
  /** Whether there are more games to load */
  hasMore: boolean;
}

// ============================================================================
// Query Key Factory
// ============================================================================

/**
 * Query key factory for carousel queries
 */
export const carouselGamesKeys = {
  all: ['carouselGames'] as const,
  lists: () => [...carouselGamesKeys.all, 'list'] as const,
  list: (options: UseCarouselGamesOptions) =>
    [...carouselGamesKeys.lists(), { options }] as const,
  source: (source: CarouselSource) =>
    [...carouselGamesKeys.all, 'source', source] as const,
};

// ============================================================================
// Transformer Functions
// ============================================================================

/**
 * Transform SharedGame from API to CarouselGame format
 */
function transformSharedGameToCarousel(
  game: SharedGame,
  badge?: string
): CarouselGame {
  return {
    id: game.id,
    title: game.title,
    subtitle: undefined, // SharedGame doesn't include publisher in list response
    imageUrl: game.imageUrl || game.thumbnailUrl,
    rating: game.averageRating ?? undefined,
    ratingMax: 10,
    metadata: [
      {
        icon: Users,
        value: game.minPlayers === game.maxPlayers
          ? `${game.minPlayers}`
          : `${game.minPlayers}-${game.maxPlayers}`,
      },
      {
        icon: Clock,
        value: `${game.playingTimeMinutes} min`,
      },
    ],
    badge,
  };
}

/**
 * Transform UserLibraryEntry to CarouselGame format
 */
function transformLibraryEntryToCarousel(entry: UserLibraryEntry): CarouselGame {
  return {
    id: entry.gameId,
    title: entry.gameTitle,
    subtitle: entry.gamePublisher ?? undefined,
    imageUrl: entry.gameImageUrl ?? entry.gameIconUrl ?? undefined,
    rating: undefined, // Library entries don't include ratings
    ratingMax: 10,
    metadata: entry.gameYearPublished
      ? [
          {
            icon: Clock,
            value: `${entry.gameYearPublished}`,
          },
        ]
      : undefined,
    badge: entry.isFavorite ? '❤️ Favorite' : undefined,
  };
}

/**
 * Assign badge based on game rank/position in featured list
 */
function getBadgeForPosition(index: number, source: CarouselSource): string | undefined {
  if (source === 'featured') {
    if (index === 0) return 'Top Rated';
    if (index < 3) return undefined;
  }
  if (source === 'trending') {
    if (index === 0) return 'Trending #1';
    if (index < 3) return 'Hot';
  }
  return undefined;
}

// ============================================================================
// API Fetchers
// ============================================================================

/**
 * Map CarouselSortValue to API sortBy field
 */
function mapSortToApiField(sort: CarouselSortValue): { sortBy: string; sortDescending: boolean } {
  switch (sort) {
    case 'rating':
      return { sortBy: 'averageRating', sortDescending: true };
    case 'popularity':
      return { sortBy: 'modifiedAt', sortDescending: true }; // Proxy for popularity
    case 'name':
      return { sortBy: 'title', sortDescending: false };
    case 'date':
      return { sortBy: 'createdAt', sortDescending: true };
    default:
      return { sortBy: 'averageRating', sortDescending: true };
  }
}

/**
 * Fetch featured games (default: sorted by rating)
 */
async function fetchFeaturedGames(
  limit: number,
  sort: CarouselSortValue = 'rating'
): Promise<CarouselGamesResult> {
  const { sortBy, sortDescending } = mapSortToApiField(sort);
  const params: SearchSharedGamesParams = {
    sortBy,
    sortDescending,
    pageSize: limit,
    page: 1,
    status: 2, // Published only
  };

  const result: PagedSharedGames = await api.sharedGames.search(params);

  return {
    games: result.items.map((game, index) =>
      transformSharedGameToCarousel(game, getBadgeForPosition(index, 'featured'))
    ),
    total: result.total,
    hasMore: result.total > limit,
  };
}

/**
 * Fetch trending games (default: sorted by popularity)
 */
async function fetchTrendingGames(
  limit: number,
  sort: CarouselSortValue = 'popularity'
): Promise<CarouselGamesResult> {
  const { sortBy, sortDescending } = mapSortToApiField(sort);
  const params: SearchSharedGamesParams = {
    sortBy,
    sortDescending,
    pageSize: limit,
    page: 1,
    status: 2, // Published only
  };

  const result: PagedSharedGames = await api.sharedGames.search(params);

  return {
    games: result.items.map((game, index) =>
      transformSharedGameToCarousel(game, getBadgeForPosition(index, 'trending'))
    ),
    total: result.total,
    hasMore: result.total > limit,
  };
}

/**
 * Fetch games by category
 */
async function fetchCategoryGames(
  categoryId: string,
  limit: number,
  sort: CarouselSortValue = 'rating'
): Promise<CarouselGamesResult> {
  const { sortBy, sortDescending } = mapSortToApiField(sort);
  const params: SearchSharedGamesParams = {
    categoryIds: categoryId,
    sortBy,
    sortDescending,
    pageSize: limit,
    page: 1,
    status: 2, // Published only
  };

  const result: PagedSharedGames = await api.sharedGames.search(params);

  return {
    games: result.items.map(game => transformSharedGameToCarousel(game)),
    total: result.total,
    hasMore: result.total > limit,
  };
}

/**
 * Fetch games from user's library (authenticated)
 */
async function fetchUserLibraryGames(limit: number): Promise<CarouselGamesResult> {
  const result: PaginatedLibraryResponse = await api.library.getLibrary({
    pageSize: limit,
    page: 1,
  });

  return {
    games: result.items.map(entry => transformLibraryEntryToCarousel(entry)),
    total: result.totalCount,
    hasMore: result.totalCount > limit,
  };
}

// ============================================================================
// Main Hook
// ============================================================================

/**
 * Hook to fetch games for the GameCarousel component
 *
 * Supports multiple data sources with automatic caching and error handling.
 * Transforms API responses to the CarouselGame format expected by the component.
 *
 * @param options - Configuration for the carousel data source
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with CarouselGamesResult
 *
 * @example
 * ```tsx
 * // Featured games
 * const { data, isLoading, error } = useCarouselGames({
 *   source: 'featured',
 *   limit: 10,
 * });
 *
 * // Category games
 * const { data } = useCarouselGames({
 *   source: 'category',
 *   categoryId: 'strategy-uuid',
 *   limit: 10,
 * });
 *
 * // User library (requires authentication)
 * const { data } = useCarouselGames({
 *   source: 'user-library',
 *   limit: 10,
 * });
 * ```
 */
export function useCarouselGames(
  options: UseCarouselGamesOptions,
  enabled: boolean = true
): UseQueryResult<CarouselGamesResult, Error> {
  const { source, categoryId, limit = 10, sort } = options;

  return useQuery({
    queryKey: carouselGamesKeys.list(options),
    queryFn: async (): Promise<CarouselGamesResult> => {
      switch (source) {
        case 'featured':
          return fetchFeaturedGames(limit, sort ?? 'rating');
        case 'trending':
          return fetchTrendingGames(limit, sort ?? 'popularity');
        case 'category':
          if (!categoryId) {
            throw new Error('categoryId is required for category source');
          }
          return fetchCategoryGames(categoryId, limit, sort ?? 'rating');
        case 'user-library':
          return fetchUserLibraryGames(limit);
        default:
          throw new Error(`Unknown carousel source: ${source}`);
      }
    },
    enabled: enabled && (source !== 'category' || !!categoryId),
    // Carousel data is displayed prominently, cache for 5 minutes
    staleTime: 5 * 60 * 1000,
    // Retry up to 3 times on error
    retry: 3,
    retryDelay: attemptIndex => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}

// ============================================================================
// Convenience Hooks
// ============================================================================

/**
 * Hook to fetch featured games for carousel
 *
 * @param limit - Maximum number of games (default: 10)
 * @param enabled - Whether to run the query (default: true)
 */
export function useFeaturedGames(
  limit: number = 10,
  enabled: boolean = true
): UseQueryResult<CarouselGamesResult, Error> {
  return useCarouselGames({ source: 'featured', limit }, enabled);
}

/**
 * Hook to fetch trending games for carousel
 *
 * @param limit - Maximum number of games (default: 10)
 * @param enabled - Whether to run the query (default: true)
 */
export function useTrendingGames(
  limit: number = 10,
  enabled: boolean = true
): UseQueryResult<CarouselGamesResult, Error> {
  return useCarouselGames({ source: 'trending', limit }, enabled);
}

/**
 * Hook to fetch games by category for carousel
 *
 * @param categoryId - Category UUID
 * @param limit - Maximum number of games (default: 10)
 * @param enabled - Whether to run the query (default: true)
 */
export function useCategoryGames(
  categoryId: string,
  limit: number = 10,
  enabled: boolean = true
): UseQueryResult<CarouselGamesResult, Error> {
  return useCarouselGames({ source: 'category', categoryId, limit }, enabled);
}

/**
 * Hook to fetch user's library games for carousel (authenticated)
 *
 * @param limit - Maximum number of games (default: 10)
 * @param enabled - Whether to run the query (default: true)
 */
export function useUserLibraryGames(
  limit: number = 10,
  enabled: boolean = true
): UseQueryResult<CarouselGamesResult, Error> {
  return useCarouselGames({ source: 'user-library', limit }, enabled);
}
