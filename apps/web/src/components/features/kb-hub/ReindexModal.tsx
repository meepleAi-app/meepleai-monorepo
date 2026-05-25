/**
 * ReindexModal — re-index full KB modal with FSM (confirm → running → done).
 *
 * Pure presentational. Issue #1481.
 * Mapped from `admin-mockups/design_files/sp4-kb-hub.jsx` ReindexModal.
 *
 * FSM contract:
 *   - phase='confirm': cost breakdown + Re-index/Annulla CTAs
 *   - phase='running': spinner + progress bar + jobId (optional)
 *   - phase='done': success badge + summary + Chiudi CTA
 * Caller owns FSM transitions and mutation lifecycle.
 */

'use client';

import type { ReactElement } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

export type ReindexPhase = 'confirm' | 'running' | 'done';

export interface ReindexCostRow {
  readonly key: string;
  readonly label: string;
  readonly value: string;
  readonly unit?: string;
  readonly bold?: boolean;
}

export interface ReindexModalLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly costHeader: string;
  readonly description: string;
  readonly reindexCta: string;
  readonly cancelCta: string;
  readonly runningTitle: string;
  readonly jobIdLabel: string;
  readonly progressTemplate: string; // "{processed} / {total} chunks"
  readonly doneTitle: string;
  readonly doneSummaryTemplate: string; // "{chunks} chunks · {embeddings} embeddings · costo reale {cost}"
  readonly closeCta: string;
}

export interface ReindexModalProgress {
  readonly current: number;
  readonly total: number;
  readonly jobId?: string;
}

export interface ReindexModalSummary {
  readonly chunks: number;
  readonly embeddings: number;
  readonly actualCost: string;
}

export interface ReindexModalProps {
  readonly open: boolean;
  readonly phase: ReindexPhase;
  readonly labels: ReindexModalLabels;
  readonly costRows: ReadonlyArray<ReindexCostRow>;
  readonly onConfirm: () => void;
  readonly onClose: () => void;
  readonly progress?: ReindexModalProgress;
  readonly summary?: ReindexModalSummary;
}

export function ReindexModal(props: ReindexModalProps): ReactElement {
  const { open, phase, labels, costRows, onConfirm, onClose, progress, summary } = props;

  const handleOpenChange = (isOpen: boolean): void => {
    if (!isOpen) onClose();
  };

  const progressPct =
    progress && progress.total > 0
      ? Math.min(100, Math.round((progress.current / progress.total) * 100))
      : 0;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent data-slot="kb-hub-reindex-modal" data-phase={phase} className="max-w-md">
        <DialogHeader>
          <DialogTitle>
            <span aria-hidden="true" className="mr-2">
              ⟳
            </span>
            {phase === 'confirm'
              ? labels.title
              : phase === 'running'
                ? labels.runningTitle
                : labels.doneTitle}
          </DialogTitle>
          {phase === 'confirm' && <DialogDescription>{labels.subtitle}</DialogDescription>}
        </DialogHeader>

        {phase === 'confirm' && (
          <>
            <div
              data-slot="kb-hub-reindex-cost-table"
              className="overflow-hidden rounded-md border border-border bg-muted"
            >
              <div className="border-b border-border px-3.5 py-2 font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
                {labels.costHeader}
              </div>
              {costRows.map(row => (
                <div
                  key={row.key}
                  className={
                    row.bold
                      ? 'flex items-center justify-between border-b border-border/50 bg-entity-kb/6 px-3.5 py-2 last:border-b-0'
                      : 'flex items-center justify-between border-b border-border/50 px-3.5 py-2 last:border-b-0'
                  }
                >
                  <span
                    className={
                      row.bold
                        ? 'text-xs font-bold text-foreground'
                        : 'text-xs text-muted-foreground'
                    }
                  >
                    {row.label}
                  </span>
                  <span
                    className={
                      row.bold
                        ? 'font-mono text-[13px] font-extrabold text-entity-kb'
                        : 'font-mono text-[11px] text-muted-foreground'
                    }
                  >
                    {row.value}
                    {row.unit && <span className="ml-0.5 text-[9px] opacity-60">{row.unit}</span>}
                  </span>
                </div>
              ))}
            </div>
            <p className="text-xs leading-relaxed text-muted-foreground">{labels.description}</p>
            <DialogFooter>
              <Button variant="outline" onClick={onClose} data-slot="kb-hub-reindex-cancel">
                {labels.cancelCta}
              </Button>
              <Button onClick={onConfirm} data-slot="kb-hub-reindex-confirm">
                {labels.reindexCta}
              </Button>
            </DialogFooter>
          </>
        )}

        {phase === 'running' && (
          <div
            data-slot="kb-hub-reindex-running"
            role="status"
            aria-live="polite"
            className="space-y-3 py-2 text-center"
          >
            <div
              aria-hidden="true"
              className="mx-auto h-12 w-12 animate-spin rounded-full border-[3px] border-entity-kb/20 border-t-entity-kb"
            />
            {progress?.jobId && (
              <div className="text-xs text-muted-foreground">
                {labels.jobIdLabel}:{' '}
                <code className="font-mono text-entity-kb">{progress.jobId}</code>
              </div>
            )}
            <div className="h-1.5 overflow-hidden rounded bg-muted">
              <div
                aria-hidden="true"
                className="h-full rounded bg-entity-kb transition-all"
                style={{ width: `${progressPct}%` }}
              />
            </div>
            {progress && (
              <div className="font-mono text-[11px] text-muted-foreground">
                {labels.progressTemplate
                  .replace('{processed}', String(progress.current))
                  .replace('{total}', String(progress.total))}
              </div>
            )}
          </div>
        )}

        {phase === 'done' && (
          <div
            data-slot="kb-hub-reindex-done"
            role="status"
            aria-live="polite"
            className="space-y-3 py-2 text-center"
          >
            <div
              aria-hidden="true"
              className="mx-auto flex h-13 w-13 items-center justify-center rounded-full bg-emerald-500 text-2xl text-white"
              style={{ width: 52, height: 52 }}
            >
              ✓
            </div>
            {summary && (
              <div className="text-xs text-muted-foreground">
                {labels.doneSummaryTemplate
                  .replace('{chunks}', summary.chunks.toLocaleString('it-IT'))
                  .replace('{embeddings}', summary.embeddings.toLocaleString('it-IT'))
                  .replace('{cost}', summary.actualCost)}
              </div>
            )}
            <DialogFooter>
              <Button onClick={onClose} data-slot="kb-hub-reindex-close">
                {labels.closeCta}
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
