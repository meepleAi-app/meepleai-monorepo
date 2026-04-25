/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 35)
 *
 * Query hook: fetch the curated golden-set bundle for a shared game.
 * Wraps `api.admin.getGolden(sharedGameId)`.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { GoldenForGameDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

/**
 * Fetch the golden-set bundle (claims + BGG tags + version hash) for a shared game.
 * Disabled when `sharedGameId` is null.
 */
export function useGoldenForGame(
  sharedGameId: string | null
): UseQueryResult<GoldenForGameDto, Error> {
  return useQuery({
    queryKey: mechanicValidationKeys.golden.byGame(sharedGameId ?? ''),
    // sharedGameId is guaranteed non-null by the `enabled` guard below.
    queryFn: () => api.admin.getGolden(sharedGameId as string),
    enabled: !!sharedGameId,
  });
}
