'use client';
import { useState } from 'react';

import { Loader2, Play } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';

import { formatRelativeTime } from '../_utils/run-formatter';
import { chipPresentation, deriveChipState, type LastRunStatus } from '../_utils/status-mapper';
import { useCatalogSyncStatus } from '../hooks/use-catalog-sync-status';
import { type CatalogSyncProvider, triggerCatalogSync } from '../lib/catalog-ingestion-api';

/** Italian thousands separator using dot — locale-safe in JSDOM test environments */
function formatItNumber(n: number): string {
  const s = String(Math.trunc(n));
  let out = '';
  for (let i = 0; i < s.length; i++) {
    if (i > 0 && (s.length - i) % 3 === 0) out += '.';
    out += s[i];
  }
  return out;
}

interface SyncStatusHeroProps {
  onOpenCsvModal?: () => void;
  onOpenManualModal?: () => void;
}

export function SyncStatusHero({ onOpenCsvModal, onOpenManualModal }: SyncStatusHeroProps) {
  const { data } = useCatalogSyncStatus();
  const [provider, setProvider] = useState<CatalogSyncProvider>('BggApi');
  const [batchSize, setBatchSize] = useState('100');
  const [rateLimit, setRateLimit] = useState('60/min');
  const [autoRetry, setAutoRetry] = useState(true);
  const [isTriggering, setIsTriggering] = useState(false);

  if (!data) return <div className="h-40 animate-pulse rounded-xl bg-card/60" />;

  // CatalogRunStatus includes 'Queued'/'Running' which are non-terminal; map those to null for chip derivation
  const rawLastStatus = data.lastRun?.status ?? null;
  const lastRunStatus: LastRunStatus =
    rawLastStatus === 'Success' || rawLastStatus === 'Failed' || rawLastStatus === 'TimedOut'
      ? rawLastStatus
      : null;
  const chipState = deriveChipState(data.status, lastRunStatus);
  const chip = chipPresentation[chipState];
  const isRunning = data.status === 'running';
  const showBggConfig = provider === 'BggApi';
  // PLAN AMENDMENT: derive activeProvider from currentRun/lastRun (no DTO field)
  const activeProvider = data.currentRun?.provider ?? data.lastRun?.provider ?? null;

  const handleRunSyncNow = async () => {
    if (provider === 'CsvImport') {
      onOpenCsvModal?.();
      return;
    }
    if (provider === 'Manual') {
      onOpenManualModal?.();
      return;
    }
    setIsTriggering(true);
    try {
      await triggerCatalogSync('BggApi');
      toast.success('Sync queued');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Trigger failed';
      toast.error(message);
    } finally {
      setIsTriggering(false);
    }
  };

  return (
    <div className="rounded-xl border border-toolkit/25 bg-gradient-to-br from-toolkit/[0.14] to-entity-game/[0.08] px-6 py-5">
      <div className="grid items-center gap-6 md:grid-cols-[1fr_320px]">
        {/* Left: status + stats */}
        <div>
          <div className="flex items-center gap-2.5">
            <h2 className="font-quicksand text-[22px] font-extrabold leading-none text-foreground">
              🔄 BGG Catalog Sync
            </h2>
            <span
              role="status"
              aria-live="polite"
              className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ${chip.toneClass} ${isRunning ? 'animate-pulse' : ''}`}
            >
              {chip.label}
            </span>
          </div>
          {chipState === 'degraded' && data.lastRun?.errorCode && (
            <div className="mt-1 inline-flex items-center rounded bg-event/10 px-2 py-0.5 font-mono text-[11px] text-event">
              {data.lastRun.errorCode}
            </div>
          )}
          <p className="mt-1.5 text-sm text-muted-foreground">
            Sincronizzazione automatica da BoardGameGeek API. Cron schedule ogni 6h.
          </p>
          <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 font-mono text-[11px] text-muted-foreground">
            <span>
              Ultima sync:{' '}
              <span className="font-bold text-foreground">
                {data.lastRun
                  ? formatRelativeTime(
                      data.lastRun.completedAt ?? data.lastRun.startedAt ?? data.lastRun.createdAt
                    )
                  : 'Mai eseguita'}
              </span>
            </span>
            <span>
              Giochi importati totali:{' '}
              <span className="font-bold text-foreground">
                {formatItNumber(data.cumulative.gamesTotal)}
              </span>
            </span>
            {data.nextScheduled && (
              <span>
                Next scheduled:{' '}
                <span className="font-bold text-foreground">{data.nextScheduled}</span>
              </span>
            )}
            {activeProvider && (
              <span>
                Provider: <span className="font-bold text-foreground">{activeProvider}</span>
              </span>
            )}
          </div>
        </div>

        {/* Right: provider + config + Run sync now */}
        <div className="flex flex-col gap-2 rounded-md bg-card p-3">
          <label className="flex items-center gap-2 text-xs">
            <span className="min-w-[88px] font-mono text-[10px] uppercase text-muted-foreground">
              Provider
            </span>
            <select
              aria-label="Provider"
              value={provider}
              onChange={e => setProvider(e.target.value as CatalogSyncProvider)}
              className="flex-1 rounded border border-border bg-background px-2 py-1 text-xs"
            >
              <option value="BggApi">BGG API v2</option>
              <option value="CsvImport">CSV import</option>
              <option value="Manual">Manual</option>
            </select>
          </label>
          {showBggConfig && (
            <>
              <label className="flex items-center gap-2 text-xs">
                <span className="min-w-[88px] font-mono text-[10px] uppercase text-muted-foreground">
                  Batch size
                </span>
                <input
                  aria-label="Batch size"
                  className="max-w-[80px] rounded border border-border bg-background px-2 py-1 font-mono text-xs"
                  value={batchSize}
                  onChange={e => setBatchSize(e.target.value)}
                />
              </label>
              <label className="flex items-center gap-2 text-xs">
                <span className="min-w-[88px] font-mono text-[10px] uppercase text-muted-foreground">
                  Rate limit
                </span>
                <input
                  aria-label="Rate limit"
                  className="max-w-[80px] rounded border border-border bg-background px-2 py-1 font-mono text-xs"
                  value={rateLimit}
                  onChange={e => setRateLimit(e.target.value)}
                />
              </label>
              <label className="flex items-center gap-2 text-xs">
                <span className="min-w-[88px] font-mono text-[10px] uppercase text-muted-foreground">
                  Auto-retry
                </span>
                <input
                  aria-label="Auto-retry"
                  type="checkbox"
                  checked={autoRetry}
                  onChange={e => setAutoRetry(e.target.checked)}
                />
              </label>
            </>
          )}
          <Button
            onClick={handleRunSyncNow}
            disabled={isRunning || isTriggering}
            title={isRunning ? 'Sync già in corso' : undefined}
            className="mt-1.5"
          >
            {isTriggering ? (
              <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" />
            ) : (
              <Play className="mr-1 h-3.5 w-3.5" />
            )}
            Run sync now
          </Button>
        </div>
      </div>
    </div>
  );
}
