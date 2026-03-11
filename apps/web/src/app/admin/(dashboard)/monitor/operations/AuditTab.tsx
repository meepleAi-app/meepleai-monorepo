'use client';

/**
 * AuditTab — Admin audit trail viewer
 * Issue #130 — Audit Trail Viewer Tab (stub for #126 page setup)
 */

import { useCallback, useEffect, useState } from 'react';

import { ChevronDown, ChevronRight, Download, Loader2 } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { AuditLogEntry, AuditLogListResult } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

export function AuditTab() {
  const [data, setData] = useState<AuditLogListResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  // Filters
  const [actionFilter, setActionFilter] = useState('');
  const [resourceFilter, setResourceFilter] = useState('');
  const [resultFilter, setResultFilter] = useState('');
  const [offset, setOffset] = useState(0);
  const limit = 50;

  const fetchData = useCallback(async () => {
    try {
      const result = await api.admin.getAuditLogs({
        limit,
        offset,
        action: actionFilter || undefined,
        resource: resourceFilter || undefined,
        result: resultFilter || undefined,
      });
      setData(result);
    } catch {
      // silent
    } finally {
      setLoading(false);
    }
  }, [actionFilter, resourceFilter, resultFilter, offset]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleExport = async () => {
    setExporting(true);
    try {
      const blob = await api.admin.exportAuditLogs({
        action: actionFilter || undefined,
        resource: resourceFilter || undefined,
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } finally {
      setExporting(false);
    }
  };

  const toggleExpand = (id: string) => {
    setExpandedId(prev => (prev === id ? null : id));
  };

  return (
    <div className="space-y-6" data-testid="audit-tab">
      <div>
        <h2 className="font-quicksand text-lg font-semibold">Audit Trail</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Administrative action log with filtering and export.
        </p>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <Input
          placeholder="Filter by action..."
          value={actionFilter}
          onChange={e => {
            setActionFilter(e.target.value);
            setOffset(0);
          }}
          className="w-48"
          data-testid="audit-action-filter"
        />
        <Input
          placeholder="Filter by resource..."
          value={resourceFilter}
          onChange={e => {
            setResourceFilter(e.target.value);
            setOffset(0);
          }}
          className="w-48"
          data-testid="audit-resource-filter"
        />
        <select
          value={resultFilter}
          onChange={e => {
            setResultFilter(e.target.value);
            setOffset(0);
          }}
          className="rounded-md border bg-background px-3 py-2 text-sm"
          data-testid="audit-result-filter"
        >
          <option value="">All Results</option>
          <option value="Success">Success</option>
          <option value="Failure">Failure</option>
        </select>
        <Button
          variant="outline"
          size="sm"
          onClick={handleExport}
          disabled={exporting}
          data-testid="export-audit-button"
        >
          {exporting ? (
            <Loader2 className="h-4 w-4 mr-2 animate-spin" />
          ) : (
            <Download className="h-4 w-4 mr-2" />
          )}
          Export CSV
        </Button>
      </div>

      {/* Table */}
      {loading ? (
        <div className="flex items-center justify-center py-10">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-xs text-muted-foreground">
                <th className="w-8 py-3 px-2" />
                <th className="text-left py-3 px-3">Timestamp</th>
                <th className="text-left py-3 px-3">User</th>
                <th className="text-left py-3 px-3">Action</th>
                <th className="text-left py-3 px-3">Resource</th>
                <th className="text-left py-3 px-3">Result</th>
              </tr>
            </thead>
            <tbody>
              {data?.entries.map(entry => (
                <AuditRow
                  key={entry.id}
                  entry={entry}
                  expanded={expandedId === entry.id}
                  onToggle={() => toggleExpand(entry.id)}
                />
              ))}
              {(!data || data.entries.length === 0) && (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-sm text-muted-foreground">
                    No audit log entries found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination */}
      {data && data.totalCount > limit && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-muted-foreground">
            Showing {offset + 1}–{Math.min(offset + limit, data.totalCount)} of {data.totalCount}
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={offset === 0}
              onClick={() => setOffset(Math.max(0, offset - limit))}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={offset + limit >= data.totalCount}
              onClick={() => setOffset(offset + limit)}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function AuditRow({
  entry,
  expanded,
  onToggle,
}: {
  entry: AuditLogEntry;
  expanded: boolean;
  onToggle: () => void;
}) {
  return (
    <>
      <tr
        className={cn(
          'border-b last:border-0 cursor-pointer hover:bg-slate-50/50 dark:hover:bg-zinc-800/30'
        )}
        onClick={onToggle}
      >
        <td className="py-3 px-2">
          {expanded ? (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          )}
        </td>
        <td className="py-3 px-3 text-xs whitespace-nowrap">
          {new Date(entry.createdAt).toLocaleString()}
        </td>
        <td className="py-3 px-3">
          <div>
            <p className="text-xs font-medium">{entry.userName ?? '—'}</p>
            <p className="text-xs text-muted-foreground">{entry.userEmail ?? ''}</p>
          </div>
        </td>
        <td className="py-3 px-3">
          <Badge variant="outline" className="text-xs">
            {entry.action}
          </Badge>
        </td>
        <td className="py-3 px-3 text-xs">{entry.resource}</td>
        <td className="py-3 px-3">
          <Badge
            variant={entry.result === 'Success' ? 'secondary' : 'destructive'}
            className="text-xs"
          >
            {entry.result}
          </Badge>
        </td>
      </tr>
      {expanded && (
        <tr className="bg-slate-50/30 dark:bg-zinc-800/20">
          <td colSpan={6} className="px-6 py-3">
            <div className="grid gap-2 text-xs sm:grid-cols-2">
              <div>
                <span className="text-muted-foreground">Resource ID: </span>
                <span className="font-mono">{entry.resourceId ?? '—'}</span>
              </div>
              <div>
                <span className="text-muted-foreground">IP Address: </span>
                <span className="font-mono">{entry.ipAddress ?? '—'}</span>
              </div>
              {entry.details && (
                <div className="sm:col-span-2">
                  <span className="text-muted-foreground">Details: </span>
                  <span>{entry.details}</span>
                </div>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
