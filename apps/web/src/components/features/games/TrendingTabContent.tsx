'use client';

import { useMemo } from 'react';

import { HorizontalRow, type RowItemBase } from '@/components/features/discover';
import { useCatalogTrending } from '@/hooks/queries/useCatalogTrending';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';

/**
 * `TrendingTabContent` — `/games?tab=trending` body.
 *
 * Issue #2191: replaces the `ComingSoonTab` placeholder for the Trending tab.
 * The backend endpoint `/api/v1/catalog/trending` ships and is already consumed
 * by the Discover hub Row 1; this component wires the same hook
 * (`useCatalogTrending`) into the Games hub Trending tab so users who follow
 * the sidebar voice / deep-link finally see live data instead of a ComingSoon
 * dead end.
 *
 * Render: `HorizontalRow` with `variant="grid"` so the tab body lays out the
 * full trending list as a responsive grid (vs the carousel used on Discover).
 * Loading/error/empty states are handled by `HorizontalRow` itself.
 */
export function TrendingTabContent({ limit = 20 }: { limit?: number } = {}) {
  const { t } = useTranslation();
  const trending = useCatalogTrending(limit);

  const items = useMemo<ReadonlyArray<RowItemBase>>(
    () =>
      (trending.data ?? []).map(g => ({
        id: g.gameId,
        name: g.title,
        imageUrl: g.thumbnailUrl,
      })),
    [trending.data]
  );

  const handleCardClick = (rowId: string, item: RowItemBase) => {
    trackEvent('games_trending_tab_card_clicked', {
      row: rowId,
      entityId: item.id,
    });
  };

  return (
    <div
      data-testid="games-tab-trending"
      data-slot="games-tab-trending"
      className="flex flex-col gap-6 py-6"
    >
      <HorizontalRow
        rowId="trending"
        variant="grid"
        title={t('pages.discover.rows.trending.title')}
        subtitle={t('pages.discover.rows.trending.subtitle')}
        items={items}
        isLoading={trending.isLoading}
        isError={trending.isError}
        onRetry={() => trending.refetch()}
        onCardClick={handleCardClick}
        retryLabel={t('pages.discover.common.retry')}
        emptyLabel={t('pages.discover.common.empty')}
        viewAllLabel={t('pages.discover.common.viewAll')}
      />
    </div>
  );
}
