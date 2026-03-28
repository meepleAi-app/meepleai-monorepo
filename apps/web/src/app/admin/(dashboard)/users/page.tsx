/**
 * Admin Users Page (Issue #132)
 *
 * Lists all users with search, role filter, and pagination.
 * Pending invitations shown as amber rows at top.
 */

'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  MailIcon,
  PlusIcon,
  RefreshCwIcon,
  SearchIcon,
  UsersIcon,
} from 'lucide-react';
import Link from 'next/link';
import { toast } from 'sonner';

import { InvitationStatusBadge } from '@/components/admin/invitations/InvitationStatusBadge';
import { InviteUserDialog } from '@/components/admin/invitations/InviteUserDialog';
import { InlineRoleSelect } from '@/components/admin/users/InlineRoleSelect';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useDebounce } from '@/hooks/useDebounce';
import { api } from '@/lib/api';
import { displayRole } from '@/lib/utils/roles';

const PAGE_SIZE = 20;
const ROLE_OPTIONS = ['all', 'user', 'editor', 'admin', 'superadmin'] as const;

export default function AdminUsersPage() {
  const queryClient = useQueryClient();
  const [inviteDialogOpen, setInviteDialogOpen] = useState(false);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 300);
  const [roleFilter, setRoleFilter] = useState<string>('all');
  const [page, setPage] = useState(1);

  const usersQuery = useQuery({
    queryKey: ['admin', 'users', { search: debouncedSearch, role: roleFilter, page }],
    queryFn: () =>
      api.admin.getAllUsers({
        search: debouncedSearch || undefined,
        role: roleFilter === 'all' ? undefined : roleFilter,
        page,
        limit: PAGE_SIZE,
      }),
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
      toast.success('Invito reinviato');
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
    },
    onError: err => {
      toast.error(err instanceof Error ? err.message : "Errore nel reinvio dell'invito");
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
      if (days > 0) return `${days}g fa`;
      const hours = Math.floor(diff / (1000 * 60 * 60));
      if (hours > 0) return `${hours}h fa`;
      const minutes = Math.floor(diff / (1000 * 60));
      return `${minutes}m fa`;
    } catch {
      return '';
    }
  }

  const users = usersQuery.data?.items ?? [];
  const totalUsers = usersQuery.data?.total ?? 0;
  const totalPages = Math.ceil(totalUsers / PAGE_SIZE);
  const pendingInvitations = pendingInvitationsQuery.data?.items ?? [];
  const isLoading = usersQuery.isLoading;

  // Reset page when filters change
  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPage(1);
  };

  const handleRoleChange = (value: string) => {
    setRoleFilter(value);
    setPage(1);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Utenti</h1>
          <p className="text-muted-foreground">
            Gestisci utenti e inviti in sospeso.
            {totalUsers > 0 && (
              <span className="ml-1 text-foreground font-medium">{totalUsers} totali</span>
            )}
          </p>
        </div>
        <Button onClick={() => setInviteDialogOpen(true)}>
          <PlusIcon className="mr-2 h-4 w-4" />
          Invita Utente
        </Button>
      </div>

      {/* Search + Filter Bar */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1">
          <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Cerca per nome o email..."
            value={search}
            onChange={e => handleSearchChange(e.target.value)}
            className="pl-9"
          />
        </div>
        <Select value={roleFilter} onValueChange={handleRoleChange}>
          <SelectTrigger className="w-full sm:w-40">
            <SelectValue placeholder="Ruolo" />
          </SelectTrigger>
          <SelectContent>
            {ROLE_OPTIONS.map(role => (
              <SelectItem key={role} value={role}>
                {role === 'all' ? 'Tutti i ruoli' : displayRole(role)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="flex items-center justify-center py-12 text-muted-foreground">
          Caricamento utenti...
        </div>
      ) : (
        <div className="rounded-md border">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Utente</th>
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Ruolo</th>
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Stato</th>
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Azioni</th>
              </tr>
            </thead>
            <tbody>
              {/* Pending invitations as amber rows at the top (only on first page without filters) */}
              {page === 1 &&
                !debouncedSearch &&
                roleFilter === 'all' &&
                pendingInvitations.map(inv => (
                  <tr key={`inv-${inv.id}`} className="border-b bg-amber-50 dark:bg-amber-950/20">
                    <td className="px-3 py-2">
                      <div className="flex items-center gap-2">
                        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-amber-100 text-amber-600">
                          <MailIcon className="h-4 w-4" />
                        </div>
                        <div>
                          <div className="font-medium">{inv.email}</div>
                          <div className="text-xs text-muted-foreground">
                            Invitato {timeAgo(inv.createdAt)}
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
                        aria-label={`Reinvia invito a ${inv.email}`}
                      >
                        <RefreshCwIcon className="mr-1 h-3 w-3" />
                        Reinvia
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
                    <Link
                      href={`/admin/users/${u.id}`}
                      className="flex items-center gap-2 hover:underline"
                    >
                      <div className="flex h-8 w-8 items-center justify-center rounded-full bg-muted text-muted-foreground">
                        <UsersIcon className="h-4 w-4" />
                      </div>
                      <div>
                        <div className="font-medium">{u.displayName || u.email}</div>
                        <div className="text-xs text-muted-foreground">{u.email}</div>
                      </div>
                    </Link>
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
                      Attivo
                    </Badge>
                  </td>
                  <td className="px-3 py-2">
                    <Link href={`/admin/users/${u.id}`}>
                      <Button variant="ghost" size="sm">
                        Dettagli
                      </Button>
                    </Link>
                  </td>
                </tr>
              ))}

              {users.length === 0 && pendingInvitations.length === 0 && (
                <tr>
                  <td colSpan={4} className="py-12 text-center text-muted-foreground">
                    {debouncedSearch
                      ? `Nessun utente trovato per "${debouncedSearch}"`
                      : 'Nessun utente trovato.'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, totalUsers)} di {totalUsers}
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
              {page} di {totalPages}
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

      <InviteUserDialog
        open={inviteDialogOpen}
        onOpenChange={setInviteDialogOpen}
        onSuccess={handleInviteSuccess}
      />
    </div>
  );
}
