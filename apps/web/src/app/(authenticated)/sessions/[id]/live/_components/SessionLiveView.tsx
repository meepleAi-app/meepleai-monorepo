/**
 * SessionLiveView — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Orchestrator for `/sessions/[id]/live` — static fixture mode.
 *
 * **Foundation scope (NO SSE wiring)**:
 *   - Reads data from VISUAL_TEST_FIXTURE_SESSION (IS_VISUAL_TEST_BUILD=true) or
 *     useSession(sessionId) (real backend).
 *   - No useSessionLiveStream — Interactions sub-PR adds real-time SSE.
 *   - All write handlers are absent (aria-disabled on child components).
 *
 * **URL state SSOT** (no useState mirrors):
 *   ?tab=tools|chat|notes (default 'tools')   — desktop right-column tab
 *   ?mtab=score|log|tools|chat (default 'score') — mobile bottom nav tab
 *   ?fixture=spectator|host|paused             — fixture variant (visual baselines)
 *   ?state=loading|not-found                   — override gated by STATE_OVERRIDE_ENABLED
 *   ?dialog=pause|endgame                      — dialog state (Foundation: derived, not rendered)
 *
 * **Gate A (ICU plural)**:
 *   `pages.sessionLive.topBar.turnLabel` has `{count, plural, ...}` — resolved here via
 *   `t(key, { count, total })`. Components receive pre-resolved strings, never ICU templates.
 *
 * **Dark theme default**:
 *   Root container carries `data-theme="dark"` for visual baselines.
 *
 * **4-state FSM** (per contract §4.1):
 *   loading | error | not-found | default
 *
 * **Subroutes preserved**:
 *   `/sessions/[id]` (D.3 summary) and `/sessions/[id]/diary/*` are UNTOUCHED.
 *
 * Pattern blueprint: Wave D.1 SessionsLibraryView + Wave C.1 AgentDetailView.
 * Wave D.2 Foundation sub-PR — Issue #746
 */

'use client';

import { useCallback, useMemo, type ReactElement } from 'react';

import { useParams, usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useIntl } from 'react-intl';

import {
  ActionLogTimeline,
  DesktopBody,
  LiveScoringPanel,
  LiveTopBar,
  MobileBody,
  PlayerRosterLive,
  TurnIndicator,
  type ActionLogTimelineLabels,
  type LiveScoringPanelLabels,
  type LiveScoringPanelScoreEntry,
  type LiveTopBarLabels,
  type MobileBodyLabels,
  type MobileTab,
  type PlayerRosterLiveLabels,
  type TurnIndicatorLabels,
} from '@/components/v2/session-live';
import { useSession } from '@/hooks/queries/useActiveSessions';
import { useTranslation } from '@/hooks/useTranslation';
import {
  deriveSessionLiveUiState,
  deriveSessionLiveDialogState,
  parseStateOverride,
  type SessionLiveUiState,
  type SessionLiveDialogState,
} from '@/lib/session-live/session-live-state';
import {
  IS_VISUAL_TEST_BUILD,
  STATE_OVERRIDE_ENABLED,
  VISUAL_TEST_FIXTURE_SESSION,
  VISUAL_TEST_FIXTURE_SESSION_AS_HOST,
  VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR,
  VISUAL_TEST_FIXTURE_SESSION_PAUSED,
  type LiveSessionFixture,
} from '@/lib/session-live/session-live-visual-test-fixture';

// ─── SessionId validation ─────────────────────────────────────────────────────
// Contract §2.1: never pass undefined or literal 'undefined' to sub-hooks.
// rawId from useParams may be undefined during pre-hydration (Next.js 16).

function resolveSessionId(rawId: string | undefined | null): string | null {
  return typeof rawId === 'string' && rawId.length > 0 ? rawId : null;
}

// ─── Fixture variant selection ────────────────────────────────────────────────

function resolveFixtureVariant(variantParam: string | null): LiveSessionFixture {
  if (variantParam === 'host') return VISUAL_TEST_FIXTURE_SESSION_AS_HOST;
  if (variantParam === 'spectator') return VISUAL_TEST_FIXTURE_SESSION_AS_SPECTATOR;
  if (variantParam === 'paused') return VISUAL_TEST_FIXTURE_SESSION_PAUSED;
  return VISUAL_TEST_FIXTURE_SESSION; // default: Player role, InProgress
}

