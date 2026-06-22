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
 *   - Mobile bottom-sheet (G1 #2374 T9 sess.46r): full-width main = ChatAgentPanel + ActionLogTimeline;
 *     floating action button opens MobileBottomSheetDrawer (Score/Turn/Widget/Notes — same content as
 *     desktop RIGHT). URL SSOT: ?msheet=open|closed + ?mtab=score|turn|widget|notes.
 *   - Desktop right column (G1 #2374): score → LiveScoringPanel, turn → TurnIndicator+PlayerRosterLive,
 *     widget → SessionToolsRail, notes → LiveSessionNotes. ChatAgentPanel now lives in LEFT mainColumn.
 *   - Write actions: handleScoreUpdate (optimistic UI), handleToolExecute,
 *     handleSendMessage, handleAddNote, handleResume, handlePause, handleEndgame
 *   - 403 handling: score rollback + toast "Permesso negato"
 *   - 429 handling: connectionState='failed' shown as ConnectionLostBanner kind='failed'
 *
 * **URL state SSOT** (no useState mirrors):
 *   ?tab=score|turn|widget|notes (default 'score') — desktop right-column tab.
 *     Back-compat aliases (G1 #2374 sess.46r, R-1):
 *       legacy ?tab=tools  → 'widget'
 *       legacy ?tab=chat   → 'score' (chat is no longer a tab; lives in LEFT mainColumn)
 *   ?mtab=score|turn|widget|notes (default 'score') — mobile bottom-sheet active tab.
 *     Back-compat aliases (T9 sess.46r, R-1 mirror of desktop ?tab):
 *       legacy ?mtab=tools  → 'widget'
 *       legacy ?mtab=chat   → 'score' (chat is always-visible in main column)
 *       legacy ?mtab=log    → 'score' (log is always-visible in main column)
 *   ?msheet=open|closed (default 'closed') — mobile bottom-sheet drawer state.
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
 * **G1 #2374 (2026-06-15)**:
 *   Refactored from 3-col fixed-width layout (LEFT 280px / CENTER flex / RIGHT 340px)
 *   to a 2-col 60/40 grid (LEFT minmax(0,3fr) / RIGHT minmax(0,2fr)).
 *   - LEFT (60%) = ChatAgentPanel (new primitive, always visible) + ActionLogTimeline stacked.
 *   - RIGHT (40%) = RightColumnTabs polymorphic with keys: Score | Turn | Widget | Notes.
 *     - Score tab → LiveScoringPanel (G5 will swap for polymorphic dispatcher).
 *     - Turn tab → TurnIndicator + PlayerRosterLive (moved out of the deprecated LEFT sidebar).
 *     - Widget tab → SessionToolsRail (relabelled from "Tools" per mockup).
 *     - Notes tab → LiveSessionNotes (unchanged).
 *   Legacy URL `?tab=tools|chat|notes` back-compat preserved via `parseLiveTab` alias map
 *   (tools→widget, chat→score, notes→notes). Token discipline: raw HSL backgrounds replaced
 *   with semantic `bg-background`/`bg-card`. Mobile bottom-sheet drawer (T9) follows in the
 *   same PR per spec-panel DEC-4.
 *
 * Pattern blueprint: Wave D.1 SessionsLibraryView + Wave C.1 AgentDetailView.
 * Wave D.2 Interactions sub-PR — Issue #750
 * G1 layout refactor — Issue #2374
 */

'use client';

import { useCallback, useEffect, useMemo, lazy, Suspense, type ReactElement } from 'react';

import { useParams, usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useIntl } from 'react-intl';

import {
  ActionLogTimeline,
  ChatAgentPanel,
  DesktopBody,
  LiveTopBar,
  MobileBody,
  PlayerRosterLive,
  TurnIndicatorRenderer,
  type ActionLogTimelineLabels,
  type ChatAgentPanelLabels,
  type LiveTopBarLabels,
  type MobileBodyLabels,
  type PlayerRosterLiveLabels,
  type TurnIndicatorRendererLabels,
} from '@/components/features/session-live';
import {
  ConnectionLostBanner,
  LiveSessionNotes,
  RightColumnTabs,
  ToolkitRenderer,
  type ConnectionLostBannerLabels,
  type LiveAgentChatLabels,
  type LiveSessionNotesLabels,
  type RightColumnTabsLabels,
  type ScoringPanelRendererLabels,
  type ToolkitRendererLabels,
} from '@/components/features/session-live';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import { useSession } from '@/hooks/queries/useActiveSessions';
import { useTranslation } from '@/hooks/useTranslation';
import { composeSessionLiveState } from '@/lib/session-live/compose-session-live-state';
import { mapConnectionState } from '@/lib/session-live/map-connection-state';
import { hasRequiredRole } from '@/lib/session-live/participant-role';
import { mapScoreDataToEndgameSummary } from '@/lib/session-live/score-data-to-endgame-summary';
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
import type { TurnState, PlayerInfo as TurnPlayerInfo } from '@/lib/session-live/turn-state';
import { useElapsedTime } from '@/lib/session-live/use-elapsed-time';
import { useSessionLiveStream } from '@/lib/session-live/use-session-live-stream';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { useToolkitRendererStore } from '@/lib/stores/toolkit-renderer-store';

import { ScoreTabContent } from './ScoreTabContent';

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
// G1 #2374 sess.46r — renamed to mockup canonical 'score' | 'turn' | 'widget' | 'notes'.
// parseLiveTab implements back-compat alias map per plan §3 D-2 (R-1 mitigation):
//   - legacy ?tab=tools  → 'widget'  (SessionToolsRail = widget semantic)
//   - legacy ?tab=chat   → 'score'   (chat is no longer a tab; live in LEFT mainColumn)
//   - legacy/missing     → 'score'   (new default)

type LiveTab = 'score' | 'turn' | 'widget' | 'notes';

function parseLiveTab(raw: string | null): LiveTab {
  if (raw === 'turn' || raw === 'widget' || raw === 'notes' || raw === 'score') return raw;
  // Back-compat aliases (R-1): legacy URL bookmarks must not 404.
  if (raw === 'tools') return 'widget';
  if (raw === 'chat') return 'score';
  return 'score'; // default
}

// G1 #2374 T9 sess.46r — mobile drawer uses the same LiveTab union as desktop.
// Legacy ?mtab=tools  → 'widget' (same back-compat as desktop ?tab)
// Legacy ?mtab=chat   → 'score'  (chat always-visible in main column)
// Legacy ?mtab=log    → 'score'  (log always-visible in main column)
function parseMobileTab(raw: string | null): LiveTab {
  if (raw === 'turn' || raw === 'widget' || raw === 'notes' || raw === 'score') return raw;
  if (raw === 'tools') return 'widget';
  if (raw === 'chat' || raw === 'log') return 'score';
  return 'score';
}

function parseMobileSheetOpen(raw: string | null): boolean {
  return raw === 'open';
}

// G3 #2375 — shared accordion FSM URL parser
function parseCollapsed(raw: string | null): boolean {
  return raw === 'collapsed';
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
        <div className="w-[280px] shrink-0 rounded-lg bg-card h-64" />
        <div className="flex-1 rounded-lg bg-card h-64" />
        <div className="w-[340px] shrink-0 rounded-lg bg-card h-64" />
      </div>
      {/* Mobile: single-column skeleton */}
      <div className="flex flex-col gap-4 lg:hidden">
        <div className="h-32 rounded-lg bg-card" />
        <div className="h-48 rounded-lg bg-card" />
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
      <p className="text-lg font-semibold text-foreground">{title}</p>
      <p className="text-sm text-muted-foreground">{description}</p>
      <button
        type="button"
        onClick={onRetry}
        data-slot="session-live-error-retry"
        className="rounded-lg bg-primary px-6 py-2 text-sm font-semibold text-primary-foreground
          hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
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
      <p className="text-lg font-semibold text-foreground">{title}</p>
      <p className="text-sm text-muted-foreground">{description}</p>
      <button
        type="button"
        onClick={onBack}
        data-slot="session-live-not-found-cta"
        className="rounded-lg bg-primary px-6 py-2 text-sm font-semibold text-primary-foreground
          hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
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
  const mobileSheetOpen = parseMobileSheetOpen(searchParams.get('msheet'));
  const chatCollapsed = parseCollapsed(searchParams.get('chat'));
  const mobileChatCollapsed = parseCollapsed(searchParams.get('mchat'));
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

    return {
      id: dto.id,
      name: `Sessione ${dto.id.slice(0, 8)}`,
      status: liveState.status === 'Paused' ? 'Paused' : 'InProgress',
      viewerRole: 'Player' as const, // Foundation default — real viewerRole from session DTO
      viewerId: '',
      currentTurn: liveState.currentTurn,
      totalTurns: liveState.totalTurns,
      activePlayerId: liveState.activePlayerId,
      players: liveState.players,
      actionLog: liveState.actionLog,
    };
  }, [fixture, sessionQuery.data, liveStream.events]);

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
      // G1 #2374 sess.46r — default 'score' is omitted from URL (clean bookmark surface).
      const val = next === 'score' ? null : next;
      router.replace(`${pathname}${buildQuery({ tab: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleMobileTabChange = useCallback(
    (next: LiveTab) => {
      // Default 'score' is omitted from URL (clean bookmark surface).
      const val = next === 'score' ? null : next;
      router.replace(`${pathname}${buildQuery({ mtab: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleMobileSheetOpenChange = useCallback(
    (open: boolean) => {
      // Default 'closed' is omitted from URL (clean bookmark surface).
      const val = open ? 'open' : null;
      router.replace(`${pathname}${buildQuery({ msheet: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  // G3 #2375 — accordion FSM handlers (DEC-1: ?chat desktop, ?mchat mobile, separate params).
  // Default expanded (param omitted) per DEC-4 / mockup canonical.
  const handleChatCollapsedChange = useCallback(
    (collapsed: boolean) => {
      const val = collapsed ? 'collapsed' : null;
      router.replace(`${pathname}${buildQuery({ chat: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleMobileChatCollapsedChange = useCallback(
    (collapsed: boolean) => {
      const val = collapsed ? 'collapsed' : null;
      router.replace(`${pathname}${buildQuery({ mchat: val })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  // G3 #2375 — stable header-click handlers (avoids new closure every render).
  const handleChatHeaderClick = useCallback(
    () => handleChatCollapsedChange(!chatCollapsed),
    [handleChatCollapsedChange, chatCollapsed]
  );

  const handleMobileChatHeaderClick = useCallback(
    () => handleMobileChatCollapsedChange(!mobileChatCollapsed),
    [handleMobileChatCollapsedChange, mobileChatCollapsed]
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
  // Note: legacy per-participant score update flow was retired in #2433
  // (post-#2389 Block C). The polymorphic flow now goes through
  // useUpdateSessionScores → PUT /game-sessions/{id}/scores-polymorphic
  // (wired in ScoreTabContent).

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
      // G4 — Issue #2355 wiring
      elapsedTimeAriaLabel: t('pages.sessionLive.topBar.elapsedTimeAriaLabel'),
      connectionStateAriaLabels: {
        connected: t('pages.sessionLive.topBar.connectionStateConnected'),
        reconnecting: t('pages.sessionLive.topBar.connectionStateReconnecting'),
        failed: t('pages.sessionLive.topBar.connectionStateFailed'),
      },
    };
  }, [t, activeSession?.currentTurn, activeSession?.totalTurns, activeSession?.name]);

  // G4 — Issue #2355: live elapsed time + connection pip wiring
  const elapsedMs = useElapsedTime(sessionQuery.data?.startedAt);
  const connectionPipState = mapConnectionState(liveStream.connectionState);

  // G5b #2378 — TurnIndicatorRenderer labels + fixture state memos.
  // Real TurnState wiring deferred to #2389; synthesise RoundRobin from activeSession fields.
  const turnRendererLabels = useMemo<TurnIndicatorRendererLabels>(
    (): TurnIndicatorRendererLabels => ({
      roundRobinHeading: t('pages.sessionLive.turnIndicator.roundRobinHeading'),
      sequentialHeading: t('pages.sessionLive.turnIndicator.sequentialHeading'),
      simultaneousHeading: t('pages.sessionLive.turnIndicator.simultaneousHeading'),
      realtimeHeading: t('pages.sessionLive.turnIndicator.realtimeHeading'),
      noneHeading: t('pages.sessionLive.turnIndicator.noneHeading'),
      customHeading: t('pages.sessionLive.turnIndicator.customHeading'),
      firstPlayerTokenHeading: t('pages.sessionLive.turnIndicator.firstPlayerTokenHeading'),
      unknownTitle: t('pages.sessionLive.turnIndicator.unknownTitle'),
      unknownBody: t('pages.sessionLive.turnIndicator.unknownBody'),
      yourTurnLabel: t('pages.sessionLive.turnIndicator.yourTurnLabel'),
      waitingLabel: t('pages.sessionLive.turnIndicator.waitingLabel'),
      roundCountTemplate:
        (intl.messages['pages.sessionLive.turnIndicator.roundCountTemplate'] as string) ??
        'Round {current} di {total}',
      playOrderHeading: t('pages.sessionLive.turnIndicator.playOrderHeading'),
      firstPlayerTokenHolderTemplate:
        (intl.messages[
          'pages.sessionLive.turnIndicator.firstPlayerTokenHolderTemplate'
        ] as string) ?? 'Token primo giocatore: {playerName}',
    }),
    [t, intl.messages]
  );

  const turnRendererState = useMemo<TurnState>(
    (): TurnState => ({
      type: 'RoundRobin',
      round: activeSession?.currentTurn ?? 0,
      totalRounds: activeSession?.totalTurns ?? 0,
      activePlayerId: activeSession?.activePlayerId ?? '',
      playOrder: activeSession?.players.map(p => p.id) ?? [],
    }),
    [activeSession]
  );

  const turnRendererPlayers = useMemo<ReadonlyArray<TurnPlayerInfo>>(
    () => activeSession?.players.map(p => ({ id: p.id, name: p.name })) ?? [],
    [activeSession]
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

  // G5a #2375: ScoringPanelRenderer labels (polymorphic Points/Ranking/BinaryWin/Objectives).
  // Block C #2389 T6 — migrate to nested catalog keys; inline italian fallbacks removed.
  // Aria templates still use `intl.messages[...]` direct access (not `t()`) because they
  // contain `{name}`/`{label}` placeholders that are runtime string-replaced downstream;
  // ICU `t()` would consume those braces. Aria-template catalog keys are not yet bundled
  // (follow-up tracked: see T6 report).
  const scoringPanelLabels = useMemo<ScoringPanelRendererLabels>(
    (): ScoringPanelRendererLabels => ({
      points: {
        heading: t('pages.sessionLive.scoring.points.title'),
        scoreAriaTemplate: intl.messages['pages.sessionLive.scoring.scoreAriaTemplate'] as string,
        leaderBadgeLabel: t('pages.sessionLive.scoring.points.leaderLabel'),
      },
      ranking: {
        heading: t('pages.sessionLive.scoring.ranking.title'),
        rankAriaTemplate: intl.messages['pages.sessionLive.scoring.rankAriaTemplate'] as string,
        firstPlaceBadgeLabel: intl.messages[
          'pages.sessionLive.scoring.firstPlaceBadgeLabel'
        ] as string,
      },
      binaryWin: {
        heading: t('pages.sessionLive.scoring.binaryWin.title'),
        inProgressLabel: t('pages.sessionLive.scoring.binaryWin.pendingLabel'),
        winLabel: t('pages.sessionLive.scoring.binaryWin.winLabel'),
        loseLabel: t('pages.sessionLive.scoring.binaryWin.loseLabel'),
        outcomeAriaTemplate: intl.messages[
          'pages.sessionLive.scoring.outcomeAriaTemplate'
        ] as string,
      },
      objectives: {
        heading: t('pages.sessionLive.scoring.objectives.title'),
        completedAriaTemplate: intl.messages[
          'pages.sessionLive.scoring.completedAriaTemplate'
        ] as string,
        doneAriaTemplate: intl.messages['pages.sessionLive.scoring.doneAriaTemplate'] as string,
        pendingAriaTemplate: intl.messages[
          'pages.sessionLive.scoring.pendingAriaTemplate'
        ] as string,
      },
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

  // G1 #2374 T9 sess.46r — bottom-sheet labels (replaces legacy bottom-nav labels).
  const mobileBodyLabels = useMemo<MobileBodyLabels>(
    (): MobileBodyLabels => ({
      openSheetCta: t('pages.sessionLive.mobile.openSheetCta'),
      closeSheetAriaLabel: t('pages.sessionLive.mobile.closeSheetAriaLabel'),
      drawerTitle: t('pages.sessionLive.mobile.drawerTitle'),
      tabsAriaLabel: t('pages.sessionLive.mobile.tabsAriaLabel'),
      tabScore: t('pages.sessionLive.rightColumn.tabScore'),
      tabTurn: t('pages.sessionLive.rightColumn.tabTurn'),
      tabWidget: t('pages.sessionLive.rightColumn.tabWidget'),
      tabNotes: t('pages.sessionLive.rightColumn.tabNotes'),
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
      tabScore: t('pages.sessionLive.rightColumn.tabScore'),
      tabTurn: t('pages.sessionLive.rightColumn.tabTurn'),
      tabWidget: t('pages.sessionLive.rightColumn.tabWidget'),
      tabNotes: t('pages.sessionLive.rightColumn.tabNotes'),
    }),
    [t]
  );

  // G5c #2376 — ToolkitRenderer labels (all 6 widget sub-namespaces).
  // Gate A: aria templates that use ICU-like {name}/{label} placeholders are
  // pulled from intl.messages directly (they are not ICU plural — just runtime
  // string-replace in each widget component) to avoid double-escaping.
  const toolkitRendererLabels = useMemo<ToolkitRendererLabels>(
    (): ToolkitRendererLabels => ({
      title: t('pages.sessionLive.toolkitRenderer.title'),
      emptyTitle: t('pages.sessionLive.toolkitRenderer.emptyTitle'),
      emptyBody: t('pages.sessionLive.toolkitRenderer.emptyBody'),
      unknownTitle: t('pages.sessionLive.toolkitRenderer.unknownTitle'),
      unknownBody: t('pages.sessionLive.toolkitRenderer.unknownBody'),
      expandAriaTemplate:
        (intl.messages['pages.sessionLive.toolkitRenderer.expandAriaTemplate'] as string) ??
        'Espandi widget {name}',
      collapseAriaTemplate:
        (intl.messages['pages.sessionLive.toolkitRenderer.collapseAriaTemplate'] as string) ??
        'Collassa widget {name}',
      randomGenerator: {
        heading: t('pages.sessionLive.toolkitRenderer.randomGenerator.heading'),
        rollLabel: t('pages.sessionLive.toolkitRenderer.randomGenerator.rollLabel'),
        lastLabel: t('pages.sessionLive.toolkitRenderer.randomGenerator.lastLabel'),
      },
      turnManager: {
        heading: t('pages.sessionLive.toolkitRenderer.turnManager.heading'),
        prevLabel: t('pages.sessionLive.toolkitRenderer.turnManager.prevLabel'),
        nextLabel: t('pages.sessionLive.toolkitRenderer.turnManager.nextLabel'),
        turnOfLabel: t('pages.sessionLive.toolkitRenderer.turnManager.turnOfLabel'),
        phaseLabel: t('pages.sessionLive.toolkitRenderer.turnManager.phaseLabel'),
      },
      scoreTracker: {
        heading: t('pages.sessionLive.toolkitRenderer.scoreTracker.heading'),
        incrementAriaTemplate:
          (intl.messages[
            'pages.sessionLive.toolkitRenderer.scoreTracker.incrementAriaTemplate'
          ] as string) ?? 'Aumenta punteggio {name}',
        decrementAriaTemplate:
          (intl.messages[
            'pages.sessionLive.toolkitRenderer.scoreTracker.decrementAriaTemplate'
          ] as string) ?? 'Diminuisci punteggio {name}',
      },
      resourceManager: {
        heading: t('pages.sessionLive.toolkitRenderer.resourceManager.heading'),
        sharedHeading: t('pages.sessionLive.toolkitRenderer.resourceManager.sharedHeading'),
        incrementAriaTemplate:
          (intl.messages[
            'pages.sessionLive.toolkitRenderer.resourceManager.incrementAriaTemplate'
          ] as string) ?? 'Aumenta {label}',
        decrementAriaTemplate:
          (intl.messages[
            'pages.sessionLive.toolkitRenderer.resourceManager.decrementAriaTemplate'
          ] as string) ?? 'Diminuisci {label}',
      },
      noteManager: {
        heading: t('pages.sessionLive.toolkitRenderer.noteManager.heading'),
        inputAriaLabel: t('pages.sessionLive.toolkitRenderer.noteManager.inputAriaLabel'),
        savingLabel: t('pages.sessionLive.toolkitRenderer.noteManager.savingLabel'),
        savedLabel: t('pages.sessionLive.toolkitRenderer.noteManager.savedLabel'),
      },
      whiteboard: {
        heading: t('pages.sessionLive.toolkitRenderer.whiteboard.heading'),
        toolPenLabel: t('pages.sessionLive.toolkitRenderer.whiteboard.toolPenLabel'),
        toolEraserLabel: t('pages.sessionLive.toolkitRenderer.whiteboard.toolEraserLabel'),
        toolCircleLabel: t('pages.sessionLive.toolkitRenderer.whiteboard.toolCircleLabel'),
        placeholderLabel: t('pages.sessionLive.toolkitRenderer.whiteboard.placeholderLabel'),
      },
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
      newMessagesToastAriaLabel: t('pages.sessionLive.chat.newMessagesToastAriaLabel'),
    }),
    [t]
  );

  // G1 #2374 sess.46r — ChatAgentPanel composite labels (Gate A: ICU resolved here).
  const chatAgentLabels = useMemo<ChatAgentPanelLabels>(
    (): ChatAgentPanelLabels => ({
      title: t('pages.sessionLive.chatAgent.title'),
      agentNameAriaLabel: t('pages.sessionLive.chatAgent.agentNameAriaLabel', { name: 'MeepleAI' }),
      onlineLabel: t('pages.sessionLive.chatAgent.onlineLabel'),
      latencyAriaLabel: t('pages.sessionLive.chatAgent.latencyAriaLabel', { ms: 42 }),
      chatPanelLabels: chatLabels,
    }),
    [t, chatLabels]
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
  //
  // #2430 Block B+: Block B's polymorphic scoring logic (selectors, memo,
  // a11y placeholder) MOVED to ScoreTabContent. SessionLiveView keeps only
  // the REST hydration useEffect because it depends on sessionQuery.data
  // (which lives at this level via useSession). The store-write side-effect
  // is forwarded to ScoreTabContent via the shared useLiveSessionStore.

  const setScoringConfig = useLiveSessionStore(s => s.setScoringConfig);
  const setTurnOrderType = useLiveSessionStore(s => s.setTurnOrderType);

  // #2431: polymorphic endgame summary — selectors feed mapScoreDataToEndgameSummary
  // below. Subscribed reactively so the EndgameDialog refreshes as scoreData
  // changes (final-tick edits before the host acknowledges).
  const endgameScoringType = useLiveSessionStore(s => s.scoringType);
  const endgameScoreData = useLiveSessionStore(s => s.scoreData);

  // #2389 Block B + #2430 Block B+: REST hydration with race guard +
  // observability. Pre-populate the store from sessionQuery.data on initial
  // mount so the renderer paints in ~300ms instead of waiting for SignalR.
  // Skip if SignalR already populated to avoid stale REST overwriting fresh
  // state.
  useEffect(() => {
    const dto = sessionQuery.data;
    if (dto?.scoringType == null || dto.scoreData == null) return;
    if (useLiveSessionStore.getState().scoringType != null) return;
    try {
      const parsed = JSON.parse(dto.scoreData) as ScoreDataByType[ScoreType];
      setScoringConfig({
        scoringType: dto.scoringType as ScoreType,
        scoreData: parsed,
      });
    } catch (err) {
      console.warn('[#2389] malformed scoreData JSON, will rely on SignalR', {
        sessionId: dto.id,
        scoreDataLength: dto.scoreData?.length ?? 0,
        err,
      });
    }
  }, [sessionQuery.data, setScoringConfig]);

  // #2483 Task 2: REST hydration for turnOrderType (path B — static, no SignalR).
  // Populate once from the DTO. No race guard needed: no SignalR event exists for
  // turnOrderType (it never changes during the session).
  useEffect(() => {
    const dto = sessionQuery.data;
    if (dto?.turnOrderType == null) return;
    setTurnOrderType(dto.turnOrderType as import('@/lib/session-live/turn-state').TurnOrderType);
  }, [sessionQuery.data, setTurnOrderType]);

  // ── G5c #2376: Zustand toolkit renderer store ─────────────────────────────
  // Store starts empty; real hydration via useQuery(['toolkit', sessionId]) is a
  // follow-up PR that wires GET /api/v1/toolkits/{toolkitId}/widgets.
  const toolkitWidgets = useToolkitRendererStore(s => s.widgets);
  const toolkitOpenId = useToolkitRendererStore(s => s.openWidgetId);
  const setToolkitOpen = useToolkitRendererStore(s => s.setOpenWidget);
  const updateToolkitConfig = useToolkitRendererStore(s => s.updateWidgetConfig);

  // Map active session players for ScoreTracker widget
  const toolkitPlayers = useMemo(
    () => activeSession?.players.map(p => ({ id: p.id, name: p.name })) ?? [],
    [activeSession]
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

  // ── Mobile main content (G1 #2374 T9 sess.46r) ───────────────────────────
  // Full-width LEFT-equivalent: ChatAgentPanel + ActionLogTimeline stacked.
  // Mirrors the desktop LEFT 60% column (see desktopMainColumn below).
  // MUST be declared BEFORE any early return per react-hooks/rules-of-hooks.
  const mobileMainContent = useMemo<React.ReactNode>(() => {
    if (activeSession == null) return null;
    return (
      <div className="flex flex-col gap-3">
        <ChatAgentPanel
          sessionId={sessionId}
          messages={chatMessages}
          viewerRole={activeSession.viewerRole}
          viewerId={activeSession.viewerId}
          onSendMessage={handleSendMessage}
          agentName="MeepleAI"
          agentEmoji="🤖"
          latencyMs={42}
          collapsed={mobileChatCollapsed}
          onHeaderClick={handleMobileChatHeaderClick}
          labels={chatAgentLabels}
          compact
        />
        <ActionLogTimeline entries={activeSession.actionLog} labels={actionLogLabels} compact />
      </div>
    );
  }, [
    activeSession,
    sessionId,
    chatMessages,
    handleSendMessage,
    chatAgentLabels,
    actionLogLabels,
    mobileChatCollapsed,
    handleMobileChatHeaderClick,
  ]);

  // ── Mobile bottom-sheet content (G1 #2374 T9 sess.46r) ───────────────────
  // Same switch as desktopRightColumn (DRY): score / turn / widget / notes.
  // Hosted inside MobileBottomSheetDrawer via MobileBody.sheetContent prop.
  const mobileSheetContent = useMemo<React.ReactNode>(() => {
    if (activeSession == null) return null;
    switch (mobileTab) {
      case 'turn':
        return (
          <div className="flex flex-col gap-4 p-3">
            <TurnIndicatorRenderer
              state={turnRendererState}
              players={turnRendererPlayers}
              viewerId={activeSession.viewerId}
              compact
              labels={turnRendererLabels}
            />
            <PlayerRosterLive
              players={activeSession.players}
              viewerId={activeSession.viewerId}
              viewerRole={activeSession.viewerRole}
              labels={rosterLabels}
            />
          </div>
        );
      case 'widget':
        return (
          <ToolkitRenderer
            widgets={toolkitWidgets}
            openWidgetId={toolkitOpenId}
            onOpenWidgetChange={setToolkitOpen}
            onWidgetConfigChange={(id, cfg) => void updateToolkitConfig(id, cfg)}
            players={toolkitPlayers}
            labels={toolkitRendererLabels}
          />
        );
      case 'notes':
        return (
          <LiveSessionNotes
            notes={noteEntries}
            viewerRole={activeSession.viewerRole}
            viewerId={activeSession.viewerId}
            onAddNote={handleAddNote}
            labels={notesLabels}
          />
        );
      case 'score':
      default:
        return (
          <ScoreTabContent
            sessionId={sessionId ?? ''}
            viewerRole={activeSession.viewerRole}
            players={activeSession.players}
            labels={scoringPanelLabels}
            className="p-2"
          />
        );
    }
  }, [
    mobileTab,
    activeSession,
    sessionId,
    scoringPanelLabels,
    turnRendererState,
    turnRendererPlayers,
    turnRendererLabels,
    rosterLabels,
    toolkitWidgets,
    toolkitOpenId,
    setToolkitOpen,
    updateToolkitConfig,
    toolkitPlayers,
    toolkitRendererLabels,
    noteEntries,
    handleAddNote,
    notesLabels,
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
        className="flex flex-col min-h-screen bg-background"
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
        className="flex flex-col min-h-screen bg-background"
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
        className="flex flex-col min-h-screen bg-background"
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
        className="flex flex-col min-h-screen bg-background"
      >
        <LoadingShell ariaLabel={t('pages.sessionLive.loading.ariaLabel')} />
      </div>
    );
  }

  // ── Default content ───────────────────────────────────────────────────────
  // (mobileContent declared before early returns per react-hooks/rules-of-hooks)
  //
  // G1 #2374 sess.46r — Desktop refactor from 3-zone (LEFT sidebar + CENTER
  // column + RIGHT tabs) to 2-zone 60/40 grid (LEFT mainColumn + RIGHT tabs).
  // TurnIndicator + PlayerRosterLive moved to RIGHT 'turn' tab (D-6).
  // LEFT mainColumn stacks ChatAgentPanel (Issue #2375 G3 §5 contract) on top
  // of ActionLogTimeline — mirrors mockup `sp4-session-skeleton-live.jsx`.

  const desktopMainColumn = (
    <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-hidden p-3">
      <ChatAgentPanel
        sessionId={sessionId}
        messages={chatMessages}
        viewerRole={activeSession.viewerRole}
        viewerId={activeSession.viewerId}
        onSendMessage={handleSendMessage}
        agentName="MeepleAI"
        agentEmoji="🤖"
        latencyMs={42}
        collapsed={chatCollapsed}
        onHeaderClick={handleChatHeaderClick}
        labels={chatAgentLabels}
      />
      <ActionLogTimeline entries={activeSession.actionLog} labels={actionLogLabels} />
    </div>
  );

  // Desktop right column: RightColumnTabs with tab content.
  // Tab keys: 'score' | 'turn' | 'widget' | 'notes' (G1 §3 D-2).
  // G5a (#2375): ScoringPanelRenderer replaces hardcoded LiveScoringPanel Points-only view.
  const desktopRightColumn = (
    <RightColumnTabs activeTab={tab} onTabChange={handleTabChange} labels={rightColumnTabsLabels}>
      {tab === 'score' && (
        <ScoreTabContent
          sessionId={sessionId ?? ''}
          viewerRole={activeSession.viewerRole}
          players={activeSession.players}
          labels={scoringPanelLabels}
          className="p-3"
        />
      )}
      {tab === 'turn' && (
        <div className="flex flex-col gap-4 p-3">
          <TurnIndicatorRenderer
            state={turnRendererState}
            players={turnRendererPlayers}
            viewerId={activeSession.viewerId}
            labels={turnRendererLabels}
          />
          <PlayerRosterLive
            players={activeSession.players}
            viewerId={activeSession.viewerId}
            viewerRole={activeSession.viewerRole}
            labels={rosterLabels}
          />
        </div>
      )}
      {tab === 'widget' && (
        <ToolkitRenderer
          widgets={toolkitWidgets}
          openWidgetId={toolkitOpenId}
          onOpenWidgetChange={setToolkitOpen}
          onWidgetConfigChange={(id, cfg) => void updateToolkitConfig(id, cfg)}
          players={toolkitPlayers}
          labels={toolkitRendererLabels}
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
      data-layout="2col-60-40"
      data-theme="dark"
      className="flex flex-col h-screen overflow-hidden bg-background text-foreground"
      aria-label={t('pages.sessionLive.a11y.viewLabel')}
    >
      {/* Sticky top bar */}
      <LiveTopBar
        sessionName={activeSession.name}
        status={activeSession.status}
        viewerRole={activeSession.viewerRole}
        onExit={handleExit}
        labels={topBarLabels}
        elapsedMs={elapsedMs}
        connectionState={connectionPipState}
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

      {/* Desktop 2-zone 60/40 layout (lg+) — G1 #2374 */}
      <DesktopBody mainColumn={desktopMainColumn} rightColumn={desktopRightColumn} />

      {/* Mobile bottom-sheet pattern (< lg) — G1 #2374 T9 sess.46r */}
      <MobileBody
        mainContent={mobileMainContent}
        sheetOpen={mobileSheetOpen}
        onSheetOpenChange={handleMobileSheetOpenChange}
        sheetActiveTab={mobileTab}
        onSheetTabChange={handleMobileTabChange}
        sheetContent={mobileSheetContent}
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
            // #2431: polymorphic path when scoringType + scoreData are loaded;
            // otherwise the legacy `{ score, isWinner: false }` shape keeps the
            // dialog renderable during cold-start.
            // TODO(#2389 Block C cleanup): once the legacy `p.score` scalar is
            // removed by the polymorphic migration, drop this fallback and
            // either return [] or gate the dialog mount on scoringType !== null.
            finalScores={
              endgameScoringType !== null && endgameScoreData !== null
                ? mapScoreDataToEndgameSummary(
                    endgameScoringType,
                    endgameScoreData,
                    activeSession.players
                  )
                : activeSession.players.map(p => ({
                    playerName: p.name,
                    score: p.score,
                    isWinner: false,
                  }))
            }
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
