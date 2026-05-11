/**
 * PlayersLibraryView — Wave 4 D1 (Issue #682) orchestrator for `/players`.
 *
 * Tier S route — single hook, no tabs, no complex filter pipeline.
 * Mirrors Wave B.2 AgentsLibraryView pattern adapted for the players surface:
 *
 *   - `usePlayerStatistics()` TanStack hook (single call, plays-as-players
 *     anti-pattern carryover from v1 — backend redesign is a followup issue)
 *   - i18n labels resolved upfront via a single `useTranslation()` call
 *   - Search filter via local `useState<string>` (Tier S — no URL persistence)
 *   - 5-state FSM: `default | loading | empty | filtered-empty | error`
 *     via `derivePlayersUiState` pure helper
 *   - `?state=...` URL override gated by
 *     `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD`
 *   - Visual-test fixture short-circuit (`IS_VISUAL_TEST_BUILD`) for CI
 *     Next.js prod-only builds without a backend API at `:8080`
 *   - clearFilters CTA: resets `search` to `''`; when an override is active
 *     also drops `?state=` via `router.push(pathname)`
 *   - Item click: `router.push('/players/' + item.id)` — preserves v1 nav
 *
 * Loading state: inline skeleton grid (EmptyPlayers only covers 3 terminal
 * states — error / empty / filtered-empty). Mirror Wave B.1 pattern of
 * rendering a distinct skeleton shell without forking EmptyPlayers.
 */

'use client';

import { useCallback, useMemo, useState, type ReactElement } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useIntl } from 'react-intl';

import {
  EmptyPlayers,
  PlayersFiltersInline,
  PlayersHero,
  PlayersResultsGrid,
  type EmptyPlayersLabels,
  type PlayersFiltersInlineLabels,
  type PlayersHeroLabels,
  type PlayersResultsGridLabels,
} from '@/components/features/players';
import { usePlayerStatistics } from '@/hooks/queries/usePlayersFromRecords';
import { useTranslation } from '@/hooks/useTranslation';
import {
  applyPlayersFilters,
  hasPlayersFilters,
  transformStatsToItems,
  type PlayerListItem,
} from '@/lib/players/players-filters';
import { derivePlayersUiState, type PlayersUiState } from '@/lib/players/players-state';
import {
  IS_VISUAL_TEST_BUILD,
  tryLoadVisualTestFixture,
} from '@/lib/players/players-visual-test-fixture';

// ─── State override hatch (dev / visual-test only) ─────────────────────────

const VALID_OVERRIDES = ['loading', 'error', 'empty', 'filtered-empty'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  if (raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}

// ─── Loading skeleton ────────────────────────────────────────────────────────

function PlayersLoadingSkeleton(): ReactElement {
  return (
    <div
      data-slot="players-loading"
      aria-label="Caricamento in corso…"
      aria-busy="true"
      aria-live="polite"
      className="grid grid-cols-[repeat(auto-fit,minmax(280px,1fr))] gap-4 px-4 sm:px-8 max-w-[1280px] mx-auto"
    >
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="animate-pulse rounded-2xl border border-border bg-muted/40 h-40" />
      ))}
    </div>
  );
}

// ─── Component ──────────────────────────────────────────────────────────────

