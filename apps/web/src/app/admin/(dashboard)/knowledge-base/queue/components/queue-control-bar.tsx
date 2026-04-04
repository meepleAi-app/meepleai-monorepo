'use client';

import { useCallback, useState } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { PauseIcon, PlayIcon, UsersIcon, AlertTriangleIcon, ClockIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Slider } from '@/components/ui/primitives/slider';
import { useToast } from '@/hooks/useToast';

import { updateQueueConfig, useQueueConfig, useQueueStatus } from '../lib/queue-api';

export function QueueControlBar() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { data: config } = useQueueConfig();
  const { data: status } = useQueueStatus();
  const [workerSliderValue, setWorkerSliderValue] = useState<number | null>(null);

  const togglePauseMutation = useMutation({
    mutationFn: () => updateQueueConfig(!config?.isPaused),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
      toast({
        title: config?.isPaused ? 'Queue resumed' : 'Queue paused',
        description: config?.isPaused
          ? 'Processing will continue.'
          : 'No new jobs will be picked up.',
      });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to update queue.', variant: 'destructive' });
    },
  });

  const updateWorkersMutation = useMutation({
    mutationFn: (workers: number) => updateQueueConfig(undefined, workers),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
      toast({ title: 'Workers updated' });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to update workers.', variant: 'destructive' });
    },
  });

  const handleWorkerCommit = useCallback(
    (value: number[]) => {
      const v = value[0];
      if (v !== config?.maxConcurrentWorkers) {
        updateWorkersMutation.mutate(v);
      }
      setWorkerSliderValue(null);
    },
    [config?.maxConcurrentWorkers, updateWorkersMutation]
  );

  const isPaused = config?.isPaused ?? false;
  const workers = workerSliderValue ?? config?.maxConcurrentWorkers ?? 3;
  const depth = status?.queueDepth ?? 0;
  const threshold = status?.backpressureThreshold ?? 50;
  const underPressure = status?.isUnderPressure ?? false;
  const eta = status?.estimatedWaitMinutes ?? 0;

  return (
    <div className="flex flex-wrap items-center gap-4 px-4 py-3 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/50 dark:border-zinc-700/50">
      {/* Pause/Resume */}
      <Button
        variant={isPaused ? 'default' : 'outline'}
        size="sm"
        onClick={() => togglePauseMutation.mutate()}
        disabled={togglePauseMutation.isPending}
        className={isPaused ? 'bg-amber-600 hover:bg-amber-700 text-white' : ''}
      >
        {isPaused ? (
          <>
            <PlayIcon className="h-4 w-4 mr-1" />
            Resume Queue
          </>
        ) : (
          <>
            <PauseIcon className="h-4 w-4 mr-1" />
            Pause Queue
          </>
        )}
      </Button>

      {isPaused && (
        <Badge
          variant="outline"
          className="bg-amber-50 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300 border-amber-200 dark:border-amber-700"
        >
          Paused
        </Badge>
      )}

      {/* Worker Slider */}
      <div className="flex items-center gap-2 min-w-[180px]">
        <UsersIcon className="h-4 w-4 text-muted-foreground shrink-0" />
        <Slider
          min={1}
          max={10}
          step={1}
          value={[workers]}
          onValueChange={v => setWorkerSliderValue(v[0])}
          onValueCommit={handleWorkerCommit}
          className="w-24"
          disabled={updateWorkersMutation.isPending}
        />
        <span className="text-xs text-muted-foreground whitespace-nowrap">
          {workers} worker{workers !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Capacity */}
      <div className="flex items-center gap-2 ml-auto">
        {underPressure && (
          <Badge
            variant="outline"
            className="bg-red-50 text-red-700 dark:bg-red-900/30 dark:text-red-300 border-red-200 dark:border-red-700"
          >
            <AlertTriangleIcon className="h-3 w-3 mr-1" />
            Backpressure
          </Badge>
        )}

        <span className="text-xs text-muted-foreground">
          {depth}/{threshold} queued
        </span>

        {eta > 0 && (
          <span className="flex items-center gap-1 text-xs text-muted-foreground">
            <ClockIcon className="h-3 w-3" />~{eta < 1 ? '<1' : Math.round(eta)}min ETA
          </span>
        )}
      </div>
    </div>
  );
}
