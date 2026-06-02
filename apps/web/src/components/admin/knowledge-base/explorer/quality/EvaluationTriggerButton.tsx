/**
 * EvaluationTriggerButton — admin-side button that fires
 * `useStartEvaluation`. Surfaces the "override cost cap" toggle only
 * when the caller has admin/superadmin role (plan amendment A4 — the
 * project has no permissions[] field on AuthUser, so role is the gate).
 *
 * Errors surface inline next to the button rather than via toast so the
 * admin sees CostCapExceeded / RateLimited messages immediately in
 * context. The mutation auto-invalidates the list cache on success.
 */

'use client';

import { useState, type JSX } from 'react';

import { useStartEvaluation } from '@/hooks/queries/useStartEvaluation';

export interface EvaluationTriggerButtonProps {
  readonly docId: string;
  readonly hasOverrideCostCapPermission: boolean;
}

export function EvaluationTriggerButton({
  docId,
  hasOverrideCostCapPermission,
}: EvaluationTriggerButtonProps): JSX.Element {
  const [override, setOverride] = useState(false);
  const mutation = useStartEvaluation(docId);

  const handleClick = () => {
    mutation.mutate({ overrideCostCap: override });
  };

  return (
    <div className="flex flex-wrap items-center gap-3">
      <button
        type="button"
        onClick={handleClick}
        disabled={mutation.isPending}
        data-testid="eval-trigger-button"
        className="rounded-md border border-border bg-card px-3 py-1.5 text-xs font-medium hover:bg-muted disabled:opacity-60"
      >
        {mutation.isPending ? 'Avvio…' : '🔬 Lancia eval'}
      </button>

      {hasOverrideCostCapPermission ? (
        <label className="inline-flex items-center gap-1 text-xs text-muted-foreground">
          <input
            type="checkbox"
            checked={override}
            onChange={e => setOverride(e.target.checked)}
            data-testid="eval-override-toggle"
          />
          Override cost cap
        </label>
      ) : null}

      {mutation.isError ? (
        <span data-testid="eval-error" className="text-xs text-rose-700 dark:text-rose-300">
          {(mutation.error as Error).message}
        </span>
      ) : null}
    </div>
  );
}
