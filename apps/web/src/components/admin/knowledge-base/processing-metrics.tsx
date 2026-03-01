'use client';

import { useQuery } from '@tanstack/react-query';
import {
  ActivityIcon,
  AlertTriangleIcon,
  ClockIcon,
  HashIcon,
  RefreshCwIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

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

interface ProcessingMetrics {
  averages: Record<string, StepAverages>;
  percentiles: Record<string, StepPercentiles>;
  lastUpdated: string;
}

/** P95 threshold in seconds for amber coloring */
const AMBER_THRESHOLD = 30;
/** P95 threshold in seconds for red coloring */
const RED_THRESHOLD = 120;

function formatDuration(seconds: number): string {
  if (seconds === 0) return '\u2014';
  if (seconds < 1) return `${Math.round(seconds * 1000)}ms`;
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  return `${(seconds / 60).toFixed(1)}m`;
}

function durationColor(seconds: number): string {
  if (seconds >= RED_THRESHOLD) return 'text-red-600 dark:text-red-400';
  if (seconds >= AMBER_THRESHOLD) return 'text-amber-600 dark:text-amber-400';
  return 'text-green-600 dark:text-green-400';
}

function barColor(seconds: number): string {
  if (seconds >= RED_THRESHOLD) return 'bg-red-500';
  if (seconds >= AMBER_THRESHOLD) return 'bg-amber-500';
  return 'bg-green-500';
}

export function ProcessingMetrics() {
  const {
    data: metrics,
    isLoading,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'processing', 'metrics'],
    queryFn: () => adminClient.getProcessingMetrics() as Promise<ProcessingMetrics | null>,
    staleTime: 60_000,
    refetchInterval: 60_000,
  });

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 bg-white/40 dark:bg-zinc-800/40 rounded animate-pulse" />
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-48 bg-white/40 dark:bg-zinc-800/40 rounded-lg animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  if (!metrics) {
    return (
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        <p className="text-sm text-slate-600 dark:text-zinc-400">No processing metrics available</p>
      </div>
    );
  }

  const stepNames = Object.keys(metrics.averages);
  const allP95 = stepNames.map((s) => metrics.percentiles[s]?.p95 ?? 0);
  const maxP95 = Math.max(...allP95, 1);
  const bottleneckStep = stepNames[allP95.indexOf(maxP95)];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 flex items-center gap-2">
          <ActivityIcon className="h-5 w-5 text-blue-500" />
          Processing Step Metrics
        </h2>
        <div className="flex items-center gap-3">
          <span className="text-xs text-slate-500 dark:text-zinc-500">
            Updated {new Date(metrics.lastUpdated).toLocaleTimeString()}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={isRefetching}
            className="gap-2"
          >
            <RefreshCwIcon className={`h-4 w-4 ${isRefetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* Step Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {stepNames.map((stepName) => {
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
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50 overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200 dark:border-zinc-700">
              <th className="text-left px-4 py-3 font-medium text-slate-600 dark:text-zinc-400">Step</th>
              <th className="text-right px-4 py-3 font-medium text-slate-600 dark:text-zinc-400">Avg</th>
              <th className="text-right px-4 py-3 font-medium text-slate-600 dark:text-zinc-400">P50</th>
              <th className="text-right px-4 py-3 font-medium text-slate-600 dark:text-zinc-400">P95</th>
              <th className="text-right px-4 py-3 font-medium text-slate-600 dark:text-zinc-400">P99</th>
              <th className="text-right px-4 py-3 font-medium text-slate-600 dark:text-zinc-400">Samples</th>
            </tr>
          </thead>
          <tbody>
            {stepNames.map((stepName) => {
              const avg = metrics.averages[stepName];
              const pct = metrics.percentiles[stepName];
              const isBottleneck = stepName === bottleneckStep && (pct?.p95 ?? 0) > 0;
              return (
                <tr
                  key={stepName}
                  className={`border-b border-slate-100 dark:border-zinc-800 ${
                    isBottleneck ? 'bg-amber-50/50 dark:bg-amber-900/10' : ''
                  }`}
                >
                  <td className="px-4 py-3 font-medium text-slate-900 dark:text-zinc-100 flex items-center gap-2">
                    {isBottleneck && <AlertTriangleIcon className="h-3.5 w-3.5 text-amber-500" />}
                    {avg?.step ?? stepName}
                  </td>
                  <td className={`text-right px-4 py-3 font-mono ${durationColor(avg?.avgDuration ?? 0)}`}>
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
                  <td className="text-right px-4 py-3 text-slate-500 dark:text-zinc-500">
                    {avg?.sampleSize ?? 0}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
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
      className={`bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-5 border ${
        isBottleneck
          ? 'border-amber-300 dark:border-amber-700 ring-1 ring-amber-200 dark:ring-amber-800'
          : 'border-slate-200/50 dark:border-zinc-700/50'
      }`}
    >
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <h3 className="font-semibold text-slate-900 dark:text-zinc-100">{stepName}</h3>
          {isBottleneck && (
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-medium bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300">
              <AlertTriangleIcon className="h-3 w-3" />
              Bottleneck
            </span>
          )}
        </div>
        <div className="flex items-center gap-1 text-xs text-slate-500 dark:text-zinc-500">
          <HashIcon className="h-3 w-3" />
          {sampleSize} samples
        </div>
      </div>

      {/* Average Duration */}
      <div className="flex items-center gap-2 mb-4">
        <ClockIcon className="h-4 w-4 text-slate-400" />
        <span className="text-sm text-slate-600 dark:text-zinc-400">Avg:</span>
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
      <span className="text-[10px] font-mono text-slate-500 dark:text-zinc-500 w-6">{label}</span>
      <div className="flex-1 h-2 bg-slate-100 dark:bg-zinc-700 rounded-full overflow-hidden">
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
