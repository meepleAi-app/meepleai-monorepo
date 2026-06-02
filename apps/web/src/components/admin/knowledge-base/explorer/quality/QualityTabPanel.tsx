/**
 * QualityTabPanel — orchestrates the master-detail layout for the KB doc
 * "Quality" tab (#1675). Owns the selected evaluationId state; left col
 * shows the trigger button + history list, right col renders the detail.
 *
 * Permission gating (plan amendment A4): the override-cost-cap toggle is
 * surfaced only when the caller is admin/superadmin. The check is lifted
 * to the parent (`KbDocDetailPanel` via `isAdminOrAbove(currentUser)`)
 * so this orchestrator stays role-agnostic and easier to test in isolation.
 */

'use client';

import { useState, type JSX } from 'react';

import { EvaluationHistoryList } from './EvaluationHistoryList';
import { EvaluationRunDetailPanel } from './EvaluationRunDetailPanel';
import { EvaluationTriggerButton } from './EvaluationTriggerButton';

export interface QualityTabPanelProps {
  readonly docId: string;
  readonly hasOverrideCostCapPermission: boolean;
}

export function QualityTabPanel({
  docId,
  hasOverrideCostCapPermission,
}: QualityTabPanelProps): JSX.Element {
  const [selected, setSelected] = useState<string | null>(null);

  return (
    <div className="grid grid-cols-12 gap-4 p-4" data-testid="kb-quality-tab-panel">
      <section className="col-span-5 space-y-3">
        <EvaluationTriggerButton
          docId={docId}
          hasOverrideCostCapPermission={hasOverrideCostCapPermission}
        />
        <h3 className="font-quicksand text-sm font-semibold">Storico eval</h3>
        <EvaluationHistoryList docId={docId} onSelect={setSelected} />
      </section>
      <section className="col-span-7 border-l border-border/60 dark:border-zinc-700/60">
        <EvaluationRunDetailPanel docId={docId} evaluationId={selected} />
      </section>
    </div>
  );
}
