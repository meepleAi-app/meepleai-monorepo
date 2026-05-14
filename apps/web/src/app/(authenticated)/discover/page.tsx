'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  DiscoverHero,
  DiscoverSearchBox,
  EntityFilterPillBar,
  FooterCTA,
  HorizontalRow,
  type EntityFilter,
  type RowItemBase,
} from '@/components/features/discover';
import { HubLayout } from '@/components/layout/HubLayout';
import { useCatalogTrending } from '@/hooks/queries/useCatalogTrending';
import { useDiscoverNewGames } from '@/hooks/queries/useDiscoverNewGames';
import { useDiscoverPopularAgents } from '@/hooks/queries/useDiscoverPopularAgents';
import { useDiscoverRecentKbDocs } from '@/hooks/queries/useDiscoverRecentKbDocs';
import { useDiscoverRecommendedToolkits } from '@/hooks/queries/useDiscoverRecommendedToolkits';
import { useDiscoverTopContributors } from '@/hooks/queries/useDiscoverTopContributors';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';

// Search is currently not implemented backend-side (GET /api/v1/catalog/search).
// Setting this flag flips the SearchBox into disabled-shell mode + telemetry
// per spec AC4. Flip to `true` when the endpoint lands (#728).
const SEARCH_ENDPOINT_AVAILABLE = false;

// Nearby events endpoint is pending #728. Disabled-shell per spec AC3.
const EVENTS_ENDPOINT_AVAILABLE = false;

const VALID_FILTERS: ReadonlyArray<EntityFilter> = [
  'all',
  'games',
  'agents',
  'toolkits',
  'kbs',
  'people',
  'events',
];

function parseFilter(raw: string | null): EntityFilter {
  if (raw && (VALID_FILTERS as ReadonlyArray<string>).includes(raw)) return raw as EntityFilter;
  return 'all';
}

