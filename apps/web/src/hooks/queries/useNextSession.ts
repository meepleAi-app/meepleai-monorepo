'use client';

import { useQuery } from '@tanstack/react-query';

export interface NextSessionData {
  id: string;
  name: string;
  scheduledAt: string;
  playerCount: number;
  games: string[];
}

/**
 * Query key factory for next-session queries
 */
export const nextSessionKeys = {
  all: ['sessions'] as const,
  next: () => [...nextSessionKeys.all, 'next'] as const,
};

/**
 * Hook to fetch the user's next upcoming session
 *
 * Returns null gracefully when sessions API is unavailable or returns empty.
 * Will be wired to the sessions API when the scheduling feature is ready.
 *
 * @returns UseQueryResult with next session data or null
 */
export function useNextSession() {
  return useQuery<NextSessionData | null>({
    queryKey: nextSessionKeys.next(),
    queryFn: async () => {
      try {
        // Sessions scheduling API not yet available — return null (discovery variant).
        // When the scheduling feature ships, wire this to:
        //   api.sessions.getActive(1, 0) and pick the nearest upcoming session.
        return null;
      } catch {
        return null;
      }
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}
