'use client';

import { useQuery } from '@tanstack/react-query';
import { ActivityIcon, AlertTriangleIcon, ClockIcon, HashIcon, RefreshCwIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useApiClient } from '@/lib/api/context';

interface StepAverages {
  step: string;
  avgDuration: number;
  sampleSize: number;
}

interface StepPercentiles {
  p50: number;
  p95: number;
  p99: number;
}

interface ProcessingMetricsData {
  averages: Record<string, StepAverages>;
  percentiles: Record<string, StepPercentiles>;
  lastUpdated: string;
}

/** P95 threshold in seconds for amber coloring */
const AMBER_THRESHOLD = 30;
/** P95 threshold in seconds for red coloring */
const RED_THRESHOLD = 120;

function formatDuration(seconds: number): string {
  if (seconds === 0) return '—';
  if (seconds < 1) return `${Math.round(seconds * 1000)}ms`;
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  return `${(seconds / 60).toFixed(1)}m`;
}

function durationColor(seconds: number): string {
  if (seconds >= RED_THRESHOLD) return 'text-entity-event';
  if (seconds >= AMBER_THRESHOLD) return 'text-amber-600';
  return 'text-entity-toolkit';
}

function barColor(seconds: number): string {
  if (seconds >= RED_THRESHOLD) return 'bg-entity-event';
  if (seconds >= AMBER_THRESHOLD) return 'bg-amber-500';
  return 'bg-entity-toolkit';
}

