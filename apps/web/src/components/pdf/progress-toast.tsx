/**
 * ProgressToast Component (Issue #4210)
 *
 * Non-blocking toast notification for background PDF progress updates.
 *
 * Features:
 * - Bottom-right position, 400px max width
 * - Progress bar integrated
 * - Action buttons: "View Details", "Dismiss"
 * - Auto-dismiss after completion (5s)
 * - Smooth animations (300ms ease-in-out)
 * - Real-time updates via usePdfProgress
 *
 * @example
 * ```tsx
 * import { toast } from '@/hooks/useToast';
 * import { ProgressToast } from '@/components/pdf/progress-toast';
 *
 * // Show toast
 * toast.custom((t) => (
 *   <ProgressToast
 *     documentId={docId}
 *     title="Game Manual.pdf"
 *     onViewDetails={() => router.push(`/pdfs/${docId}`)}
 *     onDismiss={() => toast.dismiss(t.id)}
 *   />
 * ));
 * ```
 */

'use client';

import * as React from 'react';

import { X, Eye, Check, AlertCircle } from 'lucide-react';

import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { usePdfProgress } from '@/hooks/usePdfProgress';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ProgressToastProps {
  /** Document ID for tracking progress */
  documentId: string | null;
  /** PDF title */
  title: string;
  /** Callback when "View Details" clicked */
  onViewDetails?: () => void;
  /** Callback when "Dismiss" clicked */
  onDismiss?: () => void;
  /** Optional CSS class */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function ProgressToast({
  documentId,
  title,
  onViewDetails,
  onDismiss,
  className,
}: ProgressToastProps): React.ReactElement {
  const [isVisible, setIsVisible] = React.useState(true);

  // ============================================================================
  // Progress Hook
  // ============================================================================

  const { status, metrics } = usePdfProgress(documentId, {
    onComplete: () => {
      // Auto-dismiss after 5 seconds on completion
      setTimeout(() => {
        setIsVisible(false);
        onDismiss?.();
      }, 5000);
    },
  });

  // ============================================================================
  // Computed Values
  // ============================================================================

  const progressValue = metrics?.progressPercentage ?? status?.progress ?? 0;
  const currentState = status?.state ?? 'pending';
  const isComplete = currentState === 'ready';
  const isFailed = currentState === 'failed';
  const isTerminal = isComplete || isFailed;

  // ============================================================================
  // Handlers
  // ============================================================================

  const handleDismiss = React.useCallback(() => {
    setIsVisible(false);
    onDismiss?.();
  }, [onDismiss]);

  const handleViewDetails = React.useCallback(() => {
    onViewDetails?.();
    handleDismiss();
  }, [onViewDetails, handleDismiss]);

  // ============================================================================
  // Animation Exit
  // ============================================================================

  if (!isVisible) return <></>;

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <div
      className={cn(
        'w-full max-w-[400px]',
        'rounded-lg border border-border/50',
        'bg-white/95 dark:bg-card/95 backdrop-blur-md',
        'shadow-lg dark:shadow-2xl',
        'p-4',
        'animate-in slide-in-from-right-full duration-300',
        className
      )}
      role="status"
      aria-live="polite"
      aria-atomic="true"
    >
      {/* Header */}
      <div className="flex items-start justify-between gap-2 mb-3">
        <div className="flex items-center gap-2 flex-1 min-w-0">
          {isComplete && <Check className="h-5 w-5 text-green-500 shrink-0" />}
          {isFailed && <AlertCircle className="h-5 w-5 text-destructive shrink-0" />}
          <div className="flex-1 min-w-0">
            <h4 className="font-semibold font-quicksand text-sm truncate">{title}</h4>
            <p className="text-xs text-muted-foreground font-nunito">
              {isComplete
                ? 'Processing complete'
                : isFailed
                  ? 'Processing failed'
                  : `${currentState.charAt(0).toUpperCase() + currentState.slice(1)}... ${progressValue}%`}
            </p>
          </div>
        </div>

        {/* Dismiss Button */}
        <Button
          variant="ghost"
          size="sm"
          onClick={handleDismiss}
          className="h-6 w-6 p-0 shrink-0"
          aria-label="Dismiss notification"
        >
          <X className="h-4 w-4" />
        </Button>
      </div>

      {/* Progress Bar (only if not terminal) */}
      {!isTerminal && (
        <Progress
          value={progressValue}
          className="h-1.5 mb-3 [&>div]:bg-amber-500 dark:[&>div]:bg-amber-400"
          aria-label={`Processing progress: ${progressValue}%`}
        />
      )}

      {/* Error Message */}
      {isFailed && status?.errorMessage && (
        <div className="mb-3 p-2 rounded bg-destructive/10 border border-destructive/30">
          <p className="text-xs text-destructive font-nunito">{status.errorMessage}</p>
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center gap-2">
        {onViewDetails && (
          <Button variant="ghost" size="sm" onClick={handleViewDetails} className="text-xs h-7">
            <Eye className="h-3 w-3 mr-1" />
            View Details
          </Button>
        )}
        {!onViewDetails && (
          <Button variant="ghost" size="sm" onClick={handleDismiss} className="text-xs h-7 ml-auto">
            Dismiss
          </Button>
        )}
      </div>
    </div>
  );
}
