'use client';

import { Download, Users, ScrollText, Key } from 'lucide-react';

import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';

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
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {EXPORTS.map(card => {
        const Icon = card.icon;

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
                disabled
                title="Export not yet available"
                className="inline-flex items-center gap-1.5 rounded-lg bg-amber-100/80 dark:bg-amber-900/30 px-3 py-1.5 text-xs font-semibold text-amber-900 dark:text-amber-300 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <Download className="h-3.5 w-3.5" />
                Download
              </button>
            </div>
          </div>
        );
      })}

      <div className="sm:col-span-2 lg:col-span-3">
        <EmptyFeatureState
          title="Export Massivo"
          description="L'export massivo dei dati non è ancora disponibile."
        />
      </div>
    </div>
  );
}
