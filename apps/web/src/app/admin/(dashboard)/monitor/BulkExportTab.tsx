'use client';

import { useState } from 'react';

import { Download, Key, Loader2, ScrollText, Users } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';

interface ExportCard {
  id: 'users' | 'audit-log' | 'api-keys';
  title: string;
  description: string;
  format: string;
  icon: React.ComponentType<{ className?: string }>;
}

const EXPORTS: ExportCard[] = [
  {
    id: 'users',
    title: 'Users',
    description: 'Export all user accounts as CSV',
    format: 'CSV',
    icon: Users,
  },
  {
    id: 'audit-log',
    title: 'Audit Log',
    description: 'Export audit trail entries',
    format: 'CSV',
    icon: ScrollText,
  },
  {
    id: 'api-keys',
    title: 'API Keys',
    description: 'Export API key inventory',
    format: 'CSV',
    icon: Key,
  },
];

function triggerDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

function arrayToCsv(headers: string[], rows: (string | number)[][]): string {
  return [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
}

export function BulkExportTab() {
  const [loading, setLoading] = useState<Record<string, boolean>>({});
  const { toast } = useToast();

  const setItemLoading = (id: string, value: boolean) =>
    setLoading(prev => ({ ...prev, [id]: value }));

  const handleExportUsers = async () => {
    setItemLoading('users', true);
    try {
      const result = await api.admin.getAllUsers({ limit: 10000, page: 1 });
      const headers = [
        'ID',
        'Email',
        'DisplayName',
        'Role',
        'Tier',
        'CreatedAt',
        'LastSeenAt',
        'IsSuspended',
      ];
      const rows = result.items.map(u => [
        u.id,
        u.email,
        u.displayName || '',
        u.role,
        u.tier ?? 'Free',
        u.createdAt,
        u.lastSeenAt ?? '',
        String(u.isSuspended ?? false),
      ]);
      const csv = arrayToCsv(headers, rows);
      triggerDownload(
        new Blob([csv], { type: 'text/csv' }),
        `users-${new Date().toISOString().slice(0, 10)}.csv`
      );
      toast({ title: `${result.items.length} users exported` });
    } catch {
      toast({ title: 'Users export failed', variant: 'destructive' });
    } finally {
      setItemLoading('users', false);
    }
  };

  const handleExportAuditLog = async () => {
    setItemLoading('audit-log', true);
    try {
      const blob = await api.admin.exportAuditLogs();
      triggerDownload(blob, `audit-log-${new Date().toISOString().slice(0, 10)}.csv`);
      toast({ title: 'Audit log exported' });
    } catch {
      toast({ title: 'Audit log export failed', variant: 'destructive' });
    } finally {
      setItemLoading('audit-log', false);
    }
  };

  const handleExportApiKeys = async () => {
    setItemLoading('api-keys', true);
    try {
      const result = await api.admin.getApiKeysWithStats();
      const headers = ['Name', 'Prefix', 'Created', 'LastUsed', 'TotalRequests', 'Requests30d'];
      const rows = result.keys.map(k => [
        k.apiKey.keyName,
        k.apiKey.keyPrefix,
        new Date(k.apiKey.createdAt).toLocaleDateString(),
        k.apiKey.lastUsedAt ? new Date(k.apiKey.lastUsedAt).toLocaleDateString() : 'Never',
        k.usageStats.totalUsageCount,
        k.usageStats.usageCountLast30Days,
      ]);
      const csv = arrayToCsv(headers, rows);
      triggerDownload(
        new Blob([csv], { type: 'text/csv' }),
        `api-keys-${new Date().toISOString().slice(0, 10)}.csv`
      );
      toast({ title: `${result.keys.length} API keys exported` });
    } catch {
      toast({ title: 'API keys export failed', variant: 'destructive' });
    } finally {
      setItemLoading('api-keys', false);
    }
  };

  const handlersMap: Record<ExportCard['id'], () => Promise<void>> = {
    users: handleExportUsers,
    'audit-log': handleExportAuditLog,
    'api-keys': handleExportApiKeys,
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Bulk Export
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">Export platform data in CSV format.</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {EXPORTS.map(card => {
          const Icon = card.icon;
          const isLoading = loading[card.id] ?? false;

          return (
            <div
              key={card.id}
              className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5"
            >
              <div className="flex items-center gap-3 mb-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100/80 dark:bg-amber-900/30">
                  <Icon className="h-5 w-5 text-amber-700 dark:text-amber-400" />
                </div>
                <div>
                  <p className="font-quicksand text-sm font-semibold text-foreground">
                    {card.title}
                  </p>
                  <p className="text-xs text-muted-foreground">{card.description}</p>
                </div>
              </div>

              <div className="flex items-center justify-between">
                <span className="rounded-md bg-slate-100 dark:bg-zinc-700/50 px-2 py-0.5 text-[10px] font-medium text-muted-foreground">
                  {card.format}
                </span>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={isLoading}
                  onClick={handlersMap[card.id]}
                  className="gap-1.5"
                >
                  {isLoading ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Download className="h-3.5 w-3.5" />
                  )}
                  Download
                </Button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
