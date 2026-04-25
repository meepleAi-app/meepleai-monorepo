/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 38)
 *
 * Tabular listing of the per-game dashboard rows. Rows are sorted by
 * `overallScore` descending, with any null/NaN scores pushed to the bottom.
 *
 * Columns:
 *  - name
 *  - status badge (color matches MetricsCard's cert→color mapping)
 *  - overallScore formatted `0%` (or `—` when null/NaN)
 *  - lastComputedAt formatted short date (or `—` when null)
 *  - "View" link to the review page
 */

'use client';

import { useMemo } from 'react';

import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Button } from '@/components/ui/primitives/button';
import type {
  CertificationStatus,
  ValidationDashboardRowDto,
} from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

// ──────────────────────────────────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────────────────────────────────

const STATUS_BADGE_CLASS: Record<CertificationStatus, string> = {
  Certified: 'border-green-300 bg-green-50 text-green-800 dark:bg-green-950/30 dark:text-green-300',
  NotCertified: 'border-rose-300 bg-rose-50 text-rose-800 dark:bg-rose-950/30 dark:text-rose-300',
  NotEvaluated:
    'border-slate-300 bg-slate-50 text-slate-700 dark:bg-zinc-900/40 dark:text-zinc-300',
};

const STATUS_LABEL: Record<CertificationStatus, string> = {
  Certified: 'Certified',
  NotCertified: 'Not certified',
  NotEvaluated: 'Not evaluated',
};

const DASH = '\u2014'; // em-dash fallback for null-ish values

function isScoreValid(score: number | null | undefined): score is number {
  return typeof score === 'number' && Number.isFinite(score);
}

function formatPercent(value: number | null | undefined): string {
  if (!isScoreValid(value)) {
    return DASH;
  }
  return `${(value * 100).toFixed(0)}%`;
}

function formatShortDate(iso: string | null | undefined): string {
  if (!iso) {
    return DASH;
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return DASH;
  }
  // Culture-independent short ISO date: YYYY-MM-DD
  const yyyy = date.getUTCFullYear().toString().padStart(4, '0');
  const mm = (date.getUTCMonth() + 1).toString().padStart(2, '0');
  const dd = date.getUTCDate().toString().padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}

/**
 * Sort rows by `overallScore` desc. Invalid/non-finite scores are pushed to
 * the bottom so the "best" games always surface at the top of the table.
 */
function sortRows(rows: ReadonlyArray<ValidationDashboardRowDto>): ValidationDashboardRowDto[] {
  return [...rows].sort((a, b) => {
    const aValid = isScoreValid(a.overallScore);
    const bValid = isScoreValid(b.overallScore);
    if (aValid && !bValid) return -1;
    if (!aValid && bValid) return 1;
    if (!aValid && !bValid) return 0;
    return b.overallScore - a.overallScore;
  });
}

function reviewHref(sharedGameId: string): string {
  return `/admin/knowledge-base/mechanic-extractor/review?sharedGameId=${encodeURIComponent(
    sharedGameId
  )}`;
}

// ──────────────────────────────────────────────────────────────────────────
// Component
// ──────────────────────────────────────────────────────────────────────────

export interface DashboardTableProps {
  rows: ReadonlyArray<ValidationDashboardRowDto>;
}

export function DashboardTable({ rows }: DashboardTableProps) {
  const sorted = useMemo(() => sortRows(rows), [rows]);

  if (sorted.length === 0) {
    return (
      <div
        data-testid="dashboard-table-empty"
        className="rounded-md border border-dashed border-slate-300 bg-white/40 p-6 text-center text-sm text-muted-foreground dark:border-zinc-700 dark:bg-zinc-900/40"
      >
        No games yet. Games will appear here once metrics have been computed.
      </div>
    );
  }

  return (
    <div
      data-testid="dashboard-table"
      className="rounded-md border border-slate-200 bg-white/70 backdrop-blur-md dark:border-zinc-700 dark:bg-zinc-800/70"
    >
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Game</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Overall</TableHead>
            <TableHead>Last computed</TableHead>
            <TableHead className="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {sorted.map(row => (
            <TableRow key={row.sharedGameId} data-testid="dashboard-table-row">
              <TableCell className="font-medium">{row.name}</TableCell>
              <TableCell>
                <Badge
                  variant="outline"
                  className={STATUS_BADGE_CLASS[row.status]}
                  data-testid="dashboard-row-status"
                >
                  {STATUS_LABEL[row.status]}
                </Badge>
              </TableCell>
              <TableCell data-testid="dashboard-row-overall">
                {formatPercent(row.overallScore)}
              </TableCell>
              <TableCell data-testid="dashboard-row-last-computed">
                {formatShortDate(row.lastComputedAt)}
              </TableCell>
              <TableCell className="text-right">
                <Button
                  asChild
                  variant="link"
                  className="h-auto p-0 text-amber-600 dark:text-amber-400"
                >
                  <Link data-testid="dashboard-row-view-link" href={reviewHref(row.sharedGameId)}>
                    View
                  </Link>
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
