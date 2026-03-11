'use client';

/**
 * QueueTab — PDF processing queue management
 * Issue #128 — Queue Manager Tab (stub for #126 page setup)
 */

import { useCallback, useEffect, useState } from 'react';

import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Loader2,
  PauseCircle,
  RotateCcw,
  Trash2,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { PaginatedQueue, QueueStatus } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

const STATUS_CONFIG: Record<
  string,
  {
    label: string;
    variant: 'default' | 'secondary' | 'destructive' | 'outline';
    icon: React.ReactNode;
  }
> = {
  Pending: { label: 'Pending', variant: 'outline', icon: <Clock className="h-3 w-3" /> },
  Processing: {
    label: 'Processing',
    variant: 'default',
    icon: <Loader2 className="h-3 w-3 animate-spin" />,
  },
  Completed: {
    label: 'Completed',
    variant: 'secondary',
    icon: <CheckCircle2 className="h-3 w-3" />,
  },
  Failed: { label: 'Failed', variant: 'destructive', icon: <XCircle className="h-3 w-3" /> },
  Cancelled: { label: 'Cancelled', variant: 'outline', icon: <PauseCircle className="h-3 w-3" /> },
};

export function QueueTab() {
  const [queue, setQueue] = useState<PaginatedQueue | null>(null);
  const [status, setStatus] = useState<QueueStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const fetchData = useCallback(async () => {
    try {
      const [queueRes, statusRes] = await Promise.all([
        api.admin.getProcessingQueue({
          status: statusFilter || undefined,
          search: search || undefined,
          page,
          pageSize: 20,
        }),
        api.admin.getQueueStatus(),
      ]);
      setQueue(queueRes);
      setStatus(statusRes);
    } catch {
      // silent
    } finally {
      setLoading(false);
    }
  }, [statusFilter, search, page]);

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 15_000);
    return () => clearInterval(interval);
  }, [fetchData]);

  const handleCancel = async (jobId: string) => {
    await api.admin.cancelJob(jobId);
    fetchData();
  };

  const handleRetry = async (jobId: string) => {
    await api.admin.retryJob(jobId);
    fetchData();
  };

  const handleRemove = async (jobId: string) => {
    await api.admin.removeJob(jobId);
    fetchData();
  };

  return (
    <div className="space-y-6" data-testid="queue-tab">
      <div>
        <h2 className="font-quicksand text-lg font-semibold">Processing Queue</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          PDF processing jobs, status, and queue health.
        </p>
      </div>

      {/* Queue Status Banner */}
      {status && (
        <div
          className={cn(
            'rounded-lg border p-3 flex items-center gap-3',
            status.isPaused
              ? 'border-red-300 bg-red-50 dark:bg-red-950/20'
              : status.isUnderPressure
                ? 'border-amber-300 bg-amber-50 dark:bg-amber-950/20'
                : 'border-green-300 bg-green-50 dark:bg-green-950/20'
          )}
          data-testid="queue-status-banner"
        >
          {status.isPaused ? (
            <PauseCircle className="h-5 w-5 text-red-600" />
          ) : status.isUnderPressure ? (
            <AlertCircle className="h-5 w-5 text-amber-600" />
          ) : (
            <CheckCircle2 className="h-5 w-5 text-green-600" />
          )}
          <div className="flex-1">
            <p className="text-sm font-medium">
              {status.isPaused
                ? 'Queue Paused'
                : status.isUnderPressure
                  ? 'Queue Under Pressure'
                  : 'Queue Healthy'}
            </p>
            <p className="text-xs text-muted-foreground">
              Depth: {status.queueDepth} · Workers: {status.maxConcurrentWorkers} · Est. wait:{' '}
              {status.estimatedWaitMinutes.toFixed(0)}min
            </p>
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <Input
          placeholder="Search by file name..."
          value={search}
          onChange={e => {
            setSearch(e.target.value);
            setPage(1);
          }}
          className="w-64"
          data-testid="queue-search-input"
        />
        <select
          value={statusFilter}
          onChange={e => {
            setStatusFilter(e.target.value);
            setPage(1);
          }}
          className="rounded-md border bg-background px-3 py-2 text-sm"
          data-testid="queue-status-filter"
        >
          <option value="">All Statuses</option>
          <option value="Pending">Pending</option>
          <option value="Processing">Processing</option>
          <option value="Completed">Completed</option>
          <option value="Failed">Failed</option>
          <option value="Cancelled">Cancelled</option>
        </select>
      </div>

      {/* Jobs Table */}
      {loading ? (
        <div className="flex items-center justify-center py-10">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-xs text-muted-foreground">
                <th className="text-left py-3 px-4">File</th>
                <th className="text-left py-3 px-3">Status</th>
                <th className="text-right py-3 px-3">Priority</th>
                <th className="text-left py-3 px-3">Created</th>
                <th className="text-left py-3 px-3">Step</th>
                <th className="text-right py-3 px-4">Actions</th>
              </tr>
            </thead>
            <tbody>
              {queue?.jobs.map(job => {
                const cfg = STATUS_CONFIG[job.status] ?? STATUS_CONFIG.Pending;
                return (
                  <tr key={job.id} className="border-b last:border-0">
                    <td className="py-3 px-4 font-mono text-xs max-w-[200px] truncate">
                      {job.pdfFileName}
                    </td>
                    <td className="py-3 px-3">
                      <Badge variant={cfg.variant} className="gap-1">
                        {cfg.icon}
                        {cfg.label}
                      </Badge>
                    </td>
                    <td className="py-3 px-3 text-right">{job.priority}</td>
                    <td className="py-3 px-3 text-xs">
                      {new Date(job.createdAt).toLocaleString()}
                    </td>
                    <td className="py-3 px-3 text-xs text-muted-foreground">
                      {job.currentStep ?? '—'}
                    </td>
                    <td className="py-3 px-4 text-right">
                      <div className="flex items-center justify-end gap-1">
                        {(job.status === 'Pending' || job.status === 'Processing') && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleCancel(job.id)}
                            aria-label="Cancel job"
                          >
                            <XCircle className="h-4 w-4" />
                          </Button>
                        )}
                        {job.status === 'Failed' && job.canRetry && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleRetry(job.id)}
                            aria-label="Retry job"
                          >
                            <RotateCcw className="h-4 w-4" />
                          </Button>
                        )}
                        {(job.status === 'Completed' || job.status === 'Cancelled') && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleRemove(job.id)}
                            aria-label="Remove job"
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
              {(!queue || queue.jobs.length === 0) && (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-sm text-muted-foreground">
                    No jobs found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination */}
      {queue && queue.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-muted-foreground">
            Page {queue.page} of {queue.totalPages} ({queue.total} total)
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => setPage(p => p - 1)}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= queue.totalPages}
              onClick={() => setPage(p => p + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
