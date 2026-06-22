'use client';

/**
 * FeatureCostTable (#1838 SP5 F4-C5) — costo per feature con drill provider.
 *
 * Mockup `sp5-admin-budget.html` riga 380-470 (Scenario D nello spec doc).
 *
 * Data via {@link useFeatureCosts}. Il BE serve il drill provider inline su
 * ogni feature row, quindi l'expand è una pura interazione client (zero round-trip).
 *
 * Sorting: features arrivano già sorted DESC by total cost dal BE. Per UX
 * permettiamo l'utente di toggle sort sui campi `feature` (alpha) e `totalCost`
 * (DESC default). Il `% del totale` è derivato, ordinato come `totalCost`.
 */

import { useMemo, useState } from 'react';

import { ChevronDown, ChevronRight } from 'lucide-react';

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { useFeatureCosts } from '@/hooks/useFeatureCosts';
import type {
  CostBreakdownFeature,
  CostBreakdownRange,
} from '@/lib/api/schemas/business-cost.schemas';
import { cn } from '@/lib/utils';

type SortKey = 'feature' | 'cost';
type SortDir = 'asc' | 'desc';

function formatCurrency(value: number): string {
  return `$${value.toFixed(2)}`;
}

function formatFeatureName(raw: string): string {
  // BE returns LedgerCategory enum names (PascalCase or snake_case).
  // Display as Title Case with spaces for readability.
  return raw
    .replace(/[_-]/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/\b\w/g, c => c.toUpperCase());
}

function sortFeatures(
  features: readonly CostBreakdownFeature[],
  key: SortKey,
  dir: SortDir
): CostBreakdownFeature[] {
  const arr = [...features];
  arr.sort((a, b) => {
    if (key === 'feature') {
      const cmp = a.feature.localeCompare(b.feature);
      return dir === 'asc' ? cmp : -cmp;
    }
    const cmp = a.totalCost - b.totalCost;
    return dir === 'asc' ? cmp : -cmp;
  });
  return arr;
}

interface FeatureCostTableProps {
  range: CostBreakdownRange;
  className?: string;
}

