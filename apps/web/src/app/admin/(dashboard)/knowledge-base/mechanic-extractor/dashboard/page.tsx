/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 38)
 *
 * Admin validation dashboard: three summary tiles (Certified / NotCertified /
 * NotEvaluated) plus a per-game table sorted by overall score descending.
 *
 * Hidden behind feature flag `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'`.
 */

'use client';

import { notFound } from 'next/navigation';

import { DashboardSummaryCards } from '@/components/admin/mechanic-extractor/validation/DashboardSummaryCards';
import { DashboardTable } from '@/components/admin/mechanic-extractor/validation/DashboardTable';
import { ThresholdsConfigForm } from '@/components/admin/mechanic-extractor/validation/ThresholdsConfigForm';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useThresholds } from '@/hooks/admin/useThresholds';
import { useValidationDashboard } from '@/hooks/admin/useValidationDashboard';

const FEATURE_FLAG = 'true';

export default function MechanicValidationDashboardPage() {
  if (process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED !== FEATURE_FLAG) {
    notFound();
  }

  const { data, isLoading, error } = useValidationDashboard();
  const {
    data: thresholds,
    isLoading: isThresholdsLoading,
    error: thresholdsError,
  } = useThresholds();

  return (
    <div className="mx-auto max-w-[1100px] space-y-6">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          AI Comprehension Validation — Dashboard
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Certification status across all shared games. Scores are computed from the latest metrics
          snapshot per game.
        </p>
      </div>

      {isLoading && (
        <div
          className="space-y-4"
          data-testid="dashboard-loading"
          role="status"
          aria-label="Loading dashboard"
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
          <Skeleton className="h-64 w-full" />
        </div>
      )}

      {error && !isLoading && (
        <div
          data-testid="dashboard-error"
          className="rounded-md border border-rose-300 bg-rose-50/70 p-4 text-sm text-rose-800 dark:border-rose-800/60 dark:bg-rose-950/20 dark:text-rose-300"
        >
          Failed to load dashboard: {error instanceof Error ? error.message : 'unknown error'}
        </div>
      )}

      {!isLoading && !error && data && (
        <div className="space-y-6" data-testid="dashboard-content">
          <DashboardSummaryCards rows={data} />
          <DashboardTable rows={data} />
        </div>
      )}

      <Card data-testid="thresholds-card">
        <CardHeader>
          <CardTitle className="font-quicksand text-lg">Certification Thresholds</CardTitle>
        </CardHeader>
        <CardContent>
          {isThresholdsLoading && (
            <div data-testid="thresholds-loading" role="status" aria-label="Loading thresholds">
              <Skeleton className="h-48 w-full" />
            </div>
          )}

          {thresholdsError && !isThresholdsLoading && (
            <div
              data-testid="thresholds-error"
              className="rounded-md border border-rose-300 bg-rose-50/70 p-4 text-sm text-rose-800 dark:border-rose-800/60 dark:bg-rose-950/20 dark:text-rose-300"
            >
              Failed to load thresholds:{' '}
              {thresholdsError instanceof Error ? thresholdsError.message : 'unknown error'}
            </div>
          )}

          {!isThresholdsLoading && !thresholdsError && thresholds && (
            <ThresholdsConfigForm initial={thresholds} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
