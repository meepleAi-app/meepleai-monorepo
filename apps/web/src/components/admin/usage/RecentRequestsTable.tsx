'use client';

/**
 * Recent LLM requests table.
 * Issue #5083: Admin usage page — paginated recent-requests table with filters.
 */

import { useState } from 'react';

import {
  CheckCircle,
  ChevronLeft,
  ChevronRight,
  Filter,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import type { RecentLlmRequestsDto } from '@/lib/api/schemas/admin-knowledge-base.schemas';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface RecentRequestsFilters {
  source?: string;
  model?: string;
  successOnly?: boolean;
  page: number;
  pageSize: number;
}

interface RecentRequestsTableProps {
  data: RecentLlmRequestsDto | null | undefined;
  filters: RecentRequestsFilters;
  onFiltersChange: (filters: RecentRequestsFilters) => void;
  isLoading?: boolean;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatMs(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`;
  return `${ms}ms`;
}

function formatUsd(val: number): string {
  if (val === 0) return '$0';
  return `$${val.toFixed(5)}`;
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

const SOURCE_OPTIONS = [
  '',
  'Manual',
  'RagPipeline',
  'EventDriven',
  'AutomatedTest',
  'AgentTask',
  'AdminOperation',
];

// ─── Component ───────────────────────────────────────────────────────────────

export function RecentRequestsTable({
  data,
  filters,
  onFiltersChange,
  isLoading,
}: RecentRequestsTableProps) {
  const [showFilters, setShowFilters] = useState(false);

  function setFilter<K extends keyof RecentRequestsFilters>(
    key: K,
    value: RecentRequestsFilters[K]
  ) {
    onFiltersChange({ ...filters, [key]: value, page: 1 });
  }

  function changePage(delta: number) {
    onFiltersChange({ ...filters, page: filters.page + delta });
  }

  const totalPages = data?.totalPages ?? 1;

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-medium">Recent Requests</CardTitle>
          <div className="flex items-center gap-2">
            {data && (
              <span className="text-xs text-muted-foreground">
                {data.total.toLocaleString()} total
              </span>
            )}
            <Button
              variant="ghost"
              size="sm"
              className="h-6 px-2 text-xs gap-1"
              onClick={() => setShowFilters((v) => !v)}
            >
              <Filter className="h-3 w-3" />
              Filters
            </Button>
          </div>
        </div>

        {/* Filter row */}
        {showFilters && (
          <div className="flex flex-wrap gap-2 mt-2">
            <select
              value={filters.source ?? ''}
              onChange={(e) => setFilter('source', e.target.value || undefined)}
              className="h-7 rounded-md border bg-background px-2 text-xs text-foreground"
              aria-label="Filter by source"
            >
              {SOURCE_OPTIONS.map((s) => (
                <option key={s} value={s}>
                  {s || 'All sources'}
                </option>
              ))}
            </select>

            <input
              type="text"
              placeholder="Model filter..."
              value={filters.model ?? ''}
              onChange={(e) => setFilter('model', e.target.value || undefined)}
              className="h-7 rounded-md border bg-background px-2 text-xs placeholder:text-muted-foreground"
              aria-label="Filter by model"
            />

            <select
              value={
                filters.successOnly === undefined ? '' : filters.successOnly ? 'true' : 'false'
              }
              onChange={(e) =>
                setFilter(
                  'successOnly',
                  e.target.value === '' ? undefined : e.target.value === 'true'
                )
              }
              className="h-7 rounded-md border bg-background px-2 text-xs text-foreground"
              aria-label="Filter by success"
            >
              <option value="">All results</option>
              <option value="true">Success only</option>
              <option value="false">Errors only</option>
            </select>
          </div>
        )}
      </CardHeader>

      <CardContent className="p-0">
        {isLoading ? (
          <div className="space-y-2 p-4">
            {[1, 2, 3, 4, 5].map((i) => (
              <Skeleton key={i} className="h-8 w-full" />
            ))}
          </div>
        ) : !data || data.items.length === 0 ? (
          <div className="flex h-32 items-center justify-center text-sm text-muted-foreground">
            No requests found
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-3 py-2 text-left font-medium text-muted-foreground">Time</th>
                    <th className="px-3 py-2 text-left font-medium text-muted-foreground">Model</th>
                    <th className="px-3 py-2 text-left font-medium text-muted-foreground">Source</th>
                    <th className="px-3 py-2 text-right font-medium text-muted-foreground">Tokens</th>
                    <th className="px-3 py-2 text-right font-medium text-muted-foreground">Cost</th>
                    <th className="px-3 py-2 text-right font-medium text-muted-foreground">Latency</th>
                    <th className="px-3 py-2 text-center font-medium text-muted-foreground">OK</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((req) => (
                    <tr key={req.id} className="border-b last:border-0 hover:bg-muted/30">
                      <td className="px-3 py-1.5 font-mono text-muted-foreground whitespace-nowrap">
                        {formatTime(req.requestedAt)}
                      </td>
                      <td
                        className="px-3 py-1.5 max-w-[140px] truncate"
                        title={req.modelId}
                      >
                        {req.modelId.split('/').pop() ?? req.modelId}
                        {req.isFreeModel && (
                          <Badge variant="secondary" className="ml-1 text-[9px] px-1 py-0 h-3.5">
                            free
                          </Badge>
                        )}
                      </td>
                      <td className="px-3 py-1.5 text-muted-foreground">{req.source}</td>
                      <td className="px-3 py-1.5 text-right tabular-nums">
                        {req.totalTokens.toLocaleString()}
                      </td>
                      <td className="px-3 py-1.5 text-right tabular-nums font-mono">
                        {formatUsd(req.costUsd)}
                      </td>
                      <td className="px-3 py-1.5 text-right tabular-nums text-muted-foreground">
                        {formatMs(req.latencyMs)}
                      </td>
                      <td className="px-3 py-1.5 text-center">
                        {req.success ? (
                          <CheckCircle className="h-3.5 w-3.5 text-emerald-500 mx-auto" />
                        ) : (
                          <XCircle
                            className="h-3.5 w-3.5 text-destructive mx-auto"
                            title={req.errorMessage ?? 'Error'}
                          />
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between border-t px-3 py-2 text-xs text-muted-foreground">
                <span>
                  Page {filters.page} of {totalPages}
                </span>
                <div className="flex gap-1">
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 w-6 p-0"
                    onClick={() => changePage(-1)}
                    disabled={filters.page <= 1}
                    aria-label="Previous page"
                  >
                    <ChevronLeft className="h-3.5 w-3.5" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-6 w-6 p-0"
                    onClick={() => changePage(1)}
                    disabled={filters.page >= totalPages}
                    aria-label="Next page"
                  >
                    <ChevronRight className="h-3.5 w-3.5" />
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
