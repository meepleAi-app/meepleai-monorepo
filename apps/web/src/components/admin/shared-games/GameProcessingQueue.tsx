/**
 * GameProcessingQueue — Mini-widget showing active/queued processing jobs for a game.
 *
 * Displays game-specific job count, global queue depth, individual job rows with
 * priority badges, progress indicators, and a deep-link to the full queue page.
 *
 * Polls every 10 seconds for live updates.
 */

'use client';

import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Progress } from '@/components/ui/feedback/progress';
import { api } from '@/lib/api';

// ─── Priority helpers ────────────────────────────────────────────────────────

type PriorityConfig = {
  label: string;
  variant: 'destructive' | 'default' | 'secondary' | 'outline';
};

function getPriorityConfig(priority: number): PriorityConfig {
  if (priority >= 3) return { label: 'Urgente', variant: 'destructive' };
  if (priority === 2) return { label: 'Alta', variant: 'default' };
  if (priority === 1) return { label: 'Normale', variant: 'secondary' };
  return { label: 'Bassa', variant: 'outline' };
}

// ─── Component ───────────────────────────────────────────────────────────────

interface GameProcessingQueueProps {
  gameId: string;
}

export function GameProcessingQueue({ gameId }: GameProcessingQueueProps) {
  const { data: gameQueue } = useQuery({
    queryKey: ['admin', 'queue', 'game', gameId],
    queryFn: () =>
      api.admin.getQueueJobs({
        gameId,
        limit: 5,
        status: ['Queued', 'Processing'],
      }),
    refetchInterval: 10_000,
  });

  const { data: queueStatus } = useQuery({
    queryKey: ['admin', 'queue', 'status'],
    queryFn: () => api.admin.getQueueStatus(),
    refetchInterval: 10_000,
  });

  const gameJobCount = gameQueue?.total ?? 0;
  const globalQueueDepth = queueStatus?.queueDepth ?? 0;

  // Hide widget entirely when there are no jobs anywhere
  if (gameJobCount === 0 && globalQueueDepth === 0) {
    return null;
  }

  const jobs = gameQueue?.jobs ?? [];

  return (
    <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
      <CardHeader className="pb-3">
        <CardTitle className="font-quicksand text-base">Coda elaborazione</CardTitle>
        <CardDescription>
          {gameJobCount} di questo gioco &middot; {globalQueueDepth} totali
        </CardDescription>
      </CardHeader>

      {jobs.length > 0 && (
        <CardContent className="space-y-2">
          {jobs.map(job => {
            const priorityCfg = getPriorityConfig(job.priority);
            const isProcessing = job.status === 'Processing';

            return (
              <div
                key={job.id}
                className="flex items-center gap-3 rounded-lg border px-3 py-2 text-sm"
              >
                <Badge variant={priorityCfg.variant} className="shrink-0 text-xs">
                  {priorityCfg.label}
                </Badge>
                <span className="flex-1 truncate text-xs">{job.pdfFileName}</span>
                <div className="w-24 shrink-0">
                  {isProcessing ? (
                    <Progress value={50} className="h-1.5" />
                  ) : (
                    <span className="text-xs text-muted-foreground">In coda</span>
                  )}
                </div>
              </div>
            );
          })}
        </CardContent>
      )}

      <CardFooter className="pt-2">
        <Link
          href={`/admin/knowledge-base/queue?gameId=${gameId}`}
          className="text-xs text-primary underline underline-offset-2 hover:text-primary/80"
        >
          Apri coda completa &rarr;
        </Link>
      </CardFooter>
    </Card>
  );
}
