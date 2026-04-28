/**
 * /shared-games — client body (V2, Wave A.3b, Issue #596).
 *
 * Owns:
 *   - i18n resolution (one `useTranslation()` call → all labels resolved upfront)
 *   - URL hash state for `q`, `chips`, `genre`, `sort` (deep-link friendly)
 *   - Debounced search query (300ms) before hitting React Query
 *   - SSR seed → React Query → 5-state grid surface
 *
 * Mockup parity: `admin-mockups/design_files/sp3-shared-games.jsx`.
 * Spec: `docs/superpowers/specs/2026-04-28-v2-migration-wave-a-3b-shared-games-fe.md` §3.4.
 */

'use client';

import { useMemo, type JSX } from 'react';

import {
  ContributorsSidebar,
  EmptyState,
  ErrorState,
  SharedGamesFilters,
  SharedGamesGrid,
  SharedGamesHero,
  type ContributorsSidebarItem,
  type SharedGamesFiltersChipDef,
  type SharedGamesFiltersOption,
  type SharedGamesGridGame,
  type SharedGamesGridState,
} from '@/components/ui/v2/shared-games';
import { useDebounce } from '@/hooks/useDebounce';
import { useSharedGames } from '@/hooks/useSharedGames';
import { useTranslation } from '@/hooks/useTranslation';
import { useUrlHashState } from '@/hooks/useUrlHashState';
import {
  type GameCategoryV2,
  type PagedSharedGamesV2,
  type TopContributor,
} from '@/lib/api/shared-games';
import {
  buildCategoryNameMap,
  FILTER_CHIPS,
  GENRES,
  genreKeyToCategoryIds,
  SORT_OPTIONS,
  type ChipKey,
  type SortKey,
} from '@/lib/shared-games/filters';

const SEARCH_DEBOUNCE_MS = 300;
const VALID_CHIP_KEYS: ReadonlySet<ChipKey> = new Set([
  'with-toolkit',
  'with-agent',
  'top-rated',
  'new',
]);
const VALID_SORT_KEYS: ReadonlySet<SortKey> = new Set(['rating', 'contrib', 'new', 'title']);

export interface SharedGamesPageClientProps {
  readonly initial: PagedSharedGamesV2 | null;
  readonly contributors: readonly TopContributor[];
  readonly categories: readonly GameCategoryV2[];
}

function chipsSerialize(chips: readonly ChipKey[]): string | null {
  return chips.length > 0 ? chips.join(',') : null;
}

function chipsDeserialize(raw: string): readonly ChipKey[] {
  if (!raw) return [];
  return raw
    .split(',')
    .map(c => c.trim())
    .filter((c): c is ChipKey => VALID_CHIP_KEYS.has(c as ChipKey));
}

function sortDeserialize(raw: string): SortKey {
  return VALID_SORT_KEYS.has(raw as SortKey) ? (raw as SortKey) : 'rating';
}

function sortSerialize(value: SortKey): string | null {
  return value === 'rating' ? null : value;
}

function genreSerialize(value: string): string | null {
  return value === 'all' ? null : value;
}

