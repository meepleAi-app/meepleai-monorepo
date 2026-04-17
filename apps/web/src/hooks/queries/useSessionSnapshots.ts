'use client';

/**
 * useSessionSnapshots — React Query hooks for Session Vision AI snapshots
 *
 * Provides list, game-state, and create-mutation hooks for session snapshots.
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { createSessionSnapshotsClient } from '@/lib/api/clients/sessionSnapshotsClient';

const client = createSessionSnapshotsClient();

export const snapshotKeys = {
  all: ['session-snapshots'] as const,
  list: (sid: string) => [...snapshotKeys.all, 'list', sid] as const,
  gameState: (sid: string) => [...snapshotKeys.all, 'game-state', sid] as const,
};

export function useSessionVisionSnapshots(sessionId: string, enabled = true) {
  return useQuery({
    queryKey: snapshotKeys.list(sessionId),
    queryFn: () => client.getSnapshots(sessionId),
    enabled: enabled && !!sessionId,
    staleTime: 30_000,
  });
}

export function useLatestGameState(sessionId: string, enabled = true) {
  return useQuery({
    queryKey: snapshotKeys.gameState(sessionId),
    queryFn: () => client.getGameState(sessionId),
    enabled: enabled && !!sessionId,
    staleTime: 60_000,
  });
}

export function useCreateSnapshot(sessionId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (args: { userId: string; turnNumber: number; images: File[]; caption?: string }) =>
      client.createSnapshot(sessionId, args.userId, args.turnNumber, args.images, args.caption),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: snapshotKeys.list(sessionId) });
      qc.invalidateQueries({ queryKey: snapshotKeys.gameState(sessionId) });
    },
  });
}
