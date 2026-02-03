/**
 * QuotaStickyHeader Component (Issue #2869)
 *
 * Compact sticky header showing library quota at top of library page.
 * Remains visible when scrolling for constant quota awareness.
 *
 * Features:
 * - Sticky positioning on scroll
 * - Mini progress bar (compact version)
 * - X/Y games text
 * - Percentage display
 * - Link to settings/upgrade
 */

import { ChevronRight, Sparkles } from 'lucide-react';
import Link from 'next/link';

import { Progress } from '@/components/ui/feedback/progress';
import { cn } from '@/lib/utils';

export interface QuotaStickyHeaderProps {
  currentCount: number;
  maxAllowed: number;
  percentageUsed: number;
  userTier: 'free' | 'normal' | 'premium';
  className?: string;
}

/**
 * Determines progress bar color based on usage percentage
 * - Green (primary): < 70%
 * - Yellow (amber): 70-90%
 * - Red (destructive): > 90%
 */
function getProgressColor(percentage: number): string {
  if (percentage > 90) {
    return '[&>div]:bg-destructive';
  }
  if (percentage >= 70) {
    return '[&>div]:bg-amber-500';
  }
  return ''; // Uses default primary color
}

/**
 * Determines text color based on usage percentage
 */
function getTextColor(percentage: number): string {
  if (percentage > 90) {
    return 'text-destructive';
  }
  if (percentage >= 70) {
    return 'text-amber-600 dark:text-amber-400';
  }
  return 'text-muted-foreground';
}

/**
 * QuotaStickyHeader Component
 *
 * Compact sticky header displaying library quota with mini progress bar.
 * Shows X/Y games, percentage, and upgrade link when applicable.
 *
 * @example
 * ```tsx
 * <QuotaStickyHeader
 *   currentCount={15}
 *   maxAllowed={20}
 *   percentageUsed={75}
 *   userTier="normal"
 * />
 * ```
 */
export function QuotaStickyHeader({
  currentCount,
  maxAllowed,
  percentageUsed,
  userTier,
  className,
}: QuotaStickyHeaderProps) {
  const canUpgrade = userTier !== 'premium';
  const progressColor = getProgressColor(percentageUsed);
  const textColor = getTextColor(percentageUsed);
  const isNearLimit = percentageUsed >= 70;

  return (
    <div
      className={cn(
        // Sticky positioning below main header (UnifiedHeader uses z-50 at top-0)
        'sticky top-16 z-40',
        // Background with blur
        'bg-background/95 backdrop-blur-[16px] backdrop-saturate-[180%]',
        'dark:bg-card dark:backdrop-blur-none',
        // Border and shadow
        'border-b border-border/50 dark:border-border/30 shadow-sm',
        // Padding
        'px-4 py-2',
        className
      )}
      data-testid="quota-sticky-header"
    >
      <div className="container mx-auto flex items-center gap-4">
        {/* Mini Progress Bar */}
        <div className="flex-1 max-w-xs">
          <Progress
            value={percentageUsed}
            className={cn('h-1.5 transition-all duration-300', progressColor)}
            data-testid="quota-sticky-header-progress"
          />
        </div>

        {/* Games Count: X/Y */}
        <div className="flex items-center gap-1 text-sm" data-testid="quota-sticky-header-count">
          <span className="font-semibold tabular-nums" data-testid="quota-sticky-header-current">
            {currentCount}
          </span>
          <span className="text-muted-foreground">/</span>
          <span className="tabular-nums text-muted-foreground" data-testid="quota-sticky-header-max">
            {maxAllowed >= 2147483647 ? '∞' : maxAllowed}
          </span>
          <span className="text-muted-foreground hidden sm:inline">giochi</span>
        </div>

        {/* Percentage */}
        <span
          className={cn('text-sm font-medium tabular-nums', textColor)}
          data-testid="quota-sticky-header-percentage"
        >
          {Math.round(percentageUsed)}%
        </span>

        {/* Upgrade Link */}
        {canUpgrade && (
          <Link
            href="/account/subscription"
            className={cn(
              'flex items-center gap-1 text-xs font-medium transition-colors hover:text-foreground',
              isNearLimit ? textColor : 'text-muted-foreground'
            )}
            data-testid="quota-sticky-header-upgrade-link"
          >
            {isNearLimit ? (
              <>
                <Sparkles className="h-3 w-3" />
                <span className="hidden sm:inline">Upgrade</span>
              </>
            ) : (
              <>
                <span className="hidden sm:inline">Impostazioni</span>
                <ChevronRight className="h-3 w-3" />
              </>
            )}
          </Link>
        )}
      </div>
    </div>
  );
}
