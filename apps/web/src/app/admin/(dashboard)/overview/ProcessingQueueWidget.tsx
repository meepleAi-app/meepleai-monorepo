'use client';

import { useQuery } from '@tanstack/react-query';
import { Loader2Icon, AlertCircleIcon, ClockIcon } from 'lucide-react';
import Link from 'next/link';

import { api } from '@/lib/api';

interface QueueCounts {
  queued: number;
  processing: number;
  completed: number;
  failed: number;
}

export function ProcessingQueueWidget() {
  const { data: counts, isLoading } = useQuery({
    queryKey: ['admin', 'overview', 'queue-counts'],
    queryFn: async (): Promise<QueueCounts> => {
      const [queued, processing, failed] = await Promise.all([
        api.admin.getProcessingQueue({ status: 'Queued', pageSize: 1 }).catch(() => null),
        api.admin.getProcessingQueue({ status: 'Processing', pageSize: 1 }).catch(() => null),
        api.admin.getProcessingQueue({ status: 'Failed', pageSize: 1 }).catch(() => null),
      ]);
      return {
        queued: queued?.total ?? 0,
        processing: processing?.total ?? 0,
        completed: 0, // Not shown individually — just for awareness
        failed: failed?.total ?? 0,
      };
    },
    staleTime: 30_000,
    refetchInterval: 60_000, // Auto-refresh every minute
  });

  if (isLoading) {
    return <div className="h-14 rounded-xl bg-muted/50 animate-pulse" />;
  }

  const hasActivity = counts && (counts.queued > 0 || counts.processing > 0 || counts.failed > 0);

  if (!hasActivity) return null;

  return (
    <Link
      href="/admin/knowledge-base/queue"
      className="flex items-center gap-4 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm px-4 py-3 hover:border-amber-300 dark:hover:border-amber-700 transition-colors"
      data-testid="processing-queue-widget"
    >
      <div className="flex items-center gap-1.5 text-sm font-medium text-foreground">
        <Loader2Icon className="h-4 w-4 text-amber-500 animate-spin" />
        Coda di Elaborazione
      </div>

      <div className="flex items-center gap-3 ml-auto text-xs">
        {(counts?.queued ?? 0) > 0 && (
          <span className="flex items-center gap-1 text-muted-foreground">
            <ClockIcon className="h-3.5 w-3.5" />
            {counts?.queued} in attesa
          </span>
        )}
        {(counts?.processing ?? 0) > 0 && (
          <span className="flex items-center gap-1 text-amber-600 dark:text-amber-400">
            <Loader2Icon className="h-3.5 w-3.5 animate-spin" />
            {counts?.processing} in elaborazione
          </span>
        )}
        {(counts?.failed ?? 0) > 0 && (
          <span className="flex items-center gap-1 text-red-600 dark:text-red-400">
            <AlertCircleIcon className="h-3.5 w-3.5" />
            {counts?.failed} falliti
          </span>
        )}
      </div>
    </Link>
  );
}
