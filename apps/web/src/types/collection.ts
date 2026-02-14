/**
 * Collection Types - Issue #3476
 *
 * Types and interfaces for User Collection Dashboard
 */

import type { ActivityEvent } from '@/components/dashboard/ActivityFeed';

// ============================================================================
// Game Collection Types
// ============================================================================

export interface CollectionGame {
  id: string;
  bggId?: number;
  title: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  yearPublished?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playingTime?: number;
  complexity?: number;
  rating?: number;

  // User-specific metadata
  addedAt: string;
  lastPlayedAt?: string;
  playCount: number;
  hasPdf: boolean;
  hasActiveChat: boolean;
  chatCount: number;
  status?: 'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade';
  category?: string;
  tags?: string[];
  notes?: string;
}

// ============================================================================
// Collection Statistics
// ============================================================================

export interface CollectionStats {
  totalGames: number;
  privatePdfsCount: number;
  activeChats: number;
  totalReadingMinutes: number;
  recentActivity: ActivityEvent[];
}

/**
 * Hero stats for the collection section
 * Issue #3649 - User Collection Dashboard Enhancement
 */
export interface CollectionHeroStats {
  totalGames: number;
  privatePdfsCount: number;
  totalSessions: number;
  gamesPlayedThisMonth?: number;
  totalPlayTime?: number;
}

/**
 * Inline filter configuration for collection
 * Issue #3649 - User Collection Dashboard Enhancement
 */
export interface CollectionFilters {
  hasPdf: boolean | null;
  hasActiveChat: boolean | null;
  category: string | null;
  minPlayers?: number;
  maxPlayers?: number;
}

export interface StatCard {
  label: string;
  value: number | string;
  icon: string;
  color: string;
  bgColor: string;
  trend?: {
    value: number;
    isPositive: boolean;
  };
}

// ============================================================================
// Sort & Filter Options
// ============================================================================

export type SortOption =
  | 'date-added-desc'
  | 'date-added-asc'
  | 'title-asc'
  | 'title-desc'
  | 'last-played-desc'
  | 'last-played-asc'
  | 'chat-activity-desc'
  | 'chat-activity-asc'
  | 'play-count-desc'
  | 'play-count-asc';

export interface SortConfig {
  value: SortOption;
  label: string;
  icon?: string;
}

export type FilterType = 'has-pdf' | 'has-active-chat' | 'category';

export interface FilterOption {
  type: FilterType;
  value: string;
  label: string;
  count?: number;
}

export interface ActiveFilters {
  hasPdf?: boolean;
  hasActiveChat?: boolean;
  category?: string;
}

// ============================================================================
// View Modes
// ============================================================================

export type ViewMode = 'grid' | 'list';

// ============================================================================
// Collection Query Params
// ============================================================================

export interface CollectionQueryParams {
  sortBy?: SortOption;
  filters?: ActiveFilters;
  searchQuery?: string;
  page?: number;
  pageSize?: number;
}

// ============================================================================
// API Response Types
// ============================================================================

export interface CollectionResponse {
  games: CollectionGame[];
  stats: CollectionStats;
  totalCount: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}

// ============================================================================
// Component Props Types
// ============================================================================

export interface MeepleCardProps {
  game: CollectionGame;
  onPlay?: (gameId: string) => void;
  onViewChat?: (gameId: string) => void;
  onViewPdf?: (gameId: string) => void;
  className?: string;
}

export interface CollectionGridProps {
  games: CollectionGame[];
  sortBy: SortOption;
  filters: ActiveFilters;
  onSortChange: (sort: SortOption) => void;
  onFilterChange: (filters: ActiveFilters) => void;
  onGameClick?: (gameId: string) => void;
  isLoading?: boolean;
  className?: string;
}

export interface CollectionStatsProps {
  stats?: CollectionStats;
  isLoading?: boolean;
  className?: string;
}
