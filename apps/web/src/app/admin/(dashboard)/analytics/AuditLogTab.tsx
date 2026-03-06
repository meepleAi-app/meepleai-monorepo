'use client';

import { useState, useEffect } from 'react';

import { Download } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { api } from '@/lib/api';
import type { AuditLogEntry } from '@/lib/api/schemas/admin.schemas';

export function AuditLogTab() {
  const [entries, setEntries] = useState<AuditLogEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);

  useEffect(() => {
    api.admin
      .getAuditLogs({ limit: 50 })
      .then(result => {
        setEntries(result.entries);
        setTotalCount(result.totalCount);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleExport = async () => {
    setExporting(true);
    try {
      const blob = await api.admin.exportAuditLogs();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      // export failed silently
    } finally {
      setExporting(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-8 w-48 rounded bg-muted/50" />
        <div className="h-64 rounded-lg bg-muted/50" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Audit Log
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {totalCount.toLocaleString()} entries recorded.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={handleExport} disabled={exporting}>
          <Download className="mr-2 h-4 w-4" aria-hidden="true" />
          {exporting ? 'Exporting...' : 'Export CSV'}
        </Button>
      </div>

      <div className="overflow-x-auto rounded-lg border border-slate-200/60 dark:border-zinc-700/40">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200/60 dark:border-zinc-700/40 bg-slate-50/50 dark:bg-zinc-800/50">
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Timestamp</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Action</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Resource</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Result</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Details</th>
            </tr>
          </thead>
          <tbody>
            {entries.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">
                  No audit log entries found.
                </td>
              </tr>
            ) : (
              entries.map(entry => (
                <tr
                  key={entry.id}
                  className="border-b border-slate-200/40 dark:border-zinc-700/30 last:border-b-0"
                >
                  <td className="px-4 py-3 whitespace-nowrap text-muted-foreground">
                    {new Date(entry.createdAt).toLocaleString()}
                  </td>
                  <td className="px-4 py-3 font-medium text-foreground">{entry.action}</td>
                  <td className="px-4 py-3 text-foreground">{entry.resource}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                        entry.result === 'Success'
                          ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400'
                          : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                      }`}
                    >
                      {entry.result}
                    </span>
                  </td>
                  <td className="px-4 py-3 max-w-[200px] truncate text-muted-foreground">
                    {entry.details ?? '-'}
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
