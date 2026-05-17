'use client';

import { Suspense, useCallback, useEffect, useMemo, useState } from 'react';

import dynamic from 'next/dynamic';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  DiscoverHero,
  DiscoverSearchBox,
  EntityFilterPillBar,
  HorizontalRow,
  type EntityFilter,
  type RowItemBase,
} from '@/components/features/discover';
import { HubLayout } from '@/components/layout/HubLayout';
import { useCatalogTrending } from '@/hooks/queries/useCatalogTrending';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';

// Lazy-loaded below-the-fold block (Rows 2-7 + FooterCTA + 5 useDiscover* hooks).
// `ssr: false` is required because all below-the-fold hooks use TanStack Query
// (client-only). Keeps `/discover` initial bundle within budget — see
// .bundle-budgets.json. Pattern reference: play-records/page.tsx:18-21.
const DiscoverBelowFoldRows = dynamic(
  () => import('./_DiscoverBelowFoldRows').then(mod => ({ default: mod.DiscoverBelowFoldRows })),
  {
    ssr: false,
    loading: () => <BelowFoldSkeleton />,
  }
);

// Search is currently not implemented backend-side (GET /api/v1/catalog/search).
// Setting this flag flips the SearchBox into disabled-shell mode + telemetry
// per spec AC4. Flip to `true` when the endpoint lands (#728).
const SEARCH_ENDPOINT_AVAILABLE = false;

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

/**
 * Skeleton placeholder for the below-the-fold lazy block. Reserves vertical
 * space (~1400px ≈ 6 rows) to prevent CLS while the lazy chunk loads. Uses
 * semantic tokens per CLAUDE.md "Token Canonicalization" rules (no
 * `bg-slate-*` / `bg-zinc-*`).
 */
function BelowFoldSkeleton() {
  return (
    <div data-slot="discover-below-fold-skeleton" className="flex flex-col gap-8" aria-hidden>
      {[320, 260, 180, 380, 200, 200].map((height, idx) => (
        <div key={idx} className="flex flex-col gap-3">
          <div className="h-6 w-48 bg-muted/40 rounded animate-pulse" />
          <div
            className="w-full bg-muted/30 rounded animate-pulse"
            style={{ height: `${height}px` }}
          />
        </div>
      ))}
    </div>
  );
}

/**
 * Default export wraps the discover content in a Suspense boundary.
 * Required by Next.js App Router because `useSearchParams()` opts the page
 * out of SSR static prerendering; without Suspense, the build emits a
 * "should be wrapped in a suspense boundary" CSR bailout error.
 */
export default function DiscoverPage() {
  return (
    <Suspense fallback={null}>
      <DiscoverContent />
    </Suspense>
  );
}

function DiscoverContent() {
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

  // ── Data hooks (live) — only Row 1 stays here; Rows 2-7 hooks live in the
  // lazy `_DiscoverBelowFoldRows` chunk.
  const trending = useCatalogTrending(10);

  // ── DTO → RowItemBase adapter for Row 1 (trending) ───────────────────────
  const trendingItems = useMemo<ReadonlyArray<RowItemBase>>(
    () =>
      (trending.data ?? []).map(g => ({
        id: g.gameId,
        name: g.title,
        imageUrl: g.thumbnailUrl,
      })),
    [trending.data]
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

  // ── Row visibility for Row 1 (trending) ──────────────────────────────────
  const trendingVisible = entity === 'all' || entity === 'games';

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
        {/* Row 1 — Trending games (above the fold, eager) */}
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
          visible={trendingVisible}
        />

        {/* Rows 2-7 + FooterCTA — lazy-loaded below the fold */}
        <DiscoverBelowFoldRows
          entity={entity}
          onCardClick={handleCardClick}
          onDisabledRowVisible={handleDisabledRowVisible}
        />
      </div>
    </HubLayout>
  );
}
