'use client';

import type { ComponentType } from 'react';

import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import {
  FileTextIcon,
  ClockIcon,
  LoaderIcon,
  CheckCircle2Icon,
  XCircleIcon,
  BanIcon,
  GripVerticalIcon,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { cn } from '@/lib/utils';

import type { ProcessingJobDto, JobStatus } from '../lib/queue-api';

interface QueueItemProps {
  job: ProcessingJobDto;
  isSelected: boolean;
  onSelect: (jobId: string) => void;
}

const STATUS_CONFIG: Record<
  JobStatus,
  { icon: ComponentType<{ className?: string }>; badge: string; label: string }
> = {
  Queued: {
    icon: ClockIcon,
    badge: 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300',
    label: 'Queued',
  },
  Processing: {
    icon: LoaderIcon,
    badge: 'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300',
    label: 'Processing',
  },
  Completed: {
    icon: CheckCircle2Icon,
    badge: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-900/30 dark:text-emerald-300',
    label: 'Completed',
  },
  Failed: {
    icon: XCircleIcon,
    badge: 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300',
    label: 'Failed',
  },
  Cancelled: {
    icon: BanIcon,
    badge: 'bg-slate-100 text-slate-900 dark:bg-zinc-700/50 dark:text-zinc-300',
    label: 'Cancelled',
  },
};

function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60_000);

  if (diffMin < 1) return 'just now';
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHrs = Math.floor(diffMin / 60);
  if (diffHrs < 24) return `${diffHrs}h ago`;
  const diffDays = Math.floor(diffHrs / 24);
  return `${diffDays}d ago`;
}

export function QueueItem({ job, isSelected, onSelect }: QueueItemProps) {
  const isDraggable = job.status === 'Queued';
  const config = STATUS_CONFIG[job.status];
  const StatusIcon = config.icon;

  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: job.id,
    disabled: !isDraggable,
  });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div ref={setNodeRef} style={style} className={cn(isDragging && 'opacity-50')} {...attributes}>
      <div
        className={cn(
          'flex items-center gap-2 w-full rounded-lg border transition-all px-4 py-3',
          isSelected
            ? 'bg-amber-50/80 dark:bg-amber-900/20 border-amber-300/60 dark:border-amber-600/40'
            : 'bg-white/50 dark:bg-zinc-800/50 border-slate-200/50 dark:border-zinc-700/50 hover:bg-slate-50/80 dark:hover:bg-zinc-800/80'
        )}
      >
        {/* Drag Handle - separate button for Queued items */}
        {isDraggable ? (
          <button
            type="button"
            className="flex items-center justify-center cursor-grab active:cursor-grabbing p-0.5 rounded hover:bg-slate-200/60 dark:hover:bg-zinc-600/60 shrink-0 touch-none"
            aria-label={`Drag to reorder ${job.pdfFileName}`}
            {...listeners}
          >
            <GripVerticalIcon className="h-4 w-4 text-muted-foreground" />
          </button>
        ) : (
          <div className="w-5 shrink-0" />
        )}

        {/* Selection area */}
        <button type="button" onClick={() => onSelect(job.id)} className="flex-1 text-left min-w-0">
          <div className="flex items-center justify-between gap-2">
            <div className="flex items-center gap-2.5 min-w-0">
              <FileTextIcon className="h-4 w-4 shrink-0 text-muted-foreground" />
              <span className="text-sm font-medium text-foreground truncate">
                {job.pdfFileName}
              </span>
            </div>
            <Badge variant="outline" className={`shrink-0 text-[10px] px-1.5 py-0 ${config.badge}`}>
              <StatusIcon className="h-3 w-3 mr-0.5" />
              {config.label}
            </Badge>
          </div>

          <div className="flex items-center gap-3 mt-1.5 text-xs text-muted-foreground">
            <span title={new Date(job.createdAt).toLocaleString()}>
              {formatRelativeTime(job.createdAt)}
            </span>
            {job.currentStep && (
              <span className="text-blue-600 dark:text-blue-400">{job.currentStep}</span>
            )}
            {job.status === 'Queued' && <span>Pri: {job.priority}</span>}
            {job.status === 'Failed' && job.retryCount > 0 && (
              <span className="text-red-600 dark:text-red-400">
                Retry {job.retryCount}/{job.maxRetries}
              </span>
            )}
          </div>
        </button>
      </div>
    </div>
  );
}
