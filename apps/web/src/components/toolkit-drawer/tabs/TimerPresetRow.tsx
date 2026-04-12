'use client';

/**
 * TimerPresetRow — Row of quick-select preset durations.
 */

import React from 'react';

import { cn } from '@/lib/utils';

// ============================================================================
// Presets
// ============================================================================

const PRESETS = [
  { label: '30s', seconds: 30 },
  { label: '1m', seconds: 60 },
  { label: '2m', seconds: 120 },
  { label: '3m', seconds: 180 },
  { label: '5m', seconds: 300 },
  { label: '10m', seconds: 600 },
] as const;

// ============================================================================
// Component
// ============================================================================

interface TimerPresetRowProps {
  /** Currently selected duration in seconds (matches totalSeconds in store). */
  selected: number;
  onSelect: (seconds: number) => void;
}

export function TimerPresetRow({ selected, onSelect }: TimerPresetRowProps) {
  return (
    <div className="flex flex-wrap justify-center gap-2" data-testid="timer-preset-row">
      {PRESETS.map(p => {
        const isActive = selected === p.seconds;
        return (
          <button
            key={p.seconds}
            type="button"
            onClick={() => onSelect(p.seconds)}
            className={cn(
              'min-w-[42px] rounded-lg px-3 py-1.5 text-xs font-bold transition-colors',
              isActive
                ? 'bg-[hsl(142,70%,45%)] text-white shadow-sm'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
            )}
            aria-pressed={isActive}
            data-testid={`timer-preset-${p.seconds}`}
          >
            {p.label}
          </button>
        );
      })}
    </div>
  );
}
