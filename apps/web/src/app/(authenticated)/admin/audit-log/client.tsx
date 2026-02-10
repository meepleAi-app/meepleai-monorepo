'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  DownloadIcon,
  FilterIcon,
  RefreshCwIcon,
  ShieldCheckIcon,
  XIcon,
} from 'lucide-react';

import { api } from '@/lib/api';
import type { AuditLogEntry } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

type AuditLogFilters = {
  action: string;
  resource: string;
  result: string;
  startDate: string;
  endDate: string;
};

const RESULTS = ['', 'Success', 'Error', 'Denied'] as const;
const ACTIONS = [
  '',
  'UserImpersonate',
  'EndImpersonation',
  'UserRoleChange',
  'TierChange',
  'UserBlock',
  'UserDelete',
  'FeatureFlagChange',
  'ServiceRestart',
  'CacheClear',
  'ManualLedgerEntry',
] as const;

const PAGE_SIZE = 25;

export function AuditLogClient() {
  const [entries, setEntries] = useState<AuditLogEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showFilters, setShowFilters] = useState(false);
  const [selectedEntry, setSelectedEntry] = useState<AuditLogEntry | null>(null);
  const [filters, setFilters] = useState<AuditLogFilters>({
    action: '',
    resource: '',
    result: '',
    startDate: '',
    endDate: '',
  });

  const fetchAuditLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.admin.getAuditLogs({
        limit: PAGE_SIZE,
        offset: page * PAGE_SIZE,
        action: filters.action || undefined,
        resource: filters.resource || undefined,
        result: filters.result || undefined,
        startDate: filters.startDate || undefined,
        endDate: filters.endDate || undefined,
      });
      setEntries(result.entries);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch audit logs');
    } finally {
      setLoading(false);
    }
  }, [page, filters]);

  useEffect(() => {
    fetchAuditLogs();
  }, [fetchAuditLogs]);

  // Polling every 30s
  useEffect(() => {
    const interval = setInterval(fetchAuditLogs, 30000);
    return () => clearInterval(interval);
  }, [fetchAuditLogs]);

  const handleExport = async () => {
    try {
      const blob = await api.admin.exportAuditLogs({
        action: filters.action || undefined,
        resource: filters.resource || undefined,
        result: filters.result || undefined,
        startDate: filters.startDate || undefined,
        endDate: filters.endDate || undefined,
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      setError('Export failed');
    }
  };

  const clearFilters = () => {
    setFilters({ action: '', resource: '', result: '', startDate: '', endDate: '' });
    setPage(0);
  };

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);
  const hasActiveFilters = Object.values(filters).some(v => v !== '');

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <ShieldCheckIcon className="h-6 w-6 text-muted-foreground" />
          <div>
            <h1 className="text-2xl font-bold tracking-tight">Audit Log</h1>
            <p className="text-sm text-muted-foreground">
              Track admin actions and security events ({totalCount} entries)
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={fetchAuditLogs}
            disabled={loading}
            className="inline-flex items-center gap-2 rounded-md border px-3 py-2 text-sm hover:bg-accent transition-colors"
          >
            <RefreshCwIcon className={cn('h-4 w-4', loading && 'animate-spin')} />
            Refresh
          </button>
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={cn(
              'inline-flex items-center gap-2 rounded-md border px-3 py-2 text-sm hover:bg-accent transition-colors',
              hasActiveFilters && 'border-primary bg-primary/5'
            )}
          >
            <FilterIcon className="h-4 w-4" />
            Filters
            {hasActiveFilters && (
              <span className="rounded-full bg-primary px-1.5 py-0.5 text-[10px] text-primary-foreground">
                {Object.values(filters).filter(v => v !== '').length}
              </span>
            )}
          </button>
          <button
            onClick={handleExport}
            className="inline-flex items-center gap-2 rounded-md border px-3 py-2 text-sm hover:bg-accent transition-colors"
          >
            <DownloadIcon className="h-4 w-4" />
            Export CSV
          </button>
        </div>
      </div>

      {/* Filters */}
      {showFilters && (
        <div className="rounded-lg border bg-card p-4 space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="font-medium text-sm">Filters</h3>
            {hasActiveFilters && (
              <button
                onClick={clearFilters}
                className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1"
              >
                <XIcon className="h-3 w-3" /> Clear all
              </button>
            )}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Action</label>
              <select
                value={filters.action}
                onChange={e => { setFilters(f => ({ ...f, action: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              >
                <option value="">All Actions</option>
                {ACTIONS.filter(Boolean).map(a => (
                  <option key={a} value={a}>{a}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Resource</label>
              <input
                type="text"
                placeholder="e.g. User, Service"
                value={filters.resource}
                onChange={e => { setFilters(f => ({ ...f, resource: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              />
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Result</label>
              <select
                value={filters.result}
                onChange={e => { setFilters(f => ({ ...f, result: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              >
                {RESULTS.map(r => (
                  <option key={r} value={r}>{r || 'All Results'}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">From</label>
              <input
                type="date"
                value={filters.startDate}
                onChange={e => { setFilters(f => ({ ...f, startDate: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              />
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">To</label>
              <input
                type="date"
                value={filters.endDate}
                onChange={e => { setFilters(f => ({ ...f, endDate: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              />
            </div>
          </div>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="rounded-lg border border-destructive/50 bg-destructive/5 p-4 text-sm text-destructive">
          {error}
        </div>
      )}

      {/* Table */}
      <div className="rounded-lg border bg-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Timestamp</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Action</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Resource</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Result</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Admin</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">IP</th>
              </tr>
            </thead>
            <tbody>
              {loading && entries.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                    Loading audit logs...
                  </td>
                </tr>
              ) : entries.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                    No audit log entries found
                  </td>
                </tr>
              ) : (
                entries.map(entry => (
                  <tr
                    key={entry.id}
                    onClick={() => setSelectedEntry(entry)}
                    className="border-b hover:bg-muted/30 cursor-pointer transition-colors"
                  >
                    <td className="px-4 py-3 whitespace-nowrap font-mono text-xs">
                      {new Date(entry.createdAt).toLocaleString()}
                    </td>
                    <td className="px-4 py-3">
                      <span className="inline-flex items-center rounded-md bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary">
                        {entry.action}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {entry.resource}
                      {entry.resourceId && (
                        <span className="ml-1 text-xs opacity-60">#{entry.resourceId.slice(0, 8)}</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <ResultBadge result={entry.result} />
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-muted-foreground">
                      {entry.adminUserId ? entry.adminUserId.slice(0, 8) + '...' : '-'}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-muted-foreground">
                      {entry.ipAddress || '-'}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between border-t px-4 py-3">
            <p className="text-xs text-muted-foreground">
              Showing {page * PAGE_SIZE + 1}-{Math.min((page + 1) * PAGE_SIZE, totalCount)} of {totalCount}
            </p>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage(p => Math.max(0, p - 1))}
                disabled={page === 0}
                className="rounded-md border px-3 py-1 text-xs disabled:opacity-50 hover:bg-accent"
              >
                Previous
              </button>
              <span className="px-2 text-xs text-muted-foreground">
                Page {page + 1} of {totalPages}
              </span>
              <button
                onClick={() => setPage(p => Math.min(totalPages - 1, p + 1))}
                disabled={page >= totalPages - 1}
                className="rounded-md border px-3 py-1 text-xs disabled:opacity-50 hover:bg-accent"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Detail Modal */}
      {selectedEntry && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
          onClick={() => setSelectedEntry(null)}
        >
          <div
            className="w-full max-w-lg rounded-lg border bg-card p-6 shadow-lg mx-4"
            onClick={e => e.stopPropagation()}
          >
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-semibold">Audit Log Detail</h3>
              <button
                onClick={() => setSelectedEntry(null)}
                className="rounded-md p-1 hover:bg-muted"
              >
                <XIcon className="h-4 w-4" />
              </button>
            </div>
            <dl className="space-y-3 text-sm">
              <DetailRow label="ID" value={selectedEntry.id} mono />
              <DetailRow label="Timestamp" value={new Date(selectedEntry.createdAt).toLocaleString()} />
              <DetailRow label="Action" value={selectedEntry.action} />
              <DetailRow label="Resource" value={selectedEntry.resource} />
              <DetailRow label="Resource ID" value={selectedEntry.resourceId || '-'} mono />
              <DetailRow label="Result" value={selectedEntry.result} />
              <DetailRow label="Admin User ID" value={selectedEntry.adminUserId || '-'} mono />
              <DetailRow label="IP Address" value={selectedEntry.ipAddress || '-'} mono />
              {selectedEntry.details && (
                <div>
                  <dt className="text-muted-foreground mb-1">Details</dt>
                  <dd className="rounded-md bg-muted p-3 font-mono text-xs whitespace-pre-wrap break-all">
                    {formatDetails(selectedEntry.details)}
                  </dd>
                </div>
              )}
            </dl>
          </div>
        </div>
      )}
    </div>
  );
}

function ResultBadge({ result }: { result: string }) {
  const colors: Record<string, string> = {
    Success: 'bg-green-500/10 text-green-700 dark:text-green-400',
    Error: 'bg-red-500/10 text-red-700 dark:text-red-400',
    Denied: 'bg-yellow-500/10 text-yellow-700 dark:text-yellow-400',
  };

  return (
    <span className={cn(
      'inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium',
      colors[result] || 'bg-muted text-muted-foreground'
    )}>
      {result}
    </span>
  );
}

function DetailRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-muted-foreground shrink-0">{label}</dt>
      <dd className={cn('text-right truncate', mono && 'font-mono text-xs')}>{value}</dd>
    </div>
  );
}

function formatDetails(details: string): string {
  try {
    return JSON.stringify(JSON.parse(details), null, 2);
  } catch {
    return details;
  }
}