// ─── Desktop live tab types ───────────────────────────────────────────────────

type LiveTab = 'tools' | 'chat' | 'notes';

function parseLiveTab(raw: string | null): LiveTab {
  if (raw === 'chat' || raw === 'notes') return raw;
  return 'tools'; // default
}

function parseMobileTab(raw: string | null): MobileTab {
  if (raw === 'log' || raw === 'tools' || raw === 'chat') return raw;
  return 'score'; // default
}

// ─── Skeleton shell components ────────────────────────────────────────────────
// Foundation: simple inline skeletons — no external component dependency.

function LoadingShell({ ariaLabel }: { ariaLabel: string }): ReactElement {
  return (
    <div
      data-slot="session-live-loading"
      role="status"
      aria-label={ariaLabel}
      aria-live="polite"
      className="flex flex-1 flex-col gap-4 p-4 animate-pulse"
    >
      {/* Desktop: 3-column skeleton */}
      <div className="hidden lg:flex flex-1 gap-4">
        <div className="w-[280px] shrink-0 rounded-lg bg-slate-700/40 h-64" />
        <div className="flex-1 rounded-lg bg-slate-700/40 h-64" />
        <div className="w-[340px] shrink-0 rounded-lg bg-slate-700/40 h-64" />
      </div>
      {/* Mobile: single-column skeleton */}
      <div className="flex flex-col gap-4 lg:hidden">
        <div className="h-32 rounded-lg bg-slate-700/40" />
        <div className="h-48 rounded-lg bg-slate-700/40" />
      </div>
    </div>
  );
}

function ErrorShell({
  title,
  description,
  ctaRetry,
  onRetry,
}: {
  title: string;
  description: string;
  ctaRetry: string;
  onRetry: () => void;
}): ReactElement {
  return (
    <div
      data-slot="session-live-error"
      className="flex flex-1 flex-col items-center justify-center gap-4 p-8 text-center"
    >
      <p className="text-lg font-semibold text-slate-200">{title}</p>
      <p className="text-sm text-slate-400">{description}</p>
      <button
        type="button"
        onClick={onRetry}
        data-slot="session-live-error-retry"
        className="rounded-lg bg-[hsl(240,60%,45%)] px-6 py-2 text-sm font-semibold text-white
          hover:bg-[hsl(240,60%,38%)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(240,60%,72%)]"
      >
        {ctaRetry}
      </button>
    </div>
  );
}

