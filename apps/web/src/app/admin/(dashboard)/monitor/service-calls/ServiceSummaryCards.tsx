'use client';

/**
 * ServiceSummaryCards Component
 * Per-service statistics cards showing call counts, latency, and error rates.
 */

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';

import { Badge } from '@/components/ui/badge';
import { api } from '@/lib/api';
import type { ServiceCallSummary } from '@/lib/api/schemas/admin/admin-service-calls.schemas';

type PeriodOption = '1h' | '6h' | '24h' | '7d';

const PERIOD_OPTIONS: { value: PeriodOption; label: string }[] = [
  { value: '1h', label: '1h' },
  { value: '6h', label: '6h' },
  { value: '24h', label: '24h' },
  { value: '7d', label: '7d' },
];

// Color palette for service cards (left border)
const SERVICE_COLORS = [
  'border-l-violet-500',
  'border-l-sky-500',
  'border-l-emerald-500',
  'border-l-amber-500',
  'border-l-rose-500',
  'border-l-cyan-500',
  'border-l-indigo-500',
  'border-l-orange-500',
  'border-l-teal-500',
];

function getServiceColor(index: number): string {
  return SERVICE_COLORS[index % SERVICE_COLORS.length] ?? 'border-l-zinc-500';
}

function formatLatency(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function formatTimestamp(ts: string | null): string | null {
  if (!ts) return null;
  try {
    return new Date(ts).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    });
  } catch {
    return ts;
  }
}

function errorRateBadgeClass(rate: number): string {
  if (rate === 0) return 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300';
  if (rate < 0.05) return 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300';
  return 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300';
}

interface ServiceCardProps {
  summary: ServiceCallSummary;
  colorClass: string;
}

function ServiceCard({ summary, colorClass }: ServiceCardProps) {
  const errorRatePct = (summary.errorRate * 100).toFixed(1);
  const lastError = formatTimestamp(summary.lastErrorAt);

  return (
    <div
      className={`rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70 border-l-4 ${colorClass} p-4 space-y-3`}
      data-testid={`service-card-${summary.serviceName}`}
    >
      <div className="flex items-center justify-between gap-2">
        <h3 className="font-semibold text-sm truncate">{summary.serviceName}</h3>
        <Badge
          className={`text-[10px] font-medium shrink-0 ${errorRateBadgeClass(summary.errorRate)}`}
        >
          {errorRatePct}% err
        </Badge>
      </div>

      <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
        <div className="text-muted-foreground">Total calls</div>
        <div className="font-mono font-medium tabular-nums text-right">
          {summary.totalCalls.toLocaleString()}
        </div>

        <div className="text-muted-foreground">Avg latency</div>
        <div className="font-mono font-medium tabular-nums text-right">
          {formatLatency(summary.avgLatencyMs)}
        </div>

        <div className="text-muted-foreground">p95 latency</div>
        <div className="font-mono font-medium tabular-nums text-right">
          {formatLatency(summary.p95LatencyMs)}
        </div>

        <div className="text-muted-foreground">Errors</div>
        <div
          className={`font-mono font-medium tabular-nums text-right ${
            summary.errorCount > 0 ? 'text-red-600 dark:text-red-400' : ''
          }`}
        >
          {summary.errorCount.toLocaleString()}
        </div>
      </div>

      {lastError && (
        <div className="text-[10px] text-muted-foreground border-t pt-2">
          Last error: <span className="font-mono">{lastError}</span>
        </div>
      )}
    </div>
  );
}

export function ServiceSummaryCards() {
  const [period, setPeriod] = useState<PeriodOption>('24h');

  const { data, isFetching } = useQuery({
    queryKey: ['admin', 'service-call-summary', period],
    queryFn: () => api.admin.getServiceCallSummary(period),
    staleTime: 60_000,
    refetchInterval: 120_000,
  });

  const summaries = data ?? [];

  return (
    <div data-testid="service-summary-cards" className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-semibold">Service Summary</h2>
        <div className="flex gap-1">
          {PERIOD_OPTIONS.map(opt => (
            <button
              key={opt.value}
              onClick={() => setPeriod(opt.value)}
              className={`px-2.5 py-1 rounded text-xs font-medium transition-colors ${
                period === opt.value
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground hover:text-foreground'
              }`}
              data-testid={`period-btn-${opt.value}`}
            >
              {opt.label}
            </button>
          ))}
          {isFetching && (
            <div className="ml-2 h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent self-center" />
          )}
        </div>
      </div>

      {summaries.length === 0 && !isFetching ? (
        <div
          className="py-10 text-center text-sm text-muted-foreground rounded-xl border"
          data-testid="service-summary-empty"
        >
          No service call data for this period.
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {summaries.map((summary, idx) => (
            <ServiceCard
              key={summary.serviceName}
              summary={summary}
              colorClass={getServiceColor(idx)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
