'use client';
/* eslint-disable @typescript-eslint/no-non-null-assertion -- pre-existing pattern: array/object access guarded by length/key check or by upstream validator; assertion is correct by construction. Cleanup tracked for follow-up audit. */

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ChevronLeftIcon, ChevronRightIcon, HistoryIcon, Loader2Icon } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { apiClient } from '@/lib/api/client';

import { useQueueList, type JobStatus } from '../../queue/lib/queue-api';

// ── PDF Distribution Types ────────────────────────────────────────────

interface PdfStatusCount {
  status: string;
  count: number;
}

type PdfDistribution = PdfStatusCount[];

async function fetchPdfDistribution(): Promise<PdfDistribution> {
  const result = await apiClient.get<PdfDistribution>('/api/v1/admin/pdfs/analytics/distribution');
  return result ?? [];
}

function usePdfDistribution() {
  return useQuery({
    queryKey: ['admin', 'pdfs', 'distribution'],
    queryFn: fetchPdfDistribution,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });
}

// ── Helpers ───────────────────────────────────────────────────────────

function formatDuration(startedAt: string | null, completedAt: string | null): string {
  if (!startedAt || !completedAt) return '\u2014';
  const ms = new Date(completedAt).getTime() - new Date(startedAt).getTime();
  if (ms < 1000) return `${ms}ms`;
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const remainingSec = seconds % 60;
  return `${minutes}m ${remainingSec}s`;
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// ── Main Component ────────────────────────────────────────────────────

export function HistoryTab() {
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Completed jobs
  const { data: completedData, isLoading: completedLoading } = useQueueList(
    { status: 'Completed' as JobStatus, page, pageSize },
    false
  );

  // PDF distribution
  const { data: distribution, isLoading: distLoading } = usePdfDistribution();

  const totalPages = completedData?.totalPages ?? 1;

  return (
    <div className="space-y-4">
      {/* Distribution Cards */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center gap-2">
            <HistoryIcon className="h-4 w-4" />
            PDF Distribution
          </CardTitle>
        </CardHeader>
        <CardContent>
          {distLoading ? (
            <div className="flex items-center justify-center py-6">
              <Loader2Icon className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
              {(distribution ?? []).map(item => (
                <div
                  key={item.status}
                  className="rounded-lg border bg-white/50 dark:bg-zinc-800/50 p-3 text-center"
                >
                  <p className="text-xs text-muted-foreground">{item.status}</p>
                  <p className="text-xl font-bold tabular-nums">{item.count}</p>
                </div>
              ))}
              {(distribution ?? []).length === 0 && (
                <p className="col-span-full text-sm text-muted-foreground text-center py-4">
                  No distribution data available
                </p>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Completed Jobs Table */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Completed PDFs</CardTitle>
        </CardHeader>
        <CardContent>
          {completedLoading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2Icon className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : (completedData?.jobs ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-8">No completed jobs yet</p>
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="pb-2 font-medium">Filename</th>
                      <th className="pb-2 font-medium">Duration</th>
                      <th className="pb-2 font-medium">Completed</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {completedData!.jobs.map(job => (
                      <tr key={job.id} className="hover:bg-muted/50">
                        <td className="py-2 pr-4 truncate max-w-[280px]" title={job.pdfFileName}>
                          {job.pdfFileName}
                        </td>
                        <td className="py-2 pr-4 tabular-nums whitespace-nowrap">
                          {formatDuration(job.startedAt, job.completedAt)}
                        </td>
                        <td className="py-2 whitespace-nowrap text-muted-foreground">
                          {job.completedAt ? formatDate(job.completedAt) : '\u2014'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex items-center justify-between pt-4 border-t mt-4">
                  <span className="text-xs text-muted-foreground">
                    Page {page} of {totalPages} ({completedData!.total} total)
                  </span>
                  <div className="flex gap-1">
                    <button
                      type="button"
                      disabled={page <= 1}
                      onClick={() => setPage(p => Math.max(1, p - 1))}
                      className="p-1.5 rounded hover:bg-muted disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                      <ChevronLeftIcon className="h-4 w-4" />
                    </button>
                    <button
                      type="button"
                      disabled={page >= totalPages}
                      onClick={() => setPage(p => p + 1)}
                      className="p-1.5 rounded hover:bg-muted disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                      <ChevronRightIcon className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
