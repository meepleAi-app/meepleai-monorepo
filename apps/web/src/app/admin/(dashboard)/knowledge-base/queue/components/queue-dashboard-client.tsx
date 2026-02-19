'use client';

import { useState, useCallback } from 'react';
import Link from 'next/link';
import { ArrowLeftIcon, PlusIcon, RefreshCwIcon } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';

import { Button } from '@/components/ui/primitives/button';

import { useQueueList, useJobDetail } from '../lib/queue-api';
import type { QueueFilters } from '../lib/queue-api';
import { QueueStatsBar } from './queue-stats-bar';
import { QueueFiltersBar } from './queue-filters';
import { QueueList } from './queue-list';
import { JobDetailPanel } from './job-detail-panel';

export function QueueDashboardClient() {
  const queryClient = useQueryClient();
  const [filters, setFilters] = useState<QueueFilters>({
    page: 1,
    pageSize: 20,
  });
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null);

  const { data: queueData, isLoading: isQueueLoading } = useQueueList(filters);
  const { data: jobDetail, isLoading: isDetailLoading } = useJobDetail(selectedJobId);

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
          <Link href="/admin/knowledge-base">
            <Button variant="ghost" size="icon" className="h-8 w-8">
              <ArrowLeftIcon className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
              Processing Queue
            </h1>
            <p className="text-sm text-muted-foreground mt-0.5">
              Monitor and manage PDF processing jobs
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={handleRefresh}>
            <RefreshCwIcon className="h-4 w-4 mr-1" />
            Refresh
          </Button>
          <Link href="/admin/knowledge-base/upload">
            <Button size="sm">
              <PlusIcon className="h-4 w-4 mr-1" />
              Add PDF
            </Button>
          </Link>
        </div>
      </div>

      {/* Stats Bar */}
      <QueueStatsBar data={queueData} isLoading={isQueueLoading} />

      {/* Filters */}
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
    </div>
  );
}
