/**
 * Admin Access Requests Page
 *
 * Manage user access requests for invite-only registration.
 * Features: KPI stats cards, paginated table, status filters,
 * approve/reject actions, bulk approve, selection.
 */

'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircleIcon,
  CheckCircleIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ClockIcon,
  InboxIcon,
  RefreshCwIcon,
  UserCheckIcon,
  XCircleIcon,
} from 'lucide-react';
import { toast } from 'sonner';

import { RejectDialog } from '@/components/admin/access-requests/RejectDialog';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { AccessRequestDto } from '@/lib/api/clients/accessRequestsClient';

type StatusFilter = 'all' | 'Pending' | 'Approved' | 'Rejected';

const PAGE_SIZE = 20;
const BULK_APPROVE_MAX = 25;

function formatDate(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function StatusBadge({ status }: { status: AccessRequestDto['status'] }) {
  const variants: Record<AccessRequestDto['status'], string> = {
    Pending: 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300',
    Approved: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300',
    Rejected: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300',
  };
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${variants[status]}`}
    >
      {status}
    </span>
  );
}

export default function AccessRequestsPage() {
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [page, setPage] = useState(1);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [rejectTarget, setRejectTarget] = useState<AccessRequestDto | null>(null);

  // Fetch stats
  const statsQuery = useQuery({
    queryKey: ['admin', 'access-request-stats'],
    queryFn: () => api.accessRequests.getAccessRequestStats(),
    staleTime: 30_000,
  });

  // Fetch list
  const {
    data: listData,
    isLoading,
    error,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'access-requests', { page, pageSize: PAGE_SIZE, status: statusFilter }],
    queryFn: () =>
      api.accessRequests.getAccessRequests({
        status: statusFilter === 'all' ? undefined : statusFilter,
        page,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 30_000,
  });

  function invalidateAll() {
    queryClient.invalidateQueries({ queryKey: ['admin', 'access-requests'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'access-request-stats'] });
  }

  // Approve single
  const approveMutation = useMutation({
    mutationFn: (id: string) => api.accessRequests.approveAccessRequest(id),
    onSuccess: () => {
      toast.success('Access request approved');
      invalidateAll();
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Failed to approve request');
    },
  });

  // Reject single
  const rejectMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) =>
      api.accessRequests.rejectAccessRequest(id, reason),
    onSuccess: () => {
      toast.success('Access request rejected');
      invalidateAll();
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Failed to reject request');
    },
  });

  // Bulk approve
  const bulkApproveMutation = useMutation({
    mutationFn: (ids: string[]) => api.accessRequests.bulkApproveAccessRequests(ids),
    onSuccess: result => {
      toast.success(
        `Approved ${result.succeeded} of ${result.processed} requests` +
          (result.failed > 0 ? ` (${result.failed} failed)` : '')
      );
      setSelectedIds(new Set());
      invalidateAll();
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Bulk approve failed');
    },
  });

  const items = listData?.items ?? [];
  const totalCount = listData?.totalCount ?? 0;
  const totalPages = Math.ceil(totalCount / PAGE_SIZE);
  const stats = statsQuery.data;

  // Selection helpers
  const pageIds = items.map(i => i.id);
  const allPageSelected = pageIds.length > 0 && pageIds.every(id => selectedIds.has(id));
  const somePageSelected = pageIds.some(id => selectedIds.has(id));

  function toggleSelectAll() {
    if (allPageSelected) {
      const next = new Set(selectedIds);
      pageIds.forEach(id => next.delete(id));
      setSelectedIds(next);
    } else {
      const next = new Set(selectedIds);
      pageIds.forEach(id => next.add(id));
      setSelectedIds(next);
    }
  }

  function toggleSelect(id: string) {
    const next = new Set(selectedIds);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    setSelectedIds(next);
  }

  const selectedCount = selectedIds.size;
  const bulkDisabled =
    selectedCount === 0 || selectedCount > BULK_APPROVE_MAX || bulkApproveMutation.isPending;

  async function handleRejectConfirm(reason?: string) {
    if (!rejectTarget) return;
    await rejectMutation.mutateAsync({ id: rejectTarget.id, reason });
    setRejectTarget(null);
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Access Requests
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Review and manage user access requests for invite-only registration.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={isRefetching}
            className="gap-2"
          >
            <RefreshCwIcon className={`h-3.5 w-3.5 ${isRefetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button
            size="sm"
            onClick={() => bulkApproveMutation.mutate([...selectedIds])}
            disabled={bulkDisabled}
            className="gap-2"
          >
            <UserCheckIcon className="h-3.5 w-3.5" />
            Approve Selected
            {selectedCount > 0 && ` (${selectedCount})`}
          </Button>
        </div>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center">
                <InboxIcon className="h-5 w-5 text-blue-600 dark:text-blue-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Total</p>
                <span className="text-xl font-bold block">
                  {stats ? stats.total : <Skeleton className="h-6 w-10 inline-block" />}
                </span>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center">
                <ClockIcon className="h-5 w-5 text-amber-600 dark:text-amber-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Pending</p>
                <span className="text-xl font-bold block">
                  {stats ? stats.pending : <Skeleton className="h-6 w-10 inline-block" />}
                </span>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                <CheckCircleIcon className="h-5 w-5 text-green-600 dark:text-green-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Approved</p>
                <span className="text-xl font-bold block">
                  {stats ? stats.approved : <Skeleton className="h-6 w-10 inline-block" />}
                </span>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
                <XCircleIcon className="h-5 w-5 text-red-600 dark:text-red-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Rejected</p>
                <span className="text-xl font-bold block">
                  {stats ? stats.rejected : <Skeleton className="h-6 w-10 inline-block" />}
                </span>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Status Filter */}
      <div className="flex items-center gap-3">
        <Select
          value={statusFilter}
          onValueChange={v => {
            setStatusFilter(v as StatusFilter);
            setPage(1);
            setSelectedIds(new Set());
          }}
        >
          <SelectTrigger className="w-44 bg-white/70 dark:bg-zinc-800/70">
            <SelectValue placeholder="Filter by status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Statuses</SelectItem>
            <SelectItem value="Pending">Pending</SelectItem>
            <SelectItem value="Approved">Approved</SelectItem>
            <SelectItem value="Rejected">Rejected</SelectItem>
          </SelectContent>
        </Select>
        {selectedCount > BULK_APPROVE_MAX && (
          <p className="text-sm text-amber-600 dark:text-amber-400">
            Select up to {BULK_APPROVE_MAX} requests for bulk approve
          </p>
        )}
      </div>

      {/* Error State */}
      {error && (
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 rounded-lg p-4 flex items-center gap-3">
          <AlertCircleIcon className="h-5 w-5 text-red-500 shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-red-800 dark:text-red-200">
              Failed to load access requests
            </p>
            <p className="text-xs text-red-600 dark:text-red-400 mt-1">
              {error instanceof Error ? error.message : 'Unknown error'}
            </p>
          </div>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            Retry
          </Button>
        </div>
      )}

      {/* Table */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200/60 dark:border-zinc-700/40">
                <th className="p-3 w-10">
                  <input
                    type="checkbox"
                    className="rounded border-border"
                    checked={allPageSelected && pageIds.length > 0}
                    ref={el => {
                      if (el) el.indeterminate = somePageSelected && !allPageSelected;
                    }}
                    onChange={toggleSelectAll}
                    aria-label="Select all on page"
                  />
                </th>
                <th className="text-left p-3 font-medium text-muted-foreground">Email</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Status</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Requested At</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Reviewed By</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Actions</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i} className="border-b border-slate-100 dark:border-zinc-800/60">
                    <td className="p-3">
                      <Skeleton className="h-4 w-4" />
                    </td>
                    <td className="p-3">
                      <Skeleton className="h-4 w-48" />
                    </td>
                    <td className="p-3">
                      <Skeleton className="h-5 w-20" />
                    </td>
                    <td className="p-3">
                      <Skeleton className="h-4 w-24" />
                    </td>
                    <td className="p-3">
                      <Skeleton className="h-4 w-24" />
                    </td>
                    <td className="p-3">
                      <Skeleton className="h-8 w-32" />
                    </td>
                  </tr>
                ))
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={6} className="p-8 text-center text-muted-foreground">
                    <InboxIcon className="mx-auto mb-2 h-8 w-8 opacity-50" />
                    <p>
                      {statusFilter !== 'all'
                        ? 'No requests match this filter'
                        : 'No access requests yet'}
                    </p>
                  </td>
                </tr>
              ) : (
                items.map(item => (
                  <tr
                    key={item.id}
                    className="border-b border-slate-100 dark:border-zinc-800/60 hover:bg-slate-50/50 dark:hover:bg-zinc-800/30"
                  >
                    <td className="p-3">
                      <input
                        type="checkbox"
                        className="rounded border-border"
                        checked={selectedIds.has(item.id)}
                        onChange={() => toggleSelect(item.id)}
                        aria-label={`Select ${item.email}`}
                        disabled={item.status !== 'Pending'}
                      />
                    </td>
                    <td className="p-3 font-medium">{item.email}</td>
                    <td className="p-3">
                      <StatusBadge status={item.status} />
                    </td>
                    <td className="p-3 text-muted-foreground">{formatDate(item.requestedAt)}</td>
                    <td className="p-3 text-muted-foreground">{item.reviewedBy ?? '—'}</td>
                    <td className="p-3">
                      {item.status === 'Pending' && (
                        <div className="flex items-center gap-2">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-7 text-xs gap-1 text-green-700 border-green-200 hover:bg-green-50 dark:text-green-400 dark:border-green-800 dark:hover:bg-green-900/20"
                            onClick={() => approveMutation.mutate(item.id)}
                            disabled={approveMutation.isPending}
                          >
                            <CheckCircleIcon className="h-3 w-3" />
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-7 text-xs gap-1 text-red-700 border-red-200 hover:bg-red-50 dark:text-red-400 dark:border-red-800 dark:hover:bg-red-900/20"
                            onClick={() => setRejectTarget(item)}
                            disabled={rejectMutation.isPending}
                          >
                            <XCircleIcon className="h-3 w-3" />
                            Reject
                          </Button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-slate-200/60 dark:border-zinc-700/40">
            <p className="text-sm text-muted-foreground">
              Showing {(page - 1) * PAGE_SIZE + 1}&ndash;
              {Math.min(page * PAGE_SIZE, totalCount)} of {totalCount}
            </p>
            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
              >
                <ChevronLeftIcon className="h-4 w-4" />
              </Button>
              <span className="px-3 text-sm">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                disabled={page >= totalPages}
                onClick={() => setPage(p => p + 1)}
              >
                <ChevronRightIcon className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Reject Dialog */}
      <RejectDialog
        open={rejectTarget !== null}
        onOpenChange={open => {
          if (!open) setRejectTarget(null);
        }}
        onConfirm={handleRejectConfirm}
        email={rejectTarget?.email}
      />
    </div>
  );
}
