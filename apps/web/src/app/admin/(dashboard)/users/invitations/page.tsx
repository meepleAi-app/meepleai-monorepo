/**
 * Admin Invitations Page (Issue #132)
 *
 * Dedicated page for managing user invitations.
 * Features: KPI stats cards, invitation table with pagination,
 * status filters, send/bulk invite dialogs, resend/revoke actions.
 */

'use client';

import { useEffect, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircleIcon,
  CheckCircleIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ClockIcon,
  MailIcon,
  PlusIcon,
  RefreshCwIcon,
  UploadIcon,
  XCircleIcon,
} from 'lucide-react';
import { toast } from 'sonner';

import { BulkInviteDialog } from '@/components/admin/invitations/BulkInviteDialog';
import { InvitationRow } from '@/components/admin/invitations/InvitationRow';
import { InviteUserDialog } from '@/components/admin/invitations/InviteUserDialog';
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
import { useSetNavConfig } from '@/hooks/useSetNavConfig';
import { api } from '@/lib/api';
import type { InvitationStatus } from '@/lib/api/schemas/invitation.schemas';

type StatusFilter = 'all' | InvitationStatus;

const PAGE_SIZE = 20;

export default function InvitationsPage() {
  const setNavConfig = useSetNavConfig();
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [page, setPage] = useState(1);
  const [inviteDialogOpen, setInviteDialogOpen] = useState(false);
  const [bulkDialogOpen, setBulkDialogOpen] = useState(false);

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'users', label: 'All Users', href: '/admin/users' },
        { id: 'invitations', label: 'Invitations', href: '/admin/users/invitations' },
        { id: 'roles', label: 'Roles & Permissions', href: '/admin/users/roles' },
        { id: 'activity', label: 'Activity Log', href: '/admin/users/activity' },
      ],
      actionBar: [],
    });
  }, [setNavConfig]);

  // Fetch stats
  const statsQuery = useQuery({
    queryKey: ['admin', 'invitation-stats'],
    queryFn: () => api.invitations.getInvitationStats(),
    staleTime: 30_000,
  });

  // Fetch invitations list
  const {
    data: invitationsData,
    isLoading,
    error,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'invitations', { page, pageSize: PAGE_SIZE, status: statusFilter }],
    queryFn: () =>
      api.invitations.getInvitations({
        status: statusFilter === 'all' ? undefined : statusFilter,
        page,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 30_000,
  });

  // Mutations
  const resendMutation = useMutation({
    mutationFn: (id: string) => api.invitations.resendInvitation(id),
    onSuccess: () => {
      toast.success('Invitation resent successfully');
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitation-stats'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Failed to resend invitation');
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (id: string) => api.invitations.revokeInvitation(id),
    onSuccess: () => {
      toast.success('Invitation revoked');
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitation-stats'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Failed to revoke invitation');
    },
  });

  function handleInviteSuccess() {
    queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'invitation-stats'] });
  }

  const invitations = invitationsData?.items ?? [];
  const totalCount = invitationsData?.totalCount ?? 0;
  const totalPages = Math.ceil(totalCount / PAGE_SIZE);
  const stats = statsQuery.data;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Invitations
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Manage user invitations and track their status.
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
            variant="outline"
            size="sm"
            onClick={() => setBulkDialogOpen(true)}
            className="gap-2"
          >
            <UploadIcon className="h-3.5 w-3.5" />
            Bulk Invite
          </Button>
          <Button size="sm" onClick={() => setInviteDialogOpen(true)} className="gap-2">
            <PlusIcon className="h-3.5 w-3.5" />
            Invite User
          </Button>
        </div>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center">
                <MailIcon className="h-5 w-5 text-blue-600 dark:text-blue-400" />
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
                <p className="text-sm text-muted-foreground">Accepted</p>
                <span className="text-xl font-bold block">
                  {stats ? stats.accepted : <Skeleton className="h-6 w-10 inline-block" />}
                </span>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-slate-100 dark:bg-zinc-700/50 flex items-center justify-center">
                <AlertCircleIcon className="h-5 w-5 text-slate-600 dark:text-zinc-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Expired</p>
                <span className="text-xl font-bold block">
                  {stats ? stats.expired : <Skeleton className="h-6 w-10 inline-block" />}
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
                <p className="text-sm text-muted-foreground">Revoked</p>
                <span className="text-xl font-bold block">
                  {stats ? (stats.revoked ?? 0) : <Skeleton className="h-6 w-10 inline-block" />}
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
          }}
        >
          <SelectTrigger className="w-44 bg-white/70 dark:bg-zinc-800/70">
            <SelectValue placeholder="Filter by status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Statuses</SelectItem>
            <SelectItem value="Pending">Pending</SelectItem>
            <SelectItem value="Accepted">Accepted</SelectItem>
            <SelectItem value="Expired">Expired</SelectItem>
            <SelectItem value="Revoked">Revoked</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Error State */}
      {error && (
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 rounded-lg p-4 flex items-center gap-3">
          <AlertCircleIcon className="h-5 w-5 text-red-500 shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-red-800 dark:text-red-200">
              Failed to load invitations
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

      {/* Invitations Table */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200/60 dark:border-zinc-700/40">
                <th className="text-left p-3 font-medium text-muted-foreground">Email</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Role</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Status</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Sent</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Expires</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Actions</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i} className="border-b border-slate-100 dark:border-zinc-800/60">
                    <td className="p-3">
                      <Skeleton className="h-4 w-40" />
                    </td>
                    <td className="p-3">
                      <Skeleton className="h-5 w-16" />
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
                      <Skeleton className="h-8 w-24" />
                    </td>
                  </tr>
                ))
              ) : invitations.length === 0 ? (
                <tr>
                  <td colSpan={6} className="p-8 text-center text-muted-foreground">
                    <MailIcon className="mx-auto mb-2 h-8 w-8 opacity-50" />
                    <p>
                      {statusFilter !== 'all'
                        ? 'No invitations match this filter'
                        : 'No invitations sent yet'}
                    </p>
                  </td>
                </tr>
              ) : (
                invitations.map(inv => (
                  <InvitationRow
                    key={inv.id}
                    invitation={inv}
                    onResend={id => resendMutation.mutate(id)}
                    onRevoke={id => revokeMutation.mutate(id)}
                  />
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-slate-200/60 dark:border-zinc-700/40">
            <p className="text-sm text-muted-foreground">
              Showing {(page - 1) * PAGE_SIZE + 1}&ndash;{Math.min(page * PAGE_SIZE, totalCount)} of{' '}
              {totalCount}
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

      {/* Dialogs */}
      <InviteUserDialog
        open={inviteDialogOpen}
        onOpenChange={setInviteDialogOpen}
        onSuccess={handleInviteSuccess}
      />
      <BulkInviteDialog
        open={bulkDialogOpen}
        onOpenChange={setBulkDialogOpen}
        onSuccess={handleInviteSuccess}
      />
    </div>
  );
}
