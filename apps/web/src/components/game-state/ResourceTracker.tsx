/**
 * ResourceTracker Component
 * Issue #2406: Game State Editor UI
 *
 * Visual resource counter with increment/decrement buttons.
 */

'use client';

import { Minus, Plus } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

interface ResourceTrackerProps {
  value: number;
  onChange: (delta: number) => void;
  editable?: boolean;
  min?: number;
  max?: number;
  step?: number;
  testId?: string;
  className?: string;
}

export function ResourceTracker({
  value,
  onChange,
  editable = false,
  min = 0,
  max = Infinity,
  step = 1,
  testId,
  className = '',
}: ResourceTrackerProps) {
  const canDecrement = editable && value - step >= min;
  const canIncrement = editable && value + step <= max;

  return (
    <div className={`flex items-center gap-1.5 sm:gap-2 ${className}`} data-testid={testId}>
      {editable && (
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8 sm:h-9 sm:w-9"
          onClick={() => onChange(-step)}
          disabled={!canDecrement}
          aria-label="Decrease value"
          data-testid={`${testId}-decrease`}
        >
          <Minus className="h-4 w-4" />
        </Button>
      )}

      <div
        className="min-w-[2.5rem] sm:min-w-[3rem] text-center text-sm sm:text-base font-semibold tabular-nums"
        aria-label={`Current value: ${value}`}
        data-testid={`${testId}-value`}
      >
        {value}
      </div>

      {editable && (
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8 sm:h-9 sm:w-9"
          onClick={() => onChange(step)}
          disabled={!canIncrement}
          aria-label="Increase value"
          data-testid={`${testId}-increase`}
        >
          <Plus className="h-4 w-4" />
        </Button>
      )}
    </div>
  );
}
