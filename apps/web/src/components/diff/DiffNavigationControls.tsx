import React from 'react';

import { ChevronUp, ChevronDown } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

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
 * Migrated to shadcn UI components
 */
export function DiffNavigationControls({
  currentIndex,
  totalChanges,
  onPrev,
  onNext,
  disabled = false,
}: DiffNavigationControlsProps) {
  if (totalChanges === 0) return null;

  const isPrevDisabled = disabled || currentIndex === 0;
  const isNextDisabled = disabled || currentIndex >= totalChanges - 1;

  return (
    <div
      className="diff-navigation-controls flex items-center gap-2"
      role="navigation"
      aria-label="Diff navigation"
    >
      <span className="diff-navigation-position text-sm text-muted-foreground whitespace-nowrap">
        {currentIndex + 1} / {totalChanges} changes
      </span>
      <Button
        variant="outline"
        size="sm"
        onClick={onPrev}
        disabled={isPrevDisabled}
        aria-label="Previous change"
        title="Previous change (Alt+Up)"
      >
        <ChevronUp className="h-4 w-4 mr-1" />
        Prev
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={onNext}
        disabled={isNextDisabled}
        aria-label="Next change"
        title="Next change (Alt+Down)"
      >
        Next
        <ChevronDown className="h-4 w-4 ml-1" />
      </Button>
    </div>
  );
}
