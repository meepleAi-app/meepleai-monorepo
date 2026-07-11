/**
 * useMechanicCard — React Query hook for the public, login-gated mechanic card
 * (ME-M1.6, Issue #528).
 *
 * Wraps `api.sharedGames.getPublishedMechanicCard(gameId)`:
 *   - `data === undefined` while loading / before enabled
 *   - `data === null` → backend returned 404 (no published card OR suppressed/
 *     taken down). The view translates this to Next's `notFound()`.
 *   - `data` populated → the published card DTO
 *   - `isError` → genuine failure (5xx / network / schema) — distinct from 404
 *
 * `gameId` is the sharedGameId (matches the `/games/{sharedGameId}/card` route).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { PublishedMechanicCardDto } from '@/lib/api/schemas/mechanic-card.schemas';

/** Query key factory for the public mechanic card. */
export const mechanicCardKeys = {
  all: ['mechanicCard'] as const,
  detail: (gameId: string) => [...mechanicCardKeys.all, 'detail', gameId] as const,
};

/**
 * Fetch the published mechanic card for a game.
 *
 * @param gameId SharedGame UUID (or null when the route param is not yet ready)
 * @param options.enabled Extra gate on top of the internal `!!gameId` guard
 */
export function useMechanicCard(
  gameId: string | null,
  options: { enabled?: boolean } = {}
): UseQueryResult<PublishedMechanicCardDto | null, Error> {
  const { enabled = true } = options;
  const safeId = gameId ?? '';
  return useQuery({
    queryKey: mechanicCardKeys.detail(safeId),
    queryFn: () => api.sharedGames.getPublishedMechanicCard(safeId),
    enabled: enabled && !!gameId,
    // A published card changes only on republish; safe to cache for a while.
    staleTime: 10 * 60 * 1000,
  });
}
