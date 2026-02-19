'use client';

import {
  CheckCircle2Icon,
  LoaderIcon,
  CircleIcon,
  XCircleIcon,
} from 'lucide-react';

import type { ProcessingStepDto } from '../lib/queue-api';

interface JobStepTimelineProps {
  steps: ProcessingStepDto[];
}

function getStepIcon(status: string) {
  switch (status) {
    case 'Completed':
      return <CheckCircle2Icon className="h-4 w-4 text-emerald-500" />;
    case 'Processing':
      return <LoaderIcon className="h-4 w-4 text-blue-500 animate-spin" />;
    case 'Failed':
      return <XCircleIcon className="h-4 w-4 text-red-500" />;
    case 'Pending':
    default:
      return <CircleIcon className="h-4 w-4 text-muted-foreground" />;
  }
}

function formatDuration(ms: number | null): string {
  if (ms == null) return '-';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

export function JobStepTimeline({ steps }: JobStepTimelineProps) {
  if (steps.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4">No steps recorded yet.</p>
    );
  }

  return (
    <div className="space-y-0">
      {steps.map((step, i) => {
        const isLast = i === steps.length - 1;
        return (
          <div key={step.id} className="flex gap-3">
            {/* Vertical line + icon */}
            <div className="flex flex-col items-center">
              <div className="pt-0.5">{getStepIcon(step.status)}</div>
              {!isLast && (
                <div className="w-px flex-1 bg-slate-200 dark:bg-zinc-700 my-1" />
              )}
            </div>

            {/* Content */}
            <div className="pb-4">
              <div className="flex items-center gap-2">
                <span className="text-sm font-medium text-foreground">
                  {step.stepName}
                </span>
                <span className="text-xs text-muted-foreground">
                  {formatDuration(step.durationMs)}
                </span>
              </div>
              {step.metadataJson && (
                <p className="text-xs text-muted-foreground mt-0.5">
                  {tryParseMetadata(step.metadataJson)}
                </p>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

function tryParseMetadata(json: string): string {
  try {
    const obj = JSON.parse(json);
    // Show key metrics from metadata
    const parts: string[] = [];
    if (obj.chunks) parts.push(`${obj.chunks} chunks`);
    if (obj.pages) parts.push(`${obj.pages} pages`);
    if (obj.quality) parts.push(`quality: ${obj.quality}`);
    if (obj.embeddings) parts.push(`${obj.embeddings} embeddings`);
    return parts.length > 0 ? parts.join(' | ') : JSON.stringify(obj);
  } catch {
    return json;
  }
}