export function SharedGamesPageClient({
  initial,
  contributors,
  categories,
}: SharedGamesPageClientProps): JSX.Element {
  const { t, formatMessage } = useTranslation();

  // URL hash state — deep-link friendly, SSR-safe (defaults on first paint).
  const [query, setQuery] = useUrlHashState<string>('q', '');
  const [chips, setChips] = useUrlHashState<readonly ChipKey[]>('chips', [], {
    serialize: chipsSerialize,
    deserialize: chipsDeserialize,
  });
  const [genre, setGenre] = useUrlHashState<string>('genre', 'all', {
    serialize: genreSerialize,
  });
  const [sort, setSort] = useUrlHashState<SortKey>('sort', 'rating', {
    serialize: sortSerialize,
    deserialize: sortDeserialize,
  });

  const debouncedQuery = useDebounce(query, SEARCH_DEBOUNCE_MS);

  const nameToId = useMemo(() => buildCategoryNameMap(categories), [categories]);
  const categoryIds = useMemo(() => genreKeyToCategoryIds(genre, nameToId), [genre, nameToId]);
  const activeChips = useMemo<ReadonlySet<ChipKey>>(() => new Set(chips), [chips]);

  const { data, isLoading, isError, refetch } = useSharedGames({
    query: debouncedQuery,
    chips,
    categoryIds,
    sort,
    initialData: initial ?? undefined,
  });

  const hasActiveFilters = debouncedQuery.length > 0 || chips.length > 0 || genre !== 'all';

  const handleChipToggle = (key: ChipKey): void => {
    setChips(activeChips.has(key) ? chips.filter(c => c !== key) : [...chips, key]);
  };

  const handleResetFilters = (): void => {
    setQuery('');
    setChips([]);
    setGenre('all');
  };

  // --- Resolve labels (single useMemo to keep child surfaces pure-controlled) ---

  const filterChips = useMemo<readonly SharedGamesFiltersChipDef[]>(
    () => FILTER_CHIPS.map(c => ({ key: c.key, label: t(c.i18nKey) })),
    [t]
  );

  const sortSelectOptions = useMemo<readonly SharedGamesFiltersOption[]>(
    () => SORT_OPTIONS.map(o => ({ value: o.key, label: t(o.i18nKey) })),
    [t]
  );

  const genreSelectOptions = useMemo<readonly SharedGamesFiltersOption[]>(
    () => GENRES.map(g => ({ value: g.key, label: t(g.i18nKey) })),
    [t]
  );

  const filtersLabels = useMemo(
    () => ({
      searchPlaceholder: t('pages.sharedGames.filters.searchPlaceholder'),
      searchAriaLabel: t('pages.sharedGames.filters.searchAriaLabel'),
      clearSearchAriaLabel: t('pages.sharedGames.filters.clearSearchAriaLabel'),
      genreSelectAriaLabel: t('pages.sharedGames.filters.genreAriaLabel'),
      sortSelectAriaLabel: t('pages.sharedGames.filters.sortAriaLabel'),
      resultCount: (shown: number, total: number): string =>
        formatMessage({ id: 'pages.sharedGames.filters.resultCounter' }, { shown, total }),
      totalCount: (total: number): string =>
        formatMessage({ id: 'pages.sharedGames.filters.totalCounter' }, { total }),
    }),
    [t, formatMessage]
  );

  const cardLabels = useMemo(
    () => ({
      ratingAriaLabel: t('pages.sharedGames.card.ratingAriaLabel'),
      toolkitLabel: t('pages.sharedGames.card.toolkitLabel'),
      agentLabel: t('pages.sharedGames.card.agentLabel'),
      newWeekAriaLabel: (count: number): string =>
        formatMessage({ id: 'pages.sharedGames.card.newWeekAriaLabel' }, { count }),
    }),
    [t, formatMessage]
  );

  const sidebarLabels = useMemo(
    () => ({
      title: t('pages.sharedGames.sidebar.title'),
      emptyTitle: t('pages.sharedGames.sidebar.emptyTitle'),
      toolkitsLabel: t('pages.sharedGames.sidebar.toolkitsLabel'),
      agentsLabel: t('pages.sharedGames.sidebar.agentsLabel'),
      statsHeading: t('pages.sharedGames.sidebar.statsHeading'),
      meta: (sessions: number, wins: number): string =>
        formatMessage({ id: 'pages.sharedGames.sidebar.meta' }, { sessions, wins }),
      rankAriaLabel: (rank: number): string =>
        formatMessage({ id: 'pages.sharedGames.sidebar.rankAriaLabel' }, { rank }),
    }),
    [t, formatMessage]
  );

  // --- Derive display state ---

  const games = useMemo<readonly SharedGamesGridGame[]>(() => {
    const items = data?.items ?? [];
    return items.map(g => ({
      id: g.id,
      title: g.title,
      coverUrl: g.imageUrl && g.imageUrl.length > 0 ? g.imageUrl : null,
      year: g.yearPublished > 0 ? g.yearPublished : null,
      // Backend `averageRating` is on a 0..10 scale (BGG-style). Convert to 0..5.
      rating: g.averageRating != null ? g.averageRating / 2 : 0,
      toolkitsCount: g.toolkitsCount,
      agentsCount: g.agentsCount,
      kbsCount: g.kbsCount,
      newThisWeekCount: g.newThisWeekCount,
    }));
  }, [data]);

  const total = data?.total ?? games.length;
  const shown = games.length;

  const gridState: SharedGamesGridState = (() => {
    if (isError) return 'error';
    if (isLoading && shown === 0) return 'loading';
    if (shown === 0 && debouncedQuery.length > 0) return 'empty-search';
    if (shown === 0 && hasActiveFilters) return 'filtered-empty';
    if (shown === 0) return 'empty-search';
    return 'default';
  })();

  const sidebarItems = useMemo<readonly ContributorsSidebarItem[]>(
    () =>
      contributors.map(c => ({
        userId: c.userId,
        displayName: c.displayName,
        avatarUrl: c.avatarUrl,
        totalSessions: c.totalSessions,
        totalWins: c.totalWins,
      })),
    [contributors]
  );

  const toolkitsTotal = useMemo(
    () => (data?.items ?? []).reduce((sum, g) => sum + g.toolkitsCount, 0),
    [data]
  );
  const agentsTotal = useMemo(
    () => (data?.items ?? []).reduce((sum, g) => sum + g.agentsCount, 0),
    [data]
  );

  const errorNode = (
    <ErrorState
      title={t('pages.sharedGames.states.error.title')}
      description={t('pages.sharedGames.states.error.description')}
      retryLabel={t('pages.sharedGames.states.error.retryLabel')}
      onRetry={() => {
        void refetch();
      }}
    />
  );

  const emptyNode =
    gridState === 'empty-search' ? (
      <EmptyState
        kind="empty-search"
        title={t('pages.sharedGames.states.emptySearch.title')}
        description={t('pages.sharedGames.states.emptySearch.description')}
        resetLabel={t('pages.sharedGames.states.emptySearch.resetLabel')}
        onReset={handleResetFilters}
      />
    ) : (
      <EmptyState
        kind="filtered-empty"
        title={t('pages.sharedGames.states.filteredEmpty.title')}
        description={t('pages.sharedGames.states.filteredEmpty.description')}
        resetLabel={t('pages.sharedGames.states.filteredEmpty.resetLabel')}
        onReset={handleResetFilters}
      />
    );

  return (
    <main
      data-testid="shared-games-page"
      className="min-h-screen bg-background"
      style={{
        backgroundImage:
          'radial-gradient(120% 80% at 80% 0%, hsl(var(--c-game) / .07), transparent 60%), radial-gradient(80% 70% at 0% 100%, hsl(var(--c-toolkit) / .06), transparent 70%)',
      }}
    >
      <div className="mx-auto max-w-[1280px] px-0 sm:px-4 lg:px-8">
        <SharedGamesHero
          badgeLabel={t('pages.sharedGames.hero.badge')}
          headingLead={t('pages.sharedGames.hero.headingLead')}
          headingHighlight={t('pages.sharedGames.hero.headingHighlight')}
          headingTail={t('pages.sharedGames.hero.headingTail')}
          subtitle={t('pages.sharedGames.hero.subtitle')}
        />

        <SharedGamesFilters
          searchValue={query}
          onSearchChange={setQuery}
          chips={filterChips}
          activeChips={activeChips}
          onChipToggle={handleChipToggle}
          genreOptions={genreSelectOptions}
          genreValue={genre}
          onGenreChange={setGenre}
          sortOptions={sortSelectOptions}
          sortValue={sort}
          onSortChange={setSort}
          shown={shown}
          total={total}
          hasActiveFilters={hasActiveFilters}
          labels={filtersLabels}
        />

        <div className="grid gap-6 px-4 py-6 sm:px-0 lg:grid-cols-[1fr_280px] lg:gap-8 lg:py-8">
          <SharedGamesGrid
            state={gridState}
            games={games}
            cardLabels={cardLabels}
            emptyNode={emptyNode}
            errorNode={errorNode}
          />

          <div className="hidden lg:block">
            <ContributorsSidebar
              contributors={sidebarItems}
              toolkitsTotal={toolkitsTotal}
              agentsTotal={agentsTotal}
              labels={sidebarLabels}
            />
          </div>
        </div>
      </div>
    </main>
  );
}
