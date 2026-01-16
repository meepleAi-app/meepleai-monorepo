/* eslint-disable security/detect-object-injection */
/**
 * AILoadingIndicator Component
 * Issue #2418 (Sub-Issue 2401.2): Visual loading states for AI operations
 *
 * Features:
 * - Skeleton loader matching QuickQuestion layout
 * - Animated progress bar with smooth transitions
 * - Estimated time remaining with dynamic updates
 * - Cancellation button to interrupt API calls
 * - Timeout handling with configurable duration
 * - Accessible: respects prefers-reduced-motion
 *
 * @example
 * ```tsx
 * <AILoadingIndicator
 *   isLoading={isGenerating}
 *   progress={progress}
 *   stage="Generating answer..."
 *   estimatedTotalTime={5000}
 *   onCancel={handleCancel}
 *   timeout={30000}
 *   onTimeout={handleTimeout}
 * />
 * ```
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { Loader2, X } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { useReducedMotion } from '@/lib/animations';
import { cn } from '@/lib/utils';

// ==================== Types ====================

/**
 * Loading stage configuration for multi-step progress
 */
export interface LoadingStage {
  /** Progress percentage (0-100) at this stage */
  progress: number;
  /** Display message for this stage */
  message: string;
  /** Duration in ms to reach this stage from previous */
  duration?: number;
}

/**
 * Skeleton variant matching different AI component layouts
 */
export type SkeletonVariant = 'question' | 'answer' | 'card' | 'compact';

/**
 * Props for AILoadingIndicator component
 */
export interface AILoadingIndicatorProps {
  /**
   * Whether the loading indicator is visible/active
   */
  isLoading: boolean;

  /**
   * Manual progress value (0-100)
   * If provided, disables auto-progress based on estimatedTotalTime
   */
  progress?: number;

  /**
   * Estimated total time for the operation in milliseconds
   * Used for auto-progress simulation when progress prop is not provided
   * @default 5000
   */
  estimatedTotalTime?: number;

  /**
   * Current stage message to display
   */
  stage?: string;

  /**
   * Predefined loading stages for multi-step operations
   * If provided, auto-cycles through stages based on timing
   */
  stages?: LoadingStage[];

  /**
   * Callback when cancel button is clicked
   * If not provided, cancel button is hidden
   */
  onCancel?: () => void;

  /**
   * Whether to show the cancel button
   * @default true (if onCancel is provided)
   */
  showCancelButton?: boolean;

  /**
   * Label for the cancel button
   * @default 'Cancel'
   */
  cancelLabel?: string;

  /**
   * Whether to show the skeleton loader
   * @default true
   */
  showSkeleton?: boolean;

  /**
   * Skeleton layout variant
   * @default 'question'
   */
  skeletonVariant?: SkeletonVariant;

  /**
   * Timeout duration in milliseconds
   * If operation takes longer, onTimeout is called
   * @default 30000 (30 seconds)
   */
  timeout?: number;

  /**
   * Callback when timeout is reached
   */
  onTimeout?: () => void;

  /**
   * Whether to show estimated time remaining
   * @default true
   */
  showTimeRemaining?: boolean;

  /**
   * Additional CSS classes
   */
  className?: string;

  /**
   * Accessible label for the loading indicator
   * @default 'AI generation in progress'
   */
  ariaLabel?: string;
}

// ==================== Constants ====================

const DEFAULT_STAGES: LoadingStage[] = [
  { progress: 10, message: 'Initializing...', duration: 500 },
  { progress: 30, message: 'Analyzing input...', duration: 1000 },
  { progress: 50, message: 'Processing with AI...', duration: 2000 },
  { progress: 75, message: 'Generating response...', duration: 1500 },
  { progress: 90, message: 'Finalizing...', duration: 500 },
];

const SKELETON_STYLES: Record<SkeletonVariant, string> = {
  question: 'h-32',
  answer: 'h-48',
  card: 'h-40',
  compact: 'h-20',
};

// ==================== Helper Functions ====================

/**
 * Format milliseconds to human-readable time remaining
 */
