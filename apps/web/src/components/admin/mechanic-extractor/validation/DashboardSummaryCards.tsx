/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 38)
 *
 * Three summary Cards showing counts of shared games per `CertificationStatus`
 * (Certified / NotCertified / NotEvaluated). Counts are derived in a single
 * pass over the dashboard rows array.
 */

'use client';

import { useMemo } from 'react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import type {
  CertificationStatus,
  ValidationDashboardRowDto,
} from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

// ──────────────────────────────────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────────────────────────────────

interface StatusCounts {
  Certified: number;
  NotCertified: number;
  NotEvaluated: number;
}

function computeCounts(rows: ReadonlyArray<ValidationDashboardRowDto>): StatusCounts {
  const counts: StatusCounts = { Certified: 0, NotCertified: 0, NotEvaluated: 0 };
  for (const row of rows) {
    counts[row.status] += 1;
  }
  return counts;
}

const TILE_CLASS: Record<CertificationStatus, string> = {
  Certified: 'border-green-300 bg-green-50/60 dark:border-green-800/60 dark:bg-green-950/20',
  NotCertified: 'border-rose-300 bg-rose-50/60 dark:border-rose-800/60 dark:bg-rose-950/20',
  NotEvaluated: 'border-slate-300 bg-slate-50/60 dark:border-zinc-700 dark:bg-zinc-900/30',
};

const VALUE_CLASS: Record<CertificationStatus, string> = {
  Certified: 'text-green-700 dark:text-green-300',
  NotCertified: 'text-rose-700 dark:text-rose-300',
  NotEvaluated: 'text-slate-700 dark:text-zinc-300',
};

const LABEL: Record<CertificationStatus, string> = {
  Certified: 'Certified',
  NotCertified: 'Not certified',
  NotEvaluated: 'Not evaluated',
};

const TEST_ID: Record<CertificationStatus, string> = {
  Certified: 'dashboard-summary-certified',
  NotCertified: 'dashboard-summary-not-certified',
  NotEvaluated: 'dashboard-summary-not-evaluated',
};

// ──────────────────────────────────────────────────────────────────────────
// Component
// ──────────────────────────────────────────────────────────────────────────

export interface DashboardSummaryCardsProps {
  rows: ReadonlyArray<ValidationDashboardRowDto>;
}

export function DashboardSummaryCards({ rows }: DashboardSummaryCardsProps) {
  const counts = useMemo(() => computeCounts(rows), [rows]);
  const statuses: CertificationStatus[] = ['Certified', 'NotCertified', 'NotEvaluated'];

  return (
    <div data-testid="dashboard-summary-cards" className="grid grid-cols-1 gap-4 sm:grid-cols-3">
      {statuses.map(status => (
        <Card
          key={status}
          data-testid={TEST_ID[status]}
          className={`${TILE_CLASS[status]} backdrop-blur-md`}
        >
          <CardHeader className="pb-2">
            <CardTitle className="font-quicksand text-sm text-muted-foreground">
              {LABEL[status]}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div
              data-testid={`${TEST_ID[status]}-count`}
              className={`font-quicksand text-3xl font-bold ${VALUE_CLASS[status]}`}
            >
              {counts[status]}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
