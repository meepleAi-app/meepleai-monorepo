/**
 * ConfidenceBar - Visual confidence indicator (Issue #3244)
 *
 * Displays confidence score as color-coded progress bar with percentage.
 * Animates fill on mount (0% → target%).
 */

import React, { useEffect, useState } from 'react';

import { cn } from '@/lib/utils';

interface ConfidenceBarProps {
  /** Confidence score (0.0 - 1.0) */
  confidence: number;

  /** Custom class name */
  className?: string;
}

/**
 * Get confidence color based on score thresholds
 */
function getConfidenceColor(confidence: number): {
  bg: string;
  text: string;
  label: string;
} {
  if (confidence > 0.8) {
    return {
      bg: 'bg-cyan-500',
      text: 'text-cyan-400',
      label: 'Alta',
    };
  }
  if (confidence >= 0.6) {
    return {
      bg: 'bg-yellow-500',
      text: 'text-yellow-400',
      label: 'Media',
    };
  }
  return {
    bg: 'bg-red-500',
    text: 'text-red-400',
    label: 'Bassa',
  };
}

export function ConfidenceBar({ confidence, className }: ConfidenceBarProps): React.JSX.Element {
  const [width, setWidth] = useState(0);
  const percentage = Math.round(confidence * 100);
  const colors = getConfidenceColor(confidence);

  // Animate fill on mount
  useEffect(() => {
    const timer = setTimeout(() => {
      setWidth(percentage);
    }, 50); // Small delay for smooth animation trigger

    return () => clearTimeout(timer);
  }, [percentage]);

  return (
    <div className={cn('space-y-1', className)}>
      {/* Label + Percentage */}
      <div className="flex items-center justify-between text-xs">
        <span className="text-gray-400">Confidenza:</span>
        <span className={cn('font-mono font-medium', colors.text)}>
          {percentage}% ({colors.label})
        </span>
      </div>

      {/* Progress Bar */}
      <div className="h-2 bg-gray-700 rounded-full overflow-hidden">
        <div
          className={cn(
            'h-full rounded-full transition-all duration-700 ease-out',
            colors.bg
          )}
          style={{ width: `${width}%` }}
          role="progressbar"
          aria-valuenow={percentage}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={`Confidence: ${percentage}%`}
        />
      </div>
    </div>
  );
}
