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

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  lazy,
  Suspense,
  type ReactElement,
} from 'react';

import { useParams, usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useIntl } from 'react-intl';
import { toast } from 'sonner';

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
  FlavorRenderer,
  hasFlavor,
  LiveSessionNotes,
  RightColumnTabs,
  ToolkitRenderer,
  type CatanLiveFlavorLabels,
  type ConnectionLostBannerLabels,
  type LiveAgentChatLabels,
  type LiveSessionNotesLabels,
  type RightColumnTabsLabels,
  type ScoringPanelRendererLabels,
  type ToolkitRendererLabels,
} from '@/components/features/session-live';
import { AgentDisputeTabContent } from '@/components/features/session-live/AgentDisputeTabContent';
import type { ChatMessage as LiveAgentChatMessage } from '@/components/features/session-live/LiveAgentChat';
import { PhotosTabContent } from '@/components/features/session-live/PhotosTabContent';
import { useAddDiaryEntry } from '@/hooks/mutations/useAddDiaryEntry';
import { useCompleteLiveSession } from '@/hooks/mutations/useCompleteLiveSession';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useLiveSession } from '@/hooks/queries/useLiveSession';
import { useLiveSessionDiary } from '@/hooks/queries/useLiveSessionDiary';
import { useLiveSessionPhases } from '@/hooks/queries/useLiveSessionPhases';
import { useSessionAgentLaunch } from '@/hooks/queries/useSessionAgentLaunch';
import type { ChatImagePreview } from '@/hooks/useChatImageAttachments';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { ConflictError } from '@/lib/api/core/errors';
import { useSessionAgentChat } from '@/lib/domain-hooks/useSessionAgentChat';
import { useSignalRSession } from '@/lib/domain-hooks/useSignalrSession';
import { getNavigationLinks } from '@/lib/navigation';
import { composeSessionLiveState } from '@/lib/session-live/compose-session-live-state';
import { formatSessionStartedAt } from '@/lib/session-live/format-session-started-at';
import { mapConnectionState } from '@/lib/session-live/map-connection-state';
import { mapTurnDataToTurnState } from '@/lib/session-live/map-turn-data-to-turn-state';
import { mergeHydratedDiary } from '@/lib/session-live/merge-hydrated-diary';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';
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
import { useResolvePlayRecord } from '@/lib/session-live/use-resolve-play-record';
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

