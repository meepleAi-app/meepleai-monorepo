/**
 * LibraryQuotaWidget Component (Issue #2857)
 *
 * Dashboard widget showing library quota with animated progress bar.
 *
 * Features:
 * - Animated progress fill (1.2s ease-out)
 * - Color-coded: Green (<70%), Yellow (70-90%), Red (>90%)
 * - Displays X/Y games text
 * - CTA: 'Manage Library' or 'Upgrade Plan' if >90%
 */

'use client';

import React, { useEffect, useState } from 'react';

import { AlertCircle, BookOpen, ChevronRight, Sparkles } from 'lucide-react';
import Link from 'next/link';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryQuota } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

export interface LibraryQuotaWidgetProps {
  /**
   * Optional className for custom styling
   */
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
 * Determines status text color based on usage percentage
 */
function getStatusTextColor(percentage: number): string {
  if (percentage > 90) {
    return 'text-destructive font-medium';
  }
  if (percentage >= 70) {
    return 'text-amber-600 dark:text-amber-400 font-medium';
  }
  return 'text-muted-foreground';
}

/**
 * LibraryQuotaWidget Component
 *
 * Visual widget for library quota with animated progress bar, color-coded
 * status indicators, and contextual CTA buttons.
 *
 * @example
 * ```tsx
 * <LibraryQuotaWidget />
 * ```
 */
export function LibraryQuotaWidget({ className }: LibraryQuotaWidgetProps) {
  const { data: quota, isLoading, error } = useLibraryQuota();
  const [animatedPercentage, setAnimatedPercentage] = useState(0);

  // Animate progress bar on mount/data change
  useEffect(() => {
    if (quota?.percentageUsed !== undefined) {
      // Start from 0 for animation
      setAnimatedPercentage(0);
      // Small delay to ensure CSS transition triggers
      const timer = setTimeout(() => {
        setAnimatedPercentage(quota.percentageUsed);
      }, 50);
      return () => clearTimeout(timer);
    }
  }, [quota?.percentageUsed]);

  // Loading state
  if (isLoading) {
    return (
      <Card className={className} data-testid="library-quota-widget">
        <CardHeader>
          <div className="flex items-center gap-2">
            <BookOpen className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="library-quota-widget-title">Libreria</CardTitle>
          </div>
        </CardHeader>
        <CardContent className="space-y-4" data-testid="library-quota-widget-loading">
          <Skeleton className="h-2 w-full" data-testid="library-quota-widget-skeleton-progress" />
          <div className="flex items-center justify-between">
            <Skeleton className="h-5 w-20" data-testid="library-quota-widget-skeleton-count" />
            <Skeleton className="h-5 w-16" data-testid="library-quota-widget-skeleton-percent" />
          </div>
          <Skeleton className="h-9 w-full" data-testid="library-quota-widget-skeleton-cta" />
        </CardContent>
      </Card>
    );
  }

  // Error state
  if (error || !quota) {
    return (
      <Card className={className} data-testid="library-quota-widget">
        <CardHeader>
          <div className="flex items-center gap-2">
            <BookOpen className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="library-quota-widget-title">Libreria</CardTitle>
          </div>
        </CardHeader>
        <CardContent>
          <Alert variant="destructive" className="py-2" data-testid="library-quota-widget-error">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription className="text-xs" data-testid="library-quota-widget-error-message">
              Impossibile caricare i dati. Riprova più tardi.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  const { currentCount, maxAllowed, percentageUsed } = quota;
  const showUpgradeCta = percentageUsed > 90;
  const progressColor = getProgressColor(percentageUsed);
  const statusTextColor = getStatusTextColor(percentageUsed);

  return (
    <Card className={className} data-testid="library-quota-widget">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <BookOpen className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="library-quota-widget-title">Libreria</CardTitle>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Animated Progress Bar */}
        <Progress
          value={animatedPercentage}
          className={cn(
            'h-3 transition-all duration-[1200ms] ease-out',
            progressColor
          )}
          data-testid="library-quota-widget-progress"
        />

        {/* Games Count and Percentage */}
        <div className="flex items-center justify-between">
          <p className="text-sm font-medium" data-testid="library-quota-widget-count">
            <span className="text-lg font-bold tabular-nums" data-testid="library-quota-widget-current">
              {currentCount}
            </span>
            <span className="text-muted-foreground" data-testid="library-quota-widget-separator">
              {' / '}
            </span>
            <span className="tabular-nums text-muted-foreground" data-testid="library-quota-widget-max">
              {maxAllowed}
            </span>
            <span className="ml-1 text-muted-foreground" data-testid="library-quota-widget-label">
              giochi
            </span>
          </p>
          <p
            className={cn('text-sm tabular-nums', statusTextColor)}
            data-testid="library-quota-widget-percentage"
          >
            {Math.round(percentageUsed)}%
          </p>
        </div>

        {/* CTA Button */}
        {showUpgradeCta ? (
          <Button
            variant="default"
            className="w-full"
            asChild
            data-testid="library-quota-widget-upgrade-cta"
          >
            <Link href="/settings/subscription">
              <Sparkles className="mr-2 h-4 w-4" />
              Upgrade Piano
            </Link>
          </Button>
        ) : (
          <Button
            variant="outline"
            className="w-full"
            asChild
            data-testid="library-quota-widget-manage-cta"
          >
            <Link href="/library">
              Gestisci Libreria
              <ChevronRight className="ml-2 h-4 w-4" />
            </Link>
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
