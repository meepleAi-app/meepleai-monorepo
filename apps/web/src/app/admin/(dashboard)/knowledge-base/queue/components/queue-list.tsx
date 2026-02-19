'use client';

import { useCallback, useState, useMemo } from 'react';
import { ChevronLeftIcon, ChevronRightIcon } from 'lucide-react';
import {
  closestCenter,
  DndContext,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useToast } from '@/hooks/use-toast';

import type { PaginatedQueueResponse, QueueFilters, ProcessingJobDto } from '../lib/queue-api';
import { reorderQueue } from '../lib/queue-api';
import { QueueItem } from './queue-item';

interface QueueListProps {
  data: PaginatedQueueResponse | null | undefined;
  isLoading: boolean;
  selectedJobId: string | null;
  onSelectJob: (jobId: string) => void;
  filters: QueueFilters;
  onFiltersChange: (filters: QueueFilters) => void;
}

export function QueueList({
  data,
  isLoading,
  selectedJobId,
  onSelectJob,
  filters,
  onFiltersChange,
}: QueueListProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const page = filters.page ?? 1;
  const totalPages = data?.totalPages ?? 1;
  const [activeId, setActiveId] = useState<string | null>(null);

  // DnD sensors
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 8 },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  // Reorder mutation with optimistic update
  const { mutate: reorderMutate } = useMutation({
    mutationFn: (orderedJobIds: string[]) => reorderQueue(orderedJobIds),
    onMutate: async (orderedJobIds) => {
      // Cancel outgoing refetches
      await queryClient.cancelQueries({ queryKey: ['admin', 'queue', filters] });

      // Snapshot previous value
      const previous = queryClient.getQueryData<PaginatedQueueResponse>(['admin', 'queue', filters]);

      // Optimistically update
      if (previous) {
        const jobMap = new Map(previous.jobs.map((j) => [j.id, j]));
        const reorderedQueued = orderedJobIds
          .map((id) => jobMap.get(id))
          .filter(Boolean) as ProcessingJobDto[];
        const nonQueued = previous.jobs.filter((j) => j.status !== 'Queued');
        queryClient.setQueryData<PaginatedQueueResponse>(
          ['admin', 'queue', filters],
          { ...previous, jobs: [...reorderedQueued, ...nonQueued] },
        );
      }

      return { previous };
    },
    onError: (_err, _vars, context) => {
      // Rollback on error
      if (context?.previous) {
        queryClient.setQueryData(['admin', 'queue', filters], context.previous);
      }
      toast({ title: 'Error', description: 'Failed to reorder queue.', variant: 'destructive' });
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
    },
  });

  const jobs = data?.jobs ?? [];

  // Separate queued (draggable) and non-queued items
  const queuedJobs = useMemo(() => jobs.filter((j) => j.status === 'Queued'), [jobs]);
  const sortableIds = useMemo(() => queuedJobs.map((j) => j.id), [queuedJobs]);

  const activeJob = useMemo(
    () => jobs.find((j) => j.id === activeId),
    [jobs, activeId],
  );

  const handleDragStart = useCallback((event: DragStartEvent) => {
    setActiveId(event.active.id as string);
  }, []);

  const handleDragEnd = useCallback(
    (event: DragEndEvent) => {
      const { active, over } = event;
      setActiveId(null);

      if (!over || active.id === over.id) return;

      // Only allow reordering among Queued items
      const activeJob = jobs.find((j) => j.id === active.id);
      const overJob = jobs.find((j) => j.id === over.id);
      if (!activeJob || !overJob) return;
      if (activeJob.status !== 'Queued' || overJob.status !== 'Queued') return;

      const oldIndex = queuedJobs.findIndex((j) => j.id === active.id);
      const newIndex = queuedJobs.findIndex((j) => j.id === over.id);

      if (oldIndex !== -1 && newIndex !== -1) {
        const newOrder = arrayMove(queuedJobs, oldIndex, newIndex);
        reorderMutate(newOrder.map((j) => j.id));
      }
    },
    [jobs, queuedJobs, reorderMutate],
  );

  if (isLoading) {
    return (
      <div className="space-y-2 p-2">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full rounded-lg" />
        ))}
      </div>
    );
  }

  if (jobs.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <p className="text-sm text-muted-foreground">No jobs found</p>
        <p className="text-xs text-muted-foreground mt-1">
          Try adjusting your filters or enqueue a new PDF
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      <DndContext
        sensors={sensors}
        collisionDetection={closestCenter}
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
      >
        <ScrollArea className="flex-1 min-h-0">
          <SortableContext
            items={sortableIds}
            strategy={verticalListSortingStrategy}
          >
            <div className="space-y-1.5 p-2">
              {jobs.map((job) => (
                <QueueItem
                  key={job.id}
                  job={job}
                  isSelected={selectedJobId === job.id}
                  onSelect={onSelectJob}
                />
              ))}
            </div>
          </SortableContext>
        </ScrollArea>

        <DragOverlay>
          {activeJob ? (
            <div className="bg-white dark:bg-zinc-800 rounded-lg border border-amber-300 dark:border-amber-600 shadow-lg px-4 py-3 opacity-90">
              <span className="text-sm font-medium text-foreground">
                {activeJob.pdfFileName}
              </span>
            </div>
          ) : null}
        </DragOverlay>
      </DndContext>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between px-3 py-2 border-t border-slate-200/50 dark:border-zinc-700/50">
          <span className="text-xs text-muted-foreground">
            {data?.total ?? 0} total &middot; Page {page}/{totalPages}
          </span>
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7"
              disabled={page <= 1}
              onClick={() => onFiltersChange({ ...filters, page: page - 1 })}
            >
              <ChevronLeftIcon className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7"
              disabled={page >= totalPages}
              onClick={() => onFiltersChange({ ...filters, page: page + 1 })}
            >
              <ChevronRightIcon className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
