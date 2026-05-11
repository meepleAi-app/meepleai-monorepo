/**
 * SessionsLibraryView — Wave D.1 (Issue #735) orchestrator for `/sessions`.
 *
 * Tier S route — single hook (useActiveSessions), URL-based filter state SSOT.
 * Mirrors Wave 4 D1 (PR #717) PlayersLibraryView + Wave B.1 GamesLibraryView patterns.
 *
 * Key contracts:
 *   - URL state SSOT: ?status= | ?view= | ?search= | ?state= (no useState mirrors)
 *   - useActiveSessions(50) — fetch up to 50 sessions (active + history)
 *   - gameNameMap: stub `gameId → 'game-{gameId}'` fallback; orchestrator passes
 *     the map into transformSessionsToItems which falls back to gameId when empty.
 *     A proper game-title join is a post-Wave D.1 followup (requires a game catalog
 *     hook that returns id→title map, tracked separately).
 *   - 5-state FSM: default | loading | empty | filtered-empty | error
 *     via deriveSessionsUiState + parseStateOverride
 *   - Visual-test fixture short-circuit (IS_VISUAL_TEST_BUILD) for CI prod builds
 *     without a backend API — VISUAL_TEST_FIXTURE_SESSIONS substituted for real data
 *   - i18n labels resolved upfront via a single useTranslation() call;
 *     components NEVER call t() directly
 *
 * Wave B.3 lesson applied: single-tree responsive layout — no mobile/desktop split.
 * Wave 4 D1 lesson applied: Suspense wrapper in page.tsx for useSearchParams CSR bailout.
 */

'use client';

import { useCallback, useMemo, type ReactElement } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useIntl } from 'react-intl';

import {
  EmptySessions,
  SessionCardGrid,
  SessionCardList,
  SessionsFilters,
  SessionsHero,
  type EmptySessionsLabels,
  type SessionCardGridLabels,
  type SessionCardListLabels,
  type SessionsFiltersLabels,
  type SessionsHeroLabels,
} from '@/components/features/sessions';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useTranslation } from '@/hooks/useTranslation';
import {
  applySearchFilter,
  applyStatusFilter,
  parseStatusFilter,
  parseViewMode,
  transformSessionsToItems,
  type SessionListItem,
  type SessionStatusFilter,
  type SessionViewMode,
} from '@/lib/sessions/sessions-filters';
import { deriveSessionsUiState, type SessionsUiState } from '@/lib/sessions/sessions-state';
import {
  IS_VISUAL_TEST_BUILD,
  STATE_OVERRIDE_ENABLED,
  VISUAL_TEST_FIXTURE_SESSIONS,
  VISUAL_TEST_FIXTURE_SESSIONS_EMPTY,
  parseStateOverride,
} from '@/lib/sessions/sessions-visual-test-fixture';

// ─── Component ──────────────────────────────────────────────────────────────

