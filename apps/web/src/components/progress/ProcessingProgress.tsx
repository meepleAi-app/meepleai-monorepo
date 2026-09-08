/**
 * ProcessingProgress Component (PDF-08)
 * Displays real-time PDF processing progress with polling and cancellation support
 */

'use client';

import { useEffect, useState, useCallback, useRef } from 'react';

import { api, type ProcessingProgress as ApiProcessingProgress } from '@/lib/api';
import { ProcessingStep, isProcessingComplete, getStepLabel, getStepOrder } from '@/types/pdf';

/** Transformed progress with estimatedTimeRemaining in seconds (converted from API TimeSpan string) */
interface ProcessingProgressType {
  currentStep: ProcessingStep;
  percentComplete: number;
  estimatedTimeRemaining?: number;
  errorMessage?: string;
  updatedAt: string;
}

import { SkeletonLoader } from '../loading';

interface ProcessingProgressProps {
  pdfId: string;
  onComplete?: () => void;
  onError?: (error: string) => void;
}

const POLL_INTERVAL_MS = 2000;
const MAX_POLL_DURATION_MS = 600000; // 10 minutes max

/**
 * Parse .NET TimeSpan string format "HH:mm:ss.fffffff" to seconds.
 * Returns undefined for null/empty values.
 */
function parseTimeSpanToSeconds(timeSpan: string | null | undefined): number | undefined {
  if (!timeSpan) return undefined;

  // Format: "HH:mm:ss.fffffff" or "HH:mm:ss"
  // eslint-disable-next-line security/detect-unsafe-regex -- Safe: bounded quantifiers, no nested repetition
  const match = timeSpan.match(/^(\d+):(\d+):(\d+)(?:\.(\d+))?$/);
  if (!match) return undefined;

  const hours = parseInt(match[1], 10);
  const minutes = parseInt(match[2], 10);
  const seconds = parseInt(match[3], 10);

  return hours * 3600 + minutes * 60 + seconds;
}

/**
 * Transform API ProcessingProgress to component ProcessingProgress.
 * Now that the API schema matches the backend, we can directly use currentStep.
 * Issue #3371: Schema alignment with backend ProcessingProgress model.
 */
function transformApiProgress(apiProgress: ApiProcessingProgress): ProcessingProgressType {
  return {
    currentStep: apiProgress.currentStep as ProcessingStep,
    percentComplete: apiProgress.percentComplete,
    estimatedTimeRemaining: parseTimeSpanToSeconds(apiProgress.estimatedTimeRemaining),
    errorMessage: apiProgress.errorMessage || undefined,
    updatedAt: new Date().toISOString(),
  };
}

/**
 * Formats seconds into human-readable time (e.g., "2 min 30 sec")
 */
