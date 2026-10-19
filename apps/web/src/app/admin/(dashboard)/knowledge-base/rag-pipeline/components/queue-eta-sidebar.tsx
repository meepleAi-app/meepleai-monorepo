'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ActivityIcon,
  AlertTriangleIcon,
  BrainIcon,
  ClockIcon,
  PauseIcon,
  PlayIcon,
  RefreshCwIcon,
  Trash2Icon,
  ZapIcon,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import {
  useEmbeddingServiceInfo,
  useEmbeddingServiceMetrics,
} from '@/hooks/queries/useEmbeddingService';
import { api } from '@/lib/api';
import type { PipelineStageStatus } from '@/lib/api/schemas/admin-knowledge-base.schemas';
import { cn } from '@/lib/utils';

import { useQueueConfig, updateQueueConfig, bulkReindexFailed } from '../../queue/lib/queue-api';

// ── Helper: format seconds ─────────────────────────────────────────────

function fmtDuration(seconds: number): string {
  if (seconds < 1) return '<1s';
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const m = Math.floor(seconds / 60);
  const s = Math.round(seconds % 60);
  return s > 0 ? `${m}m ${s}s` : `${m}m`;
}

const HEALTH_STYLES: Record<PipelineStageStatus, { dot: string; text: string }> = {
  healthy: { dot: 'bg-emerald-500', text: 'text-emerald-700 dark:text-emerald-400' },
  warning: { dot: 'bg-amber-500', text: 'text-amber-700 dark:text-amber-400' },
  error: { dot: 'bg-red-500', text: 'text-red-700 dark:text-red-400' },
};

// ── Component ──────────────────────────────────────────────────────────

