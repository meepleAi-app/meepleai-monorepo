'use client';

import { useState, useEffect } from 'react';

import { Download } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { ApiKeyWithStatsDto } from '@/lib/api/schemas/admin.schemas';

export function ApiKeysTab() {
  const [keys, setKeys] = useState<ApiKeyWithStatsDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.admin
      .getApiKeysWithStats()
      .then(result => {
        setKeys(result.keys);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleExport = () => {
    const header = 'Name,Prefix,Created,Last Used,Total Requests,Requests (30d)';
    const rows = keys.map(k =>
      [
        k.apiKey.keyName,
        k.apiKey.keyPrefix,
        new Date(k.apiKey.createdAt).toLocaleDateString(),
        k.apiKey.lastUsedAt ? new Date(k.apiKey.lastUsedAt).toLocaleDateString() : 'Never',
        k.usageStats.totalUsageCount,
        k.usageStats.usageCountLast30Days,
      ].join(',')
    );
    const csv = [header, ...rows].join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `api-keys-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
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
            API Keys
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {keys.length} key{keys.length !== 1 ? 's' : ''} registered.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={handleExport} disabled={keys.length === 0}>
          <Download className="mr-2 h-4 w-4" aria-hidden="true" />
          Export CSV
        </Button>
      </div>

      <div className="overflow-x-auto rounded-lg border border-slate-200/60 dark:border-zinc-700/40">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200/60 dark:border-zinc-700/40 bg-slate-50/50 dark:bg-zinc-800/50">
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Name</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Prefix</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Created</th>
              <th className="px-4 py-3 text-left font-medium text-muted-foreground">Last Used</th>
              <th className="px-4 py-3 text-right font-medium text-muted-foreground">Requests</th>
            </tr>
          </thead>
          <tbody>
            {keys.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">
                  No API keys found.
                </td>
              </tr>
            ) : (
              keys.map(k => (
                <tr
                  key={k.apiKey.id}
                  className="border-b border-slate-200/40 dark:border-zinc-700/30 last:border-b-0"
                >
                  <td className="px-4 py-3 font-medium text-foreground">{k.apiKey.keyName}</td>
                  <td className="px-4 py-3 font-mono text-xs text-muted-foreground">
                    {k.apiKey.keyPrefix}...
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {new Date(k.apiKey.createdAt).toLocaleDateString()}
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {k.apiKey.lastUsedAt
                      ? new Date(k.apiKey.lastUsedAt).toLocaleDateString()
                      : 'Never'}
                  </td>
                  <td className="px-4 py-3 text-right font-medium text-foreground">
                    {k.usageStats.totalUsageCount.toLocaleString()}
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
