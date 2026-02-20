'use client';

/**
 * TimeTravelOverlay - Visual treatment for viewing past session state
 * Issue #4758 - Snapshot History Slider UI + Time Travel Mode
 *
 * Renders an amber-tinted overlay on MeepleCard when viewing a past snapshot.
 * Shows snapshot info badge, turn indicator, and "Return to present" button.
 */

import React, { useCallback, useEffect } from 'react';

import {
  ArrowRight,
  Clock,
  History,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import type { SnapshotInfo } from '../extra-meeple-card/types';

// ============================================================================
// Types
// ============================================================================

export interface TimeTravelOverlayProps {
  /** The snapshot being viewed */
  snapshot: SnapshotInfo;
  /** Total number of snapshots */
  totalSnapshots: number;
  /** Whether the overlay is visible */
  isActive: boolean;
  /** Callback to exit time travel mode */
  onExit?: () => void;
  /** Loading state while reconstructing */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function formatTimeAgo(dateString: string): string {
  const now = new Date();
  const then = new Date(dateString);
  const diffMs = now.getTime() - then.getTime();
  const diffMin = Math.floor(diffMs / 60000);

  if (diffMin < 1) return 'just now';
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHours = Math.floor(diffMin / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays}d ago`;
}

// ============================================================================
// Main Component
// ============================================================================

export function TimeTravelOverlay({
  snapshot,
  totalSnapshots,
  isActive,
  onExit,
  isLoading,
  className,
  'data-testid': testId = 'time-travel-overlay',
}: TimeTravelOverlayProps) {
  // ESC key closes overlay
  useEffect(() => {
    if (!isActive) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onExit?.();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isActive, onExit]);

  const handleExit = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      onExit?.();
    },
    [onExit],
  );

  if (!isActive) return null;

  const timeAgo = formatTimeAgo(snapshot.createdAt);
  const turnLabel = snapshot.turnNumber
    ? `Turn ${snapshot.turnNumber}`
    : `Snapshot #${snapshot.snapshotNumber}`;

  return (
    <div
      className={cn(
        'absolute inset-0 z-30 rounded-2xl overflow-hidden',
        'pointer-events-none',
        className,
      )}
      data-testid={testId}
    >
      {/* Amber/sepia tint overlay */}
      <div className="absolute inset-0 bg-amber-900/10 mix-blend-multiply pointer-events-none" />

      {/* Amber border glow */}
      <div className="absolute inset-0 rounded-2xl ring-2 ring-amber-400/60 ring-inset pointer-events-none" />

      {/* Top badge: Time travel indicator */}
      <div
        className={cn(
          'absolute top-3 left-3 right-3 pointer-events-auto',
          'flex items-center justify-between',
        )}
      >
        <div
          className={cn(
            'flex items-center gap-1.5 rounded-full px-3 py-1.5',
            'bg-amber-100/95 backdrop-blur-sm',
            'text-amber-800 text-xs font-semibold font-nunito',
            'shadow-sm',
          )}
          data-testid="time-travel-badge"
        >
          <Clock className="h-3 w-3" aria-hidden="true" />
          <span>{turnLabel}</span>
          <span className="text-amber-500 mx-0.5">&middot;</span>
          <span className="font-normal text-amber-600">{timeAgo}</span>
        </div>

        {/* Snapshot position indicator */}
        <div
          className={cn(
            'flex items-center gap-1 rounded-full px-2 py-1',
            'bg-white/80 backdrop-blur-sm',
            'text-slate-500 text-[10px] font-mono font-semibold',
          )}
        >
          <History className="h-2.5 w-2.5" aria-hidden="true" />
          {snapshot.snapshotNumber}/{totalSnapshots}
        </div>
      </div>

      {/* Bottom bar: Description + Return button */}
      <div
        className={cn(
          'absolute bottom-3 left-3 right-3 pointer-events-auto',
          'flex items-center justify-between gap-2',
        )}
      >
        {/* Description */}
        <div
          className={cn(
            'flex-1 min-w-0 rounded-lg px-3 py-2',
            'bg-amber-50/90 backdrop-blur-sm',
            'border border-amber-200/50',
          )}
        >
          <p className="text-[10px] uppercase tracking-wider text-amber-500 font-nunito">
            Viewing historical state
          </p>
          <p className="text-xs font-medium text-amber-800 font-nunito truncate">
            {snapshot.description}
          </p>
        </div>

        {/* Return to present button */}
        <button
          type="button"
          onClick={handleExit}
          className={cn(
            'flex items-center gap-1 rounded-lg px-3 py-2',
            'bg-indigo-500 text-white text-xs font-semibold font-nunito',
            'hover:bg-indigo-600 transition-colors',
            'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-1',
            'shadow-sm whitespace-nowrap',
          )}
          aria-label="Return to present state"
          data-testid="time-travel-exit"
        >
          {isLoading ? (
            <span className="animate-pulse">Loading...</span>
          ) : (
            <>
              Present
              <ArrowRight className="h-3 w-3" aria-hidden="true" />
            </>
          )}
        </button>
      </div>

      {/* Screen reader live region (separate from pointer-events-none root) */}
      <div aria-live="polite" aria-atomic="true" className="sr-only">
        Viewing {turnLabel} — {timeAgo}
      </div>
    </div>
  );
}
