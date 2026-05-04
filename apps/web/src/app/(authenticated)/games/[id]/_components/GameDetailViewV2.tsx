/**
 * GameDetailViewV2 — Wave C.1 (Issue #581) orchestrator for `/games/[id]`.
 *
 * Mirrors the Wave B.3 LibraryHubV2 pattern: brownfield big-bang replacement
 * of the v1 hero + 4-link tab nav. There is no feature flag — rollback is
 * `git revert` on the merge commit.
 *
 * Subroute pages `/games/[id]/{faqs,rules,reviews,sessions,strategies}` are
 * preserved unchanged. The new v2 hero links to them via the FAQ list and
 * rules accordion "view all" CTAs (decision: keep subroute pages).
 *
 * State surface (5-state FSM):
 *   - `loading`   → query in flight
 *   - `error`     → query failed
 *   - `not-found` → query returned null (game not in user library)
 *   - `default`   → query returned a `LibraryGameDetail`
 *   - `empty`     → reserved (treated identically to `not-found` for now)
 *
 * Tab state is local React state. URL hash sync is intentionally NOT wired
 * here — staying close to the spec's minimum surface to avoid scope creep.
 *
 * Data sourcing:
 *   - `useLibraryGameDetail(id)` — game metadata + library data + recent sessions
 *   - `api.agents.getUserAgentsForGame(gameId)` — agents linked to this game
 *     (loaded only when the agents tab is active to avoid the eager fetch)
 *   - FAQ / rule sections are not yet exposed by the backend on `/games/[id]`
 *     so the v2 panes show empty-state copy that links to the legacy
 *     subroute pages where the actual data lives.
 *
 * `?state=...` URL override hatch (mirror Wave B.3): only enabled when
 * `process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD`. Legal values:
 * `loading | empty | not-found | error`.
 */

'use client';

import { useMemo, useState, type ReactElement } from 'react';

