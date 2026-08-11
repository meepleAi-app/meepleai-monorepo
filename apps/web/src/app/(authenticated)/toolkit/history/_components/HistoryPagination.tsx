'use client';

import type { JSX } from 'react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

export interface HistoryPaginationProps {
  page: number;
  total: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}

/** Selectable page-size options, matching the mockup's `[10, 20, 50, 100]`. */
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100];

type PageListEntry = number | 'ellipsis-left' | 'ellipsis-right';

/**
 * Builds the visible page-number list with `…` ellipses for a given current
 * page and page count. Ports the mockup's `pageList()` helper (also mirrored
 * by `CatalogPagination`'s `getPageNumbers()`): all pages when `pages <= 7`,
 * otherwise first + last page pinned with a sliding 3-page window around the
 * current page and ellipses filling the gaps.
 */
function getPageList(current: number, pages: number): PageListEntry[] {
  const out: PageListEntry[] = [];
  if (pages <= 7) {
    for (let i = 1; i <= pages; i++) out.push(i);
    return out;
  }

  out.push(1);
  let lo = Math.max(2, current - 1);
  let hi = Math.min(pages - 1, current + 1);
  if (current <= 3) {
    lo = 2;
    hi = 4;
  }
  if (current >= pages - 2) {
    lo = pages - 3;
    hi = pages - 1;
  }
  if (lo > 2) out.push('ellipsis-left');
  for (let i = lo; i <= hi; i++) out.push(i);
  if (hi < pages - 1) out.push('ellipsis-right');
  out.push(pages);

  return out;
}

/**
 * Client-side pagination bar for /toolkit/history (Issue #3006, Task A6).
 *
 * `total` is the real filtered-result count (the history orchestrator fetches
 * a batch and paginates the already-filtered rows client-side, per the task
 * brief), so `pages = ceil(total / pageSize)` and the `{from}–{to}` range are
 * computed directly here rather than trusting server metadata.
 *
 * Matches `admin-mockups/design_files/sp4-toolkit-history-ui.jsx`'s
 * `Pagination` component: a desktop layout (← Prec. · page numbers with `···`
 * ellipsis · Succ. → · range meta · per-page select) hidden below `sm`, and a
 * compact mobile layout (← · "Pag. X di Y · N sess." · →) hidden at `sm` and
 * up. Both are rendered so the pagination controls survive SSR/CSR without a
 * media-query hydration flicker; only one is visible at a time via Tailwind's
 * responsive display utilities.
 */
export function HistoryPagination({
  page,
  total,
  pageSize,
  onPageChange,
  onPageSizeChange,
}: HistoryPaginationProps): JSX.Element {
  const { t } = useTranslation();

  const pages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);
  const rangeLabel =
    total === 0
      ? t('pages.toolkitHistory.pagination.empty')
      : t('pages.toolkitHistory.pagination.rangeMeta', { from, to, total });
  const mobileLabel = t('pages.toolkitHistory.pagination.mobileMeta', { page, pages, total });
  const prevAria = t('pages.toolkitHistory.pagination.prevAria');
  const nextAria = t('pages.toolkitHistory.pagination.nextAria');

  const canPrev = page > 1;
  const canNext = page < pages;

  const handlePrev = () => {
    if (canPrev) onPageChange(page - 1);
  };
  const handleNext = () => {
    if (canNext) onPageChange(page + 1);
  };

  const pageList = getPageList(page, pages);

  return (
    <nav className="flex flex-col gap-3" aria-label={t('pages.toolkitHistory.pagination.listAria')}>
      {/* Mobile layout */}
      <div
        data-testid="history-pagination-mobile"
        className="flex items-center justify-between gap-3 sm:hidden"
      >
        <Button
          variant="outline"
          size="icon"
          onClick={handlePrev}
          disabled={!canPrev}
          aria-label={prevAria}
        >
          <span aria-hidden="true">←</span>
        </Button>
        <span className="text-sm text-muted-foreground">{mobileLabel}</span>
        <Button
          variant="outline"
          size="icon"
          onClick={handleNext}
          disabled={!canNext}
          aria-label={nextAria}
        >
          <span aria-hidden="true">→</span>
        </Button>
      </div>

      {/* Desktop layout */}
      <div
        data-testid="history-pagination-desktop"
        className="hidden flex-wrap items-center gap-3 sm:flex"
      >
        <Button
          variant="outline"
          size="sm"
          onClick={handlePrev}
          disabled={!canPrev}
          aria-label={prevAria}
        >
          <span aria-hidden="true">←</span> {t('pages.toolkitHistory.pagination.prev')}
        </Button>

        <div className="flex items-center gap-1">
          {pageList.map((entry, i) =>
            typeof entry === 'number' ? (
              <Button
                key={entry}
                variant={entry === page ? 'default' : 'outline'}
                size="icon"
                onClick={() => onPageChange(entry)}
                aria-current={entry === page ? 'page' : undefined}
              >
                {entry}
              </Button>
            ) : (
              <span key={`${entry}-${i}`} aria-hidden="true" className="px-2 text-muted-foreground">
                ···
              </span>
            )
          )}
        </div>

        <Button
          variant="outline"
          size="sm"
          onClick={handleNext}
          disabled={!canNext}
          aria-label={nextAria}
        >
          {t('pages.toolkitHistory.pagination.next')} <span aria-hidden="true">→</span>
        </Button>

        <span className="flex-1" />

        <span className="text-sm text-muted-foreground">{rangeLabel}</span>

        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          {t('pages.toolkitHistory.pagination.perPage')}
          <Select value={String(pageSize)} onValueChange={value => onPageSizeChange(Number(value))}>
            <SelectTrigger
              className="h-9 w-[4.5rem]"
              aria-label={t('pages.toolkitHistory.pagination.perPageAria')}
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAGE_SIZE_OPTIONS.map(n => (
                <SelectItem key={n} value={String(n)}>
                  {n}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </label>
      </div>
    </nav>
  );
}
