/**
 * useSessionDetail - React Query hooks for ExtraMeepleCard session data
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 *
 * Provides hooks for fetching session detail, toolkit, snapshots, and state.
 * Uses the sessionsKeys query key factory from useActiveSessions.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { GameSessionDto } from '@/lib/api/schemas';
import type { SnapshotInfo } from '@/components/ui/data-display/extra-meeple-card/types';

import { sessionsKeys } from './useActiveSessions';

// ============================================================================
// Query Key Extensions
// ============================================================================

export const sessionDetailKeys = {
  ...sessionsKeys,
  state: (id: string) => [...sessionsKeys.detail(id), 'state'] as const,
  snapshots: (id: string) => [...sessionsKeys.detail(id), 'snapshots'] as const,
  toolkit: (id: string) => [...sessionsKeys.detail(id), 'toolkit'] as const,
};

// ============================================================================
// Hooks
// ============================================================================

/**
 * Hook to fetch full session detail for ExtraMeepleCard.
 * Note: uses 15s staleTime (vs 30s in useSession) since the ExtraMeepleCard
 * needs fresher data during active gameplay with multiple tabs.
 */
export function useSessionDetail(
  sessionId: string,
  enabled: boolean = true
): UseQueryResult<GameSessionDto | null, Error> {
  return useQuery({
    queryKey: sessionDetailKeys.detail(sessionId),
    queryFn: () => api.sessions.getById(sessionId),
    enabled: enabled && !!sessionId,
    staleTime: 15 * 1000,
    refetchOnWindowFocus: true,
  });
}

/**
 * Hook to fetch current game state
 */
export function useSessionState(
  sessionId: string,
  enabled: boolean = true
): UseQueryResult<unknown, Error> {
  return useQuery({
    queryKey: sessionDetailKeys.state(sessionId),
    queryFn: () => api.sessions.getState(sessionId),
    enabled: enabled && !!sessionId,
    staleTime: 10 * 1000, // 10s - state changes more frequently
  });
}

/**
 * Hook to fetch session snapshots for history tab
 */
export function useSessionSnapshots(
  sessionId: string,
  enabled: boolean = true
): UseQueryResult<SnapshotInfo[], Error> {
  return useQuery({
    queryKey: sessionDetailKeys.snapshots(sessionId),
    queryFn: () => api.sessions.getSnapshots(sessionId) as Promise<SnapshotInfo[]>,
    enabled: enabled && !!sessionId,
    staleTime: 30 * 1000,
  });
}
