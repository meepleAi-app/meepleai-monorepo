/**
 * GamebookIndexView — SP6 Phase B Tier M orchestrator (Issue #788).
 *
 * Composes 2 hook queries (`useGamebooks`, `useQuotaInfo`) + URL-state SSOT
 * (`?fixture=`) and renders the `/gamebook` index 6-cell FSM
 * (loading | error | empty | default | quota-soft | quota-hard).
 *
 * v2 brownfield route — pre-Phase-B the path `/gamebook` had no index page
 * (only `/gamebook/[gameId]` and `/gamebook/upload` existed). This is the
 * first index orchestrator landing under that prefix.
 *
 * Hook composition (Phase 0.5 contract §3):
 *   1. useGamebooks()   — v1 carryover STUB returning fixture data
 *   2. useQuotaInfo()   — v1 carryover STUB returning fixture data
 *
 * Schema reality v1 carryover (Gate B):
 *   Both `useGamebooks` and `useQuotaInfo` resolve to canonical fixture data
 *   (see `apps/web/src/hooks/queries/useGamebooks.ts` JSDoc) because the
 *   backend `/api/v1/gamebooks` and quota endpoints are NOT exposed in v1.
 *   Real backend integration deferred to a follow-up issue post-Phase B.
 *
 * URL state SSOT (no useState mirror):
 *   ?fixture=default|empty|quota-soft|quota-hard|loading|error
 *     — visual fixture override gated by `STATE_OVERRIDE_ENABLED`. Allows
 *       E2E specs and dev navigation to render deterministic FSM cells
 *       without backend. The `loading` and `error` overrides are forced
 *       upstream of the FSM (because the FSM derives those cells from
 *       query state, not data shape).
 *
 * Gate A (ICU plural): all label strings are resolved here via
 *   `useTranslation().t(key, valuesOrDefault)`. Pure components receive
 *   pre-resolved strings (Wave D.3 pattern). NEVER `intl.messages[k].replace(…)`.
 *
 * Gate C (MeepleCard fit): `GamebookCard` DIVERGES per Phase B Task 2 audit
 *   (status pill with retry button, indexing progress bar, 4-chip metadata
 *   strip). The shared MeepleCard API is insufficient.
 *
 * Pattern blueprint: Wave D.3 SessionSummaryView orchestrator (i18n hook,
 * fixture short-circuit, FSM rendering).
 *
 * @see docs/superpowers/specs/2026-05-06-sp6-libro-game.md
 */

'use client';

import { useCallback, useMemo, type ReactElement } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import {
  EmptyGamebooks,
  GamebookCard,
  GamebookCardSkeleton,
  GamebookHero,
  QuotaWidget,
  type EmptyGamebooksLabels,
  type GamebookCardLabels,
  type GamebookHeroLabels,
  type QuotaWidgetLabels,
  type QuotaWidgetVariant,
} from '@/components/v2/gamebook';
import { useGamebooks, useQuotaInfo } from '@/hooks/queries/useGamebooks';
import { useTranslation } from '@/hooks/useTranslation';
import {
  deriveGamebookIndexState,
  gamebookIndexFixtures,
  parseStateOverride,
  STATE_OVERRIDE_ENABLED,
  type GamebookCardData,
  type GamebookIndexFSMCell,
  type GamebookIndexFixture,
  type GamebookIndexFixtureKind,
  type QuotaInfo,
} from '@/lib/gamebook-index';

// ─── Constants ────────────────────────────────────────────────────────────

const SKELETON_COUNT = 6;

// ─── Component ────────────────────────────────────────────────────────────

