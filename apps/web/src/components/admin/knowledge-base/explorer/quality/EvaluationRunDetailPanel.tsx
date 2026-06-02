/**
 * EvaluationRunDetailPanel — detail half of the quality tab master-detail
 * (#1675). Renders the full EvaluationDetailDto: lifecycle chips,
 * goldset/seed/cost, then a 3-col Stat grid for precision/MRR/latency.
 *
 * Auto-polls via useEvaluation while the run is non-terminal (Pending,
 * GoldsetGenerating, Running). The errorMessage box appears only when
 * the run is Failed; the polling stops automatically once the status
 * lands in a terminal state.
 */

'use client';

import type { JSX } from 'react';

import { useEvaluation } from '@/hooks/queries/useEvaluation';

import { EvaluationStatusChip } from './EvaluationStatusChip';
import { QualityBandChip } from './QualityBandChip';

export interface EvaluationRunDetailPanelProps {
  readonly docId: string;
  readonly evaluationId: string | null;
}

export function EvaluationRunDetailPanel({
  docId,
  evaluationId,
}: EvaluationRunDetailPanelProps): JSX.Element {
  const query = useEvaluation(docId, evaluationId);

  if (evaluationId === null) {
    return (
      <div className="p-4 text-xs text-muted-foreground" data-testid="eval-detail-empty">
        Seleziona una run dall&apos;elenco per vederne i dettagli.
      </div>
    );
  }

  if (query.isLoading) {
    return (
      <div className="p-4 text-xs text-muted-foreground" data-testid="eval-detail-loading">
        Caricamento dettaglio…
      </div>
    );
  }

  if (query.isError || !query.data) {
    return (
      <div className="p-4 text-xs text-rose-700 dark:text-rose-300" data-testid="eval-detail-error">
        Errore: {(query.error as Error | undefined)?.message ?? 'detail unavailable'}
      </div>
    );
  }

  const run = query.data;

  return (
    <article className="space-y-3 p-4" data-testid="eval-detail-panel">
      <header className="flex flex-wrap items-center gap-2">
        <h3 className="font-quicksand text-sm font-bold">Run {run.evaluationId.slice(0, 8)}</h3>
        <EvaluationStatusChip status={run.status} />
        {run.metrics ? <QualityBandChip band={run.metrics.qualityBand} /> : null}
      </header>

      <dl className="grid grid-cols-3 gap-2 text-[11px]">
        <Stat label="Goldset" value={run.goldsetVersion} />
        <Stat label="Seed" value={run.goldsetGenerationSeed.toString()} />
        <Stat label="Cost" value={run.costUsd !== null ? `$${run.costUsd.toFixed(3)}` : '—'} />
        {run.metrics ? (
          <>
            <Stat label="Precision@1" value={run.metrics.precision.at1.toFixed(3)} />
            <Stat label="Precision@3" value={run.metrics.precision.at3.toFixed(3)} />
            <Stat label="Precision@5" value={run.metrics.precision.at5.toFixed(3)} />
            <Stat label="MRR" value={run.metrics.ranking.mrr.toFixed(3)} />
            <Stat label="p50 latency" value={`${run.metrics.latency.p50Ms} ms`} />
            <Stat label="p95 latency" value={`${run.metrics.latency.p95Ms} ms`} />
          </>
        ) : null}
      </dl>

      {run.errorMessage ? (
        <div
          className="rounded-md border border-rose-500/30 p-2 text-xs text-rose-700 dark:text-rose-300"
          data-testid="eval-detail-error-message"
        >
          {run.errorMessage}
        </div>
      ) : null}
    </article>
  );
}

interface StatProps {
  label: string;
  value: string;
}

function Stat({ label, value }: StatProps): JSX.Element {
  return (
    <div className="rounded-md border border-border/40 bg-muted/40 px-2 py-1.5">
      <dt className="font-mono text-[9px] uppercase tracking-wider text-muted-foreground">
        {label}
      </dt>
      <dd className="font-quicksand text-[13px] font-bold">{value}</dd>
    </div>
  );
}
