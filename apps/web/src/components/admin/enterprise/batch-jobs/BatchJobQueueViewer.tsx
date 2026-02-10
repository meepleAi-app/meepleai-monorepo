/**
 * BatchJobQueueViewer Component
 * Issue #3693 - Batch Job System
 *
 * Table displaying batch jobs with status, progress, and actions.
 * Features:
 * - Status badges with color coding
 * - Progress bars for running jobs
 * - Duration calculation
 * - Row click to open detail modal
 * - Action buttons (Cancel, Retry, Delete)
 */

'use client';

import { useState } from 'react';

import {
  AlertCircleIcon,
  CheckCircleIcon,
  ClockIcon,
  PlayCircleIcon,
  StopCircleIcon,
  TrashIcon,
  XCircleIcon,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { BatchJobDto, BatchJobStatus } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

interface BatchJobQueueViewerProps {
  jobs: BatchJobDto[];
  loading: boolean;
  onJobClick: (job: BatchJobDto) => void;
}

function getStatusBadge(status: BatchJobStatus) {
  switch (status) {
    case 'Queued':
      return (
        <Badge variant="outline" className="bg-zinc-100 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300">
          <ClockIcon className="h-3 w-3 mr-1" />
          Queued
        </Badge>
      );
    case 'Running':
      return (
        <Badge className="bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 border-blue-300 dark:border-blue-700">
          <PlayCircleIcon className="h-3 w-3 mr-1" />
          Running
        </Badge>
      );
    case 'Completed':
      return (
        <Badge className="bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300 border-green-300 dark:border-green-700">
          <CheckCircleIcon className="h-3 w-3 mr-1" />
          Completed
        </Badge>
      );
    case 'Failed':
      return (
        <Badge className="bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300 border-red-300 dark:border-red-700">
          <AlertCircleIcon className="h-3 w-3 mr-1" />
          Failed
        </Badge>
      );
    case 'Cancelled':
      return (
        <Badge className="bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-300 border-yellow-300 dark:border-yellow-700">
          <XCircleIcon className="h-3 w-3 mr-1" />
          Cancelled
        </Badge>
      );
    default:
      return <Badge variant="outline">{status}</Badge>;
  }
}

function formatDuration(seconds: number | null): string {
  if (!seconds) return '-';
  if (seconds < 60) return `${Math.round(seconds)}s`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${Math.round(seconds % 60)}s`;
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  return `${hours}h ${minutes}m`;
}

function formatDate(dateStr: string): string {
  const date = new Date(dateStr);
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

export function BatchJobQueueViewer({ jobs, loading, onJobClick }: BatchJobQueueViewerProps) {
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const handleCancel = async (e: React.MouseEvent, jobId: string) => {
    e.stopPropagation();
    setActionLoading(jobId);
    try {
      await api.admin.cancelBatchJob(jobId);
      toast.success('Job cancelled successfully');
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      onJobClick(jobs.find(j => j.id === jobId)!);
    } catch (error) {
      toast.error('Failed to cancel job');
      console.error('Cancel job error:', error);
    } finally {
      setActionLoading(null);
    }
  };

  const handleRetry = async (e: React.MouseEvent, jobId: string) => {
    e.stopPropagation();
    setActionLoading(jobId);
    try {
      await api.admin.retryBatchJob(jobId);
      toast.success('Job retried successfully');
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      onJobClick(jobs.find(j => j.id === jobId)!);
    } catch (error) {
      toast.error('Failed to retry job');
      console.error('Retry job error:', error);
    } finally {
      setActionLoading(null);
    }
  };

  const handleDelete = async (e: React.MouseEvent, jobId: string) => {
    e.stopPropagation();
    if (!confirm('Are you sure you want to delete this job?')) return;

    setActionLoading(jobId);
    try {
      await api.admin.deleteBatchJob(jobId);
      toast.success('Job deleted successfully');
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      onJobClick(jobs.find(j => j.id === jobId)!);
    } catch (error) {
      toast.error('Failed to delete job');
      console.error('Delete job error:', error);
    } finally {
      setActionLoading(null);
    }
  };

  if (loading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-16 bg-muted/30 rounded animate-pulse" />
        ))}
      </div>
    );
  }

  if (jobs.length === 0) {
    return (
      <div className="text-center py-12 text-zinc-500 dark:text-zinc-400">
        No batch jobs found
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-zinc-200 dark:border-zinc-800 overflow-hidden">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Type</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Progress</TableHead>
            <TableHead>Duration</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {jobs.map((job) => (
            <TableRow
              key={job.id}
              onClick={() => onJobClick(job)}
              className={cn('cursor-pointer hover:bg-muted/50')}
              data-testid={`batch-job-row-${job.id}`}
            >
              <TableCell className="font-medium">{job.type}</TableCell>
              <TableCell>{getStatusBadge(job.status)}</TableCell>
              <TableCell>
                {job.status === 'Running' ? (
                  <div className="flex items-center gap-2">
                    <Progress value={job.progress} className="h-2 w-24" />
                    <span className="text-xs text-zinc-600 dark:text-zinc-400 tabular-nums">
                      {job.progress}%
                    </span>
                  </div>
                ) : (
                  <span className="text-sm text-zinc-500 dark:text-zinc-400">-</span>
                )}
              </TableCell>
              <TableCell className="text-sm text-zinc-600 dark:text-zinc-400 tabular-nums">
                {formatDuration(job.duration)}
              </TableCell>
              <TableCell className="text-sm text-zinc-600 dark:text-zinc-400">
                {formatDate(job.createdAt)}
              </TableCell>
              <TableCell className="text-right">
                <div className="flex items-center justify-end gap-2">
                  {(job.status === 'Queued' || job.status === 'Running') && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={(e) => handleCancel(e, job.id)}
                      disabled={actionLoading === job.id}
                      title="Cancel job"
                    >
                      <StopCircleIcon className="h-4 w-4" />
                    </Button>
                  )}
                  {job.status === 'Failed' && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={(e) => handleRetry(e, job.id)}
                      disabled={actionLoading === job.id}
                      title="Retry job"
                    >
                      <PlayCircleIcon className="h-4 w-4" />
                    </Button>
                  )}
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={(e) => handleDelete(e, job.id)}
                    disabled={actionLoading === job.id}
                    title="Delete job"
                  >
                    <TrashIcon className="h-4 w-4 text-red-500" />
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
