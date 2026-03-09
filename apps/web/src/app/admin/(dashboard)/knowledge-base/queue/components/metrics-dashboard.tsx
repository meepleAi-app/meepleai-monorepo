'use client';

import { useState } from 'react';

import {
  ActivityIcon,
  CheckCircle2Icon,
  XCircleIcon,
  ClockIcon,
  BarChart3Icon,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { StatCard } from '@/components/ui/data-display/stat-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

import { useDashboardMetrics } from '../lib/queue-api';

import type { MetricsPeriod, PhaseTimingDto } from '../lib/queue-api';

const PERIOD_OPTIONS: { value: MetricsPeriod; label: string }[] = [
  { value: '24h', label: 'Last 24h' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
];

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const minutes = Math.floor(seconds / 60);
  const remainingSecs = seconds % 60;
  return `${minutes}m ${remainingSecs.toFixed(0)}s`;
}

function PhaseTimingBar({ phase, maxDuration }: { phase: PhaseTimingDto; maxDuration: number }) {
  const widthPercent = maxDuration > 0 ? (phase.avgDurationSeconds / maxDuration) * 100 : 0;

  return (
    <div className="space-y-1" data-testid={`phase-timing-${phase.phase}`}>
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium text-foreground truncate mr-2">{phase.phase}</span>
        <span className="text-muted-foreground shrink-0">
          avg {formatDuration(phase.avgDurationSeconds)} ({phase.sampleCount} samples)
        </span>
      </div>
      <div className="h-6 bg-slate-100 dark:bg-zinc-700/50 rounded-md overflow-hidden relative">
        {/* Min-max range bar */}
        <div
          className="absolute top-0 h-full bg-blue-100 dark:bg-blue-900/30 rounded-md"
          style={{
            left: `${maxDuration > 0 ? (phase.minDurationSeconds / maxDuration) * 100 : 0}%`,
            width: `${maxDuration > 0 ? ((phase.maxDurationSeconds - phase.minDurationSeconds) / maxDuration) * 100 : 0}%`,
          }}
        />
        {/* Average bar */}
        <div
          className="absolute top-0 h-full bg-blue-500 dark:bg-blue-400 rounded-md transition-all duration-500"
          style={{ width: `${widthPercent}%` }}
        />
        {/* Label overlay */}
        <div className="absolute inset-0 flex items-center px-2">
          <span className="text-xs font-medium text-white drop-shadow-sm">
            {widthPercent > 15 ? formatDuration(phase.avgDurationSeconds) : ''}
          </span>
        </div>
      </div>
      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <span>min: {formatDuration(phase.minDurationSeconds)}</span>
        <span>max: {formatDuration(phase.maxDurationSeconds)}</span>
      </div>
    </div>
  );
}

export function MetricsDashboard() {
  const [period, setPeriod] = useState<MetricsPeriod>('24h');
  const { data: metrics, isLoading } = useDashboardMetrics(period);

  const maxDuration = metrics
    ? Math.max(...metrics.phaseTimings.map(p => p.maxDurationSeconds), 1)
    : 1;

  return (
    <div className="space-y-4" data-testid="metrics-dashboard">
      {/* Period selector */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <BarChart3Icon className="h-5 w-5 text-muted-foreground" />
          <h2 className="font-quicksand text-lg font-semibold text-foreground">
            Processing Metrics
          </h2>
        </div>
        <div className="flex items-center gap-1 bg-slate-100 dark:bg-zinc-700/50 rounded-lg p-0.5">
          {PERIOD_OPTIONS.map(opt => (
            <button
              key={opt.value}
              type="button"
              onClick={() => setPeriod(opt.value)}
              className={cn(
                'px-3 py-1 text-sm font-medium rounded-md transition-all',
                period === opt.value
                  ? 'bg-white dark:bg-zinc-600 text-foreground shadow-sm'
                  : 'text-muted-foreground hover:text-foreground'
              )}
              data-testid={`period-${opt.value}`}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard
          label="Total Processed"
          value={isLoading ? '' : (metrics?.totalProcessed ?? 0).toLocaleString()}
          icon={CheckCircle2Icon}
          variant="success"
          loading={isLoading}
        />
        <StatCard
          label="Total Failed"
          value={isLoading ? '' : (metrics?.totalFailed ?? 0).toLocaleString()}
          icon={XCircleIcon}
          variant={metrics && metrics.totalFailed > 0 ? 'danger' : 'default'}
          loading={isLoading}
        />
        <StatCard
          label="Failure Rate"
          value={isLoading ? '' : `${metrics?.failureRatePercent ?? 0}%`}
          icon={ActivityIcon}
          variant={
            metrics && metrics.failureRatePercent > 10
              ? 'danger'
              : metrics && metrics.failureRatePercent > 5
                ? 'warning'
                : 'default'
          }
          loading={isLoading}
        />
        <StatCard
          label="Avg Duration"
          value={isLoading ? '' : formatDuration(metrics?.avgTotalDurationSeconds ?? 0)}
          icon={ClockIcon}
          loading={isLoading}
        />
      </div>

      {/* Phase Timings */}
      <Card className="border-border/50 dark:border-border/30 bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none">
        <CardHeader className="pb-3">
          <CardTitle className="text-base font-quicksand">Per-Phase Timing Breakdown</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-4" data-testid="phase-timing-skeleton">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="space-y-2">
                  <div className="flex justify-between">
                    <Skeleton className="h-4 w-32" />
                    <Skeleton className="h-4 w-24" />
                  </div>
                  <Skeleton className="h-6 w-full" />
                </div>
              ))}
            </div>
          ) : metrics && metrics.phaseTimings.length > 0 ? (
            <div className="space-y-4">
              {metrics.phaseTimings.map(phase => (
                <PhaseTimingBar key={phase.phase} phase={phase} maxDuration={maxDuration} />
              ))}
            </div>
          ) : (
            <div className="text-center py-8 text-muted-foreground" data-testid="no-metrics">
              No processing metrics available for this period.
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
