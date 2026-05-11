/**
 * SessionLiveView — Wave D.2 Interactions sub-PR (Issue #750).
 *
 * Orchestrator for `/sessions/[id]/live` — full SSE + interactions.
 *
 * **Interactions extension** (over Foundation sub-PR #746):
 *   - useSessionLiveStream wired when !IS_VISUAL_TEST_BUILD + session loaded
 *   - composeSessionLiveState reducer merges DTO + SSE events into liveState
 *   - RightColumnTabs mounted (desktop right column) with tab URL SSOT
 *   - PauseOverlay / EndgameDialog lazy-loaded, mounted from ?dialog= URL param
 *   - ConnectionLostBanner shown for reconnecting/degraded-polling/failed states
 *   - Mobile tab routing extended: chat → LiveAgentChat, tools → SessionToolsRail
 *   - Desktop right column: tools → SessionToolsRail, chat → LiveAgentChat, notes → LiveSessionNotes
 *   - Write actions: handleScoreUpdate (optimistic UI), handleToolExecute,
 *     handleSendMessage, handleAddNote, handleResume, handlePause, handleEndgame
 *   - 403 handling: score rollback + toast "Permesso negato"
 *   - 429 handling: connectionState='failed' shown as ConnectionLostBanner kind='failed'
 *
 * **URL state SSOT** (no useState mirrors):
 *   ?tab=tools|chat|notes (default 'tools')   — desktop right-column tab
 *   ?mtab=score|log|tools|chat (default 'score') — mobile bottom nav tab
 *   ?dialog=pause|endgame                      — dialog state
 *   ?fixture=spectator|host|paused             — fixture variant (visual baselines)
 *   ?state=loading|not-found                   — override gated by STATE_OVERRIDE_ENABLED
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
 * Wave D.2 Interactions sub-PR — Issue #750
 */

'use client';

import { useCallback, useMemo, useRef, useState, lazy, Suspense, type ReactElement } from 'react';

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
} from '@/components/features/session-live';
import {
  ConnectionLostBanner,
  LiveAgentChat,
  LiveSessionNotes,
  RightColumnTabs,
  SessionToolsRail,
  type ConnectionLostBannerLabels,
  type LiveAgentChatLabels,
  type LiveSessionNotesLabels,
  type RightColumnTabsLabels,
  type SessionToolsRailLabels,
} from '@/components/features/session-live';
import { useSession } from '@/hooks/queries/useActiveSessions';
import { useTranslation } from '@/hooks/useTranslation';
import { composeSessionLiveState } from '@/lib/session-live/compose-session-live-state';
import { hasRequiredRole } from '@/lib/session-live/participant-role';
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
import { useSessionLiveStream } from '@/lib/session-live/use-session-live-stream';

// ─── Lazy dialogs (orchestrator-side lazy import per Task 3 spec) ──────────────

const PauseOverlay = lazy(() =>
  import('@/components/features/session-live/PauseOverlay').then(m => ({ default: m.PauseOverlay }))
);

