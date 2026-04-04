/**
 * ProgressBadge Component (Issue #4210)
 *
 * Minimal icon-only status indicator for PDF progress in list views.
 *
 * Features:
 * - Badge size 24x24px
 * - Color-coded: blue (processing), green (ready), red (failed), amber (uploading)
 * - Tooltip on hover with details (state, progress%, ETA)
 * - Pulse animation for processing states
 * - Optional real-time updates via usePdfProgress
 *
 * @example
 * ```tsx
 * // With real-time updates
 * <ProgressBadge documentId={docId} />
 *
 * // Static state (no hook)
 * <ProgressBadge state="ready" progress={100} />
 * ```
 */

'use client';

import * as React from 'react';

import { Loader2, Check, X, Upload, FileText } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { usePdfProgress } from '@/hooks/usePdfProgress';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ProgressBadgeProps {
  /** Document ID for real-time updates (optional if using static state/progress) */
  documentId?: string | null;
  /** Static state (used if documentId not provided) */
  state?: string;
  /** Static progress (used if documentId not provided) */
  progress?: number;
  /** Static ETA (used if documentId not provided) */
  eta?: string | null;
  /** Optional CSS class */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function getStateColor(state: string): string {
  const colors: Record<string, string> = {
    pending: 'text-muted-foreground bg-muted',
    uploading: 'text-amber-600 bg-amber-100 dark:text-amber-400 dark:bg-amber-900/30',
    extracting: 'text-blue-600 bg-blue-100 dark:text-blue-400 dark:bg-blue-900/30',
    chunking: 'text-blue-600 bg-blue-100 dark:text-blue-400 dark:bg-blue-900/30',
    embedding: 'text-blue-600 bg-blue-100 dark:text-blue-400 dark:bg-blue-900/30',
    indexing: 'text-blue-600 bg-blue-100 dark:text-blue-400 dark:bg-blue-900/30',
    ready: 'text-green-600 bg-green-100 dark:text-green-400 dark:bg-green-900/30',
    indexed: 'text-green-600 bg-green-100 dark:text-green-400 dark:bg-green-900/30',
    failed: 'text-destructive bg-destructive/10',
  };
  return colors[state.toLowerCase()] || colors['pending'];
}

function getStateIcon(state: string): React.ReactNode {
  const iconClass = 'h-3.5 w-3.5';

  switch (state) {
    case 'pending':
      return <FileText className={iconClass} />;
    case 'uploading':
      return <Upload className={iconClass} />;
    case 'ready':
    case 'indexed':
      return <Check className={iconClass} />;
    case 'failed':
      return <X className={iconClass} />;
    default:
      // Processing states
      return <Loader2 className={cn(iconClass, 'animate-spin')} />;
  }
}

function getStateLabel(state: string): string {
  const labels: Record<string, string> = {
    pending: 'Pending',
    uploading: 'Uploading',
    extracting: 'Extracting',
    chunking: 'Chunking',
    embedding: 'Embedding',
    indexing: 'Indexing',
    ready: 'Ready',
    indexed: 'Ready',
    failed: 'Failed',
  };
  return labels[state] || state;
}

function isProcessingState(state: string): boolean {
  return ['uploading', 'extracting', 'chunking', 'embedding', 'indexing'].includes(state);
}

// ============================================================================
// Component
// ============================================================================

export function ProgressBadge({
  documentId,
  state: staticState,
  progress: staticProgress,
  eta: staticEta,
  className,
}: ProgressBadgeProps): React.ReactElement {
  // ============================================================================
  // Progress Hook (optional)
  // ============================================================================

  const hookData = usePdfProgress(documentId || null);

  // ============================================================================
  // Computed Values (prefer hook data, fallback to static)
  // ============================================================================

  const state = hookData.status?.state ?? staticState ?? 'pending';
  const progress =
    hookData.metrics?.progressPercentage ?? hookData.status?.progress ?? staticProgress ?? 0;
  const eta = hookData.metrics?.estimatedTimeRemaining ?? hookData.status?.eta ?? staticEta;

  // Format ETA
  const formatEta = (etaValue: string | null | undefined): string => {
    if (!etaValue) return 'N/A';

    const parts = etaValue.split(':');
    if (parts.length < 2) return etaValue;

    const hours = parseInt(parts[0], 10);
    const minutes = parseInt(parts[1], 10);
    const seconds = Math.floor(parseFloat(parts[2] || '0'));

    if (hours > 0) return `${hours}h ${minutes}m`;
    if (minutes > 0) return `${minutes}m ${seconds}s`;
    return `${seconds}s`;
  };

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            className={cn(
              'inline-flex items-center justify-center',
              'h-6 w-6 rounded-full',
              'transition-all duration-300',
              getStateColor(state),
              isProcessingState(state) && 'animate-pulse',
              className
            )}
            aria-label={`PDF status: ${getStateLabel(state)}`}
            role="status"
          >
            {getStateIcon(state)}
          </div>
        </TooltipTrigger>
        <TooltipContent>
          <div className="space-y-1 text-xs font-nunito">
            <p className="font-semibold font-quicksand">{getStateLabel(state)}</p>
            {state !== 'ready' && state !== 'failed' && (
              <>
                <p className="text-muted-foreground">
                  Progress: <span className="text-foreground">{progress}%</span>
                </p>
                {eta && (
                  <p className="text-muted-foreground">
                    ETA:{' '}
                    <span className="text-amber-600 dark:text-amber-400">{formatEta(eta)}</span>
                  </p>
                )}
              </>
            )}
            {state === 'ready' && <p className="text-green-600 dark:text-green-400">Complete</p>}
            {state === 'failed' && <p className="text-destructive">Processing failed</p>}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
