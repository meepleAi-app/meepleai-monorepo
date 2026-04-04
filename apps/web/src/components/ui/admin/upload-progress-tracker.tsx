'use client';

import { useEffect, useState } from 'react';

import { usePdfProgress } from '@/hooks/usePdfProgress';
import { cn } from '@/lib/utils';
import type { PdfState } from '@/types/pdf';

// ============================================================================
// Types
// ============================================================================

interface UploadProgressTrackerProps {
  documentId: string | null;
  fileName?: string;
  className?: string;
}

type StepStatus = 'done' | 'current' | 'pending' | 'failed';

interface PipelineStep {
  key: PdfState;
  label: string;
}

// ============================================================================
// Constants
// ============================================================================

const PIPELINE_STEPS: PipelineStep[] = [
  { key: 'extracting', label: 'Estrazione' },
  { key: 'chunking', label: 'Chunking' },
  { key: 'embedding', label: 'Embedding' },
  { key: 'indexing', label: 'Indicizzazione' },
];

// Order of states for determining progress
const STATE_ORDER: PdfState[] = [
  'pending',
  'uploading',
  'extracting',
  'chunking',
  'embedding',
  'indexing',
  'ready',
  'failed',
];

const STATUS_ICONS: Record<StepStatus, string> = {
  done: '✓',
  current: '●',
  pending: '○',
  failed: '✕',
};

const STATUS_STYLES: Record<StepStatus, string> = {
  done: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400',
  current: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
  pending: 'bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-500',
  failed: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400',
};

// ============================================================================
// Helpers
// ============================================================================

function getStepStatus(stepKey: PdfState, currentState: PdfState): StepStatus {
  const currentIdx = STATE_ORDER.indexOf(currentState);
  const stepIdx = STATE_ORDER.indexOf(stepKey);

  if (currentState === 'failed') {
    // The step that was current when failed shows as failed
    // Steps before the failed step are done
    // The failed state maps to the last processing step before 'ready'
    // We show the step that was the last active one as failed
    if (stepIdx < currentIdx - 1) return 'done';
    if (stepIdx === currentIdx - 1) return 'failed';
    return 'pending';
  }

  if (currentState === 'ready') {
    return 'done';
  }

  if (stepIdx < currentIdx) return 'done';
  if (stepIdx === currentIdx) return 'current';
  return 'pending';
}

// ============================================================================
// Component
// ============================================================================

export function UploadProgressTracker({
  documentId,
  fileName,
  className,
}: UploadProgressTrackerProps) {
  const { status, isConnected } = usePdfProgress(documentId);
  const [hidden, setHidden] = useState(false);

  // Auto-hide 5 seconds after reaching a terminal state
  const currentState = status?.state;
  useEffect(() => {
    if (currentState !== 'ready' && currentState !== 'failed') return;

    const timer = setTimeout(() => {
      setHidden(true);
    }, 5000);

    return () => clearTimeout(timer);
  }, [currentState]);

  if (!documentId || !status || hidden) return null;

  const isTerminal = status.state === 'ready' || status.state === 'failed';
  const showPollingWarning = !isConnected && !isTerminal;

  return (
    <div
      data-testid="upload-progress-tracker"
      className={cn('rounded-lg border border-border bg-card px-4 py-3 space-y-3', className)}
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-foreground">
          {fileName ? (
            <>
              Elaborazione: <span className="font-semibold">{fileName}</span>
            </>
          ) : (
            'Elaborazione PDF in corso...'
          )}
        </span>
        <span className="text-xs text-muted-foreground">{Math.round(status.progress)}%</span>
      </div>

      {/* Progress bar */}
      <div
        className="h-1.5 w-full overflow-hidden rounded-full bg-secondary"
        role="progressbar"
        aria-valuenow={Math.round(status.progress)}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label="Progresso elaborazione PDF"
      >
        <div
          className="h-full rounded-full bg-primary transition-all duration-500"
          style={{ width: `${Math.min(100, Math.max(0, status.progress))}%` }}
        />
      </div>

      {/* Pipeline steps */}
      <div className="flex items-center gap-2 flex-wrap">
        {PIPELINE_STEPS.map((step, i) => {
          const stepStatus = getStepStatus(step.key, status.state);
          return (
            <div key={step.key} className="flex items-center gap-2">
              {i > 0 && <span className="text-muted-foreground text-xs">→</span>}
              <span
                data-testid={`progress-step-${step.key}`}
                data-status={stepStatus}
                className={cn(
                  'rounded-full px-2 py-0.5 text-xs font-medium',
                  STATUS_STYLES[stepStatus]
                )}
              >
                {STATUS_ICONS[stepStatus]} {step.label}
              </span>
            </div>
          );
        })}
      </div>

      {/* ETA */}
      {status.eta && !isTerminal && (
        <p className="text-xs text-muted-foreground">Tempo rimanente stimato: {status.eta}</p>
      )}

      {/* Error message */}
      {status.state === 'failed' && status.errorMessage && (
        <p data-testid="progress-error-message" className="text-xs text-destructive">
          {status.errorMessage}
        </p>
      )}

      {/* Polling warning */}
      {showPollingWarning && (
        <p className="text-xs text-muted-foreground">Connessione SSE persa, polling attivo...</p>
      )}
    </div>
  );
}
