'use client';

import type { JSX } from 'react';

import { useTranslation } from '@/hooks/useTranslation';

import type { Priority } from '../_lib/wishlist-filters';

/**
 * WishlistStats — header "qstat" line for /library/wishlist (Issue #3007,
 * Task B5).
 *
 * Mirrors `admin-mockups/design_files/sp4-library-wishlist-ui.jsx`'s
 * `Header` component `lw-qstat` span (~line 423):
 * `{n} giochi · {n} alta priorità · spesa stimata {€}`.
 *
 * Consumes the exact return shape of `computeStats()` from Task B1
 * (`_lib/wishlist-filters.ts`) as its `stats` prop. `priorityCounts` is
 * accepted here for shape-parity with `computeStats`'s return type — the
 * per-bucket breakdown is rendered by `WishlistToolbar`'s priority chips,
 * not by this line.
 */

export interface WishlistStatsProps {
  stats: {
    total: number;
    highCount: number;
    totalSpend: number;
    priorityCounts: Record<Priority, number>;
  };
}

export function WishlistStats({ stats }: WishlistStatsProps): JSX.Element {
  const { t, formatNumber } = useTranslation();

  const formattedSpend = formatNumber(stats.totalSpend, {
    style: 'currency',
    currency: 'EUR',
  });

  return (
    <span
      className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground"
      data-testid="wishlist-stats"
    >
      <span className="font-semibold text-foreground">
        {t('pages.library.wishlist.stats.games', { count: stats.total })}
      </span>
      <span aria-hidden="true">·</span>
      <span className="font-semibold text-foreground">
        {t('pages.library.wishlist.stats.highPriority', { count: stats.highCount })}
      </span>
      <span aria-hidden="true">·</span>
      <span>{t('pages.library.wishlist.stats.estimatedSpend', { amount: formattedSpend })}</span>
    </span>
  );
}
