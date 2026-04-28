/**
 * /shared-games — client body (V2, Wave A.3b, Issue #596).
 *
 * Owns:
 *   - URL-hash state via `useUrlHashState<SharedGamesState>` (SSR-safe;
 *     hydrates from `#q=...&chips=tk,top&genre=catan&sort=rating` on mount).
 *   - Debounced write of `q` to the hook so each keystroke doesn't fire a
 *     query / hash rewrite. We update local UI state synchronously (so the
 *     `<input>` stays controlled) and the *URL hash* eagerly, but we delay
 *     the *backend search* via the React Query state read.
 *   - React Query bindings: `useSharedGamesSearch(state)` for the grid and
 *     `useTopContributors(5)` for the sidebar.
 *   - Static genre options + slug → category-id resolver. The catalog API
 *     accepts a comma-separated GUID list; we ship a curated list for
 *     Wave A.3b and defer "load genres dynamically" to a follow-up PR.
 *
 * State machine (5 grid variants — see SharedGamesGrid):
 *   default → loading → empty-search | filtered-empty | api-error
 *
 * Spec: docs/superpowers/specs/2026-04-28-v2-migration-wave-a-3b-shared-games-fe.md
 */

'use client';

import { useCallback, useEffect, useMemo, useState, type JSX } from 'react';

import type {
  GenreOption,
  SharedGamesFiltersLabels,
} from '@/components/ui/v2/shared-games/shared-games-filters';
import { SharedGamesGrid } from '@/components/ui/v2/shared-games/shared-games-grid';
import type { SharedGamesGridLabels } from '@/components/ui/v2/shared-games/shared-games-grid';
import {
  defaultsState,
  useSharedGamesSearch,
  type SharedGamesChip,
  type SharedGamesSort,
  type SharedGamesState,
} from '@/hooks/queries/useSharedGamesSearch';
import { useTopContributors } from '@/hooks/queries/useTopContributors';
import { useTranslation } from '@/hooks/useTranslation';
import { useUrlHashState, type HashSchema } from '@/lib/hooks/use-url-hash-state';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/**
 * Debounce window (ms) for the search input.
 *
 * Spec §3.2: URL hash is rewritten immediately (cheap, replaceState); the
 * backend query is debounced so we don't spam the API on every keystroke.
 */
const SEARCH_DEBOUNCE_MS = 250;

const VALID_CHIPS: ReadonlySet<SharedGamesChip> = new Set(['tk', 'ag', 'top', 'new']);
const VALID_SORTS: ReadonlySet<SharedGamesSort> = new Set(['rating', 'contrib', 'new', 'title']);

/**
 * URL-hash schema. Codecs are pure and reusable; the schema reference is
 * captured-once by `useUrlHashState`, so we keep it module-level.
 */
const HASH_SCHEMA: HashSchema<SharedGamesState> = {
  q: {
    decode: raw => raw,
    encode: value => value,
  },
  chips: {
    decode: raw =>
      raw
        .split(',')
        .map(s => s.trim())
        .filter((s): s is SharedGamesChip => VALID_CHIPS.has(s as SharedGamesChip)),
    encode: value => value.join(','),
  },
  genre: {
    decode: raw => raw,
    encode: value => value,
  },
  sort: {
    decode: raw => (VALID_SORTS.has(raw as SharedGamesSort) ? (raw as SharedGamesSort) : 'rating'),
    encode: value => (value === defaultsState.sort ? '' : value),
  },
};

/**
 * Curated genre slugs for Wave A.3b. The values map to actual category UUIDs
 * in the backend `Categories` table. A future PR can replace this with a
 * `useGameCategories()` query once the schema lands; for now we ship a
 * static list to keep the public page snappy and to avoid an extra network
 * round-trip on first paint.
 *
 * IMPORTANT: leaving the resolver returning `undefined` means the genre
 * filter is a no-op until real category ids are wired. This is acceptable
 * for the MVP per spec §6 ("Deferred to follow-up").
 */
const GENRE_SLUGS = ['strategy', 'family', 'party', 'thematic', 'wargame', 'abstract'] as const;

