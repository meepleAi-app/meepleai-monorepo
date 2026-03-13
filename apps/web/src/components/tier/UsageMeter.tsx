/**
 * UsageMeter - Single Usage Progress Bar (Game Night Improvvisata - E2-4)
 *
 * Compact progress bar for displaying a single usage metric with color coding.
 * Green by default, amber at 80%+, red at 100%.
 */

'use client';

import React from 'react';

import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';

// ============================================================================
// Constants
// ============================================================================

const UNLIMITED_THRESHOLD = 999_999;

// ============================================================================
// Types
// ============================================================================

export interface UsageMeterProps {
  /** Label for this metric (e.g., "Giochi privati") */
  label: string;
  /** Current usage count */
  current: number;
  /** Maximum allowed count */
  max: number;
  /** Optional extra CSS class */
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function getPercentage(current: number, max: number): number {
  if (max <= 0 || max > UNLIMITED_THRESHOLD) return 0;
  return Math.min((current / max) * 100, 100);
}

function getIndicatorColor(percent: number): string {
  if (percent >= 100) return '[&>div]:bg-red-500';
  if (percent >= 80) return '[&>div]:bg-amber-500';
  return '[&>div]:bg-emerald-500';
}

function formatMax(max: number): string {
  if (max > UNLIMITED_THRESHOLD) return 'Illimitato';
  return String(max);
}

// ============================================================================
// Component
// ============================================================================

export const UsageMeter = React.memo(function UsageMeter({
  label,
  current,
  max,
  className,
}: UsageMeterProps) {
  const isUnlimited = max > UNLIMITED_THRESHOLD;
  const percent = getPercentage(current, max);
  const indicatorColor = getIndicatorColor(percent);

  return (
    <div className={cn('space-y-1', className)} data-testid="usage-meter">
      <div className="flex items-center justify-between text-xs">
        <span className="text-muted-foreground font-nunito">{label}</span>
        <span className="font-semibold text-foreground font-nunito">
          {current}/{formatMax(max)}
        </span>
      </div>
      <Progress
        value={isUnlimited ? 0 : percent}
        className={cn('h-1.5', indicatorColor)}
        aria-label={`${label}: ${current} di ${isUnlimited ? 'illimitato' : max}`}
      />
    </div>
  );
});

UsageMeter.displayName = 'UsageMeter';
