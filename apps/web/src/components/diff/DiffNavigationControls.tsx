import React from 'react';

export interface DiffNavigationControlsProps {
  currentIndex: number;
  totalChanges: number;
  onPrev: () => void;
  onNext: () => void;
  disabled?: boolean;
}

/**
 * Navigation controls for jumping between changes
 * Prev/Next buttons with keyboard support
 */
export function DiffNavigationControls({
  currentIndex,
  totalChanges,
  onPrev,
  onNext,
  disabled = false
}: DiffNavigationControlsProps) {
  if (totalChanges === 0) return null;

  const isPrevDisabled = disabled || currentIndex === 0;
  const isNextDisabled = disabled || currentIndex >= totalChanges - 1;

  return (
    <div className="diff-navigation-controls" role="navigation" aria-label="Diff navigation">
      <span className="diff-navigation-position">
        {currentIndex + 1} / {totalChanges} changes
      </span>
      <button
        onClick={onPrev}
        disabled={isPrevDisabled}
        className="diff-nav-button diff-nav-button--prev"
        aria-label="Previous change"
        title="Previous change (Alt+Up)"
      >
        ↑ Prev
      </button>
      <button
        onClick={onNext}
        disabled={isNextDisabled}
        className="diff-nav-button diff-nav-button--next"
        aria-label="Next change"
        title="Next change (Alt+Down)"
      >
        Next ↓
      </button>
    </div>
  );
}
