/**
 * /hub/games — authenticated community catalog (Stage 3 cluster #1166, mockup #1151).
 *
 * Moved from `(public)` to `(authenticated)` to align the chrome with the
 * sibling `/hub/agents` and `/hub/toolkits` routes (both `authOnly`). The
 * `StickyAccessCta` (visitor → sign-in) was removed because the auth gate is
 * now upstream; visitor-facing showcase, if needed, must live under a separate
 * route family.
 */

'use client';

import { Suspense, useCallback, useMemo, type ReactElement } from 'react';

import {
  HubCatalogView,
  HubGameCard,
  type HubFilter,
  type HubKpi,
} from '@/components/features/hub';
import { useSharedGames } from '@/hooks/queries/useSharedGames';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

function matchGame(game: SharedGame, filter: HubFilter, q: string): boolean {
  if (q) {
    const needle = q.toLowerCase();
    const hay = `${game.title} ${game.description ?? ''}`.toLowerCase();
    if (!hay.includes(needle)) return false;
  }
  if (filter === 'all') return true;
  if (filter === 'featured') return (game.averageRating ?? 0) >= 7.5;
  if (filter === 'new') return game.yearPublished >= new Date().getFullYear() - 2;
  if (filter === 'top') return (game.averageRating ?? 0) >= 8.0;
  return true;
}

function HubGamesContent(): ReactElement {
  const { t } = useTranslation();
  const query = useSharedGames({ page: 1, pageSize: 100 });
  const items = useMemo<ReadonlyArray<SharedGame>>(() => query.data?.items ?? [], [query.data]);

  const kpi = useMemo<ReadonlyArray<HubKpi>>(() => {
    const total = query.data?.total ?? 0;
    const featured = items.filter(g => (g.averageRating ?? 0) >= 7.5).length;
    const recent = items.filter(g => g.yearPublished >= new Date().getFullYear() - 2).length;
    return [
      { label: t('pages.hub.games.kpi.total'), value: total },
      { label: t('pages.hub.games.kpi.featured'), value: featured },
      { label: t('pages.hub.games.kpi.recent'), value: recent },
    ];
  }, [query.data, items, t]);

  const handleCardClick = useCallback((id: string) => {
    trackEvent('hub_card_clicked', { entity: 'games', itemId: id });
  }, []);

  const labels = {
    badge: t('pages.hub.games.badge'),
    title: t('pages.hub.games.title'),
    subtitle: t('pages.hub.games.subtitle'),
    searchPlaceholder: t('pages.hub.common.searchPlaceholder'),
    filterAriaLabel: t('pages.hub.common.filterAriaLabel'),
    filterLabels: {
      all: t('pages.hub.common.filters.all'),
      featured: t('pages.hub.common.filters.featured'),
      new: t('pages.hub.common.filters.new'),
      top: t('pages.hub.common.filters.top'),
    },
    emptyTitle: t('pages.hub.games.emptyTitle'),
    emptyBody: t('pages.hub.games.emptyBody'),
    filteredEmptyTitle: t('pages.hub.common.filteredEmptyTitle'),
    filteredEmptyBody: t('pages.hub.common.filteredEmptyBody'),
    errorTitle: t('pages.hub.common.errorTitle'),
    errorBody: t('pages.hub.common.errorBody'),
    retryLabel: t('pages.hub.common.retry'),
    resultsCountTemplate: t('pages.hub.common.resultsCountTemplate'),
  };

  return (
    <HubCatalogView<SharedGame>
      entity="game"
      labels={labels}
      kpi={kpi}
      items={items}
      itemMatches={matchGame}
      renderCard={game => <HubGameCard game={game} onClick={handleCardClick} />}
      getItemKey={game => game.id}
      isLoading={query.isLoading}
      isError={query.isError}
      onRetry={() => query.refetch()}
      onSearchCommitted={q => trackEvent('hub_search_committed', { entity: 'games', q })}
      onFilterChanged={(from, to) =>
        trackEvent('hub_filter_changed', { entity: 'games', from, to })
      }
    />
  );
}

export default function HubGamesPage() {
  return (
    <Suspense fallback={null}>
      <HubGamesContent />
    </Suspense>
  );
}
