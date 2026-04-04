'use client';

import {
  ListOrderedIcon,
  LoaderIcon,
  ClockIcon,
  CheckCircleIcon,
  XCircleIcon,
  ArrowRightIcon,
  FileIcon,
} from 'lucide-react';
import Link from 'next/link';

import {
  useQueueList,
  useQueueStats,
  type JobStatus,
} from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';
import { PriorityBadge } from '@/components/admin/knowledge-base/priority-badge';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';

const statusStyles: Record<JobStatus, { badge: string; label: string }> = {
  Queued: {
    badge: 'bg-slate-100 text-slate-800 dark:bg-slate-900/30 dark:text-slate-300',
    label: 'In coda',
  },
  Processing: {
    badge: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300',
    label: 'In elaborazione',
  },
  Completed: {
    badge: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300',
    label: 'Completato',
  },
  Failed: {
    badge: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300',
    label: 'Fallito',
  },
  Cancelled: {
    badge: 'bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-300',
    label: 'Annullato',
  },
};

export function QueuePreviewWidget() {
  // Hub preview uses polling only — SSE is reserved for the full queue dashboard
  // to avoid duplicate persistent connections per browser tab
  const { data, isLoading } = useQueueList({ pageSize: 5 }, false);

  const statsQueries = useQueueStats();
  const queuedCount = statsQueries[0]?.data?.total ?? 0;
  const processingCount = statsQueries[1]?.data?.total ?? 0;
  const completedCount = statsQueries[2]?.data?.total ?? 0;
  const failedCount = statsQueries[3]?.data?.total ?? 0;

  const activeJobs = (data?.jobs ?? []).filter(
    j => j.status === 'Processing' || j.status === 'Queued'
  );

  return (
    <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center justify-between text-base">
          <div className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-cyan-500 to-teal-600 flex items-center justify-center">
              <ListOrderedIcon className="h-4 w-4 text-white" />
            </div>
            <span>Coda Elaborazione</span>
          </div>
          <Link
            href="/admin/knowledge-base/queue"
            className="text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400 flex items-center gap-1"
            aria-label="Coda completa"
          >
            Coda completa <ArrowRightIcon className="h-3 w-3" />
          </Link>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {/* Stats Counters */}
        <div className="grid grid-cols-4 gap-2">
          {[
            {
              label: 'In coda',
              count: queuedCount,
              icon: ClockIcon,
              color: 'text-slate-600 dark:text-slate-400',
            },
            {
              label: 'Attivi',
              count: processingCount,
              icon: LoaderIcon,
              color: 'text-blue-600 dark:text-blue-400',
            },
            {
              label: 'Completati',
              count: completedCount,
              icon: CheckCircleIcon,
              color: 'text-green-600 dark:text-green-400',
            },
            {
              label: 'Falliti',
              count: failedCount,
              icon: XCircleIcon,
              color: 'text-red-600 dark:text-red-400',
            },
          ].map(({ label, count, icon: Icon, color }) => (
            <div
              key={label}
              className="text-center p-2 rounded-lg bg-slate-50/50 dark:bg-zinc-900/50"
            >
              <Icon className={`h-3.5 w-3.5 mx-auto mb-0.5 ${color}`} />
              <p className="text-lg font-bold text-slate-900 dark:text-zinc-100">{count}</p>
              <p className="text-[10px] text-muted-foreground">{label}</p>
            </div>
          ))}
        </div>

        {/* Active Jobs */}
        {isLoading && (
          <div className="space-y-2">
            {Array.from({ length: 2 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full rounded-lg" />
            ))}
          </div>
        )}

        {!isLoading && activeJobs.length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-2">Nessun job attivo</p>
        )}

        {activeJobs.map(job => {
          const style = statusStyles[job.status] ?? statusStyles.Queued;
          return (
            <div
              key={job.id}
              className="flex items-center gap-3 p-2 rounded-lg bg-slate-50/50 dark:bg-zinc-900/50 border border-slate-200/30 dark:border-zinc-700/30"
            >
              <FileIcon className="h-4 w-4 text-slate-500 dark:text-zinc-400 shrink-0" />
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-1.5">
                  <p className="text-sm font-medium text-slate-900 dark:text-zinc-100 truncate">
                    {job.pdfFileName}
                  </p>
                  <PriorityBadge priority={job.priority} />
                </div>
                {job.currentStep && (
                  <p className="text-xs text-blue-600 dark:text-blue-400">
                    Step: {job.currentStep}
                  </p>
                )}
              </div>
              <Badge variant="outline" className={style.badge}>
                {job.status === 'Processing' && (
                  <LoaderIcon className="w-3 h-3 mr-1 animate-spin" />
                )}
                {style.label}
              </Badge>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