function formatTimeRemaining(seconds: number): string {
  if (seconds <= 0) {
    return 'Less than a minute';
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;

  if (minutes === 0) {
    return `${remainingSeconds} sec`;
  }

  if (remainingSeconds === 0) {
    return `${minutes} min`;
  }

  return `${minutes} min ${remainingSeconds} sec`;
}

export function ProcessingProgress({ pdfId, onComplete, onError }: ProcessingProgressProps) {
  const [progress, setProgress] = useState<ProcessingProgressType | null>(null);
  const [loading, setLoading] = useState(true);
  const [networkError, setNetworkError] = useState<string | null>(null);
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [canceling, setCanceling] = useState(false);

  const pollStartTimeRef = useRef<number>(Date.now());
  const intervalRef = useRef<NodeJS.Timeout | null>(null);
  const isMountedRef = useRef(true);
  const latestProgressRef = useRef<ProcessingProgressType | null>(null);
  const hasNotifiedTerminalRef = useRef(false);

  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    hasNotifiedTerminalRef.current = false;
    latestProgressRef.current = null;
    setProgress(null);
    setLoading(true);
    setNetworkError(null);
  }, [pdfId]);

  // Fetch progress from API
  const fetchProgress = useCallback(async () => {
    try {
      const data = await api.pdf.getProcessingProgress(pdfId);

      if (!isMountedRef.current) {
        return;
      }

      if (!data) {
        setNetworkError('Failed to fetch processing progress');
        setLoading(false);
        return;
      }

      const transformedProgress = transformApiProgress(data);

      latestProgressRef.current = transformedProgress;
      setProgress(transformedProgress);
      setNetworkError(null);
      setLoading(false);

      // Check for completion.
      // Issue #3878: lo stato terminale notifica UNA volta sola, sia in successo
      // sia in fallimento. Prima `onError` era subordinato a `errorMessage`, che il
      // contratto dichiara `nullable().optional()`: un fallimento senza messaggio
      // non veniva segnalato affatto e il chiamante non sapeva di dover reagire.
      if (
        isProcessingComplete(transformedProgress.currentStep) &&
        !hasNotifiedTerminalRef.current
      ) {
        hasNotifiedTerminalRef.current = true;

        if (transformedProgress.currentStep === ProcessingStep.Completed) {
          onComplete?.();
        } else {
          onError?.(transformedProgress.errorMessage ?? 'Processing failed with no reported cause');
        }
      }
    } catch (error) {
      if (!isMountedRef.current) {
        return;
      }

      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      setNetworkError(errorMessage);
      setLoading(false);
    }
  }, [pdfId, onComplete, onError]);

  // Setup polling
  useEffect(() => {
    pollStartTimeRef.current = Date.now();

    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }

    // Initial fetch
    void fetchProgress();

    // Setup interval for polling
    intervalRef.current = setInterval(() => {
      if (!isMountedRef.current) {
        return;
      }

      // Check max poll duration
      const elapsed = Date.now() - pollStartTimeRef.current;
      if (elapsed > MAX_POLL_DURATION_MS) {
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
        setNetworkError(
          'Processing timeout exceeded (10 minutes). Please refresh to check status.'
        );
        return;
      }

      const currentProgress = latestProgressRef.current;
      if (currentProgress && isProcessingComplete(currentProgress.currentStep)) {
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
          intervalRef.current = null;
        }
        return;
      }

      void fetchProgress();
    }, POLL_INTERVAL_MS);

    // Cleanup on unmount
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, [fetchProgress]);

  // Handle cancel
  const handleCancelClick = useCallback(() => {
    setShowCancelDialog(true);
  }, []);

  const handleConfirmCancel = useCallback(async () => {
    setCanceling(true);
    setShowCancelDialog(false);

    try {
      await api.pdf.cancelProcessing(pdfId);

      if (!isMountedRef.current) {
        return;
      }

      // Fetch updated status
      await fetchProgress();
    } catch (error) {
      if (!isMountedRef.current) {
        return;
      }

      const errorMessage = error instanceof Error ? error.message : 'Failed to cancel processing';
      setNetworkError(errorMessage);
    } finally {
      if (isMountedRef.current) {
        setCanceling(false);
      }
    }
  }, [pdfId, fetchProgress]);

  const handleCancelDialogClose = useCallback(() => {
    setShowCancelDialog(false);
  }, []);

  // Presentazione (#3878): classi semantiche al posto degli stili inline.
  // Gli stili inline erano invisibili a `local/no-hardcoded-color-utility`, che
  // ispeziona solo l'attributo `className`: il pannello aveva colori fissi e in
  // tema scuro restava chiaro su fondo scuro. I token seguono il tema.
  const containerClass = 'rounded-lg border border-border bg-card p-6';

  /**
   * `--c-success`/`--c-warning` passano AA solo come decorativo/bordo; per il
   * TESTO il design system impone le varianti `-ink` (design-tokens-canonical.css).
   */
  const progressBarFillClass =
    progress?.currentStep === ProcessingStep.Completed
      ? 'bg-[hsl(var(--c-success))]'
      : progress?.currentStep === ProcessingStep.Failed
        ? 'bg-destructive'
        : 'bg-primary';

  const stepIndicatorClass = (step: ProcessingStep): string => {
    const currentStepOrder = progress ? getStepOrder(progress.currentStep) : -1;
    const stepOrder = getStepOrder(step);
    const isActive = progress?.currentStep === step;
    const isCompleted = stepOrder < currentStepOrder;

    if (isActive) {
      return 'flex-1 rounded border-2 border-primary bg-primary/10 px-1 py-2 text-center text-xs font-semibold text-primary';
    }
    if (isCompleted) {
      return 'flex-1 rounded border-2 border-[hsl(var(--c-success)/0.4)] bg-[hsl(var(--c-success)/0.1)] px-1 py-2 text-center text-xs text-[hsl(var(--c-success-ink))]';
    }
    return 'flex-1 rounded border-2 border-border bg-muted px-1 py-2 text-center text-xs text-muted-foreground';
  };

  // Non-terminal steps for step indicator
  const steps = [
    ProcessingStep.Uploading,
    ProcessingStep.Extracting,
    ProcessingStep.Chunking,
    ProcessingStep.Embedding,
    ProcessingStep.Indexing,
  ];

  if (loading && !progress) {
    return (
      <div className={containerClass}>
        <SkeletonLoader variant="processingProgress" ariaLabel="Loading processing progress" />
      </div>
    );
  }

  return (
    <div data-testid="processing-progress" className={containerClass}>
      <h3 className="mb-4 mt-0 text-lg font-semibold text-foreground">PDF Processing Progress</h3>

      {/* Progress Bar */}
      <div
        role="progressbar"
        aria-label="PDF processing progress"
        aria-valuenow={progress?.percentComplete ?? 0}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-live="polite"
        className="mb-4 h-3 w-full overflow-hidden rounded-full bg-muted"
      >
        <div
          className={`h-full transition-[width] duration-500 ease-out motion-reduce:transition-none ${progressBarFillClass}`}
          style={{ width: `${progress?.percentComplete ?? 0}%` }}
        />
      </div>

      {/* Step Indicators */}
      <div className="mb-5 flex justify-between gap-2" aria-label="Processing steps">
        {steps.map(step => (
          <div key={step} className={stepIndicatorClass(step)} title={getStepLabel(step)}>
            {step}
          </div>
        ))}
      </div>

      {/* Network Error - Display even when progress is null */}
      {/* This allows errors to be shown during initial fetch failures */}
      {networkError && (
        <div
          className="mb-4 rounded border border-[hsl(var(--c-warning)/0.4)] bg-[hsl(var(--c-warning)/0.1)] p-3 text-sm text-[hsl(var(--c-warning-ink))]"
          role="alert"
        >
          <strong>Network Error:</strong> {networkError}
        </div>
      )}

      {/* Current Status */}
      {progress && (
        <div>
          <p className="mb-3 text-base font-medium text-foreground">
            <strong>Processing status:</strong> {getStepLabel(progress.currentStep)}
          </p>

          {/* Time Remaining */}
          {progress.estimatedTimeRemaining !== undefined &&
            progress.estimatedTimeRemaining !== null &&
            !isProcessingComplete(progress.currentStep) && (
              <p className="mb-4 text-sm text-muted-foreground">
                <strong>Estimated time remaining:</strong>{' '}
                {formatTimeRemaining(progress.estimatedTimeRemaining)}
              </p>
            )}

          {/* Progress Percentage */}
          <p className="mb-4 text-sm text-muted-foreground">
            <strong>Progress:</strong> {progress.percentComplete}%
          </p>

          {/* Error Message - Processing failure errors */}
          {progress.currentStep === ProcessingStep.Failed && progress.errorMessage && (
            <div
              className="mb-4 rounded border border-destructive bg-destructive/10 p-3 text-sm text-destructive"
              role="alert"
            >
              <strong>Error:</strong> {progress.errorMessage}
            </div>
          )}

          {/* Cancel Button */}
          {!isProcessingComplete(progress.currentStep) && (
            <div className="flex gap-3">
              <button
                onClick={handleCancelClick}
                disabled={canceling}
                className="rounded bg-destructive px-5 py-2.5 text-sm font-medium text-destructive-foreground disabled:cursor-not-allowed disabled:opacity-60"
                aria-label="Cancel PDF processing"
              >
                {canceling ? 'Canceling...' : 'Cancel Processing'}
              </button>
            </div>
          )}
        </div>
      )}

      {/* Cancel Confirmation Dialog */}
      {showCancelDialog && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[hsl(var(--c-overlay-scrim))]"
          onClick={handleCancelDialogClose}
          role="dialog"
          aria-modal="true"
          aria-labelledby="cancel-dialog-title"
        >
          <div
            className="w-[90%] max-w-md rounded-lg bg-card p-6"
            onClick={e => e.stopPropagation()}
          >
            <h4
              id="cancel-dialog-title"
              className="mb-4 mt-0 text-lg font-semibold text-foreground"
            >
              Cancel PDF Processing?
            </h4>
            <p className="mb-5 text-sm leading-relaxed text-muted-foreground">
              Are you sure you want to cancel the PDF processing? This action cannot be undone.
            </p>
            <div className="flex justify-end gap-3">
              <button
                onClick={handleCancelDialogClose}
                className="rounded bg-secondary px-4 py-2 text-sm font-medium text-secondary-foreground"
              >
                No, Continue Processing
              </button>
              <button
                onClick={handleConfirmCancel}
                className="rounded bg-destructive px-4 py-2 text-sm font-medium text-destructive-foreground"
              >
                Yes, Cancel
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
