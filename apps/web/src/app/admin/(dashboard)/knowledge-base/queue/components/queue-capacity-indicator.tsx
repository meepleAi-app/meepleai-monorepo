'use client';

import { cn } from '@/lib/utils';

import { useQueueStatus } from '../lib/queue-api';

export function QueueCapacityIndicator() {
  const { data: status } = useQueueStatus();

  const depth = status?.queueDepth ?? 0;
  const max = 100; // MaxQueueSize from ProcessingJob entity
  const pct = Math.min((depth / max) * 100, 100);

  const barColor = pct >= 80 ? 'bg-red-500' : pct >= 50 ? 'bg-amber-500' : 'bg-emerald-500';

  return (
    <div className="flex items-center gap-3 px-4 py-2 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50">
      <span className="text-xs font-medium text-muted-foreground whitespace-nowrap">
        Queue Capacity
      </span>
      <div className="flex-1 h-2.5 bg-slate-100 dark:bg-zinc-700 rounded-full overflow-hidden">
        <div
          className={cn('h-full rounded-full transition-all duration-500', barColor)}
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="text-xs font-medium text-muted-foreground whitespace-nowrap">
        {depth}/{max}
      </span>
    </div>
  );
}