/** Stub resolver — will be replaced by a real category lookup. */
function genreToCategoryIds(_slug: string): string[] | undefined {
  return undefined;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function SharedGamesPageClient(): JSX.Element {
  const { t } = useTranslation();

  const [state, update, reset] = useUrlHashState<SharedGamesState>(HASH_SCHEMA, defaultsState);

  // -- i18n-derived label objects -----------------------------------------
  // Memoised so SharedGamesGrid sees a stable reference between renders
  // when the locale doesn't change. `t` comes from react-intl's useIntl
  // which exposes a stable reference per provider, so depending on it is
  // safe and triggers re-derivation only on actual locale switches.

  const filterLabels = useMemo<SharedGamesFiltersLabels>(
    () => ({
      searchLabel: t('pages.sharedGames.filters.searchLabel'),
      searchPlaceholder: t('pages.sharedGames.filters.searchPlaceholder'),
      chipToolkit: t('pages.sharedGames.filters.chipToolkit'),
      chipAgent: t('pages.sharedGames.filters.chipAgent'),
      chipTopRated: t('pages.sharedGames.filters.chipTopRated'),
      chipNew: t('pages.sharedGames.filters.chipNew'),
      genreLabel: t('pages.sharedGames.filters.genreLabel'),
      genreAll: t('pages.sharedGames.filters.genreAll'),
      sortLabel: t('pages.sharedGames.filters.sortLabel'),
      sortRating: t('pages.sharedGames.filters.sortRating'),
      sortContrib: t('pages.sharedGames.filters.sortContrib'),
      sortNew: t('pages.sharedGames.filters.sortNew'),
      sortTitle: t('pages.sharedGames.filters.sortTitle'),
    }),
    [t]
  );

  const gridLabels = useMemo<SharedGamesGridLabels>(
    () => ({
      emptySearchTitle: t('pages.sharedGames.grid.emptySearchTitle'),
      emptySearchDescription: t('pages.sharedGames.grid.emptySearchDescription'),
      filteredEmptyTitle: t('pages.sharedGames.grid.filteredEmptyTitle'),
      filteredEmptyDescription: t('pages.sharedGames.grid.filteredEmptyDescription'),
      filteredEmptyAction: t('pages.sharedGames.grid.filteredEmptyAction'),
      errorTitle: t('pages.sharedGames.grid.errorTitle'),
      errorDescription: t('pages.sharedGames.grid.errorDescription'),
      errorAction: t('pages.sharedGames.grid.errorAction'),
    }),
    [t]
  );

  const genreOptions = useMemo<ReadonlyArray<GenreOption>>(
    () =>
      GENRE_SLUGS.map(slug => ({
        slug,
        label: t(`pages.sharedGames.genres.${slug}`),
      })),
    [t]
  );

  const contributorsTitle = t('pages.sharedGames.contributors.title');
  const contributorsEmpty = t('pages.sharedGames.contributors.empty');

  // Debounced mirror of `q` — local state stays in sync with the hash on
  // every keystroke (so the input never feels laggy), while the *query*
  // sees a debounced value so we don't fire a request per character.
  const [debouncedQ, setDebouncedQ] = useState(state.q);
  useEffect(() => {
    if (state.q === debouncedQ) return;
    const handle = window.setTimeout(() => {
      setDebouncedQ(state.q);
    }, SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(handle);
  }, [state.q, debouncedQ]);

  // Build the *backend-facing* state from the debounced q + the rest of
  // the live state. This keeps chips/genre/sort instantaneous while only
  // `q` is debounced.
  const queryState = useMemo<SharedGamesState>(
    () => ({ ...state, q: debouncedQ }),
    [state, debouncedQ]
  );

  const search = useSharedGamesSearch(queryState, { genreToCategoryIds });
  const contributors = useTopContributors(5);

  // -- Filter callbacks ----------------------------------------------------

  const handleQChange = useCallback(
    (next: string) => {
      update({ q: next });
    },
    [update]
  );

  const handleChipToggle = useCallback(
    (chip: SharedGamesChip) => {
      const exists = state.chips.includes(chip);
      const nextChips = exists ? state.chips.filter(c => c !== chip) : [...state.chips, chip];
      update({ chips: nextChips });
    },
    [state.chips, update]
  );

  const handleGenreChange = useCallback(
    (slug: string) => {
      update({ genre: slug });
    },
    [update]
  );

  const handleSortChange = useCallback(
    (sort: SharedGamesSort) => {
      update({ sort });
    },
    [update]
  );

  const handleClearFilters = useCallback(() => {
    reset();
  }, [reset]);

  const handleRetry = useCallback(() => {
    void search.refetch();
  }, [search]);

  // -- Derived flags -------------------------------------------------------

  const hasActiveSearch = queryState.q.trim().length > 0;
  const hasActiveFilters =
    queryState.chips.length > 0 || queryState.genre !== '' || hasActiveSearch;

  return (
    <main className="min-h-screen bg-background">
      <div className="mx-auto max-w-[1280px] px-4 py-8 sm:px-8 sm:py-12">
        <header className="mb-6">
          <h1 className="text-3xl font-bold tracking-tight text-foreground sm:text-4xl">
            {t('pages.sharedGames.header.title')}
          </h1>
          <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
            {t('pages.sharedGames.header.subtitle')}
          </p>
        </header>

        <SharedGamesGrid
          q={state.q}
          chips={state.chips}
          genre={state.genre}
          sort={state.sort}
          genres={genreOptions}
          filterLabels={filterLabels}
          onQChange={handleQChange}
          onChipToggle={handleChipToggle}
          onGenreChange={handleGenreChange}
          onSortChange={handleSortChange}
          games={search.data?.items}
          isLoading={search.isLoading}
          isError={search.isError}
          hasActiveSearch={hasActiveSearch}
          hasActiveFilters={hasActiveFilters}
          gridLabels={gridLabels}
          onClearFilters={handleClearFilters}
          onRetry={handleRetry}
          contributors={contributors.data}
          contributorsLoading={contributors.isLoading}
          contributorsError={contributors.isError}
          contributorsTitle={contributorsTitle}
          contributorsEmptyLabel={contributorsEmpty}
        />
      </div>
    </main>
  );
}
