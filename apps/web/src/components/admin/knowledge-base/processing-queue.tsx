'use client';

import { useState } from 'react';

import {
  FileIcon,
  CheckCircleIcon,
  LoaderIcon,
  XCircleIcon,
  ClockIcon,
  BanIcon,
  RotateCcwIcon,
  TrashIcon,
  WifiIcon,
  WifiOffIcon,
} from 'lucide-react';

import { useQueueSSE } from '@/app/admin/(dashboard)/knowledge-base/queue/hooks/use-queue-sse';
import {
  useQueueList,
  cancelJob,
  retryJob,
  removeJob,
  type JobStatus,
  type ProcessingJobDto,
} from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';

// ── Status Config ──────────────────────────────────────────────────────

const statusConfig: Record<JobStatus, {
  icon: React.ComponentType<{ className?: string }>;
  badge: string;
  label: string;
}> = {
  Queued: {
    icon: ClockIcon,
    badge: 'bg-slate-100 text-slate-900 dark:bg-slate-900/30 dark:text-slate-300',
    label: 'Queued',
  },
  Processing: {
    icon: LoaderIcon,
    badge: 'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300',
    label: 'Processing',
  },
  Completed: {
    icon: CheckCircleIcon,
    badge: 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
    label: 'Completed',
  },
  Failed: {
    icon: XCircleIcon,
    badge: 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300',
    label: 'Failed',
  },
  Cancelled: {
    icon: BanIcon,
    badge: 'bg-gray-100 text-gray-900 dark:bg-gray-900/30 dark:text-gray-300',
    label: 'Cancelled',
  },
};

// ── Component ──────────────────────────────────────────────────────────

export function ProcessingQueue() {
  const { connectionState } = useQueueSSE(true);
  const sseConnected = connectionState === 'connected';

  const { data, isLoading, error } = useQueueList(
    { pageSize: 10 },
    sseConnected
  );

  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const jobs = data?.jobs ?? [];

  const handleAction = async (jobId: string, action: 'cancel' | 'retry' | 'remove') => {
    setActionLoading(jobId);
    try {
      if (action === 'cancel') await cancelJob(jobId);
      if (action === 'retry') await retryJob(jobId);
      if (action === 'remove') await removeJob(jobId);
    } catch {
      // Actions will reflect in next SSE update
    } finally {
      setActionLoading(null);
    }
  };

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
      <div className="flex items-center justify-between mb-4">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100">
          Processing Queue
        </h2>
        <div className="flex items-center gap-1.5 text-xs">
          {sseConnected ? (
            <>
              <WifiIcon className="w-3.5 h-3.5 text-green-500" />
              <span className="text-green-600 dark:text-green-400">Live</span>
            </>
          ) : (
            <>
              <WifiOffIcon className="w-3.5 h-3.5 text-slate-400" />
              <span className="text-slate-500">Polling</span>
            </>
          )}
        </div>
      </div>

      {isLoading && (
        <div className="flex items-center justify-center py-8 text-sm text-slate-500">
          <LoaderIcon className="w-4 h-4 animate-spin mr-2" />
          Loading queue...
        </div>
      )}

      {error && (
        <div className="py-4 text-sm text-red-500 text-center">
          Failed to load queue. Retrying...
        </div>
      )}

      {!isLoading && !error && jobs.length === 0 && (
        <div className="py-8 text-sm text-slate-500 dark:text-zinc-400 text-center">
          No jobs in queue. Upload a PDF to get started.
        </div>
      )}

      <div className="space-y-3">
        {jobs.map((job: ProcessingJobDto) => {
          const config = statusConfig[job.status] ?? statusConfig.Queued;
          const StatusIcon = config.icon;
          const isActioning = actionLoading === job.id;

          return (
            <div
              key={job.id}
              className="p-3 bg-slate-50/50 dark:bg-zinc-900/50 rounded-lg border border-slate-200/50 dark:border-zinc-700/50"
            >
              <div className="flex items-center justify-between mb-1">
                <div className="flex items-center gap-2 min-w-0">
                  <FileIcon className="w-4 h-4 text-gray-500 dark:text-zinc-400 shrink-0" />
                  <span className="font-medium text-slate-900 dark:text-zinc-100 text-sm truncate">
                    {job.pdfFileName}
                  </span>
                </div>
                <Badge variant="outline" className={config.badge}>
                  <StatusIcon className={`w-3 h-3 mr-1 ${job.status === 'Processing' ? 'animate-spin' : ''}`} />
                  {config.label}
                </Badge>
              </div>

              {job.currentStep && job.status === 'Processing' && (
                <p className="text-xs text-blue-600 dark:text-blue-400 mb-1 ml-6">
                  Step: {job.currentStep}
                </p>
              )}

              {job.errorMessage && job.status === 'Failed' && (
                <p className="text-xs text-red-500 mb-1 ml-6 truncate" title={job.errorMessage}>
                  {job.errorMessage}
                </p>
              )}

              {/* Actions */}
              <div className="flex items-center gap-1 mt-1.5 ml-6">
                {job.status === 'Processing' && (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 px-2 text-xs"
                    disabled={isActioning}
                    onClick={() => handleAction(job.id, 'cancel')}
                  >
                    <BanIcon className="w-3 h-3 mr-1" />
                    Cancel
                  </Button>
                )}
                {job.status === 'Failed' && job.canRetry && (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 px-2 text-xs"
                    disabled={isActioning}
                    onClick={() => handleAction(job.id, 'retry')}
                  >
                    <RotateCcwIcon className="w-3 h-3 mr-1" />
                    Retry
                  </Button>
                )}
                {job.status === 'Queued' && (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 px-2 text-xs text-red-600"
                    disabled={isActioning}
                    onClick={() => handleAction(job.id, 'remove')}
                  >
                    <TrashIcon className="w-3 h-3 mr-1" />
                    Remove
                  </Button>
                )}
                <span className="text-xs text-slate-400 ml-auto">
                  {new Date(job.createdAt).toLocaleTimeString()}
                </span>
              </div>
            </div>
          );
        })}
      </div>

      {data && data.total > 10 && (
        <div className="mt-3 text-center">
          <a
            href="/admin/knowledge-base/queue"
            className="text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400"
          >
            View all {data.total} jobs →
          </a>
        </div>
      )}
    </div>
  );
}