export function PlayersLibraryView(): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const [search, setSearch] = useState<string>('');

  const stateOverride = parseStateOverride(searchParams.get('state'));

  // ── Data ─────────────────────────────────────────────────────────────────

  const statsQuery = usePlayerStatistics();

  const fixture = useMemo(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    if (stateOverride === 'empty') return [] as ReadonlyArray<PlayerListItem>;
    return tryLoadVisualTestFixture('default');
  }, [stateOverride]);

  const realItems = useMemo<ReadonlyArray<PlayerListItem>>(() => {
    if (fixture != null) return fixture;
    if (statsQuery.data == null) return [];
    return transformStatsToItems(statsQuery.data.gamePlayCounts);
  }, [fixture, statsQuery.data]);

  const filteredItems = useMemo(
    () => applyPlayersFilters(realItems, { search }),
    [realItems, search]
  );

  // ── FSM ──────────────────────────────────────────────────────────────────

  const realKind = useMemo<PlayersUiState>(() => {
    if (fixture != null) return 'default';
    return derivePlayersUiState({
      isLoading: statsQuery.isLoading,
      isError: statsQuery.isError,
      hasData: realItems.length > 0,
      hasFilters: hasPlayersFilters({ search }),
      filteredCount: filteredItems.length,
    });
  }, [
    fixture,
    statsQuery.isLoading,
    statsQuery.isError,
    realItems.length,
    search,
    filteredItems.length,
  ]);

  const effectiveKind: PlayersUiState = stateOverride ?? realKind;

  // ── i18n labels ───────────────────────────────────────────────────────────

  const heroLabels = useMemo<PlayersHeroLabels>(
    () => ({
      title: t('pages.players.hero.title'),
      subtitle: t('pages.players.hero.subtitle'),
      totalPlays: t('pages.players.hero.totalPlays'),
      distinctGames: t('pages.players.hero.distinctGames'),
    }),
    [t]
  );

  const filtersLabels = useMemo<PlayersFiltersInlineLabels>(
    () => ({
      searchPlaceholder: t('pages.players.filters.searchPlaceholder'),
      searchAriaLabel: t('pages.players.filters.searchAriaLabel'),
      clearFilters: t('pages.players.filters.clearFilters'),
    }),
    [t]
  );

  const gridLabels = useMemo<PlayersResultsGridLabels>(
    () => ({
      resultsAriaLabel: t('pages.players.results.resultsAriaLabel'),
      // ICU plural resolved per render with the current filtered item count
      resultsCount: t('pages.players.results.resultsCount', { count: filteredItems.length }),
      // cardSubtitle + cardOpenAriaLabel contain {count}/{gameName} placeholder tokens
      // that the PlayersResultsGrid component substitutes via String.replace() per item.
      // We must NOT let react-intl parse these as ICU variables (it would throw because
      // no value is provided at label-resolution time). Read the raw message string
      // via intl.messages to bypass ICU parsing — the grid component handles substitution.
      cardSubtitle:
        (intl.messages['pages.players.results.cardSubtitle'] as string) ?? '{count} partite',
      cardOpenAriaLabel:
        (intl.messages['pages.players.results.cardOpenAriaLabel'] as string) ?? 'Apri {gameName}',
    }),
    [t, intl.messages, filteredItems.length]
  );

  const emptyLabels = useMemo<EmptyPlayersLabels>(
    () => ({
      empty: {
        title: t('pages.players.states.empty.title'),
        subtitle: t('pages.players.states.empty.subtitle'),
        cta: t('pages.players.states.empty.cta'),
      },
      filteredEmpty: {
        title: t('pages.players.states.filteredEmpty.title'),
        subtitle: t('pages.players.states.filteredEmpty.subtitle'),
        cta: t('pages.players.states.filteredEmpty.cta'),
      },
      error: {
        title: t('pages.players.states.error.title'),
        subtitle: t('pages.players.states.error.subtitle'),
        cta: t('pages.players.states.error.cta'),
      },
    }),
    [t]
  );

  // ── Handlers ─────────────────────────────────────────────────────────────

  const handleItemClick = useCallback(
    (item: PlayerListItem) => {
      router.push(`/players/${item.id}`);
    },
    [router]
  );

  const handleRetry = useCallback(() => {
    void statsQuery.refetch?.();
  }, [statsQuery]);

  const handleClearFilters = useCallback(() => {
    setSearch('');
    if (stateOverride != null) {
      router.push(pathname);
    }
  }, [stateOverride, router, pathname]);

  // ── Stats derived for Hero ────────────────────────────────────────────────

  const totalSessions = statsQuery.data?.totalSessions ?? 0;
  const distinctGames = realItems.length;

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div data-slot="players-library-view" className="flex flex-col gap-6 pb-24">
      <PlayersHero
        totalSessions={totalSessions}
        distinctGames={distinctGames}
        labels={heroLabels}
      />
      <PlayersFiltersInline
        search={search}
        onSearchChange={setSearch}
        onClearFilters={handleClearFilters}
        hasFilters={hasPlayersFilters({ search })}
        labels={filtersLabels}
      />
      {effectiveKind === 'loading' ? (
        <PlayersLoadingSkeleton />
      ) : effectiveKind === 'default' ? (
        <PlayersResultsGrid
          items={filteredItems}
          onItemClick={handleItemClick}
          labels={gridLabels}
        />
      ) : (
        <EmptyPlayers
          kind={effectiveKind}
          labels={emptyLabels}
          onClearFilters={handleClearFilters}
          onRetry={handleRetry}
        />
      )}
    </div>
  );
}
