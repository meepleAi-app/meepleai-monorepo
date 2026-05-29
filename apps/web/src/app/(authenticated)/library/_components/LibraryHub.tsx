/**
 * LibraryHub — Phase 2a (#1605): hybrid multi-entity hub orchestrator.
 *
 * Migrated from the Wave B.3 games-only view. Tab state is `HybridHubTab`
 * (6 tabs); the 3 ready sources (games/sessions/chat) are orchestrated by
 * `useHybridHubItems` and merged/filtered/sorted by `deriveHybridItems`.
 * Agents + KB are stubbed `[]` until BE-2 #1589 / BE-1 #1588 (Phase 2b).
 *
 * Game-state filters (ex-`loaned`/`kb` tabs) live in the `CrossEntityFilters`
 * STATO chip, applied to the games source before merge. Selection mode is
 * game-scoped: forced to `browse` outside the `games` tab. FSM degrades on
 * partial failure: `error` only when all ready sources fail.
 */

'use client';

import { useCallback, useEffect, useMemo, useState, type ReactElement } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  GamesEmptyState,
  GamesFiltersInline,
  GamesResultsGrid,
  type GamesEmptyKind,
  type GamesEmptyStateLabels,
  type GamesFiltersInlineLabels,
  type GamesResultsView,
  type GamesSortKey,
  type GamesStatusKey,
  type GamesViewKey,
} from '@/components/features/games';
import {
  AdvancedFiltersDrawer,
  BulkSelectionBar,
  CrossEntityFilters,
  EmptyLibrary,
  LibraryHeroDesktop,
  LibraryHybridGrid,
  LibraryTabs,
  RecentActivityRail,
  type ActivityItem,
  type BulkSelectionBarLabels,
  type EmptyLibraryLabels,
  type GameStateFilter,
  type LibraryFilters,
  type LibraryHeroDesktopLabels,
  type LibraryHeroStat,
  type LibrarySelectionMode,
  type LibraryTabConfig,
  type LibraryViewMode,
} from '@/components/features/library';
import { useHybridHubItems } from '@/hooks/queries/useHybridHubItems';
import { useLibrary, useRemoveGameFromLibrary } from '@/hooks/queries/useLibrary';
import { useActivityFeed } from '@/hooks/useActivityFeed';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useTranslation } from '@/hooks/useTranslation';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { deriveGamesTabEntries } from '@/lib/library/games-tab-filters';
import {
  deriveHybridItems,
  type HybridHubSources,
  type HybridHubTab,
} from '@/lib/library/hybrid-hub.derive';
import type { HybridHubEntity, HybridHubItem } from '@/lib/library/hybrid-hub.types';
import type { LibrarySortKey } from '@/lib/library/library-filters';
import { useLibraryView } from '@/lib/library/use-library-view';
import { IS_VISUAL_TEST_BUILD } from '@/lib/library/visual-test-fixture';

// ─── State override hatch (dev / visual-test only) ─────────────────────────

const VALID_OVERRIDES = ['loading', 'empty', 'filtered-empty', 'error'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];
const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;
function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED || raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}
type SurfaceKind = 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';

const HUB_TABS: readonly HybridHubTab[] = ['all', 'games', 'agents', 'kb', 'sessions', 'chat'];

