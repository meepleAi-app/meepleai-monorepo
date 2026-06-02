/**
 * useEvaluationList — TanStack Query paginated history hook (#1675).
 *
 * Cached for 30s so navigating tabs doesn't re-fetch immediately; the
 * useStartEvaluation mutation invalidates this cache on success so a
 * new run shows up without waiting for the stale window to elapse.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { PagedEvaluations } from '@/lib/api/schemas/kb-quality.schemas';

import { kbQualityKeys } from './useStartEvaluation';

export function useEvaluationList(docId: string, page = 1, pageSize = 20) {
  return useQuery<PagedEvaluations, Error>({
    queryKey: [...kbQualityKeys.list(docId), page, pageSize],
    queryFn: () => api.kbQuality.listEvaluations(docId, page, pageSize),
    staleTime: 30_000,
  });
}
