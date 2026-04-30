/**
 * GamesLibraryView — Wave B.1 (Issue #633) orchestrator for `/games?tab=library`.
 *
 * Composes the 4 v2 components introduced in Commit 2 with:
 *   - `useLibrary` TanStack hook (real fetch, cached 2min staleTime)
 *   - i18n labels resolved upfront via a single `useTranslation()`
 *   - 4 derivation pipelines (filterByStatus → matchQuery → sortLibraryEntries
 *     → deriveStats) over `UserLibraryEntry`
 *   - 5-state FSM: `default | loading | empty | filtered-empty | error`
 *   - `?state=...` URL override (NODE_ENV !== 'production' OR visual-test build)
 *   - Visual-test fixture short-circuit (`IS_VISUAL_TEST_BUILD`) for CI
 *   - clearFilters CTA: resets `query` + `status` (sort/view preserved); when an
 *     override is active also drops `?state=` from the URL via `router.push`
 *
 * Wiring is intentionally strict: HubLayout / catalog / kb tabs are out of
 * scope (page.tsx branches on `tab === 'library'` and renders this component
 * instead — see AC-13 in the spec).
 */

'use client';

import { useCallback, useMemo, useState, type ReactElement } from 'react';

import { useRouter, useSearchParams, usePathname } from 'next/navigation';

import {
  GamesEmptyState,
  GamesFiltersInline,
  GamesHero,
  GamesResultsGrid,
  type GamesEmptyKind,
  type GamesEmptyStateLabels,
  type GamesFiltersInlineLabels,
  type GamesHeroLabels,
  type GamesSortKey,
  type GamesStatusKey,
  type GamesViewKey,
} from '@/components/v2/games';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';
import {
  deriveStats,
  filterByStatus,
  matchQuery,
  sortLibraryEntries,
} from '@/lib/games/library-filters';
import { IS_VISUAL_TEST_BUILD, tryLoadVisualTestFixture } from '@/lib/games/visual-test-fixture';

// ─── State override hatch (dev/visual-test only) ───────────────────────────

