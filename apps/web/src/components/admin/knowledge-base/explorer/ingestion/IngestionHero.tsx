'use client';

import { estimateCost } from '@/lib/admin-kb/calcCost';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

interface IngestionHeroProps {
  readonly log: IngestionLog;
  readonly chunkCount: number;
  readonly pageCount: number;
  readonly embeddingModel?: string;
}

function statusChipClass(status: string): string {
  const s = status.toLowerCase();
  if (s === 'completed' || s === 'done')
    return 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300';
  if (s === 'running' || s === 'processing')
    return 'bg-amber-500/15 text-amber-700 dark:text-amber-300 animate-pulse';
  if (s === 'failed') return 'bg-rose-500/15 text-rose-700 dark:text-rose-300';
  return 'bg-muted text-muted-foreground';
}

function deriveProgressPct(log: IngestionLog): number {
  if (log.status === 'Completed') return 100;
  if (log.status === 'Failed') return 0;
  const done = log.steps.filter(s => s.status.toLowerCase() === 'done').length;
  return Math.round((done / 5) * 100);
}

function formatCost(value: number): string {
  if (value === 0) return '$0.00';
  if (value < 0.001) return `≈ $${value.toFixed(6)}`;
  return `≈ $${value.toFixed(4)}`;
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="bg-card/60 border border-border/40 rounded-md px-3 py-2">
      <dt className="font-mono text-[9.5px] uppercase tracking-wider text-muted-foreground">
        {label}
      </dt>
      <dd className="font-quicksand text-[15px] font-bold">{value}</dd>
      {hint && <dd className="font-mono text-[9px] text-muted-foreground mt-0.5">{hint}</dd>}
    </div>
  );
}

export function IngestionHero({
  log,
  chunkCount,
  pageCount,
  embeddingModel = 'bge-base-en-v1.5',
}: IngestionHeroProps) {
  const cost = estimateCost(chunkCount, embeddingModel);
  const progress = deriveProgressPct(log);

  return (
    <header className="p-4 border-b border-border/60 bg-gradient-to-b from-amber-500/5 to-transparent">
      <div className="flex items-start gap-3">
        <span aria-hidden="true" className="text-3xl shrink-0">
          📄
        </span>
        <div className="flex-1 min-w-0">
          <h3 className="font-quicksand font-bold text-base truncate">{log.pdfFileName}</h3>
          <div className="mt-1 flex flex-wrap items-center gap-1.5 font-mono text-[10px] text-muted-foreground">
            <span>job {log.id.slice(0, 8)}</span>
            <span aria-hidden="true">·</span>
            <span>started {log.startedAt ?? '—'}</span>
            {log.retryCount > 0 && (
              <>
                <span aria-hidden="true">·</span>
                <span data-testid="ingestion-retry-counter">
                  retry {log.retryCount}/{log.maxRetries}
                </span>
              </>
            )}
            <span
              className={`ml-auto inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full ${statusChipClass(log.status)}`}
            >
              {log.status}
            </span>
          </div>
        </div>
      </div>

      <dl className="mt-3 grid grid-cols-4 gap-2">
        <Stat label="Progresso" value={`${progress}%`} />
        <Stat label="Chunks" value={chunkCount.toLocaleString('it-IT')} />
        <Stat label="Pages" value={pageCount === 0 ? '—' : pageCount.toLocaleString('it-IT')} />
        <Stat
          label="Cost"
          value={cost ? formatCost(cost.value) : '—'}
          hint={cost?.formula ?? 'modello sconosciuto'}
        />
      </dl>
    </header>
  );
}
