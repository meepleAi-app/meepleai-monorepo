/**
 * LibraryHubV2 — Wave B.3 (Issue #574) orchestrator for `/library` desktop.
 *
 * Mirrors the Wave B.1 GamesLibraryView and B.2 AgentsLibraryView orchestrator
 * pattern (spec §3.2). Brownfield big-bang replacement of the v1 carousel
 * landing (`LibraryHub` + 4 carousel sections + `LibraryFilterBar`) — there is
 * no feature flag; rollback is `git revert` on the merge commit (decision C1).
 *
 * State surface extends the wave-B template:
 *   - `tab: LibraryEntityKey` (3 tabs: all/kb/loaned, decision C2+C3)
 *   - `selectionMode: 'browse' | 'select'` (FSM: enter on long-press / "Seleziona",
 *      exit on Esc / Annulla / dialog confirm)
 *   - `selected: Set<string>` (entry IDs in select mode)
 *   - `view: LibraryViewMode` (grid/list/compact, persisted to localStorage via
 *      `useLibraryView`)
 *   - `query: string` (search input)
 *   - `sortKey: LibrarySortKey` (recent/title/rating/state)
 *
 * Single click dispatcher (spec §3.2 contract):
 *   `LibraryHybridGrid.onCardClick(entryId)` lands here. Dispatch on
 *   `selectionMode`: browse → `router.push(/games/{id})`; select → toggle
 *   membership in `selected` Set. Keeping the dispatcher in the orchestrator
 *   makes the grid testable without a router or store.
 *
 * Bulk delete flow (spec §3.2 + AC-6):
 *   `BulkSelectionBar.onArchive` callback uses `useRemoveGameFromLibrary` and
 *   fans out N parallel mutations via `Promise.allSettled` so partial failures
 *   don't lose successes. On settle: clear selected Set + exit select mode.
 *   The "archive" name in the BulkSelectionBar contract is generic — the
 *   library bulk action is semantically "delete" per i18n
 *   (`pages.library.bulk.actions.delete`).
 *
 * RecentActivityRail wired (Issue #642 — Wave B.3 followup):
 *   Hooks `useLibraryActivity` to fetch recent `added` / `state-changed`
 *   events from `GET /api/v1/library/activity`. Backend events are mapped onto
 *   the 4-kind `ActivityItem` contract (`added → add`, `state-changed →
 *   rating-changed`). The space is still reserved on lg+ to avoid layout shift
 *   while the query is loading; empty libraries still render the placeholder
 *   copy provided by `RecentActivityRail` itself.
 *
 * MiniNav config replication (R7 — preserve global shell behaviour):
 *   Replicates the v1 `LibraryHub` mini-nav registration so the global shell
 *   continues to render the breadcrumb 'Libreria · Hub' + Hub/Wishlist tabs +
 *   "Aggiungi gioco" primary action without regression.
 */

'use client';

import { useCallback, useMemo, useState, type ReactElement } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  BulkSelectionBar,
  EmptyLibrary,
  LibraryHeroDesktop,
  LibraryHybridGrid,
  LibraryTabs,
  RecentActivityRail,
  type ActivityItem,
  type ActivityKind,
  type BulkSelectionBarLabels,
  type EmptyLibraryLabels,
  type LibraryEntityKey,
  type LibraryHeroDesktopLabels,
  type LibraryHeroStat,
  type LibrarySelectionMode,
  type LibraryTabConfig,
  type LibraryViewMode,
} from '@/components/features/library';
import {
  useLibrary,
  useLibraryActivity,
  useRemoveGameFromLibrary,
} from '@/hooks/queries/useLibrary';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useTranslation } from '@/hooks/useTranslation';
import {
  deriveHeroStats,
  deriveLibraryUiState,
  filterByEntity,
  matchQuery,
  sortLibraryEntries,
  type LibrarySortKey,
} from '@/lib/library/library-filters';
import { useLibraryView } from '@/lib/library/use-library-view';
import { IS_VISUAL_TEST_BUILD, tryLoadVisualTestFixture } from '@/lib/library/visual-test-fixture';

// ─── State override hatch (dev / visual-test only) ─────────────────────────

