/**
 * EvaluationHistoryList — paginated history of per-doc eval runs (#1675).
 *
 * Master half of the quality tab master-detail. Each row is a button so
 * the parent can lift the selected evaluationId state into
 * EvaluationRunDetailPanel. The pagination footer renders a friendly
 * "page X of Y" plus prev/next that disable themselves at boundaries.
 *
 * Loading / error / empty states each have their own data-testid for
 * deterministic E2E selectors.
 */

'use client';

import { useState, type JSX } from 'react';

import { useEvaluationList } from '@/hooks/queries/useEvaluationList';

import { EvaluationStatusChip } from './EvaluationStatusChip';
import { QualityBandChip } from './QualityBandChip';

export interface EvaluationHistoryListProps {
  readonly docId: string;
  readonly onSelect: (evaluationId: string) => void;
}

const PAGE_SIZE = 20;

export function EvaluationHistoryList({
  docId,
  onSelect,
}: EvaluationHistoryListProps): JSX.Element {
  const [page, setPage] = useState(1);
  const query = useEvaluationList(docId, page, PAGE_SIZE);

  if (query.isLoading) {
    return (
      <div className="text-xs text-muted-foreground" data-testid="eval-list-loading">
        Caricamento storico…
      </div>
    );
  }

  if (query.isError) {
    return (
      <div className="text-xs text-rose-700 dark:text-rose-300" data-testid="eval-list-error">
        Errore: {(query.error as Error).message}
      </div>
    );
  }

  if (!query.data || query.data.items.length === 0) {
    return (
      <div className="text-xs text-muted-foreground" data-testid="eval-list-empty">
        Nessuna eval per questo documento.
      </div>
    );
  }

  const totalPages = Math.max(1, Math.ceil(query.data.totalCount / query.data.pageSize));
  const isLastPage = page * query.data.pageSize >= query.data.totalCount;

  return (
    <div className="space-y-2" data-testid="eval-list">
      <ul className="divide-y divide-border/60">
        {query.data.items.map(item => (
          <li key={item.evaluationId} className="flex flex-wrap items-center gap-3 py-2">
            <button
              type="button"
              onClick={() => onSelect(item.evaluationId)}
              className="flex-1 rounded text-left font-mono text-[11px] hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              data-testid={`eval-list-row-${item.evaluationId}`}
            >
              {new Date(item.startedAt).toLocaleString('it-IT')}
            </button>
            <EvaluationStatusChip status={item.status} />
            <QualityBandChip band={item.qualityBand} />
            <span className="font-mono text-[10px] text-muted-foreground">
              p@5 {item.precisionAt5?.toFixed(2) ?? '—'} · mrr {item.mrr?.toFixed(2) ?? '—'} · $
              {item.costUsd?.toFixed(3) ?? '—'}
            </span>
          </li>
        ))}
      </ul>

      <div className="flex items-center justify-between text-[10px] text-muted-foreground">
        <span>
          Pagina {query.data.page} / {totalPages}
        </span>
        <div className="flex gap-2">
          <button
            type="button"
            disabled={page <= 1}
            onClick={() => setPage(p => Math.max(1, p - 1))}
            className="rounded px-1 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:opacity-40"
            data-testid="eval-list-prev"
          >
            ← prev
          </button>
          <button
            type="button"
            disabled={isLastPage}
            onClick={() => setPage(p => p + 1)}
            className="rounded px-1 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:opacity-40"
            data-testid="eval-list-next"
          >
            next →
          </button>
        </div>
      </div>
    </div>
  );
}
