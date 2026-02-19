'use client';

import type { ComponentType } from 'react';
import {
  ClockIcon,
  LoaderIcon,
  CheckCircle2Icon,
  XCircleIcon,
  LayersIcon,
} from 'lucide-react';

import type { PaginatedQueueResponse } from '../lib/queue-api';

interface QueueStatsBarProps {
  data: PaginatedQueueResponse | null | undefined;
  isLoading: boolean;
}

interface StatItem {
  label: string;
  value: number;
  icon: ComponentType<{ className?: string }>;
  color: string;
}

export function QueueStatsBar({ data, isLoading }: QueueStatsBarProps) {
  const jobs = data?.jobs ?? [];

  const stats: StatItem[] = [
    {
      label: 'Total Jobs',
      value: data?.total ?? 0,
      icon: LayersIcon,
      color: 'text-slate-600 dark:text-slate-400',
    },
    {
      label: 'Queued',
      value: jobs.filter((j) => j.status === 'Queued').length,
      icon: ClockIcon,
      color: 'text-amber-600 dark:text-amber-400',
    },
    {
      label: 'Processing',
      value: jobs.filter((j) => j.status === 'Processing').length,
      icon: LoaderIcon,
      color: 'text-blue-600 dark:text-blue-400',
    },
    {
      label: 'Failed',
      value: jobs.filter((j) => j.status === 'Failed').length,
      icon: XCircleIcon,
      color: 'text-red-600 dark:text-red-400',
    },
  ];

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

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
      {stats.map((stat) => {
        const Icon = stat.icon;
        return (
          <div
            key={stat.label}
            className="flex items-center gap-3 px-4 py-3 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50"
          >
            <Icon className={`h-5 w-5 ${stat.color}`} />
            <div>
              <div className="text-lg font-semibold text-foreground">{stat.value}</div>
              <div className="text-xs text-muted-foreground">
                {stat.label === 'Total Jobs' ? stat.label : `${stat.label} (this page)`}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