export function GamebookIndexView(): ReactElement {
  const { t, formatDate } = useTranslation();
  const searchParams = useSearchParams();
  const router = useRouter();

  // ── URL state SSOT ─────────────────────────────────────────────────────
  const fixtureOverride: GamebookIndexFixtureKind | null = STATE_OVERRIDE_ENABLED
    ? parseStateOverride(searchParams.get('fixture'))
    : null;

  // ── Visual fixture path: short-circuit hooks when override active ─────
  const fixture: GamebookIndexFixture | null = useMemo(() => {
    if (fixtureOverride === null) return null;
    if (fixtureOverride === 'loading' || fixtureOverride === 'error') {
      // These two cells live upstream of fixture data — they short-circuit
      // the FSM. We still resolve to the matching fixture record for
      // component-prop completeness (e.g. quota fallback in error shells).
      return gamebookIndexFixtures[fixtureOverride];
    }
    return gamebookIndexFixtures[fixtureOverride];
  }, [fixtureOverride]);

  // ── Real data hooks (always called for hook-rules consistency) ────────
  const gamebooksQuery = useGamebooks();
  const quotaQuery = useQuotaInfo();

  // ── FSM derivation (delegate to foundation) ───────────────────────────
  const fsmCell: GamebookIndexFSMCell = useMemo<GamebookIndexFSMCell>(() => {
    if (fixtureOverride === 'loading') return { kind: 'loading' };
    if (fixtureOverride === 'error') {
      return { kind: 'error', error: new Error('Fixture-forced error') };
    }
    if (fixture !== null) {
      // Fixture path: build the corresponding FSM cell deterministically
      // by feeding the fixture data into the same derivation function the
      // real-data path uses. This guarantees the rendered output matches
      // what real queries would produce given the same data.
      return deriveGamebookIndexState({
        gamebooksQuery: {
          isPending: false,
          isError: false,
          error: null,
          data: fixture.gamebooks,
        },
        quotaQuery: {
          isPending: false,
          isError: false,
          error: null,
          data: fixture.quota,
        },
      });
    }
    // Real-data path
    return deriveGamebookIndexState({
      gamebooksQuery: {
        isPending: gamebooksQuery.isPending,
        isError: gamebooksQuery.isError,
        error: gamebooksQuery.error ?? null,
        data: gamebooksQuery.data ?? null,
      },
      quotaQuery: {
        isPending: quotaQuery.isPending,
        isError: quotaQuery.isError,
        error: quotaQuery.error ?? null,
        data: quotaQuery.data ?? null,
      },
    });
  }, [
    fixtureOverride,
    fixture,
    gamebooksQuery.isPending,
    gamebooksQuery.isError,
    gamebooksQuery.error,
    gamebooksQuery.data,
    quotaQuery.isPending,
    quotaQuery.isError,
    quotaQuery.error,
    quotaQuery.data,
  ]);

  // ── Navigation handlers ────────────────────────────────────────────────
  const handleAddManualClick = useCallback(() => {
    router.push('/gamebook/upload');
  }, [router]);

  const handleGamebookClick = useCallback(
    (gamebookId: string) => {
      // Find the gamebook to retrieve its gameId for the navigation target.
      // Cells with kind=default|quota-soft|quota-hard expose the array.
      const gamebooks = extractGamebooks(fsmCell);
      const gamebook = gamebooks.find(g => g.id === gamebookId);
      if (gamebook && gamebook.status === 'ready') {
        // Navigate to the existing play route under /library/games/[gameId]/play.
        // The /gamebook/[id] route was never implemented (issue #865) — the play
        // surface lives under /library/games for historical reasons (PR #794+).
        router.push(`/library/games/${gamebook.gameId}/play`);
      }
    },
    [fsmCell, router]
  );

  const handleUpgradeClick = useCallback(() => {
    // Out-of-scope Phase B: wire to billing portal in follow-up.
    // For now, no-op — keeps the CTA visible without leaking a stub URL.
  }, []);

  const handleRetry = useCallback(() => {
    void gamebooksQuery.refetch?.();
    void quotaQuery.refetch?.();
  }, [gamebooksQuery, quotaQuery]);

  // ── i18n labels (Gate A: ICU plural resolved here) ────────────────────

  const heroLabels: GamebookHeroLabels = useMemo(
    () => ({
      title: t('gamebook.index.hero.title'),
      subtitle: t('gamebook.index.hero.subtitle'),
      kpiTotalGamebooks: t('gamebook.index.hero.kpiTotalGamebooks'),
      kpiTotalSessions: t('gamebook.index.hero.kpiTotalSessions'),
      kpiActiveAgents: t('gamebook.index.hero.kpiActiveAgents'),
      ctaAddManual: t('gamebook.index.hero.ctaAddManual'),
    }),
    [t]
  );

  const emptyLabels: EmptyGamebooksLabels = useMemo(
    () => ({
      title: t('gamebook.index.empty.title'),
      description: t('gamebook.index.empty.description'),
      cta: t('gamebook.index.empty.cta'),
    }),
    [t]
  );

  // Quota labels factory — depends on the active quota record (used / total /
  // resetDate). Memoised against a derived quota object.
  const buildQuotaLabels = useCallback(
    (quota: QuotaInfo): QuotaWidgetLabels => ({
      title: t('gamebook.index.quota.title'),
      // Gate A: ICU plural for "X di Y" (it.json) / "X of Y" (en.json).
      usedLabel: t('gamebook.index.quota.usedCount', {
        used: quota.used,
        total: quota.total,
      }),
      resetIn: t('gamebook.index.quota.resetIn', {
        date: formatDate(quota.resetDate, { dateStyle: 'long' }),
      }),
      softWarning: t('gamebook.index.quota.softWarning'),
      hardLimit: t('gamebook.index.quota.hardLimit'),
      upgrade: t('gamebook.index.quota.upgrade'),
    }),
    [t, formatDate]
  );

  // Card labels factory — depends on the gamebook record (pages, chunks, etc).
  const buildCardLabels = useCallback(
    (gb: GamebookCardData): GamebookCardLabels => ({
      statusReady: t('gamebook.index.card.statusReady'),
      statusIndexing: t('gamebook.index.card.statusIndexing'),
      statusError: t('gamebook.index.card.statusError'),
      // Gate A: ICU plurals
      pagesCount: t('gamebook.index.card.pagesCount', { pages: gb.pages }),
      chunksCount: t('gamebook.index.card.chunksCount', { count: gb.chunks }),
      qaCount: t('gamebook.index.card.qaCount', { count: gb.qaCount }),
      sessionsCount: t('gamebook.index.card.sessionsCount', { count: gb.sessionsCount }),
      indexingProgress: t('gamebook.index.card.indexingProgress', {
        indexed: gb.pages,
        total: gb.totalPages,
      }),
      errorRetry: t('gamebook.index.card.errorRetry'),
      openGamebook: t('gamebook.index.card.openGamebook'),
    }),
    [t]
  );

  // ── KPI counts derivation ──────────────────────────────────────────────
  const kpiCounts = useMemo(() => {
    const gamebooks = extractGamebooks(fsmCell);
    const totalGamebooks = gamebooks.length;
    const totalSessions = gamebooks.reduce((acc, g) => acc + g.sessionsCount, 0);
    const activeAgents = gamebooks.reduce((acc, g) => acc + g.qaCount, 0);
    return { totalGamebooks, totalSessions, activeAgents };
  }, [fsmCell]);

  // ── Quota CTA gating ───────────────────────────────────────────────────
  // Hero CTA is disabled when quota is at hard limit (uploads cannot start
  // a new translation). For loading/error/empty cells we keep it enabled
  // because the user has no quota context yet.
  const isHardLimit = fsmCell.kind === 'quota-hard';

  // ── Render ────────────────────────────────────────────────────────────

  // Cell: loading
  if (fsmCell.kind === 'loading') {
    return (
      <div data-slot="gamebook-index-view" data-ui-state="loading" className="flex flex-col">
        <GamebookHero
          totalGamebooks={0}
          totalSessions={0}
          activeAgents={0}
          onAddManualClick={handleAddManualClick}
          labels={heroLabels}
        />
        <div
          role="status"
          aria-label={t('gamebook.index.loading.label')}
          aria-live="polite"
          className="mx-auto grid w-full max-w-[1280px] grid-cols-1 gap-4 px-4 py-4 sm:grid-cols-2 sm:px-8 lg:grid-cols-3"
        >
          {Array.from({ length: SKELETON_COUNT }).map((_, idx) => (
            <GamebookCardSkeleton key={`gb-skeleton-${idx}`} />
          ))}
        </div>
      </div>
    );
  }

  // Cell: error
  if (fsmCell.kind === 'error') {
    return (
      <div data-slot="gamebook-index-view" data-ui-state="error" className="flex flex-col">
        <GamebookHero
          totalGamebooks={0}
          totalSessions={0}
          activeAgents={0}
          onAddManualClick={handleAddManualClick}
          labels={heroLabels}
        />
        <div
          role="alert"
          data-slot="gamebook-index-error"
          className="mx-auto flex max-w-[1280px] flex-col items-center gap-4 px-4 py-12 text-center sm:px-8"
        >
          <p className="text-lg font-semibold text-foreground">{t('gamebook.index.error.title')}</p>
          <p className="text-sm text-slate-700">{t('gamebook.index.error.description')}</p>
          <button
            type="button"
            onClick={handleRetry}
            data-slot="gamebook-index-error-retry"
            className="rounded-lg bg-[hsl(25,95%,39%)] px-6 py-2 text-sm font-semibold text-white transition-colors hover:bg-[hsl(25,95%,32%)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring motion-reduce:transition-none"
          >
            {t('gamebook.index.error.retry')}
          </button>
        </div>
      </div>
    );
  }

  // Cell: empty (quota always present in this cell per FSM contract)
  if (fsmCell.kind === 'empty') {
    return (
      <div data-slot="gamebook-index-view" data-ui-state="empty" className="flex flex-col">
        <GamebookHero
          totalGamebooks={0}
          totalSessions={0}
          activeAgents={0}
          onAddManualClick={handleAddManualClick}
          labels={heroLabels}
        />
        <div className="mx-auto flex w-full max-w-[1280px] flex-col gap-6 px-4 py-6 sm:px-8">
          <QuotaWidget
            quota={fsmCell.quota}
            variant="default"
            onUpgradeClick={handleUpgradeClick}
            labels={buildQuotaLabels(fsmCell.quota)}
          />
          <EmptyGamebooks onAddManualClick={handleAddManualClick} labels={emptyLabels} />
        </div>
      </div>
    );
  }

  // Cells with grid: default | quota-soft | quota-hard
  const gamebooks = fsmCell.gamebooks;
  const quota = fsmCell.quota;
  const variant: QuotaWidgetVariant =
    fsmCell.kind === 'quota-hard' ? 'hard' : fsmCell.kind === 'quota-soft' ? 'soft' : 'default';

  return (
    <div data-slot="gamebook-index-view" data-ui-state={fsmCell.kind} className="flex flex-col">
      <GamebookHero
        totalGamebooks={kpiCounts.totalGamebooks}
        totalSessions={kpiCounts.totalSessions}
        activeAgents={kpiCounts.activeAgents}
        onAddManualClick={isHardLimit ? handleUpgradeClick : handleAddManualClick}
        labels={heroLabels}
      />
      <div className="mx-auto flex w-full max-w-[1280px] flex-col gap-6 px-4 py-6 sm:px-8">
        <QuotaWidget
          quota={quota}
          variant={variant}
          onUpgradeClick={handleUpgradeClick}
          labels={buildQuotaLabels(quota)}
        />
        <div
          data-slot="gamebook-index-grid"
          className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
        >
          {gamebooks.map(gb => (
            <GamebookCard
              key={gb.id}
              gamebook={gb}
              onClick={handleGamebookClick}
              labels={buildCardLabels(gb)}
            />
          ))}
        </div>
      </div>
    </div>
  );
}

// ─── Helpers ──────────────────────────────────────────────────────────────

/**
 * Extract the gamebooks array from any FSM cell that exposes one. Returns
 * an empty array for cells without gamebooks (loading / error / empty).
 */
function extractGamebooks(cell: GamebookIndexFSMCell): readonly GamebookCardData[] {
  if (cell.kind === 'default' || cell.kind === 'quota-soft' || cell.kind === 'quota-hard') {
    return cell.gamebooks;
  }
  return [];
}
