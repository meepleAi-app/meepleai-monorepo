/**
 * Collection Progress Bar Component
 * Epic #4068 - Issue #4183
 *
 * Displays progress towards collection limits with color-coded warnings
 */

'use client';

import React from 'react';

import { AlertTriangle } from 'lucide-react';

import { cn } from '@/lib/utils';

interface CollectionProgressBarProps {
  /** Current value (e.g., games count or storage MB) */
  current: number;
  /** Maximum allowed value */
  max: number;
  /** Label for the metric */
  label: string;
  /** Unit for display (e.g., 'games', 'GB') */
  unit?: string;
  /** Show warning icon when >75% */
  showWarning?: boolean;
  /** Additional CSS classes */
  className?: string;
}

/**
 * CollectionProgressBar shows quota usage with color coding:
 * - Green: <75% (safe)
 * - Yellow: 75-90% (warning)
 * - Red: >90% (critical)
 */
export function CollectionProgressBar({
  current,
  max,
  label,
  unit = '',
  showWarning = true,
  className
}: CollectionProgressBarProps) {
  const percentage = max === Number.MAX_SAFE_INTEGER ? 0 : Math.min((current / max) * 100, 100);
  const isUnlimited = max === Number.MAX_SAFE_INTEGER || max === 2147483647; // int.MaxValue

  // Color coding based on usage
  const getColor = () => {
    if (percentage >= 90) return 'hsl(0 84% 60%)';      // Red
    if (percentage >= 75) return 'hsl(38 92% 50%)';     // Amber
    return 'hsl(142 76% 36%)';                          // Green
  };

  const getBgColor = () => {
    if (percentage >= 90) return 'hsl(0 84% 95%)';
    if (percentage >= 75) return 'hsl(38 92% 95%)';
    return 'hsl(142 76% 95%)';
  };

  const color = getColor();
  const bgColor = getBgColor();
  const showWarningIcon = showWarning && percentage >= 75;

  // Format display value
  const formatValue = (val: number) => {
    if (unit === 'GB' || unit === 'MB') {
      // Convert MB to GB if needed
      const inGB = unit === 'MB' ? val / 1024 : val;
      return inGB < 1 ? `${val} MB` : `${inGB.toFixed(1)} GB`;
    }
    return val.toLocaleString();
  };

  const displayCurrent = formatValue(current);
  const displayMax = isUnlimited ? '∞' : formatValue(max);

  return (
    <div className={cn('space-y-2', className)}>
      {/* Label Row */}
      <div className="flex items-center justify-between text-sm">
        <div className="flex items-center gap-2">
          <span className="font-medium text-foreground">{label}</span>
          {showWarningIcon && (
            <AlertTriangle
              className="w-4 h-4"
              style={{ color }}
              aria-label="Approaching limit"
            />
          )}
        </div>
        <span className="text-muted-foreground">
          {displayCurrent} / {displayMax} {unit}
        </span>
      </div>

      {/* Progress Bar */}
      {!isUnlimited && (
        <div className="relative h-2 rounded-full overflow-hidden" style={{ backgroundColor: bgColor }}>
          <div
            className="absolute inset-y-0 left-0 rounded-full transition-all duration-500 ease-out"
            style={{
              width: `${percentage}%`,
              backgroundColor: color
            }}
            role="progressbar"
            aria-valuenow={Math.round(percentage)}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-label={`${label}: ${Math.round(percentage)}% used`}
          />
        </div>
      )}

      {/* Warning Message */}
      {showWarningIcon && percentage >= 90 && (
        <p className="text-xs font-medium" style={{ color }}>
          ⚠️ Approaching limit - Consider upgrading
        </p>
      )}

      {isUnlimited && (
        <p className="text-xs text-muted-foreground">
          ✨ Unlimited (Enterprise tier)
        </p>
      )}
    </div>
  );
}