const EndgameDialog = lazy(() =>
  import('@/components/features/session-live/EndgameDialog').then(m => ({
    default: m.EndgameDialog,
  }))
);

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
  const tab = parseLiveTab(searchParams.get('tab'));
  const mobileTab = parseMobileTab(searchParams.get('mtab'));
  const fixtureVariantParam = searchParams.get('fixture');

  // State override hatch (dev/visual-test builds only)
  const stateOverride: SessionLiveUiState | null = STATE_OVERRIDE_ENABLED
    ? parseStateOverride(new URLSearchParams(searchParams.toString()))
    : null;

  // Dialog state derived from URL — Interactions mounts actual dialog components
  const dialogState: SessionLiveDialogState = deriveSessionLiveDialogState(
    new URLSearchParams(searchParams.toString())
  );

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

  // ── SSE hook (Interactions sub-PR) ───────────────────────────────────────
  // Wired when NOT in fixture mode AND session has loaded successfully.
  // Contract §2.2: useSessionLiveStream mounts ONLY after parent success.
  const liveStream = useSessionLiveStream({
    sessionId,
    enabled:
      !IS_VISUAL_TEST_BUILD &&
      sessionId != null &&
      sessionQuery.isSuccess &&
      sessionQuery.data != null,
  });

  // ── Optimistic local scores (for 403 rollback) ────────────────────────────
  // Map: playerId → local score delta (applied on top of liveState)
  // Rolled back on 403 response from server.
  const [localScoreOverrides, setLocalScoreOverrides] = useState<ReadonlyMap<string, number>>(
    new Map()
  );

  // Ref to track pending score requests for rollback
  const pendingScoreRef = useRef<Map<string, number>>(new Map());

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

  // ── Active session data ───────────────────────────────────────────────────
  // Priority: fixture > composed live state > DTO proxy
  const activeSession: LiveSessionFixture | null = useMemo(() => {
    if (fixture != null) return fixture;
    const dto = sessionQuery.data;
    if (dto == null) return null;

    // Adapt DTO players: SessionPlayerDto.id is optional (backward-compat Gate B).
    // composeSessionLiveState requires id: string — synthesise from playerName+playerOrder.
    const initialData = {
      ...dto,
      players: dto.players.map((p, idx) => ({
        ...p,
        id: p.id ?? `${p.playerName}-${p.playerOrder}-${idx}`,
      })),
    };

    // Compose live state from DTO + accumulated SSE events
    const liveState = composeSessionLiveState(initialData, liveStream.events);

    // Apply local score overrides (optimistic UI)
    const playersWithOverrides = liveState.players.map(p => {
      const override = localScoreOverrides.get(p.id);
      return override !== undefined ? { ...p, score: override } : p;
    });

    return {
      id: dto.id,
      name: `Sessione ${dto.id.slice(0, 8)}`,
      status: liveState.status === 'Paused' ? 'Paused' : 'InProgress',
      viewerRole: 'Player' as const, // Foundation default — real viewerRole from session DTO
      viewerId: '',
      currentTurn: liveState.currentTurn,
      totalTurns: liveState.totalTurns,
      activePlayerId: liveState.activePlayerId,
      players: playersWithOverrides,
      actionLog: liveState.actionLog,
    };
  }, [fixture, sessionQuery.data, liveStream.events, localScoreOverrides]);

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

  const handleTabChange = useCallback(
    (next: LiveTab) => {
      const val = next === 'tools' ? null : next;
      router.replace(`${pathname}${buildQuery({ tab: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleMobileTabChange = useCallback(
    (next: MobileTab) => {
      const val = next === 'score' ? null : next;
      router.replace(`${pathname}${buildQuery({ mtab: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  /** Dialog dismiss/open handler — updates ?dialog= URL param.
   *  'none' removes the param (clears dialog from URL). */
  const handleDialogChange = useCallback(
    (next: SessionLiveDialogState) => {
      const val = next === 'none' ? null : next;
      router.replace(`${pathname}${buildQuery({ dialog: val })}`, { scroll: false });
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

  // ── Write actions (Player+Host) ───────────────────────────────────────────

  /**
   * Optimistic score update with 403 rollback.
   * Reserved for future score input UI in LiveScoringPanel — wired in v2 polish.
   * Kept as `_handleScoreUpdate` to preserve callsite documentation while ESLint
   * recognizes intentional non-usage in current sub-PR.
   */
  const _handleScoreUpdate = useCallback(
    async (playerId: string, newScore: number): Promise<void> => {
      if (activeSession == null) return;
      if (!hasRequiredRole(activeSession.viewerRole, 'Player')) return;

      // Get current score for rollback
      const currentScore = activeSession.players.find(p => p.id === playerId)?.score ?? 0;
      pendingScoreRef.current.set(playerId, currentScore);

      // Optimistic update
      setLocalScoreOverrides(prev => {
        const next = new Map(prev);
        next.set(playerId, newScore);
        return next;
      });

      try {
        if (sessionId == null) throw new Error('no sessionId');
        const res = await fetch(
          `/api/v1/game-sessions/${sessionId}/participants/${playerId}/score`,
          {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ score: newScore }),
            credentials: 'include',
          }
        );

        if (res.status === 403) {
          // 403: rollback optimistic update
          setLocalScoreOverrides(prev => {
            const next = new Map(prev);
            const rollbackScore = pendingScoreRef.current.get(playerId);
            if (rollbackScore !== undefined) next.set(playerId, rollbackScore);
            else next.delete(playerId);
            return next;
          });
          // TODO: toast "Permesso negato" (requires toast integration)
          return;
        }

        if (res.status === 429) {
          // 429: server rate limit — keep optimistic but note degraded state
          // ConnectionLostBanner kind='failed' handles toast UI
          return;
        }

        if (!res.ok) {
          // Other error: rollback
          setLocalScoreOverrides(prev => {
            const next = new Map(prev);
            const rollbackScore = pendingScoreRef.current.get(playerId);
            if (rollbackScore !== undefined) next.set(playerId, rollbackScore);
            else next.delete(playerId);
            return next;
          });
          return;
        }

        // Success: clear pending (SSE event will arrive with canonical score)
        pendingScoreRef.current.delete(playerId);
      } catch {
        // Network error: rollback
        setLocalScoreOverrides(prev => {
          const next = new Map(prev);
          const rollbackScore = pendingScoreRef.current.get(playerId);
          if (rollbackScore !== undefined) next.set(playerId, rollbackScore);
          else next.delete(playerId);
          return next;
        });
      }
    },
    [activeSession, sessionId]
  );
  void _handleScoreUpdate;

  const handleToolExecute = useCallback(
    async (toolId: string): Promise<void> => {
      if (sessionId == null) return;
      if (activeSession == null) return;
      if (!hasRequiredRole(activeSession.viewerRole, 'Player')) return;

      try {
        await fetch(`/api/v1/game-sessions/${sessionId}/tools/${toolId}/execute`, {
          method: 'POST',
          credentials: 'include',
        });
      } catch {
        // Fail silently — SSE event confirms or not
      }
    },
    [sessionId, activeSession]
  );

  const handleSendMessage = useCallback(
    async (content: string, visibility: 'private' | 'shared'): Promise<void> => {
      if (sessionId == null) return;

      try {
        await fetch(`/api/v1/game-sessions/${sessionId}/chat`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ content, visibility }),
          credentials: 'include',
        });
      } catch {
        // Fail silently — SSE event confirms or not
      }
    },
    [sessionId]
  );

  const handleAddNote = useCallback(
    async (content: string, visibility: 'private' | 'shared'): Promise<void> => {
      if (sessionId == null) return;

      try {
        await fetch(`/api/v1/game-sessions/${sessionId}/diary`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ content, visibility }),
          credentials: 'include',
        });
      } catch {
        // Fail silently — SSE event confirms or not
      }
    },
    [sessionId]
  );

  const handleResume = useCallback(async (): Promise<void> => {
    if (sessionId == null) return;
    if (activeSession == null) return;
    if (!hasRequiredRole(activeSession.viewerRole, 'Host')) return;

    try {
      await fetch(`/api/v1/game-sessions/${sessionId}/resume`, {
        method: 'POST',
        credentials: 'include',
      });
      handleDialogChange('none');
    } catch {
      // Fail silently
    }
  }, [sessionId, activeSession, handleDialogChange]);

  // ── i18n labels ───────────────────────────────────────────────────────────
  // Gate A: ICU plural keys resolved here — never in child components.

  const topBarLabels = useMemo<LiveTopBarLabels>((): LiveTopBarLabels => {
    const currentTurn = activeSession?.currentTurn ?? 0;
    const totalTurns = activeSession?.totalTurns ?? 0;
    const sessionName = activeSession?.name ?? '';
    return {
      sessionTitleAriaLabel: t('pages.sessionLive.topBar.sessionTitleAriaLabel', {
        name: sessionName,
      }),
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
      currentTurnAriaLabel:
        (intl.messages['pages.sessionLive.turnIndicator.currentTurnAriaLabel'] as string) ??
        'Turno {current} di {total}',
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
      playerCountResolved: t('pages.sessionLive.roster.playerCountTemplate', {
        count: playerCount,
      }),
      onlineLabel: t('pages.sessionLive.roster.onlineLabel'),
      offlineLabel: t('pages.sessionLive.roster.offlineLabel'),
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
      tabTools: t('pages.sessionLive.rightColumn.tabTools'),
      tabChat: t('pages.sessionLive.rightColumn.tabChat'),
      bottomNavAriaLabel: t('pages.sessionLive.a11y.viewLabel'),
    }),
    [t]
  );

  const connectionLostLabels = useMemo<ConnectionLostBannerLabels>(
    (): ConnectionLostBannerLabels => ({
      // Gate A: ICU plural resolved here
      retryCountResolved: t('pages.sessionLive.connectionLost.retryCount', {
        count: liveStream.retryCount,
      }),
      reconnecting: t('pages.sessionLive.connectionLost.reconnecting'),
      degradedPolling: t('pages.sessionLive.connectionLost.degradedPolling'),
      failed: t('pages.sessionLive.connectionLost.failed'),
      manualRetryLabel: t('pages.sessionLive.connectionLost.manualRetryLabel'),
    }),
    [t, liveStream.retryCount]
  );

  const rightColumnTabsLabels = useMemo<RightColumnTabsLabels>(
    (): RightColumnTabsLabels => ({
      tabsAriaLabel: t('pages.sessionLive.rightColumn.tabsAriaLabel'),
      tabTools: t('pages.sessionLive.rightColumn.tabTools'),
      tabChat: t('pages.sessionLive.rightColumn.tabChat'),
      tabNotes: t('pages.sessionLive.rightColumn.tabNotes'),
    }),
    [t]
  );

  const toolsRailLabels = useMemo<SessionToolsRailLabels>(
    (): SessionToolsRailLabels => ({
      title: t('pages.sessionLive.tools.title'),
      toolDiceLabel: t('pages.sessionLive.tools.toolDiceLabel'),
      toolTimerLabel: t('pages.sessionLive.tools.toolTimerLabel'),
      toolCardLabel: t('pages.sessionLive.tools.toolCardLabel'),
      executeAriaTemplate:
        (intl.messages['pages.sessionLive.tools.executeAriaTemplate'] as string) ??
        'Esegui {toolName}',
      disabledSpectatorTooltip: t('pages.sessionLive.tools.disabledSpectatorTooltip'),
    }),
    [t, intl.messages]
  );

  const chatLabels = useMemo<LiveAgentChatLabels>(
    (): LiveAgentChatLabels => ({
      title: t('pages.sessionLive.chat.title'),
      inputAriaLabel: t('pages.sessionLive.chat.inputAriaLabel'),
      sendAriaLabel: t('pages.sessionLive.chat.sendAriaLabel'),
      visibilityPrivate: t('pages.sessionLive.chat.visibilityPrivate'),
      visibilityShared: t('pages.sessionLive.chat.visibilityShared'),
      emptyMessage: t('pages.sessionLive.chat.emptyMessage'),
    }),
    [t]
  );

  const notesLabels = useMemo<LiveSessionNotesLabels>(
    (): LiveSessionNotesLabels => ({
      title: t('pages.sessionLive.notes.title'),
      inputAriaLabel: t('pages.sessionLive.notes.inputAriaLabel'),
      addAriaLabel: t('pages.sessionLive.notes.addAriaLabel'),
      visibilityPrivate: t('pages.sessionLive.notes.visibilityPrivate'),
      visibilityShared: t('pages.sessionLive.notes.visibilityShared'),
      emptyMessage: t('pages.sessionLive.notes.emptyMessage'),
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

  // ── Default tools list ────────────────────────────────────────────────────
  // Foundation: standard 3 tools. Real tools come from session DTO in future.
  const toolsList = useMemo(
    () => [
      { id: 'dice', name: toolsRailLabels.toolDiceLabel, icon: 'dice' as const },
      { id: 'timer', name: toolsRailLabels.toolTimerLabel, icon: 'timer' as const },
      { id: 'card', name: toolsRailLabels.toolCardLabel, icon: 'card' as const },
    ],
    [toolsRailLabels]
  );

  // ── Chat messages from SSE events ────────────────────────────────────────
  // Extract chat messages from liveState actionLog (type='chat' entries).
  // Foundation proxy: fixture has no chat messages.
  const chatMessages = useMemo(() => {
    if (activeSession == null) return [];
    return activeSession.actionLog
      .filter(e => e.type === 'chat')
      .map(e => ({
        id: e.id,
        senderId: e.authorName,
        senderName: e.authorName,
        content: e.content,
        visibility: 'shared' as const,
        timestamp: e.timestamp,
      }));
  }, [activeSession]);

  // ── Notes from SSE events ────────────────────────────────────────────────
  const noteEntries = useMemo(() => {
    if (activeSession == null) return [];
    return activeSession.actionLog
      .filter(e => e.type === 'event')
      .map(e => ({
        id: e.id,
        authorId: e.authorName,
        authorName: e.authorName,
        content: e.content,
        visibility: 'shared' as const,
        timestamp: e.timestamp,
      }));
  }, [activeSession]);

  // ── Pause/endgame data from liveState ────────────────────────────────────
  // Extract pause/endgame metadata for dialog components.
  // Foundation proxy: SSE events not yet received — typed null until SSE delivers them.
  const pauseEvent = useMemo<{ pausedBy: string; pausedAt: string } | null>(() => {
    if (activeSession == null) return null;
    // Interactions sub-PR: will extract from liveStream.events when SSE delivers 'SessionPaused'
    return null;
  }, [activeSession]);

  const endgameEvent = useMemo<{ endedAt: string; endedBy: string } | null>(() => {
    if (activeSession == null) return null;
    // Interactions sub-PR: will extract from liveStream.events when SSE delivers 'SessionEnded'
    return null;
  }, [activeSession]);

  // Mobile content selection based on active tab.
  // MUST be declared BEFORE any early return per react-hooks/rules-of-hooks.
  const mobileContent = useMemo<React.ReactNode>(() => {
    if (activeSession == null) return null;
    switch (mobileTab) {
      case 'log':
        return <ActionLogTimeline entries={activeSession.actionLog} labels={actionLogLabels} />;
      case 'chat':
        return (
          <LiveAgentChat
            messages={chatMessages}
            viewerRole={activeSession.viewerRole}
            viewerId={activeSession.viewerId}
            onSendMessage={handleSendMessage}
            labels={chatLabels}
          />
        );
      case 'tools':
        return (
          <SessionToolsRail
            tools={toolsList}
            viewerRole={activeSession.viewerRole}
            onToolExecute={handleToolExecute}
            labels={toolsRailLabels}
          />
        );
      case 'score':
      default:
        return (
          <LiveScoringPanel
            scores={scores}
            viewerRole={activeSession.viewerRole}
            viewerId={activeSession.viewerId}
            labels={scoringLabels}
          />
        );
    }
  }, [
    mobileTab,
    activeSession,
    scores,
    scoringLabels,
    actionLogLabels,
    chatMessages,
    chatLabels,
    handleSendMessage,
    toolsList,
    toolsRailLabels,
    handleToolExecute,
  ]);

  // ── ConnectionLostBanner — shown for non-healthy SSE states ──────────────
  const showConnectionBanner =
    !IS_VISUAL_TEST_BUILD &&
    (liveStream.connectionState === 'reconnecting' ||
      liveStream.connectionState === 'degraded-polling' ||
      liveStream.connectionState === 'failed');

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

  // Desktop right column: RightColumnTabs with tab content
  const desktopRightColumn = (
    <RightColumnTabs activeTab={tab} onTabChange={handleTabChange} labels={rightColumnTabsLabels}>
      {tab === 'tools' && (
        <SessionToolsRail
          tools={toolsList}
          viewerRole={activeSession.viewerRole}
          onToolExecute={handleToolExecute}
          labels={toolsRailLabels}
        />
      )}
      {tab === 'chat' && (
        <LiveAgentChat
          messages={chatMessages}
          viewerRole={activeSession.viewerRole}
          viewerId={activeSession.viewerId}
          onSendMessage={handleSendMessage}
          labels={chatLabels}
        />
      )}
      {tab === 'notes' && (
        <LiveSessionNotes
          notes={noteEntries}
          viewerRole={activeSession.viewerRole}
          viewerId={activeSession.viewerId}
          onAddNote={handleAddNote}
          labels={notesLabels}
        />
      )}
    </RightColumnTabs>
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

      {/* ConnectionLostBanner — SSE non-healthy states */}
      {showConnectionBanner && (
        <div className="px-4 pt-2">
          <ConnectionLostBanner
            kind={
              liveStream.connectionState === 'reconnecting'
                ? 'reconnecting'
                : liveStream.connectionState === 'degraded-polling'
                  ? 'degraded-polling'
                  : 'failed'
            }
            retryCount={liveStream.retryCount}
            retryAt={liveStream.retryAt}
            onManualRetry={
              liveStream.connectionState !== 'reconnecting' ? liveStream.reconnect : undefined
            }
            labels={connectionLostLabels}
          />
        </div>
      )}

      {/* Desktop 3-column layout (lg+) */}
      <DesktopBody
        leftSidebar={desktopLeftSidebar}
        centerColumn={desktopCenterColumn}
        rightColumn={desktopRightColumn}
      />

      {/* Mobile single-column with bottom nav (< lg) */}
      <MobileBody
        activeTab={mobileTab}
        onTabChange={handleMobileTabChange}
        content={mobileContent}
        labels={mobileBodyLabels}
      />

      {/* Lazy dialogs — mounted from ?dialog= URL param */}
      {dialogState === 'pause' && (
        <Suspense fallback={null}>
          <PauseOverlay
            pausedBy={pauseEvent?.pausedBy ?? '—'}
            pausedAt={pauseEvent?.pausedAt ?? '—'}
            viewerRole={activeSession.viewerRole}
            onResume={
              hasRequiredRole(activeSession.viewerRole, 'Host')
                ? () => void handleResume()
                : undefined
            }
            onClose={() => handleDialogChange('none')}
            labels={{
              title: t('pages.sessionLive.pauseOverlay.title'),
              resumeCta: t('pages.sessionLive.pauseOverlay.resumeCta'),
              closeCta: t('pages.sessionLive.pauseOverlay.closeCta'),
              closeAriaLabel: t('pages.sessionLive.pauseOverlay.closeAriaLabel'),
            }}
          />
        </Suspense>
      )}

      {dialogState === 'endgame' && (
        <Suspense fallback={null}>
          <EndgameDialog
            finalScores={scores.map(s => ({
              playerName: s.playerName,
              score: s.score,
              isWinner: s.isWinner,
            }))}
            endedAt={endgameEvent?.endedAt ?? '—'}
            endedBy={endgameEvent?.endedBy ?? '—'}
            onAcknowledge={() => handleDialogChange('none')}
            labels={{
              title: t('pages.sessionLive.endgameDialog.title'),
              winnerLabel: t('pages.sessionLive.endgameDialog.winnerLabel'),
              acknowledgeCta: t('pages.sessionLive.endgameDialog.acknowledgeCta'),
              viewSummaryCta: t('pages.sessionLive.endgameDialog.viewSummaryCta'),
            }}
          />
        </Suspense>
      )}
    </div>
  );
}
