'use client';

import { useState, useCallback } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeftIcon, PlusIcon, RefreshCwIcon } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

import { BulkActionsBar } from './bulk-actions-bar';
import { JobDetailPanel } from './job-detail-panel';
import { MetricsDashboard } from './metrics-dashboard';
import { QueueCapacityIndicator } from './queue-capacity-indicator';
import { QueueControlBar } from './queue-control-bar';
import { QueueFiltersBar } from './queue-filters';
import { QueueList } from './queue-list';
import { QueueStatsBar } from './queue-stats-bar';
import { SSEConnectionIndicator } from './sse-connection-indicator';
import { useJobSSE } from '../hooks/use-job-sse';
import { useQueueSSE } from '../hooks/use-queue-sse';
import { useQueueList, useJobDetail } from '../lib/queue-api';

import type { QueueFilters } from '../lib/queue-api';

export function QueueDashboardClient({ gameId }: { gameId?: string }) {
  const queryClient = useQueryClient();
  const [filters, setFilters] = useState<QueueFilters>({
    page: 1,
    pageSize: 20,
  });
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null);

  // SSE connections
  const { connectionState: queueSSEState, reconnect: reconnectQueueSSE } = useQueueSSE(true);
  const { connectionState: jobSSEState } = useJobSSE(selectedJobId);

  const sseConnected = queueSSEState === 'connected';
  const jobSseConnected = jobSSEState === 'connected';

  // Reduce polling when SSE is active
  const { data: queueData, isLoading: isQueueLoading } = useQueueList(filters, sseConnected);
  const { data: jobDetail, isLoading: isDetailLoading } = useJobDetail(
    selectedJobId,
    jobSseConnected
  );

  const handleSelectJob = useCallback((jobId: string) => {
    setSelectedJobId(jobId);
  }, []);

  const handleRefresh = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
  }, [queryClient]);

  return (
    <div className="space-y-4 h-full">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href={gameId ? `/admin/shared-games/${gameId}` : '/admin/knowledge-base'}>
            <Button variant="ghost" size="icon" className="h-8 w-8">
              <ArrowLeftIcon className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
              Processing Queue
            </h1>
            <p className="text-sm text-muted-foreground mt-0.5">
              {gameId
                ? 'Processing jobs for selected game'
                : 'Monitor and manage PDF processing jobs'}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <SSEConnectionIndicator state={queueSSEState} onReconnect={reconnectQueueSSE} />
          <Button variant="outline" size="sm" onClick={handleRefresh}>
            <RefreshCwIcon className="h-4 w-4 mr-1" />
            Refresh
          </Button>
          <Link href={`/admin/knowledge-base/upload${gameId ? `?gameId=${gameId}` : ''}`}>
            <Button size="sm">
              <PlusIcon className="h-4 w-4 mr-1" />
              Add PDF
            </Button>
          </Link>
        </div>
      </div>

      {/* Queue Control Bar (Pause/Resume, Workers, Backpressure) */}
      <QueueControlBar />

      {/* Capacity Indicator */}
      <QueueCapacityIndicator />

      {/* Stats Bar */}
      <QueueStatsBar />

      {/* Bulk Actions + Filters */}
      <div className="flex flex-wrap items-center justify-between gap-2">
        <BulkActionsBar />
      </div>
      <QueueFiltersBar filters={filters} onFiltersChange={setFilters} />

      {/* Main Content: List (40%) + Detail (60%) */}
      <div className="grid grid-cols-1 lg:grid-cols-5 gap-4 min-h-[500px]">
        {/* Queue List - 2/5 = 40% */}
        <div className="lg:col-span-2 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50 overflow-hidden">
          <QueueList
            data={queueData}
            isLoading={isQueueLoading}
            selectedJobId={selectedJobId}
            onSelectJob={handleSelectJob}
            filters={filters}
            onFiltersChange={setFilters}
          />
        </div>

        {/* Detail Panel - 3/5 = 60% */}
        <div className="lg:col-span-3 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50 overflow-hidden">
          <JobDetailPanel job={jobDetail} isLoading={isDetailLoading} />
        </div>
      </div>

      {/* Metrics Dashboard */}
      <MetricsDashboard />
    </div>
  );
}
