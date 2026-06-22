/**
 * Admin Staging Allowlist Page (#845 — DevOps Wave 1)
 *
 * Superadmin-only CRUD over the email allowlist that gates access to staging.
 * Replaces the deprecated STAGING_ALLOWED_EMAILS env-var configuration:
 * changes apply within ~60s (in-memory cache TTL) without a redeploy.
 */

'use client';

import { useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Trash2Icon, UserPlusIcon, RefreshCwIcon, ShieldCheckIcon } from 'lucide-react';
import { toast } from 'sonner';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { StagingAllowlistEntryDto } from '@/lib/api/clients/stagingAllowlistClient';

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export default function StagingAccessPage() {
  const queryClient = useQueryClient();
  const [emailInput, setEmailInput] = useState('');
  const [noteInput, setNoteInput] = useState('');

  const listQuery = useQuery({
    queryKey: ['admin', 'staging-allowlist'],
    queryFn: () => api.stagingAllowlist.list(),
  });

  const addMutation = useMutation({
    mutationFn: () =>
      api.stagingAllowlist.add({ email: emailInput.trim(), note: noteInput.trim() || null }),
    onSuccess: entry => {
      toast.success(`Added ${entry.email} to staging allowlist`);
      setEmailInput('');
      setNoteInput('');
      void queryClient.invalidateQueries({ queryKey: ['admin', 'staging-allowlist'] });
    },
    onError: (error: Error) => {
      const message = error.message ?? 'Failed to add entry';
      if (message.toLowerCase().includes('conflict') || message.includes('409')) {
        toast.error(`${emailInput.trim()} is already in the allowlist`);
      } else {
        toast.error(message);
      }
    },
  });

  const removeMutation = useMutation({
    mutationFn: (id: string) => api.stagingAllowlist.remove(id),
    onSuccess: () => {
      toast.success('Entry removed from staging allowlist');
      void queryClient.invalidateQueries({ queryKey: ['admin', 'staging-allowlist'] });
    },
    onError: (error: Error) => toast.error(error.message ?? 'Failed to remove entry'),
  });

  const handleAdd = (e: React.FormEvent) => {
    e.preventDefault();
    if (!emailInput.trim()) {
      toast.error('Email is required');
      return;
    }
    addMutation.mutate();
  };

  const handleRemove = (entry: StagingAllowlistEntryDto) => {
    if (typeof window !== 'undefined') {
      const confirmed = window.confirm(`Remove ${entry.email} from the staging allowlist?`);
      if (!confirmed) return;
    }
    removeMutation.mutate(entry.id);
  };

  const entries = listQuery.data ?? [];

  return (
    <div className="space-y-6 p-6">
      <header className="flex items-center justify-between gap-4">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
            <ShieldCheckIcon className="size-6" aria-hidden />
            Staging Allowlist
          </h1>
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
            Controls who can access the staging environment. Changes apply within ~60s without a
            redeploy (in-memory cache invalidates on add/remove). Superadmin only.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => listQuery.refetch()}
          disabled={listQuery.isFetching}
          aria-label="Refresh allowlist"
        >
          <RefreshCwIcon
            className={`size-4 ${listQuery.isFetching ? 'animate-spin' : ''}`}
            aria-hidden
          />
          Refresh
        </Button>
      </header>

      <Card>
        <CardContent className="pt-6">
          <form onSubmit={handleAdd} className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="flex-1">
              <label htmlFor="staging-allowlist-email" className="text-sm font-medium">
                Email
              </label>
              <Input
                id="staging-allowlist-email"
                type="email"
                placeholder="user@example.com"
                value={emailInput}
                onChange={e => setEmailInput(e.target.value)}
                required
                disabled={addMutation.isPending}
                className="mt-1"
              />
            </div>
            <div className="flex-1">
              <label htmlFor="staging-allowlist-note" className="text-sm font-medium">
                Note (optional)
              </label>
              <Input
                id="staging-allowlist-note"
                type="text"
                placeholder="Tester for invite-only flow"
                value={noteInput}
                onChange={e => setNoteInput(e.target.value)}
                maxLength={500}
                disabled={addMutation.isPending}
                className="mt-1"
              />
            </div>
            <Button type="submit" disabled={addMutation.isPending || !emailInput.trim()}>
              <UserPlusIcon className="size-4" aria-hidden />
              {addMutation.isPending ? 'Adding…' : 'Add'}
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {listQuery.isLoading ? (
            <div className="space-y-3 p-6">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : listQuery.isError ? (
            <div className="p-6 text-sm text-destructive">
              Failed to load allowlist: {listQuery.error.message ?? 'unknown error'}
            </div>
          ) : entries.length === 0 ? (
            <div className="p-6 text-center text-sm text-muted-foreground">
              No entries yet. Add the first email above to grant staging access.
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-muted/50 text-left text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-2 font-medium">Email</th>
                  <th className="px-4 py-2 font-medium">Note</th>
                  <th className="px-4 py-2 font-medium">Added</th>
                  <th className="px-4 py-2 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {entries.map(entry => (
                  <tr key={entry.id} className="border-t border-border">
                    <td className="px-4 py-3 font-mono">{entry.email}</td>
                    <td className="px-4 py-3 text-muted-foreground">{entry.note ?? '—'}</td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatDateTime(entry.addedAt)}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleRemove(entry)}
                        disabled={removeMutation.isPending}
                        aria-label={`Remove ${entry.email}`}
                      >
                        <Trash2Icon className="size-4" aria-hidden />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