function NotFoundShell({
  title,
  description,
  ctaBack,
  onBack,
}: {
  title: string;
  description: string;
  ctaBack: string;
  onBack: () => void;
}): ReactElement {
  return (
    <div
      data-slot="session-live-not-found"
      className="flex flex-1 flex-col items-center justify-center gap-4 p-8 text-center"
    >
      <p className="text-lg font-semibold text-slate-200">{title}</p>
      <p className="text-sm text-slate-400">{description}</p>
      <button
        type="button"
        onClick={onBack}
        data-slot="session-live-not-found-cta"
        className="rounded-lg bg-[hsl(240,60%,45%)] px-6 py-2 text-sm font-semibold text-white
          hover:bg-[hsl(240,60%,38%)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(240,60%,72%)]"
      >
        {ctaBack}
      </button>
    </div>
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export function SessionLiveView(): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const params = useParams<{ id: string }>();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  // ── SessionId validation (contract §2.1) ─────────────────────────────────
  const sessionId = resolveSessionId(params?.id);

  // ── URL state SSOT ────────────────────────────────────────────────────────
  // `tab` parsed but unused in Foundation (RightColumnTabs is Interactions sub-PR).
  // Reserved for forward compatibility — will be passed to RightColumnTabs once mounted.
  const _tab = parseLiveTab(searchParams.get('tab'));
  void _tab;
  const mobileTab = parseMobileTab(searchParams.get('mtab'));
  const fixtureVariantParam = searchParams.get('fixture');

  // State override hatch (dev/visual-test builds only)
  const stateOverride: SessionLiveUiState | null = STATE_OVERRIDE_ENABLED
    ? parseStateOverride(new URLSearchParams(searchParams.toString()))
    : null;

  // Dialog state derived from URL (Foundation: derived but no dialog components mounted)
  // Included so E2E ?state= override visual tests can verify dialog-state derivation.
  const _dialogState: SessionLiveDialogState = deriveSessionLiveDialogState(
    new URLSearchParams(searchParams.toString())
  );
  void _dialogState; // Interactions sub-PR mounts actual dialog components

  // ── Data: fixture or real hook ────────────────────────────────────────────
  const fixture: LiveSessionFixture | null = useMemo(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    return resolveFixtureVariant(fixtureVariantParam);
  }, [fixtureVariantParam]);

  // Real data hook — disabled when fixture is active or sessionId is null
  const sessionQuery = useSession(
    sessionId ?? '',
    /* enabled= */ !IS_VISUAL_TEST_BUILD && sessionId != null
  );

  // ── FSM derivation ────────────────────────────────────────────────────────
  const realUiState = useMemo<SessionLiveUiState>(() => {
    if (fixture != null) return 'default'; // fixture always renders default shell
    return deriveSessionLiveUiState({
      sessionId,
      isLoading: sessionQuery.isLoading,
      isError: sessionQuery.isError,
      hasData: sessionQuery.data != null,
    });
  }, [fixture, sessionId, sessionQuery.isLoading, sessionQuery.isError, sessionQuery.data]);

  const effectiveUiState: SessionLiveUiState = stateOverride ?? realUiState;

  // ── Active fixture data (fixture or real session mapped to fixture shape) ─
  // Foundation: only fixture data is fully typed; real GameSessionDto lacks live fields.
  // Interactions sub-PR wires useSessionLiveStream + reducer for real live state.
  const activeSession: LiveSessionFixture | null = useMemo(() => {
    if (fixture != null) return fixture;
    // When real session is loaded (Cell 5), create a minimal fixture-shaped proxy
    // for Foundation display (no live fields — SSE wiring comes in Interactions).
    const dto = sessionQuery.data;
    if (dto == null) return null;
    return {
      id: dto.id,
      name: `Sessione ${dto.id.slice(0, 8)}`,
      status: (dto.status === 'Paused' ? 'Paused' : 'InProgress') as 'InProgress' | 'Paused',
      viewerRole: 'Player', // Foundation default — SSE wires real viewerRole
      viewerId: '',
      currentTurn: 0,
      totalTurns: 0,
      activePlayerId: '',
      players: dto.players.map((p, idx) => ({
        // SessionPlayerDto has no `id` field — generate a deterministic positional key.
        // SSE wiring (Interactions sub-PR) will replace this with real participant IDs.
        id: `player-order-${p.playerOrder ?? idx}`,
        name: p.playerName,
        role: 'Player' as const,
        score: 0,
        isOnline: true,
      })),
      actionLog: [],
    };
  }, [fixture, sessionQuery.data]);

  // ── Navigation handlers ───────────────────────────────────────────────────

  /** Build a new search string preserving params not in overrides. */
  const buildQuery = useCallback(
    (overrides: Partial<Record<string, string | null>>): string => {
      const next = new URLSearchParams(searchParams.toString());
      Object.entries(overrides).forEach(([k, v]) => {
        if (v == null || v === '') next.delete(k);
        else next.set(k, v);
      });
      const qs = next.toString();
      return qs ? `?${qs}` : '';
    },
    [searchParams]
  );

  // handleTabChange reserved for Interactions sub-PR (RightColumnTabs orchestration)
  const _handleTabChange = useCallback(
    (next: LiveTab) => {
      const val = next === 'tools' ? null : next;
      router.replace(`${pathname}${buildQuery({ tab: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );
  void _handleTabChange;

  const handleMobileTabChange = useCallback(
    (next: MobileTab) => {
      const val = next === 'score' ? null : next;
      router.replace(`${pathname}${buildQuery({ mtab: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  /** Exit handler: navigate to /sessions/{sessionId} (D.3 summary route). */
  const handleExit = useCallback(() => {
    router.push(sessionId ? `/sessions/${sessionId}` : '/sessions');
  }, [router, sessionId]);

  const handleRetry = useCallback(() => {
    void sessionQuery.refetch?.();
  }, [sessionQuery]);

  const handleBack = useCallback(() => {
    router.push('/sessions');
  }, [router]);

  // ── i18n labels ───────────────────────────────────────────────────────────
  // Gate A: ICU plural keys resolved here — never in child components.
  // t(key, { count }) delegates to react-intl formatter which handles {count, plural, ...}.

  const topBarLabels = useMemo<LiveTopBarLabels>((): LiveTopBarLabels => {
    const currentTurn = activeSession?.currentTurn ?? 0;
    const totalTurns = activeSession?.totalTurns ?? 0;
    const sessionName = activeSession?.name ?? '';
    return {
      // Gate A: sessionTitleAriaLabel has {name} interpolation — resolved here.
      // LiveTopBar also does `.replace('{name}', sessionName)` as a secondary guard;
      // by pre-resolving we avoid formatjs missing-variable console errors in tests.
      sessionTitleAriaLabel: t('pages.sessionLive.topBar.sessionTitleAriaLabel', {
        name: sessionName,
      }),
      // Gate A: ICU plural resolved here with { count, total } values
      turnLabelResolved: t('pages.sessionLive.topBar.turnLabel', {
        count: currentTurn,
        total: totalTurns,
      }),
      statusInProgress: t('pages.sessionLive.topBar.statusInProgress'),
      statusPaused: t('pages.sessionLive.topBar.statusPaused'),
      pauseCta: t('pages.sessionLive.topBar.pauseCta'),
      resumeCta: t('pages.sessionLive.topBar.resumeCta'),
      endgameCta: t('pages.sessionLive.topBar.endgameCta'),
      exitAriaLabel: t('pages.sessionLive.topBar.exitAriaLabel'),
    };
  }, [t, activeSession?.currentTurn, activeSession?.totalTurns, activeSession?.name]);

  const turnIndicatorLabels = useMemo<TurnIndicatorLabels>(
    (): TurnIndicatorLabels => ({
      // currentTurnAriaLabel has {current}/{total} — raw template, TurnIndicator does .replace()
      currentTurnAriaLabel:
        (intl.messages['pages.sessionLive.turnIndicator.currentTurnAriaLabel'] as string) ??
        'Turno {current} di {total}',
      // activePlayerLabel has {playerName} — raw template, component does .replace()
      activePlayerLabel:
        (intl.messages['pages.sessionLive.turnIndicator.activePlayerLabel'] as string) ??
        '{playerName}',
      yourTurnLabel: t('pages.sessionLive.turnIndicator.yourTurnLabel'),
      waitingLabel: t('pages.sessionLive.turnIndicator.waitingLabel'),
    }),
    [t, intl.messages]
  );

  const rosterLabels = useMemo<PlayerRosterLiveLabels>((): PlayerRosterLiveLabels => {
    const playerCount = activeSession?.players.length ?? 0;
    return {
      title: t('pages.sessionLive.roster.title'),
      // Gate A: ICU plural resolved here with { count } value
      playerCountResolved: t('pages.sessionLive.roster.playerCountTemplate', {
        count: playerCount,
      }),
      onlineLabel: t('pages.sessionLive.roster.onlineLabel'),
      offlineLabel: t('pages.sessionLive.roster.offlineLabel'),
      // kickAriaLabelTemplate has {playerName} — raw template, component does .replace()
      kickAriaLabelTemplate:
        (intl.messages['pages.sessionLive.roster.kickAriaLabel'] as string) ??
        'Espelli {playerName}',
      roleSpectator: t('pages.sessionLive.roster.roleSpectator'),
      rolePlayer: t('pages.sessionLive.roster.rolePlayer'),
      roleHost: t('pages.sessionLive.roster.roleHost'),
    };
  }, [t, intl.messages, activeSession?.players.length]);

  const scoringLabels = useMemo<LiveScoringPanelLabels>(
    (): LiveScoringPanelLabels => ({
      title: t('pages.sessionLive.scoring.title'),
      // Template strings with {score}/{playerName} — raw templates, component does .replace()
      scoreLabelTemplate:
        (intl.messages['pages.sessionLive.scoring.scoreLabel'] as string) ?? 'Punteggio: {score}',
      winnerLabel: t('pages.sessionLive.scoring.winnerLabel'),
      myScoreLabel: t('pages.sessionLive.scoring.myScoreLabel'),
      incrementAriaLabelTemplate:
        (intl.messages['pages.sessionLive.scoring.incrementAriaLabel'] as string) ??
        'Aumenta punteggio di {playerName}',
      decrementAriaLabelTemplate:
        (intl.messages['pages.sessionLive.scoring.decrementAriaLabel'] as string) ??
        'Diminuisci punteggio di {playerName}',
      scoreInputAriaLabelTemplate:
        (intl.messages['pages.sessionLive.scoring.scoreInputAriaLabel'] as string) ??
        'Inserisci punteggio per {playerName}',
    }),
    [t, intl.messages]
  );

  const actionLogLabels = useMemo<ActionLogTimelineLabels>(
    (): ActionLogTimelineLabels => ({
      title: t('pages.sessionLive.actionLog.title'),
      emptyLabel: t('pages.sessionLive.actionLog.emptyLabel'),
      typeScore: t('pages.sessionLive.actionLog.typeScore'),
      typeTool: t('pages.sessionLive.actionLog.typeTool'),
      typeAgent: t('pages.sessionLive.actionLog.typeAgent'),
      typeChat: t('pages.sessionLive.actionLog.typeChat'),
      typePhoto: t('pages.sessionLive.actionLog.typePhoto'),
      typeEvent: t('pages.sessionLive.actionLog.typeEvent'),
      timestampAriaLabel: t('pages.sessionLive.actionLog.timestampAriaLabel'),
    }),
    [t]
  );

  const mobileBodyLabels = useMemo<MobileBodyLabels>(
    (): MobileBodyLabels => ({
      tabScore: t('pages.sessionLive.scoring.title'),
      tabLog: t('pages.sessionLive.actionLog.title'),
      tabTools: t('pages.sessionLive.topBar.endgameCta'), // placeholder; Interactions sub-PR refines
      tabChat: t('pages.sessionLive.connectionLost.manualRetryLabel'), // placeholder
      bottomNavAriaLabel: t('pages.sessionLive.a11y.viewLabel'),
    }),
    [t]
  );

  // ── Derived data for components ───────────────────────────────────────────

  const scores = useMemo<ReadonlyArray<LiveScoringPanelScoreEntry>>(() => {
    if (activeSession == null) return [];
    return activeSession.players.map(p => ({
      playerId: p.id,
      playerName: p.name,
      score: p.score,
      isWinner: false,
    }));
  }, [activeSession]);

  const isMyTurn = useMemo<boolean>(() => {
    if (activeSession == null) return false;
    return (
      activeSession.viewerRole === 'Player' &&
      activeSession.activePlayerId === activeSession.viewerId
    );
  }, [activeSession]);

  const activePlayerName = useMemo<string>(() => {
    if (activeSession == null) return '';
    const active = activeSession.players.find(p => p.id === activeSession.activePlayerId);
    return active?.name ?? '';
  }, [activeSession]);

  // Mobile content selection based on active tab.
  // MUST be declared BEFORE any early return per react-hooks/rules-of-hooks.
  // Returns null when activeSession not yet ready (loading/error/not-found shells use early returns below).
  const mobileContent = useMemo<React.ReactNode>(() => {
    if (activeSession == null) return null;
    if (mobileTab === 'log') {
      return <ActionLogTimeline entries={activeSession.actionLog} labels={actionLogLabels} />;
    }
    // Default and score tab: scoring panel
    return (
      <LiveScoringPanel
        scores={scores}
        viewerRole={activeSession.viewerRole}
        viewerId={activeSession.viewerId}
        labels={scoringLabels}
      />
    );
  }, [mobileTab, activeSession, scores, scoringLabels, actionLogLabels]);

  // ── Render ────────────────────────────────────────────────────────────────

  // FSM loading shell
  if (effectiveUiState === 'loading') {
    return (
      <div
        data-slot="session-live-view"
        data-ui-state="loading"
        data-theme="dark"
        className="flex flex-col min-h-screen bg-[hsl(240,40%,8%)]"
      >
        <LoadingShell ariaLabel={t('pages.sessionLive.loading.ariaLabel')} />
      </div>
    );
  }

  // FSM error shell
  if (effectiveUiState === 'error') {
    return (
      <div
        data-slot="session-live-view"
        data-ui-state="error"
        data-theme="dark"
        className="flex flex-col min-h-screen bg-[hsl(240,40%,8%)]"
      >
        <ErrorShell
          title={t('pages.sessionLive.error.title')}
          description={t('pages.sessionLive.error.description')}
          ctaRetry={t('pages.sessionLive.error.ctaRetry')}
          onRetry={handleRetry}
        />
      </div>
    );
  }

  // FSM not-found shell (Cells 1 and 4)
  if (effectiveUiState === 'not-found') {
    return (
      <div
        data-slot="session-live-view"
        data-ui-state="not-found"
        data-theme="dark"
        className="flex flex-col min-h-screen bg-[hsl(240,40%,8%)]"
      >
        <NotFoundShell
          title={t('pages.sessionLive.notFound.title')}
          description={t('pages.sessionLive.notFound.description')}
          ctaBack={t('pages.sessionLive.notFound.ctaBack')}
          onBack={handleBack}
        />
      </div>
    );
  }

  // FSM default shell (Cell 5) — requires activeSession
  if (activeSession == null) {
    // Guard: effectiveUiState='default' but activeSession null is a race guard.
    // Should not happen in practice; treat as loading.
    return (
      <div
        data-slot="session-live-view"
        data-ui-state="loading"
        data-theme="dark"
        className="flex flex-col min-h-screen bg-[hsl(240,40%,8%)]"
      >
        <LoadingShell ariaLabel={t('pages.sessionLive.loading.ariaLabel')} />
      </div>
    );
  }

  // ── Default content ───────────────────────────────────────────────────────
  // (mobileContent declared before early returns per react-hooks/rules-of-hooks)

  const desktopLeftSidebar = (
    <div className="flex flex-col gap-0 divide-y divide-slate-100">
      <div className="p-4">
        <TurnIndicator
          current={activeSession.currentTurn}
          total={activeSession.totalTurns}
          activePlayerName={activePlayerName}
          isMyTurn={isMyTurn}
          labels={turnIndicatorLabels}
        />
      </div>
      <div className="p-4">
        <PlayerRosterLive
          players={activeSession.players}
          viewerId={activeSession.viewerId}
          viewerRole={activeSession.viewerRole}
          labels={rosterLabels}
        />
      </div>
    </div>
  );

  const desktopCenterColumn = (
    <div className="flex flex-col gap-6">
      <LiveScoringPanel
        scores={scores}
        viewerRole={activeSession.viewerRole}
        viewerId={activeSession.viewerId}
        labels={scoringLabels}
      />
      <ActionLogTimeline entries={activeSession.actionLog} labels={actionLogLabels} />
    </div>
  );

  return (
    <div
      data-slot="session-live-view"
      data-ui-state="default"
      data-theme="dark"
      className="flex flex-col h-screen overflow-hidden bg-[hsl(240,40%,8%)] text-slate-100"
      aria-label={t('pages.sessionLive.a11y.viewLabel')}
    >
      {/* Sticky top bar */}
      <LiveTopBar
        sessionName={activeSession.name}
        status={activeSession.status}
        viewerRole={activeSession.viewerRole}
        onExit={handleExit}
        labels={topBarLabels}
      />

      {/* Desktop 3-column layout (lg+) */}
      <DesktopBody
        leftSidebar={desktopLeftSidebar}
        centerColumn={desktopCenterColumn}
        // rightColumn: Foundation placeholder (Interactions sub-PR: RightColumnTabs)
      />

      {/* Mobile single-column with bottom nav (< lg) */}
      <MobileBody
        activeTab={mobileTab}
        onTabChange={handleMobileTabChange}
        content={mobileContent}
        labels={mobileBodyLabels}
      />
    </div>
  );
}
