'use client';

import { ChevronLeftIcon, ChevronRightIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Skeleton } from '@/components/ui/feedback/skeleton';

import type { PaginatedQueueResponse, QueueFilters } from '../lib/queue-api';
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
  const page = filters.page ?? 1;
  const totalPages = data?.totalPages ?? 1;

  if (isLoading) {
    return (
      <div className="space-y-2 p-2">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full rounded-lg" />
        ))}
      </div>
    );
  }

  const jobs = data?.jobs ?? [];

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
      <ScrollArea className="flex-1 min-h-0">
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
      </ScrollArea>

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
