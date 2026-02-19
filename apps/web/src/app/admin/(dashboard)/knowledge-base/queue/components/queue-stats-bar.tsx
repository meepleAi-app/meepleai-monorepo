'use client';

import type { ComponentType } from 'react';
import {
  ClockIcon,
  LoaderIcon,
  CheckCircle2Icon,
  XCircleIcon,
} from 'lucide-react';

import { useQueueStats } from '../lib/queue-api';

interface StatItem {
  label: string;
  value: number;
  icon: ComponentType<{ className?: string }>;
  color: string;
  bgColor: string;
}

const STAT_CONFIG: { label: string; icon: ComponentType<{ className?: string }>; color: string; bgColor: string }[] = [
  { label: 'Queued', icon: ClockIcon, color: 'text-amber-600 dark:text-amber-400', bgColor: 'bg-amber-50 dark:bg-amber-900/20' },
  { label: 'Processing', icon: LoaderIcon, color: 'text-blue-600 dark:text-blue-400', bgColor: 'bg-blue-50 dark:bg-blue-900/20' },
  { label: 'Completed', icon: CheckCircle2Icon, color: 'text-emerald-600 dark:text-emerald-400', bgColor: 'bg-emerald-50 dark:bg-emerald-900/20' },
  { label: 'Failed', icon: XCircleIcon, color: 'text-red-600 dark:text-red-400', bgColor: 'bg-red-50 dark:bg-red-900/20' },
];

export function QueueStatsBar() {
  const results = useQueueStats();

  const isLoading = results.some((r) => r.isLoading);

  if (isLoading) {
    return (
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <div
            key={i}
            className="h-16 bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse"
          />
        ))}
      </div>
    );
  }

  const stats: StatItem[] = STAT_CONFIG.map((cfg, i) => ({
    ...cfg,
    value: results[i]?.data?.total ?? 0,
  }));

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
      {stats.map((stat) => {
        const Icon = stat.icon;
        return (
          <div
            key={stat.label}
            className="flex items-center gap-3 px-4 py-3 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50"
          >
            <div className={`p-1.5 rounded-lg ${stat.bgColor}`}>
              <Icon className={`h-4 w-4 ${stat.color}`} />
            </div>
            <div>
              <div className="text-lg font-semibold text-foreground">{stat.value}</div>
              <div className="text-xs text-muted-foreground">{stat.label}</div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
