'use client';

import { useState, useMemo } from 'react';

import {
  CheckCircleIcon,
  ClockIcon,
  AlertTriangleIcon,
  Loader2Icon,
  InboxIcon,
} from 'lucide-react';

import { UploadZone } from '@/components/admin/knowledge-base/upload-zone';

import { QueueETASidebar } from './queue-eta-sidebar';
import { BulkActionsBar } from '../../queue/components/bulk-actions-bar';
import { QueueFiltersBar } from '../../queue/components/queue-filters';
import { QueueList } from '../../queue/components/queue-list';
import { SSEConnectionIndicator } from '../../queue/components/sse-connection-indicator';
import { useQueueSSE } from '../../queue/hooks/use-queue-sse';
import { useQueueList, useQueueStats, type QueueFilters } from '../../queue/lib/queue-api';
import { useQueueETA, formatETA } from '../lib/use-queue-eta';

// ── Stats Bar Cell ─────────────────────────────────────────────────────

interface StatCellProps {
  label: string;
  value: string | number;
  icon: React.ReactNode;
  accent?: string;
}

function StatCell({ label, value, icon, accent }: StatCellProps) {
  return (
    <div className="flex items-center gap-3 px-4 py-3">
      <div className={accent ?? 'text-muted-foreground'}>{icon}</div>
      <div className="min-w-0">
        <p className="text-xs text-muted-foreground truncate">{label}</p>
        <p className="text-lg font-bold font-mono leading-tight text-foreground">{value}</p>
      </div>
    </div>
  );
}

// ── Main Component ─────────────────────────────────────────────────────

export function UploadAndQueueTab() {
  const [filters, setFilters] = useState<QueueFilters>({ page: 1, pageSize: 20 });
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null);

  // SSE connection
  const { connectionState, reconnect } = useQueueSSE(true);
  const isConnected = connectionState === 'connected';

  // Queue data
  const { data: queueData, isLoading } = useQueueList(filters, isConnected);

  // Stats queries (one per status)
  const statsQueries = useQueueStats();
  const stats = useMemo(() => {
    const [queued, processing, completed, failed] = statsQueries;
    return {
      queued: queued?.data?.total ?? 0,
      processing: processing?.data?.total ?? 0,
      completed: completed?.data?.total ?? 0,
      failed: failed?.data?.total ?? 0,
    };
  }, [statsQueries]);

  // ETA
  const { data: etaData } = useQueueETA(true);
  const etaDisplay = useMemo(
    () => formatETA(etaData?.totalDrainTimeMinutes ?? 0),
    [etaData?.totalDrainTimeMinutes]
  );

  return (
    <div className="space-y-4">
      {/* Stats Bar */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50 grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 divide-x divide-slate-200/50 dark:divide-zinc-700/50">
        <StatCell
          label="Queued"
          value={stats.queued}
          icon={<InboxIcon className="h-5 w-5" />}
          accent="text-blue-600 dark:text-blue-400"
        />
        <StatCell
          label="Processing"
          value={stats.processing}
          icon={<Loader2Icon className="h-5 w-5 animate-spin" />}
          accent="text-amber-600 dark:text-amber-400"
        />
        <StatCell
          label="Completed 24h"
          value={stats.completed}
          icon={<CheckCircleIcon className="h-5 w-5" />}
          accent="text-emerald-600 dark:text-emerald-400"
        />
        <StatCell
          label="Failed"
          value={stats.failed}
          icon={<AlertTriangleIcon className="h-5 w-5" />}
          accent="text-red-600 dark:text-red-400"
        />
        <StatCell
          label="ETA (drain)"
          value={etaDisplay}
          icon={<ClockIcon className="h-5 w-5" />}
          accent="text-purple-600 dark:text-purple-400"
        />
      </div>

      {/* Grid: Left (upload + queue) + Right (sidebar) */}
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_280px] gap-4">
        {/* Left Column */}
        <div className="space-y-4">
          {/* Upload Zone */}
          <UploadZone />

          {/* Queue Toolbar */}
          <div className="flex flex-col sm:flex-row items-start sm:items-center gap-3">
            <div className="flex-1 w-full">
              <QueueFiltersBar filters={filters} onFiltersChange={setFilters} />
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <BulkActionsBar />
              <SSEConnectionIndicator state={connectionState} onReconnect={reconnect} />
            </div>
          </div>

          {/* Queue List */}
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50 min-h-[300px]">
            <QueueList
              data={queueData}
              isLoading={isLoading}
              selectedJobId={selectedJobId}
              onSelectJob={setSelectedJobId}
              filters={filters}
              onFiltersChange={setFilters}
            />
          </div>
        </div>

        {/* Right Sidebar */}
        <aside className="hidden lg:block">
          <QueueETASidebar />
        </aside>
      </div>
    </div>
  );
}
