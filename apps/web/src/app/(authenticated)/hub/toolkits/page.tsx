/**
 * /hub/toolkits — authenticated community toolkits catalog (Stage 3 #1166).
 *
 * Authenticated route. Uses `useDiscoverRecommendedToolkits({ limit: 50 })`
 * per Path C top-50 listing.
 */

'use client';

import { Suspense, useCallback, useMemo, type ReactElement } from 'react';

import {
  HubCatalogView,
  HubToolkitCard,
  type HubFilter,
  type HubKpi,
} from '@/components/features/hub';
import { useDiscoverRecommendedToolkits } from '@/hooks/queries/useDiscoverRecommendedToolkits';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';
import type { RecommendedToolkit } from '@/lib/api/schemas/discover-cross-cutting.schemas';

function matchToolkit(toolkit: RecommendedToolkit, filter: HubFilter, q: string): boolean {
  if (q) {
    const needle = q.toLowerCase();
    const hay = `${toolkit.name} ${toolkit.authorName ?? ''}`.toLowerCase();
    if (!hay.includes(needle)) return false;
  }
  if (filter === 'all') return true;
  if (filter === 'featured') return (toolkit.ratingAverage ?? 0) >= 4;
  if (filter === 'new') return true; // permissive in v1
  if (filter === 'top') return (toolkit.ratingAverage ?? 0) >= 4.5 && toolkit.ratingCount >= 5;
  return true;
}

function HubToolkitsContent(): ReactElement {
  const { t } = useTranslation();
  const query = useDiscoverRecommendedToolkits({ limit: 50 });
  const items = useMemo<ReadonlyArray<RecommendedToolkit>>(() => query.data ?? [], [query.data]);

  const kpi = useMemo<ReadonlyArray<HubKpi>>(() => {
    const total = items.length;
    const featured = items.filter(t => (t.ratingAverage ?? 0) >= 4).length;
    const totalInstalls = items.reduce((sum, t) => sum + t.installCount, 0);
    return [
      { label: t('pages.hub.toolkits.kpi.total'), value: total },
      { label: t('pages.hub.toolkits.kpi.featured'), value: featured },
      { label: t('pages.hub.toolkits.kpi.installs'), value: totalInstalls },
    ];
  }, [items, t]);

  const handleCardClick = useCallback((id: string) => {
    trackEvent('hub_card_clicked', { entity: 'toolkits', itemId: id });
  }, []);

  const handleInstall = useCallback((id: string) => {
    trackEvent('hub_install_clicked', { entity: 'toolkits', itemId: id });
  }, []);

  const cardLabels = {
    installLabel: t('pages.hub.toolkits.installLabel'),
    toolsTemplate: t('pages.hub.toolkits.toolsTemplate'),
    installsTemplate: t('pages.hub.toolkits.installsTemplate'),
  };

  const labels = {
    badge: t('pages.hub.toolkits.badge'),
    title: t('pages.hub.toolkits.title'),
    subtitle: t('pages.hub.toolkits.subtitle'),
    searchPlaceholder: t('pages.hub.common.searchPlaceholder'),
    filterAriaLabel: t('pages.hub.common.filterAriaLabel'),
    filterLabels: {
      all: t('pages.hub.common.filters.all'),
      featured: t('pages.hub.common.filters.featured'),
      new: t('pages.hub.common.filters.new'),
      top: t('pages.hub.common.filters.top'),
    },
    emptyTitle: t('pages.hub.toolkits.emptyTitle'),
    emptyBody: t('pages.hub.toolkits.emptyBody'),
    filteredEmptyTitle: t('pages.hub.common.filteredEmptyTitle'),
    filteredEmptyBody: t('pages.hub.common.filteredEmptyBody'),
    errorTitle: t('pages.hub.common.errorTitle'),
    errorBody: t('pages.hub.common.errorBody'),
    retryLabel: t('pages.hub.common.retry'),
    resultsCountTemplate: t('pages.hub.common.resultsCountTemplate'),
  };

  return (
    <HubCatalogView<RecommendedToolkit>
      entity="toolkit"
      labels={labels}
      kpi={kpi}
      items={items}
      itemMatches={matchToolkit}
      renderCard={toolkit => (
        <HubToolkitCard
          toolkit={toolkit}
          labels={cardLabels}
          onClick={handleCardClick}
          onInstall={handleInstall}
        />
      )}
      getItemKey={toolkit => toolkit.id}
      isLoading={query.isLoading}
      isError={query.isError}
      onRetry={() => query.refetch()}
      onSearchCommitted={q => trackEvent('hub_search_committed', { entity: 'toolkits', q })}
      onFilterChanged={(from, to) =>
        trackEvent('hub_filter_changed', { entity: 'toolkits', from, to })
      }
    />
  );
}

export default function HubToolkitsPage() {
  return (
    <Suspense fallback={null}>
      <HubToolkitsContent />
    </Suspense>
  );
}
