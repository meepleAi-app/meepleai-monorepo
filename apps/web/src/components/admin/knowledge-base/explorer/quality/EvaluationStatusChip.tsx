/**
 * EvaluationStatusChip — small pill rendering the EvaluationStatus enum
 * with an IT label + colour treatment matching the lifecycle phase:
 * amber for in-flight (Pending/GoldsetGenerating/Running), emerald for
 * Completed, rose for any failure state (Failed/RateLimited/CostCapped).
 */

import type { JSX } from 'react';

import type { EvaluationStatus } from '@/lib/api/schemas/kb-quality.schemas';

interface StatusStyle {
  label: string;
  cls: string;
}

const STYLE: Record<EvaluationStatus, StatusStyle> = {
  Pending: {
    label: 'In coda',
    cls: 'bg-amber-500/10 text-amber-700 border-amber-500/30 dark:text-amber-300',
  },
  GoldsetGenerating: {
    label: 'Generazione goldset',
    cls: 'bg-amber-500/10 text-amber-700 border-amber-500/30 dark:text-amber-300',
  },
  Running: {
    label: 'In esecuzione',
    cls: 'bg-amber-500/10 text-amber-700 border-amber-500/30 dark:text-amber-300',
  },
  Completed: {
    label: 'Completato',
    cls: 'bg-emerald-500/10 text-emerald-700 border-emerald-500/30 dark:text-emerald-300',
  },
  Failed: {
    label: 'Fallito',
    cls: 'bg-rose-500/10 text-rose-700 border-rose-500/30 dark:text-rose-300',
  },
  RateLimited: {
    label: 'Rate limited',
    cls: 'bg-rose-500/10 text-rose-700 border-rose-500/30 dark:text-rose-300',
  },
  CostCapped: {
    label: 'Cost cap',
    cls: 'bg-rose-500/10 text-rose-700 border-rose-500/30 dark:text-rose-300',
  },
};

export interface EvaluationStatusChipProps {
  readonly status: EvaluationStatus;
}

export function EvaluationStatusChip({ status }: EvaluationStatusChipProps): JSX.Element {
  const s = STYLE[status];
  return (
    <span
      data-testid={`eval-status-chip-${status.toLowerCase()}`}
      className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-semibold ${s.cls}`}
    >
      {s.label}
    </span>
  );
}
