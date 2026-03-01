'use client';

/**
 * SnapshotHistorySlider - Dot-based timeline for session snapshots
 * Issue #4758 - Snapshot History Slider UI + Time Travel Mode
 *
 * Displays a horizontal strip of dots representing session snapshots.
 * Placed on MeepleCard above the navigation footer.
 */

import React, { useCallback, useRef } from 'react';

import {
  Camera,
  ChevronLeft,
  ChevronRight,
  Clock,
  Zap,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import type { SnapshotInfo } from '../extra-meeple-card/types';

// ============================================================================
// Types
// ============================================================================

export interface SnapshotHistorySliderProps {
  /** List of snapshots */
  snapshots: SnapshotInfo[];
  /** Currently selected snapshot index (0-based) */
  currentIndex?: number;
  /** Callback when a snapshot dot is clicked */
  onSelect?: (index: number) => void;
  /** Whether time travel mode is active */
  isTimeTravelMode?: boolean;
  /** Toggle time travel mode */
  onTimeTravelToggle?: (enabled: boolean) => void;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

// ============================================================================
// Constants
// ============================================================================

/** Max dots before collapsing to range indicator */
const MAX_VISIBLE_DOTS = 20;

/** Trigger type icon mapping */
const TRIGGER_ICONS: Record<string, React.ElementType> = {
  manual: Camera,
  turnEnd: ChevronRight,
  automatic: Zap,
};

/** Trigger type color mapping */
const TRIGGER_COLORS: Record<string, string> = {
  manual: 'bg-indigo-500',
  turnEnd: 'bg-amber-500',
  automatic: 'bg-green-500',
};

// ============================================================================
// Sub-components
// ============================================================================

function SnapshotDot({
  snapshot,
  index,
  isSelected,
  isCurrent,
  onClick,
}: {
  snapshot: SnapshotInfo;
  index: number;
  isSelected: boolean;
  isCurrent: boolean;
  onClick: (index: number) => void;
}) {
  const TriggerIcon = TRIGGER_ICONS[snapshot.triggerType] ?? Camera;
  const dotColor = TRIGGER_COLORS[snapshot.triggerType] ?? 'bg-slate-400';

  return (
    <button
      type="button"
      onClick={(e) => {
        e.stopPropagation();
        onClick(index);
      }}
      className={cn(
        'relative flex items-center justify-center rounded-full transition-all duration-200',
        'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-1',
        isSelected
          ? 'h-7 w-7 ring-2 ring-indigo-400 scale-110'
          : 'h-5 w-5 hover:scale-110',
        isCurrent && !isSelected && 'ring-1 ring-slate-300',
      )}
      aria-label={`Snapshot #${snapshot.snapshotNumber}: ${snapshot.description}`}
      aria-current={isSelected ? 'step' : undefined}
      data-testid={`snapshot-dot-${index}`}
    >
      <span
        className={cn(
          'absolute inset-0 rounded-full opacity-20',
          dotColor,
        )}
      />
      <TriggerIcon
        className={cn(
          'relative z-10',
          isSelected ? 'h-3.5 w-3.5 text-indigo-700' : 'h-2.5 w-2.5 text-slate-500',
        )}
        aria-hidden="true"
      />
    </button>
  );
}

function CollapsedRange({
  total,
  currentIndex,
}: {
  total: number;
  currentIndex: number;
}) {
  return (
    <div
      className="flex items-center gap-1 px-2 py-1 rounded-full bg-slate-100 text-xs font-nunito text-slate-500"
      data-testid="snapshot-collapsed-range"
    >
      <span className="font-mono font-semibold text-indigo-600">
        {currentIndex + 1}
      </span>
      <span>/</span>
      <span className="font-mono">{total}</span>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function SnapshotHistorySlider({
  snapshots,
  currentIndex = 0,
  onSelect,
  isTimeTravelMode,
  onTimeTravelToggle,
  className,
  'data-testid': testId = 'snapshot-history-slider',
}: SnapshotHistorySliderProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const safeIndex = Math.max(0, Math.min(currentIndex, snapshots.length - 1));

  const handleSelect = useCallback(
    (index: number) => {
      onSelect?.(index);
    },
    [onSelect],
  );

  const handlePrev = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      if (safeIndex > 0) {
        onSelect?.(safeIndex - 1);
      }
    },
    [safeIndex, onSelect],
  );

  const handleNext = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      if (safeIndex < snapshots.length - 1) {
        onSelect?.(safeIndex + 1);
      }
    },
    [safeIndex, snapshots.length, onSelect],
  );

  const handleTimeTravelToggle = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      onTimeTravelToggle?.(!isTimeTravelMode);
    },
    [isTimeTravelMode, onTimeTravelToggle],
  );

  if (snapshots.length === 0) return null;

  const isCollapsed = snapshots.length > MAX_VISIBLE_DOTS;

  return (
    <div
      className={cn(
        'flex items-center gap-1.5 px-3 py-2',
        'border-t border-slate-100',
        className,
      )}
      data-testid={testId}
      role="navigation"
      aria-label="Snapshot history"
    >
      {/* Prev button */}
      <button
        type="button"
        onClick={handlePrev}
        disabled={safeIndex === 0}
        className={cn(
          'flex h-6 w-6 items-center justify-center rounded-full transition-colors',
          'hover:bg-slate-100 disabled:opacity-30 disabled:cursor-not-allowed',
          'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400',
        )}
        aria-label="Previous snapshot"
        data-testid="snapshot-prev"
      >
        <ChevronLeft className="h-3.5 w-3.5 text-slate-500" aria-hidden="true" />
      </button>

      {/* Dots or collapsed range */}
      {isCollapsed ? (
        <CollapsedRange total={snapshots.length} currentIndex={safeIndex} />
      ) : (
        <div
          ref={scrollRef}
          className="flex items-center gap-1 overflow-x-auto scrollbar-none flex-1"
          aria-label="Snapshot dots"
        >
          {snapshots.map((snapshot, index) => (
            <SnapshotDot
              key={snapshot.id}
              snapshot={snapshot}
              index={index}
              isSelected={index === safeIndex}
              isCurrent={index === snapshots.length - 1}
              onClick={handleSelect}
            />
          ))}
        </div>
      )}

      {/* Next button */}
      <button
        type="button"
        onClick={handleNext}
        disabled={safeIndex === snapshots.length - 1}
        className={cn(
          'flex h-6 w-6 items-center justify-center rounded-full transition-colors',
          'hover:bg-slate-100 disabled:opacity-30 disabled:cursor-not-allowed',
          'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400',
        )}
        aria-label="Next snapshot"
        data-testid="snapshot-next"
      >
        <ChevronRight className="h-3.5 w-3.5 text-slate-500" aria-hidden="true" />
      </button>

      {/* Time travel toggle */}
      {onTimeTravelToggle && (
        <button
          type="button"
          onClick={handleTimeTravelToggle}
          className={cn(
            'ml-1 flex h-6 w-6 items-center justify-center rounded-full transition-colors',
            'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400',
            isTimeTravelMode
              ? 'bg-amber-100 text-amber-700'
              : 'hover:bg-slate-100 text-slate-400',
          )}
          aria-label={isTimeTravelMode ? 'Exit time travel' : 'Enter time travel'}
          aria-pressed={!!isTimeTravelMode}
          data-testid="snapshot-time-travel-toggle"
        >
          <Clock className="h-3.5 w-3.5" aria-hidden="true" />
        </button>
      )}
    </div>
  );
}
