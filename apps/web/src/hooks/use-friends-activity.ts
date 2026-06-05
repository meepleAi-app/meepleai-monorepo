/**
 * useFriendsActivity Hook
 *
 * Fetches friend activity feed for dashboard "Cosa fanno i tuoi" section.
 * Backend endpoint: GET /api/v1/dashboard/friends-activity?limit=10
 *
 * Asse C (#1898) WP5 T5.
 */

import { useQuery } from '@tanstack/react-query';

import { getApiBase } from '@/lib/api/core/httpClient';

export type FriendActivityVerb = 'completed' | 'created' | 'joined';
export type FriendActivityRefType = 'game' | 'gameNight';

export interface FriendActivity {
  friendUserId: string;
  avatar: string;
  name: string;
  verb: FriendActivityVerb;
  gameOrEventId: string;
  gameOrEventType: FriendActivityRefType;
  gameOrEventName: string;
  timestamp: string; // ISO
}

interface UseFriendsActivityOptions {
  enabled?: boolean;
  limit?: number;
}

export function useFriendsActivity(options: UseFriendsActivityOptions = {}) {
  const { enabled = true, limit = 10 } = options;

  return useQuery({
    queryKey: ['friends-activity', limit],
    queryFn: async () => {
      const apiBase = getApiBase();
      const res = await fetch(`${apiBase}/api/v1/dashboard/friends-activity?limit=${limit}`, {
        credentials: 'include',
      });

      if (res.status === 401) {
        return [] as FriendActivity[]; // unauthenticated, silent fallback
      }
      if (!res.ok) {
        throw new Error(`Friends activity fetch failed: ${res.status}`);
      }

      return (await res.json()) as FriendActivity[];
    },
    enabled,
    staleTime: 60 * 1000, // 1 min
    refetchOnWindowFocus: false,
  });
}
