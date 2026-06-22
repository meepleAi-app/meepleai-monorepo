'use client';

import { useQuery } from '@tanstack/react-query';
import {
  ActivityIcon,
  AlertCircleIcon,
  BrainCircuitIcon,
  CheckCircleIcon,
  CpuIcon,
  GlobeIcon,
  RefreshCwIcon,
  XCircleIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function StatusBadge({ status }: { status: string }) {
  const isHealthy = status === 'healthy' || status === 'ok';
  const isUnavailable = status === 'unavailable' || status === 'unhealthy';

  if (isHealthy) {
    return (
      <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wide bg-entity-toolkit/12 text-entity-toolkit">
        <CheckCircleIcon className="h-3 w-3" />
        Healthy
      </span>
    );
  }

  if (isUnavailable) {
    return (
      <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wide bg-entity-event/12 text-entity-event">
        <XCircleIcon className="h-3 w-3" />
        Unavailable
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wide bg-amber-500/14 text-amber-600">
      <AlertCircleIcon className="h-3 w-3" />
      {status}
    </span>
  );
}

function KpiSkeleton() {
  return (
    <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-kb animate-pulse min-h-[88px]">
      <div className="h-2.5 w-24 bg-muted rounded" />
      <div className="h-7 w-16 bg-muted rounded mt-1" />
    </div>
  );
}

export default function EmbeddingServicePage() {
  const {
    data: info,
    isLoading: infoLoading,
    refetch: refetchInfo,
    isRefetching: infoRefetching,
  } = useQuery({
    queryKey: ['admin', 'embedding', 'info'],
    queryFn: () => adminClient.getEmbeddingInfo(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  const {
    data: metrics,
    isLoading: metricsLoading,
    refetch: refetchMetrics,
    isRefetching: metricsRefetching,
  } = useQuery({
    queryKey: ['admin', 'embedding', 'metrics'],
    queryFn: () => adminClient.getEmbeddingMetrics(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  const isRefreshing = infoRefetching || metricsRefetching;

  const handleRefresh = () => {
    refetchInfo();
    refetchMetrics();
  };

  return (
    <div className="space-y-6">
      {/* Page toolbar */}
      <div className="flex justify-end">
        <Button
          variant="outline"
          size="sm"
          onClick={handleRefresh}
          disabled={isRefreshing}
          className="gap-2"
        >
          <RefreshCwIcon className={`h-4 w-4 ${isRefreshing ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {/* Service Health */}
      {infoLoading ? (
        <div className="h-32 bg-muted rounded-[10px] animate-pulse" />
      ) : (
        <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
          {/* Panel header */}
          <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
            <BrainCircuitIcon className="h-4 w-4 text-entity-kb shrink-0" />
            <h2 className="font-quicksand text-[13px] font-extrabold text-foreground">
              Service Status
            </h2>
            <StatusBadge status={info?.status ?? 'unknown'} />
          </div>

          {/* Panel body */}
          <div className="p-4 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="flex flex-col gap-1">
              <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                Model
              </span>
              <span className="font-mono text-[12px] font-semibold text-entity-kb">
                {info?.model ?? '—'}
              </span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                Device
              </span>
              <span className="font-mono text-[12px] font-semibold text-foreground flex items-center gap-1.5">
                <CpuIcon className="h-3.5 w-3.5 text-muted-foreground" />
                {info?.device?.toUpperCase() ?? '—'}
              </span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                Dimensions
              </span>
              <span className="font-mono text-[12px] font-semibold text-foreground">
                {info?.dimension ?? '—'}
              </span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                Languages
              </span>
              <span className="font-mono text-[12px] font-semibold text-foreground flex items-center gap-1.5">
                <GlobeIcon className="h-3.5 w-3.5 text-muted-foreground" />
                {info?.supportedLanguages?.join(', ').toUpperCase() ?? '—'}
              </span>
            </div>
          </div>

          <div className="border-t border-border/60 px-4 py-3 grid grid-cols-1 sm:grid-cols-3 gap-4 bg-background">
            <div className="flex flex-col gap-0.5">
              <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                Max Input
              </span>
              <span className="font-mono text-[11px] text-foreground">
                {info?.maxInputChars ? `${info.maxInputChars.toLocaleString()} chars` : '—'}
              </span>
            </div>
            <div className="flex flex-col gap-0.5">
              <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                Max Batch
              </span>
              <span className="font-mono text-[11px] text-foreground">
                {info?.maxBatchSize ? `${info.maxBatchSize} texts` : '—'}
              </span>
            </div>
            <div className="flex flex-col gap-0.5">
              <span className="font-mono text-[9.5px] font-bold uppercase tracking-wide text-muted-foreground">
                Auto-refresh
              </span>
              <span className="font-mono text-[11px] text-foreground">Every 30s</span>
            </div>
          </div>
        </section>
      )}

      {/* Throughput Metrics */}
      {metricsLoading ? (
        <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <KpiSkeleton key={i} />
          ))}
        </div>
      ) : (
        <>
          <div className="flex items-center gap-2">
            <ActivityIcon className="h-4 w-4 text-entity-kb" />
            <h2 className="font-quicksand text-[13px] font-extrabold text-foreground">
              Throughput Metrics
            </h2>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
            {/* Total Requests */}
            <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-kb min-h-[88px]">
              <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
                Total Requests
              </span>
              <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground leading-tight">
                {metrics?.requestsTotal?.toLocaleString() ?? '0'}
              </span>
            </div>

            {/* Total Failures */}
            <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-event min-h-[88px]">
              <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
                Total Failures
              </span>
              <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-entity-event leading-tight">
                {metrics?.failuresTotal?.toLocaleString() ?? '0'}
              </span>
            </div>

            {/* Failure Rate */}
            <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-event min-h-[88px]">
              <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
                Failure Rate
              </span>
              <span
                className={`font-quicksand text-[28px] font-extrabold tabular-nums leading-tight ${
                  metrics?.failureRate && metrics.failureRate > 5
                    ? 'text-entity-event'
                    : 'text-entity-toolkit'
                }`}
              >
                {`${metrics?.failureRate ?? 0}%`}
              </span>
            </div>

            {/* Avg Duration */}
            <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-chat min-h-[88px]">
              <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
                Avg Duration
              </span>
              <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground leading-tight">
                {`${metrics?.avgDurationMs ?? 0}`}
                <span className="font-quicksand text-[14px] font-bold text-muted-foreground ml-0.5">
                  ms
                </span>
              </span>
            </div>

            {/* Total Duration */}
            <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-chat min-h-[88px]">
              <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
                Total Duration
              </span>
              <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground leading-tight">
                {`${((metrics?.durationMsSum ?? 0) / 1000).toFixed(1)}`}
                <span className="font-quicksand text-[14px] font-bold text-muted-foreground ml-0.5">
                  s
                </span>
              </span>
            </div>

            {/* Characters Processed */}
            <div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-toolkit min-h-[88px]">
              <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
                Characters Processed
              </span>
              <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground leading-tight">
                {(metrics?.totalCharsSum ?? 0).toLocaleString()}
              </span>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