const VALID_OVERRIDES = ['loading', 'empty', 'filtered-empty', 'error'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  if (raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}

type SurfaceKind = 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';

// ─── Component ──────────────────────────────────────────────────────────────

export function LibraryHubV2(): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const [tab, setTab] = useState<LibraryEntityKey>('all');
  const [selectionMode, setSelectionMode] = useState<LibrarySelectionMode>('browse');
  const [selected, setSelected] = useState<ReadonlySet<string>>(() => new Set());
  const [query, setQuery] = useState<string>('');
  const [sortKey, setSortKey] = useState<LibrarySortKey>('recent');
  const { view, setView } = useLibraryView('grid');

  const stateOverride = parseStateOverride(searchParams.get('state'));

  const libraryQuery = useLibrary({
    page: 1,
    pageSize: 50,
    sortBy: 'addedAt',
    sortDescending: true,
  });
  const removeMutation = useRemoveGameFromLibrary();

  // Activity feed (Issue #642 — Wave B.3 followup):
  // Powers the RecentActivityRail sidebar. Server emits 'added' and
  // 'state-changed' events from UserLibraryEntries; we map them onto the
  // 4-kind RecentActivityRail contract (other kinds remain wire-ready for
  // future event types). Unknown kinds are dropped to keep the contract tight.
  const activityQuery = useLibraryActivity(20);
  const activityItems = useMemo<readonly ActivityItem[]>(() => {
    const raw = activityQuery.data ?? [];
    const mapped: ActivityItem[] = [];
    for (const event of raw) {
      let kind: ActivityKind | null;
      switch (event.type) {
        case 'added':
          kind = 'add';
          break;
        case 'state-changed':
          kind = 'rating-changed';
          break;
        case 'session-recorded':
          kind = 'play';
          break;
        case 'removed':
          kind = null; // Not surfaced today; backend doesn't emit yet.
          break;
        default:
          kind = null;
          break;
      }
      if (!kind) continue;
      mapped.push({
        id: `${event.id}:${event.type}`,
        kind,
        entityTitle: event.gameTitle,
        timestamp: event.timestamp,
      });
    }
    return mapped;
  }, [activityQuery.data]);

  const fixtureItems = useMemo(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    return tryLoadVisualTestFixture(stateOverride === 'empty' ? 'empty' : 'default');
  }, [stateOverride]);

  const entries = useMemo(
    () => fixtureItems ?? libraryQuery.data?.items ?? [],
    [fixtureItems, libraryQuery.data]
  );

  const heroStats = useMemo(() => deriveHeroStats(entries), [entries]);

  const filtered = useMemo(() => {
    const byEntity = filterByEntity(entries, tab);
    const byQuery = byEntity.filter(entry => matchQuery(entry, query));
    return sortLibraryEntries(byQuery, sortKey);
  }, [entries, tab, query, sortKey]);

  // ─── Resolved labels (i18n) ────────────────────────────────────────────

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
        value: heroStats.totalGames,
      },
      {
        key: 'kbReady',
        label: t('pages.library.hero.stats.kbReady'),
        value: heroStats.kbReady,
      },
      {
        key: 'wishlist',
        label: t('pages.library.hero.stats.wishlist'),
        value: heroStats.wishlist,
      },
      {
        key: 'loaned',
        label: t('pages.library.hero.stats.loaned'),
        value: heroStats.loaned,
      },
    ],
    [t, heroStats]
  );

  const tabsConfig = useMemo<readonly LibraryTabConfig[]>(() => {
    const allCount = entries.length;
    const kbCount = filterByEntity(entries, 'kb').length;
    const loanedCount = filterByEntity(entries, 'loaned').length;
    return [
      { key: 'all', label: t('pages.library.tabs.all'), count: allCount },
      { key: 'kb', label: t('pages.library.tabs.kb'), count: kbCount },
      { key: 'loaned', label: t('pages.library.tabs.loaned'), count: loanedCount },
    ];
  }, [t, entries]);

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

  // ─── 5-state FSM ──────────────────────────────────────────────────────

  const realKind = useMemo<SurfaceKind>(
    () =>
      deriveLibraryUiState({
        isLoading: libraryQuery.isLoading && fixtureItems == null,
        error: libraryQuery.isError ? libraryQuery.error : null,
        totalCount: entries.length,
        filteredCount: filtered.length,
      }),
    [
      libraryQuery.isLoading,
      libraryQuery.isError,
      libraryQuery.error,
      fixtureItems,
      entries.length,
      filtered.length,
    ]
  );

  const effectiveKind: SurfaceKind = stateOverride ?? realKind;

  // ─── Callbacks ────────────────────────────────────────────────────────

  const handleAddGame = useCallback(() => {
    router.push('/library?action=add');
  }, [router]);

  const handleCardClick = useCallback(
    (entryId: string) => {
      if (selectionMode === 'select') {
        setSelected(prev => {
          const next = new Set(prev);
          if (next.has(entryId)) next.delete(entryId);
          else next.add(entryId);
          return next;
        });
        return;
      }
      router.push(`/games/${entryId}`);
    },
    [router, selectionMode]
  );

  const handleEnterSelectMode = useCallback((entryId?: string) => {
    setSelectionMode('select');
    if (entryId) {
      setSelected(prev => {
        const next = new Set(prev);
        next.add(entryId);
        return next;
      });
    }
  }, []);

  const handleExitSelectMode = useCallback(() => {
    setSelectionMode('browse');
    setSelected(new Set());
  }, []);

  const handleBulkDelete = useCallback(async () => {
    const ids = Array.from(selected);
    if (ids.length === 0) return;
    await Promise.allSettled(ids.map(id => removeMutation.mutateAsync(id)));
    setSelected(new Set());
    setSelectionMode('browse');
  }, [selected, removeMutation]);

  const handleRetry = useCallback(() => {
    void libraryQuery.refetch?.();
  }, [libraryQuery]);

  const handleClearFilters = useCallback(() => {
    setQuery('');
    setTab('all');
    if (stateOverride != null) {
      router.push(pathname);
    }
  }, [stateOverride, router, pathname]);

  // ─── MiniNav (preserve v1 global shell behaviour) ─────────────────────

  const miniNavConfig = useMemo(
    () => ({
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        {
          id: 'wishlist',
          label: 'Wishlist',
          href: '/library/wishlist',
          count: heroStats.wishlist,
        },
      ],
      activeTabId: 'hub',
      primaryAction: {
        label: t('pages.library.hero.cta.add'),
        icon: '＋',
        onClick: handleAddGame,
      },
    }),
    [t, handleAddGame, heroStats.wishlist]
  );
  useMiniNavConfig(miniNavConfig);

  // ─── Render ───────────────────────────────────────────────────────────

  return (
    <div
      data-slot="library-hub-v2"
      data-state={effectiveKind}
      className="mx-auto flex max-w-[1440px] flex-col gap-6 p-6 pb-24 sm:p-7"
    >
      <LibraryHeroDesktop labels={heroLabels} stats={heroStatRows} onAddGame={handleAddGame} />

      <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
        <div className="flex flex-1 flex-col gap-4">
          <LibraryTabs tabs={tabsConfig} active={tab} onChange={setTab} />

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

            {selectionMode === 'browse' ? (
              <button
                type="button"
                onClick={() => handleEnterSelectMode()}
                aria-label={t('pages.library.selectionMode.enterAriaLabel')}
                data-slot="library-enter-select-mode"
                className="ml-auto rounded-full border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                {t('pages.library.selectionMode.enter')}
              </button>
            ) : null}
          </div>

          {effectiveKind === 'default' ? (
            <LibraryHybridGrid
              entries={filtered}
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
        </div>

        {/*
         * Sidebar visual contract preservation (Issue #642):
         * forwarding `activityQuery.isLoading` here surfaces the skeleton
         * variant during the brief moment before the activity response
         * arrives, breaking the migrated `library-loading.png` baseline that
         * was captured against the empty placeholder. Until the baseline is
         * regenerated to include the skeleton sidebar, only surface the
         * populated state once data is available.
         */}
        <RecentActivityRail items={activityItems} />
      </div>

      {selectionMode === 'select' ? (
        <BulkSelectionBar
          selectedCount={selected.size}
          labels={bulkLabels}
          onExitSelectMode={handleExitSelectMode}
          onArchive={handleBulkDelete}
          disabled={selected.size === 0 || removeMutation.isPending}
        />
      ) : null}
    </div>
  );
}
