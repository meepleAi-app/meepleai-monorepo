/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 37)
 *
 * Trigger button for `useCalculateMetrics(analysisId)`. Shows pending spinner
 * while the mutation is in flight. Toasts on success/failure are owned by the
 * underlying hook (`useCalculateMetrics`) — this component only forwards the
 * `onSuccess` callback to the parent so the page can refetch data.
 */

'use client';

import { Loader2Icon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useCalculateMetrics } from '@/hooks/admin/useCalculateMetrics';
import type { CalculateMetricsResponse } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

export interface EvaluateButtonProps {
  /** Mechanic analysis aggregate id whose snapshot to (re)compute. */
  analysisId: string;
  /** Optional callback after the mutation succeeds. */
  onSuccess?: (response: CalculateMetricsResponse) => void;
}

export function EvaluateButton({ analysisId, onSuccess }: EvaluateButtonProps) {
  const mutation = useCalculateMetrics();

  const handleClick = () => {
    mutation.mutate(analysisId, {
      onSuccess: response => {
        onSuccess?.(response);
      },
    });
  };

  return (
    <Button
      type="button"
      onClick={handleClick}
      disabled={mutation.isPending}
      data-testid="evaluate-metrics-button"
    >
      {mutation.isPending && <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />}
      {mutation.isPending ? 'Evaluating…' : 'Evaluate metrics'}
    </Button>
  );
}
