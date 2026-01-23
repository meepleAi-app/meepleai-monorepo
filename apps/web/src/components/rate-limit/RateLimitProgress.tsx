/**
 * RateLimitProgress Component
 * Issue #2749: Frontend - Rate Limit Feedback UI
 *
 * Displays progress bar for rate limit usage with color coding:
 * - Normal: < 80% (primary color)
 * - Warning: 80-99% (amber)
 * - Limit: >= 100% (destructive)
 */

import { formatDistanceToNow } from 'date-fns';

import { Progress } from '@/components/ui/feedback/progress';
import { cn } from '@/lib/utils';

export interface RateLimitProgressProps {
  /**
   * Current usage count
   */
  current: number;

  /**
   * Maximum allowed count
   */
  max: number;

  /**
   * Label describing what this limit tracks (e.g., "Pending Requests", "Monthly Requests")
   */
  label: string;

  /**
   * Optional reset timestamp (ISO string)
   */
  resetAt?: string;

  /**
   * Optional className for custom styling
   */
  className?: string;
}

/**
 * RateLimitProgress Component
 *
 * Visual feedback for rate limit consumption with:
 * - Progress bar with color-coded states
 * - Current/max count display
 * - Optional reset time countdown
 *
 * @example
 * ```tsx
 * <RateLimitProgress
 *   current={3}
 *   max={5}
 *   label="Pending Requests"
 *   resetAt="2024-02-01T00:00:00Z"
 * />
 * ```
 */
export function RateLimitProgress({
  current,
  max,
  label,
  resetAt,
  className,
}: RateLimitProgressProps): JSX.Element {
  const percentage = max > 0 ? (current / max) * 100 : 0;
  const isWarning = percentage >= 80 && percentage < 100;
  const isLimit = percentage >= 100;

  return (
    <div className={cn('space-y-2', className)}>
      {/* Label + Count */}
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium text-foreground">{label}</span>
        <span className="tabular-nums text-muted-foreground">
          {current} / {max}
        </span>
      </div>

      {/* Progress Bar */}
      <Progress
        value={Math.min(percentage, 100)}
        className={cn(
          'h-2',
          isLimit && '[&>div]:bg-destructive',
          isWarning && !isLimit && '[&>div]:bg-amber-500'
        )}
      />

      {/* Reset Time (shown when >= 80%) */}
      {resetAt && percentage >= 80 && (
        <p className="text-xs text-muted-foreground">
          Resets {formatDistanceToNow(new Date(resetAt), { addSuffix: true })}
        </p>
      )}
    </div>
  );
}
