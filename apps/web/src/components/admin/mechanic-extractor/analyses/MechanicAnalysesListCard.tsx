'use client';

/**
 * Discovery list of recent mechanic analyses (spec-panel gap #2 / ADR-051 M1.2).
 *
 * Renders a paged table of the most recent {@link MechanicAnalysisListItemDto}
 * rows so reviewers can navigate without having to know the analysis UUID
 * up-front. Clicking a row hands the id back to the parent page via
 * {@link MechanicAnalysesListCardProps.onSelect}, which then drives the
 * existing status-polling + claims viewer card on the same page.
 *
 * Design notes
 * ────────────
 * - Suppressed rows are visible (server bypasses the IsSuppressed query
 *   filter) and carry a red "Suppressed" badge — admins must be able to find
 *   suppressed entries from the index, not only via direct UUID.
 * - Pagination is plain offset (Page/PageSize); the dataset is bounded
 *   (a few hundred rows max), so keyset would be overkill.
 * - We re-fetch on `analysisId` mutations elsewhere in the page by keying the
 *   query on `[page, pageSize]`; React Query invalidations from the parent
 *   only target `['mechanic-analysis', id]` so this list is intentionally
 *   stable across lifecycle actions (an admin reviewing claims doesn't want
 *   the list scrolling under them).
 */

import * as React from 'react';
import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  Loader2Icon,
  RefreshCwIcon,
  ShieldAlertIcon,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import {
  MECHANIC_ANALYSIS_STATUS_LABELS,
  MechanicAnalysisStatus,
} from '@/lib/api/schemas/mechanic-analyses.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

const STATUS_BADGE_CLASS: Record<number, string> = {
  [MechanicAnalysisStatus.Draft]: 'bg-slate-100 text-slate-700 border-slate-300',
  [MechanicAnalysisStatus.InReview]: 'bg-amber-100 text-amber-800 border-amber-300',
  [MechanicAnalysisStatus.Published]: 'bg-green-100 text-green-800 border-green-300',
  [MechanicAnalysisStatus.Rejected]: 'bg-rose-100 text-rose-800 border-rose-300',
  [MechanicAnalysisStatus.PartiallyExtracted]: 'bg-yellow-100 text-yellow-800 border-yellow-400',
};

const PAGE_SIZE = 20;

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString();
}

function formatCurrency(value: number | null | undefined): string {
  if (value == null) return '—';
  return `$${value.toFixed(4)}`;
}

export interface MechanicAnalysesListCardProps {
  /** Called when the user clicks a row — receives the analysis UUID. */
  onSelect: (id: string) => void;
  /** When set, the matching row is highlighted to show "current selection". */
  selectedId?: string | null;
}

export function MechanicAnalysesListCard({
  onSelect,
  selectedId,
}: MechanicAnalysesListCardProps): React.JSX.Element {
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ['mechanic-analyses-list', page, PAGE_SIZE],
    queryFn: () => adminClient.listMechanicAnalyses({ page, pageSize: PAGE_SIZE }),
    staleTime: 15_000,
  });

  const data = query.data;
  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = totalCount > 0 ? Math.ceil(totalCount / PAGE_SIZE) : 1;
  const canPrev = page > 1;
  const canNext = page < totalPages;

  return (
    <Card
      className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/60"
      data-testid="mechanic-analyses-list-card"
    >
      <CardContent className="pt-6 space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="text-lg font-medium font-quicksand">Recent analyses</h2>
            <p className="text-xs text-muted-foreground">
              Click a row to load its status, claims and lifecycle controls below.
              {totalCount > 0 && (
                <>
                  {' '}
                  Showing page {page} of {totalPages} ({totalCount} total).
                </>
              )}
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => query.refetch()}
            disabled={query.isFetching}
            data-testid="mechanic-analyses-list-refresh"
          >
            {query.isFetching ? (
              <Loader2Icon className="mr-1 h-3 w-3 animate-spin" />
            ) : (
              <RefreshCwIcon className="mr-1 h-3 w-3" />
            )}
            Refresh
          </Button>
        </div>

        {query.isError && (
          <div className="rounded-md border border-rose-300 bg-rose-50 p-2 text-sm text-rose-800">
            Failed to load analyses:{' '}
            {query.error instanceof Error ? query.error.message : 'Unknown error'}
          </div>
        )}

        {query.isLoading && <p className="text-sm text-muted-foreground">Loading analyses…</p>}

        {!query.isLoading && items.length === 0 && (
          <p className="text-sm text-muted-foreground">
            No mechanic analyses yet — start one from the form below.
          </p>
        )}

        {items.length > 0 && (
          <div className="overflow-x-auto rounded-md border border-slate-200 dark:border-zinc-700">
            <table className="w-full text-xs">
              <thead className="bg-slate-50 dark:bg-zinc-900">
                <tr>
                  <th className="p-2 text-left">Game</th>
                  <th className="p-2 text-left">Status</th>
                  <th className="p-2 text-right">Claims</th>
                  <th className="p-2 text-right">Cost</th>
                  <th className="p-2 text-left">Created</th>
                </tr>
              </thead>
              <tbody>
                {items.map(row => {
                  const isSelected = selectedId === row.id;
                  return (
                    <tr
                      key={row.id}
                      onClick={() => onSelect(row.id)}
                      className={
                        'cursor-pointer border-t border-slate-200 transition-colors hover:bg-amber-50 dark:border-zinc-700 dark:hover:bg-amber-950/20 ' +
                        (isSelected ? 'bg-amber-100/70 dark:bg-amber-950/30' : '')
                      }
                      data-testid={`mechanic-analyses-list-row-${row.id}`}
                    >
                      <td className="p-2">
                        <div className="font-medium">{row.gameTitle}</div>
                        <div className="font-mono text-[10px] text-muted-foreground">{row.id}</div>
                      </td>
                      <td className="p-2">
                        <div className="flex flex-wrap items-center gap-1">
                          <Badge variant="outline" className={STATUS_BADGE_CLASS[row.status] ?? ''}>
                            {MECHANIC_ANALYSIS_STATUS_LABELS[row.status] ?? String(row.status)}
                          </Badge>
                          {row.isSuppressed && (
                            <Badge
                              variant="outline"
                              className="border-rose-400 bg-rose-50 text-rose-800"
                              data-testid={`mechanic-analyses-list-suppressed-${row.id}`}
                            >
                              <ShieldAlertIcon className="mr-1 h-3 w-3" />
                              Suppressed
                            </Badge>
                          )}
                        </div>
                      </td>
                      <td className="p-2 text-right">{row.claimsCount}</td>
                      <td className="p-2 text-right">{formatCurrency(row.estimatedCostUsd)}</td>
                      <td className="p-2">{formatDate(row.createdAt)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {totalCount > PAGE_SIZE && (
          <div className="flex items-center justify-between gap-2 pt-1">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={!canPrev || query.isFetching}
              data-testid="mechanic-analyses-list-prev"
            >
              <ChevronLeftIcon className="mr-1 h-3 w-3" />
              Previous
            </Button>
            <span className="text-xs text-muted-foreground">
              Page {page} of {totalPages}
            </span>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage(p => p + 1)}
              disabled={!canNext || query.isFetching}
              data-testid="mechanic-analyses-list-next"
            >
              Next
              <ChevronRightIcon className="ml-1 h-3 w-3" />
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
