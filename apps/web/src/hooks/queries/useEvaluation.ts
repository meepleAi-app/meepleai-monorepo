/**
 * useEvaluation — TanStack Query detail hook with auto-polling while the
 * run is non-terminal (#1675).
 *
 * Polls every 3s for Pending/GoldsetGenerating/Running; stops polling once
 * the BE marks the run Completed/Failed/RateLimited/CostCapped. `enabled`
 * is gated on a non-null evaluationId so the hook can sit dormant in the
 * detail panel until a row is selected.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { EvaluationDetailDto } from '@/lib/api/schemas/kb-quality.schemas';

import { kbQualityKeys } from './useStartEvaluation';

const TERMINAL_STATUSES: ReadonlySet<EvaluationDetailDto['status']> = new Set([
  'Completed',
  'Failed',
  'RateLimited',
  'CostCapped',
]);

export function useEvaluation(docId: string, evaluationId: string | null) {
  return useQuery<EvaluationDetailDto | null, Error>({
    queryKey: kbQualityKeys.detail(docId, evaluationId),
    queryFn: async () => {
      if (evaluationId === null) {
        return null;
      }
      return api.kbQuality.getEvaluation(docId, evaluationId);
    },
    enabled: evaluationId !== null,
    refetchInterval: query => {
      const status = query.state.data?.status;
      if (status === undefined) {
        return 3_000;
      }
      return TERMINAL_STATUSES.has(status) ? false : 3_000;
    },
  });
}
