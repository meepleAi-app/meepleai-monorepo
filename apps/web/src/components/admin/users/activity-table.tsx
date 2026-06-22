/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import { useEffect } from 'react';

import { useQuery } from '@tanstack/react-query';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { AuditLogEntry } from '@/lib/api/schemas/admin.schemas';

interface ActivityTableProps {
  actionFilter?: string;
  dateRange?: string;
  page?: number;
  onPageChange?: (page: number) => void;
  onTotalCountChange?: (count: number) => void;
}

const actionTypeColors: Record<string, string> = {
  UserRoleChange: 'bg-purple-100 text-purple-900 dark:bg-purple-900/30 dark:text-purple-300',
  'DomainEvent.RoleChangedEvent':
    'bg-purple-100 text-purple-900 dark:bg-purple-900/30 dark:text-purple-300',
  'DomainEvent.UserInvitedEvent':
    'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300',
  'DomainEvent.InvitationAcceptedEvent':
    'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
  email_sent: 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300',
};

const resultColors: Record<string, string> = {
  Success: 'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
  Error: 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300',
  Denied: 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300',
};

export function getDateRange(range?: string): { startDate?: string; endDate?: string } {
  if (!range || range === 'all') return {};
  const now = new Date();
  const end = now.toISOString();
  let start: Date;
  switch (range) {
    case '24h':
      start = new Date(now.getTime() - 24 * 60 * 60 * 1000);
      break;
    case '7d':
      start = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      break;
    case '30d':
      start = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
      break;
    default:
      return {};
  }
  return { startDate: start.toISOString(), endDate: end };
}

function formatTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleString('it-IT', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  } catch {
    return iso;
  }
}

function getUserInitials(name: string): string {
  return name
    .split(' ')
    .map(n => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
}

function formatAction(action: string): string {
  return action
    .replace('DomainEvent.', '')
    .replace(/Event$/, '')
    .replace(/([A-Z])/g, ' $1')
    .trim();
}

const PAGE_SIZE = 50;

export function ActivityTable({
  actionFilter,
  dateRange,
  page = 0,
  onPageChange,
  onTotalCountChange,
}: ActivityTableProps) {
  const dateParams = getDateRange(dateRange);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['admin', 'audit-log', 'activity', actionFilter, dateRange, page],
    queryFn: () =>
      api.admin.getAuditLogs({
        limit: PAGE_SIZE,
        offset: page * PAGE_SIZE,
        action: actionFilter && actionFilter !== 'all' ? actionFilter : undefined,
        ...dateParams,
      }),
    staleTime: 30_000,
  });

  const entries: AuditLogEntry[] = data?.entries ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.ceil(totalCount / PAGE_SIZE);

  // Notify parent of total count changes
  useEffect(() => {
    onTotalCountChange?.(totalCount);
  }, [totalCount, onTotalCountChange]);

  if (isLoading) {
    return (
      <div className="bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg p-8 text-center text-muted-foreground">
        Caricamento log...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg p-8 text-center text-red-600">
        Impossibile caricare il log di attività.
      </div>
    );
  }

  return (
    <div className="bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="bg-amber-100/50 dark:bg-zinc-900/50 border-b border-amber-200/50 dark:border-zinc-700/50">
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                Timestamp
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                User
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                Action
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                Resource
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                IP Address
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase tracking-wider">
                Result
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-zinc-700">
            {entries.length === 0 ? (
              <tr>
                <td colSpan={6} className="py-12 text-center text-muted-foreground">
                  Nessuna attività trovata.
                </td>
              </tr>
            ) : (
              entries.map(entry => (
                <tr
                  key={entry.id}
                  className="hover:bg-muted/50 dark:hover:bg-zinc-900/50 transition-colors"
                >
                  <td className="py-3 px-4 font-mono text-sm text-muted-foreground dark:text-muted-foreground">
                    {formatTimestamp(entry.createdAt)}
                  </td>
                  <td className="py-3 px-4">
                    {entry.userName ? (
                      <div className="flex items-center gap-3">
                        <div className="h-8 w-8 rounded-full bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center text-xs font-semibold text-amber-900 dark:text-amber-300">
                          {getUserInitials(entry.userName)}
                        </div>
                        <div>
                          <div className="font-medium text-foreground dark:text-zinc-100">
                            {entry.userName}
                          </div>
                          {entry.userEmail && (
                            <div className="text-xs text-muted-foreground dark:text-muted-foreground">
                              {entry.userEmail}
                            </div>
                          )}
                        </div>
                      </div>
                    ) : (
                      <span className="text-muted-foreground text-sm">Sistema</span>
                    )}
                  </td>
                  <td className="py-3 px-4">
                    <Badge
                      variant="outline"
                      className={
                        actionTypeColors[entry.action] ??
                        'bg-muted text-foreground dark:bg-card dark:text-slate-300'
                      }
                    >
                      {formatAction(entry.action)}
                    </Badge>
                  </td>
                  <td className="py-3 px-4 text-foreground dark:text-zinc-300">
                    {entry.resource}
                    {entry.resourceId && (
                      <span className="text-xs text-muted-foreground ml-1">({entry.resourceId})</span>
                    )}
                  </td>
                  <td className="py-3 px-4 font-mono text-sm text-muted-foreground dark:text-muted-foreground">
                    {entry.ipAddress ?? '—'}
                  </td>
                  <td className="py-3 px-4 text-right">
                    <Badge
                      variant="outline"
                      className={resultColors[entry.result] ?? 'bg-muted text-foreground'}
                    >
                      {entry.result}
                    </Badge>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between px-4 py-3 border-t border-amber-200/50 dark:border-zinc-700/50">
          <p className="text-sm text-muted-foreground">
            Showing {page * PAGE_SIZE + 1}–{Math.min((page + 1) * PAGE_SIZE, totalCount)} of{' '}
            {totalCount.toLocaleString()}
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={page === 0}
              onClick={() => onPageChange?.(page - 1)}
            >
              Precedente
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= totalPages - 1}
              onClick={() => onPageChange?.(page + 1)}
            >
              Successivo
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
