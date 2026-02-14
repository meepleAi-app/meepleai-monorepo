/**
 * ProgressCard Component (Issue #4210)
 *
 * Compact card for PDF progress in list views (Dashboard/KB).
 *
 * Features:
 * - Progress ring indicator (circular progress)
 * - Status badge (uploading, processing, ready, failed)
 * - Expandable details on click
 * - Glassmorphic design
 * - Real-time updates via usePdfProgress
 *
 * @example
 * ```tsx
 * <ProgressCard
 *   documentId={docId}
 *   title="Game Manual.pdf"
 *   onViewDetails={() => router.push(`/pdfs/${docId}`)}
 * />
 * ```
 */

'use client';

import * as React from 'react';

import { ChevronDown, ChevronUp, Clock, FileText, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/data-display/badge';
import { usePdfProgress } from '@/hooks/usePdfProgress';
import { cn } from '@/lib/utils';
import { formatTimeSpan } from '@/lib/utils/formatTimeSpan';

// ============================================================================
// Types
// ============================================================================

export interface ProgressCardProps {
  /** Document ID for tracking progress */
  documentId: string | null;
  /** PDF title */
  title: string;
  /** Optional subtitle (e.g., filename, date) */
  subtitle?: string;
  /** Initially expanded state */
  defaultExpanded?: boolean;
  /** Callback when "View Details" clicked */
  onViewDetails?: () => void;
  /** Optional CSS class */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function getStatusBadgeVariant(state: string): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (state === 'ready') return 'outline'; // green text will be added via className
  if (state === 'failed') return 'destructive';
  if (state === 'pending') return 'secondary';
  return 'default'; // processing states
}

function getStatusLabel(state: string): string {
  const labels: Record<string, string> = {
    'pending': 'Pending',
    'uploading': 'Uploading',
    'extracting': 'Extracting',
    'chunking': 'Chunking',
    'embedding': 'Embedding',
    'indexing': 'Indexing',
    'ready': 'Ready',
    'failed': 'Failed',
  };
  return labels[state] || state;
}

// ============================================================================
// Progress Ring Component
// ============================================================================

interface ProgressRingProps {
  progress: number;
  size?: number;
  strokeWidth?: number;
  color?: string;
}

function ProgressRing({
  progress,
  size = 64,
  strokeWidth = 4,
  color = 'text-amber-500',
}: ProgressRingProps): React.ReactElement {
  const radius = (size - strokeWidth) / 2;
  const circumference = radius * 2 * Math.PI;
  const offset = circumference - (progress / 100) * circumference;

  return (
    <div className="relative" style={{ width: size, height: size }}>
      {/* Background circle */}
      <svg className="transform -rotate-90" width={size} height={size}>
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke="currentColor"
          strokeWidth={strokeWidth}
          fill="none"
          className="text-muted/30"
        />
        {/* Progress circle */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke="currentColor"
          strokeWidth={strokeWidth}
          fill="none"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          className={cn(color, 'transition-all duration-300 ease-in-out')}
          strokeLinecap="round"
        />
      </svg>
      {/* Center text */}
      <div className="absolute inset-0 flex items-center justify-center">
        <span className="text-sm font-semibold font-quicksand">{progress}%</span>
      </div>
    </div>
  );
}

// ============================================================================
// Component
// ============================================================================

export function ProgressCard({
  documentId,
  title,
  subtitle,
  defaultExpanded = false,
  onViewDetails,
  className,
}: ProgressCardProps): React.ReactElement {
  const [isExpanded, setIsExpanded] = React.useState(defaultExpanded);

  // ============================================================================
  // Progress Hook
  // ============================================================================

  const { status, metrics, isPolling } = usePdfProgress(documentId);

  // ============================================================================
  // Computed Values
  // ============================================================================

  const progressValue = metrics?.progressPercentage ?? status?.progress ?? 0;
  const currentState = status?.state ?? 'pending';
  const isProcessing = !['ready', 'failed', 'pending'].includes(currentState);

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <div
      className={cn(
        'rounded-lg border border-border/50 bg-white/70 dark:bg-white/5 backdrop-blur-md p-4 transition-all',
        'hover:shadow-md dark:hover:shadow-lg',
        className
      )}
    >
      {/* Header */}
      <div className="flex items-start gap-4">
        {/* Progress Ring */}
        <ProgressRing
          progress={progressValue}
          color={
            currentState === 'failed'
              ? 'text-destructive'
              : currentState === 'ready'
              ? 'text-green-500'
              : 'text-amber-500'
          }
        />

        {/* Content */}
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <div className="flex-1 min-w-0">
              <h3 className="font-semibold font-quicksand text-base truncate">{title}</h3>
              {subtitle && (
                <p className="text-sm text-muted-foreground font-nunito truncate">{subtitle}</p>
              )}
            </div>

            {/* Status Badge */}
            <Badge 
              variant={getStatusBadgeVariant(currentState)} 
              className={cn(
                "shrink-0",
                currentState === 'ready' && "text-green-600 dark:text-green-400 border-green-500/50"
              )}
            >
              {isProcessing && <Loader2 className="mr-1 h-3 w-3 animate-spin" />}
              {getStatusLabel(currentState)}
            </Badge>
          </div>

          {/* Polling Indicator */}
          {isPolling && (
            <p className="text-xs text-muted-foreground mt-1 font-nunito">
              ⚠️ Checking status...
            </p>
          )}
        </div>
      </div>

      {/* Expandable Details */}
      {(isExpanded || isProcessing) && (
        <div className="mt-4 pt-4 border-t border-border/30 space-y-2">
          {/* Metrics */}
          <div className="grid grid-cols-2 gap-3 text-sm">
            <div className="flex items-center gap-2">
              <FileText className="h-4 w-4 text-muted-foreground" />
              <div>
                <p className="text-xs text-muted-foreground font-nunito">Pages</p>
                <p className="font-semibold font-quicksand">{metrics?.pageCount ?? 'N/A'}</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Clock className="h-4 w-4 text-muted-foreground" />
              <div>
                <p className="text-xs text-muted-foreground font-nunito">
                  {currentState === 'ready' ? 'Duration' : 'ETA'}
                </p>
                <p className="font-semibold font-quicksand text-amber-600 dark:text-amber-400">
                  {currentState === 'ready'
                    ? (metrics?.totalDuration ? formatTimeSpan(metrics.totalDuration) : '-')
                    : (metrics?.estimatedTimeRemaining ? formatTimeSpan(metrics.estimatedTimeRemaining) : 'Calculating...')}
                </p>
              </div>
            </div>
          </div>

          {/* Error Message */}
          {currentState === 'failed' && status?.errorMessage && (
            <div className="mt-2 p-2 rounded bg-destructive/10 border border-destructive/30">
              <p className="text-xs text-destructive font-nunito">{status.errorMessage}</p>
            </div>
          )}

          {/* Actions */}
          <div className="flex items-center justify-between gap-2 pt-2">
            {onViewDetails && (
              <Button variant="ghost" size="sm" onClick={onViewDetails} className="text-xs">
                View Details
              </Button>
            )}
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setIsExpanded(!isExpanded)}
              className="ml-auto text-xs"
            >
              {isExpanded ? (
                <>
                  <ChevronUp className="h-3 w-3 mr-1" />
                  Collapse
                </>
              ) : (
                <>
                  <ChevronDown className="h-3 w-3 mr-1" />
                  Expand
                </>
              )}
            </Button>
          </div>
        </div>
      )}

      {/* Expand Toggle (when not expanded and not processing) */}
      {!isExpanded && !isProcessing && (
        <Button
          variant="ghost"
          size="sm"
          onClick={() => setIsExpanded(true)}
          className="w-full mt-3 text-xs"
        >
          <ChevronDown className="h-3 w-3 mr-1" />
          Show Details
        </Button>
      )}
    </div>
  );
}