/**
 * PdfProcessingProgressBar Component (Issue #3369)
 *
 * Displays real-time PDF processing progress with step visualization,
 * progress bar, time estimates, and cancel functionality.
 *
 * Features:
 * - 6 step visualization with icons (Uploading → Extracting → Chunking → Embedding → Indexing → Completed)
 * - Progress bar with % completion
 * - Elapsed and estimated time remaining
 * - Error state with retry option
 * - Cancel button
 * - Accessibility: aria-live for screen readers
 */

'use client';

import { useCallback, useState } from 'react';

import {
  Upload,
  FileText,
  Scissors,
  Brain,
  Database,
  CheckCircle2,
  AlertCircle,
  XCircle,
  Loader2,
  RefreshCw,
} from 'lucide-react';

import { ConfirmDialog } from '@/components/ui/feedback/confirm-dialog';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { usePdfProcessingProgress } from '@/hooks/usePdfProcessingProgress';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface PdfProcessingProgressBarProps {
  /** PDF ID to track processing progress */
  pdfId: string;
  /** Callback when processing completes successfully */
  onComplete?: () => void;
  /** Callback when processing fails with error message */
  onError?: (error: string) => void;
  /** Callback when user cancels processing */
  onCancel?: () => void;
  /** Callback when user requests processing retry (parent handles actual retry API call) */
  onRetry?: (pdfId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

type ProcessingStepValue =
  | 'Uploading'
  | 'Extracting'
  | 'Chunking'
  | 'Embedding'
  | 'Indexing'
  | 'Completed'
  | 'Failed';

interface StepConfig {
  icon: typeof Upload;
  label: string;
  description: string;
}

// ============================================================================
// Constants
// ============================================================================

const STEP_CONFIG: Record<ProcessingStepValue, StepConfig> = {
  Uploading: {
    icon: Upload,
    label: 'Caricamento',
    description: 'Caricamento del file PDF...',
  },
  Extracting: {
    icon: FileText,
    label: 'Estrazione',
    description: 'Estrazione del testo dal documento...',
  },
  Chunking: {
    icon: Scissors,
    label: 'Suddivisione',
    description: 'Suddivisione del testo in chunks...',
  },
  Embedding: {
    icon: Brain,
    label: 'Embedding',
    description: 'Generazione degli embeddings vettoriali...',
  },
  Indexing: {
    icon: Database,
    label: 'Indicizzazione',
    description: 'Indicizzazione nel database vettoriale...',
  },
  Completed: {
    icon: CheckCircle2,
    label: 'Completato',
    description: 'Elaborazione completata con successo!',
  },
  Failed: {
    icon: XCircle,
    label: 'Errore',
    description: "Si è verificato un errore durante l'elaborazione.",
  },
};

const PROCESSING_STEPS: ProcessingStepValue[] = [
  'Uploading',
  'Extracting',
  'Chunking',
  'Embedding',
  'Indexing',
  'Completed',
];

// ============================================================================
// Helper Functions
// ============================================================================

function getStepIndex(step: ProcessingStepValue): number {
  if (step === 'Failed') return -1;
  return PROCESSING_STEPS.indexOf(step);
}

function formatTimeSpan(timeSpan: string | null): string {
  if (!timeSpan) return '--:--';

  // Parse .NET TimeSpan format "HH:mm:ss.fffffff"
  // eslint-disable-next-line security/detect-unsafe-regex -- Safe: bounded quantifiers
  const match = timeSpan.match(/^(\d+):(\d+):(\d+)(?:\.(\d+))?$/);
  if (!match) return '--:--';

  const hours = parseInt(match[1], 10);
  const minutes = parseInt(match[2], 10);
  const seconds = parseInt(match[3], 10);

  if (hours > 0) {
    return `${hours}h ${minutes}m ${seconds}s`;
  }
  if (minutes > 0) {
    return `${minutes}m ${seconds}s`;
  }
  return `${seconds}s`;
}

// ============================================================================
// Step Indicator Component
// ============================================================================

interface StepIndicatorProps {
  step: ProcessingStepValue;
  currentStep: ProcessingStepValue;
  index: number;
}

function StepIndicator({ step, currentStep, index }: StepIndicatorProps) {
  const config = STEP_CONFIG[step];
  const Icon = config.icon;

  const currentIndex = getStepIndex(currentStep);
  const stepIndex = getStepIndex(step);
  const isFailed = currentStep === 'Failed';

  const isCompleted = !isFailed && stepIndex < currentIndex;
  const isActive = !isFailed && stepIndex === currentIndex;
  const isPending = !isFailed && stepIndex > currentIndex;

  return (
    <div
      className={cn(
        'flex flex-col items-center gap-2 transition-all duration-300',
        isCompleted && 'opacity-100',
        isActive && 'scale-105',
        isPending && 'opacity-50'
      )}
      data-testid={`step-indicator-${step.toLowerCase()}`}
    >
      {/* Step Circle */}
      <div
        className={cn(
          'relative flex h-10 w-10 items-center justify-center rounded-full border-2 transition-all duration-300',
          isCompleted && 'border-green-500 bg-green-500/10 text-green-600 dark:text-green-400',
          isActive && 'border-primary bg-primary/10 text-primary animate-pulse',
          isPending && 'border-muted-foreground/30 text-muted-foreground/50',
          isFailed && 'border-destructive bg-destructive/10 text-destructive'
        )}
      >
        {isActive && !isFailed ? (
          <Loader2 className="h-5 w-5 animate-spin" />
        ) : (
          <Icon className="h-5 w-5" />
        )}

        {/* Connector Line (except for last step, hidden on mobile) */}
        {index < PROCESSING_STEPS.length - 1 && (
          <div
            className={cn(
              'absolute left-full top-1/2 hidden h-0.5 w-8 -translate-y-1/2 transition-colors duration-300 sm:block',
              isCompleted ? 'bg-green-500' : 'bg-muted-foreground/30'
            )}
          />
        )}
      </div>

      {/* Step Label */}
      <span
        className={cn(
          'text-xs font-medium transition-colors duration-300',
          isCompleted && 'text-green-600 dark:text-green-400',
          isActive && 'text-primary',
          isPending && 'text-muted-foreground/50',
          isFailed && 'text-destructive'
        )}
      >
        {config.label}
      </span>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function PdfProcessingProgressBar({
  pdfId,
  onComplete,
  onError,
  onCancel,
  onRetry,
  className,
}: PdfProcessingProgressBarProps) {
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [isCanceling, setIsCanceling] = useState(false);

  const { progress, isLoading, error, refetch } = usePdfProcessingProgress(pdfId, {
    pollingInterval: 2000,
    onComplete,
    onError,
  });

  const currentStep: ProcessingStepValue = progress?.currentStep ?? 'Uploading';
  const percentComplete = progress?.percentComplete ?? 0;
  const isFailed = currentStep === 'Failed';
  const isCompleted = currentStep === 'Completed';
  const isProcessing = !isFailed && !isCompleted && !error;

  const handleCancelClick = useCallback(() => {
    setShowCancelDialog(true);
  }, []);

  const handleConfirmCancel = useCallback(async () => {
    setShowCancelDialog(false);
    setIsCanceling(true);

    try {
      await api.pdf.cancelProcessing(pdfId);
      onCancel?.();
    } catch {
      // Error already handled by hook or callback
    } finally {
      setIsCanceling(false);
    }
  }, [pdfId, onCancel]);

  const handleRetry = useCallback(() => {
    if (onRetry) {
      onRetry(pdfId);
    }
    refetch();
  }, [onRetry, pdfId, refetch]);

  // Loading state
  if (isLoading && !progress) {
    return (
      <div
        className={cn('rounded-lg border bg-card p-6', className)}
        data-testid="pdf-progress-loading"
      >
        <div className="flex items-center justify-center gap-3">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          <span className="text-sm text-muted-foreground">Caricamento stato elaborazione...</span>
        </div>
      </div>
    );
  }

  // Network error state
  if (error && !progress) {
    return (
      <div
        className={cn('rounded-lg border border-destructive/50 bg-destructive/5 p-6', className)}
        role="alert"
        data-testid="pdf-progress-error"
      >
        <div className="flex flex-col items-center gap-4">
          <AlertCircle className="h-10 w-10 text-destructive" />
          <div className="text-center">
            <p className="font-medium text-destructive">Errore di connessione</p>
            <p className="mt-1 text-sm text-muted-foreground">{error.message}</p>
          </div>
          <Button variant="outline" size="sm" onClick={handleRetry}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Riprova
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div
      className={cn('rounded-lg border bg-card p-6', className)}
      data-testid="pdf-processing-progress-bar"
      role="region"
      aria-label="Progresso elaborazione PDF"
    >
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <h3 className="text-lg font-semibold">Elaborazione PDF</h3>
        {isProcessing && (
          <Button
            variant="ghost"
            size="sm"
            onClick={handleCancelClick}
            disabled={isCanceling}
            data-testid="cancel-button"
          >
            {isCanceling ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <XCircle className="mr-2 h-4 w-4" />
            )}
            Annulla
          </Button>
        )}
      </div>

      {/* Step Indicators */}
      <div
        className="mb-6 flex items-center justify-between gap-1 overflow-x-auto pb-2 sm:gap-0"
        aria-label="Passaggi elaborazione"
      >
        {PROCESSING_STEPS.map((step, index) => (
          <StepIndicator key={step} step={step} currentStep={currentStep} index={index} />
        ))}
      </div>

      {/* Progress Bar */}
      <div className="mb-4">
        <div className="mb-2 flex items-center justify-between text-sm">
          <span className="font-medium">{STEP_CONFIG[currentStep].description}</span>
          <span className="text-muted-foreground">{percentComplete}%</span>
        </div>
        <Progress
          value={percentComplete}
          className={cn(
            'h-2',
            isFailed && '[&>div]:bg-destructive',
            isCompleted && '[&>div]:bg-green-500'
          )}
          aria-label={`Elaborazione ${percentComplete}% completata`}
        />
      </div>

      {/* Time Info */}
      {progress && !isFailed && !isCompleted && (
        <div
          className="flex items-center justify-between text-sm text-muted-foreground"
          aria-live="polite"
        >
          <span>Tempo trascorso: {formatTimeSpan(progress.elapsedTime)}</span>
          <span>Tempo stimato: {formatTimeSpan(progress.estimatedTimeRemaining)}</span>
        </div>
      )}

      {/* Error State */}
      {isFailed && (
        <div
          className="mt-4 rounded-md border border-destructive/50 bg-destructive/5 p-4"
          role="alert"
          aria-live="assertive"
        >
          <div className="flex items-start gap-3">
            <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-destructive" />
            <div className="flex-1">
              <p className="font-medium text-destructive">Elaborazione fallita</p>
              {progress?.errorMessage && (
                <p className="mt-1 text-sm text-muted-foreground">{progress.errorMessage}</p>
              )}
            </div>
            <Button variant="outline" size="sm" onClick={handleRetry}>
              <RefreshCw className="mr-2 h-4 w-4" />
              Riprova
            </Button>
          </div>
        </div>
      )}

      {/* Success State */}
      {isCompleted && (
        <div
          className="mt-4 rounded-md border border-green-500/50 bg-green-500/5 p-4"
          role="status"
          aria-live="polite"
        >
          <div className="flex items-center gap-3">
            <CheckCircle2 className="h-5 w-5 text-green-600 dark:text-green-400" />
            <p className="font-medium text-green-600 dark:text-green-400">
              Elaborazione completata! Il PDF è pronto per le domande.
            </p>
          </div>
        </div>
      )}

      {/* Cancel Confirmation Dialog */}
      <ConfirmDialog
        open={showCancelDialog}
        onOpenChange={setShowCancelDialog}
        title="Vuoi cancellare l'elaborazione?"
        message="Sei sicuro di voler cancellare l'elaborazione del PDF? Questa azione non può essere annullata e dovrai ricaricare il file."
        variant="destructive"
        confirmText="Sì, cancella"
        cancelText="No, continua"
        onConfirm={handleConfirmCancel}
        onCancel={() => setShowCancelDialog(false)}
      />
    </div>
  );
}

export default PdfProcessingProgressBar;
