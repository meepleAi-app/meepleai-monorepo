/**
 * GameDetailView — Wave C.1 (Issue #581) orchestrator.
 *
 * Phase 0.5 contract enforced (docs/frontend/contracts/games-id-hooks.md):
 *   - gameId normalized to string|null at page boundary (NEVER undefined)
 *   - useLibraryGameDetail (parent hook) gated by !!gameId
 *   - useGameAgents (lazy sub-hook) gated by !!gameId && detailQuery.isSuccess && tab === 'agents'
 *   - 5-state FSM via deriveGameDetailUiState (gameId null check FIRST per Cell 1 contract)
 *   - Visual fixture short-circuit for CI prod build (IS_VISUAL_TEST_BUILD)
 *   - ?state= URL override (dev + visual-test only)
 *
 * REWRITE from PR #697 commit 15b63e86c — fixes /api/v1/agents/undefined cascade.
 * Anti-pattern eliminated:
 *   ❌ const id = params?.id ?? '';  // string(undefined) → 'undefined' → !!id === true
 *   ❌ enabled: !!id && (fixture == null ? detailQuery.data != null : true)  // fixture bypass
 *
 * Tab state lives here (useState) — pure presentation in child components.
 * AgentsState discriminated union prevents data + loading co-occurrence (sez. 4.3).
 *
 * Refs #581.
 */

'use client';

import { useMemo, useState, type ReactElement } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import {
  GameDetailAgentsList,
  GameDetailFaqList,
  GameDetailHero,
  GameDetailKbDocList,
  GameDetailKpiCards,
  GameDetailRulesAccordion,
  GameDetailSessionsRail,
  GameDetailTabsAnimated,
  panelIdFor,
  tabIdFor,
  type AgentsState,
  type GameDetailAgentEntry,
  type GameDetailHeroMeta,
  type TabKey,
} from '@/components/features/game-detail';
import { toast } from '@/components/layout/Toast';
import { useGameAgents } from '@/hooks/queries/useGameAgents';
import {
  useAddGameToLibrary,
  useLibraryGameDetail,
  type LibraryGameDetail,
} from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';
import { deriveGameDetailUiState } from '@/lib/games/game-detail-state';
import {
  IS_VISUAL_TEST_BUILD,
  tryLoadVisualTestFixture,
} from '@/lib/games/game-detail-visual-test-fixture';

// ─── State override hatch (dev / visual-test only) ─────────────────────────