export function LibraryHub(): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const [tab, setTab] = useState<HybridHubTab>('all');
  const [selectionMode, setSelectionMode] = useState<LibrarySelectionMode>('browse');
  const [selected, setSelected] = useState<ReadonlySet<string>>(() => new Set());
  const [query, setQuery] = useState('');
  const [sortKey, setSortKey] = useState<LibrarySortKey>('recent');
  const [gameStateFilter, setGameStateFilter] = useState<GameStateFilter>({
    states: [],
    withKb: false,
  });
  const { view, setView } = useLibraryView('grid');

  // ─── games-tab local state (#1566) ───
  const [gamesStatus, setGamesStatus] = useState<GamesStatusKey>('all');
  const [gamesSort, setGamesSort] = useState<GamesSortKey>('last-played');
  const [gamesQuery, setGamesQuery] = useState('');
  const [gamesView, setGamesView] = useState<GamesViewKey>('grid');

  // Phase 3b #1593: AdvancedFiltersDrawer state
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [activeFilters, setActiveFilters] = useState<LibraryFilters>({ scope: 'game' });

  const drawerEntityScope = useMemo<HybridHubEntity>(() => {
    switch (tab) {
      case 'games':
        return 'game';
      case 'agents':
        return 'agent';
      case 'kb':
        return 'kb';
      case 'sessions':
        return 'session';
      case 'chat':
        return 'chat';
      case 'all':
      default:
        return 'game';
    }
  }, [tab]);

  // Reset filters to the new scope's empty variant when the tab changes.
  useEffect(() => {
    setActiveFilters({ scope: drawerEntityScope } as LibraryFilters);
  }, [drawerEntityScope]);

  // Count non-empty filter fields (excluding the `scope` discriminant) for the chip badge.
  const activeFiltersCount = useMemo(() => {
    let count = 0;
    for (const [key, value] of Object.entries(activeFilters)) {
      if (key === 'scope') continue;
      if (value === undefined || value === null) continue;
      if (Array.isArray(value) && value.length === 0) continue;
      if (value === false) continue;
      count++;
    }
    return count;
  }, [activeFilters]);

  const stateOverride = parseStateOverride(searchParams.get('state'));

  const hub = useHybridHubItems();

  // #1566: dedicated library fetch for the games tab. Identical key to the call
  // inside useHybridHubItems (useHybridHubItems.ts:41-46) → TanStack dedups to a
  // single network request; this is 1 fetch, 2 references (not a double fetch).
  const libraryQuery = useLibrary({
    page: 1,
    pageSize: 50,
    sortBy: 'addedAt',
    sortDescending: true,
  });
  const gameEntries = useMemo<readonly UserLibraryEntry[]>(
    () => libraryQuery.data?.items ?? [],
    [libraryQuery.data]
  );
  const gamesFiltered = useMemo<readonly UserLibraryEntry[]>(
    () => deriveGamesTabEntries(gameEntries, gamesStatus, gamesQuery, gamesSort),
    [gameEntries, gamesStatus, gamesQuery, gamesSort]
  );
  const gamesKind = useMemo<GamesEmptyKind | 'default'>(() => {
    if (libraryQuery.isLoading) return 'loading';
    if (libraryQuery.isError) return 'error';
    if (gameEntries.length === 0) return 'empty';
    if (gamesFiltered.length === 0) return 'filtered-empty';
    return 'default';
  }, [libraryQuery.isLoading, libraryQuery.isError, gameEntries.length, gamesFiltered.length]);
  const gamesEffectiveKind: GamesEmptyKind | 'default' =
    (stateOverride as GamesEmptyKind | null) ?? gamesKind;

  const removeMutation = useRemoveGameFromLibrary();
  const activityQuery = useActivityFeed(20);

  // Selection mode is game-scoped — force browse when leaving the games tab.
  useEffect(() => {
    if (tab !== 'games' && selectionMode === 'select') {
      setSelectionMode('browse');
      setSelected(new Set());
    }
  }, [tab, selectionMode]);

  // Apply game-state filters (ex-loaned/kb tabs) to the games source before merge.
  const filteredSources = useMemo<HybridHubSources>(() => {
    const { states, withKb } = gameStateFilter;
    if (states.length === 0 && !withKb) return hub.sources;
    const games = hub.sources.games.filter(item => {
      if (item.entity !== 'game') return true;
      const stateOk = states.length === 0 || (item.state != null && states.includes(item.state));
      const kbOk = !withKb || item.hasKb === true; // Task 0 added hasKb to GameHubItem (optional)
      return stateOk && kbOk;
    });
    return { ...hub.sources, games };
  }, [hub.sources, gameStateFilter]);

  const merged = useMemo<HybridHubItem[]>(
    () => deriveHybridItems(filteredSources, tab, query, sortKey),
    [filteredSources, tab, query, sortKey]
  );

  // Hero stats: hybrid counts (games/agents/docs/chats) from pre-filter totals.
  const heroStats = useMemo(
    () => ({
      games: hub.totalCounts.games,
      agents: hub.totalCounts.agents,
      docs: hub.totalCounts.kb,
      chats: hub.totalCounts.chat,
    }),
    [hub.totalCounts]
  );

  const activityItems = useMemo<readonly ActivityItem[]>(
    () => activityQuery.data?.items ?? [],
    [activityQuery.data]
  );

  // ─── Labels ───
  const heroLabels = useMemo<LibraryHeroDesktopLabels>(
    () => ({
      title: t('pages.library.hero.title'),
      subtitle: t('pages.library.hero.subtitle'),
      ctaAdd: t('pages.library.hero.cta.add'),
    }),
    [t]
  );

  const heroStatRows = useMemo<readonly LibraryHeroStat[]>(
    () => [
      {
        key: 'totalGames',
        label: t('pages.library.hero.stats.totalGames'),
        value: heroStats.games,
      },
      { key: 'agents', label: t('pages.library.hero.stats.agents'), value: heroStats.agents },
      { key: 'docs', label: t('pages.library.hero.stats.docs'), value: heroStats.docs },
      { key: 'chats', label: t('pages.library.hero.stats.chats'), value: heroStats.chats },
    ],
    [t, heroStats]
  );

  const tabsConfig = useMemo<readonly LibraryTabConfig<HybridHubTab>[]>(() => {
    const countFor = (tk: HybridHubTab): number => {
      if (tk === 'all') return Object.values(hub.totalCounts).reduce((a, b) => a + b, 0);
      if (tk === 'games') return hub.totalCounts.games;
      if (tk === 'agents') return hub.totalCounts.agents;
      if (tk === 'kb') return hub.totalCounts.kb;
      if (tk === 'sessions') return hub.totalCounts.sessions;
      return hub.totalCounts.chat;
    };
    return HUB_TABS.map(tk => ({
      key: tk,
      label: t(`pages.library.hubTabs.${tk}`),
      count: countFor(tk),
    }));
  }, [t, hub.totalCounts]);

  const emptyLabels = useMemo<EmptyLibraryLabels>(
    () => ({
      empty: {
        title: t('pages.library.emptyState.default.title'),
        subtitle: t('pages.library.emptyState.default.subtitle'),
        cta: t('pages.library.emptyState.default.cta'),
      },
      filteredEmpty: {
        title: t('pages.library.emptyState.filteredEmpty.title'),
        subtitle: t('pages.library.emptyState.filteredEmpty.subtitle'),
        cta: t('pages.library.emptyState.filteredEmpty.cta'),
      },
      error: {
        title: t('pages.library.emptyState.error.title'),
        subtitle: t('pages.library.emptyState.error.subtitle'),
        cta: t('pages.library.emptyState.error.cta'),
      },
    }),
    [t]
  );

  const bulkLabels = useMemo<BulkSelectionBarLabels>(() => {
    const count = selected.size;
    return {
      regionLabel: t('pages.library.selectionMode.selectedCount', { count }),
      counter: t('pages.library.selectionMode.selectedCount', { count }),
      cancel: t('pages.library.selectionMode.exit'),
      archive: t('pages.library.bulk.actions.delete'),
      confirmTitle: t('pages.library.bulk.confirm.deleteTitle', { count }),
      confirmDescription: t('pages.library.bulk.confirm.deleteMessage'),
      confirmCta: t('pages.library.bulk.confirm.confirmCta'),
      cancelCta: t('pages.library.bulk.confirm.cancelCta'),
    };
  }, [t, selected]);

  const gamesFiltersLabels = useMemo<GamesFiltersInlineLabels>(
    () => ({
      search: {
        placeholder: t('pages.library.gamesTab.filters.search.placeholder'),
        ariaLabel: t('pages.library.gamesTab.filters.search.ariaLabel'),
        clearAriaLabel: t('pages.library.gamesTab.filters.search.clearAriaLabel'),
      },
      status: {
        label: t('pages.library.gamesTab.filters.status.label'),
        options: {
          all: t('pages.library.gamesTab.filters.status.options.all'),
          owned: t('pages.library.gamesTab.filters.status.options.owned'),
          wishlist: t('pages.library.gamesTab.filters.status.options.wishlist'),
          played: t('pages.library.gamesTab.filters.status.options.played'),
        },
      },
      sort: {
        label: t('pages.library.gamesTab.filters.sort.label'),
        options: {
          'last-played': t('pages.library.gamesTab.filters.sort.options.last-played'),
          rating: t('pages.library.gamesTab.filters.sort.options.rating'),
          title: t('pages.library.gamesTab.filters.sort.options.title'),
          year: t('pages.library.gamesTab.filters.sort.options.year'),
        },
      },
      view: {
        label: t('pages.library.gamesTab.filters.view.label'),
        options: {
          grid: t('pages.library.gamesTab.filters.view.options.grid'),
          list: t('pages.library.gamesTab.filters.view.options.list'),
        },
      },
      resultCount: (count: number) => t('pages.library.gamesTab.filters.resultCount', { count }),
    }),
    [t]
  );

  const gamesEmptyLabels = useMemo<GamesEmptyStateLabels>(
    () => ({
      empty: {
        title: t('pages.library.gamesTab.emptyState.empty.title'),
        subtitle: t('pages.library.gamesTab.emptyState.empty.subtitle'),
        cta: t('pages.library.gamesTab.emptyState.empty.cta'),
      },
      filteredEmpty: {
        title: t('pages.library.gamesTab.emptyState.filteredEmpty.title'),
        subtitle: t('pages.library.gamesTab.emptyState.filteredEmpty.subtitle'),
        cta: t('pages.library.gamesTab.emptyState.filteredEmpty.cta'),
      },
      error: {
        title: t('pages.library.gamesTab.emptyState.error.title'),
        subtitle: t('pages.library.gamesTab.emptyState.error.subtitle'),
        cta: t('pages.library.gamesTab.emptyState.error.cta'),
      },
    }),
    [t]
  );

  // ─── FSM (partial-failure aware) ───
  const realKind = useMemo<SurfaceKind>(() => {
    if (hub.allFailed) return 'error';
    if (hub.isLoading) return 'loading';
    const totalAll = Object.values(hub.totalCounts).reduce((a, b) => a + b, 0);
    if (totalAll === 0) return 'empty';
    if (merged.length === 0) return 'filtered-empty';
    return 'default';
  }, [hub.allFailed, hub.isLoading, hub.totalCounts, merged.length]);

  const effectiveKind: SurfaceKind = stateOverride ?? realKind;

  // ─── Callbacks ───
  const handleAddGame = useCallback(() => router.push('/library?action=add'), [router]);

  const handleCardClick = useCallback(
    (itemId: string) => {
      if (selectionMode === 'select') {
        setSelected(prev => {
          const n = new Set(prev);
          if (n.has(itemId)) n.delete(itemId);
          else n.add(itemId);
          return n;
        });
        return;
      }
      const item = merged.find(i => i.id === itemId);
      if (item) router.push(item.href);
    },
    [router, selectionMode, merged]
  );

  const handleEnterSelectMode = useCallback(
    (itemId?: string) => {
      if (tab !== 'games') return; // game-scoped guard
      setSelectionMode('select');
      if (itemId)
        setSelected(prev => {
          const n = new Set(prev);
          n.add(itemId);
          return n;
        });
    },
    [tab]
  );

  const handleExitSelectMode = useCallback(() => {
    setSelectionMode('browse');
    setSelected(new Set());
  }, []);

  // #1566: retained for re-wiring (spec §3.5) — unreachable until enter-select-mode is restored.
  const handleBulkDelete = useCallback(async () => {
    const ids = Array.from(selected);
    if (ids.length === 0) return;
    // ids are HybridHubItem ids in the games tab; the games source id IS the library entry id.
    await Promise.allSettled(ids.map(id => removeMutation.mutateAsync(id)));
    setSelected(new Set());
    setSelectionMode('browse');
  }, [selected, removeMutation]);

  const handleRetry = useCallback(() => {
    /* per-source refetch handled by TanStack; no-op surfaces retry CTA */
  }, []);

  const handleClearFilters = useCallback(() => {
    setQuery('');
    setTab('all');
    setGameStateFilter({ states: [], withKb: false });
    if (stateOverride != null) router.push(pathname);
  }, [stateOverride, router, pathname]);

  const handleGamesClearFilters = useCallback(() => {
    setGamesQuery('');
    setGamesStatus('all');
    if (stateOverride != null) router.push(pathname);
  }, [stateOverride, router, pathname]);

  // ─── MiniNav ───
  const miniNavConfig = useMemo(
    () => ({
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'wishlist', label: 'Wishlist', href: '/library/wishlist', count: 0 },
      ],
      activeTabId: 'hub',
      primaryAction: { label: t('pages.library.hero.cta.add'), icon: '＋', onClick: handleAddGame },
    }),
    [t, handleAddGame]
  );
  useMiniNavConfig(miniNavConfig);

  // ─── Render ───
  return (
    <div
      data-slot="library-hub-v2"
      data-state={effectiveKind}
      className="mx-auto flex max-w-[1440px] flex-col gap-6 p-6 pb-24 sm:p-7"
    >
      <LibraryHeroDesktop labels={heroLabels} stats={heroStatRows} onAddGame={handleAddGame} />
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
        <div className="flex flex-1 flex-col gap-4">
          <LibraryTabs<HybridHubTab> tabs={tabsConfig} active={tab} onChange={setTab} />
          {tab === 'games' ? (
            <>
              <GamesFiltersInline
                labels={gamesFiltersLabels}
                query={gamesQuery}
                onQueryChange={setGamesQuery}
                status={gamesStatus}
                onStatusChange={setGamesStatus}
                sort={gamesSort}
                onSortChange={setGamesSort}
                view={gamesView}
                onViewChange={setGamesView}
                resultCount={gamesFiltered.length}
                onMoreFilters={() => setDrawerOpen(true)}
                activeFiltersCount={activeFiltersCount}
              />
              {gamesEffectiveKind === 'default' ? (
                <GamesResultsGrid entries={gamesFiltered} view={gamesView as GamesResultsView} />
              ) : (
                <GamesEmptyState
                  kind={gamesEffectiveKind}
                  labels={gamesEmptyLabels}
                  onAddGame={handleAddGame}
                  onClearFilters={handleGamesClearFilters}
                  onRetry={handleRetry}
                />
              )}
            </>
          ) : (
            <>
              <CrossEntityFilters
                tab={tab}
                gameStateFilter={gameStateFilter}
                onGameStateFilterChange={setGameStateFilter}
                onMoreFilters={() => setDrawerOpen(true)}
                activeFiltersCount={activeFiltersCount}
              />
              <div
                data-slot="library-toolbar"
                className="flex flex-wrap items-center gap-3 rounded-2xl border border-border bg-card p-3 shadow-sm"
              >
                <input
                  type="search"
                  value={query}
                  onChange={e => setQuery(e.target.value)}
                  placeholder={t('pages.library.filters.search.placeholder')}
                  aria-label={t('pages.library.filters.search.ariaLabel')}
                  data-slot="library-search-input"
                  className="min-w-[12rem] flex-1 rounded-full border border-input bg-background px-4 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
                <label className="flex items-center gap-2 text-sm text-muted-foreground">
                  <span className="sr-only sm:not-sr-only">{t('pages.library.sort.label')}</span>
                  <select
                    value={sortKey}
                    onChange={e => setSortKey(e.target.value as LibrarySortKey)}
                    aria-label={t('pages.library.sort.ariaLabel')}
                    data-slot="library-sort-select"
                    className="rounded-full border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    <option value="recent">{t('pages.library.sort.recent')}</option>
                    <option value="title">{t('pages.library.sort.title')}</option>
                    <option value="rating">{t('pages.library.sort.rating')}</option>
                    <option value="state">{t('pages.library.sort.state')}</option>
                  </select>
                </label>
                <div
                  role="group"
                  aria-label={t('pages.library.view.ariaLabel')}
                  data-slot="library-view-toggle"
                  className="inline-flex items-center gap-1 rounded-full border border-input bg-background p-1"
                >
                  {(['grid', 'list', 'compact'] as const).map(mode => (
                    <button
                      key={mode}
                      type="button"
                      onClick={() => setView(mode)}
                      aria-pressed={view === mode}
                      data-view-mode={mode}
                      className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                        view === mode
                          ? 'bg-primary text-primary-foreground'
                          : 'text-muted-foreground hover:text-foreground'
                      }`}
                    >
                      {t(`pages.library.view.${mode}` as const)}
                    </button>
                  ))}
                </div>
                {/* #1566: the enter-select-mode button was deleted — it is not in
                    the games branch (handled by GamesFiltersInline) and not re-added
                    here. The else-branch exists only for non-games tabs. */}
              </div>
              {effectiveKind === 'default' ? (
                <LibraryHybridGrid
                  items={merged}
                  view={view as LibraryViewMode}
                  selectionMode={selectionMode}
                  selected={selected}
                  onCardClick={handleCardClick}
                  onLongPressEnter={handleEnterSelectMode}
                />
              ) : (
                <EmptyLibrary
                  kind={effectiveKind}
                  labels={emptyLabels}
                  onAddGame={handleAddGame}
                  onClearFilters={handleClearFilters}
                  onRetry={handleRetry}
                />
              )}
            </>
          )}
        </div>
        <RecentActivityRail
          items={activityItems}
          isLoading={activityQuery.isLoading}
          error={activityQuery.error}
        />
      </div>
      {tab === 'games' && selectionMode === 'select' ? (
        <BulkSelectionBar
          selectedCount={selected.size}
          labels={bulkLabels}
          onExitSelectMode={handleExitSelectMode}
          onArchive={handleBulkDelete}
          disabled={selected.size === 0 || removeMutation.isPending}
        />
      ) : null}
      <AdvancedFiltersDrawer
        open={drawerOpen}
        onOpenChange={setDrawerOpen}
        entityScope={drawerEntityScope}
        activeFilters={activeFilters}
        onApply={setActiveFilters}
        onClear={() => setActiveFilters({ scope: drawerEntityScope } as LibraryFilters)}
      />
    </div>
  );
}
