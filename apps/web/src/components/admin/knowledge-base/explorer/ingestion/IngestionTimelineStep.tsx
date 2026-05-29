'use client';

import type { IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

interface IngestionTimelineStepProps {
  readonly step: IngestionStep;
  readonly index: number;
  readonly isLast: boolean;
}

const STEP_LABELS: Record<IngestionStep['stepName'], string> = {
  Upload: 'Upload & validate',
  Extract: 'Estrazione testo',
  Chunk: 'Chunking',
  Embed: 'Embedding',
  Index: 'Indexing pgvector',
};

const STEP_SUBS: Record<IngestionStep['stepName'], string> = {
  Upload: 'PDF integrity check · MIME · size',
  Extract: 'Unstructured / SmolDocling / Docnet',
  Chunk: 'Sentence-based, target 512 tok',
  Embed: 'Vector embedding generation',
  Index: 'HNSW index, commit tx',
};

function chipForStatus(status: string): { wrapper: string; icon: string } {
  const s = status.toLowerCase();
  if (s === 'done' || s === 'completed' || s === 'succeeded') {
    return { wrapper: 'bg-emerald-500/20 text-emerald-700 dark:text-emerald-300', icon: '✓' };
  }
  if (s === 'running' || s === 'processing') {
    return {
      wrapper: 'bg-amber-500/30 text-amber-800 dark:text-amber-200 animate-pulse',
      icon: '▸',
    };
  }
  if (s === 'failed' || s === 'errored') {
    return { wrapper: 'bg-rose-500/20 text-rose-700 dark:text-rose-300', icon: '✕' };
  }
  return { wrapper: 'bg-muted text-muted-foreground', icon: '·' };
}

function formatDuration(ms: number | null, startedAt: string | null): string {
  if (ms !== null) return `+ ${(ms / 1000).toFixed(2)}s`;
  if (startedAt !== null) return 'in corso';
  return 'in attesa';
}

export function IngestionTimelineStep({ step, index: _index, isLast }: IngestionTimelineStepProps) {
  const chip = chipForStatus(step.status);
  return (
    <li
      data-testid={`ingestion-step-${step.stepName.toLowerCase()}`}
      data-step-status={step.status.toLowerCase()}
      className="relative grid grid-cols-[28px_1fr_auto] gap-2.5 items-center py-2 px-1"
    >
      {!isLast && (
        <span
          aria-hidden="true"
          className="absolute left-[13px] top-[28px] bottom-[-8px] w-0.5 bg-border/60"
        />
      )}
      <span
        aria-hidden="true"
        className={`relative z-10 size-6 rounded-full grid place-items-center font-mono text-[11px] font-bold border-2 border-card ${chip.wrapper}`}
      >
        {chip.icon}
      </span>
      <span className="min-w-0">
        <span className="block font-quicksand text-[12.5px] font-bold truncate">
          {STEP_LABELS[step.stepName]}
        </span>
        <span className="block font-mono text-[10px] font-medium text-muted-foreground truncate">
          {STEP_SUBS[step.stepName]}
        </span>
      </span>
      <span className="font-mono text-[10.5px] text-muted-foreground whitespace-nowrap">
        {formatDuration(step.durationMs, step.startedAt)}
      </span>
    </li>
  );
}
