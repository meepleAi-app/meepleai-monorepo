/**
 * useGameAgents - Fetch Agents for a Game
 *
 * Issue #4229: Knowledge Base Documents Display
 *
 * React Query hook for fetching all AI agents associated with a game.
 * Used by KnowledgeBaseTab to get agents whose documents should be displayed.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { AgentDto } from '@/lib/api/schemas';

// ========== Query Keys ==========

export const gameAgentsKeys = {
  all: ['agents', 'by-game'] as const,
  byGame: (gameId: string) => ['agents', 'by-game', gameId] as const,
} as const;

// ========== Query Options ==========

interface UseGameAgentsOptions {
  gameId: string | null;
  enabled?: boolean;
}

// ========== Query Hook ==========

/**
 * Fetch all AI agents available for a game
 *
 * @param options - Query options
 * @returns Query result with agents data
 *
 * @example
 * ```tsx
 * const { data: agents, isLoading } = useGameAgents({ gameId: 'xxx' });
 * ```
 */
export function useGameAgents({ gameId, enabled = true }: UseGameAgentsOptions) {
  return useQuery<AgentDto[], Error>({
    queryKey: gameAgentsKeys.byGame(gameId ?? ''),
    queryFn: async () => {
      if (!gameId) {
        throw new Error('Game ID is required');
      }
      return api.games.getAgents(gameId);
    },
    enabled: enabled && gameId !== null,
    staleTime: 30_000, // 30 seconds - agent lists don't change frequently
    gcTime: 5 * 60_000, // 5 minutes cache
    // Don't retry on 404 — game ID may not exist in shared catalog (e.g. private game UUIDs)
    retry: false,
  });
}
