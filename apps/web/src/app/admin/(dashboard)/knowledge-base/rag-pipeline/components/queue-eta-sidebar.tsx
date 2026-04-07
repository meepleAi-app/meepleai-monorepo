'use client';

import { useMemo } from 'react';

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
import { apiClient } from '@/lib/api/client';
import { cn } from '@/lib/utils';

import { useQueueConfig, updateQueueConfig, bulkReindexFailed } from '../../queue/lib/queue-api';

// ── Types for sidebar API responses ────────────────────────────────────

interface StepTiming {
  step: string;
  avgDurationSeconds: number;
  sampleCount: number;
}

interface PipelineMetrics {
  stepTimings: StepTiming[];
  avgTotalSeconds: number;
}

interface StageHealth {
  stage: string;
  status: 'healthy' | 'degraded' | 'down';
  message?: string;
}

interface PipelineHealth {
  stages: StageHealth[];
  overallStatus: 'healthy' | 'degraded' | 'down';
}

interface EmbeddingInfo {
  model: string;
  dimension: number;
  maxTokens: number;
}

interface EmbeddingMetrics {
  totalRequests: number;
  failedRequests: number;
  avgLatencyMs: number;
  throughputPerSecond: number;
}

// ── Fetch helpers ──────────────────────────────────────────────────────

async function fetchPipelineMetrics(): Promise<PipelineMetrics | null> {
  try {
    return await apiClient.get<PipelineMetrics>('/api/v1/admin/pipeline/metrics');
  } catch {
    return null;
  }
}

async function fetchPipelineHealth(): Promise<PipelineHealth | null> {
  try {
    return await apiClient.get<PipelineHealth>('/api/v1/admin/pipeline/health');
  } catch {
    return null;
  }
}

async function fetchEmbeddingInfo(): Promise<EmbeddingInfo | null> {
  try {
    return await apiClient.get<EmbeddingInfo>('/api/v1/admin/embedding/info');
  } catch {
    return null;
  }
}

async function fetchEmbeddingMetrics(): Promise<EmbeddingMetrics | null> {
  try {
    return await apiClient.get<EmbeddingMetrics>('/api/v1/admin/embedding/metrics');
  } catch {
    return null;
  }
}

async function cleanupOrphans(): Promise<{ cleaned: number }> {
  const result = await apiClient.post<{ cleaned: number }>(
    '/api/v1/admin/pipeline/cleanup-orphans'
  );
  return result ?? { cleaned: 0 };
}

// ── Helper: format seconds ─────────────────────────────────────────────

function fmtDuration(seconds: number): string {
  if (seconds < 1) return '<1s';
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const m = Math.floor(seconds / 60);
  const s = Math.round(seconds % 60);
  return s > 0 ? `${m}m ${s}s` : `${m}m`;
}

const HEALTH_STYLES: Record<string, { dot: string; text: string }> = {
  healthy: { dot: 'bg-emerald-500', text: 'text-emerald-700 dark:text-emerald-400' },
  degraded: { dot: 'bg-amber-500', text: 'text-amber-700 dark:text-amber-400' },
  down: { dot: 'bg-red-500', text: 'text-red-700 dark:text-red-400' },
};

// ── Component ──────────────────────────────────────────────────────────

export function QueueETASidebar() {
  const queryClient = useQueryClient();

  // Fetch sidebar data
  const { data: metrics } = useQuery({
    queryKey: ['admin', 'pipeline', 'metrics'],
    queryFn: fetchPipelineMetrics,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const { data: health } = useQuery({
    queryKey: ['admin', 'pipeline', 'health'],
    queryFn: fetchPipelineHealth,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const { data: embeddingInfo } = useQuery({
    queryKey: ['admin', 'embedding', 'info'],
    queryFn: fetchEmbeddingInfo,
    staleTime: 60_000,
    refetchInterval: 120_000,
  });

  const { data: embeddingMetrics } = useQuery({
    queryKey: ['admin', 'embedding', 'metrics'],
    queryFn: fetchEmbeddingMetrics,
    staleTime: 15_000,
    refetchInterval: 30_000,
  });

  const { data: queueConfig } = useQueueConfig();

  // Mutations
  const reindexMutation = useMutation({
    mutationFn: bulkReindexFailed,
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] }),
  });

  const cleanupMutation = useMutation({
    mutationFn: cleanupOrphans,
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] }),
  });

  const togglePauseMutation = useMutation({
    mutationFn: (pause: boolean) => updateQueueConfig(pause),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['admin', 'queue', 'config'] }),
  });

  const isPaused = queueConfig?.isPaused ?? false;

  // Computed embedding stats
  const failureRate = useMemo(() => {
    if (!embeddingMetrics || embeddingMetrics.totalRequests === 0) return 0;
    return (embeddingMetrics.failedRequests / embeddingMetrics.totalRequests) * 100;
  }, [embeddingMetrics]);

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
          {metrics?.stepTimings && metrics.stepTimings.length > 0 ? (
            <div className="space-y-2">
              {metrics.stepTimings.map(step => (
                <div key={step.step} className="flex items-center justify-between text-xs">
                  <span className="text-muted-foreground truncate mr-2">{step.step}</span>
                  <span className="font-mono font-medium text-foreground shrink-0">
                    {fmtDuration(step.avgDurationSeconds)}
                  </span>
                </div>
              ))}
              <div className="border-t border-border/50 pt-2 mt-2 flex items-center justify-between text-xs font-medium">
                <span className="text-muted-foreground">Total avg</span>
                <span className="font-mono text-amber-700 dark:text-amber-400">
                  {fmtDuration(metrics.avgTotalSeconds)}
                </span>
              </div>
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">No timing data yet</p>
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
                const style = HEALTH_STYLES[stage.status] ?? HEALTH_STYLES.down;
                return (
                  <div key={stage.stage} className="flex items-center gap-2 text-xs">
                    <span className={cn('h-2 w-2 rounded-full shrink-0', style.dot)} />
                    <span className="text-muted-foreground truncate">{stage.stage}</span>
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
                  title={embeddingInfo.model}
                >
                  {embeddingInfo.model}
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
                <span className="text-muted-foreground">Throughput</span>
                <span className="font-mono text-foreground">
                  {embeddingMetrics.throughputPerSecond.toFixed(1)}/s
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Avg latency</span>
                <span className="font-mono text-foreground">
                  {Math.round(embeddingMetrics.avgLatencyMs)}ms
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