export default function DiscoverPage() {
  const { t } = useTranslation();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  useMiniNavConfig({
    breadcrumb: t('pages.discover.miniNav.breadcrumb'),
    tabs: [{ id: 'all', label: t('pages.discover.miniNav.tabAll'), href: '/discover' }],
    activeTabId: 'all',
  });

  // ── URL state SSOT ────────────────────────────────────────────────────────
  const initialQ = searchParams.get('q') ?? '';
  const initialEntity = parseFilter(searchParams.get('entity'));

  const [q, setQ] = useState(initialQ);
  const [entity, setEntity] = useState<EntityFilter>(initialEntity);

  // Sync local state when URL changes externally (back/forward navigation)
  useEffect(() => {
    setQ(searchParams.get('q') ?? '');
    setEntity(parseFilter(searchParams.get('entity')));
  }, [searchParams]);

  // ── Data hooks (live) ─────────────────────────────────────────────────────
  const trending = useCatalogTrending(10);
  const newGames = useDiscoverNewGames({ limit: 10 });
  const popularAgents = useDiscoverPopularAgents({ limit: 10 });
  const recentKbDocs = useDiscoverRecentKbDocs({ limit: 10 });
  const recommendedToolkits = useDiscoverRecommendedToolkits({ limit: 10 });
  const topContributors = useDiscoverTopContributors({ limit: 10 });
  // Events: backend not live (see flag above). Hook intentionally omitted —
  // disabled-shell rendered instead.

  // ── DTO → RowItemBase adapters ───────────────────────────────────────────
  // Each hook returns its native DTO shape. We project to the generic
  // RowItemBase contract that HorizontalRow consumes, keeping the component
  // decoupled from per-row schema details.
  const trendingItems = useMemo<ReadonlyArray<RowItemBase>>(
    () =>
      (trending.data ?? []).map(g => ({
        id: g.gameId,
        name: g.title,
        imageUrl: g.thumbnailUrl,
      })),
    [trending.data]
  );

  const newGameItems = useMemo<ReadonlyArray<RowItemBase>>(
    () =>
      (newGames.data ?? []).map(g => ({
        id: g.id,
        name: g.name,
        publisher: g.publisher,
        year: g.year,
        imageUrl: g.imageUrl,
      })),
    [newGames.data]
  );

  const popularAgentItems = useMemo<ReadonlyArray<RowItemBase>>(
    () =>
      (popularAgents.data ?? []).map(a => ({
        id: a.id,
        name: a.name,
        gameName: a.gameName,
        installCount: a.installCount,
        invocationCount: a.invocationCount,
      })),
    [popularAgents.data]
  );

  const recentKbDocItems = useMemo<ReadonlyArray<RowItemBase>>(
    () =>
      (recentKbDocs.data ?? []).map(d => ({
        id: d.id,
        title: d.title,
        gameName: d.gameName,
        docType: d.docType,
        chunkCount: d.chunkCount,
      })),
    [recentKbDocs.data]
  );

  const recommendedToolkitItems = useMemo<ReadonlyArray<RowItemBase>>(
    () =>
      (recommendedToolkits.data ?? []).map(tk => ({
        id: tk.id,
        name: tk.name,
        authorName: tk.authorName,
        installCount: tk.installCount,
        coverImageUrl: tk.coverImageUrl,
      })),
    [recommendedToolkits.data]
  );

  const topContributorItems = useMemo<ReadonlyArray<RowItemBase>>(
    () =>
      (topContributors.data ?? []).map(c => ({
        id: c.id,
        displayName: c.displayName,
        avatarUrl: c.avatarUrl,
        contributionCount: c.contributionCount,
      })),
    [topContributors.data]
  );

  // ── URL update helpers ────────────────────────────────────────────────────
  const updateUrl = useCallback(
    (next: { q?: string; entity?: EntityFilter }) => {
      const params = new URLSearchParams(searchParams.toString());
      const nextQ = next.q !== undefined ? next.q : q;
      const nextEntity = next.entity !== undefined ? next.entity : entity;
      if (nextQ) params.set('q', nextQ);
      else params.delete('q');
      if (nextEntity && nextEntity !== 'all') params.set('entity', nextEntity);
      else params.delete('entity');
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
    },
    [searchParams, q, entity, pathname, router]
  );

  // ── Search handlers ───────────────────────────────────────────────────────
  const handleSearchCommit = useCallback(
    (value: string) => {
      setQ(value);
      updateUrl({ q: value });
      trackEvent('discover_search_committed', { q: value, entityFilter: entity });
    },
    [entity, updateUrl]
  );

  const handleDisabledSearchFocus = useCallback(() => {
    trackEvent('discover_search_attempted_unavailable', { rowId: 'search' });
  }, []);

  // ── Filter pill handler ───────────────────────────────────────────────────
  const handleFilterChange = useCallback(
    (next: EntityFilter) => {
      const prev = entity;
      setEntity(next);
      updateUrl({ entity: next });
      trackEvent('discover_filter_pill_clicked', { entity: next, previousEntity: prev });
    },
    [entity, updateUrl]
  );

  // ── Telemetry: card click + disabled-row impression ───────────────────────
  const handleCardClick = useCallback((rowId: string, item: RowItemBase) => {
    trackEvent('discover_card_clicked', {
      row: rowId,
      entityId: item.id,
      entityType: rowId,
    });
  }, []);

  const handleDisabledRowVisible = useCallback((rowId: string) => {
    trackEvent('discover_disabled_row_visible', { row: rowId });
  }, []);

  // ── Pill labels ──────────────────────────────────────────────────────────
  const pillLabels = useMemo<Readonly<Record<EntityFilter, string>>>(
    () => ({
      all: t('pages.discover.filters.all'),
      games: t('pages.discover.filters.games'),
      agents: t('pages.discover.filters.agents'),
      toolkits: t('pages.discover.filters.toolkits'),
      kbs: t('pages.discover.filters.kbs'),
      people: t('pages.discover.filters.people'),
      events: t('pages.discover.filters.events'),
    }),
    [t]
  );

  // ── Row visibility (client-side filter) ───────────────────────────────────
  // A row is visible when filter is 'all' OR matches the row's entity tag.
  const rowVisible = useCallback(
    (rowEntity: EntityFilter | 'trending') => {
      if (entity === 'all') return true;
      // Trending and Games rows are both "games" for filter purposes
      if (entity === 'games') return rowEntity === 'games' || rowEntity === 'trending';
      return rowEntity === entity;
    },
    [entity]
  );

  return (
    <HubLayout searchPlaceholder={t('pages.discover.search.placeholder')}>
      <DiscoverHero
        title={t('pages.discover.hero.title')}
        subtitle={t('pages.discover.hero.subtitle')}
        searchSlot={
          <DiscoverSearchBox
            value={q}
            onCommit={handleSearchCommit}
            placeholder={t('pages.discover.search.placeholder')}
            disabled={!SEARCH_ENDPOINT_AVAILABLE}
            disabledTooltip={t('pages.discover.search.disabledTooltip')}
            onDisabledFocus={handleDisabledSearchFocus}
          />
        }
        filterSlot={
          <EntityFilterPillBar
            value={entity}
            onChange={handleFilterChange}
            labels={pillLabels}
            ariaLabel={t('pages.discover.filters.ariaLabel')}
          />
        }
      />

      <div data-slot="discover-rows" className="flex flex-col">
        {/* Row 1 — Trending games */}
        <HorizontalRow
          rowId="trending"
          variant="featured"
          title={t('pages.discover.rows.trending.title')}
          subtitle={t('pages.discover.rows.trending.subtitle')}
          items={trendingItems}
          isLoading={trending.isLoading}
          isError={trending.isError}
          onRetry={() => trending.refetch()}
          onCardClick={handleCardClick}
          retryLabel={t('pages.discover.common.retry')}
          emptyLabel={t('pages.discover.common.empty')}
          viewAllLabel={t('pages.discover.common.viewAll')}
          visible={rowVisible('trending')}
        />

        {/* Row 2 — New games */}
        <HorizontalRow
          rowId="games"
          variant="featured"
          title={t('pages.discover.rows.newGames.title')}
          subtitle={t('pages.discover.rows.newGames.subtitle')}
          items={newGameItems}
          isLoading={newGames.isLoading}
          isError={newGames.isError}
          onRetry={() => newGames.refetch()}
          onCardClick={handleCardClick}
          retryLabel={t('pages.discover.common.retry')}
          emptyLabel={t('pages.discover.common.empty')}
          viewAllLabel={t('pages.discover.common.viewAll')}
          visible={rowVisible('games')}
        />

        {/* Row 3 — Popular agents */}
        <HorizontalRow
          rowId="agents"
          variant="compact"
          title={t('pages.discover.rows.popularAgents.title')}
          subtitle={t('pages.discover.rows.popularAgents.subtitle')}
          items={popularAgentItems}
          isLoading={popularAgents.isLoading}
          isError={popularAgents.isError}
          onRetry={() => popularAgents.refetch()}
          onCardClick={handleCardClick}
          retryLabel={t('pages.discover.common.retry')}
          emptyLabel={t('pages.discover.common.empty')}
          viewAllLabel={t('pages.discover.common.viewAll')}
          visible={rowVisible('agents')}
        />

        {/* Row 4 — Recommended toolkits */}
        <HorizontalRow
          rowId="toolkits"
          variant="grid"
          title={t('pages.discover.rows.recommendedToolkits.title')}
          subtitle={t('pages.discover.rows.recommendedToolkits.subtitle')}
          items={recommendedToolkitItems}
          isLoading={recommendedToolkits.isLoading}
          isError={recommendedToolkits.isError}
          onRetry={() => recommendedToolkits.refetch()}
          onCardClick={handleCardClick}
          retryLabel={t('pages.discover.common.retry')}
          emptyLabel={t('pages.discover.common.empty')}
          viewAllLabel={t('pages.discover.common.viewAll')}
          visible={rowVisible('toolkits')}
        />

        {/* Row 5 — Recent KB docs */}
        <HorizontalRow
          rowId="kbs"
          variant="list-row"
          title={t('pages.discover.rows.recentKbDocs.title')}
          subtitle={t('pages.discover.rows.recentKbDocs.subtitle')}
          items={recentKbDocItems}
          isLoading={recentKbDocs.isLoading}
          isError={recentKbDocs.isError}
          onRetry={() => recentKbDocs.refetch()}
          onCardClick={handleCardClick}
          retryLabel={t('pages.discover.common.retry')}
          emptyLabel={t('pages.discover.common.empty')}
          viewAllLabel={t('pages.discover.common.viewAll')}
          visible={rowVisible('kbs')}
        />

        {/* Row 6 — Top contributors */}
        <HorizontalRow
          rowId="people"
          variant="list-row"
          title={t('pages.discover.rows.topContributors.title')}
          subtitle={t('pages.discover.rows.topContributors.subtitle')}
          items={topContributorItems}
          isLoading={topContributors.isLoading}
          isError={topContributors.isError}
          onRetry={() => topContributors.refetch()}
          onCardClick={handleCardClick}
          retryLabel={t('pages.discover.common.retry')}
          emptyLabel={t('pages.discover.common.empty')}
          viewAllLabel={t('pages.discover.common.viewAll')}
          visible={rowVisible('people')}
        />

        {/* Row 7 — Nearby events (disabled-shell, pending #728) */}
        <HorizontalRow
          rowId="events"
          variant="list-row"
          title={t('pages.discover.rows.nearbyEvents.title')}
          subtitle={t('pages.discover.rows.nearbyEvents.subtitle')}
          items={[]}
          state={EVENTS_ENDPOINT_AVAILABLE ? 'enabled' : 'disabled'}
          disabledTooltip={t('pages.discover.common.disabledPhase05')}
          onVisible={EVENTS_ENDPOINT_AVAILABLE ? undefined : handleDisabledRowVisible}
          visible={rowVisible('events')}
        />
      </div>

      <FooterCTA
        title={t('pages.discover.footer.title')}
        description={t('pages.discover.footer.description')}
        primaryCta={{
          label: t('pages.discover.footer.primaryCta'),
          href: '/library',
        }}
        secondaryCta={{
          label: t('pages.discover.footer.secondaryCta'),
          href: '/players',
        }}
      />
    </HubLayout>
  );
}
