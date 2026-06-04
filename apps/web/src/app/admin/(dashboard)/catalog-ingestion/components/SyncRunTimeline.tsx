'use client';
import { ChevronRight } from 'lucide-react';

import { Card, CardHeader, CardTitle } from '@/components/ui/data-display/card';

import { formatDuration, parseTimeSpanToMs } from '../_utils/run-formatter';
import { useCatalogSyncRuns } from '../hooks/use-catalog-sync-runs';

import type { CatalogRunStatus, CatalogSyncRunSummary } from '../lib/catalog-ingestion-api';

interface SyncRunTimelineProps {
  onDrillDown: (runId: string) => void;
}

function statusDotClass(status: CatalogRunStatus): string {
  if (status === 'Failed' || status === 'TimedOut') return 'bg-event';
  if (status === 'Running' || status === 'Queued') return 'bg-kb animate-pulse';
  return 'bg-toolkit';
}

function rowBgClass(status: CatalogRunStatus): string {
  if (status === 'Failed' || status === 'TimedOut') return 'bg-event/[0.04]';
  return '';
}

function successRate(runs: CatalogSyncRunSummary[]): string {
  if (runs.length === 0) return '—';
  const successCount = runs.filter(r => r.status === 'Success').length;
  return `${((successCount / runs.length) * 100).toFixed(1)}%`;
}

export function SyncRunTimeline({ onDrillDown }: SyncRunTimelineProps) {
  const { data, isLoading } = useCatalogSyncRuns();

  if (isLoading) return <Card className="h-40 animate-pulse" />;
  if (!data || data.items.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Sync history</CardTitle>
        </CardHeader>
        <div className="px-4 py-8 text-center text-sm text-muted-foreground">
          Nessun run registrato.
        </div>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Sync history · ultime {data.items.length} run</CardTitle>
        <span className="font-mono text-xs text-muted-foreground">
          success rate {successRate(data.items)}
        </span>
      </CardHeader>
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
        {data.items.map(run => (
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
            <div className="text-right font-mono font-bold text-toolkit">+{run.itemsAdded}</div>

            {/* Items updated */}
            <div className="text-right font-mono font-bold text-chat">~{run.itemsUpdated}</div>

            {/* Items failed */}
            <div className="text-right font-mono font-bold text-event">{run.itemsFailed}</div>

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
    </Card>
  );
}
