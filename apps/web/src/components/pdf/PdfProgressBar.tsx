/**
 * PdfProgressBar Component (Issue #4217)
 *
 * Linear progress bar (0-100%) with current state label display.
 * Displays PDF processing progress with semantic colors.
 *
 * Features:
 * - Linear progress indicator 0-100%
 * - Current state label with optional display
 * - State-specific colors (processing: blue, ready: green, failed: red)
 * - Glassmorphic design
 * - WCAG 2.1 AA compliant
 */

'use client';

import { Progress } from '@/components/ui/feedback/progress';
import { cn } from '@/lib/utils';
import type { PdfState } from '@/types/pdf';
import { getPdfStateLabel } from '@/types/pdf';

// ============================================================================
// Types
// ============================================================================

export interface PdfProgressBarProps {
  /** Current PDF processing state */
  state: PdfState;
  /** Progress percentage (0-100) */
  progress?: number;
  /** Show state label above progress bar */
  showLabel?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Get progress bar color based on state
 */
function getProgressColor(state: PdfState): string {
  if (state === 'ready') {
    return '[&>div]:bg-green-500';
  }
  if (state === 'failed') {
    return '[&>div]:bg-red-500';
  }
  if (state === 'pending') {
    return '[&>div]:bg-gray-400';
  }
  // Processing states: uploading, extracting, chunking, embedding, indexing
  return '[&>div]:bg-blue-500';
}

/**
 * Calculate progress value (defaults based on state if not provided)
 */
function getProgressValue(state: PdfState, progress?: number): number {
  if (progress !== undefined) {
    return Math.max(0, Math.min(100, progress));
  }

  // Default progress based on state
  const stateProgress: Record<PdfState, number> = {
    pending: 0,
    uploading: 15,
    extracting: 30,
    chunking: 50,
    embedding: 70,
    indexing: 85,
    ready: 100,
    failed: 0,
  };

  return stateProgress[state];
}

// ============================================================================
// Component
// ============================================================================

export function PdfProgressBar({
  state,
  progress,
  showLabel = true,
  className,
}: PdfProgressBarProps) {
  const progressValue = getProgressValue(state, progress);
  const label = getPdfStateLabel(state);
  const colorClass = getProgressColor(state);

  return (
    <div
      className={cn('w-full', className)}
      data-testid="pdf-progress-bar"
      role="progressbar"
      aria-valuenow={progressValue}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={`PDF Processing: ${label}`}
    >
      {/* Label and Progress Percentage */}
      {showLabel && (
        <div className="mb-2 flex items-center justify-between text-sm">
          <span className="font-medium text-muted-foreground" data-testid="progress-label">
            {label}
          </span>
          <span
            className="text-muted-foreground"
            data-testid="progress-percentage"
            aria-live="polite"
          >
            {progressValue}%
          </span>
        </div>
      )}

      {/* Progress Bar */}
      <Progress
        value={progressValue}
        className={cn(
          'h-2',
          colorClass,
          // Glassmorphic effect
          'bg-white/30 backdrop-blur-sm border border-white/20'
        )}
        aria-hidden="true"
      />
    </div>
  );
}

export default PdfProgressBar;
