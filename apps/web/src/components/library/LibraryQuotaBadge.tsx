'use client';

/**
 * LibraryQuotaBadge — Compact inline pill showing collection quota usage
 *
 * Displays "X / Y" with color coding based on usage percentage:
 * - Normal (<70%): neutral muted style
 * - Warning (>=70%): amber warning style
 * - Critical (>=100%): destructive style with upgrade link
 */

import { Sparkles } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

export interface LibraryQuotaBadgeProps {
  currentCount: number;
  maxAllowed: number;
  percentageUsed: number;
  className?: string;
}

export function LibraryQuotaBadge({
  currentCount,
  maxAllowed,
  percentageUsed,
  className,
}: LibraryQuotaBadgeProps) {
  const isWarning = percentageUsed >= 70 && percentageUsed < 100;
  const isCritical = percentageUsed >= 100;

  return (
    <div
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium',
        isCritical && 'border-destructive/50 bg-destructive/5 text-destructive',
        isWarning &&
          'border-amber-500/40 bg-amber-50/60 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400',
        !isWarning && !isCritical && 'border-border bg-muted/40 text-muted-foreground',
        className
      )}
    >
      <span className="tabular-nums">
        {currentCount} / {maxAllowed}
      </span>
      {(isWarning || isCritical) && (
        <Link
          href="/settings/subscription"
          className="inline-flex items-center gap-0.5 hover:underline"
          aria-label="Upgrade plan"
        >
          <Sparkles className="h-3 w-3" aria-hidden="true" />
        </Link>
      )}
    </div>
  );
}