const AddPlayerDialog = lazy(() =>
  import('@/components/features/session-live/AddPlayerDialog').then(m => ({
    default: m.AddPlayerDialog,
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

type LiveTab = 'flavor' | 'score' | 'turn' | 'widget' | 'notes' | 'photos' | 'agent';

function parseLiveTab(raw: string | null): LiveTab {
  if (
    raw === 'flavor' ||
    raw === 'turn' ||
    raw === 'widget' ||
    raw === 'notes' ||
    raw === 'score' ||
    raw === 'photos' ||
    raw === 'agent'
  )
    return raw;
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
  if (
    raw === 'flavor' ||
    raw === 'turn' ||
    raw === 'widget' ||
    raw === 'notes' ||
    raw === 'score' ||
    raw === 'photos' ||
    raw === 'agent'
  )
    return raw;
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

  // ── Current user (for viewerRole derivation — #2505) ─────────────────────
  const { data: currentUser } = useCurrentUser();

  // ── AddPlayerDialog state (#2505) ────────────────────────────────────────
  const [addPlayerOpen, setAddPlayerOpen] = useState(false);

  const handleAddPlayer = useCallback(() => {
    setAddPlayerOpen(true);
  }, []);

  // ── #2503: Endgame confirm dialog state ──────────────────────────────────
  const [endgameConfirmOpen, setEndgameConfirmOpen] = useState(false);
  // Host explicitly clicked "Salva partita": gates the single navigation path
  // (code-review CRITICAL 1 — no auto-nav independent of the button click).
  const [saveIntent, setSaveIntent] = useState(false);

  // ── #2503: Complete session mutation + play-record polling ───────────────
  const completeLiveSession = useCompleteLiveSession(sessionId ?? '');
  // ── #2575: diary write-path mutation ──────────────────────────────────────
  const addDiary = useAddDiaryEntry(sessionId ?? '');
  // Destructure stable members (code-review IMPORTANT 4 — the hook returns a new
  // object literal each render; depending on the whole object re-memoizes every
  // poll tick). `start` is a stable useCallback.
  const {
    status: resolveStatus,
    playRecordId: resolvedPlayRecordId,
    start: startResolvePlayRecord,
  } = useResolvePlayRecord();

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

  // Real data hook — disabled when fixture is active or sessionId is null.
  // ADR-083 Fase 1 (#2501): load the canonical LiveGameSession aggregate
  // (LiveSessionDto) — the one the wizards actually create — instead of the empty
  // GameSession shell, which returned 404 for funnel-created sessions.
  const sessionQuery = useLiveSession(
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

  // ── #2575: hydrate historical diary entries on load ───────────────────────
  // Previously the diary was SSE-passive only (reopening a session showed no prior entries
  // until new SSE events arrived). These are merged ahead of the live SSE events below.
  const diaryQuery = useLiveSessionDiary(
    sessionId ?? '',
    /* enabled= */ !IS_VISUAL_TEST_BUILD &&
      sessionId != null &&
      sessionQuery.isSuccess &&
      sessionQuery.data != null
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

  // ── Active session data ───────────────────────────────────────────────────
  // Priority: fixture > composed live state > DTO proxy
  const activeSession: LiveSessionFixture | null = useMemo(() => {
    if (fixture != null) return fixture;
    const dto = sessionQuery.data;
    if (dto == null) return null;

    // ADR-083 Fase 1 (#2501): LiveSessionDto players already carry a stable id +
    // displayName, and the DTO exposes status/currentTurnIndex/currentTurnPlayerId
    // — all consumed directly by composeSessionLiveState (designed for LiveSessionDto).
    // #2575: merge the hydrated historical diary (GET /diary) with the live SSE stream —
    // deduped by entryId (hydrated wins) and re-sorted by timestamp so the composed actionLog
    // stays chronological. See mergeHydratedDiary for the ordering rationale.
    const mergedEvents = mergeHydratedDiary(diaryQuery.data ?? [], liveStream.events, dto.id);
    // Compose live state from DTO + hydrated diary + accumulated SSE events.
    const liveState = composeSessionLiveState(dto, mergedEvents);

    // #2505: derive viewerRole from currentUser.id matched against dto.players.
    // BE PlayerRole has 'Moderator' which FE ParticipantRole does not — map to 'Player'.
    const currentUserId = currentUser?.id ?? '';
    const viewerPlayer = dto.players.find(p => p.userId === currentUserId);
    const derivedRole = viewerPlayer?.role;
    const viewerRole: ParticipantRole =
      derivedRole === 'Host' || derivedRole === 'Spectator' ? derivedRole : 'Player';

    return {
      id: dto.id,
      name: `Sessione ${dto.id.slice(0, 8)}`,
      status: liveState.status === 'Paused' ? 'Paused' : 'InProgress',
      viewerRole,
      viewerId: viewerPlayer?.id ?? '',
      currentTurn: liveState.currentTurn,
      totalTurns: liveState.totalTurns,
      activePlayerId: liveState.activePlayerId,
      players: liveState.players,
      actionLog: liveState.actionLog,
    };
  }, [fixture, sessionQuery.data, liveStream.events, diaryQuery.data, currentUser?.id]);

  // #3025 L1: mirror the opaque live game-state into the store — hydrate from the DTO,
  // then let the latest `session:game-state` SSE event win. L3 flavors read `s.gameState`.
  useEffect(() => {
    const dtoState = sessionQuery.data?.gameState ?? null;
    const latest = [...liveStream.events].reverse().find(e => e.type === 'session:game-state');
    useLiveSessionStore
      .getState()
      .setGameState(latest && latest.type === 'session:game-state' ? latest.state : dtoState);
  }, [sessionQuery.data?.gameState, liveStream.events]);

  // #2483 Task 2: reactive selector for turnOrderType — must be declared before
  // turnRendererState useMemo (which appears in the i18n labels section below).
  const storeTurnOrderType = useLiveSessionStore(s => s.turnOrderType);

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

  // ── #2503: Endgame trigger (Host-only) ────────────────────────────────────
  const handleRequestEndgame = useCallback(() => {
    setEndgameConfirmOpen(true);
  }, []);

  /**
   * Confirm endgame → capture the pre-complete most-recent record id (baseline,
   * code-review CRITICAL 2) BEFORE POST /complete, then complete + start polling
   * with that baseline so resolution skips any pre-existing record for the game.
   */
  const handleConfirmEndgame = useCallback(async () => {
    if (sessionId == null) return;
    setEndgameConfirmOpen(false);

    const gameId = sessionQuery.data?.gameId;

    // Capture baseline BEFORE completing — awaited so the new record cannot leak
    // into the snapshot (race-free identification of the auto-created record).
    let previousRecordId: string | null = null;
    if (gameId) {
      try {
        const baseline = await api.playRecords.getHistory({ gameId, pageSize: 1 });
        previousRecordId = baseline.records[0]?.id ?? null;
      } catch {
        previousRecordId = null;
      }
    }

    completeLiveSession.mutate(undefined, {
      onSuccess: () => {
        handleDialogChange('endgame');
        if (gameId) startResolvePlayRecord(gameId, previousRecordId);
      },
      onError: (err: Error) => {
        // AC-MEDIA-4: if the session was already Completed (409 Conflict), show a toast
        // and do NOT open the endgame dialog or start polling.
        if (err instanceof ConflictError || (err as { statusCode?: number }).statusCode === 409) {
          toast.error(t('pages.sessionLive.endgameDialog.alreadyCompletedToast'), {
            id: 'endgame-already-completed',
          });
        }
        // Other errors: no additional handling (mutation error state is available to callers).
      },
    });
  }, [
    sessionId,
    completeLiveSession,
    handleDialogChange,
    sessionQuery.data?.gameId,
    startResolvePlayRecord,
    t,
  ]);

  /**
   * "Salva partita" CTA — records the intent only; the effect below owns the
   * single navigation path (code-review CRITICAL 1: avoids a button push racing
   * an independent auto-nav effect). If polling is still in-flight the button
   * shows a spinner (saving) and navigation fires once the record resolves.
   */
  const handleSaveGame = useCallback(() => {
    setSaveIntent(true);
  }, []);

  // Single navigation path — fires ONLY after the Host expressed save intent.
  useEffect(() => {
    if (!saveIntent) return;
    const navLinks = getNavigationLinks();
    if (resolveStatus === 'resolved' && resolvedPlayRecordId != null) {
      router.push(navLinks.playRecordDetail(resolvedPlayRecordId));
    } else if (resolveStatus === 'timeout') {
      // Opzione C fallback: record never surfaced → list view.
      router.push(navLinks.playRecords);
    }
  }, [saveIntent, resolveStatus, resolvedPlayRecordId, router]);

  // ── Write actions (Player+Host) ───────────────────────────────────────────
  // Note: legacy per-participant score update flow was retired in #2433
  // (post-#2389 Block C). The polymorphic flow now goes through
  // useUpdateSessionScores → PUT /game-sessions/{id}/scores-polymorphic
  // (wired in ScoreTabContent).

  // NOTE: the social-chat POST handler (/game-sessions/{id}/chat) was retired
  // in #2500 Task 4 (AC-CHAT-0). ChatAgentPanel now routes through handleAgentSendMessage
  // (→ useSessionAgentChat → /agent/chat RAG endpoint). The social endpoint remains
  // available on the backend for SSE-driven action-log entries but is no longer
  // called from this orchestrator.

  const handleAddNote = useCallback(
    // #2575: repointed off the legacy raw fetch to /game-sessions/{id}/diary onto the SP3
    // text-only endpoint via useAddDiaryEntry. `visibility` stays a FE-only affordance (the SP3
    // command is text-only) — the onAddNote(content, visibility) signature is kept unchanged.
    async (content: string, _visibility: 'private' | 'shared'): Promise<void> => {
      if (sessionId == null) return;

      try {
        await addDiary.mutateAsync({ text: content });
      } catch {
        // Surface the failure to the user so they know the note was not saved
        // (finding #16, SP5-a Task 3) — do not silently lose writes.
        toast.error(t('pages.sessionLive.notes.addNoteErrorToast'), {
          id: 'note-add-error',
        });
      }
    },
    [sessionId, addDiary, t]
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
      // SI-4 (#2635) — derived start-time chip
      startedAtChipAriaLabel: t('pages.sessionLive.topBar.startedAtChipAriaLabel'),
      connectionStateAriaLabels: {
        connected: t('pages.sessionLive.topBar.connectionStateConnected'),
        reconnecting: t('pages.sessionLive.topBar.connectionStateReconnecting'),
        failed: t('pages.sessionLive.topBar.connectionStateFailed'),
      },
    };
  }, [t, activeSession?.currentTurn, activeSession?.totalTurns, activeSession?.name]);

  // G4 — Issue #2355: live elapsed time + connection pip wiring
  const elapsedMs = useElapsedTime(sessionQuery.data?.startedAt);
  // SI-4 (#2635): read-only derived start-time chip label (Invariante 5 — never user-editable).
  const startedAt = sessionQuery.data?.startedAt;
  const startedAtLabel = startedAt
    ? t('pages.sessionLive.topBar.startedAtChip', {
        // Format the instant in the viewer's RESOLVED locale (not the mapper's it-IT default) so an
        // EN user does not get a mixed-language "Started at 5 lug…" chip.
        time: formatSessionStartedAt(startedAt, { locale: intl.locale }),
      })
    : undefined;
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

  // #2483 Task 4: replace hardcoded RoundRobin with real turnOrderType from store.
  // mapTurnDataToTurnState is pure and handles null/unknown safely (→ 'None' fallback).
  // storeTurnOrderType in deps so the memo re-fires when the DTO hydration effect runs.
  const turnRendererState = useMemo<TurnState>(
    () =>
      mapTurnDataToTurnState(storeTurnOrderType, {
        currentTurn: activeSession?.currentTurn,
        totalTurns: activeSession?.totalTurns,
        activePlayerId: activeSession?.activePlayerId,
        players: activeSession?.players,
      }),
    [storeTurnOrderType, activeSession]
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
      addPlayerLabel: t('pages.sessionLive.roster.addPlayerCta'),
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
      tabFlavor: t('pages.sessionLive.rightColumn.tabFlavor'),
      tabScore: t('pages.sessionLive.rightColumn.tabScore'),
      tabTurn: t('pages.sessionLive.rightColumn.tabTurn'),
      tabWidget: t('pages.sessionLive.rightColumn.tabWidget'),
      tabNotes: t('pages.sessionLive.rightColumn.tabNotes'),
      tabPhotos: t('pages.sessionLive.rightColumn.tabPhotos'),
      tabAgent: t('pages.sessionLive.rightColumn.tabAgent'),
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
      tabFlavor: t('pages.sessionLive.rightColumn.tabFlavor'),
      tabScore: t('pages.sessionLive.rightColumn.tabScore'),
      tabTurn: t('pages.sessionLive.rightColumn.tabTurn'),
      tabWidget: t('pages.sessionLive.rightColumn.tabWidget'),
      tabNotes: t('pages.sessionLive.rightColumn.tabNotes'),
      tabPhotos: t('pages.sessionLive.rightColumn.tabPhotos'),
      tabAgent: t('pages.sessionLive.rightColumn.tabAgent'),
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
      attachAriaLabel: t('pages.sessionLive.chat.attachAriaLabel'),
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
  // ADR-083 Fase 1 (#2501): the polymorphic REST hydration of scoringType/
  // scoreData/turnOrderType (Block B #2389 / #2430 / #2483) was removed when the
  // loader switched to the canonical LiveGameSession aggregate. LiveSessionDto is
  // round-based and exposes none of those polymorphic fields; on the real funnel
  // they were always undefined (empty GameSession shell), so the effects never ran
  // in production. The store is still consumed below and may be populated via
  // SignalR; wiring round-based scoring from LiveSessionDto.roundScores/
  // scoringConfig is deferred to Fase 2.

  // #2431: polymorphic endgame summary — selectors feed mapScoreDataToEndgameSummary
  // below. Subscribed reactively so the EndgameDialog refreshes as scoreData
  // changes (final-tick edits before the host acknowledges).
  const endgameScoringType = useLiveSessionStore(s => s.scoringType);
  const endgameScoreData = useLiveSessionStore(s => s.scoreData);

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

  // ── RAG agent chat (AC-CHAT-0) ────────────────────────────────────────────
  // agentSessionId is resolved lazily via useSessionAgentLaunch:
  //   1. getAgents(gameId) → pick first active agent
  //   2. launch(sessionId, { agentDefinitionId }) → { agentSessionId }
  // status discriminates lifecycle so the panel always shows feedback (R-FINDING-5):
  //   'idle'      → preconditions not met (no sessionId/gameId or fixture mode)
  //   'launching' → getAgents/launch in flight
  //   'ready'     → agentSessionId obtained, chat can send
  //   'no-agent'  → no assistant for this game
  //   'error'     → getAgents or launch failed
  // Disabled in fixture/visual-test builds so no real fetch is issued.
  const agentLaunch = useSessionAgentLaunch(
    sessionId ?? null,
    sessionQuery.data?.gameId ?? null,
    !fixture
  );
  const agentSessionId = agentLaunch.agentSessionId;

  // I1 (#2500 Opzione A): inject gameContext from LiveSessionDto so the RAG retrieval
  // receives the real game/player context. The game-night store (useSessionStore) is NOT
  // populated in the live session route, so we pass data explicitly here.
  // Nullable gameId guard: if the session has no associated game, gameContext is undefined
  // (RAG will still run in degraded mode, same as before).
  const liveSessionDto = sessionQuery.data;
  const agentGameContext = useMemo(() => {
    if (!liveSessionDto?.gameId) return undefined;
    return {
      gameId: liveSessionDto.gameId,
      gameTitle: liveSessionDto.gameName,
      players: liveSessionDto.players.map(p => p.displayName),
      currentTurn: liveSessionDto.currentTurnIndex,
    };
  }, [liveSessionDto]);

  // ── G6a #2787: per-game Catan flavor (conditional tab) ────────────────────
  const showFlavorTab = hasFlavor(liveSessionDto?.gameSlug);

  // #2787: live SignalR points (Points scoring) overlaid on the DTO leaderboard —
  // the store's scoreData is fresher than the up-to-staletime LiveSessionDto.totalScore.
  const catanLivePoints = useMemo<ReadonlyMap<string, number> | null>(() => {
    if (endgameScoringType !== 'Points' || endgameScoreData == null) return null;
    const scores = (
      endgameScoreData as { scores: ReadonlyArray<{ playerId: string; points: number }> }
    ).scores;
    return new Map(scores.map(s => [s.playerId, s.points]));
  }, [endgameScoringType, endgameScoreData]);

  // #2787: current phase name for the flavor turn header. Fetched only when a flavor
  // tab exists; TurnPhasesDto returns hasPhases:false/currentPhaseName:null → graceful.
  const phasesQuery = useLiveSessionPhases(sessionId ?? '', showFlavorTab && sessionId != null);
  const catanPhaseName = phasesQuery.data?.currentPhaseName ?? null;

  // Placeholder-bearing templates ({n}/{name}/{score}) are read RAW from
  // intl.messages so react-intl does NOT ICU-interpolate them — the flavor
  // component does the runtime .replace. Same pattern as the toolkitRenderer
  // aria templates above. Non-placeholder labels use t() normally.
  const catanFlavorLabels = useMemo<CatanLiveFlavorLabels>(
    () => ({
      panelAriaLabel: t('pages.sessionLive.flavor.catan.panelAriaLabel'),
      roundTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.roundTemplate'] as string) ?? 'Round {n}',
      activePlayerTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.activePlayerTemplate'] as string) ??
        'Turno di {name}',
      phaseTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.phaseTemplate'] as string) ?? 'Fase: {name}',
      leaderboardHeading: t('pages.sessionLive.flavor.catan.leaderboardHeading'),
      leaderBadgeLabel: t('pages.sessionLive.flavor.catan.leaderBadgeLabel'),
      scoreAriaTemplate:
        (intl.messages['pages.sessionLive.flavor.catan.scoreAriaTemplate'] as string) ??
        'Punti di {name}: {score}',
      dimensionsHeading: t('pages.sessionLive.flavor.catan.dimensionsHeading'),
      emptyLabel: t('pages.sessionLive.flavor.catan.emptyLabel'),
    }),
    [t, intl.messages]
  );
  // #2787: never strand the user on ?tab=flavor / ?mtab=flavor when the game has
  // no flavor (e.g. a stale bookmark carried to a non-catan session) — the flavor
  // tab button is hidden (showFlavorTab=false), so fall the panel back to 'score'.
  const effectiveTab: LiveTab = tab === 'flavor' && !showFlavorTab ? 'score' : tab;
  const effectiveMobileTab: LiveTab =
    mobileTab === 'flavor' && !showFlavorTab ? 'score' : mobileTab;

  const agentChat = useSessionAgentChat(sessionId ?? '', agentSessionId, {
    persistHistory: !fixture,
    gameContext: agentGameContext,
  });

  // #2588 A4: SignalR connection for dispute hydration.
  // Mounted once at orchestrator level so DisputeResolved events populate
  // useLiveSessionStore.disputes even when the user is on another tab.
  // Self-tears-down on unmount/sessionId change (hook contract).
  // Disabled in fixture/visual-test builds to avoid real hub connections.
  useSignalRSession(!fixture ? (sessionId ?? '') : '');

  // #2588 A3: local state for image-path messages (ask-agent JSON endpoint).
  // These are merged into agentChatMessages below so they appear in the same panel.
  const [imageMessages, setImageMessages] = useState<LiveAgentChatMessage[]>([]);

  // Map useSessionAgentChat.ChatMessage → LiveAgentChat.ChatMessage.
  // The two shapes differ: agent uses role:'user'|'assistant', LiveAgentChat uses
  // senderId/senderName/visibility.  We map:
  //   role:'user'      → senderId=viewerId (consistent with how LiveAgentChat
  //                       computes isOwn; '' when viewerId not yet known — R-FINDING-6)
  //   role:'assistant' → senderId='agent', senderName='MeepleAI', visibility:'shared'
  // Citations pass through unchanged (same ChatCitation type, shared via ChatCitationCard import).
  // When the launch is not yet ready, a single system message is prepended so the user
  // always sees feedback instead of a silent empty panel (R-FINDING-5 / AC-CHAT-NULL).
  const agentChatMessages = useMemo<LiveAgentChatMessage[]>(() => {
    const viewerId = activeSession?.viewerId ?? '';
    const realMessages = agentChat.messages.map(m => ({
      id: m.id,
      senderId: m.role === 'user' ? viewerId : 'agent',
      senderName: m.role === 'user' ? '' : 'MeepleAI',
      content: m.content,
      visibility: 'shared' as const,
      timestamp: m.timestamp,
      citations: m.citations,
      // AC-CHAT-3: propagate isNonGrounded ONLY from hook messages.
      // System status messages (prepended below) do NOT get this flag → no disclaimer.
      isNonGrounded: m.isNonGrounded,
    }));

    // Prepend a system status message when not ready (R-FINDING-5).
    // 'idle' → preconditions not met yet, no message needed.
    let statusContent: string | null = null;
    if (agentLaunch.status === 'launching') {
      statusContent = t('pages.sessionLive.chatAgent.launchingMessage');
    } else if (agentLaunch.status === 'no-agent') {
      statusContent = t('pages.sessionLive.chatAgent.noAgentMessage');
    } else if (agentLaunch.status === 'error') {
      statusContent = t('pages.sessionLive.chatAgent.errorMessage');
    }

    // #2588 A3: merge image-path messages (ask-agent) with RAG messages, sorted by timestamp.
    const merged = [...realMessages, ...imageMessages].sort((a, b) =>
      a.timestamp.localeCompare(b.timestamp)
    );

    if (statusContent != null) {
      const statusMessage: LiveAgentChatMessage = {
        id: `agent-status-${agentLaunch.status}`,
        senderId: 'agent',
        senderName: 'MeepleAI',
        content: statusContent,
        visibility: 'shared' as const,
        timestamp: new Date().toISOString(),
      };
      return [statusMessage, ...merged];
    }

    return merged;
  }, [agentChat.messages, activeSession?.viewerId, agentLaunch.status, t, imageMessages]);

  // AC-CHAT-0: send goes to the RAG agent, NOT /game-sessions/{id}/chat.
  // AC-CHAT-NULL: if agentLaunch.status !== 'ready', agent is not available yet —
  // the status message above provides user feedback; suppress the actual ask() call.
  // #2588 A3: dual-path — images → /ask-agent (multipart JSON); text-only → RAG SSE.
  const handleAgentSendMessage = useCallback(
    async (
      content: string,
      _visibility: 'private' | 'shared',
      images?: ChatImagePreview[]
    ): Promise<void> => {
      if (images && images.length > 0) {
        // Image path: multipart POST to /ask-agent (JSON response, not SSE).
        if (sessionId == null) return;
        const question =
          content.trim() ||
          intl.formatMessage({ id: 'pages.sessionLive.chatAgent.imageAsk.defaultQuestion' });
        const fd = new FormData();
        fd.append('question', question);
        fd.append('senderId', activeSession?.viewerId ?? '');
        images.forEach(img => fd.append('images', img.file));

        // Optimistically append user message so the UI feels responsive.
        const userMsgId = crypto.randomUUID();
        const now = new Date().toISOString();
        setImageMessages(prev => [
          ...prev,
          {
            id: userMsgId,
            senderId: activeSession?.viewerId ?? '',
            senderName: '',
            content: question,
            visibility: 'shared' as const,
            timestamp: now,
          },
        ]);

        try {
          const res = await fetch(`/api/v1/game-sessions/${sessionId}/chat/ask-agent`, {
            method: 'POST',
            credentials: 'include',
            body: fd,
          });
          if (!res.ok) throw new Error(`ask-agent ${res.status}`);
          const json = (await res.json()) as { answer?: string; confidence?: number };
          setImageMessages(prev => [
            ...prev,
            {
              id: crypto.randomUUID(),
              senderId: 'agent',
              senderName: 'MeepleAI',
              content:
                json.answer ??
                intl.formatMessage({ id: 'pages.sessionLive.chatAgent.imageAsk.fallbackResponse' }),
              visibility: 'shared' as const,
              timestamp: new Date().toISOString(),
            },
          ]);
        } catch {
          toast.error(
            intl.formatMessage({ id: 'pages.sessionLive.chatAgent.imageAsk.errorToast' }),
            {
              id: 'image-ask-error',
            }
          );
          // Remove the optimistic user message on failure.
          setImageMessages(prev => prev.filter(m => m.id !== userMsgId));
        }
      } else {
        // Text-only RAG path (unchanged).
        if (!agentSessionId || !content.trim()) return;
        await agentChat.ask(content);
      }
    },
    // `intl` is stable (react-intl memoizes useIntl); listing it clears a pre-existing
    // exhaustive-deps baseline error surfaced while touching this file for SI-4 (#2635).
    [agentSessionId, agentChat, sessionId, activeSession?.viewerId, intl]
  );

  // ── Chat messages from SSE events ────────────────────────────────────────
  // NOTE: ChatAgentPanel now receives agentChatMessages (RAG) — see above.
  // ActionLogTimeline uses activeSession.actionLog directly; this extraction
  // is no longer needed and has been removed to avoid unused-var lint errors.

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
          messages={agentChatMessages}
          viewerRole={activeSession.viewerRole}
          viewerId={activeSession.viewerId}
          onSendMessage={handleAgentSendMessage}
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
    agentChatMessages,
    handleAgentSendMessage,
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
    switch (effectiveMobileTab) {
      case 'flavor':
        return liveSessionDto != null ? (
          <FlavorRenderer
            gameSlug={liveSessionDto.gameSlug}
            view="live"
            session={liveSessionDto}
            labels={catanFlavorLabels}
            livePoints={catanLivePoints}
            phaseName={catanPhaseName}
            className="p-3"
          />
        ) : null;
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
              onAddPlayer={
                hasRequiredRole(activeSession.viewerRole, 'Host') ? handleAddPlayer : undefined
              }
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
      case 'photos':
        return (
          <PhotosTabContent
            sessionId={sessionId ?? ''}
            userId={currentUser?.id ?? ''}
            currentTurn={activeSession.currentTurn}
          />
        );
      case 'agent':
        return (
          <AgentDisputeTabContent
            sessionId={sessionId ?? ''}
            players={activeSession.players.map(p => ({
              id: p.id,
              name:
                ('displayName' in p ? (p.displayName as string | undefined) : undefined) ?? p.name,
            }))}
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
    effectiveMobileTab,
    activeSession,
    liveSessionDto,
    catanFlavorLabels,
    catanLivePoints,
    catanPhaseName,
    sessionId,
    currentUser?.id,
    scoringPanelLabels,
    turnRendererState,
    turnRendererPlayers,
    turnRendererLabels,
    rosterLabels,
    handleAddPlayer,
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
        messages={agentChatMessages}
        viewerRole={activeSession.viewerRole}
        viewerId={activeSession.viewerId}
        onSendMessage={handleAgentSendMessage}
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
    <RightColumnTabs
      activeTab={effectiveTab}
      onTabChange={handleTabChange}
      labels={rightColumnTabsLabels}
      showFlavorTab={showFlavorTab}
    >
      {effectiveTab === 'flavor' && liveSessionDto != null && (
        <FlavorRenderer
          gameSlug={liveSessionDto.gameSlug}
          view="live"
          session={liveSessionDto}
          labels={catanFlavorLabels}
          livePoints={catanLivePoints}
          phaseName={catanPhaseName}
          className="p-3"
        />
      )}
      {effectiveTab === 'score' && (
        <ScoreTabContent
          sessionId={sessionId ?? ''}
          viewerRole={activeSession.viewerRole}
          players={activeSession.players}
          labels={scoringPanelLabels}
          className="p-3"
        />
      )}
      {effectiveTab === 'turn' && (
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
            onAddPlayer={
              hasRequiredRole(activeSession.viewerRole, 'Host') ? handleAddPlayer : undefined
            }
            labels={rosterLabels}
          />
        </div>
      )}
      {effectiveTab === 'widget' && (
        <ToolkitRenderer
          widgets={toolkitWidgets}
          openWidgetId={toolkitOpenId}
          onOpenWidgetChange={setToolkitOpen}
          onWidgetConfigChange={(id, cfg) => void updateToolkitConfig(id, cfg)}
          players={toolkitPlayers}
          labels={toolkitRendererLabels}
        />
      )}
      {effectiveTab === 'notes' && (
        <LiveSessionNotes
          notes={noteEntries}
          viewerRole={activeSession.viewerRole}
          viewerId={activeSession.viewerId}
          onAddNote={handleAddNote}
          labels={notesLabels}
        />
      )}
      {effectiveTab === 'photos' && (
        <PhotosTabContent
          sessionId={sessionId ?? ''}
          userId={currentUser?.id ?? ''}
          currentTurn={activeSession.currentTurn}
        />
      )}
      {effectiveTab === 'agent' && (
        <AgentDisputeTabContent
          sessionId={sessionId ?? ''}
          players={activeSession.players.map(p => ({
            id: p.id,
            name:
              ('displayName' in p ? (p.displayName as string | undefined) : undefined) ?? p.name,
          }))}
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
        onEndgame={
          hasRequiredRole(activeSession.viewerRole, 'Host') ? handleRequestEndgame : undefined
        }
        labels={topBarLabels}
        elapsedMs={elapsedMs}
        startedAtLabel={startedAtLabel}
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
        sheetActiveTab={effectiveMobileTab}
        onSheetTabChange={handleMobileTabChange}
        sheetContent={mobileSheetContent}
        labels={mobileBodyLabels}
        showFlavorTab={showFlavorTab}
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
            recordId={resolvedPlayRecordId}
            onAcknowledge={() => handleDialogChange('none')}
            onSave={hasRequiredRole(activeSession.viewerRole, 'Host') ? handleSaveGame : undefined}
            saving={saveIntent && resolveStatus === 'resolving'}
            labels={{
              title: t('pages.sessionLive.endgameDialog.title'),
              winnerLabel: t('pages.sessionLive.endgameDialog.winnerLabel'),
              acknowledgeCta: t('pages.sessionLive.endgameDialog.acknowledgeCta'),
              viewSummaryCta: t('pages.sessionLive.endgameDialog.viewSummaryCta'),
              saveGameCta: t('pages.sessionLive.endgameDialog.saveGameCta'),
              savingLabel: t('pages.sessionLive.endgameDialog.savingLabel'),
            }}
          />
        </Suspense>
      )}

      {/* #2503: Endgame confirm dialog — Host-only, shown before POST /complete */}
      {endgameConfirmOpen && (
        <div
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="endgame-confirm-title"
          data-slot="endgame-confirm-dialog"
          className="fixed inset-0 z-50 flex items-center justify-center bg-foreground/80"
        >
          <div className="w-full max-w-sm rounded-xl border border-border/60 bg-card p-6 shadow-2xl">
            <h2 id="endgame-confirm-title" className="mb-2 text-base font-semibold text-foreground">
              {t('pages.sessionLive.endgameConfirm.title')}
            </h2>
            <p className="mb-6 text-sm text-muted-foreground">
              {t('pages.sessionLive.endgameConfirm.body')}
            </p>
            <div className="flex gap-3">
              <button
                type="button"
                onClick={() => setEndgameConfirmOpen(false)}
                data-slot="endgame-confirm-cancel"
                className="flex-1 rounded-lg border border-border px-4 py-2.5 text-sm
                  font-medium text-foreground hover:bg-muted
                  focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                {t('pages.sessionLive.endgameConfirm.cancelCta')}
              </button>
              <button
                type="button"
                onClick={() => void handleConfirmEndgame()}
                disabled={completeLiveSession.isPending}
                data-slot="endgame-confirm-cta"
                className="flex-1 rounded-lg bg-destructive px-4 py-2.5 text-sm font-semibold
                  text-destructive-foreground hover:opacity-90 disabled:opacity-60
                  focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                {t('pages.sessionLive.endgameConfirm.confirmCta')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* #2505: Host-only AddPlayerDialog — uses dto.players for color slots (LiveSessionFixturePlayer has no color field) */}
      {addPlayerOpen && sessionId != null && (
        <Suspense fallback={null}>
          <AddPlayerDialog
            sessionId={sessionId}
            players={sessionQuery.data?.players ?? []}
            open={addPlayerOpen}
            onClose={() => setAddPlayerOpen(false)}
            labels={{
              dialogTitle: t('pages.sessionLive.roster.addPlayerDialogTitle'),
              guestTab: t('pages.sessionLive.roster.guestTab'),
              registeredTab: t('pages.sessionLive.roster.registeredTab'),
              displayNameLabel: t('pages.sessionLive.roster.displayNameLabel'),
              displayNamePlaceholder: t('pages.sessionLive.roster.displayNamePlaceholder'),
              searchUserPlaceholder: t('pages.sessionLive.roster.searchUserPlaceholder'),
              confirmCta: t('pages.sessionLive.roster.confirmCta'),
              cancelCta: t('pages.sessionLive.roster.cancelCta'),
              errorNoColorAvailable: t('pages.sessionLive.roster.errorNoColorAvailable'),
              errorDuplicateName: t('pages.sessionLive.roster.errorDuplicateName'),
              errorColorTaken: t('pages.sessionLive.roster.errorColorTaken'),
              errorGeneric: t('pages.sessionLive.roster.errorGeneric'),
            }}
          />
        </Suspense>
      )}
    </div>
  );
}
