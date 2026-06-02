/**
 * useStartEvaluation — TanStack Query mutation to trigger a per-doc
 * evaluation run (#1675).
 *
 * On success the list cache for the doc is invalidated so the freshly
 * persisted run appears in the history list immediately. The detail
 * polling lives in `useEvaluation`, which can be wired to the result's
 * evaluationId by the caller.
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  EvaluationStartedResult,
  StartEvaluationRequest,
} from '@/lib/api/schemas/kb-quality.schemas';

export const kbQualityKeys = {
  all: ['kb-quality'] as const,
  list: (docId: string) => [...kbQualityKeys.all, 'list', docId] as const,
  detail: (docId: string, evaluationId: string | null) =>
    [...kbQualityKeys.all, 'detail', docId, evaluationId] as const,
};

export function useStartEvaluation(docId: string) {
  const qc = useQueryClient();
  return useMutation<EvaluationStartedResult, Error, StartEvaluationRequest>({
    mutationKey: [...kbQualityKeys.all, 'start', docId],
    mutationFn: body => api.kbQuality.startEvaluation(docId, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: kbQualityKeys.list(docId) });
    },
  });
}
