/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 37)
 *
 * Query hook: fetch the single most recent metrics snapshot for a shared game.
 * Wraps `api.admin.getTrend(sharedGameId, 1)` and returns the first element (or null).
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { MechanicAnalysisMetricsDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

/**
 * Fetch the latest metrics snapshot for a shared game (descending by `computedAt`).
 * Returns `null` when no metrics have ever been computed.
 * Disabled when `sharedGameId` is null/empty.
 */
export function useLatestMetrics(
  sharedGameId: string | null
): UseQueryResult<MechanicAnalysisMetricsDto | null, Error> {
  return useQuery({
    queryKey: mechanicValidationKeys.trend.byGame(sharedGameId ?? '', 1),
    queryFn: async () => {
      const trend = await api.admin.getTrend(sharedGameId as string, 1);
      return trend.length > 0 ? trend[0] : null;
    },
    enabled: !!sharedGameId,
    staleTime: 30_000,
  });
}
