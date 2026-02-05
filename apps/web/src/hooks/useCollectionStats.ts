/**
 * useCollectionStats Hook - Issue #3476
 *
 * Custom hook for fetching collection statistics and games
 * Currently uses mock data (TODO: integrate with real API)
 */

'use client';

import { useState, useEffect } from 'react';

import type { ActivityEvent } from '@/components/dashboard/ActivityFeed';
import type {
  CollectionStats,
  CollectionHeroStats,
  CollectionGame,
  CollectionQueryParams,
  SortOption,
  ActiveFilters,
} from '@/types/collection';

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_GAMES: CollectionGame[] = [
  {
    id: 'game-1',
    bggId: 266192,
    title: 'Wingspan',
    imageUrl: '/images/games/wingspan.jpg',
    thumbnailUrl: '/images/games/wingspan-thumb.jpg',
    yearPublished: 2019,
    minPlayers: 1,
    maxPlayers: 5,
    playingTime: 70,
    complexity: 2.4,
    rating: 8.1,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 30).toISOString(), // 30 days ago
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(), // 2 days ago
    playCount: 12,
    hasPdf: true,
    hasActiveChat: true,
    chatCount: 3,
    category: 'Strategy',
    tags: ['birds', 'engine-building'],
    notes: 'Great for relaxing game nights',
  },
  {
    id: 'game-2',
    bggId: 13,
    title: 'Catan',
    imageUrl: '/images/games/catan.jpg',
    thumbnailUrl: '/images/games/catan-thumb.jpg',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    playingTime: 120,
    complexity: 2.3,
    rating: 7.2,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 90).toISOString(), // 90 days ago
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7).toISOString(), // 7 days ago
    playCount: 25,
    hasPdf: false,
    hasActiveChat: true,
    chatCount: 5,
    category: 'Family',
    tags: ['trading', 'resource-management'],
  },
  {
    id: 'game-3',
    bggId: 174430,
    title: 'Gloomhaven',
    imageUrl: '/images/games/gloomhaven.jpg',
    thumbnailUrl: '/images/games/gloomhaven-thumb.jpg',
    yearPublished: 2017,
    minPlayers: 1,
    maxPlayers: 4,
    playingTime: 120,
    complexity: 3.9,
    rating: 8.9,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 60).toISOString(), // 60 days ago
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 14).toISOString(), // 14 days ago
    playCount: 8,
    hasPdf: true,
    hasActiveChat: false,
    chatCount: 0,
    category: 'Strategy',
    tags: ['dungeon-crawler', 'campaign'],
    notes: 'Long campaign, worth every minute',
  },
  {
    id: 'game-4',
    bggId: 167791,
    title: 'Terraforming Mars',
    imageUrl: '/images/games/terraforming-mars.jpg',
    thumbnailUrl: '/images/games/terraforming-mars-thumb.jpg',
    yearPublished: 2016,
    minPlayers: 1,
    maxPlayers: 5,
    playingTime: 120,
    complexity: 3.2,
    rating: 8.4,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 45).toISOString(), // 45 days ago
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 10).toISOString(), // 10 days ago
    playCount: 15,
    hasPdf: true,
    hasActiveChat: true,
    chatCount: 2,
    category: 'Strategy',
    tags: ['space', 'engine-building'],
  },
  {
    id: 'game-5',
    bggId: 230802,
    title: 'Azul',
    imageUrl: '/images/games/azul.jpg',
    thumbnailUrl: '/images/games/azul-thumb.jpg',
    yearPublished: 2017,
    minPlayers: 2,
    maxPlayers: 4,
    playingTime: 45,
    complexity: 1.8,
    rating: 7.9,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 20).toISOString(), // 20 days ago
    lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 1).toISOString(), // 1 day ago
    playCount: 18,
    hasPdf: false,
    hasActiveChat: false,
    chatCount: 0,
    category: 'Family',
    tags: ['abstract', 'pattern-building'],
  },
  {
    id: 'game-6',
    bggId: 220308,
    title: 'Gaia Project',
    imageUrl: '/images/games/gaia-project.jpg',
    thumbnailUrl: '/images/games/gaia-project-thumb.jpg',
    yearPublished: 2017,
    minPlayers: 1,
    maxPlayers: 4,
    playingTime: 150,
    complexity: 4.4,
    rating: 8.6,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 75).toISOString(), // 75 days ago
    playCount: 6,
    hasPdf: true,
    hasActiveChat: false,
    chatCount: 0,
    category: 'Strategy',
    tags: ['space', '4x'],
  },
];

const MOCK_ACTIVITY: ActivityEvent[] = [
  {
    id: 'activity-1',
    type: 'session_completed',
    title: 'Giocato "Azul"',
    description: '2 giocatori • 45 min',
    entityId: 'game-5',
    entityType: 'session',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString(), // 1 day ago
  },
  {
    id: 'activity-2',
    type: 'game_added',
    title: 'Aggiunto "Azul"',
    description: 'Aggiunto alla collezione',
    entityId: 'game-5',
    entityType: 'game',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 20).toISOString(), // 20 days ago
  },
  {
    id: 'activity-3',
    type: 'chat_saved',
    title: 'Chat "Regole Wingspan"',
    description: '8 messaggi',
    entityId: 'game-1',
    entityType: 'chat',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 5).toISOString(), // 5 days ago
  },
];

