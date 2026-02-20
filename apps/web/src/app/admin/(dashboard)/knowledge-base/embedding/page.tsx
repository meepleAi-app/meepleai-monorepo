'use client';

import { useQuery } from '@tanstack/react-query';
import {
  ActivityIcon,
  AlertCircleIcon,
  BrainCircuitIcon,
  CheckCircleIcon,
  CpuIcon,
  GlobeIcon,
  HashIcon,
  RefreshCwIcon,
  TextIcon,
  TimerIcon,
  XCircleIcon,
  ZapIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function StatCard({ label, value, icon: Icon, color }: {
  label: string;
  value: string | number;
  icon: React.ElementType;
  color?: string;
}) {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
      <div className="flex items-center gap-2 text-sm text-gray-600 dark:text-zinc-400 mb-1">
        <Icon className={`h-4 w-4 ${color ?? ''}`} />
        {label}
      </div>
      <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">{value}</div>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const isHealthy = status === 'healthy' || status === 'ok';
  const isUnavailable = status === 'unavailable' || status === 'unhealthy';

  if (isHealthy) {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300">
        <CheckCircleIcon className="h-3.5 w-3.5" />
        Healthy
      </span>
    );
  }

  if (isUnavailable) {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300">
        <XCircleIcon className="h-3.5 w-3.5" />
        Unavailable
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300">
      <AlertCircleIcon className="h-3.5 w-3.5" />
      {status}
    </span>
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
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Embedding Service
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Monitor the multilingual embedding model and throughput
          </p>
        </div>
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
        <div className="h-32 bg-white/40 dark:bg-zinc-800/40 rounded-xl animate-pulse" />
      ) : (
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-white/40 dark:border-zinc-700/40">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-zinc-100 flex items-center gap-2">
              <BrainCircuitIcon className="h-5 w-5 text-violet-500" />
              Service Status
            </h2>
            <StatusBadge status={info?.status ?? 'unknown'} />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div>
              <div className="text-xs text-gray-500 dark:text-zinc-500 uppercase tracking-wider mb-1">Model</div>
              <div className="text-sm font-medium text-gray-900 dark:text-zinc-100">
                {info?.model ?? '\u2014'}
              </div>
            </div>
            <div>
              <div className="text-xs text-gray-500 dark:text-zinc-500 uppercase tracking-wider mb-1">Device</div>
              <div className="text-sm font-medium text-gray-900 dark:text-zinc-100 flex items-center gap-1.5">
                <CpuIcon className="h-3.5 w-3.5" />
                {info?.device?.toUpperCase() ?? '\u2014'}
              </div>
            </div>
            <div>
              <div className="text-xs text-gray-500 dark:text-zinc-500 uppercase tracking-wider mb-1">Dimensions</div>
              <div className="text-sm font-medium text-gray-900 dark:text-zinc-100">
                {info?.dimension ?? '\u2014'}
              </div>
            </div>
            <div>
              <div className="text-xs text-gray-500 dark:text-zinc-500 uppercase tracking-wider mb-1">Languages</div>
              <div className="text-sm font-medium text-gray-900 dark:text-zinc-100 flex items-center gap-1.5">
                <GlobeIcon className="h-3.5 w-3.5" />
                {info?.supportedLanguages?.join(', ').toUpperCase() ?? '\u2014'}
              </div>
            </div>
          </div>

          <div className="mt-4 pt-4 border-t border-gray-200/50 dark:border-zinc-700/50 grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div>
              <div className="text-xs text-gray-500 dark:text-zinc-500 uppercase tracking-wider mb-1">Max Input</div>
              <div className="text-sm text-gray-700 dark:text-zinc-300">
                {info?.maxInputChars ? `${info.maxInputChars.toLocaleString()} chars` : '\u2014'}
              </div>
            </div>
            <div>
              <div className="text-xs text-gray-500 dark:text-zinc-500 uppercase tracking-wider mb-1">Max Batch</div>
              <div className="text-sm text-gray-700 dark:text-zinc-300">
                {info?.maxBatchSize ? `${info.maxBatchSize} texts` : '\u2014'}
              </div>
            </div>
            <div>
              <div className="text-xs text-gray-500 dark:text-zinc-500 uppercase tracking-wider mb-1">Auto-refresh</div>
              <div className="text-sm text-gray-700 dark:text-zinc-300">Every 30s</div>
            </div>
          </div>
        </div>
      )}

      {/* Throughput Metrics */}
      {metricsLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-24 bg-white/40 dark:bg-zinc-800/40 rounded-lg animate-pulse" />
          ))}
        </div>
      ) : (
        <>
          <h2 className="text-lg font-semibold text-gray-900 dark:text-zinc-100 flex items-center gap-2">
            <ActivityIcon className="h-5 w-5 text-blue-500" />
            Throughput Metrics
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <StatCard
              label="Total Requests"
              value={metrics?.requestsTotal?.toLocaleString() ?? '0'}
              icon={ZapIcon}
              color="text-blue-500"
            />
            <StatCard
              label="Total Failures"
              value={metrics?.failuresTotal?.toLocaleString() ?? '0'}
              icon={XCircleIcon}
              color="text-red-500"
            />
            <StatCard
              label="Failure Rate"
              value={`${metrics?.failureRate ?? 0}%`}
              icon={AlertCircleIcon}
              color={metrics?.failureRate && metrics.failureRate > 5 ? 'text-red-500' : 'text-green-500'}
            />
            <StatCard
              label="Avg Duration"
              value={`${metrics?.avgDurationMs ?? 0} ms`}
              icon={TimerIcon}
              color="text-amber-500"
            />
            <StatCard
              label="Total Duration"
              value={`${((metrics?.durationMsSum ?? 0) / 1000).toFixed(1)} s`}
              icon={HashIcon}
              color="text-violet-500"
            />
            <StatCard
              label="Characters Processed"
              value={(metrics?.totalCharsSum ?? 0).toLocaleString()}
              icon={TextIcon}
              color="text-emerald-500"
            />
          </div>
        </>
      )}
    </div>
  );
}