export function SessionsLibraryView(): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  // ── URL state SSOT (no useState mirrors) ─────────────────────────────────
  // All filter/view/search state lives in URL. Components call handlers which
  // call router.replace to update URL, triggering re-render with new searchParams.

  const statusFilter = parseStatusFilter(searchParams.get('status'));
  const view = parseViewMode(searchParams.get('view'));
  const search = searchParams.get('search') ?? '';

  // State override hatch (dev/visual-test only — dead code in production)
  // Pass a URLSearchParams-compatible object by constructing one from the
  // current search string so parseStateOverride works regardless of mock shape.
  const stateOverride = STATE_OVERRIDE_ENABLED
    ? parseStateOverride(new URLSearchParams(searchParams.toString()))
    : null;

  // ── Data ─────────────────────────────────────────────────────────────────

  const sessionsQuery = useActiveSessions(50);

  // Visual fixture short-circuit: CI prod build without backend API
  const fixture = useMemo<ReadonlyArray<SessionListItem> | null>(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    // ?fixture=empty or stateOverride='empty' → empty fixture list
    if (searchParams.get('fixture') === 'empty' || stateOverride === 'empty') {
      return VISUAL_TEST_FIXTURE_SESSIONS_EMPTY;
    }
    return VISUAL_TEST_FIXTURE_SESSIONS;
  }, [stateOverride, searchParams]);

  // Transform backend DTOs → display-ready SessionListItem[]
  // gameNameMap stub: gameId → 'game-{gameId}' for now. When a game-catalog hook
  // that exposes id→name becomes available, inject it here.
  // The empty map causes transformSessionsToItems to fall back to dto.gameId as-is.
  const allItems = useMemo<ReadonlyArray<SessionListItem>>(() => {
    if (fixture != null) return fixture;
    const sessions = sessionsQuery.data?.sessions ?? [];
    return transformSessionsToItems(sessions, /* gameNameMap= */ {});
  }, [fixture, sessionsQuery.data]);

  // Apply status filter
  const statusFiltered = useMemo(
    () => applyStatusFilter(allItems, statusFilter),
    [allItems, statusFilter]
  );

  // Apply search filter
  const filtered = useMemo(
    () => applySearchFilter(statusFiltered, search),
    [statusFiltered, search]
  );

  // ── FSM ──────────────────────────────────────────────────────────────────

  const realKind = useMemo<SessionsUiState>(() => {
    if (fixture != null) return 'default'; // fixture always renders default view
    return deriveSessionsUiState({
      isLoading: sessionsQuery.isLoading,
      isError: sessionsQuery.isError,
      totalCount: allItems.length,
      filteredCount: filtered.length,
    });
  }, [fixture, sessionsQuery.isLoading, sessionsQuery.isError, allItems.length, filtered.length]);

  const effectiveKind: SessionsUiState = stateOverride ?? realKind;

  // ── i18n labels ───────────────────────────────────────────────────────────

  const heroLabels = useMemo<SessionsHeroLabels>(
    () => ({
      title: t('pages.sessions.hero.title'),
      // Resolve ICU plural subtitle with count param via next-intl's t() (proper formatter)
      subtitle: t('pages.sessions.hero.subtitle', { count: allItems.length }),
      ctaNew: t('pages.sessions.hero.ctaNew'),
    }),
    [t, allItems.length]
  );

  const filtersLabels = useMemo<SessionsFiltersLabels>(
    () => ({
      statusAll: t('pages.sessions.filters.status.all'),
      statusActive: t('pages.sessions.filters.status.active'),
      statusCompleted: t('pages.sessions.filters.status.completed'),
      statusAbandoned: t('pages.sessions.filters.status.abandoned'),
      searchPlaceholder: t('pages.sessions.filters.searchPlaceholder'),
      searchAriaLabel: t('pages.sessions.filters.searchAriaLabel'),
      viewList: t('pages.sessions.filters.view.list'),
      viewGrid: t('pages.sessions.filters.view.grid'),
      statusGroupLabel: t('pages.sessions.filters.status.all'), // reuse "Tutti" as group label
      viewToggleLabel: t('pages.sessions.filters.view.label'),
    }),
    [t]
  );

  const cardLabels = useMemo<SessionCardListLabels>(
    () => ({
      outcomeWon: t('pages.sessions.card.outcome.won'),
      outcomeLost: t('pages.sessions.card.outcome.lost'),
      outcomeTie: t('pages.sessions.card.outcome.tie'),
      statusLive: t('pages.sessions.card.status.live'),
      statusPaused: t('pages.sessions.card.status.paused'),
      statusAbandoned: t('pages.sessions.card.status.abandoned'),
      playerCountTemplate:
        (intl.messages['pages.sessions.card.playerCount'] as string) ?? '{count} giocatori',
      chatCountTemplate: '{count}',
      turnTemplate: (intl.messages['pages.sessions.card.turnLabel'] as string) ?? 'Turno {turn}',
      winnerLabel: t('pages.sessions.card.winnerLabel'),
      openSessionAriaTemplate:
        (intl.messages['pages.sessions.card.openAriaLabel'] as string) ??
        'Apri sessione {gameName}',
    }),
    [t, intl.messages]
  );

  // SessionCardGrid shares the same label shape as SessionCardList
  const gridCardLabels = useMemo<SessionCardGridLabels>(
    () => ({
      ...cardLabels,
      scoreOverflowTemplate: t('pages.sessions.card.scoreOverflowTemplate'),
    }),
    [cardLabels, t]
  );

  const emptyLabels = useMemo<EmptySessionsLabels>(
    () => ({
      emptyTitle: t('pages.sessions.empty.title'),
      emptyDescription: t('pages.sessions.empty.description'),
      emptyCta: t('pages.sessions.empty.ctaStart'),
      filteredEmptyTitle: t('pages.sessions.filteredEmpty.title'),
      filteredEmptyDescription: t('pages.sessions.filteredEmpty.description'),
      filteredEmptyCta: t('pages.sessions.filteredEmpty.ctaClear'),
      loadingAriaLabel: t('pages.sessions.loading.ariaLabel'),
      errorTitle: t('pages.sessions.error.title'),
      errorDescription: t('pages.sessions.error.description'),
      errorCta: t('pages.sessions.error.ctaRetry'),
    }),
    [t]
  );

  // ── URL update helpers ────────────────────────────────────────────────────

  /** Build a new URLSearchParams preserving params not owned by this view. */
  const buildQuery = useCallback(
    (overrides: Partial<Record<string, string | null>>): string => {
      const params = new URLSearchParams(searchParams.toString());
      Object.entries(overrides).forEach(([key, val]) => {
        if (val == null || val === '') {
          params.delete(key);
        } else {
          params.set(key, val);
        }
      });
      const qs = params.toString();
      return qs ? `?${qs}` : '';
    },
    [searchParams]
  );

  const handleStatusChange = useCallback(
    (next: SessionStatusFilter) => {
      const val = next === 'all' ? null : next;
      router.replace(`${pathname}${buildQuery({ status: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleViewChange = useCallback(
    (next: SessionViewMode) => {
      const val = next === 'list' ? null : next;
      router.replace(`${pathname}${buildQuery({ view: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleSearchChange = useCallback(
    (next: string) => {
      router.replace(`${pathname}${buildQuery({ search: next || null })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleNewSession = useCallback(() => {
    router.push('/sessions/new');
  }, [router]);

  const handleItemClick = useCallback(
    (item: SessionListItem) => {
      router.push(`/sessions/${item.id}`);
    },
    [router]
  );

  const handleRetry = useCallback(() => {
    void sessionsQuery.refetch?.();
  }, [sessionsQuery]);

  /** Filtered-empty CTA: reset status to 'all' (clears ?status= param). */
  const handleClearFilters = useCallback(() => {
    // Drop ?status= and ?search= params; preserve ?view= and ?state=
    router.replace(`${pathname}${buildQuery({ status: null, search: null })}`, { scroll: false });
  }, [router, pathname, buildQuery]);

  // ── Status counts for pill badges ────────────────────────────────────────

  const counts = useMemo<Partial<Record<SessionStatusFilter, number>>>(
    () => ({
      all: allItems.length,
      active: allItems.filter(s => s.status === 'inprogress' || s.status === 'paused').length,
      completed: allItems.filter(s => s.status === 'completed').length,
      abandoned: allItems.filter(s => s.status === 'abandoned').length,
    }),
    [allItems]
  );

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div
      data-slot="sessions-library-view"
      className="flex flex-col gap-6 pb-24"
      id="sessions-content"
    >
      <SessionsHero onNewSession={handleNewSession} labels={heroLabels} />

      <SessionsFilters
        statusFilter={statusFilter}
        onStatusChange={handleStatusChange}
        view={view}
        onViewChange={handleViewChange}
        search={search}
        onSearchChange={handleSearchChange}
        counts={counts}
        labels={filtersLabels}
      />

      {/* FSM render branches */}
      {effectiveKind === 'default' ? (
        view === 'grid' ? (
          <div
            role="region"
            aria-label={t('pages.sessions.a11y.resultsLabel')}
            className="mx-auto grid w-full max-w-[1280px] grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-4 px-4 sm:px-8"
          >
            {filtered.map(item => (
              <SessionCardGrid
                key={item.id}
                item={item}
                onClick={handleItemClick}
                labels={gridCardLabels}
              />
            ))}
          </div>
        ) : (
          <div
            role="region"
            aria-label={t('pages.sessions.a11y.resultsLabel')}
            className="mx-auto flex w-full max-w-[1280px] flex-col gap-2 px-4 sm:px-8"
          >
            {filtered.map(item => (
              <SessionCardList
                key={item.id}
                item={item}
                onClick={handleItemClick}
                labels={cardLabels}
              />
            ))}
          </div>
        )
      ) : (
        <EmptySessions
          kind={effectiveKind}
          labels={emptyLabels}
          onPrimaryAction={
            effectiveKind === 'empty'
              ? handleNewSession
              : effectiveKind === 'filtered-empty'
                ? handleClearFilters
                : effectiveKind === 'error'
                  ? handleRetry
                  : undefined
          }
        />
      )}
    </div>
  );
}
