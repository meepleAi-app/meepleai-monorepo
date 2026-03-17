/**
 * ProgressModal Component (Issue #4210)
 *
 * Full-screen modal for PDF upload progress with real-time updates.
 *
 * Features:
 * - Progress bar animated (8px height)
 * - 7 step indicators with current step highlighted
 * - Metrics display: pages processed, duration, ETA
 * - Cancel button with confirmation dialog
 * - Glassmorphic design system integration
 * - ARIA live regions for screen readers
 * - SSE real-time updates via usePdfProgress
 *
 * @example
 * ```tsx
 * <ProgressModal
 *   isOpen={isProcessing}
 *   documentId={docId}
 *   onClose={() => setIsProcessing(false)}
 *   onCancel={async () => await api.pdf.cancel(docId)}
 * />
 * ```
 */

'use client';

import * as React from 'react';

import { Check, X, Loader2, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/feedback/progress';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { usePdfProgress } from '@/hooks/usePdfProgress';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';
import { formatTimeSpan } from '@/lib/utils/formatTimeSpan';
import { getStepLabel, getStepOrder, ProcessingStep } from '@/types/pdf';

// ============================================================================
// Types
// ============================================================================

export interface ProgressModalProps {
  /** Document ID for tracking progress */
  documentId: string | null;
  /** Whether modal is open */
  isOpen: boolean;
  /** Callback when user closes modal */
  onClose: () => void;
  /** Callback when user cancels processing (returns promise for API call) */
  onCancel?: () => Promise<void>;
  /** Optional custom title */
  title?: string;
  /** Hide cancel button */
  hideCancelButton?: boolean;
}

// ============================================================================
// Processing Steps Configuration
// ============================================================================

const PROCESSING_STEPS = [
  ProcessingStep.Uploading,
  ProcessingStep.Extracting,
  ProcessingStep.Chunking,
  ProcessingStep.Embedding,
  ProcessingStep.Indexing,
  ProcessingStep.Completed,
  ProcessingStep.Failed,
];

// ============================================================================
// Helper Functions
// ============================================================================

function _getStateFromStep(step: string): string {
  // Map ProcessingStep enum to PdfState
  const mapping: Record<string, string> = {
    Uploading: 'uploading',
    Extracting: 'extracting',
    Chunking: 'chunking',
    Embedding: 'embedding',
    Indexing: 'indexing',
    Completed: 'ready',
    Failed: 'failed',
  };
  return mapping[step] || step.toLowerCase();
}

// ============================================================================
// Component
// ============================================================================

export function ProgressModal({
  documentId,
  isOpen,
  onClose,
  onCancel,
  title = 'Processing PDF',
  hideCancelButton = false,
}: ProgressModalProps): React.ReactElement {
  const [showCancelConfirm, setShowCancelConfirm] = React.useState(false);
  const [isCanceling, setIsCanceling] = React.useState(false);

  // ============================================================================
  // Progress Hook
  // ============================================================================

  const {
    status,
    metrics,
    isConnected,
    isPolling,
    isLoading: _isLoading,
    error,
    metricsError,
  } = usePdfProgress(documentId, {
    onComplete: () => {
      // Auto-close on completion after 2s
      setTimeout(() => {
        onClose();
      }, 2000);
    },
    onError: err => {
      logger.error('[ProgressModal] Error:', err);
    },
  });

  // ============================================================================
  // Current Step Calculation
  // ============================================================================

  const currentStep = React.useMemo(() => {
    if (metrics?.currentState) {
      return metrics.currentState as ProcessingStep;
    }
    if (status?.state) {
      // Map state back to ProcessingStep
      const stateMapping: Record<string, ProcessingStep> = {
        uploading: ProcessingStep.Uploading,
        extracting: ProcessingStep.Extracting,
        chunking: ProcessingStep.Chunking,
        embedding: ProcessingStep.Embedding,
        indexing: ProcessingStep.Indexing,
        ready: ProcessingStep.Completed,
        failed: ProcessingStep.Failed,
      };
      return stateMapping[status.state] || ProcessingStep.Uploading;
    }
    return ProcessingStep.Uploading;
  }, [metrics?.currentState, status?.state]);

  const currentStepOrder = getStepOrder(currentStep);

  // ============================================================================
  // Progress Value
  // ============================================================================

  const progressValue = React.useMemo(() => {
    return metrics?.progressPercentage ?? status?.progress ?? 0;
  }, [metrics?.progressPercentage, status?.progress]);

  // ============================================================================
  // Cancel Handler
  // ============================================================================

  const handleCancel = React.useCallback(async () => {
    if (!onCancel) return;

    setIsCanceling(true);
    try {
      await onCancel();
      onClose();
    } catch (err) {
      logger.error('[ProgressModal] Cancel failed:', err);
    } finally {
      setIsCanceling(false);
      setShowCancelConfirm(false);
    }
  }, [onCancel, onClose]);

  // ============================================================================
  // Render
  // ============================================================================

  const isTerminalState =
    currentStep === ProcessingStep.Completed || currentStep === ProcessingStep.Failed;
  const hasError = currentStep === ProcessingStep.Failed || error || metricsError;

  return (
    <>
      <Dialog open={isOpen} onOpenChange={open => !open && onClose()}>
        <DialogContent
          className="max-w-4xl"
          hideCloseButton={!isTerminalState}
          aria-describedby="progress-description"
        >
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 font-quicksand text-2xl">
              {hasError ? (
                <>
                  <AlertCircle className="h-6 w-6 text-destructive" />
                  Processing Failed
                </>
              ) : isTerminalState ? (
                <>
                  <Check className="h-6 w-6 text-green-500" />
                  Processing Complete
                </>
              ) : (
                <>
                  <Loader2 className="h-6 w-6 animate-spin text-amber-500" />
                  {title}
                </>
              )}
            </DialogTitle>
            <DialogDescription id="progress-description" className="font-nunito">
              {hasError
                ? 'An error occurred during processing. Please try again.'
                : isTerminalState
                  ? 'Your PDF has been processed successfully.'
                  : getStepLabel(currentStep)}
            </DialogDescription>
          </DialogHeader>

          {/* Progress Bar */}
          <div className="space-y-2" aria-live="polite" aria-atomic="true">
            <div className="flex justify-between text-sm font-nunito">
              <span className="text-muted-foreground">
                {isPolling && '⚠️ '}
                {isConnected ? 'Live updates' : 'Checking status...'}
              </span>
              <span className="font-medium text-amber-600 dark:text-amber-400">
                {progressValue}%
              </span>
            </div>
            <Progress
              value={progressValue}
              className="h-2 [&>div]:bg-amber-500 dark:[&>div]:bg-amber-400"
              aria-label={`Processing progress: ${progressValue}%`}
            />
          </div>

          {/* Step Indicators */}
          <div className="flex justify-between gap-2 py-4">
            {PROCESSING_STEPS.slice(0, 6).map(step => {
              const stepOrder = getStepOrder(step);
              const isCurrent = stepOrder === currentStepOrder;
              const isComplete = stepOrder < currentStepOrder;
              const isFailed =
                currentStep === ProcessingStep.Failed && stepOrder === currentStepOrder;

              return (
                <div key={step} className="flex flex-col items-center gap-2">
                  <div
                    className={cn(
                      'flex h-8 w-8 items-center justify-center rounded-full border-2 transition-all duration-300',
                      isComplete && 'border-green-500 bg-green-500 text-white',
                      isCurrent &&
                        !isFailed &&
                        'border-amber-500 bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300 animate-pulse',
                      isFailed && 'border-destructive bg-destructive/10 text-destructive',
                      !isComplete && !isCurrent && 'border-muted bg-muted/30 text-muted-foreground'
                    )}
                    aria-label={`Step ${stepOrder + 1}: ${getStepLabel(step)}`}
                    aria-current={isCurrent ? 'step' : undefined}
                  >
                    {isComplete ? (
                      <Check className="h-4 w-4" />
                    ) : isFailed ? (
                      <X className="h-4 w-4" />
                    ) : isCurrent ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <span className="text-xs font-medium">{stepOrder + 1}</span>
                    )}
                  </div>
                  <p className="text-xs text-center max-w-[80px] font-nunito text-muted-foreground">
                    {step.replace(/([A-Z])/g, ' $1').trim()}
                  </p>
                </div>
              );
            })}
          </div>

          {/* Metrics Display */}
          {!hasError && (
            <div className="grid grid-cols-3 gap-4 rounded-lg bg-white/70 dark:bg-white/5 backdrop-blur-md p-4 border border-border/50">
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground font-nunito">Pages Processed</p>
                <p className="text-lg font-semibold font-quicksand text-amber-600 dark:text-amber-400">
                  {metrics?.pageCount ?? 'N/A'}
                </p>
              </div>
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground font-nunito">Duration</p>
                <p className="text-lg font-semibold font-quicksand">
                  {metrics?.totalDuration
                    ? formatTimeSpan(metrics.totalDuration)
                    : 'Calculating...'}
                </p>
              </div>
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground font-nunito">ETA</p>
                <p className="text-lg font-semibold font-quicksand text-amber-600 dark:text-amber-400">
                  {isTerminalState
                    ? 'Complete'
                    : metrics?.estimatedTimeRemaining
                      ? formatTimeSpan(metrics.estimatedTimeRemaining)
                      : 'Calculating...'}
                </p>
              </div>
            </div>
          )}

          {/* Error Message */}
          {hasError && (
            <div className="rounded-lg bg-destructive/10 p-4 border border-destructive/30">
              <p className="text-sm text-destructive font-nunito">
                {status?.errorMessage ||
                  error?.message ||
                  metricsError?.message ||
                  'An unknown error occurred'}
              </p>
            </div>
          )}

          {/* Actions */}
          <div className="flex justify-end gap-2 pt-4">
            {!isTerminalState && !hideCancelButton && onCancel && (
              <Button
                variant="outline"
                onClick={() => setShowCancelConfirm(true)}
                disabled={isCanceling}
              >
                {isCanceling ? 'Canceling...' : 'Cancel Processing'}
              </Button>
            )}
            {isTerminalState && <Button onClick={onClose}>Close</Button>}
          </div>
        </DialogContent>
      </Dialog>

      {/* Cancel Confirmation Dialog */}
      <Dialog open={showCancelConfirm} onOpenChange={setShowCancelConfirm}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel Processing?</DialogTitle>
            <DialogDescription>
              This will stop the PDF processing. You can restart it later, but progress will be
              lost.
            </DialogDescription>
          </DialogHeader>
          <div className="flex justify-end gap-2 pt-4">
            <Button
              variant="outline"
              onClick={() => setShowCancelConfirm(false)}
              disabled={isCanceling}
            >
              Continue Processing
            </Button>
            <Button variant="destructive" onClick={handleCancel} disabled={isCanceling}>
              {isCanceling ? 'Canceling...' : 'Yes, Cancel'}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
