/**
 * Admin Users Page (Issue #132)
 *
 * Lists all users with pending invitations shown as amber rows at top.
 * Provides quick access to invite/resend actions.
 */

'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MailIcon, PlusIcon, RefreshCwIcon, UsersIcon } from 'lucide-react';
import { toast } from 'sonner';

import { InvitationStatusBadge } from '@/components/admin/invitations/InvitationStatusBadge';
import { InviteUserDialog } from '@/components/admin/invitations/InviteUserDialog';
import { InlineRoleSelect } from '@/components/admin/users/InlineRoleSelect';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

export default function AdminUsersPage() {
  const queryClient = useQueryClient();
  const [inviteDialogOpen, setInviteDialogOpen] = useState(false);

  const usersQuery = useQuery({
    queryKey: ['admin', 'users'],
    queryFn: () => api.admin.getAllUsers({ limit: 50 }),
    staleTime: 30_000,
  });

  const pendingInvitationsQuery = useQuery({
    queryKey: ['admin', 'invitations', 'pending-inline'],
    queryFn: () => api.invitations.getInvitations({ status: 'Pending', pageSize: 20 }),
    staleTime: 30_000,
  });

  const resendMutation = useMutation({
    mutationFn: (id: string) => api.invitations.resendInvitation(id),
    onSuccess: () => {
      toast.success('Invitation resent');
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
    },
    onError: err => {
      toast.error(err instanceof Error ? err.message : 'Failed to resend invitation');
    },
  });

  function handleInviteSuccess() {
    queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
  }

  function timeAgo(iso: string): string {
    try {
      const diff = Date.now() - new Date(iso).getTime();
      const days = Math.floor(diff / (1000 * 60 * 60 * 24));
      if (days > 0) return `${days}d ago`;
      const hours = Math.floor(diff / (1000 * 60 * 60));
      if (hours > 0) return `${hours}h ago`;
      const minutes = Math.floor(diff / (1000 * 60));
      return `${minutes}m ago`;
    } catch {
      return '';
    }
  }

  const users = usersQuery.data?.users ?? [];
  const pendingInvitations = pendingInvitationsQuery.data?.items ?? [];
  const isLoading = usersQuery.isLoading;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Users</h1>
          <p className="text-muted-foreground">Manage users and pending invitations.</p>
        </div>
        <Button onClick={() => setInviteDialogOpen(true)}>
          <PlusIcon className="mr-2 h-4 w-4" />
          Invite User
        </Button>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="flex items-center justify-center py-12 text-muted-foreground">
          Loading users...
        </div>
      ) : (
        <div className="rounded-md border">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">User</th>
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Role</th>
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Status</th>
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Actions</th>
              </tr>
            </thead>
            <tbody>
              {/* Pending invitations as amber rows at the top */}
              {pendingInvitations.map(inv => (
                <tr key={`inv-${inv.id}`} className="border-b bg-amber-50 dark:bg-amber-950/20">
                  <td className="px-3 py-2">
                    <div className="flex items-center gap-2">
                      <div className="flex h-8 w-8 items-center justify-center rounded-full bg-amber-100 text-amber-600">
                        <MailIcon className="h-4 w-4" />
                      </div>
                      <div>
                        <div className="font-medium">{inv.email}</div>
                        <div className="text-xs text-muted-foreground">
                          Invited {timeAgo(inv.createdAt)}
                        </div>
                      </div>
                    </div>
                  </td>
                  <td className="px-3 py-2">
                    <Badge variant="secondary">{inv.role}</Badge>
                  </td>
                  <td className="px-3 py-2">
                    <InvitationStatusBadge status="Pending" />
                  </td>
                  <td className="px-3 py-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => resendMutation.mutate(inv.id)}
                      aria-label={`Resend invitation to ${inv.email}`}
                    >
                      <RefreshCwIcon className="mr-1 h-3 w-3" />
                      Resend
                    </Button>
                  </td>
                </tr>
              ))}

              {/* Registered users */}
              {users.map(u => (
                <tr
                  key={u.id}
                  className="border-b border-border/50 transition-colors hover:bg-muted/50"
                >
                  <td className="px-3 py-2">
                    <div className="flex items-center gap-2">
                      <div className="flex h-8 w-8 items-center justify-center rounded-full bg-muted text-muted-foreground">
                        <UsersIcon className="h-4 w-4" />
                      </div>
                      <div>
                        <div className="font-medium">{u.displayName || u.email}</div>
                        <div className="text-xs text-muted-foreground">{u.email}</div>
                      </div>
                    </div>
                  </td>
                  <td className="px-3 py-2">
                    <InlineRoleSelect
                      userId={u.id}
                      currentRole={u.role}
                      userName={u.displayName || u.email}
                    />
                  </td>
                  <td className="px-3 py-2">
                    <Badge
                      variant="outline"
                      className="border-green-300 bg-green-50 text-green-900"
                    >
                      Active
                    </Badge>
                  </td>
                  <td className="px-3 py-2" />
                </tr>
              ))}

              {users.length === 0 && pendingInvitations.length === 0 && (
                <tr>
                  <td colSpan={4} className="py-12 text-center text-muted-foreground">
                    No users found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      <InviteUserDialog
        open={inviteDialogOpen}
        onOpenChange={setInviteDialogOpen}
        onSuccess={handleInviteSuccess}
      />
    </div>
  );
}