export function QueueETASidebar() {
  const queryClient = useQueryClient();

  // #4511: timing, health and the orphan cleanup called three routes under `/admin/pipeline/` that
  // the API does not have, with types of their own: the two GETs fell into a `catch` and showed
  // nothing, and the cleanup button answered 404.
  const { data: metrics, isError: metricsFailed } = useQuery({
    queryKey: ['admin', 'pipeline', 'metrics'],
    queryFn: () => api.admin.getPdfMetrics(),
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const { data: health } = useQuery({
    queryKey: ['admin', 'pipeline', 'health'],
    queryFn: () => api.admin.getPipelineHealth(),
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  // #4446: the dimension shown here was a 1024 written in the endpoint.
  const { data: embeddingInfo } = useEmbeddingServiceInfo({ refetchInterval: 120_000 });

  // #4446: this read `throughputPerSecond`, `avgLatencyMs` and `failedRequests`, which the API
  // never sent: the `.toFixed` on the missing throughput took the whole RAG pipeline page down.
  const { data: embeddingMetrics } = useEmbeddingServiceMetrics();

  const { data: queueConfig } = useQueueConfig();

  // Mutations
  const reindexMutation = useMutation({
    mutationFn: bulkReindexFailed,
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] }),
  });

  const cleanupMutation = useMutation({
    mutationFn: () => api.admin.cleanupOrphans(),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] }),
  });

  const togglePauseMutation = useMutation({
    mutationFn: (pause: boolean) => updateQueueConfig(pause, queueConfig?.maxConcurrentWorkers),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['admin', 'queue', 'config'] }),
  });

  const isPaused = queueConfig?.isPaused ?? false;

  // Keyed by step, in the order the API records them; durations are in seconds.
  const stepTimings = metrics ? Object.values(metrics.averages) : [];

  // Already a percentage, computed by the API from the service's counters.
  const failureRate = embeddingMetrics?.failureRate ?? 0;

  return (
    <div className="space-y-4">
      {/* 1. Step Timing Averages */}
      <Card className="hover:translate-y-0">
        <CardHeader className="pb-2 p-4">
          <CardTitle className="text-sm flex items-center gap-2">
            <ClockIcon className="h-4 w-4 text-amber-600 dark:text-amber-400" />
            Step Timing
          </CardTitle>
        </CardHeader>
        <CardContent className="p-4 pt-0">
          {stepTimings.length > 0 ? (
            <div className="space-y-2">
              {stepTimings.map(step => (
                <div key={step.step} className="flex items-center justify-between text-xs">
                  <span className="text-muted-foreground truncate mr-2">{step.step}</span>
                  <span className="font-mono font-medium text-foreground shrink-0">
                    {fmtDuration(step.avgDuration)}
                  </span>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">
              {metricsFailed ? 'Timing data unavailable' : 'No timing data yet'}
            </p>
          )}
        </CardContent>
      </Card>

      {/* 2. Pipeline Health */}
      <Card className="hover:translate-y-0">
        <CardHeader className="pb-2 p-4">
          <CardTitle className="text-sm flex items-center gap-2">
            <ActivityIcon className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
            Pipeline Health
          </CardTitle>
        </CardHeader>
        <CardContent className="p-4 pt-0">
          {health?.stages && health.stages.length > 0 ? (
            <div className="space-y-2">
              {health.stages.map(stage => {
                const style = HEALTH_STYLES[stage.status];
                return (
                  <div key={stage.name} className="flex items-center gap-2 text-xs">
                    <span className={cn('h-2 w-2 rounded-full shrink-0', style.dot)} />
                    <span className="text-muted-foreground truncate">{stage.name}</span>
                    <span className={cn('ml-auto capitalize shrink-0', style.text)}>
                      {stage.status}
                    </span>
                  </div>
                );
              })}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">Health data unavailable</p>
          )}
        </CardContent>
      </Card>

      {/* 3. Embedding Service Mini */}
      <Card className="hover:translate-y-0">
        <CardHeader className="pb-2 p-4">
          <CardTitle className="text-sm flex items-center gap-2">
            <BrainIcon className="h-4 w-4 text-purple-600 dark:text-purple-400" />
            Embedding Service
          </CardTitle>
        </CardHeader>
        <CardContent className="p-4 pt-0 space-y-2">
          {embeddingInfo ? (
            <div className="text-xs space-y-1">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Model</span>
                <span
                  className="font-mono text-foreground truncate ml-2 max-w-[140px]"
                  title={embeddingInfo.model ?? undefined}
                >
                  {embeddingInfo.model ?? 'unavailable'}
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Dim</span>
                <span className="font-mono text-foreground">{embeddingInfo.dimension}</span>
              </div>
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">Service info unavailable</p>
          )}
          {embeddingMetrics && (
            <div className="text-xs space-y-1 border-t border-border/50 pt-2">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Avg latency</span>
                <span className="font-mono text-foreground">
                  {Math.round(embeddingMetrics.avgDurationMs)}ms
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Failure rate</span>
                <span
                  className={cn(
                    'font-mono',
                    failureRate > 10
                      ? 'text-red-600 dark:text-red-400'
                      : failureRate > 2
                        ? 'text-amber-600 dark:text-amber-400'
                        : 'text-emerald-600 dark:text-emerald-400'
                  )}
                >
                  {failureRate.toFixed(1)}%
                </span>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* 4. Quick Actions */}
      <Card className="hover:translate-y-0">
        <CardHeader className="pb-2 p-4">
          <CardTitle className="text-sm flex items-center gap-2">
            <ZapIcon className="h-4 w-4 text-amber-600 dark:text-amber-400" />
            Quick Actions
          </CardTitle>
        </CardHeader>
        <CardContent className="p-4 pt-0 space-y-2">
          <Button
            variant="outline"
            size="sm"
            className="w-full justify-start text-xs"
            disabled={reindexMutation.isPending}
            onClick={() => reindexMutation.mutate()}
          >
            <RefreshCwIcon
              className={cn('h-3.5 w-3.5 mr-2', reindexMutation.isPending && 'animate-spin')}
            />
            {reindexMutation.isPending ? 'Retrying...' : 'Retry all failed'}
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="w-full justify-start text-xs"
            disabled={cleanupMutation.isPending}
            onClick={() => cleanupMutation.mutate()}
          >
            <Trash2Icon className="h-3.5 w-3.5 mr-2" />
            {cleanupMutation.isPending ? 'Cleaning...' : 'Cleanup orphans'}
          </Button>
          {/* Chunks only: CleanupOrphansCommandHandler always answers `orphanedVectors: 0`. */}
          {cleanupMutation.isSuccess && (
            <p className="text-xs text-muted-foreground px-1">
              Removed {cleanupMutation.data.orphanedChunks} orphaned chunks
            </p>
          )}
          {cleanupMutation.isError && (
            <p className="text-xs text-red-600 dark:text-red-400 px-1">Cleanup failed</p>
          )}
          <Button
            variant="outline"
            size="sm"
            className={cn(
              'w-full justify-start text-xs',
              isPaused &&
                'border-amber-300 dark:border-amber-700 text-amber-700 dark:text-amber-400'
            )}
            disabled={togglePauseMutation.isPending}
            onClick={() => togglePauseMutation.mutate(!isPaused)}
          >
            {isPaused ? (
              <>
                <PlayIcon className="h-3.5 w-3.5 mr-2" />
                Resume queue
              </>
            ) : (
              <>
                <PauseIcon className="h-3.5 w-3.5 mr-2" />
                Pause queue
              </>
            )}
          </Button>
          {isPaused && (
            <div className="flex items-center gap-1.5 text-xs text-amber-600 dark:text-amber-400 px-1">
              <AlertTriangleIcon className="h-3 w-3 shrink-0" />
              Queue is paused
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
