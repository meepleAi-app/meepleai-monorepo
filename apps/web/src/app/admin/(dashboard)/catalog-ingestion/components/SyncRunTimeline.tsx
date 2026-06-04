'use client';
import { AlertCircle, ChevronRight } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import { formatDuration, parseTimeSpanToMs } from '../_utils/run-formatter';
import { useCatalogSyncRuns } from '../hooks/use-catalog-sync-runs';

import type { CatalogRunStatus, CatalogSyncRunSummary } from '../lib/catalog-ingestion-api';

interface SyncRunTimelineProps {
  onDrillDown: (runId: string) => void;
}

function statusDotClass(status: CatalogRunStatus): string {
  if (status === 'Failed' || status === 'TimedOut') return 'bg-entity-event';
  if (status === 'Running' || status === 'Queued') return 'bg-entity-kb animate-pulse';
  return 'bg-entity-toolkit';
}

function rowBgClass(status: CatalogRunStatus): string {
  if (status === 'Failed' || status === 'TimedOut') return 'bg-entity-event/[0.04]';
  return '';
}

function successRate(runs: CatalogSyncRunSummary[]): string {
  if (runs.length === 0) return '—';
  const successCount = runs.filter(r => r.status === 'Success').length;
  return `${((successCount / runs.length) * 100).toFixed(1)}%`;
}

export function SyncRunTimeline({ onDrillDown }: SyncRunTimelineProps) {
  const {
    data,
    isLoading,
    isError,
    error,
    refetch,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useCatalogSyncRuns();

  if (isLoading) return <div className="h-40 animate-pulse rounded-xl bg-card/60" />;
  // Issue #1880: distinguish fetch error from empty state (both had `!data` true).
  if (isError) {
    return (
      <section
        role="alert"
        className="overflow-hidden rounded-xl border border-entity-event/30 bg-card"
      >
        <header className="flex items-center gap-2.5 border-b border-border bg-muted/30 px-3.5 py-2.5">
          <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">
            Sync history
          </h3>
        </header>
        <div className="flex items-center gap-3 px-4 py-6">
          <AlertCircle className="h-4 w-4 shrink-0 text-entity-event" aria-hidden />
          <p className="flex-1 text-sm text-entity-event">
            Impossibile caricare la timeline (
            {error instanceof Error ? error.message : 'errore di rete'})
          </p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            Riprova
          </Button>
        </div>
      </section>
    );
  }

  // Issue #1881: flatten pages from useInfiniteQuery into a single render list.
  const items: CatalogSyncRunSummary[] = data?.pages.flatMap(p => p.items) ?? [];

  if (items.length === 0) {
    return (
      <section className="overflow-hidden rounded-xl border border-border bg-card">
        <header className="flex items-center gap-2.5 border-b border-border bg-muted/30 px-3.5 py-2.5">
          <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">
            Sync history
          </h3>
        </header>
        <div className="px-4 py-8 text-center text-sm text-muted-foreground">
          Nessun run registrato.
        </div>
      </section>
    );
  }

  return (
    <section className="overflow-hidden rounded-xl border border-border bg-card">
      <header className="flex items-center gap-2.5 border-b border-border bg-muted/30 px-3.5 py-2.5">
        <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">
          Sync history · ultime {items.length} run
        </h3>
        <span className="ml-auto font-mono text-[10px] text-muted-foreground">
          success rate {successRate(items)}
        </span>
      </header>
      <div>
        {/* Column header sub-row */}
        <div className="grid grid-cols-[32px_1fr_90px_60px_60px_60px_24px] gap-3 border-b border-border bg-muted/30 px-3.5 py-2 font-mono text-[9.5px] font-bold uppercase tracking-wider text-muted-foreground">
          <div />
          <div>Run</div>
          <div className="text-right">Durata</div>
          <div className="text-right">+add</div>
          <div className="text-right">~upd</div>
          <div className="text-right">×fail</div>
          <div />
        </div>
        {items.map(run => (
          <div
            key={run.id}
            data-testid="run-row"
            className={`grid grid-cols-[32px_1fr_90px_60px_60px_60px_24px] gap-3 border-b border-border/70 px-3.5 py-3 text-xs last:border-b-0 ${rowBgClass(run.status)}`}
          >
            {/* Status dot */}
            <div className="flex items-center">
              <span className={`h-2.5 w-2.5 rounded-full ${statusDotClass(run.status)}`} />
            </div>

            {/* Title + meta */}
            <div>
              <div className="font-quicksand font-bold text-foreground">{run.title}</div>
              <div className="mt-0.5 font-mono text-[10px] text-muted-foreground">
                {run.startedAt ? new Date(run.startedAt).toLocaleString('it-IT') : '—'}
                {run.triggeredByUserId === null ? ' · cron' : ' · by user'}
                {run.errorCode && ` · ${run.errorCode}`}
              </div>
            </div>

            {/* Duration */}
            <div className="text-right font-mono font-bold text-foreground">
              {run.duration !== null ? formatDuration(parseTimeSpanToMs(run.duration) ?? 0) : '—'}
            </div>

            {/* Items added */}
            <div className="text-right font-mono font-bold text-entity-toolkit">
              +{run.itemsAdded}
            </div>

            {/* Items updated */}
            <div className="text-right font-mono font-bold text-entity-chat">
              ~{run.itemsUpdated}
            </div>

            {/* Items failed */}
            <div className="text-right font-mono font-bold text-entity-event">
              {run.itemsFailed}
            </div>

            {/* Drill-down button */}
            <button
              onClick={() => onDrillDown(run.id)}
              aria-label={`Open logs for run ${run.id}`}
              className="text-muted-foreground hover:text-foreground"
            >
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        ))}
      </div>

      {/* Issue #1881: Load more footer (append next page on click) */}
      {hasNextPage && (
        <div className="flex items-center justify-center border-t border-border bg-muted/20 px-3.5 py-2.5">
          <Button
            variant="outline"
            size="sm"
            onClick={() => fetchNextPage()}
            disabled={isFetchingNextPage}
          >
            {isFetchingNextPage ? 'Loading…' : 'Load more'}
          </Button>
        </div>
      )}
    </section>
  );
}