const VALID_OVERRIDES = ['loading', 'error', 'not-found', 'empty'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  if (raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}

// ─── Props ─────────────────────────────────────────────────────────────────

export interface GameDetailViewProps {
  /**
   * Normalized gameId from URL params.
   * MUST be string | null — NEVER undefined or the string 'undefined'.
   * See Phase 0.5 contract sez. 2.1.
   */
  readonly gameId: string | null;
}

// ─── Tab config ────────────────────────────────────────────────────────────

function buildTabsConfig(t: ReturnType<typeof useTranslation>['t']): ReadonlyArray<{
  key: TabKey;
  label: string;
}> {
  return [
    { key: 'info' as TabKey, label: t('pages.gameDetail.tabs.info') },
    { key: 'rules' as TabKey, label: t('pages.gameDetail.tabs.rules') },
    { key: 'faqs' as TabKey, label: t('pages.gameDetail.tabs.faqs') },
    { key: 'sessions' as TabKey, label: t('pages.gameDetail.tabs.sessions') },
    { key: 'agents' as TabKey, label: t('pages.gameDetail.tabs.agents') },
    { key: 'documents' as TabKey, label: t('pages.gameDetail.tabs.documents') },
  ];
}

// ─── AgentsQuery → AgentsState mapping ─────────────────────────────────────

function deriveAgentsState(
  agentsQuery: ReturnType<typeof useGameAgents>,
  onRetry: () => void,
  onCreateAgent: () => void
): AgentsState {
  if (agentsQuery.isLoading) return { kind: 'loading' };
  if (agentsQuery.isError) return { kind: 'error', retry: onRetry };
  if (!agentsQuery.data || agentsQuery.data.length === 0) {
    return { kind: 'empty', ctaCreate: onCreateAgent };
  }
  const agents: GameDetailAgentEntry[] = agentsQuery.data.map(a => ({
    id: a.id,
    name: a.name,
    model: a.type ?? null,
    kbCount: 0,
    invocations: a.invocationCount,
    isActive: a.isActive,
  }));
  return { kind: 'success', agents };
}

// ─── Shells ────────────────────────────────────────────────────────────────

function LoadingShell({ label }: { label: string }): ReactElement {
  return (
    <div
      data-slot="game-detail-loading"
      aria-busy="true"
      aria-label={label}
      className="flex min-h-[60vh] flex-col gap-4 p-4 sm:p-8"
    >
      <div className="h-[240px] w-full animate-pulse rounded-2xl bg-muted" />
      <div className="h-12 w-full animate-pulse rounded-xl bg-muted" />
      <div className="h-40 w-full animate-pulse rounded-2xl bg-muted" />
    </div>
  );
}

function ErrorShell({
  title,
  subtitle,
  cta,
  onRetry,
}: {
  title: string;
  subtitle: string;
  cta: string;
  onRetry: () => void;
}): ReactElement {
  return (
    <div
      data-slot="game-detail-error"
      className="flex min-h-[60vh] flex-col items-center justify-center gap-4 p-4 text-center sm:p-8"
    >
      <span aria-hidden="true" className="text-4xl">
        ⚠️
      </span>
      <h2 className="font-display text-[20px] font-extrabold text-foreground">{title}</h2>
      <p className="max-w-sm text-[14px] text-muted-foreground">{subtitle}</p>
      <button
        type="button"
        onClick={onRetry}
        data-slot="game-detail-error-retry"
        className="rounded-md border border-border bg-transparent px-4 py-2 font-display text-[13px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {cta}
      </button>
    </div>
  );
}

function NotFoundShell({
  title,
  subtitle,
  cta,
}: {
  title: string;
  subtitle: string;
  cta: string;
}): ReactElement {
  return (
    <div
      data-slot="game-detail-not-found"
      className="flex min-h-[60vh] flex-col items-center justify-center gap-4 p-4 text-center sm:p-8"
    >
      <span aria-hidden="true" className="text-5xl">
        🎲
      </span>
      <h2 className="font-display text-[20px] font-extrabold text-foreground">{title}</h2>
      <p className="max-w-sm text-[14px] text-muted-foreground">{subtitle}</p>
      <a
        href="/games"
        data-slot="game-detail-not-found-cta"
        className="inline-flex items-center gap-1.5 rounded-md border-none bg-amber-700 px-4 py-2.5 font-display text-[13px] font-extrabold text-white shadow-md transition-shadow hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {cta}
      </a>
    </div>
  );
}

// ─── Hero meta builder ─────────────────────────────────────────────────────

function buildHeroMeta(detail: LibraryGameDetail): GameDetailHeroMeta {
  const { minPlayers, maxPlayers, playingTimeMinutes, complexityRating, averageRating } = detail;

  let players: string | null = null;
  if (minPlayers != null && maxPlayers != null) {
    players = minPlayers === maxPlayers ? String(minPlayers) : `${minPlayers}–${maxPlayers}`;
  }

  const duration = playingTimeMinutes != null ? `${playingTimeMinutes}m` : null;
  const complexity = complexityRating != null ? String(complexityRating.toFixed(1)) : null;
  const rating = averageRating != null ? String(averageRating.toFixed(1)) : null;

  const designerName = detail.designers?.[0]?.name ?? null;

  return {
    designer: designerName,
    year: detail.gameYearPublished ?? null,
    players,
    duration,
    complexity,
    rating,
  };
}

// ─── KPI cards builder ─────────────────────────────────────────────────────

function buildKpiCards(
  detail: LibraryGameDetail,
  t: ReturnType<typeof useTranslation>['t']
): ReadonlyArray<{
  key: string;
  label: string;
  value: string;
  unit?: string;
  accent?: 'rating' | 'complexity' | 'players' | 'time';
}> {
  const na = t('pages.gameDetail.kpi.notAvailable');
  return [
    {
      key: 'rating',
      label: t('pages.gameDetail.kpi.rating'),
      value: detail.averageRating != null ? String(detail.averageRating.toFixed(1)) : na,
      unit: detail.averageRating != null ? t('pages.gameDetail.kpi.ratingUnit') : undefined,
      accent: 'rating' as const,
    },
    {
      key: 'complexity',
      label: t('pages.gameDetail.kpi.complexity'),
      value: detail.complexityRating != null ? String(detail.complexityRating.toFixed(1)) : na,
      unit: detail.complexityRating != null ? t('pages.gameDetail.kpi.complexityUnit') : undefined,
      accent: 'complexity' as const,
    },
    {
      key: 'players',
      label: t('pages.gameDetail.kpi.players'),
      value:
        detail.minPlayers != null && detail.maxPlayers != null
          ? detail.minPlayers === detail.maxPlayers
            ? String(detail.minPlayers)
            : `${detail.minPlayers}–${detail.maxPlayers}`
          : na,
      accent: 'players' as const,
    },
    {
      key: 'time',
      label: t('pages.gameDetail.kpi.playTime'),
      value: detail.playingTimeMinutes != null ? String(detail.playingTimeMinutes) : na,
      unit: detail.playingTimeMinutes != null ? t('pages.gameDetail.kpi.playTimeUnit') : undefined,
      accent: 'time' as const,
    },
  ];
}

// ─── Main orchestrator ─────────────────────────────────────────────────────

/**
 * GameDetailView — orchestrator for /games/[id] v2 surface.
 *
 * Accepts `gameId: string | null` (normalized at page boundary by page.tsx).
 * Never accepts undefined — page.tsx is responsible for the normalization:
 *   const rawId = params?.id;
 *   const gameId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;
 */
export function GameDetailView({ gameId }: GameDetailViewProps): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const searchParams = useSearchParams();

  // ── Tab state ────────────────────────────────────────────────────────────
  const [tab, setTab] = useState<TabKey>('info');

  // ── URL override hatch ───────────────────────────────────────────────────
  const stateOverride = parseStateOverride(searchParams.get('state'));

  // ── Visual fixture short-circuit (CI prod build) ─────────────────────────
  // Fixture is loaded when IS_VISUAL_TEST_BUILD === true and override is not
  // 'not-found' or 'empty'. Dead-code-eliminated in production builds.
  const fixture = useMemo(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    if (stateOverride === 'not-found' || stateOverride === 'empty') return null;
    return tryLoadVisualTestFixture('default');
  }, [stateOverride]);

  // ── Parent hook (Phase 0.5 sez. 2.2) ────────────────────────────────────
  // ⚠️ CRITICAL: gameId is string|null. Hook accepts string but gates internally
  // via `enabled && !!gameId`. We pass gameId ?? '' to satisfy the type while
  // keeping the null-safe gate. The empty string is falsy → enabled=false.
  const detailQuery = useLibraryGameDetail(gameId ?? '');

  // G1 SMART goal · POST /api/v1/library/games/{gameId} con toast + redirect
  // alla canonical detail page. Idempotency: heroVariant === 'community' è già
  // false quando l'utente possiede già l'entry (libraryEntryId presente).
  const addToLibrary = useAddGameToLibrary();

  // ── Sub-hook (Phase 0.5 sez. 2.2 — lazy, gated by parent + tab) ─────────
  // ⚠️ CRITICAL: enabled MUST be: !!gameId && detailQuery.isSuccess && detailQuery.data != null && tab === 'agents'
  // Cell 4 guard: even if isSuccess=true and tab='agents', do NOT fetch agents
  // when detail=null (game not found in library). Without this, the agents
  // sub-hook would fire a real network request for a non-existent game.
  // NOTE: data != null check is NOT the PR #697 fixture-bypass anti-pattern —
  // that anti-pattern concerned bypassing real queries with fixture data. This
  // is a legitimate guard against Cell 4 race (isSuccess=true, data=null).
  const agentsQuery = useGameAgents({
    gameId,
    // Cell 4 guard: even if isSuccess=true and tab='agents', do NOT fetch agents
    // when detail=null (game not found in library). Without this, the agents
    // sub-hook would fire a real network request for a non-existent game.
    enabled: !!gameId && detailQuery.isSuccess && detailQuery.data != null && tab === 'agents',
  });

  // ── Effective detail (fixture takes priority over real data) ─────────────
  const detail = fixture ?? detailQuery.data ?? null;

  // ── FSM state derivation ─────────────────────────────────────────────────
  const realKind = useMemo(() => {
    // Fixture overrides the FSM when IS_VISUAL_TEST_BUILD — render 'default' directly.
    if (fixture != null) return 'default' as const;
    return deriveGameDetailUiState({
      gameId,
      isLoading: detailQuery.isLoading,
      isError: detailQuery.isError,
      hasData: detailQuery.data != null,
    });
  }, [fixture, gameId, detailQuery.isLoading, detailQuery.isError, detailQuery.data]);

  // URL override maps 'empty' → 'not-found' (alias per contract sez. 3)
  const effectiveKind =
    stateOverride === 'empty' ? 'not-found' : stateOverride != null ? stateOverride : realKind;

  // ── Shell renders (FSM cells 1, 2, 3, 4) ─────────────────────────────────
  if (effectiveKind === 'loading') {
    return <LoadingShell label={t('pages.gameDetail.states.loading.ariaLabel')} />;
  }

  if (effectiveKind === 'error') {
    return (
      <ErrorShell
        title={t('pages.gameDetail.states.error.title')}
        subtitle={t('pages.gameDetail.states.error.subtitle')}
        cta={t('pages.gameDetail.states.error.cta')}
        onRetry={() => detailQuery.refetch()}
      />
    );
  }

  if (effectiveKind === 'not-found') {
    return (
      <NotFoundShell
        title={t('pages.gameDetail.states.notFound.title')}
        subtitle={t('pages.gameDetail.states.notFound.subtitle')}
        cta={t('pages.gameDetail.states.notFound.cta')}
      />
    );
  }

  // ── Default render — detail guaranteed non-null (FSM cells 5-9) ──────────
  // TypeScript: effectiveKind === 'default' → real FSM ensures detail != null
  // fixture branch also guarantees detail != null
  const safeDetail = detail!;

  const tabsConfig = buildTabsConfig(t);
  const heroMeta = buildHeroMeta(safeDetail);
  const kpiCards = buildKpiCards(safeDetail, t);

  const heroVariant = safeDetail.libraryEntryId ? 'own' : 'community';
  const heroLabels = {
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
  };

  // Note: openAriaLabel / indexedLabel / invocationsLabel are used by GameDetailAgentsList
  // via .replace('{name}', ...) / .replace('{count}', ...). We pass raw template strings
  // rather than passing through t() which would try to ICU-format missing variables.
  const agentsLabels = {
    title: t('pages.gameDetail.agents.title'),
    subtitle: t('pages.gameDetail.agents.subtitle'),
    loadingLabel: t('pages.gameDetail.states.loading.ariaLabel'),
    errorLabel: t('pages.gameDetail.states.error.title'),
    errorSubtitle: t('pages.gameDetail.states.error.subtitle'),
    retryLabel: t('pages.gameDetail.states.error.cta'),
    empty: t('pages.gameDetail.agents.empty'),
    emptySubtitle: t('pages.gameDetail.agents.emptySubtitle'),
    createCta: t('pages.gameDetail.agents.createCta'),
    // Template strings: component calls .replace('{name}', agentName) / .replace('{count}', n)
    openAriaLabel: 'Apri agente {name}',
    indexedLabel: '{count} KB indicizzati',
    invocationsLabel: '{count} invocazioni',
  };

  const faqLabels = {
    title: t('pages.gameDetail.faqs.title'),
    subtitle: t('pages.gameDetail.faqs.subtitle'),
    viewAll: t('pages.gameDetail.faqs.viewAll'),
    viewAllAriaLabel: t('pages.gameDetail.faqs.viewAllAriaLabel'),
    empty: t('pages.gameDetail.faqs.empty'),
    // Template string: component calls .replace('{question}', ...)
    questionAriaLabel: 'Domanda: {question}',
  };

  const rulesLabels = {
    title: t('pages.gameDetail.rules.title'),
    subtitle: t('pages.gameDetail.rules.subtitle'),
    viewAll: t('pages.gameDetail.rules.viewAll'),
    viewAllAriaLabel: t('pages.gameDetail.rules.viewAllAriaLabel'),
    empty: t('pages.gameDetail.rules.empty'),
  };

  const sessionsLabels = {
    title: t('pages.gameDetail.sessions.title'),
    subtitle: t('pages.gameDetail.sessions.subtitle'),
    viewAll: t('pages.gameDetail.sessions.viewAll'),
    viewAllAriaLabel: t('pages.gameDetail.sessions.viewAllAriaLabel'),
    empty: t('pages.gameDetail.sessions.empty'),
    emptySubtitle: t('pages.gameDetail.sessions.emptySubtitle'),
    newSession: t('pages.gameDetail.sessions.newSession'),
    winLabel: t('pages.gameDetail.sessions.winLabel'),
    lossLabel: t('pages.gameDetail.sessions.lossLabel'),
  };

  const kbDocLabels = {
    title: t('pages.gameDetail.documents.title'),
    subtitle: t('pages.gameDetail.documents.subtitle'),
    empty: t('pages.gameDetail.documents.empty'),
    emptySubtitle: t('pages.gameDetail.documents.emptySubtitle'),
    uploadCta: t('pages.gameDetail.documents.uploadCta'),
    openCta: t('pages.gameDetail.documents.openCta'),
    // Template string: component calls .replace('{name}', ...)
    openAriaLabel: 'Apri documento {name}',
    statusIndexed: t('pages.gameDetail.documents.statusIndexed'),
    statusProcessing: t('pages.gameDetail.documents.statusProcessing'),
    statusFailed: t('pages.gameDetail.documents.statusFailed'),
    // Template string: component calls .replace('{pages}', ...) etc.
    statsLineTemplate: '{pages} pag · {chunks} chunk · {size}',
  };

  const agentsState: AgentsState = deriveAgentsState(
    agentsQuery,
    () => agentsQuery.refetch(),
    () => router.push('/agents/new')
  );

  // Sessions from recent sessions fixture/data
  const recentSessions = (safeDetail.recentSessions ?? []).map(s => ({
    id: s.id,
    playedAt: s.playedAt,
    durationFormatted: s.durationFormatted,
    didWin: s.didWin,
    playersLine: s.players ?? '',
  }));

  return (
    <div data-slot="game-detail-view" className="flex min-h-screen flex-col bg-background">
      {/* Hero (A11y: aria-label on section inside component) */}
      <GameDetailHero
        title={safeDetail.gameTitle}
        imageUrl={safeDetail.gameImageUrl ?? null}
        variant={heroVariant}
        meta={heroMeta}
        isFavorite={safeDetail.isFavorite}
        labels={heroLabels}
        onBack={() => router.push('/games')}
        onPlay={
          heroVariant === 'own' ? () => router.push(`/sessions/new?gameId=${gameId}`) : undefined
        }
        // Edit is admin-only; on /games/{id} (catalog/library view) we don't expose it
        // (the route /library/{id}/edit doesn't exist, and SharedGame edit is in /admin/shared-games/{id})
        onEdit={undefined}
        onShare={() => {
          if (typeof navigator !== 'undefined' && navigator.share) {
            void navigator.share({ title: safeDetail.gameTitle, url: window.location.href });
          }
        }}
        onAddToLibrary={
          heroVariant === 'community' && gameId
            ? () => {
                if (addToLibrary.isPending) return;
                addToLibrary.mutate(
                  { gameId },
                  {
                    onSuccess: () => {
                      toast.success(`${safeDetail.gameTitle} aggiunto alla tua libreria.`);
                      router.push(`/library/${gameId}`);
                    },
                    onError: error => {
                      toast.error(
                        error instanceof Error
                          ? error.message
                          : 'Impossibile aggiungere il gioco. Riprova.'
                      );
                    },
                  }
                );
              }
            : undefined
        }
      />

      {/* Tab navigation — A11y: role=tablist inside component, role=tabpanel on panels below */}
      <GameDetailTabsAnimated
        tabs={tabsConfig}
        active={tab}
        onChange={setTab}
        ariaLabel={t('pages.gameDetail.tabs.ariaLabel')}
      />

      {/* Tab panels — role=tabpanel, aria-labelledby wired to tabIdFor */}
      <div className="container mx-auto max-w-4xl px-4 py-6 sm:px-8">
        {/* Cell 5: info tab */}
        <div
          role="tabpanel"
          id={panelIdFor('info')}
          aria-labelledby={tabIdFor('info')}
          hidden={tab !== 'info'}
          data-slot="game-detail-panel-info"
        >
          <div className="flex flex-col gap-6">
            <GameDetailKpiCards cards={kpiCards} />
            {/* FAQ inline preview (CTA links to subroute) */}
            <GameDetailFaqList faqs={[]} viewAllHref={`/games/${gameId}/faqs`} labels={faqLabels} />
          </div>
        </div>

        {/* Rules tab */}
        <div
          role="tabpanel"
          id={panelIdFor('rules')}
          aria-labelledby={tabIdFor('rules')}
          hidden={tab !== 'rules'}
          data-slot="game-detail-panel-rules"
        >
          <GameDetailRulesAccordion
            sections={[]}
            viewAllHref={`/games/${gameId}/rules`}
            labels={rulesLabels}
          />
        </div>

        {/* FAQs tab */}
        <div
          role="tabpanel"
          id={panelIdFor('faqs')}
          aria-labelledby={tabIdFor('faqs')}
          hidden={tab !== 'faqs'}
          data-slot="game-detail-panel-faqs"
        >
          <GameDetailFaqList faqs={[]} viewAllHref={`/games/${gameId}/faqs`} labels={faqLabels} />
        </div>

        {/* Sessions tab */}
        <div
          role="tabpanel"
          id={panelIdFor('sessions')}
          aria-labelledby={tabIdFor('sessions')}
          hidden={tab !== 'sessions'}
          data-slot="game-detail-panel-sessions"
        >
          <GameDetailSessionsRail
            sessions={recentSessions}
            viewAllHref={`/games/${gameId}/sessions`}
            labels={sessionsLabels}
            onNewSession={() => router.push(`/sessions/new?gameId=${gameId}`)}
          />
        </div>

        {/* Agents tab — FSM cells 5-9 */}
        <div
          role="tabpanel"
          id={panelIdFor('agents')}
          aria-labelledby={tabIdFor('agents')}
          hidden={tab !== 'agents'}
          data-slot="game-detail-panel-agents"
        >
          <GameDetailAgentsList state={agentsState} labels={agentsLabels} />
        </div>

        {/* Documents tab */}
        <div
          role="tabpanel"
          id={panelIdFor('documents')}
          aria-labelledby={tabIdFor('documents')}
          hidden={tab !== 'documents'}
          data-slot="game-detail-panel-documents"
        >
          <GameDetailKbDocList docs={[]} labels={kbDocLabels} />
        </div>
      </div>
    </div>
  );
}
