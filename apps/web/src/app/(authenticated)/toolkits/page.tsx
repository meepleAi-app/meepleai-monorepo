/**
 * /toolkits — community toolkits catalog hub (canonical SP4 route, Issue #1480).
 *
 * Renders the 7-component mockup (sp4-hub-toolkits.jsx) implementation under
 * `features/toolkits-index/`. Replaces the legacy `/hub/toolkits` route (which
 * now redirects here). Same `useDiscoverRecommendedToolkits` hook (limit 50)
 * with FE-side filter + sort + KPI derivation.
 */

'use client';

import { Suspense, useCallback, useMemo, useState, type ReactElement } from 'react';

import {
  HubToolkitsBody,
  type HubEmptyFilteredLabels,
  type HubFiltersLabels,
  type HubFiltersSort,
  type HubFiltersStatus,
  type HubToolkitCardGridLabels,
  type HubToolkitCardItem,
  type HubToolkitsBodyState,
  type HubToolkitsHeroLabels,
  type HubToolkitsHeroStat,
  type ErrorStateLabels,
} from '@/components/features/toolkits-index';
import { useDiscoverRecommendedToolkits } from '@/hooks/queries/useDiscoverRecommendedToolkits';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';
import type { RecommendedToolkit } from '@/lib/api/schemas/discover-cross-cutting.schemas';

function matchToolkit(toolkit: RecommendedToolkit, status: HubFiltersStatus, q: string): boolean {
  if (q) {
    const needle = q.toLowerCase();
    const hay = `${toolkit.name} ${toolkit.authorName ?? ''}`.toLowerCase();
    if (!hay.includes(needle)) return false;
  }
  if (status === 'all') return true;
  if (status === 'featured') return (toolkit.ratingAverage ?? 0) >= 4;
  // P83 deferred: RecommendedToolkit v1 schema has no createdAt/publishedAt field,
  // so "new" cannot be filtered yet — returns all items. Follow-up: extend BE schema
  // with a createdAt cursor so this branch can filter to e.g. last 30 days.
  if (status === 'new') return true;
  if (status === 'top') return (toolkit.ratingAverage ?? 0) >= 4.5 && toolkit.ratingCount >= 5;
  return true;
}

function sortToolkits(
  list: ReadonlyArray<RecommendedToolkit>,
  sort: HubFiltersSort
): ReadonlyArray<RecommendedToolkit> {
  const copy = [...list];
  if (sort === 'rating') {
    copy.sort((a, b) => (b.ratingAverage ?? 0) - (a.ratingAverage ?? 0));
  } else if (sort === 'title') {
    copy.sort((a, b) => a.name.localeCompare(b.name));
  } else {
    copy.sort((a, b) => b.installCount - a.installCount);
  }
  return copy;
}

function mapToCardItem(toolkit: RecommendedToolkit): HubToolkitCardItem {
  return {
    id: toolkit.id,
    title: toolkit.name,
    authorName: toolkit.authorName,
    installCount: toolkit.installCount,
    ratingAverage: toolkit.ratingAverage,
    ratingCount: toolkit.ratingCount,
    coverImageUrl: toolkit.coverImageUrl,
    // P83 deferred fields (version / toolCount / useCount / gameName / badge) intentionally
    // omitted — RecommendedToolkit doesn't expose them in v1 (Gate B). Component gracefully
    // hides them when undefined. Follow-up issues will extend the schema.
  };
}

