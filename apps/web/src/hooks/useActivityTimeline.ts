/**
 * useActivityTimeline - React Query hook for fetched/filtered activity timeline
 *
 * Issue #3925 - Advanced Timeline Filters & Search
 * Depends on: #3923 (Advanced Timeline Service - backend)
 *
 * Calls GET /api/v1/dashboard/activity-timeline with filter parameters.
 * Uses 5-min stale time matching existing dashboard cache TTL.
 *
 * @example
 * ```tsx
 * const { params } = useActivityTimelineParams();
 * const { data, isLoading } = useActivityTimeline(params);
 * ```
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import type { ActivityEventType } from '@/components/dashboard/ActivityFeed';

// ============================================================================
// Types
// ============================================================================

export interface ActivityTimelineRequest {
  types: ActivityEventType[];
  search: string;
  skip: number;
  take: number;
  order: 'asc' | 'desc';
}

export interface ActivityTimelineItem {
  id: string;
  type: ActivityEventType;
  gameId?: string;
  gameName?: string;
  sessionId?: string;
  chatId?: string;
  topic?: string;
  timestamp: string;
}

export interface ActivityTimelineResponse {
  items: ActivityTimelineItem[];
  totalCount: number;
  hasMore: boolean;
}

// ============================================================================
// Query Key Factory
// ============================================================================

export const activityTimelineKeys = {
  all: ['activity-timeline'] as const,
  filtered: (params: ActivityTimelineRequest) =>
    [...activityTimelineKeys.all, params] as const,
};

// ============================================================================
// API Client
// ============================================================================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
const USE_MOCK_DATA = process.env.NEXT_PUBLIC_USE_MOCK_DASHBOARD !== 'false';

/**
 * Mock data for development (until backend integration is stable)
 */
const MOCK_ITEMS: ActivityTimelineItem[] = [
  {
    id: 'event-1',
    type: 'game_added',
    gameId: 'game-wingspan',
    gameName: 'Wingspan',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
  },
  {
    id: 'event-2',
    type: 'session_completed',
    sessionId: 'session-1',
    gameName: 'Catan',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 5).toISOString(),
  },
  {
    id: 'event-3',
    type: 'chat_saved',
    chatId: 'chat-1',
    topic: 'Regole Wingspan',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 28).toISOString(),
  },
  {
    id: 'event-4',
    type: 'wishlist_added',
    gameId: 'game-terraforming',
    gameName: 'Terraforming Mars',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(),
  },
  {
    id: 'event-5',
    type: 'achievement_unlocked',
    gameName: 'Streak 7 giorni',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 3).toISOString(),
  },
  {
    id: 'event-6',
    type: 'session_completed',
    sessionId: 'session-2',
    gameName: 'Azul',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 4).toISOString(),
  },
  {
    id: 'event-7',
    type: 'game_added',
    gameId: 'game-gloomhaven',
    gameName: 'Gloomhaven',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 5).toISOString(),
  },
  {
    id: 'event-8',
    type: 'chat_saved',
    chatId: 'chat-2',
    topic: 'Strategie Catan',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 6).toISOString(),
  },
  {
    id: 'event-9',
    type: 'wishlist_added',
    gameId: 'game-spirit-island',
    gameName: 'Spirit Island',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7).toISOString(),
  },
  {
    id: 'event-10',
    type: 'achievement_unlocked',
    gameName: 'Collezionista 10',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 10).toISOString(),
  },
];

function filterMockItems(params: ActivityTimelineRequest): ActivityTimelineResponse {
  let filtered = [...MOCK_ITEMS];

  // Filter by types
  if (params.types.length > 0) {
    filtered = filtered.filter((item) => params.types.includes(item.type));
  }

  // Filter by search (case-insensitive on gameName/topic)
  if (params.search) {
    const searchLower = params.search.toLowerCase();
    filtered = filtered.filter(
      (item) =>
        item.gameName?.toLowerCase().includes(searchLower) ||
        item.topic?.toLowerCase().includes(searchLower)
    );
  }

  // Sort
  filtered.sort((a, b) => {
    const dateA = new Date(a.timestamp).getTime();
    const dateB = new Date(b.timestamp).getTime();
    return params.order === 'desc' ? dateB - dateA : dateA - dateB;
  });

  const totalCount = filtered.length;
  const items = filtered.slice(params.skip, params.skip + params.take);

  return {
    items,
    totalCount,
    hasMore: params.skip + params.take < totalCount,
  };
}

/**
 * Fetch activity timeline from backend API
 */
export async function fetchActivityTimeline(
  params: ActivityTimelineRequest
): Promise<ActivityTimelineResponse> {
  if (USE_MOCK_DATA) {
    await new Promise((resolve) => setTimeout(resolve, 200));
    return filterMockItems(params);
  }

  const url = new URL(`${API_BASE_URL}/api/v1/dashboard/activity-timeline`);

  if (params.types.length > 0) {
    for (const type of params.types) {
      url.searchParams.append('type', type);
    }
  }
  if (params.search) {
    url.searchParams.set('search', params.search);
  }
  url.searchParams.set('skip', params.skip.toString());
  url.searchParams.set('take', params.take.toString());
  url.searchParams.set('order', params.order);

  const response = await fetch(url.toString(), {
    method: 'GET',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error(`Activity timeline API error: ${response.status} ${response.statusText}`);
  }

  return response.json();
}

// ============================================================================
// Hook
// ============================================================================

export function useActivityTimeline(
  params: ActivityTimelineRequest
): UseQueryResult<ActivityTimelineResponse, Error> {
  return useQuery({
    queryKey: activityTimelineKeys.filtered(params),
    queryFn: () => fetchActivityTimeline(params),
    staleTime: 5 * 60 * 1000, // 5 minutes (matches dashboard cache)
    retry: 2,
    refetchOnWindowFocus: true,
  });
}
