'use client';

import { useQuery } from '@tanstack/react-query';

import { Badge } from '@/components/ui/badge';
import { api } from '@/lib/api';
import type { AuditLogEntry } from '@/lib/api/schemas/admin.schemas';

interface ActivityTableProps {
  actionFilter?: string;
  dateRange?: string;
}

const actionTypeColors: Record<string, string> = {
  UserRoleChange:
    'bg-purple-100 text-purple-900 dark:bg-purple-900/30 dark:text-purple-300',
  'DomainEvent.RoleChangedEvent':
    'bg-purple-100 text-purple-900 dark:bg-purple-900/30 dark:text-purple-300',
  'DomainEvent.UserInvitedEvent':
    'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300',
  'DomainEvent.InvitationAcceptedEvent':
    'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
  email_sent:
    'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300',
};

const resultColors: Record<string, string> = {
  Success:
    'bg-green-100 text-green-900 dark:bg-green-900/30 dark:text-green-300',
  Error: 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300',
  Denied:
    'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300',
};

function getDateRange(range?: string): { startDate?: string; endDate?: string } {
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
    .map((n) => n[0])
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

export function ActivityTable({ actionFilter, dateRange }: ActivityTableProps) {
  const dateParams = getDateRange(dateRange);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['admin', 'audit-log', 'activity', actionFilter, dateRange],
    queryFn: () =>
      api.admin.getAuditLogs({
        limit: 50,
        action: actionFilter && actionFilter !== 'all' ? actionFilter : undefined,
        ...dateParams,
      }),
    staleTime: 30_000,
  });

  const entries: AuditLogEntry[] = data?.entries ?? [];

  if (isLoading) {
    return (
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg p-8 text-center text-muted-foreground">
        Loading activity log...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg p-8 text-center text-red-600">
        Failed to load activity log.
      </div>
    );
  }

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg overflow-hidden">
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
                <td
                  colSpan={6}
                  className="py-12 text-center text-muted-foreground"
                >
                  No activity found.
                </td>
              </tr>
            ) : (
              entries.map((entry) => (
                <tr
                  key={entry.id}
                  className="hover:bg-slate-50/50 dark:hover:bg-zinc-900/50 transition-colors"
                >
                  <td className="py-3 px-4 font-mono text-sm text-slate-600 dark:text-zinc-400">
                    {formatTimestamp(entry.createdAt)}
                  </td>
                  <td className="py-3 px-4">
                    {entry.userName ? (
                      <div className="flex items-center gap-3">
                        <div className="h-8 w-8 rounded-full bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center text-xs font-semibold text-amber-900 dark:text-amber-300">
                          {getUserInitials(entry.userName)}
                        </div>
                        <div>
                          <div className="font-medium text-slate-900 dark:text-zinc-100">
                            {entry.userName}
                          </div>
                          {entry.userEmail && (
                            <div className="text-xs text-slate-500 dark:text-zinc-400">
                              {entry.userEmail}
                            </div>
                          )}
                        </div>
                      </div>
                    ) : (
                      <span className="text-slate-400 text-sm">System</span>
                    )}
                  </td>
                  <td className="py-3 px-4">
                    <Badge
                      variant="outline"
                      className={
                        actionTypeColors[entry.action] ??
                        'bg-slate-100 text-slate-900 dark:bg-slate-900/30 dark:text-slate-300'
                      }
                    >
                      {formatAction(entry.action)}
                    </Badge>
                  </td>
                  <td className="py-3 px-4 text-slate-700 dark:text-zinc-300">
                    {entry.resource}
                    {entry.resourceId && (
                      <span className="text-xs text-slate-400 ml-1">
                        ({entry.resourceId})
                      </span>
                    )}
                  </td>
                  <td className="py-3 px-4 font-mono text-sm text-slate-600 dark:text-zinc-400">
                    {entry.ipAddress ?? '—'}
                  </td>
                  <td className="py-3 px-4 text-right">
                    <Badge
                      variant="outline"
                      className={
                        resultColors[entry.result] ??
                        'bg-slate-100 text-slate-900'
                      }
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
    </div>
  );
}
