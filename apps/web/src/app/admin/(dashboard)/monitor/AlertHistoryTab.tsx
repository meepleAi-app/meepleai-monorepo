'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import { History } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

interface AlertHistoryItem {
  id: string;
  alertType: string;
  severity: 'Critical' | 'Warning' | 'Info';
  message: string;
  metadata: Record<string, unknown> | null;
  triggeredAt: string;
  resolvedAt: string | null;
  isActive: boolean;
  channelSent: Record<string, boolean> | null;
}

type SeverityFilter = 'All' | 'Critical' | 'Warning' | 'Info';
type StatusFilter = 'All' | 'Active' | 'Resolved';

function severityBadgeClass(severity: AlertHistoryItem['severity']): string {
  switch (severity) {
    case 'Critical':
      return 'bg-red-100 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-400 dark:border-red-800';
    case 'Warning':
      return 'bg-yellow-100 text-yellow-700 border-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-400 dark:border-yellow-800';
    case 'Info':
      return 'bg-blue-100 text-blue-700 border-blue-200 dark:bg-blue-900/30 dark:text-blue-400 dark:border-blue-800';
  }
}

function formatDate(iso: string | null): string {
  if (!iso) return '—';
  try {
    return format(new Date(iso), 'dd MMM yyyy HH:mm');
  } catch {
    return iso;
  }
}

export function AlertHistoryTab() {
  const [severityFilter, setSeverityFilter] = useState<SeverityFilter>('All');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All');

  const { data: alerts = [], isLoading } = useQuery<AlertHistoryItem[]>({
    queryKey: ['admin', 'alerts', 'history'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/alerts');
      if (!res.ok) return [];
      return res.json();
    },
    refetchInterval: 30_000,
    retry: false,
  });

  const filtered = alerts.filter(alert => {
    const matchesSeverity = severityFilter === 'All' || alert.severity === severityFilter;
    const matchesStatus =
      statusFilter === 'All' ||
      (statusFilter === 'Active' && alert.isActive) ||
      (statusFilter === 'Resolved' && !alert.isActive);
    return matchesSeverity && matchesStatus;
  });

  if (isLoading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-8 w-48 rounded bg-muted/50" />
        <div className="h-64 rounded-xl bg-muted/50" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Alert History
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            History of all system alerts and their resolution status.
          </p>
        </div>

        {/* Filters */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Severity dropdown */}
          <select
            value={severityFilter}
            onChange={e => setSeverityFilter(e.target.value as SeverityFilter)}
            className={cn(
              'h-9 rounded-md border border-input bg-background px-3 py-1 text-sm',
              'focus:outline-none focus:ring-1 focus:ring-ring'
            )}
            aria-label="Filter by severity"
          >
            <option value="All">All Severities</option>
            <option value="Critical">Critical</option>
            <option value="Warning">Warning</option>
            <option value="Info">Info</option>
          </select>

          {/* Status toggle */}
          <div
            className="flex overflow-hidden rounded-md border border-input"
            role="group"
            aria-label="Filter by status"
          >
            {(['All', 'Active', 'Resolved'] as StatusFilter[]).map(status => (
              <button
                key={status}
                onClick={() => setStatusFilter(status)}
                className={cn(
                  'px-3 py-1.5 text-xs font-medium transition-colors',
                  statusFilter === status
                    ? 'bg-foreground text-background'
                    : 'bg-background text-muted-foreground hover:bg-muted/50'
                )}
                aria-pressed={statusFilter === status}
              >
                {status}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Table */}
      <div
        className={cn(
          'overflow-hidden rounded-xl border border-border/60',
          'bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md'
        )}
      >
        {filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-3 py-16 text-muted-foreground">
            <History className="h-10 w-10 opacity-40" />
            <p className="text-sm">No alerts</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border/60 bg-muted/30">
                  <th className="px-4 py-3 text-left font-medium text-muted-foreground">
                    Alert Type
                  </th>
                  <th className="px-4 py-3 text-left font-medium text-muted-foreground">
                    Severity
                  </th>
                  <th className="px-4 py-3 text-left font-medium text-muted-foreground">Message</th>
                  <th className="px-4 py-3 text-left font-medium text-muted-foreground">
                    Triggered At
                  </th>
                  <th className="px-4 py-3 text-left font-medium text-muted-foreground">
                    Resolved At
                  </th>
                  <th className="px-4 py-3 text-left font-medium text-muted-foreground">Status</th>
                  <th className="px-4 py-3 text-left font-medium text-muted-foreground">
                    Channels
                  </th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((alert, idx) => (
                  <tr
                    key={alert.id}
                    className={cn(
                      'border-b border-border/40 transition-colors hover:bg-muted/20',
                      idx % 2 === 1 && 'bg-muted/10'
                    )}
                  >
                    <td className="px-4 py-3 font-medium text-foreground">{alert.alertType}</td>
                    <td className="px-4 py-3">
                      <Badge
                        className={cn(
                          'rounded-full border text-xs font-medium',
                          severityBadgeClass(alert.severity)
                        )}
                      >
                        {alert.severity}
                      </Badge>
                    </td>
                    <td className="max-w-xs truncate px-4 py-3 text-muted-foreground">
                      {alert.message}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatDate(alert.triggeredAt)}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatDate(alert.resolvedAt)}
                    </td>
                    <td className="px-4 py-3">
                      <span className="flex items-center gap-1.5">
                        <span
                          className={cn(
                            'h-2 w-2 rounded-full',
                            alert.isActive ? 'bg-red-500' : 'bg-green-500'
                          )}
                        />
                        <span className="text-xs text-muted-foreground">
                          {alert.isActive ? 'Active' : 'Resolved'}
                        </span>
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-1">
                        {!alert.channelSent ||
                        Object.entries(alert.channelSent).filter(([, sent]) => sent).length ===
                          0 ? (
                          <span className="text-xs text-muted-foreground">—</span>
                        ) : (
                          Object.entries(alert.channelSent)
                            .filter(([, sent]) => sent)
                            .map(([channel]) => (
                              <span
                                key={channel}
                                className="rounded bg-muted/50 px-1.5 py-0.5 text-xs text-muted-foreground"
                              >
                                {channel}
                              </span>
                            ))
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
