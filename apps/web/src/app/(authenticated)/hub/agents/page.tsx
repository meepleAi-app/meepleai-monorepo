/**
 * /hub/agents — authenticated community agents catalog (Stage 3 #1166).
 *
 * Authenticated route (via `(authenticated)` route group). Uses
 * `useDiscoverPopularAgents({ limit: 50 })` per Path C top-50 listing.
 * Full pagination deferred.
 */

'use client';

import { Suspense, useCallback, useMemo, type ReactElement } from 'react';

import {
  HubAgentCard,
  HubCatalogView,
  type HubFilter,
  type HubKpi,
} from '@/components/features/hub';
import { useDiscoverPopularAgents } from '@/hooks/queries/useDiscoverPopularAgents';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';
import type { PopularAgent } from '@/lib/api/schemas/discover.schemas';

function matchAgent(agent: PopularAgent, filter: HubFilter, q: string): boolean {
  if (q) {
    const needle = q.toLowerCase();
    const hay = `${agent.name} ${agent.gameName ?? ''}`.toLowerCase();
    if (!hay.includes(needle)) return false;
  }
  if (filter === 'all') return true;
  if (filter === 'featured') return agent.invocationCount >= 100;
  if (filter === 'new') return true; // backend doesn't expose createdAt; "new" is permissive in v1
  if (filter === 'top') return agent.invocationCount >= 500;
  return true;
}

function HubAgentsContent(): ReactElement {
  const { t } = useTranslation();
  const query = useDiscoverPopularAgents({ limit: 50 });
  const items = useMemo<ReadonlyArray<PopularAgent>>(() => query.data ?? [], [query.data]);

  const kpi = useMemo<ReadonlyArray<HubKpi>>(() => {
    const total = items.length;
    const popular = items.filter(a => a.invocationCount >= 100).length;
    const withGame = items.filter(a => a.gameName).length;
    return [
      { label: t('pages.hub.agents.kpi.total'), value: total },
      { label: t('pages.hub.agents.kpi.popular'), value: popular },
      { label: t('pages.hub.agents.kpi.gameAgents'), value: withGame },
    ];
  }, [items, t]);

  const handleCardClick = useCallback((id: string) => {
    trackEvent('hub_card_clicked', { entity: 'agents', itemId: id });
  }, []);

  const handleInstall = useCallback((id: string) => {
    trackEvent('hub_install_clicked', { entity: 'agents', itemId: id });
  }, []);

  const cardLabels = {
    installLabel: t('pages.hub.agents.installLabel'),
    invocationsTemplate: t('pages.hub.agents.invocationsTemplate'),
  };

  const labels = {
    badge: t('pages.hub.agents.badge'),
    title: t('pages.hub.agents.title'),
    subtitle: t('pages.hub.agents.subtitle'),
    searchPlaceholder: t('pages.hub.common.searchPlaceholder'),
    filterAriaLabel: t('pages.hub.common.filterAriaLabel'),
    filterLabels: {
      all: t('pages.hub.common.filters.all'),
      featured: t('pages.hub.common.filters.featured'),
      new: t('pages.hub.common.filters.new'),
      top: t('pages.hub.common.filters.top'),
    },
    emptyTitle: t('pages.hub.agents.emptyTitle'),
    emptyBody: t('pages.hub.agents.emptyBody'),
    filteredEmptyTitle: t('pages.hub.common.filteredEmptyTitle'),
    filteredEmptyBody: t('pages.hub.common.filteredEmptyBody'),
    errorTitle: t('pages.hub.common.errorTitle'),
    errorBody: t('pages.hub.common.errorBody'),
    retryLabel: t('pages.hub.common.retry'),
    resultsCountTemplate: t('pages.hub.common.resultsCountTemplate'),
  };

  return (
    <HubCatalogView<PopularAgent>
      entity="agent"
      labels={labels}
      kpi={kpi}
      items={items}
      itemMatches={matchAgent}
      renderCard={agent => (
        <HubAgentCard
          agent={agent}
          labels={cardLabels}
          onClick={handleCardClick}
          onInstall={handleInstall}
        />
      )}
      getItemKey={agent => agent.id}
      isLoading={query.isLoading}
      isError={query.isError}
      onRetry={() => query.refetch()}
      onSearchCommitted={q => trackEvent('hub_search_committed', { entity: 'agents', q })}
      onFilterChanged={(from, to) =>
        trackEvent('hub_filter_changed', { entity: 'agents', from, to })
      }
    />
  );
}

export default function HubAgentsPage() {
  return (
    <Suspense fallback={null}>
      <HubAgentsContent />
    </Suspense>
  );
}
