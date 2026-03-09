'use client';

import { useState } from 'react';

import { Download, Users, ScrollText, Key, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout';

interface ExportCard {
  id: string;
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

export function BulkExportTab() {
  const [loadingId, setLoadingId] = useState<string | null>(null);

  async function handleExport(card: ExportCard) {
    setLoadingId(card.id);
    try {
      // Backend export endpoints not yet implemented
      await new Promise(resolve => setTimeout(resolve, 600));
      toast.info(`${card.title} export: API endpoint not yet connected`);
    } finally {
      setLoadingId(null);
    }
  }

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {EXPORTS.map(card => {
        const Icon = card.icon;
        const isLoading = loadingId === card.id;

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
                <p className="font-quicksand text-sm font-semibold text-foreground">{card.title}</p>
                <p className="text-xs text-muted-foreground">{card.description}</p>
              </div>
            </div>

            <div className="flex items-center justify-between">
              <span className="rounded-md bg-slate-100 dark:bg-zinc-700/50 px-2 py-0.5 text-[10px] font-medium text-muted-foreground">
                {card.format}
              </span>
              <button
                onClick={() => handleExport(card)}
                disabled={isLoading}
                className="inline-flex items-center gap-1.5 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 px-3 py-1.5 text-xs font-semibold text-amber-900 dark:text-amber-300 transition-colors hover:bg-amber-200/80 dark:hover:bg-amber-900/50 disabled:opacity-60"
              >
                {isLoading ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  <Download className="h-3.5 w-3.5" />
                )}
                Download
              </button>
            </div>
          </div>
        );
      })}
    </div>
  );
}