import { useQuery } from '@tanstack/react-query';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  GameDetailAgentsList,
  GameDetailFaqList,
  GameDetailHero,
  GameDetailKbDocList,
  GameDetailKpiCards,
  GameDetailRulesAccordion,
  GameDetailSessionsRail,
  GameDetailTabsAnimated,
  type GameDetailAgentEntry,
  type GameDetailFaqEntry,
  type GameDetailHeroLabels,
  type GameDetailHeroMeta,
  type GameDetailHeroVariant,
  type GameDetailKbDocEntry,
  type GameDetailKpiCard,
  type GameDetailRuleSection,
  type GameDetailSessionEntry,
  type GameDetailTabConfig,
} from '@/components/v2/game-detail';
import { useLibraryGameDetail, type LibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { deriveGameDetailUiState, type GameDetailUiState } from '@/lib/games/game-detail-state';
import {
  IS_VISUAL_TEST_BUILD,
  tryLoadVisualTestFixture,
} from '@/lib/games/game-detail-visual-test-fixture';

// ─── Types ──────────────────────────────────────────────────────────────

type TabKey = 'info' | 'rules' | 'faqs' | 'sessions' | 'agents' | 'documents';

// ─── State override hatch ──────────────────────────────────────────────

const VALID_OVERRIDES = ['loading', 'empty', 'not-found', 'error'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  if (raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}

// ─── Component ──────────────────────────────────────────────────────────

export interface GameDetailViewV2Props {
  readonly id: string;
}

export function GameDetailViewV2(props: GameDetailViewV2Props): ReactElement {
  const { id } = props;
  const { t } = useTranslation();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const [tab, setTab] = useState<TabKey>('info');

  const stateOverride = parseStateOverride(searchParams.get('state'));

  const detailQuery = useLibraryGameDetail(id);

  // Visual-test fixture short-circuit (CI can't reach backend at :8080).
  const fixture = useMemo<LibraryGameDetail | null>(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    if (stateOverride === 'not-found' || stateOverride === 'empty') return null;
    return tryLoadVisualTestFixture('default');
  }, [stateOverride]);

  // Agents linked to this game — loaded lazily when the agents tab is active
  // OR when we need to compute the count for the tab badge.
  const agentsQuery = useQuery({
    queryKey: ['game-detail', 'agents', id],
    queryFn: async () => {
      try {
        return await api.agents.getUserAgentsForGame(id);
      } catch {
        return [];
      }
    },
    enabled: !!id && (fixture == null ? detailQuery.data != null : true),
    staleTime: 60_000,
  });

  // ─── Resolve actual data: prefer fixture, fall back to query ──────────
  const detail = fixture ?? detailQuery.data ?? null;

  const realKind = useMemo<GameDetailUiState>(() => {
    if (fixture != null) return 'default';
    return deriveGameDetailUiState({
      isLoading: detailQuery.isLoading,
      isError: detailQuery.isError,
      hasData: detailQuery.data != null,
    });
  }, [detailQuery.isLoading, detailQuery.isError, detailQuery.data, fixture]);

  const effectiveKind: GameDetailUiState = stateOverride ?? realKind;

  // ─── i18n labels resolved upfront ─────────────────────────────────────

  const heroLabels = useMemo<GameDetailHeroLabels>(
    () => ({
      back: t('pages.gameDetail.hero.back'),
      backAriaLabel: t('pages.gameDetail.hero.backAriaLabel'),
      ownedBadge: t('pages.gameDetail.hero.ownedBadge'),
      communityBadge: t('pages.gameDetail.hero.communityBadge'),
      ctaPlay: t('pages.gameDetail.hero.ctaPlay'),
      ctaEdit: t('pages.gameDetail.hero.ctaEdit'),
      ctaShare: t('pages.gameDetail.hero.ctaShare'),
      ctaShareAriaLabel: t('pages.gameDetail.hero.ctaShareAriaLabel'),
      ctaAddToLibrary: t('pages.gameDetail.hero.ctaAddToLibrary'),
      ctaSimilar: t('pages.gameDetail.hero.ctaSimilar'),
      favoriteAriaLabel: t('pages.gameDetail.hero.favoriteAriaLabel'),
    }),
    [t]
  );

  const tabsAriaLabel = t('pages.gameDetail.tabs.ariaLabel');

  // ─── Derived view models ──────────────────────────────────────────────

  const variant: GameDetailHeroVariant = detail?.libraryEntryId ? 'own' : 'community';

  const heroMeta = useMemo<GameDetailHeroMeta>(() => {
    if (!detail) {
      return {
        designer: null,
        year: null,
        players: null,
        duration: null,
        complexity: null,
        rating: null,
      };
    }
    const designer = detail.designers?.[0]?.name ?? null;
    const players =
      detail.minPlayers != null &&
      detail.maxPlayers != null &&
      detail.minPlayers !== detail.maxPlayers
        ? t('pages.gameDetail.hero.metaPlayers', {
            min: detail.minPlayers,
            max: detail.maxPlayers,
          })
        : detail.minPlayers != null
          ? t('pages.gameDetail.hero.metaPlayersSingle', { count: detail.minPlayers })
          : null;
    const duration =
      detail.playingTimeMinutes != null
        ? t('pages.gameDetail.hero.metaDuration', { minutes: detail.playingTimeMinutes })
        : null;
    const complexity =
      detail.complexityRating != null
        ? t('pages.gameDetail.hero.metaWeight', { value: detail.complexityRating.toFixed(1) })
        : null;
    const rating =
      detail.averageRating != null
        ? t('pages.gameDetail.hero.metaRating', { value: detail.averageRating.toFixed(1) })
        : null;
    return {
      designer,
      year: detail.gameYearPublished ?? null,
      players,
      duration,
      complexity,
      rating,
    };
  }, [detail, t]);

  const kpiCards = useMemo<ReadonlyArray<GameDetailKpiCard>>(() => {
    const cards: GameDetailKpiCard[] = [];
    cards.push({
      key: 'rating',
      label: t('pages.gameDetail.kpi.rating'),
      value:
        detail?.averageRating != null
          ? detail.averageRating.toFixed(1)
          : t('pages.gameDetail.kpi.notAvailable'),
      unit: detail?.averageRating != null ? t('pages.gameDetail.kpi.ratingUnit') : undefined,
      icon: '⭐',
      accent: 'rating',
    });
    cards.push({
      key: 'complexity',
      label: t('pages.gameDetail.kpi.complexity'),
      value:
        detail?.complexityRating != null
          ? detail.complexityRating.toFixed(1)
          : t('pages.gameDetail.kpi.notAvailable'),
      unit: detail?.complexityRating != null ? t('pages.gameDetail.kpi.complexityUnit') : undefined,
      icon: '🧠',
      accent: 'complexity',
    });
    cards.push({
      key: 'players',
      label: t('pages.gameDetail.kpi.players'),
      value:
        detail?.minPlayers != null && detail?.maxPlayers != null
          ? t('pages.gameDetail.kpi.playersRange', {
              min: detail.minPlayers,
              max: detail.maxPlayers,
            })
          : detail?.minPlayers != null
            ? t('pages.gameDetail.kpi.playersValue', { count: detail.minPlayers })
            : t('pages.gameDetail.kpi.notAvailable'),
      icon: '👥',
      accent: 'players',
    });
    cards.push({
      key: 'time',
      label: t('pages.gameDetail.kpi.playTime'),
      value:
        detail?.playingTimeMinutes != null
          ? String(detail.playingTimeMinutes)
          : t('pages.gameDetail.kpi.notAvailable'),
      unit: detail?.playingTimeMinutes != null ? t('pages.gameDetail.kpi.playTimeUnit') : undefined,
      icon: '⏱',
      accent: 'time',
    });
    return cards;
  }, [detail, t]);

  // FAQ + rule sections: backend doesn't yet expose these on /games/[id], so
  // the v2 panes link out to the legacy subroute pages. The list is empty
  // here, the empty-state CTA points to the subroute.
  const faqs = useMemo<ReadonlyArray<GameDetailFaqEntry>>(() => [], []);
  const ruleSections = useMemo<ReadonlyArray<GameDetailRuleSection>>(() => [], []);

  const sessions = useMemo<ReadonlyArray<GameDetailSessionEntry>>(() => {
    if (!detail?.recentSessions) return [];
    return detail.recentSessions.map(s => ({
      id: s.id,
      playedAt: s.playedAt,
      durationFormatted: s.durationFormatted,
      didWin: s.didWin,
      playersLine: s.players ?? '',
    }));
  }, [detail]);

  const agents = useMemo<ReadonlyArray<GameDetailAgentEntry>>(() => {
    const raw = agentsQuery.data ?? [];
    return raw.map(agent => ({
      id: agent.id,
      name: agent.name,
      model: 'model' in agent && typeof agent.model === 'string' ? agent.model : null,
      kbCount: 'kbCount' in agent && typeof agent.kbCount === 'number' ? agent.kbCount : 0,
      invocations:
        'invocations' in agent && typeof agent.invocations === 'number' ? agent.invocations : 0,
      isActive: 'isActive' in agent && typeof agent.isActive === 'boolean' ? agent.isActive : true,
    }));
  }, [agentsQuery.data]);

  // KB documents — minimal placeholder until /games/[id]/kb endpoint exists.
  const kbDocs = useMemo<ReadonlyArray<GameDetailKbDocEntry>>(() => [], []);

  // ─── Tabs config ──────────────────────────────────────────────────────

  const tabs = useMemo<ReadonlyArray<GameDetailTabConfig<TabKey>>>(
    () => [
      { key: 'info', label: t('pages.gameDetail.tabs.info'), icon: '📘' },
      { key: 'rules', label: t('pages.gameDetail.tabs.rules'), icon: '📜' },
      { key: 'faqs', label: t('pages.gameDetail.tabs.faqs'), icon: '❓' },
      {
        key: 'sessions',
        label: t('pages.gameDetail.tabs.sessions'),
        icon: '🎯',
        count: detail?.timesPlayed ?? 0,
        locked: variant !== 'own',
      },
      {
        key: 'agents',
        label: t('pages.gameDetail.tabs.agents'),
        icon: '🤖',
        count: agents.length,
      },
      { key: 'documents', label: t('pages.gameDetail.tabs.documents'), icon: '📄' },
    ],
    [t, detail, variant, agents.length]
  );

  // ─── Callbacks ────────────────────────────────────────────────────────

  const handleBack = (): void => {
    router.push('/games');
  };

  const handleClearOverride = (): void => {
    if (stateOverride != null) {
      router.push(pathname);
    }
  };

  const handleRetry = (): void => {
    handleClearOverride();
    void detailQuery.refetch();
  };

  // ─── Render: not-found / error / loading shells ───────────────────────

  if (effectiveKind === 'loading') {
    return (
      <div
        data-slot="game-detail-view"
        data-state="loading"
        aria-label={t('pages.gameDetail.states.loading.ariaLabel')}
        className="flex flex-col"
      >
        <Skeleton className="h-[180px] w-full sm:h-[240px]" />
        <div className="space-y-4 p-4 sm:p-8">
          <Skeleton className="h-9 w-1/3" />
          <Skeleton className="h-24 w-full" />
          <div className="grid grid-cols-2 gap-2.5 md:grid-cols-4">
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
          </div>
        </div>
      </div>
    );
  }

  if (effectiveKind === 'error') {
    return (
      <div
        data-slot="game-detail-view"
        data-state="error"
        role="alert"
        className="mx-auto flex max-w-md flex-col items-center justify-center gap-2 p-12 text-center"
      >
        <span aria-hidden="true" className="text-4xl">
          ⚠️
        </span>
        <h2 className="font-display text-xl font-extrabold text-foreground">
          {t('pages.gameDetail.states.error.title')}
        </h2>
        <p className="text-sm text-muted-foreground">
          {t('pages.gameDetail.states.error.subtitle')}
        </p>
        <button
          type="button"
          onClick={handleRetry}
          className="mt-2 rounded-md bg-rose-600 px-4 py-2 font-display text-sm font-bold text-white shadow-md hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {t('pages.gameDetail.states.error.cta')}
        </button>
      </div>
    );
  }

  if (effectiveKind === 'not-found' || effectiveKind === 'empty' || !detail) {
    return (
      <div
        data-slot="game-detail-view"
        data-state="not-found"
        role="status"
        className="mx-auto flex max-w-md flex-col items-center justify-center gap-2 p-12 text-center"
      >
        <span aria-hidden="true" className="text-4xl">
          🎲
        </span>
        <h2 className="font-display text-xl font-extrabold text-foreground">
          {t('pages.gameDetail.states.notFound.title')}
        </h2>
        <p className="text-sm text-muted-foreground">
          {t('pages.gameDetail.states.notFound.subtitle')}
        </p>
        <button
          type="button"
          onClick={handleBack}
          className="mt-2 rounded-md bg-amber-600 px-4 py-2 font-display text-sm font-bold text-white shadow-md hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {t('pages.gameDetail.states.notFound.cta')}
        </button>
      </div>
    );
  }

  // ─── Render: default surface ──────────────────────────────────────────

  return (
    <div data-slot="game-detail-view" data-state="default" className="flex flex-col">
      <GameDetailHero
        title={detail.gameTitle}
        imageUrl={detail.gameImageUrl}
        variant={variant}
        meta={heroMeta}
        isFavorite={detail.isFavorite}
        labels={heroLabels}
        onBack={handleBack}
      />

      <GameDetailTabsAnimated
        tabs={tabs}
        active={tab}
        onChange={setTab}
        ariaLabel={tabsAriaLabel}
      />

      <div
        role="tabpanel"
        id={`game-detail-panel-${tab}`}
        aria-labelledby={`game-detail-tab-${tab}`}
        data-slot="game-detail-tab-panel"
        data-active-tab={tab}
        className="flex flex-col gap-4 p-4 sm:p-8"
      >
        {tab === 'info' ? (
          <>
            <GameDetailKpiCards cards={kpiCards} />
            <section
              data-slot="game-detail-description"
              className="rounded-2xl border border-border bg-card p-4 shadow-sm"
            >
              <h3 className="mb-2 font-display text-[15px] font-extrabold text-foreground">
                {t('pages.gameDetail.info.descriptionTitle')}
              </h3>
              <p className="text-[13.5px] leading-relaxed text-muted-foreground">
                {detail.description ?? t('pages.gameDetail.info.noDescription')}
              </p>
            </section>
            <section
              data-slot="game-detail-specs"
              className="rounded-2xl border border-border bg-card p-4 shadow-sm"
            >
              <h3 className="mb-3 font-display text-[15px] font-extrabold text-foreground">
                {t('pages.gameDetail.info.specsTitle')}
              </h3>
              <dl className="grid grid-cols-2 gap-x-4 gap-y-2 sm:grid-cols-3">
                {heroMeta.designer ? (
                  <div>
                    <dt className="font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground">
                      {t('pages.gameDetail.info.specsDesigner')}
                    </dt>
                    <dd className="font-display text-[13px] font-bold text-foreground">
                      {heroMeta.designer}
                    </dd>
                  </div>
                ) : null}
                {detail.gamePublisher ? (
                  <div>
                    <dt className="font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground">
                      {t('pages.gameDetail.info.specsPublisher')}
                    </dt>
                    <dd className="font-display text-[13px] font-bold text-foreground">
                      {detail.gamePublisher}
                    </dd>
                  </div>
                ) : null}
                {detail.gameYearPublished ? (
                  <div>
                    <dt className="font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground">
                      {t('pages.gameDetail.info.specsYear')}
                    </dt>
                    <dd className="font-display text-[13px] font-bold text-foreground">
                      {detail.gameYearPublished}
                    </dd>
                  </div>
                ) : null}
                {detail.minAge ? (
                  <div>
                    <dt className="font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground">
                      {t('pages.gameDetail.info.specsAge')}
                    </dt>
                    <dd className="font-display text-[13px] font-bold text-foreground">
                      {detail.minAge}+
                    </dd>
                  </div>
                ) : null}
              </dl>
            </section>
          </>
        ) : null}

        {tab === 'rules' ? (
          <GameDetailRulesAccordion
            sections={ruleSections}
            viewAllHref={`/games/${id}/rules`}
            labels={{
              title: t('pages.gameDetail.rules.title'),
              subtitle: t('pages.gameDetail.rules.subtitle'),
              viewAll: t('pages.gameDetail.rules.viewAll'),
              viewAllAriaLabel: t('pages.gameDetail.rules.viewAllAriaLabel'),
              empty: t('pages.gameDetail.rules.empty'),
            }}
          />
        ) : null}

        {tab === 'faqs' ? (
          <GameDetailFaqList
            faqs={faqs}
            viewAllHref={`/games/${id}/faqs`}
            labels={{
              title: t('pages.gameDetail.faqs.title'),
              subtitle: t('pages.gameDetail.faqs.subtitle', { title: detail.gameTitle }),
              viewAll: t('pages.gameDetail.faqs.viewAll'),
              viewAllAriaLabel: t('pages.gameDetail.faqs.viewAllAriaLabel'),
              empty: t('pages.gameDetail.faqs.empty'),
              questionAriaLabel: t('pages.gameDetail.faqs.questionAriaLabel'),
            }}
          />
        ) : null}

        {tab === 'sessions' ? (
          <GameDetailSessionsRail
            sessions={sessions}
            viewAllHref={`/games/${id}/sessions`}
            labels={{
              title: t('pages.gameDetail.sessions.title'),
              subtitle: t('pages.gameDetail.sessions.subtitle'),
              viewAll: t('pages.gameDetail.sessions.viewAll'),
              viewAllAriaLabel: t('pages.gameDetail.sessions.viewAllAriaLabel'),
              empty: t('pages.gameDetail.sessions.empty'),
              emptySubtitle: t('pages.gameDetail.sessions.emptySubtitle'),
              newSession: t('pages.gameDetail.sessions.newSession'),
              winLabel: t('pages.gameDetail.sessions.winLabel'),
              lossLabel: t('pages.gameDetail.sessions.lossLabel'),
            }}
          />
        ) : null}

        {tab === 'agents' ? (
          <GameDetailAgentsList
            agents={agents}
            labels={{
              title: t('pages.gameDetail.agents.title'),
              subtitle: t('pages.gameDetail.agents.subtitle'),
              empty: t('pages.gameDetail.agents.empty'),
              emptySubtitle: t('pages.gameDetail.agents.emptySubtitle'),
              createCta: t('pages.gameDetail.agents.createCta'),
              openAriaLabel: t('pages.gameDetail.agents.openAriaLabel', { name: '{name}' }),
              indexedLabel: t('pages.gameDetail.agents.indexedLabel', { count: '{count}' }),
              invocationsLabel: t('pages.gameDetail.agents.invocationsLabel', {
                count: '{count}',
              }),
            }}
          />
        ) : null}

        {tab === 'documents' ? (
          <GameDetailKbDocList
            docs={kbDocs}
            labels={{
              title: t('pages.gameDetail.documents.title'),
              subtitle: t('pages.gameDetail.documents.subtitle'),
              empty: t('pages.gameDetail.documents.empty'),
              emptySubtitle: t('pages.gameDetail.documents.emptySubtitle'),
              uploadCta: t('pages.gameDetail.documents.uploadCta'),
              openCta: t('pages.gameDetail.documents.openCta'),
              openAriaLabel: t('pages.gameDetail.documents.openAriaLabel', { name: '{name}' }),
              statusIndexed: t('pages.gameDetail.documents.statusIndexed'),
              statusProcessing: t('pages.gameDetail.documents.statusProcessing'),
              statusFailed: t('pages.gameDetail.documents.statusFailed'),
              statsLineTemplate: t('pages.gameDetail.documents.statsLine', {
                size: '{size}',
                pages: '{pages}',
                chunks: '{chunks}',
              }),
            }}
          />
        ) : null}
      </div>
    </div>
  );
}
