'use client';

import { Loader2, History } from 'lucide-react';

import { useSyncHistory } from '../hooks/useSyncOperations';

export function HistoryTab() {
  const { data: history, isLoading, error } = useSyncHistory();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800/40 dark:bg-red-900/20 dark:text-red-400">
        Failed to load history: {(error as Error).message}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Sync History
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Recent database synchronization operations.
        </p>
      </div>

      {(!history || history.length === 0) && (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <History className="mb-4 h-10 w-10 text-muted-foreground/50" aria-hidden="true" />
          <p className="text-sm text-muted-foreground">No sync operations recorded yet.</p>
        </div>
      )}

      {history && history.length > 0 && (
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
              {history.map((entry, i) => (
                <tr
                  key={`${entry.createdAt}-${i}`}
                  className="border-b border-slate-200/40 dark:border-zinc-700/30 last:border-b-0"
                >
                  <td className="px-4 py-3 whitespace-nowrap text-muted-foreground">
                    {new Date(entry.createdAt).toLocaleString()}
                  </td>
                  <td className="px-4 py-3 font-medium text-foreground">{entry.action}</td>
                  <td className="px-4 py-3 text-foreground">
                    {entry.resource}
                    {entry.resourceId && (
                      <span className="ml-1 font-mono text-xs text-muted-foreground">
                        ({entry.resourceId})
                      </span>
                    )}
                  </td>
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
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