export function ProcessingMetrics() {
  const apiClient = useApiClient();
  const {
    data: metrics,
    isLoading,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'processing', 'metrics'],
    queryFn: () => apiClient.admin.getProcessingMetrics() as Promise<ProcessingMetricsData | null>,
    staleTime: 60_000,
    refetchInterval: 60_000,
  });

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 bg-card/40 rounded-[10px] animate-pulse" />
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-48 bg-card/40 rounded-[10px] animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  if (!metrics) {
    return (
      <section className="rounded-[10px] border border-border/60 bg-card p-6">
        <p className="text-sm text-muted-foreground">Nessuna metrica di elaborazione disponibile</p>
      </section>
    );
  }

  const stepNames = Object.keys(metrics.averages);
  const allP95 = stepNames.map(s => metrics.percentiles[s]?.p95 ?? 0);
  const maxP95 = Math.max(...allP95, 1);
  const bottleneckStep = stepNames[allP95.indexOf(maxP95)];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <h2 className="font-quicksand text-xl font-bold text-foreground flex items-center gap-2">
          <ActivityIcon className="h-5 w-5 text-entity-kb" />
          Metriche Elaborazione
        </h2>
        <div className="flex items-center gap-3">
          <time
            className="text-xs text-muted-foreground font-mono"
            dateTime={metrics.lastUpdated}
            suppressHydrationWarning
          >
            Aggiornato {new Date(metrics.lastUpdated).toLocaleTimeString()}
          </time>
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={isRefetching}
            className="gap-2"
          >
            <RefreshCwIcon className={`h-4 w-4 ${isRefetching ? 'animate-spin' : ''}`} />
            Aggiorna
          </Button>
        </div>
      </div>

      {/* Step Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {stepNames.map(stepName => {
          const avg = metrics.averages[stepName];
          const pct = metrics.percentiles[stepName];
          const isBottleneck = stepName === bottleneckStep && (pct?.p95 ?? 0) > 0;

          return (
            <StepMetricCard
              key={stepName}
              stepName={avg?.step ?? stepName}
              avgDuration={avg?.avgDuration ?? 0}
              sampleSize={avg?.sampleSize ?? 0}
              p50={pct?.p50 ?? 0}
              p95={pct?.p95 ?? 0}
              p99={pct?.p99 ?? 0}
              maxP95={maxP95}
              isBottleneck={isBottleneck}
            />
          );
        })}
      </div>

      {/* Comparison Table */}
      <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border/60 bg-muted">
              <th className="text-left px-4 py-3 font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
                Fase
              </th>
              <th className="text-right px-4 py-3 font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
                Media
              </th>
              <th className="text-right px-4 py-3 font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
                P50
              </th>
              <th className="text-right px-4 py-3 font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
                P95
              </th>
              <th className="text-right px-4 py-3 font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
                P99
              </th>
              <th className="text-right px-4 py-3 font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
                Campioni
              </th>
            </tr>
          </thead>
          <tbody>
            {stepNames.map(stepName => {
              const avg = metrics.averages[stepName];
              const pct = metrics.percentiles[stepName];
              const isBottleneck = stepName === bottleneckStep && (pct?.p95 ?? 0) > 0;
              return (
                <tr
                  key={stepName}
                  className={`border-b border-border/60 ${isBottleneck ? 'bg-amber-500/6' : ''}`}
                >
                  <td className="px-4 py-3 font-medium text-foreground flex items-center gap-2">
                    {isBottleneck && <AlertTriangleIcon className="h-3.5 w-3.5 text-amber-500" />}
                    {avg?.step ?? stepName}
                  </td>
                  <td
                    className={`text-right px-4 py-3 font-mono ${durationColor(avg?.avgDuration ?? 0)}`}
                  >
                    {formatDuration(avg?.avgDuration ?? 0)}
                  </td>
                  <td className={`text-right px-4 py-3 font-mono ${durationColor(pct?.p50 ?? 0)}`}>
                    {formatDuration(pct?.p50 ?? 0)}
                  </td>
                  <td className={`text-right px-4 py-3 font-mono ${durationColor(pct?.p95 ?? 0)}`}>
                    {formatDuration(pct?.p95 ?? 0)}
                  </td>
                  <td className={`text-right px-4 py-3 font-mono ${durationColor(pct?.p99 ?? 0)}`}>
                    {formatDuration(pct?.p99 ?? 0)}
                  </td>
                  <td className="text-right px-4 py-3 font-mono text-muted-foreground">
                    {avg?.sampleSize ?? 0}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </section>
    </div>
  );
}

function StepMetricCard({
  stepName,
  avgDuration,
  sampleSize,
  p50,
  p95,
  p99,
  maxP95,
  isBottleneck,
}: {
  stepName: string;
  avgDuration: number;
  sampleSize: number;
  p50: number;
  p95: number;
  p99: number;
  maxP95: number;
  isBottleneck: boolean;
}) {
  return (
    <div
      className={`rounded-[10px] p-5 border ${
        isBottleneck
          ? 'border-amber-500/40 ring-1 ring-amber-500/20 bg-amber-500/6'
          : 'border-border/60 bg-card'
      }`}
    >
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <h3 className="font-quicksand font-semibold text-foreground">{stepName}</h3>
          {isBottleneck && (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full font-mono text-[10px] font-bold bg-amber-500/14 text-amber-600">
              <AlertTriangleIcon className="h-3 w-3" />
              Collo di bottiglia
            </span>
          )}
        </div>
        <div className="flex items-center gap-1 font-mono text-xs text-muted-foreground">
          <HashIcon className="h-3 w-3" />
          {sampleSize} campioni
        </div>
      </div>

      {/* Average Duration */}
      <div className="flex items-center gap-2 mb-4">
        <ClockIcon className="h-4 w-4 text-muted-foreground" />
        <span className="text-sm text-muted-foreground">Avg:</span>
        <span className={`text-lg font-bold font-mono ${durationColor(avgDuration)}`}>
          {formatDuration(avgDuration)}
        </span>
      </div>

      {/* Percentile Bars */}
      <div className="space-y-2">
        <PercentileBar label="P50" value={p50} max={maxP95} />
        <PercentileBar label="P95" value={p95} max={maxP95} />
        <PercentileBar label="P99" value={p99} max={maxP95} />
      </div>
    </div>
  );
}

function PercentileBar({ label, value, max }: { label: string; value: number; max: number }) {
  const widthPct = max > 0 ? Math.min((value / max) * 100, 100) : 0;

  return (
    <div className="flex items-center gap-2">
      <span className="text-[10px] font-mono text-muted-foreground w-6">{label}</span>
      <div className="flex-1 h-2 bg-muted rounded-full overflow-hidden">
        <div
          className={`h-full rounded-full transition-all ${barColor(value)}`}
          style={{ width: `${widthPct}%` }}
        />
      </div>
      <span className={`text-[10px] font-mono w-12 text-right ${durationColor(value)}`}>
        {formatDuration(value)}
      </span>
    </div>
  );
}