function HubToolkitsContent(): ReactElement {
  const { t } = useTranslation();
  const query = useDiscoverRecommendedToolkits({ limit: 50 });
  const items = useMemo<ReadonlyArray<RecommendedToolkit>>(() => query.data ?? [], [query.data]);

  const [searchQ, setSearchQ] = useState('');
  const [status, setStatus] = useState<HubFiltersStatus>('all');
  const [sort, setSort] = useState<HubFiltersSort>('popular');

  const filtered = useMemo(() => {
    const matched = items.filter(t => matchToolkit(t, status, searchQ));
    return sortToolkits(matched, sort);
  }, [items, status, searchQ, sort]);

  const toolkits = useMemo<ReadonlyArray<HubToolkitCardItem>>(
    () => filtered.map(mapToCardItem),
    [filtered]
  );

  const heroStats = useMemo<ReadonlyArray<HubToolkitsHeroStat>>(() => {
    const totalInstalls = items.reduce((sum, x) => sum + x.installCount, 0);
    const featuredCount = items.filter(x => (x.ratingAverage ?? 0) >= 4).length;
    return [
      { label: t('pages.toolkitsHub.heroStats.toolkits'), value: items.length },
      { label: t('pages.toolkitsHub.heroStats.installs'), value: totalInstalls.toLocaleString() },
      { label: t('pages.toolkitsHub.heroStats.featured'), value: featuredCount },
    ];
  }, [items, t]);

  const heroLabels = useMemo<HubToolkitsHeroLabels>(
    () => ({
      eyebrow: t('pages.toolkitsHub.eyebrow'),
      title: t('pages.hub.toolkits.title'),
      subtitle: t('pages.hub.toolkits.subtitle'),
    }),
    [t]
  );

  const filterLabels = useMemo<HubFiltersLabels>(
    () => ({
      searchPlaceholder: t('pages.hub.common.searchPlaceholder'),
      searchClearAriaLabel: t('pages.toolkitsHub.searchClearAriaLabel'),
      statusTablistAriaLabel: t('pages.toolkitsHub.statusTablistAriaLabel'),
      statusOptions: {
        all: t('pages.hub.common.filters.all'),
        featured: t('pages.hub.common.filters.featured'),
        new: t('pages.hub.common.filters.new'),
        top: t('pages.hub.common.filters.top'),
      },
      sortLabel: t('pages.toolkitsHub.sortLabel'),
      sortOptions: {
        popular: t('pages.toolkitsHub.sortOptions.popular'),
        rating: t('pages.toolkitsHub.sortOptions.rating'),
        title: t('pages.toolkitsHub.sortOptions.title'),
        uses: t('pages.toolkitsHub.sortOptions.uses'),
      },
      countTemplate: t('pages.toolkitsHub.countTemplate'),
    }),
    [t]
  );

  const cardLabels = useMemo<HubToolkitCardGridLabels>(
    () => ({
      gameRefFallback: t('pages.toolkitsHub.gameRefFallback'),
      installCta: t('pages.hub.toolkits.installLabel'),
      installAriaLabel: t('pages.toolkitsHub.installAriaLabel'),
      toolsLabel: t('pages.toolkitsHub.toolsLabel'),
      usesLabel: t('pages.toolkitsHub.usesLabel'),
    }),
    [t]
  );

  const emptyLabels = useMemo<HubEmptyFilteredLabels>(
    () => ({
      title: t('pages.hub.common.filteredEmptyTitle'),
      body: t('pages.hub.common.filteredEmptyBody'),
      reset: t('pages.toolkitsHub.resetFilters'),
      resetAriaLabel: t('pages.toolkitsHub.resetFiltersAriaLabel'),
    }),
    [t]
  );

  const errorLabels = useMemo<ErrorStateLabels>(
    () => ({
      title: t('pages.hub.common.errorTitle'),
      body: t('pages.hub.common.errorBody'),
      retry: t('pages.hub.common.retry'),
      retryAriaLabel: t('pages.toolkitsHub.retryAriaLabel'),
    }),
    [t]
  );

  const fsmState: HubToolkitsBodyState = query.isError
    ? 'error'
    : query.isLoading
      ? 'loading'
      : 'default';

  const handleInstall = useCallback((id: string) => {
    trackEvent('hub_install_clicked', { entity: 'toolkits', itemId: id });
  }, []);

  const handleCardClick = useCallback((id: string) => {
    trackEvent('hub_card_clicked', { entity: 'toolkits', itemId: id });
  }, []);

  const handleRetry = useCallback(() => {
    void query.refetch();
  }, [query]);

  return (
    <HubToolkitsBody
      state={fsmState}
      toolkits={toolkits}
      heroStats={heroStats}
      query={searchQ}
      onQueryChange={setSearchQ}
      status={status}
      onStatusChange={setStatus}
      sort={sort}
      onSortChange={setSort}
      onInstall={handleInstall}
      onCardClick={handleCardClick}
      onRetry={handleRetry}
      heroLabels={heroLabels}
      filterLabels={filterLabels}
      cardLabels={cardLabels}
      emptyLabels={emptyLabels}
      errorLabels={errorLabels}
    />
  );
}

export default function ToolkitsHubPage(): ReactElement {
  return (
    <Suspense fallback={null}>
      <HubToolkitsContent />
    </Suspense>
  );
}