const MOCK_STATS: CollectionStats = {
  totalGames: 6,
  privatePdfsCount: 4,
  activeChats: 3,
  totalReadingMinutes: 245,
  recentActivity: MOCK_ACTIVITY,
};

// Hero stats for collection section - Issue #3649
const MOCK_HERO_STATS: CollectionHeroStats = {
  totalGames: 6,
  privatePdfsCount: 4,
  totalSessions: 106,
  gamesPlayedThisMonth: 3,
  totalPlayTime: 2450, // minutes
};

// ============================================================================
// Sort Functions
// ============================================================================

function sortGames(games: CollectionGame[], sortBy: SortOption): CollectionGame[] {
  const sorted = [...games];

  switch (sortBy) {
    case 'date-added-desc':
      return sorted.sort((a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime());
    case 'date-added-asc':
      return sorted.sort((a, b) => new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime());
    case 'title-asc':
      return sorted.sort((a, b) => a.title.localeCompare(b.title));
    case 'title-desc':
      return sorted.sort((a, b) => b.title.localeCompare(a.title));
    case 'last-played-desc':
      return sorted.sort((a, b) => {
        const aTime = a.lastPlayedAt ? new Date(a.lastPlayedAt).getTime() : 0;
        const bTime = b.lastPlayedAt ? new Date(b.lastPlayedAt).getTime() : 0;
        return bTime - aTime;
      });
    case 'last-played-asc':
      return sorted.sort((a, b) => {
        const aTime = a.lastPlayedAt ? new Date(a.lastPlayedAt).getTime() : 0;
        const bTime = b.lastPlayedAt ? new Date(b.lastPlayedAt).getTime() : 0;
        return aTime - bTime;
      });
    case 'chat-activity-desc':
      return sorted.sort((a, b) => b.chatCount - a.chatCount);
    case 'chat-activity-asc':
      return sorted.sort((a, b) => a.chatCount - b.chatCount);
    case 'play-count-desc':
      return sorted.sort((a, b) => b.playCount - a.playCount);
    case 'play-count-asc':
      return sorted.sort((a, b) => a.playCount - b.playCount);
    default:
      return sorted;
  }
}

// ============================================================================
// Filter Functions
// ============================================================================

function filterGames(games: CollectionGame[], filters: ActiveFilters): CollectionGame[] {
  let filtered = [...games];

  if (filters.hasPdf !== undefined) {
    filtered = filtered.filter((game) => game.hasPdf === filters.hasPdf);
  }

  if (filters.hasActiveChat !== undefined) {
    filtered = filtered.filter((game) => game.hasActiveChat === filters.hasActiveChat);
  }

  if (filters.category) {
    filtered = filtered.filter((game) => game.category === filters.category);
  }

  return filtered;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export interface UseCollectionStatsResult {
  stats: CollectionStats | null;
  heroStats: CollectionHeroStats | null;
  games: CollectionGame[];
  isLoading: boolean;
  error: Error | null;
  refetch: () => void;
}

export function useCollectionStats(params?: CollectionQueryParams): UseCollectionStatsResult {
  const [stats, setStats] = useState<CollectionStats | null>(null);
  const [heroStats, setHeroStats] = useState<CollectionHeroStats | null>(null);
  const [games, setGames] = useState<CollectionGame[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const fetchData = () => {
    setIsLoading(true);
    setError(null);

    // Simulate API call with timeout
    // TODO: Replace with real API calls:
    // GET /api/v1/users/{userId}/library → UserLibraryEntry[]
    // GET /api/v1/users/{userId}/library/stats → CollectionHeroStats
    setTimeout(() => {
      try {
        // Apply filters
        let filteredGames = params?.filters
          ? filterGames(MOCK_GAMES, params.filters)
          : MOCK_GAMES;

        // Apply sorting
        if (params?.sortBy) {
          filteredGames = sortGames(filteredGames, params.sortBy);
        }

        setStats(MOCK_STATS);
        setHeroStats(MOCK_HERO_STATS);
        setGames(filteredGames);
        setIsLoading(false);
      } catch (err) {
        setError(err instanceof Error ? err : new Error('Failed to fetch collection data'));
        setIsLoading(false);
      }
    }, 500); // Simulate 500ms network delay
  };

  useEffect(() => {
    fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params?.sortBy, params?.filters?.hasPdf, params?.filters?.hasActiveChat, params?.filters?.category]);

  return {
    stats,
    heroStats,
    games,
    isLoading,
    error,
    refetch: fetchData,
  };
}

// ============================================================================
// Helper Hook for Individual Game
// ============================================================================

export interface UseCollectionGameResult {
  game: CollectionGame | null;
  isLoading: boolean;
  error: Error | null;
}

export function useCollectionGame(gameId: string): UseCollectionGameResult {
  const [game, setGame] = useState<CollectionGame | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    setIsLoading(true);
    setError(null);

    // Simulate API call
    setTimeout(() => {
      try {
        const foundGame = MOCK_GAMES.find((g) => g.id === gameId);
        if (!foundGame) {
          throw new Error(`Game with id ${gameId} not found`);
        }
        setGame(foundGame);
        setIsLoading(false);
      } catch (err) {
        setError(err instanceof Error ? err : new Error('Failed to fetch game'));
        setIsLoading(false);
      }
    }, 300);
  }, [gameId]);

  return {
    game,
    isLoading,
    error,
  };
}
