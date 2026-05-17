'use client';

import { useMemo } from 'react';

import {
  FooterCTA,
  HorizontalRow,
  type EntityFilter,
  type RowItemBase,
} from '@/components/features/discover';
import { useDiscoverNewGames } from '@/hooks/queries/useDiscoverNewGames';
import { useDiscoverPopularAgents } from '@/hooks/queries/useDiscoverPopularAgents';
import { useDiscoverRecentKbDocs } from '@/hooks/queries/useDiscoverRecentKbDocs';
import { useDiscoverRecommendedToolkits } from '@/hooks/queries/useDiscoverRecommendedToolkits';
import { useDiscoverTopContributors } from '@/hooks/queries/useDiscoverTopContributors';
import { useTranslation } from '@/hooks/useTranslation';

// Nearby events endpoint is pending #728. Disabled-shell per spec AC3.
const EVENTS_ENDPOINT_AVAILABLE = false;

export interface DiscoverBelowFoldRowsProps {
  entity: EntityFilter;
  onCardClick: (rowId: string, item: RowItemBase) => void;
  onDisabledRowVisible: (rowId: string) => void;
}

/**
 * Below-the-fold rows for `/discover` (Rows 2-7 + FooterCTA).
 * Lazy-loaded via `next/dynamic({ ssr: false })` from page.tsx to keep the
 * initial bundle small. Row 1 (trending) stays in page.tsx because it sits
 * above the fold on all viewports.
 */
export function DiscoverBelowFoldRows({
  entity,
  onCardClick,
  onDisabledRowVisible,
}: DiscoverBelowFoldRowsProps) {
  const { t } = useTranslation();

  const newGames = useDiscoverNewGames({ limit: 10 });
  const popularAgents = useDiscoverPopularAgents({ limit: 10 });
  const recentKbDocs = useDiscoverRecentKbDocs({ limit: 10 });
  const recommendedToolkits = useDiscoverRecommendedToolkits({ limit: 10 });
  const topContributors = useDiscoverTopContributors({ limit: 10 });

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

  // Mirror of the page-level rowVisible: a row is visible when filter is 'all'
  // or matches the row's entity tag. Local copy avoids passing a callback prop.
  const rowVisible = (rowEntity: EntityFilter | 'trending'): boolean => {
    if (entity === 'all') return true;
    if (entity === 'games') return rowEntity === 'games' || rowEntity === 'trending';
    return rowEntity === entity;
  };

  return (
    <>
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
        onCardClick={onCardClick}
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
        onCardClick={onCardClick}
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
        onCardClick={onCardClick}
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
        onCardClick={onCardClick}
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
        onCardClick={onCardClick}
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
        onVisible={EVENTS_ENDPOINT_AVAILABLE ? undefined : onDisabledRowVisible}
        visible={rowVisible('events')}
      />

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
    </>
  );
}
