'use client';

/**
 * Admin KB Processing Queue Client
 * Issue #4892 - Lists processing jobs with status filtering and retry support
 */

import { useCallback, useEffect, useState } from 'react';

import { ArrowLeft, ChevronLeft, ChevronRight, RefreshCw, RotateCcw, Search } from 'lucide-react';
import Link from 'next/link';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Badge } from '@/components/ui/data-display/badge';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { ProcessingJobDto } from '@/lib/api/schemas';

// ─── Constants ────────────────────────────────────────────────────────────────

const PAGE_SIZE = 20;

const STATUS_OPTIONS = [
  { value: '', label: 'All Statuses' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Processing', label: 'Processing' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

// ─── Types ────────────────────────────────────────────────────────────────────

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

function statusVariant(
  status: string
): 'default' | 'secondary' | 'destructive' | 'outline' {
  switch (status.toLowerCase()) {
    case 'completed':
      return 'default';
    case 'failed':
      return 'destructive';
    case 'processing':
      return 'secondary';
    default:
      return 'outline';
  }
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function TableSkeleton() {
  return (
    <div className="space-y-3">
      {Array.from({ length: 5 }).map((_, i) => (
        <Skeleton key={i} className="h-14 w-full" />
      ))}
    </div>
  );
}

function JobRow({
  job,
  onRetry,
  retrying,
}: {
  job: ProcessingJobDto;
  onRetry: (jobId: string, pdfDocumentId: string) => void;
  retrying: string | null;
}) {
  return (
    <tr className="border-b last:border-0 hover:bg-muted/30 transition-colors">
      <td className="py-3 pr-4">
        <p className="font-medium text-sm truncate max-w-[200px]" title={job.pdfFileName}>
          {job.pdfFileName}
        </p>
        <p className="text-xs text-muted-foreground font-mono">{job.pdfDocumentId.slice(0, 8)}…</p>
      </td>
      <td className="py-3 pr-4">
        <Badge variant={statusVariant(job.status)}>{job.status}</Badge>
      </td>
      <td className="py-3 pr-4 text-sm text-muted-foreground">
        {job.currentStep ?? '—'}
      </td>
      <td className="py-3 pr-4 text-sm text-right">{job.priority}</td>
      <td className="py-3 pr-4 text-sm text-muted-foreground">
        <div>{formatDate(job.createdAt)}</div>
        {job.startedAt && (
          <div className="text-xs">Started: {formatDate(job.startedAt)}</div>
        )}
      </td>
      <td className="py-3 pr-4 text-sm">
        {job.retryCount > 0 && (
          <span className="text-amber-600">
            {job.retryCount}/{job.maxRetries}
          </span>
        )}
        {job.errorMessage && (
          <p
            className="text-xs text-destructive truncate max-w-[180px]"
            title={job.errorMessage}
          >
            {job.errorMessage}
          </p>
        )}
      </td>
      <td className="py-3 text-right">
        {job.canRetry && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onRetry(job.id, job.pdfDocumentId)}
            disabled={retrying === job.id}
            title="Reindex this document"
          >
            <RotateCcw
              className={`h-4 w-4 ${retrying === job.id ? 'animate-spin' : ''}`}
            />
          </Button>
        )}
      </td>
    </tr>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export function ProcessingQueueClient() {
  const { user, loading: authLoading } = useAuthUser();

  const [jobs, setJobs] = useState<ProcessingJobDto[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [searchText, setSearchText] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [dataLoading, setDataLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retrying, setRetrying] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts((prev) => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 5000);
  }, []);

  const fetchJobs = useCallback(async () => {
    setDataLoading(true);
    setError(null);
    try {
      const result = await api.admin.getProcessingQueueAdmin({
        statusFilter: statusFilter || undefined,
        searchText: searchText || undefined,
        page,
        pageSize: PAGE_SIZE,
      });
      setJobs(result.jobs);
      setTotal(result.total);
      setTotalPages(result.totalPages);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to load processing queue';
      setError(msg);
      addToast('error', msg);
    } finally {
      setDataLoading(false);
    }
  }, [statusFilter, searchText, page, addToast]);

  useEffect(() => {
    fetchJobs();
  }, [fetchJobs]);

  const handleSearch = useCallback(() => {
    setSearchText(searchInput);
    setPage(1);
  }, [searchInput]);

  const handleStatusFilter = useCallback((value: string) => {
    setStatusFilter(value);
    setPage(1);
  }, []);

  const handleRetry = useCallback(
    async (jobId: string, pdfDocumentId: string) => {
      setRetrying(jobId);
      try {
        await api.admin.reindexPdf(pdfDocumentId);
        addToast('success', 'Reindex queued successfully');
        await fetchJobs();
      } catch (err) {
        addToast('error', err instanceof Error ? err.message : 'Failed to reindex');
      } finally {
        setRetrying(null);
      }
    },
    [addToast, fetchJobs]
  );

  return (
    <AdminAuthGuard user={user} loading={authLoading}>
      {/* Toast container */}
      <div className="fixed top-4 right-4 z-50 flex flex-col gap-2">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={`px-4 py-3 rounded-lg shadow-lg text-sm font-medium text-white ${
              toast.type === 'success'
                ? 'bg-green-600'
                : toast.type === 'error'
                  ? 'bg-red-600'
                  : 'bg-blue-600'
            }`}
          >
            {toast.message}
          </div>
        ))}
      </div>

      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <Button asChild variant="ghost" size="sm" className="mb-2 -ml-2 font-nunito">
              <Link href="/admin/knowledge-base">
                <ArrowLeft className="h-4 w-4 mr-1" /> Knowledge Base
              </Link>
            </Button>
            <h1 className="text-2xl font-bold font-quicksand">Processing Queue</h1>
            <p className="text-muted-foreground text-sm font-nunito">
              {total > 0 ? `${total.toLocaleString()} total jobs` : 'Document processing pipeline'}
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={fetchJobs}
            disabled={dataLoading}
            className="font-nunito"
          >
            <RefreshCw className={`h-4 w-4 mr-2 ${dataLoading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>

        {/* Filters */}
        <div className="flex flex-col sm:flex-row gap-3">
          <div className="flex gap-2 flex-1">
            <Input
              placeholder="Search by file name..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              className="max-w-sm font-nunito"
            />
            <Button variant="outline" size="sm" onClick={handleSearch} className="shrink-0">
              <Search className="h-4 w-4" />
            </Button>
          </div>
          <select
            value={statusFilter}
            onChange={(e) => handleStatusFilter(e.target.value)}
            className="border rounded-md px-3 py-1.5 text-sm bg-background font-nunito"
          >
            {STATUS_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Error */}
        {error && !dataLoading && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {/* Table */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Jobs</CardTitle>
          </CardHeader>
          <CardContent>
            {dataLoading ? (
              <TableSkeleton />
            ) : jobs.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-8">
                No jobs found matching the current filters.
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b">
                      <th className="text-left py-2 pr-4 font-medium text-muted-foreground">
                        File
                      </th>
                      <th className="text-left py-2 pr-4 font-medium text-muted-foreground">
                        Status
                      </th>
                      <th className="text-left py-2 pr-4 font-medium text-muted-foreground">
                        Step
                      </th>
                      <th className="text-right py-2 pr-4 font-medium text-muted-foreground">
                        Priority
                      </th>
                      <th className="text-left py-2 pr-4 font-medium text-muted-foreground">
                        Dates
                      </th>
                      <th className="text-left py-2 pr-4 font-medium text-muted-foreground">
                        Retries / Error
                      </th>
                      <th className="text-right py-2 font-medium text-muted-foreground">
                        Actions
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {jobs.map((job) => (
                      <JobRow
                        key={job.id}
                        job={job}
                        onRetry={handleRetry}
                        retrying={retrying}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between text-sm font-nunito">
            <span className="text-muted-foreground">
              Page {page} of {totalPages}
            </span>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1 || dataLoading}
              >
                <ChevronLeft className="h-4 w-4" />
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages || dataLoading}
              >
                Next
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </div>
    </AdminAuthGuard>
  );
}