function formatTimeRemaining(ms: number): string {
  if (ms <= 0) return 'Almost done...';
  if (ms < 1000) return 'Less than a second';

  const seconds = Math.ceil(ms / 1000);
  if (seconds < 60) {
    return `~${seconds} second${seconds !== 1 ? 's' : ''} remaining`;
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  if (remainingSeconds === 0) {
    return `~${minutes} minute${minutes !== 1 ? 's' : ''} remaining`;
  }
  return `~${minutes}m ${remainingSeconds}s remaining`;
}

// ==================== Sub-Components ====================

interface ProgressDisplayProps {
  progress: number;
  stage: string;
  timeRemaining: string | null;
  showTimeRemaining: boolean;
  shouldReduceMotion: boolean;
}

function ProgressDisplay({
  progress,
  stage,
  timeRemaining,
  showTimeRemaining,
  shouldReduceMotion,
}: ProgressDisplayProps) {
  return (
    <div className="space-y-3">
      {/* Stage message */}
      <div className="flex items-center gap-2">
        <Loader2
          className={cn('h-4 w-4 text-primary', !shouldReduceMotion && 'animate-spin')}
          aria-hidden="true"
        />
        <span className="text-sm font-medium text-foreground">{stage}</span>
      </div>

      {/* Progress bar */}
      <div className="space-y-1">
        <Progress
          value={progress}
          className={cn('h-2', !shouldReduceMotion && 'transition-all duration-300')}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={progress}
        />

        {/* Progress percentage and time remaining */}
        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <span aria-live="polite">{Math.round(progress)}%</span>
          {showTimeRemaining && timeRemaining && (
            <span aria-live="polite" className="text-right">
              {timeRemaining}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

interface SkeletonDisplayProps {
  variant: SkeletonVariant;
  shouldReduceMotion: boolean;
}

function SkeletonDisplay({ variant, shouldReduceMotion }: SkeletonDisplayProps) {
  const baseClasses = cn(
    'rounded-lg bg-muted/50',
    !shouldReduceMotion && 'animate-pulse',
    SKELETON_STYLES[variant]
  );

  return (
    <div className={baseClasses} aria-hidden="true">
      <div className="h-full flex flex-col p-4 space-y-3">
        {variant === 'question' && (
          <>
            {/* Question text skeleton */}
            <div className="h-4 bg-muted rounded w-full" />
            <div className="h-4 bg-muted rounded w-4/5" />
            {/* Metadata skeleton */}
            <div className="mt-auto flex items-center gap-2">
              <div className="h-3 bg-muted rounded w-16" />
              <div className="h-3 bg-muted rounded w-24" />
            </div>
          </>
        )}

        {variant === 'answer' && (
          <>
            {/* Answer header skeleton */}
            <div className="h-5 bg-muted rounded w-1/3" />
            {/* Answer content skeleton */}
            <div className="space-y-2 flex-1">
              <div className="h-4 bg-muted rounded w-full" />
              <div className="h-4 bg-muted rounded w-full" />
              <div className="h-4 bg-muted rounded w-3/4" />
              <div className="h-4 bg-muted rounded w-5/6" />
            </div>
            {/* Footer skeleton */}
            <div className="h-3 bg-muted rounded w-1/4" />
          </>
        )}

        {variant === 'card' && (
          <>
            {/* Card header skeleton */}
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-muted rounded-full flex-shrink-0" />
              <div className="flex-1 space-y-2">
                <div className="h-4 bg-muted rounded w-1/2" />
                <div className="h-3 bg-muted rounded w-1/3" />
              </div>
            </div>
            {/* Card content skeleton */}
            <div className="flex-1 space-y-2">
              <div className="h-4 bg-muted rounded w-full" />
              <div className="h-4 bg-muted rounded w-4/5" />
            </div>
          </>
        )}

        {variant === 'compact' && (
          <div className="flex items-center gap-3 h-full">
            <div className="w-8 h-8 bg-muted rounded flex-shrink-0" />
            <div className="flex-1 space-y-1">
              <div className="h-4 bg-muted rounded w-3/4" />
              <div className="h-3 bg-muted rounded w-1/2" />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ==================== Main Component ====================

export function AILoadingIndicator({
  isLoading,
  progress: manualProgress,
  estimatedTotalTime = 5000,
  stage: manualStage,
  stages = DEFAULT_STAGES,
  onCancel,
  showCancelButton = true,
  cancelLabel = 'Cancel',
  showSkeleton = true,
  skeletonVariant = 'question',
  timeout = 30000,
  onTimeout,
  showTimeRemaining = true,
  className,
  ariaLabel = 'AI generation in progress',
}: AILoadingIndicatorProps) {
  const shouldReduceMotion = useReducedMotion();

  // Internal state for auto-progress
  const [autoProgress, setAutoProgress] = useState(0);
  const [currentStageIndex, setCurrentStageIndex] = useState(0);
  const [elapsedTime, setElapsedTime] = useState(0);

  // Refs for cleanup
  const progressIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const startTimeRef = useRef<number>(0);

  // Determine actual progress and stage
  const progress = manualProgress ?? autoProgress;
  const currentStage = stages[currentStageIndex] ?? stages[stages.length - 1];
  const stage = manualStage ?? currentStage?.message ?? 'Processing...';

  // Calculate time remaining
  const timeRemaining =
    showTimeRemaining && isLoading && manualProgress === undefined
      ? formatTimeRemaining(estimatedTotalTime - elapsedTime)
      : null;

  // Cleanup function
  const cleanup = useCallback(() => {
    if (progressIntervalRef.current) {
      clearInterval(progressIntervalRef.current);
      progressIntervalRef.current = null;
    }
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);

  // Update stage index based on auto-progress (derived state pattern)
  useEffect(() => {
    if (manualProgress !== undefined) return; // Skip if using manual progress

    const newStageIndex = stages.findIndex(s => s.progress > autoProgress);
    const stageIdx = newStageIndex === -1 ? stages.length - 1 : Math.max(0, newStageIndex - 1);
    setCurrentStageIndex(stageIdx);
  }, [autoProgress, stages, manualProgress]);

  // Reset state when loading starts/stops
  useEffect(() => {
    if (isLoading) {
      // Reset state
      setAutoProgress(0);
      setCurrentStageIndex(0);
      setElapsedTime(0);
      startTimeRef.current = Date.now();

      // Only run auto-progress if manual progress is not provided
      if (manualProgress === undefined) {
        // Calculate progress increment based on estimated time
        const updateInterval = 100; // ms
        const totalUpdates = estimatedTotalTime / updateInterval;

        // Guard against division by zero - skip auto-progress if estimatedTotalTime is 0 or negative
        if (totalUpdates <= 0) {
          return cleanup;
        }

        const progressIncrement = 95 / totalUpdates; // Stop at 95% until complete

        progressIntervalRef.current = setInterval(() => {
          const elapsed = Date.now() - startTimeRef.current;
          setElapsedTime(elapsed);

          setAutoProgress(prev => Math.min(prev + progressIncrement, 95));
        }, updateInterval);
      }

      // Setup timeout
      if (onTimeout) {
        timeoutRef.current = setTimeout(() => {
          cleanup();
          onTimeout();
        }, timeout);
      }
    } else {
      // Set to 100% briefly when loading completes (use functional update to avoid stale closure)
      setAutoProgress(prev => (prev > 0 ? 100 : prev));
      cleanup();
    }

    return cleanup;
  }, [isLoading, manualProgress, estimatedTotalTime, stages, timeout, onTimeout, cleanup]);

  // Don't render if not loading
  if (!isLoading && progress === 0) {
    return null;
  }

  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy={isLoading}
      aria-label={ariaLabel}
      className={cn('space-y-4', className)}
    >
      {/* Screen reader text */}
      <span className="sr-only">
        {stage} {Math.round(progress)}% complete.
        {timeRemaining && ` ${timeRemaining}.`}
      </span>

      {/* Progress display */}
      <ProgressDisplay
        progress={progress}
        stage={stage}
        timeRemaining={timeRemaining}
        showTimeRemaining={showTimeRemaining}
        shouldReduceMotion={shouldReduceMotion}
      />

      {/* Skeleton loader */}
      {showSkeleton && (
        <SkeletonDisplay variant={skeletonVariant} shouldReduceMotion={shouldReduceMotion} />
      )}

      {/* Cancel button */}
      {showCancelButton && onCancel && (
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={onCancel}
          className="w-full sm:w-auto"
          aria-label={`${cancelLabel} AI generation`}
        >
          <X className="h-4 w-4 mr-2" aria-hidden="true" />
          {cancelLabel}
        </Button>
      )}
    </div>
  );
}
