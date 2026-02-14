/**
 * PdfStatusTimeline Component (Issue #4217)
 * Vertical stepper showing completed/current/pending states with timestamps
 */

'use client';

import { Check, Loader2 } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { PdfState } from '@/types/pdf';
import { getPdfStateLabel, getPdfStateOrder } from '@/types/pdf';

export interface PdfStatusTimelineProps {
  currentState: PdfState;
  completedStates: PdfState[];
  startedAt?: string;
  estimatedCompletion?: string;
  className?: string;
}

const TIMELINE_STATES: PdfState[] = ['uploading', 'extracting', 'chunking', 'embedding', 'indexing', 'ready'];

function formatTimestamp(timestamp?: string): string {
  if (!timestamp) return '--:--';
  try {
    const date = new Date(timestamp);
    return date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
  } catch {
    return '--:--';
  }
}

export function PdfStatusTimeline({
  currentState,
  completedStates,
  startedAt,
  estimatedCompletion,
  className,
}: PdfStatusTimelineProps) {
  const currentOrder = getPdfStateOrder(currentState);

  return (
    <div className={cn('space-y-0', className)} data-testid="pdf-status-timeline">
      {/* Header */}
      {startedAt && (
        <div className="mb-4 text-sm text-muted-foreground">
          <span>Started: {formatTimestamp(startedAt)}</span>
          {estimatedCompletion && currentState !== 'ready' && currentState !== 'failed' && (
            <span className="ml-4">ETA: {formatTimestamp(estimatedCompletion)}</span>
          )}
        </div>
      )}

      {/* Timeline Steps */}
      {TIMELINE_STATES.map((state, index) => {
        const stateOrder = getPdfStateOrder(state);
        const isCompleted = completedStates.includes(state) || stateOrder < currentOrder;
        const isCurrent = state === currentState;
        const isPending = stateOrder > currentOrder && !completedStates.includes(state);

        return (
          <div key={state} className="relative flex gap-3" data-testid={`timeline-step-${state}`}>
            {/* Connector Line */}
            {index < TIMELINE_STATES.length - 1 && (
              <div
                className={cn(
                  'absolute left-[15px] top-8 w-0.5 h-8',
                  isCompleted ? 'bg-green-500' : 'bg-gray-300 dark:bg-gray-700'
                )}
              />
            )}

            {/* Step Circle */}
            <div
              className={cn(
                'relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2 transition-all',
                isCompleted && 'border-green-500 bg-green-500 text-white',
                isCurrent && 'border-blue-500 bg-blue-500/10 text-blue-600',
                isPending && 'border-gray-300 bg-white text-gray-400 dark:border-gray-700 dark:bg-gray-900'
              )}
            >
              {isCompleted && <Check className="h-4 w-4" />}
              {isCurrent && <Loader2 className="h-4 w-4 animate-spin" />}
              {isPending && <div className="h-2 w-2 rounded-full bg-gray-400" />}
            </div>

            {/* Step Label */}
            <div className="flex-1 pb-6">
              <p
                className={cn(
                  'text-sm font-medium',
                  isCompleted && 'text-green-600 dark:text-green-400',
                  isCurrent && 'text-blue-600 dark:text-blue-400',
                  isPending && 'text-gray-500 dark:text-gray-400'
                )}
              >
                {getPdfStateLabel(state)}
              </p>
              {isCurrent && (
                <p className="text-xs text-muted-foreground mt-0.5">In progress...</p>
              )}
              {isCompleted && (
                <p className="text-xs text-green-600/70 dark:text-green-400/70 mt-0.5">Completed</p>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

export default PdfStatusTimeline;
