/**
 * JobDetailModal Component
 * Issue #3693 - Batch Job System
 *
 * Detailed view of a batch job with parameters, results, logs, and actions.
 * Features:
 * - Job info display (type, status, created, duration)
 * - Parameters section (JSON pretty-print)
 * - Results section (summary + download link if available)
 * - Logs section (real-time for running jobs - mocked for now)
 * - Error details (if failed)
 * - Action buttons (Retry, Cancel, Close)
 */

'use client';

import { useEffect, useState } from 'react';

import {
  AlertCircleIcon,
  CheckCircleIcon,
  ClockIcon,
  DownloadIcon,
  PlayCircleIcon,
  StopCircleIcon,
  XIcon,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { api } from '@/lib/api';
import type { BatchJobDto, BatchJobStatus } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

interface JobDetailModalProps {
  job: BatchJobDto;
  open: boolean;
  onClose: () => void;
  onAction: () => void;
}

function getStatusIcon(status: BatchJobStatus) {
  switch (status) {
    case 'Queued':
      return <ClockIcon className="h-5 w-5 text-zinc-500" />;
    case 'Running':
      return <PlayCircleIcon className="h-5 w-5 text-blue-500 animate-pulse" />;
    case 'Completed':
      return <CheckCircleIcon className="h-5 w-5 text-green-500" />;
    case 'Failed':
      return <AlertCircleIcon className="h-5 w-5 text-red-500" />;
    case 'Cancelled':
      return <StopCircleIcon className="h-5 w-5 text-yellow-500" />;
    default:
      return null;
  }
}

function formatDuration(seconds: number | null): string {
  if (!seconds) return 'Not started';
  if (seconds < 60) return `${Math.round(seconds)}s`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${Math.round(seconds % 60)}s`;
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  return `${hours}h ${minutes}m`;
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return 'N/A';
  const date = new Date(dateStr);
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(date);
}

export function JobDetailModal({ job, open, onClose, onAction }: JobDetailModalProps) {
  const [actionLoading, setActionLoading] = useState(false);
  const [mockLogs] = useState([
    { timestamp: new Date().toISOString(), level: 'INFO', message: 'Job queued' },
    { timestamp: new Date().toISOString(), level: 'INFO', message: 'Job started processing' },
    { timestamp: new Date().toISOString(), level: 'INFO', message: 'Processing data...' },
  ]);

  const handleCancel = async () => {
    setActionLoading(true);
    try {
      await api.admin.cancelBatchJob(job.id);
      toast.success('Job cancelled successfully');
      onAction();
    } catch (error) {
      toast.error('Failed to cancel job');
      console.error('Cancel job error:', error);
    } finally {
      setActionLoading(false);
    }
  };

  const handleRetry = async () => {
    setActionLoading(true);
    try {
      await api.admin.retryBatchJob(job.id);
      toast.success('Job retried successfully');
      onAction();
      onClose();
    } catch (error) {
      toast.error('Failed to retry job');
      console.error('Retry job error:', error);
    } finally {
      setActionLoading(false);
    }
  };

  // Auto-refresh running jobs
  useEffect(() => {
    if (job.status !== 'Running') return;

    const interval = setInterval(async () => {
      try {
        const updatedJob = await api.admin.getBatchJob(job.id);
        if (updatedJob.status !== 'Running') {
          onAction();
        }
      } catch {
        // Fail silently
      }
    }, 3000);

    return () => clearInterval(interval);
  }, [job.id, job.status, onAction]);

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-hidden flex flex-col">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-3">
            {getStatusIcon(job.status)}
            <span>Batch Job Details</span>
            <Badge variant="outline" className="ml-auto">
              {job.type}
            </Badge>
          </DialogTitle>
        </DialogHeader>

        <ScrollArea className="flex-1 pr-4">
          <div className="space-y-6">
            {/* Job Info */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <p className="text-sm font-medium text-zinc-700 dark:text-zinc-300">Status</p>
                <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1">{job.status}</p>
              </div>
              <div>
                <p className="text-sm font-medium text-zinc-700 dark:text-zinc-300">Duration</p>
                <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1 tabular-nums">
                  {formatDuration(job.duration)}
                </p>
              </div>
              <div>
                <p className="text-sm font-medium text-zinc-700 dark:text-zinc-300">Created</p>
                <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1">
                  {formatDate(job.createdAt)}
                </p>
              </div>
              <div>
                <p className="text-sm font-medium text-zinc-700 dark:text-zinc-300">Started</p>
                <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1">
                  {formatDate(job.startedAt)}
                </p>
              </div>
              <div>
                <p className="text-sm font-medium text-zinc-700 dark:text-zinc-300">Completed</p>
                <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1">
                  {formatDate(job.completedAt)}
                </p>
              </div>
              <div>
                <p className="text-sm font-medium text-zinc-700 dark:text-zinc-300">Progress</p>
                <p className="text-sm text-zinc-600 dark:text-zinc-400 mt-1 tabular-nums">
                  {job.progress}%
                </p>
              </div>
            </div>

            {/* Parameters */}
            <div>
              <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100 mb-2">
                Parameters
              </h3>
              {job.parameters ? (
                <pre className="bg-zinc-100 dark:bg-zinc-800 rounded-lg p-3 text-xs overflow-x-auto">
                  {JSON.stringify(job.parameters, null, 2)}
                </pre>
              ) : (
                <p className="text-sm text-zinc-500 dark:text-zinc-400">No parameters</p>
              )}
            </div>

            {/* Results */}
            {job.status === 'Completed' && (
              <div>
                <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100 mb-2">
                  Results
                </h3>
                {job.results ? (
                  <>
                    <pre className="bg-zinc-100 dark:bg-zinc-800 rounded-lg p-3 text-xs overflow-x-auto mb-2">
                      {JSON.stringify(job.results, null, 2)}
                    </pre>
                    <Button variant="outline" size="sm">
                      <DownloadIcon className="h-4 w-4 mr-2" />
                      Download Results
                    </Button>
                  </>
                ) : (
                  <p className="text-sm text-zinc-500 dark:text-zinc-400">No results available</p>
                )}
              </div>
            )}

            {/* Error Details */}
            {job.status === 'Failed' && job.errorMessage && (
              <div>
                <h3 className="text-sm font-semibold text-red-600 dark:text-red-400 mb-2">
                  Error Details
                </h3>
                <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3">
                  <p className="text-sm text-red-700 dark:text-red-300 font-mono">
                    {job.errorMessage}
                  </p>
                </div>
              </div>
            )}

            {/* Logs */}
            <div>
              <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100 mb-2">
                Logs {job.status === 'Running' && '(Live)'}
              </h3>
              <div className="bg-zinc-900 dark:bg-zinc-950 rounded-lg p-3 max-h-48 overflow-y-auto">
                {mockLogs.map((log, i) => (
                  <div key={i} className="text-xs font-mono mb-1">
                    <span className="text-zinc-500">{new Date(log.timestamp).toLocaleTimeString()}</span>
                    <span className={cn(
                      'ml-2 font-semibold',
                      log.level === 'ERROR' ? 'text-red-400' : 'text-blue-400'
                    )}>
                      {log.level}
                    </span>
                    <span className="ml-2 text-zinc-300">{log.message}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </ScrollArea>

        {/* Actions */}
        <div className="flex items-center justify-end gap-2 pt-4 border-t border-zinc-200 dark:border-zinc-800">
          {job.status === 'Failed' && (
            <Button
              onClick={handleRetry}
              disabled={actionLoading}
            >
              <PlayCircleIcon className="h-4 w-4 mr-2" />
              Retry
            </Button>
          )}
          {(job.status === 'Queued' || job.status === 'Running') && (
            <Button
              variant="destructive"
              onClick={handleCancel}
              disabled={actionLoading}
            >
              <StopCircleIcon className="h-4 w-4 mr-2" />
              Cancel
            </Button>
          )}
          <Button variant="outline" onClick={onClose}>
            <XIcon className="h-4 w-4 mr-2" />
            Close
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