const VALID_OVERRIDES = ['loading', 'empty', 'filtered-empty', 'error'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  if (raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}

type SurfaceKind = GamesEmptyKind | 'default';

// ─── Component ──────────────────────────────────────────────────────────────

export function GamesLibraryView(): ReactElement {
  const { t } = useTranslation();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const [query, setQuery] = useState<string>('');
  const [status, setStatus] = useState<GamesStatusKey>('all');
  const [sort, setSort] = useState<GamesSortKey>('last-played');
  const [view, setView] = useState<GamesViewKey>('grid');

  const stateOverride = parseStateOverride(searchParams.get('state'));

  const libraryQuery = useLibrary();

  const fixtureItems = useMemo(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    return tryLoadVisualTestFixture(stateOverride === 'empty' ? 'empty' : 'default');
  }, [stateOverride]);

  const items = useMemo(
    () => fixtureItems ?? libraryQuery.data?.items ?? [],
    [fixtureItems, libraryQuery.data]
  );

  const stats = useMemo(() => deriveStats(items), [items]);

  const filtered = useMemo(() => {
    const byStatus = filterByStatus(items, status);
    const byQuery = byStatus.filter(entry => matchQuery(entry, query));
    return sortLibraryEntries(byQuery, sort);
  }, [items, status, query, sort]);

  const heroLabels = useMemo<GamesHeroLabels>(
    () => ({
      title: t('pages.games.library.hero.title'),
      subtitle: t('pages.games.library.hero.subtitle'),
      ctaAdd: t('pages.games.library.hero.cta.add'),
    }),
    [t]
  );

  const heroStats = useMemo(
    () => [
      { label: t('pages.games.library.hero.stats.owned'), value: stats.owned },
      { label: t('pages.games.library.hero.stats.wishlist'), value: stats.wishlist },
      {
        label: t('pages.games.library.hero.stats.totalEntries'),
        value: stats.totalEntries,
      },
      { label: t('pages.games.library.hero.stats.kbDocs'), value: stats.kbDocs },
    ],
    [t, stats]
  );

  const filtersLabels = useMemo<GamesFiltersInlineLabels>(
    () => ({
      search: {
        placeholder: t('pages.games.library.filters.search.placeholder'),
        ariaLabel: t('pages.games.library.filters.search.ariaLabel'),
        clearAriaLabel: t('pages.games.library.filters.search.clearAriaLabel'),
      },
      status: {
        label: t('pages.games.library.filters.status.label'),
        options: {
          all: t('pages.games.library.filters.status.all'),
          owned: t('pages.games.library.filters.status.owned'),
          wishlist: t('pages.games.library.filters.status.wishlist'),
          played: t('pages.games.library.filters.status.played'),
        },
      },
      sort: {
        label: t('pages.games.library.filters.sort.label'),
        options: {
          'last-played': t('pages.games.library.filters.sort.lastPlayed'),
          rating: t('pages.games.library.filters.sort.rating'),
          title: t('pages.games.library.filters.sort.title'),
          year: t('pages.games.library.filters.sort.year'),
        },
      },
      view: {
        label: t('pages.games.library.filters.view.label'),
        options: {
          grid: t('pages.games.library.filters.view.grid'),
          list: t('pages.games.library.filters.view.list'),
        },
      },
      resultCount: (count: number) => t('pages.games.library.filters.resultCount', { count }),
    }),
    [t]
  );

  const emptyLabels = useMemo<GamesEmptyStateLabels>(
    () => ({
      empty: {
        title: t('pages.games.library.empty.library.title'),
        subtitle: t('pages.games.library.empty.library.subtitle'),
        cta: t('pages.games.library.empty.library.cta'),
      },
      filteredEmpty: {
        title: t('pages.games.library.empty.filteredEmpty.title'),
        subtitle: t('pages.games.library.empty.filteredEmpty.subtitle'),
        cta: t('pages.games.library.empty.filteredEmpty.cta'),
      },
      error: {
        title: t('pages.games.library.empty.error.title'),
        subtitle: t('pages.games.library.empty.error.subtitle'),
        cta: t('pages.games.library.empty.error.cta'),
      },
    }),
    [t]
  );

  const realKind: SurfaceKind = useMemo(() => {
    if (libraryQuery.isLoading && fixtureItems == null) return 'loading';
    if (libraryQuery.isError) return 'error';
    if (items.length === 0) return 'empty';
    if (filtered.length === 0) return 'filtered-empty';
    return 'default';
  }, [libraryQuery.isLoading, libraryQuery.isError, fixtureItems, items.length, filtered.length]);

  const effectiveKind: SurfaceKind = stateOverride ?? realKind;

  const handleAddGame = useCallback(() => {
    router.push('/games/new');
  }, [router]);

  const handleRetry = useCallback(() => {
    void libraryQuery.refetch?.();
  }, [libraryQuery]);

  const handleClearFilters = useCallback(() => {
    setQuery('');
    setStatus('all');
    if (stateOverride != null) {
      const tab = searchParams.get('tab');
      const search = tab ? `?tab=${tab}` : '';
      router.push(`${pathname}${search}`);
    }
  }, [stateOverride, searchParams, router, pathname]);

  return (
    <div data-slot="games-library-view" className="flex flex-col gap-6 pb-24">
      <GamesHero labels={heroLabels} stats={heroStats} onAddGame={handleAddGame} />
      <GamesFiltersInline
        labels={filtersLabels}
        query={query}
        onQueryChange={setQuery}
        status={status}
        onStatusChange={setStatus}
        sort={sort}
        onSortChange={setSort}
        view={view}
        onViewChange={setView}
        resultCount={filtered.length}
      />
      {effectiveKind === 'default' ? (
        <GamesResultsGrid entries={filtered} view={view} />
      ) : (
        <GamesEmptyState
          kind={effectiveKind}
          labels={emptyLabels}
          onAddGame={handleAddGame}
          onClearFilters={handleClearFilters}
          onRetry={handleRetry}
        />
      )}
    </div>
  );
}
