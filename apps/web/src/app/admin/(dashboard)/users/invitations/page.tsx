/**
 * Admin Invitations Page (Issue #132)
 *
 * Dedicated page for managing user invitations.
 * Features: invitation table, status filters, send/bulk invite dialogs, resend.
 */

'use client';

import { useEffect, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MailIcon, PlusIcon, UploadIcon } from 'lucide-react';
import { toast } from 'sonner';

import { BulkInviteDialog } from '@/components/admin/invitations/BulkInviteDialog';
import { InvitationRow } from '@/components/admin/invitations/InvitationRow';
import { InviteUserDialog } from '@/components/admin/invitations/InviteUserDialog';
import { Button } from '@/components/ui/primitives/button';
import { useSetNavConfig } from '@/hooks/useSetNavConfig';
import { api } from '@/lib/api';
import type { InvitationStatus } from '@/lib/api/schemas/invitation.schemas';

type FilterTab = 'All' | InvitationStatus;

const FILTER_TABS: FilterTab[] = ['All', 'Pending', 'Accepted', 'Expired'];

export default function InvitationsPage() {
  const setNavConfig = useSetNavConfig();
  const queryClient = useQueryClient();
  const [activeFilter, setActiveFilter] = useState<FilterTab>('All');
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

  const statsQuery = useQuery({
    queryKey: ['admin', 'invitation-stats'],
    queryFn: () => api.invitations.getInvitationStats(),
    staleTime: 30_000,
  });

  const invitationsQuery = useQuery({
    queryKey: ['admin', 'invitations', activeFilter],
    queryFn: () =>
      api.invitations.getInvitations({
        status: activeFilter === 'All' ? undefined : activeFilter,
        pageSize: 50,
      }),
    staleTime: 30_000,
  });

  const resendMutation = useMutation({
    mutationFn: (id: string) => api.invitations.resendInvitation(id),
    onSuccess: () => {
      toast.success('Invitation resent successfully');
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitation-stats'] });
    },
    onError: err => {
      toast.error(err instanceof Error ? err.message : 'Failed to resend invitation');
    },
  });

  function handleInviteSuccess() {
    queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'invitation-stats'] });
  }

  function getTabCount(tab: FilterTab): number | undefined {
    if (!statsQuery.data) return undefined;
    switch (tab) {
      case 'All':
        return statsQuery.data.total;
      case 'Pending':
        return statsQuery.data.pending;
      case 'Accepted':
        return statsQuery.data.accepted;
      case 'Expired':
        return statsQuery.data.expired;
    }
  }

  const invitations = invitationsQuery.data?.items ?? [];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Invitations</h1>
          <p className="text-muted-foreground">Manage user invitations and track their status.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setBulkDialogOpen(true)}>
            <UploadIcon className="mr-2 h-4 w-4" />
            Bulk Invite
          </Button>
          <Button onClick={() => setInviteDialogOpen(true)}>
            <PlusIcon className="mr-2 h-4 w-4" />
            Invite User
          </Button>
        </div>
      </div>

      {/* Filter Tabs */}
      <div className="flex gap-1 rounded-lg bg-muted/50 p-1" role="tablist">
        {FILTER_TABS.map(tab => {
          const count = getTabCount(tab);
          const isActive = activeFilter === tab;
          return (
            <button
              key={tab}
              role="tab"
              aria-selected={isActive}
              className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                isActive
                  ? 'bg-background text-foreground shadow-sm'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
              onClick={() => setActiveFilter(tab)}
            >
              {tab}
              {count !== undefined && <span className="ml-1.5 text-xs opacity-70">({count})</span>}
            </button>
          );
        })}
      </div>

      {/* Table */}
      {invitationsQuery.isLoading ? (
        <div className="flex items-center justify-center py-12 text-muted-foreground">
          Loading invitations...
        </div>
      ) : invitationsQuery.isError ? (
        <div className="flex items-center justify-center py-12 text-red-600">
          Failed to load invitations. Please try again.
        </div>
      ) : invitations.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
          <MailIcon className="mb-2 h-8 w-8 opacity-50" />
          <p>No invitations found.</p>
        </div>
      ) : (
        <div className="rounded-md border">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-2 py-2 text-left font-medium text-muted-foreground">Email</th>
                <th className="px-2 py-2 text-left font-medium text-muted-foreground">Role</th>
                <th className="px-2 py-2 text-left font-medium text-muted-foreground">Status</th>
                <th className="px-2 py-2 text-left font-medium text-muted-foreground">Created</th>
                <th className="px-2 py-2 text-left font-medium text-muted-foreground">Expires</th>
                <th className="px-2 py-2 text-left font-medium text-muted-foreground">Actions</th>
              </tr>
            </thead>
            <tbody>
              {invitations.map(inv => (
                <InvitationRow
                  key={inv.id}
                  invitation={inv}
                  onResend={id => resendMutation.mutate(id)}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}

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
