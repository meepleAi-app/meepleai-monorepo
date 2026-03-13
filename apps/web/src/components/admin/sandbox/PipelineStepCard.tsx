'use client';

import { useState } from 'react';

import { ChevronDown, AlertCircle, Clock } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Collapsible, CollapsibleTrigger, CollapsibleContent } from '@/components/ui/collapsible';
import { cn } from '@/lib/utils';

import type { PipelineStepInfo, StepStatus } from './contexts/PipelineContext';

/** Step-specific detail data for expanded view */
export interface StepDetails {
  /** Extraction: number of pages found */
  pageCount?: number;
  /** Chunking: sample chunk previews (first N) */
  chunkSamples?: string[];
  /** Embedding: total vector count stored */
  vectorCount?: number;
  /** Arbitrary key-value parameters used for this step */
  parameters?: Record<string, string | number>;
}

interface PipelineStepCardProps {
  step: PipelineStepInfo;
  details?: StepDetails;
}

const STATUS_BADGE_VARIANT: Record<
  StepStatus,
  'default' | 'secondary' | 'destructive' | 'outline'
> = {
  completed: 'default',
  in_progress: 'secondary',
  failed: 'destructive',
  pending: 'outline',
};

const STATUS_LABEL: Record<StepStatus, string> = {
  completed: 'Completato',
  in_progress: 'In corso',
  failed: 'Errore',
  pending: 'In attesa',
};

const STEP_LABEL: Record<string, string> = {
  upload: 'Upload',
  extraction: 'Estrazione',
  chunking: 'Chunking',
  embedding: 'Embedding',
  ready: 'Pronto',
};

function formatDuration(ms?: number): string | null {
  if (ms === undefined || ms === null) return null;
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function truncateText(text: string, maxLines: number): string {
  const lines = text.split('\n').slice(0, maxLines);
  const result = lines.join('\n');
  if (result.length > 200) return result.slice(0, 200) + '...';
  return result;
}

export function PipelineStepCard({ step, details }: PipelineStepCardProps) {
  const [open, setOpen] = useState(false);
  const duration = formatDuration(step.durationMs);
  const hasDetails =
    details &&
    (details.pageCount !== undefined ||
      (details.chunkSamples && details.chunkSamples.length > 0) ||
      details.vectorCount !== undefined ||
      (details.parameters && Object.keys(details.parameters).length > 0));

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <div
        data-testid={`step-card-${step.step}`}
        className={cn(
          'rounded-lg border bg-white/70 backdrop-blur-sm transition-colors',
          step.status === 'failed' && 'border-red-300 bg-red-50/70'
        )}
      >
        {/* Header row: always visible */}
        <CollapsibleTrigger asChild>
          <button
            className="flex w-full items-center justify-between px-4 py-3 text-left"
            aria-label={`Dettagli step ${STEP_LABEL[step.step] ?? step.step}`}
          >
            <div className="flex items-center gap-3">
              <span className="font-quicksand text-sm font-semibold">
                {STEP_LABEL[step.step] ?? step.step}
              </span>
              <Badge variant={STATUS_BADGE_VARIANT[step.status]}>{STATUS_LABEL[step.status]}</Badge>
            </div>
            <div className="flex items-center gap-2">
              {duration && (
                <span className="flex items-center gap-1 font-nunito text-xs text-muted-foreground">
                  <Clock className="h-3 w-3" />
                  {duration}
                </span>
              )}
              {hasDetails && (
                <ChevronDown
                  className={cn(
                    'h-4 w-4 text-muted-foreground transition-transform',
                    open && 'rotate-180'
                  )}
                />
              )}
            </div>
          </button>
        </CollapsibleTrigger>

        {/* Expanded content */}
        <CollapsibleContent>
          <div className="border-t px-4 py-3 space-y-3">
            {/* Error message */}
            {step.error && (
              <div
                data-testid="step-error"
                className="flex items-start gap-2 rounded-md bg-red-50 p-2 text-sm text-red-700"
              >
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                <span className="font-nunito">{step.error}</span>
              </div>
            )}

            {/* Extraction details */}
            {details?.pageCount !== undefined && (
              <div className="font-nunito text-sm">
                <span className="text-muted-foreground">Pagine estratte: </span>
                <span className="font-semibold" data-testid="page-count">
                  {details.pageCount}
                </span>
              </div>
            )}

            {/* Chunk samples */}
            {details?.chunkSamples && details.chunkSamples.length > 0 && (
              <div className="space-y-2">
                <span className="font-nunito text-xs text-muted-foreground">
                  Anteprima chunk ({Math.min(details.chunkSamples.length, 3)} di{' '}
                  {details.chunkSamples.length}):
                </span>
                {details.chunkSamples.slice(0, 3).map((chunk, i) => (
                  <pre
                    key={i}
                    data-testid={`chunk-sample-${i}`}
                    className="rounded-md bg-gray-50 p-2 font-mono text-xs text-gray-700 line-clamp-2"
                  >
                    {truncateText(chunk, 2)}
                  </pre>
                ))}
              </div>
            )}

            {/* Vector count */}
            {details?.vectorCount !== undefined && (
              <div className="font-nunito text-sm">
                <span className="text-muted-foreground">Vettori generati: </span>
                <span className="font-semibold" data-testid="vector-count">
                  {details.vectorCount.toLocaleString()}
                </span>
              </div>
            )}

            {/* Parameters */}
            {details?.parameters && Object.keys(details.parameters).length > 0 && (
              <div className="space-y-1">
                <span className="font-nunito text-xs text-muted-foreground">Parametri:</span>
                <div className="grid grid-cols-2 gap-x-4 gap-y-1">
                  {Object.entries(details.parameters).map(([key, value]) => (
                    <div key={key} className="font-nunito text-xs">
                      <span className="text-muted-foreground">{key}: </span>
                      <span className="font-medium">{String(value)}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}