export function FeatureCostTable({ range, className }: FeatureCostTableProps) {
  const { data, isLoading, isError, error } = useFeatureCosts(range);
  const [sortKey, setSortKey] = useState<SortKey>('cost');
  const [sortDir, setSortDir] = useState<SortDir>('desc');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const sorted = useMemo(
    () => sortFeatures(data?.features ?? [], sortKey, sortDir),
    [data, sortKey, sortDir]
  );

  const toggleSort = (key: SortKey) => {
    if (sortKey === key) {
      setSortDir(d => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir(key === 'feature' ? 'asc' : 'desc');
    }
  };

  const toggleExpand = (feature: string) => {
    setExpanded(prev => {
      const next = new Set(prev);
      if (next.has(feature)) next.delete(feature);
      else next.add(feature);
      return next;
    });
  };

  if (isLoading) {
    return (
      <section
        className={cn('overflow-hidden rounded-xl border border-border bg-card/60 p-4', className)}
        data-testid="feature-cost-table-loading"
      >
        <div className="mb-3 h-4 w-40 animate-pulse rounded bg-muted" />
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div
              key={i}
              className="h-10 animate-pulse rounded bg-muted/50"
              data-testid={`feature-cost-skeleton-${i}`}
            />
          ))}
        </div>
      </section>
    );
  }

  if (isError || !data) {
    return (
      <section
        className={cn('overflow-hidden rounded-xl border border-border bg-card/60 p-4', className)}
        data-testid="feature-cost-table-error"
      >
        <h3 className="font-quicksand text-sm font-bold text-foreground">Costi per feature</h3>
        <p className="mt-2 font-mono text-[11px] text-rose-600 dark:text-rose-400">
          Errore caricamento dati: {error instanceof Error ? error.message : 'unknown'}
        </p>
      </section>
    );
  }

  return (
    <section
      className={cn('overflow-hidden rounded-xl border border-border bg-card/60', className)}
      data-testid="feature-cost-table"
    >
      <header className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 bg-muted/30 px-4 py-2.5">
        <div className="flex items-center gap-2">
          <h3 className="font-quicksand text-sm font-bold text-foreground">
            Costi per feature · {range}
          </h3>
          <span className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
            click su una riga per il drill provider
          </span>
        </div>
        <span className="font-mono text-[10px] text-muted-foreground">
          tot {formatCurrency(data.grandTotal)} · {sorted.length} feature
        </span>
      </header>

      <Table>
        <TableHeader>
          <TableRow className="bg-muted/20">
            <TableHead className="w-8" aria-label="expand" />
            <TableHead className="w-[40%]">
              <button
                type="button"
                onClick={() => toggleSort('feature')}
                className="font-mono text-[10px] uppercase tracking-wider hover:text-foreground"
                aria-sort={
                  sortKey === 'feature' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'
                }
              >
                Feature {sortKey === 'feature' && (sortDir === 'asc' ? '▲' : '▼')}
              </button>
            </TableHead>
            <TableHead className="w-[25%] text-right">
              <button
                type="button"
                onClick={() => toggleSort('cost')}
                className="font-mono text-[10px] uppercase tracking-wider hover:text-foreground"
                aria-sort={
                  sortKey === 'cost' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'
                }
              >
                Cost totale {sortKey === 'cost' && (sortDir === 'asc' ? '▲' : '▼')}
              </button>
            </TableHead>
            <TableHead className="w-[25%] text-right font-mono text-[10px] uppercase tracking-wider">
              % del totale
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {sorted.length === 0 ? (
            <TableRow>
              <TableCell colSpan={4} className="py-10 text-center text-muted-foreground">
                Nessuna spesa registrata per il periodo selezionato.
              </TableCell>
            </TableRow>
          ) : (
            sorted.map(feature => {
              const isOpen = expanded.has(feature.feature);
              return (
                <>
                  <TableRow
                    key={feature.feature}
                    data-testid={`feature-cost-row-${feature.feature}`}
                    className="cursor-pointer hover:bg-muted/40"
                    onClick={() => toggleExpand(feature.feature)}
                  >
                    <TableCell className="text-muted-foreground">
                      {isOpen ? (
                        <ChevronDown className="h-3.5 w-3.5" aria-hidden />
                      ) : (
                        <ChevronRight className="h-3.5 w-3.5" aria-hidden />
                      )}
                    </TableCell>
                    <TableCell>
                      <span className="font-quicksand text-sm font-bold text-foreground">
                        {formatFeatureName(feature.feature)}
                      </span>
                    </TableCell>
                    <TableCell className="text-right font-mono text-[12px] font-bold tabular-nums text-foreground">
                      {formatCurrency(feature.totalCost)}
                    </TableCell>
                    <TableCell className="text-right font-mono text-[12px] tabular-nums text-muted-foreground">
                      {feature.percentageOfTotal.toFixed(1)}%
                    </TableCell>
                  </TableRow>
                  {isOpen && feature.providers.length > 0 && (
                    <TableRow
                      key={`${feature.feature}-drill`}
                      data-testid={`feature-cost-drill-${feature.feature}`}
                      className="border-l-2 border-l-entity-chat bg-muted/10"
                    >
                      <TableCell />
                      <TableCell colSpan={3}>
                        <ul className="space-y-1 py-1.5">
                          {[...feature.providers]
                            .sort((a, b) => b.cost - a.cost)
                            .map(p => (
                              <li
                                key={p.provider}
                                className="flex items-center justify-between font-mono text-[11px]"
                              >
                                <span className="text-muted-foreground">└─ {p.provider}</span>
                                <span className="font-bold tabular-nums text-foreground">
                                  {formatCurrency(p.cost)}
                                </span>
                              </li>
                            ))}
                        </ul>
                      </TableCell>
                    </TableRow>
                  )}
                </>
              );
            })
          )}
        </TableBody>
      </Table>
    </section>
  );
}
