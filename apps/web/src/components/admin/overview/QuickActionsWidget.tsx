'use client';

import { useState } from 'react';

import { Trash2, ListOrdered, RefreshCw, Download, HeartPulse, Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { toast } from '@/components/layout';

interface QuickAction {
  id: string;
  label: string;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
  type: 'navigate' | 'api';
  href?: string;
}

const QUICK_ACTIONS: QuickAction[] = [
  {
    id: 'clear-cache',
    label: 'Clear Cache',
    description: 'Flush Redis & KB cache',
    icon: Trash2,
    type: 'api',
  },
  {
    id: 'view-queue',
    label: 'View Queue',
    description: 'Processing queue status',
    icon: ListOrdered,
    type: 'navigate',
    href: '/admin/knowledge-base/queue',
  },
  {
    id: 'reindex-all',
    label: 'Reindex All',
    description: 'Rebuild vector indices',
    icon: RefreshCw,
    type: 'api',
  },
  {
    id: 'export-users',
    label: 'Export Users',
    description: 'Download user CSV',
    icon: Download,
    type: 'api',
  },
  {
    id: 'system-health',
    label: 'System Health',
    description: 'Infrastructure status',
    icon: HeartPulse,
    type: 'navigate',
    href: '/admin/monitor?tab=infra',
  },
];

export function QuickActionsWidget() {
  const router = useRouter();
  const [loadingId, setLoadingId] = useState<string | null>(null);

  async function handleAction(action: QuickAction) {
    if (action.type === 'navigate' && action.href) {
      router.push(action.href);
      return;
    }

    setLoadingId(action.id);
    try {
      // API endpoints not yet connected — show placeholder feedback
      await new Promise(resolve => setTimeout(resolve, 800));
      toast.info(`${action.label}: API endpoint not yet connected`);
    } finally {
      setLoadingId(null);
    }
  }

  return (
    <div>
      <h2 className="font-quicksand text-sm font-semibold text-foreground mb-3">Quick Actions</h2>
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        {QUICK_ACTIONS.map(action => {
          const Icon = action.icon;
          const isLoading = loadingId === action.id;

          return (
            <button
              key={action.id}
              onClick={() => handleAction(action)}
              disabled={isLoading}
              className="flex flex-col items-center gap-2 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/60 dark:bg-zinc-900/60 backdrop-blur-md p-4 text-center transition-all hover:border-amber-300/60 hover:bg-amber-50/30 dark:hover:bg-amber-900/10 disabled:opacity-60"
            >
              <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-amber-100/80 dark:bg-amber-900/30">
                {isLoading ? (
                  <Loader2 className="h-4 w-4 text-amber-700 dark:text-amber-400 animate-spin" />
                ) : (
                  <Icon className="h-4 w-4 text-amber-700 dark:text-amber-400" />
                )}
              </div>
              <div>
                <p className="font-quicksand text-xs font-semibold text-foreground">
                  {action.label}
                </p>
                <p className="text-[10px] text-muted-foreground leading-tight mt-0.5">
                  {action.description}
                </p>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}
