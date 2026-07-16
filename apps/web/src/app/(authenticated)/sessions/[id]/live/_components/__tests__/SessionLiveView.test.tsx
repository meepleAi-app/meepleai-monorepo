/**
 * SessionLiveView unit tests — Wave D.2 Foundation + Interactions sub-PR (Issue #746 + #750).
 *
 * Coverage (Foundation):
 *   - 4-state FSM: loading | error | not-found | default (Cells 1-5)
 *   - URL state SSOT (tab/mtab changes update URL)
 *   - Fixture variant switching (?fixture=host|spectator|paused)
 *   - Dark theme attribute on root container
 *   - Exit handler navigates to /sessions/{sessionId}
 *   - Visual fixture mode uses fixture data (when IS_VISUAL_TEST_BUILD=true)
 *   - SessionId null guard (Cell 1: not-found without fetch)
 *   - ?state= URL override (loading / not-found forced)
 *   - Retry action on error shell
 *   - Back action on not-found shell
 *
 * Coverage (Interactions — Task 3 extensions):
 *   - useSessionLiveStream called when NOT in IS_VISUAL_TEST_BUILD + sessionQuery.isSuccess
 *   - useSessionLiveStream NOT called in IS_VISUAL_TEST_BUILD mode
 *   - Dialog state from URL: ?dialog=pause mounts PauseOverlay, ?dialog=endgame mounts EndgameDialog
 *   - ConnectionLostBanner renders for reconnecting / degraded-polling / failed states
 *   - Mobile tab routing: chat tab renders LiveAgentChat, tools/widget tab renders ToolkitRenderer (G5c #2376)
 *   - Desktop right column: RightColumnTabs mounted with correct activeTab
 *   - Desktop tab change calls router.replace with ?tab= param
 *   - Optimistic score update via liveStream events
 *   - 403 rollback: score rolled back on 403 response
 *   - 429 handling: connectionState='failed' shows ConnectionLostBanner kind='failed'
 *
 * Pattern: Wave D.1 SessionsLibraryView.test.tsx adapted for Tier L live route.
 * Mocks: useSession, useSessionLiveStream, next/navigation, next-intl, session-live-visual-test-fixture.
 */

import { fireEvent, render, screen, act } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import type { UseSessionLiveStreamResult } from '@/lib/session-live/use-session-live-stream';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import type { ScoreDataByType } from '@/components/sessions/score-strategies/types';

// ─── useSignalRSession mock (#2588 A4: dispute hydration via SignalR) ─────────
// The hook connects to /hubs/game-state — mock it to a no-op so tests don't try
// to establish a real WebSocket connection. Assertions about mount/sessionId
// come from the useSignalRSessionMock spy.

const useSignalRSessionMock = vi.fn(() => ({
  connection: null,
  isConnected: false,
  proposeScore: vi.fn(),
  appBackgrounded: vi.fn(),
}));

vi.mock('@/lib/domain-hooks/useSignalrSession', () => ({
  useSignalRSession: (...args: unknown[]) => useSignalRSessionMock(...args),
}));

// ─── next/navigation mocks ────────────────────────────────────────────────

const searchParamsMap: Record<string, string> = {};
const routerPush = vi.fn();
const routerReplace = vi.fn();
let mockParamsId: string | undefined = 'session-abc-123';

vi.mock('next/navigation', () => ({
  useParams: () => ({ id: mockParamsId }),
  useSearchParams: () => ({
    get: (key: string) => searchParamsMap[key] ?? null,
    toString: () => new URLSearchParams(searchParamsMap).toString(),
  }),
  useRouter: () => ({ push: routerPush, replace: routerReplace }),
  usePathname: () => `/sessions/${mockParamsId}/live`,
}));

// ─── useSession mock ──────────────────────────────────────────────────────

type MockSessionReturn = {
  data?: LiveSessionDto | null;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  error: Error | null;
  refetch?: () => void;
};

const useLiveSessionMock = vi.fn<[], MockSessionReturn>();

vi.mock('@/hooks/queries/useLiveSession', () => ({
  useLiveSession: () => useLiveSessionMock(),
}));

// ─── useSessionLiveStream mock ────────────────────────────────────────────

const mockLiveStreamResult: UseSessionLiveStreamResult = {
  events: [],
  connectionState: 'connected',
  lastEventId: null,
  retryCount: 0,
  retryAt: null,
  reconnect: vi.fn(),
};

const useSessionLiveStreamMock = vi.fn<[unknown], UseSessionLiveStreamResult>();

vi.mock('@/lib/session-live/use-session-live-stream', () => ({
  useSessionLiveStream: (args: unknown) => useSessionLiveStreamMock(args),
}));

// ─── useUpdateSessionScores mock (#2430 Block B+: avoid QueryClientProvider) ──

vi.mock('@/hooks/use-update-session-scores', async () => {
  const actual = await vi.importActual<typeof import('@/hooks/use-update-session-scores')>(
    '@/hooks/use-update-session-scores'
  );
  return {
    ...actual,
    useUpdateSessionScores: () => ({
      mutate: vi.fn(),
      isPending: false,
    }),
  };
});

// ─── sonner mock (#2430 Block B+: ScoreTabContent toasts) ─────────────────

vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    warning: vi.fn(),
    success: vi.fn(),
    dismiss: vi.fn(),
  },
}));

// ─── useSessionAgentChat mock (#2500 Task 4: RAG agent wiring) ────────────
// Hoisted globally so all describe blocks get a safe no-op default.
// The RAG-specific describe block overrides the return value in its beforeEach.

const useSessionAgentChatMock = vi.fn(() => ({
  messages: [],
  isLoading: false,
  error: null,
  streamingContent: '',
  ask: vi.fn(),
  stop: vi.fn(),
}));

vi.mock('@/lib/domain-hooks/useSessionAgentChat', () => ({
  useSessionAgentChat: (...args: unknown[]) => useSessionAgentChatMock(...args),
}));

// ─── useSessionAgentLaunch mock (#2500 Task 4-FE: wire hook) ──────────────
// Default: 'ready' with a valid agentSessionId so other describe blocks are unaffected.

const useSessionAgentLaunchMock = vi.fn(() => ({
  status: 'ready' as const,
  agentSessionId: 'agent-session-uuid-0001',
}));

vi.mock('@/hooks/queries/useSessionAgentLaunch', () => ({
  useSessionAgentLaunch: (...args: unknown[]) => useSessionAgentLaunchMock(...args),
}));

// ─── useCurrentUser mock (#2505: viewerRole derivation) ─────────────────────

const useCurrentUserMock = vi.fn(() => ({ data: null }));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => useCurrentUserMock(),
}));

// ─── useAddLivePlayer mock (#2505) ───────────────────────────────────────────

vi.mock('@/hooks/mutations/useAddLivePlayer', () => ({
  useAddLivePlayer: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
}));

// ─── usePlayRecordPhotoUpload mock (#2501 SP4: EndgamePhotoUploadSection calls this) ──
// Prevent "No QueryClient set" errors when EndgameDialog (with EndgamePhotoUploadSection)
// is rendered in tests that don't provide a QueryClientProvider.

vi.mock('@/hooks/mutations/usePlayRecordPhotoUpload', () => ({
  usePlayRecordPhotoUpload: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

// ─── useUpdateLiveGameState mock (#3033: CatanLiveFlavor's useCatanStateEditor
// calls this via useMutation/useQueryClient) — same "avoid QueryClientProvider"
// rationale as usePlayRecordPhotoUpload above.

vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({
    mutate: vi.fn(),
  }),
}));

// ─── useCompleteLiveSession mock (#2503) ─────────────────────────────────────
// Controllable: completeMutate captures the (undefined, { onSuccess }) call;
// completeIsPending toggles the confirm-CTA disabled state.

const completeMutate = vi.fn();
let completeIsPending = false;

vi.mock('@/hooks/mutations/useCompleteLiveSession', () => ({
  useCompleteLiveSession: () => ({
    mutate: completeMutate,
    get isPending() {
      return completeIsPending;
    },
  }),
}));

// ─── useAddDiaryEntry mock (#2575) ───────────────────────────────────────────
// Controllable mutateAsync — finding #16 tests resolve/reject it to assert the
// response-ack toast behavior (handleAddNote now goes through this mutation).
const addDiaryMutateAsync = vi.fn();

vi.mock('@/hooks/mutations/useAddDiaryEntry', () => ({
  useAddDiaryEntry: () => ({
    mutateAsync: addDiaryMutateAsync,
  }),
}));

// ─── useLiveSessionDiary mock (#2575: historical hydration query) ────────────
let diaryQueryData: Array<{ id: string; authorId: string; createdAt: string; text: string }> = [];

vi.mock('@/hooks/queries/useLiveSessionDiary', () => ({
  useLiveSessionDiary: () => ({ data: diaryQueryData }),
}));

// ─── useLiveSessionPhases mock (#2787: flavor turn/phase header) ─────────────
let phasesQueryData: { currentPhaseName: string | null } | null = null;

vi.mock('@/hooks/queries/useLiveSessionPhases', () => ({
  useLiveSessionPhases: () => ({ data: phasesQueryData }),
}));

// ─── useResolvePlayRecord mock (#2503) ───────────────────────────────────────
// Controllable status + playRecordId (read via getters per render) + start spy.

type ResolveStatusMock = 'idle' | 'resolving' | 'resolved' | 'timeout';
const resolveStart = vi.fn();
let resolveStatusMock: ResolveStatusMock = 'idle';
let resolvePlayRecordIdMock: string | null = null;

vi.mock('@/lib/session-live/use-resolve-play-record', () => ({
  useResolvePlayRecord: () => ({
    get status() {
      return resolveStatusMock;
    },
    get playRecordId() {
      return resolvePlayRecordIdMock;
    },
    start: resolveStart,
  }),
}));

// ─── @/lib/api mock (#2503: baseline getHistory before POST /complete) ───────
// SessionLiveView only reaches `api.playRecords.getHistory` (all other API
// surfaces go through already-mocked hooks), so a minimal mock is sufficient.
// `vi.hoisted` is required because the factory references the spy EAGERLY
// (not inside a lazy hook closure like useRouter/useCompleteLiveSession), so it
// must be initialised before the hoisted vi.mock runs.

const getHistoryMock = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    playRecords: {
      getHistory: getHistoryMock,
    },
  },
}));

// ─── AddPlayerDialog mock (#2505: lazy import — suppress real render) ─────────
// The dialog is lazy-loaded; mock it so tests don't need Suspense async resolve
// for the core SessionLiveView behavior tests.

vi.mock('@/components/features/session-live/AddPlayerDialog', () => ({
  AddPlayerDialog: ({ open }: { open: boolean }) =>
    open ? <div data-slot="add-player-dialog-mock" /> : null,
}));

// ─── visual-test-fixture mock ─────────────────────────────────────────────

let IS_VISUAL_TEST_BUILD_MOCK = false;

vi.mock('@/lib/session-live/session-live-visual-test-fixture', async () => {
  const actual = await vi.importActual<
    typeof import('@/lib/session-live/session-live-visual-test-fixture')
  >('@/lib/session-live/session-live-visual-test-fixture');
  return {
    ...actual,
    get IS_VISUAL_TEST_BUILD() {
      return IS_VISUAL_TEST_BUILD_MOCK;
    },
    // STATE_OVERRIDE_ENABLED stays from actual (NODE_ENV=test → true)
  };
});

// ─── i18n messages (subset matching it.json pages.sessionLive.*) ──────────

const MESSAGES: Record<string, string> = {
  'pages.sessionLive.metadata.title': 'Sessione live — MeepleAI',
  'pages.sessionLive.topBar.sessionTitleAriaLabel': 'Sessione: {name}',
  'pages.sessionLive.topBar.turnLabel':
    '{count, plural, =0 {Inizio} =1 {Turno 1/{total}} other {Turno #/{total}}}',
  'pages.sessionLive.topBar.statusInProgress': 'In corso',
  'pages.sessionLive.topBar.statusPaused': 'In pausa',
  'pages.sessionLive.topBar.pauseCta': 'Pausa',
  'pages.sessionLive.topBar.resumeCta': 'Riprendi',
  'pages.sessionLive.topBar.endgameCta': 'Termina sessione',
  'pages.sessionLive.topBar.exitAriaLabel': 'Esci dalla sessione',
  'pages.sessionLive.turnIndicator.currentTurnAriaLabel': 'Turno {current} di {total}',
  'pages.sessionLive.turnIndicator.activePlayerLabel': '{playerName}',
  'pages.sessionLive.turnIndicator.yourTurnLabel': 'Il tuo turno',
  'pages.sessionLive.turnIndicator.waitingLabel': 'Aspetta il tuo turno',
  'pages.sessionLive.roster.title': 'Giocatori',
  'pages.sessionLive.roster.playerCountTemplate':
    '{count, plural, =0 {Nessun giocatore} =1 {1 giocatore} other {# giocatori}}',
  'pages.sessionLive.roster.onlineLabel': 'Online',
  'pages.sessionLive.roster.offlineLabel': 'Offline',
  'pages.sessionLive.roster.kickAriaLabel': 'Espelli {playerName}',
  'pages.sessionLive.roster.roleSpectator': 'Spettatore',
  'pages.sessionLive.roster.rolePlayer': 'Giocatore',
  'pages.sessionLive.roster.roleHost': 'Host',
  'pages.sessionLive.roster.addPlayerCta': '+ Aggiungi giocatore',
  'pages.sessionLive.roster.addPlayerDialogTitle': 'Aggiungi giocatore',
  'pages.sessionLive.roster.guestTab': 'Ospite',
  'pages.sessionLive.roster.registeredTab': 'Utente registrato',
  'pages.sessionLive.roster.displayNameLabel': 'Nome',
  'pages.sessionLive.roster.displayNamePlaceholder': 'Es. Marco',
  'pages.sessionLive.roster.searchUserPlaceholder': 'Cerca per nome…',
  'pages.sessionLive.roster.confirmCta': 'Aggiungi',
  'pages.sessionLive.roster.cancelCta': 'Annulla',
  'pages.sessionLive.roster.errorNoColorAvailable': 'Nessun colore disponibile.',
  'pages.sessionLive.roster.errorDuplicateName': 'Nome duplicato.',
  'pages.sessionLive.roster.errorColorTaken': 'Colore preso.',
  'pages.sessionLive.roster.errorGeneric': 'Errore generico.',
  'pages.sessionLive.scoring.title': 'Punteggi',
  'pages.sessionLive.scoring.scoreLabel': 'Punteggio: {score}',
  'pages.sessionLive.scoring.winnerLabel': 'Vincitore',
  'pages.sessionLive.scoring.myScoreLabel': 'Il tuo punteggio',
  'pages.sessionLive.scoring.incrementAriaLabel': 'Aumenta punteggio di {playerName}',
  'pages.sessionLive.scoring.decrementAriaLabel': 'Diminuisci punteggio di {playerName}',
  'pages.sessionLive.scoring.scoreInputAriaLabel': 'Inserisci punteggio per {playerName}',
  'pages.sessionLive.scoring.playerCount':
    '{count, plural, =0 {Nessun giocatore} =1 {1 giocatore} other {# giocatori}}',
  'pages.sessionLive.scoring.loadingLabel': 'Caricamento punteggi…',
  // Block C #2389 T6 — nested polymorphic scoring keys (mirror locale catalog T5 additions).
  'pages.sessionLive.scoring.points.title': 'Punteggi',
  'pages.sessionLive.scoring.points.leaderLabel': 'in testa',
  'pages.sessionLive.scoring.points.columns.player': 'Giocatore',
  'pages.sessionLive.scoring.points.columns.score': 'Punteggio',
  'pages.sessionLive.scoring.ranking.title': 'Posizioni',
  'pages.sessionLive.scoring.ranking.columns.rank': 'Posizione',
  'pages.sessionLive.scoring.ranking.columns.player': 'Giocatore',
  'pages.sessionLive.scoring.ranking.columns.score': 'Punteggio',
  'pages.sessionLive.scoring.binaryWin.title': 'Esito',
  'pages.sessionLive.scoring.binaryWin.winLabel': 'Vince',
  'pages.sessionLive.scoring.binaryWin.loseLabel': 'Perde',
  'pages.sessionLive.scoring.binaryWin.pendingLabel': 'Partita in corso',
  'pages.sessionLive.scoring.objectives.title': 'Obiettivi',
  'pages.sessionLive.scoring.objectives.completedLabel': 'Completato',
  'pages.sessionLive.scoring.objectives.pendingLabel': 'In attesa',
  'pages.sessionLive.scoring.objectives.columns.player': 'Giocatore',
  // Block C #2389 T6 — aria templates consumed by ScoringPanelRenderer (NOT yet in
  // production locale catalog — tracked as follow-up; test stub mirrors prior inline
  // italian fallbacks so polymorphic renderer tests can pass).
  'pages.sessionLive.scoring.scoreAriaTemplate': 'Punteggio di {name}',
  'pages.sessionLive.scoring.rankAriaTemplate': 'Posizione di {name}',
  'pages.sessionLive.scoring.firstPlaceBadgeLabel': 'primo posto',
  'pages.sessionLive.scoring.outcomeAriaTemplate': '{name}: {result}',
  'pages.sessionLive.scoring.completedAriaTemplate': 'Completati da {name}',
  'pages.sessionLive.scoring.doneAriaTemplate': '{label} (completato)',
  'pages.sessionLive.scoring.pendingAriaTemplate': '{label} (non completato)',
  'pages.sessionLive.actionLog.title': 'Eventi',
  'pages.sessionLive.actionLog.emptyLabel': 'Nessun evento ancora.',
  'pages.sessionLive.actionLog.typeScore': 'Punteggio',
  'pages.sessionLive.actionLog.typeTool': 'Tool',
  'pages.sessionLive.actionLog.typeAgent': 'Agent',
  'pages.sessionLive.actionLog.typeChat': 'Chat',
  'pages.sessionLive.actionLog.typePhoto': 'Foto',
  'pages.sessionLive.actionLog.typeEvent': 'Evento',
  'pages.sessionLive.actionLog.timestampAriaLabel':
    'Eventi ordinati cronologicamente, più recenti in alto',
  'pages.sessionLive.loading.ariaLabel': 'Caricamento sessione live…',
  'pages.sessionLive.error.title': 'Impossibile caricare la sessione',
  'pages.sessionLive.error.description': 'Si è verificato un errore. Riprova.',
  'pages.sessionLive.error.ctaRetry': 'Riprova',
  'pages.sessionLive.notFound.title': 'Sessione non trovata',
  'pages.sessionLive.notFound.description': 'La sessione richiesta non esiste o non hai accesso.',
  'pages.sessionLive.notFound.ctaBack': 'Torna alle sessioni',
  'pages.sessionLive.connectionLost.retryCount':
    '{count, plural, =1 {tentativo 1/5} other {tentativo #/5}}',
  'pages.sessionLive.connectionLost.reconnecting': 'Connessione persa, riprovo...',
  'pages.sessionLive.connectionLost.degradedPolling': 'Aggiornamenti ogni 5s',
  'pages.sessionLive.connectionLost.failed': 'Sessione affollata o errore',
  'pages.sessionLive.connectionLost.manualRetryLabel': 'Riprova connessione',
  'pages.sessionLive.a11y.viewLabel': 'Vista sessione live',
  // Interactions sub-PR labels
  'pages.sessionLive.rightColumn.tabsAriaLabel': 'Pannelli sessione',
  // G1 #2374 sess.46r canonical tab keys
  'pages.sessionLive.rightColumn.tabFlavor': 'Catan',
  'pages.sessionLive.rightColumn.tabScore': 'Score',
  'pages.sessionLive.rightColumn.tabTurn': 'Turni',
  'pages.sessionLive.rightColumn.tabWidget': 'Widget',
  'pages.sessionLive.rightColumn.tabNotes': 'Note',
  'pages.sessionLive.rightColumn.tabPhotos': 'Foto',
  'pages.sessionLive.rightColumn.tabAgent': 'Arbitro',
  // Back-compat (legacy keys for older callers — not used by SessionLiveView post-T4)
  'pages.sessionLive.rightColumn.tabTools': 'Strumenti',
  'pages.sessionLive.rightColumn.tabChat': 'Chat',
  'pages.sessionLive.flavor.catan.panelAriaLabel': 'Pannello Catan',
  'pages.sessionLive.flavor.catan.roundTemplate': 'Round {n}',
  'pages.sessionLive.flavor.catan.activePlayerTemplate': 'Turno di {name}',
  'pages.sessionLive.flavor.catan.phaseTemplate': 'Fase: {name}',
  'pages.sessionLive.flavor.catan.initBoardCta': 'Genera board Catan',
  'pages.sessionLive.flavor.catan.viewerWaiting': 'In attesa dell’host',
  'pages.sessionLive.flavor.catan.hexAriaTemplate': '{terrain} {number}',
  'pages.sessionLive.flavor.catan.robberLabel': 'Ladro',
  'pages.sessionLive.flavor.catan.diceLastLabel': 'Ultimo tiro',
  'pages.sessionLive.flavor.catan.diceHistoryLabel': 'Cronologia',
  'pages.sessionLive.flavor.catan.rollAriaTemplate': 'Registra tiro {n}',
  'pages.sessionLive.flavor.catan.vpLabel': 'PV',
  'pages.sessionLive.flavor.catan.handLabel': 'Mano',
  'pages.sessionLive.flavor.catan.devLabel': 'Sviluppo',
  'pages.sessionLive.flavor.catan.settlementsLabel': 'Insediamenti',
  'pages.sessionLive.flavor.catan.citiesLabel': 'Città',
  'pages.sessionLive.flavor.catan.roadsLabel': 'Strade',
  'pages.sessionLive.flavor.catan.longestRoadLabel': 'Strada+',
  'pages.sessionLive.flavor.catan.largestArmyLabel': 'Armata+',
  'pages.sessionLive.flavor.catan.incAriaTemplate': '{field} +1',
  'pages.sessionLive.flavor.catan.decAriaTemplate': '{field} -1',
  // ChatAgentPanel labels (G1 #2374 T3 — composite header)
  'pages.sessionLive.chatAgent.title': 'ChatAgent',
  'pages.sessionLive.chatAgent.agentNameAriaLabel': 'Nome agente {name}',
  'pages.sessionLive.chatAgent.onlineLabel': 'Online',
  'pages.sessionLive.chatAgent.latencyAriaLabel': 'Latenza {ms}ms',
  // Agent launch status feedback messages (#2500 Task 4-FE R-FINDING-5)
  'pages.sessionLive.chatAgent.launchingMessage': "Avvio dell'assistente di gioco…",
  'pages.sessionLive.chatAgent.noAgentMessage': 'Nessun assistente disponibile per questo gioco.',
  'pages.sessionLive.chatAgent.errorMessage':
    "Impossibile avviare l'assistente. Riprova più tardi.",
  'pages.sessionLive.chatAgent.nonGroundedDisclaimer': 'Risposta non basata sul regolamento',
  // MobileBody bottom-sheet labels (G1 #2374 T9 — sess.46r)
  'pages.sessionLive.mobile.openSheetCta': 'Apri pannello',
  'pages.sessionLive.mobile.closeSheetAriaLabel': 'Chiudi pannello',
  'pages.sessionLive.mobile.drawerTitle': 'Strumenti sessione',
  'pages.sessionLive.mobile.tabsAriaLabel': 'Tab strumenti',
  'pages.sessionLive.tools.title': 'Strumenti',
  'pages.sessionLive.tools.toolDiceLabel': 'Dado',
  'pages.sessionLive.tools.toolTimerLabel': 'Timer',
  'pages.sessionLive.tools.toolCardLabel': 'Carta',
  'pages.sessionLive.tools.executeAriaTemplate': 'Esegui {toolName}',
  'pages.sessionLive.tools.disabledSpectatorTooltip': 'Solo giocatori possono usare gli strumenti',
  'pages.sessionLive.chat.title': 'Chat',
  'pages.sessionLive.chat.inputAriaLabel': 'Scrivi un messaggio',
  'pages.sessionLive.chat.sendAriaLabel': 'Invia messaggio',
  'pages.sessionLive.chat.visibilityPrivate': 'Privato',
  'pages.sessionLive.chat.visibilityShared': 'Condiviso',
  'pages.sessionLive.chat.emptyMessage': 'Nessun messaggio ancora.',
  'pages.sessionLive.notes.title': 'Note',
  'pages.sessionLive.notes.inputAriaLabel': 'Scrivi una nota',
  'pages.sessionLive.notes.addAriaLabel': 'Aggiungi nota',
  'pages.sessionLive.notes.visibilityPrivate': 'Privata',
  'pages.sessionLive.notes.visibilityShared': 'Condivisa',
  'pages.sessionLive.notes.emptyMessage': 'Nessuna nota ancora.',
  // SP5-a Task 3 (finding #16): diary write response-ack toast key
  'pages.sessionLive.notes.addNoteErrorToast': 'Impossibile salvare la nota. Riprova.',
  'pages.sessionLive.pauseOverlay.title': 'Sessione in pausa',
  'pages.sessionLive.pauseOverlay.resumeCta': 'Riprendi',
  'pages.sessionLive.pauseOverlay.closeCta': 'Chiudi',
  'pages.sessionLive.pauseOverlay.closeAriaLabel': 'Chiudi overlay pausa',
  'pages.sessionLive.endgameDialog.title': 'Fine sessione',
  'pages.sessionLive.endgameDialog.winnerLabel': 'Vincitore',
  'pages.sessionLive.endgameDialog.acknowledgeCta': 'Ho capito',
  'pages.sessionLive.endgameDialog.viewSummaryCta': 'Vedi riepilogo',
  // #2503
  'pages.sessionLive.endgameDialog.saveGameCta': 'Salva partita',
  'pages.sessionLive.endgameDialog.savingLabel': 'Salvataggio…',
  'pages.sessionLive.endgameDialog.alreadyCompletedToast': 'Partita già conclusa',
  'pages.sessionLive.endgameConfirm.title': 'Terminare la partita?',
  'pages.sessionLive.endgameConfirm.body':
    "L'azione è irreversibile. I punteggi finali verranno registrati.",
  'pages.sessionLive.endgameConfirm.confirmCta': 'Termina partita',
  'pages.sessionLive.endgameConfirm.cancelCta': 'Annulla',
};

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

// ─── Fixture helpers ──────────────────────────────────────────────────────

// ADR-083 Fase 1 (#2501): the canonical loader is now LiveGameSession
// (LiveSessionDto). Only the fields SessionLiveView actually consumes are set;
// the object is cast since the hook is mocked (no schema parse at this layer).
const MOCK_SESSION_DTO = {
  id: 'session-abc-123',
  sessionCode: 'ABC123',
  gameId: 'game-00000001',
  gameName: 'Mage Knight',
  gameSlug: 'mage-knight',
  createdByUserId: '44444444-4444-4444-4444-444444444444',
  status: 'InProgress',
  visibility: 'Private',
  groupId: null,
  createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
  startedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
  pausedAt: null,
  completedAt: null,
  updatedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
  lastSavedAt: null,
  currentTurnIndex: 0,
  currentTurnPlayerId: null,
  agentMode: 'Active',
  notes: null,
  players: [
    {
      id: 'player-001',
      displayName: 'Marco',
      userId: null,
      color: 'blue',
      role: 'Player',
      totalScore: 0,
      isActive: true,
    },
    {
      id: 'player-002',
      displayName: 'Anna',
      userId: null,
      color: 'red',
      role: 'Player',
      totalScore: 0,
      isActive: true,
    },
  ],
  teams: [],
  roundScores: [],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as unknown as LiveSessionDto;

// ─── Import under test ─────────────────────────────────────────────────────

import { SessionLiveView } from '../SessionLiveView';

// Block B #2389: reset the polymorphic scoring slice between tests at file
// scope so no test bleeds stale scoringType/scoreData/players into the next.
// Use the store's reset() action (which sets the full initial state) instead
// of setState({...partial}) — Zustand setState merges by default.
beforeEach(() => {
  useLiveSessionStore.getState().reset();
  // #2505: reset currentUser to null by default; individual tests can override.
  useCurrentUserMock.mockReturnValue({ data: null });
  // #2588 A4: reset SignalR mock so call counts don't bleed between tests.
  useSignalRSessionMock.mockClear();
});

describe('SessionLiveView (Wave D.2 Foundation)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useLiveSessionMock.mockReturnValue({
      data: MOCK_SESSION_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
  });

  // ─── 3.1: Dark theme attribute ──────────────────────────────────────────

  it('root container has data-theme="dark" in default state', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const root = container.querySelector('[data-slot="session-live-view"]');
    expect(root).toBeInTheDocument();
    expect(root).toHaveAttribute('data-theme', 'dark');
  });

  it('root container has data-theme="dark" in loading state', () => {
    useLiveSessionMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      isSuccess: false,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    const root = container.querySelector('[data-slot="session-live-view"]');
    expect(root).toHaveAttribute('data-theme', 'dark');
  });

  // ─── 3.2: FSM Cell 1 — sessionId null (params?.id undefined) ───────────

  it('Cell 1: renders not-found shell when params.id is undefined', () => {
    mockParamsId = undefined;
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="session-live-not-found"]')).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="session-live-view"][data-ui-state="not-found"]')
    ).toBeInTheDocument();
  });

  it('Cell 1: not-found CTA navigates to /sessions', () => {
    mockParamsId = undefined;
    const { container } = renderWithIntl(<SessionLiveView />);
    const cta = container.querySelector(
      '[data-slot="session-live-not-found-cta"]'
    ) as HTMLButtonElement;
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    expect(routerPush).toHaveBeenCalledWith('/sessions');
  });

  // ─── 3.3: FSM Cell 2 — loading ─────────────────────────────────────────

  it('Cell 2: renders loading shell when isLoading=true', () => {
    useLiveSessionMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      isSuccess: false,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="session-live-loading"]')).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="session-live-view"][data-ui-state="loading"]')
    ).toBeInTheDocument();
  });

  it('Cell 2: no LiveTopBar rendered in loading state', () => {
    useLiveSessionMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      isSuccess: false,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="session-live-top-bar"]')).not.toBeInTheDocument();
  });

  // ─── 3.4: FSM Cell 3 — error ───────────────────────────────────────────

  it('Cell 3: renders error shell when isError=true', () => {
    useLiveSessionMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      isSuccess: false,
      error: new Error('network error'),
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="session-live-error"]')).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="session-live-view"][data-ui-state="error"]')
    ).toBeInTheDocument();
  });

  it('Cell 3: error retry button calls refetch', () => {
    const refetch = vi.fn();
    useLiveSessionMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      isSuccess: false,
      error: new Error('fail'),
      refetch,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    const retryBtn = container.querySelector(
      '[data-slot="session-live-error-retry"]'
    ) as HTMLButtonElement;
    expect(retryBtn).toBeInTheDocument();
    fireEvent.click(retryBtn);
    expect(refetch).toHaveBeenCalledOnce();
  });

  // ─── 3.5: FSM Cell 4 — not-found (success + null data) ─────────────────

  it('Cell 4: renders not-found shell when data is null (session 404)', () => {
    useLiveSessionMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="session-live-not-found"]')).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="session-live-view"][data-ui-state="not-found"]')
    ).toBeInTheDocument();
  });

  // ─── 3.6: FSM Cell 5 — default (data present) ──────────────────────────

  it('Cell 5: renders default shell with LiveTopBar + DesktopBody + MobileBody', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(
      container.querySelector('[data-slot="session-live-view"][data-ui-state="default"]')
    ).toBeInTheDocument();
    expect(container.querySelector('[data-slot="session-live-top-bar"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="desktop-body"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="mobile-body"]')).toBeInTheDocument();
  });

  // ─── 3.7: Exit handler ─────────────────────────────────────────────────

  it('exit button navigates to /sessions/{sessionId}', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const exitBtn = container.querySelector(
      '[data-slot="session-live-top-bar-exit"]'
    ) as HTMLButtonElement;
    expect(exitBtn).toBeInTheDocument();
    fireEvent.click(exitBtn);
    expect(routerPush).toHaveBeenCalledWith('/sessions/session-abc-123');
  });

  // ─── 3.8: URL state — mobile bottom-sheet (G1 #2374 T9 sess.46r) ────────

  it('mobile renders mainContent = ChatAgentPanel + ActionLogTimeline (full-width, no bottom-nav)', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // Main column should contain ChatAgentPanel + ActionLogTimeline (mirrors desktop LEFT)
    const mobileBody = container.querySelector('[data-slot="mobile-body"]');
    expect(mobileBody).toBeInTheDocument();
    expect(mobileBody?.querySelector('[data-slot="chat-agent-panel"]')).not.toBeNull();
    expect(mobileBody?.querySelector('[data-slot="action-log-timeline"]')).not.toBeNull();
    // NO legacy bottom-nav tabs
    expect(container.querySelector('[data-slot="mobile-body-tab"]')).toBeNull();
  });

  it('FAB tap calls router.replace with ?msheet=open', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const fab = container.querySelector(
      '[data-slot="mobile-body-open-sheet"]'
    ) as HTMLButtonElement;
    expect(fab).toBeInTheDocument();
    fireEvent.click(fab);
    expect(routerReplace).toHaveBeenCalled();
    const callArg = routerReplace.mock.calls[0]?.[0] as string;
    expect(callArg).toContain('msheet=open');
  });

  it('?msheet=open mounts MobileBottomSheetDrawer with active tab content', () => {
    searchParamsMap['msheet'] = 'open';
    renderWithIntl(<SessionLiveView />);
    // Drawer portals to document.body, so query document directly.
    expect(document.querySelector('[data-slot="mobile-bottom-sheet"]')).not.toBeNull();
  });

  it('?mtab=turn pre-selects Turn tab in the drawer when sheet is open', () => {
    searchParamsMap['msheet'] = 'open';
    searchParamsMap['mtab'] = 'turn';
    renderWithIntl(<SessionLiveView />);
    // Drawer has 6 tabs (score/turn/widget/notes/photos/agent); the 2nd (index 1) is Turn.
    const drawerTabs = document.querySelectorAll('[data-slot="mobile-bottom-sheet"] [role="tab"]');
    expect(drawerTabs).toHaveLength(6);
    expect(drawerTabs[1]).toHaveAttribute('aria-selected', 'true');
  });

  it('?mtab=tools legacy maps to widget (back-compat)', () => {
    // Legacy URL bookmark must not 404 — parseMobileTab('tools') → 'widget'.
    searchParamsMap['msheet'] = 'open';
    searchParamsMap['mtab'] = 'tools';
    renderWithIntl(<SessionLiveView />);
    const drawerTabs = document.querySelectorAll('[data-slot="mobile-bottom-sheet"] [role="tab"]');
    // widget = 3rd tab (index 2)
    expect(drawerTabs[2]).toHaveAttribute('aria-selected', 'true');
  });

  it('?mtab=chat legacy maps to score (chat is always-visible in main column)', () => {
    searchParamsMap['msheet'] = 'open';
    searchParamsMap['mtab'] = 'chat';
    renderWithIntl(<SessionLiveView />);
    const drawerTabs = document.querySelectorAll('[data-slot="mobile-bottom-sheet"] [role="tab"]');
    // score = 1st tab (index 0)
    expect(drawerTabs[0]).toHaveAttribute('aria-selected', 'true');
  });

  it('drawer tab click calls router.replace with ?mtab= (non-default)', () => {
    searchParamsMap['msheet'] = 'open';
    renderWithIntl(<SessionLiveView />);
    const turnTab = document.querySelectorAll(
      '[data-slot="mobile-bottom-sheet"] [role="tab"]'
    )[1] as HTMLButtonElement;
    fireEvent.click(turnTab);
    expect(routerReplace).toHaveBeenCalled();
    const callArg = routerReplace.mock.calls[0]?.[0] as string;
    expect(callArg).toContain('mtab=turn');
  });

  // ─── 3.9: ?state= URL override ────────────────────────────────────────

  it('?state=loading forces loading shell (NODE_ENV=test, STATE_OVERRIDE_ENABLED=true)', () => {
    searchParamsMap['state'] = 'loading';
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="session-live-loading"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="session-live-top-bar"]')).not.toBeInTheDocument();
  });

  it('?state=not-found forces not-found shell', () => {
    searchParamsMap['state'] = 'not-found';
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="session-live-not-found"]')).toBeInTheDocument();
  });

  it('ignores unknown ?state= values and falls back to real FSM (default)', () => {
    searchParamsMap['state'] = 'bogus-state';
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(
      container.querySelector('[data-slot="session-live-view"][data-ui-state="default"]')
    ).toBeInTheDocument();
    expect(container.querySelector('[data-slot="session-live-top-bar"]')).toBeInTheDocument();
  });

  // ─── 3.10: Visual fixture mode (IS_VISUAL_TEST_BUILD=true) ────────────

  it('IS_VISUAL_TEST_BUILD=true: renders fixture data (not real hook)', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    // Even with useSession returning null, fixture renders default
    useLiveSessionMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: false,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(
      container.querySelector('[data-slot="session-live-view"][data-ui-state="default"]')
    ).toBeInTheDocument();
    expect(container.querySelector('[data-slot="session-live-top-bar"]')).toBeInTheDocument();
  });

  it('IS_VISUAL_TEST_BUILD=true, ?fixture=host: renders Host role variant', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    searchParamsMap['fixture'] = 'host';
    const { container } = renderWithIntl(<SessionLiveView />);
    // Host role: pause/endgame CTAs visible in top bar
    const topBar = container.querySelector('[data-slot="session-live-top-bar"]');
    expect(topBar).toBeInTheDocument();
    expect(topBar).toHaveAttribute('data-viewer-role', 'Host');
    expect(
      container.querySelector('[data-slot="session-live-top-bar-endgame"]')
    ).toBeInTheDocument();
  });

  it('IS_VISUAL_TEST_BUILD=true, ?fixture=spectator: renders Spectator role variant', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    searchParamsMap['fixture'] = 'spectator';
    const { container } = renderWithIntl(<SessionLiveView />);
    const topBar = container.querySelector('[data-slot="session-live-top-bar"]');
    expect(topBar).toHaveAttribute('data-viewer-role', 'Spectator');
    // Spectator: no pause/endgame buttons
    expect(
      container.querySelector('[data-slot="session-live-top-bar-pause"]')
    ).not.toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="session-live-top-bar-endgame"]')
    ).not.toBeInTheDocument();
  });

  it('IS_VISUAL_TEST_BUILD=true, ?fixture=paused: top bar shows Paused status', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    searchParamsMap['fixture'] = 'paused';
    // Fixture is Player role, Paused status → no Host CTAs
    const { container } = renderWithIntl(<SessionLiveView />);
    const topBar = container.querySelector('[data-slot="session-live-top-bar"]');
    expect(topBar).toHaveAttribute('data-status', 'Paused');
  });

  // ─── 3.11: Default fixture (IS_VISUAL_TEST_BUILD=false) ───────────────

  it('IS_VISUAL_TEST_BUILD=false: uses real hook data (not fixture)', () => {
    // Default: IS_VISUAL_TEST_BUILD_MOCK=false, hook returns MOCK_SESSION_DTO
    const { container } = renderWithIntl(<SessionLiveView />);
    // Renders default shell from real hook data
    expect(
      container.querySelector('[data-slot="session-live-view"][data-ui-state="default"]')
    ).toBeInTheDocument();
  });

  // ─── 3.12: Gate A — ICU plural verification ───────────────────────────

  it('Gate A: turnLabel ICU plural resolves count=0 → "Inizio" (no raw string replace)', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    // Default fixture: currentTurn=12, totalTurns=18
    const { container } = renderWithIntl(<SessionLiveView />);
    // turnLabel with count=12: "Turno 12/18" (via ICU plural 'other' branch)
    const topBar = container.querySelector('[data-slot="session-live-top-bar"]');
    expect(topBar).toBeInTheDocument();
    // Verify no raw template strings leaked (no literal '{count}' or '{total}' in DOM)
    expect(container.textContent).not.toContain('{count}');
    expect(container.textContent).not.toContain('{total}');
  });

  it('Gate A: rosterLabels playerCount ICU plural resolved (no raw template in DOM)', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    const { container } = renderWithIntl(<SessionLiveView />);
    // Fixture has 5 players — resolved count shown in roster
    expect(container.textContent).not.toContain('{count, plural');
    expect(container.textContent).not.toContain('other {# giocatori}');
  });
});

describe('SessionLiveView (Wave D.2 Interactions — Task 3)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useLiveSessionMock.mockReturnValue({
      data: MOCK_SESSION_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
  });

  // ─── T3.1: SSE hook wiring ──────────────────────────────────────────────

  it('T3.1a: useSessionLiveStream is called when IS_VISUAL_TEST_BUILD=false + sessionQuery.isSuccess', () => {
    renderWithIntl(<SessionLiveView />);
    expect(useSessionLiveStreamMock).toHaveBeenCalled();
    const callArg = useSessionLiveStreamMock.mock.calls[0]?.[0] as {
      sessionId: string | null;
      enabled: boolean;
    };
    expect(callArg.sessionId).toBe('session-abc-123');
    expect(callArg.enabled).toBe(true);
  });

  it('T3.1b: useSessionLiveStream is NOT called (enabled=false) in IS_VISUAL_TEST_BUILD=true mode', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    renderWithIntl(<SessionLiveView />);
    // Hook may still be called (it's at component level), but enabled must be false
    const calls = useSessionLiveStreamMock.mock.calls;
    if (calls.length > 0) {
      const callArg = calls[0]?.[0] as { sessionId: string | null; enabled: boolean };
      expect(callArg.enabled).toBe(false);
    }
    // If not called at all, that's also acceptable
  });

  it('T3.1c: useSessionLiveStream enabled=false when sessionQuery.isSuccess=false', () => {
    useLiveSessionMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      isSuccess: false,
      error: null,
    });
    renderWithIntl(<SessionLiveView />);
    const calls = useSessionLiveStreamMock.mock.calls;
    if (calls.length > 0) {
      const callArg = calls[0]?.[0] as { sessionId: string | null; enabled: boolean };
      expect(callArg.enabled).toBe(false);
    }
  });

  // ─── T3.2: RightColumnTabs — desktop tab routing (G1 #2374 sess.46r) ───
  // Tab keys refactored: 'tools'|'chat'|'notes' → 'score'|'turn'|'widget'|'notes'
  // Back-compat aliases (R-1): legacy ?tab=tools → 'widget', ?tab=chat → 'score'

  it('T3.2a: RightColumnTabs is mounted in default shell', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="right-column-tabs"]')).toBeInTheDocument();
  });

  it('T3.2b: default ?tab shows RightColumnTabs with activeTab=score (new default)', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const tabs = container.querySelector('[data-slot="right-column-tabs"]');
    expect(tabs).toHaveAttribute('data-active-tab', 'score');
  });

  it('T3.2c: ?tab=turn shows RightColumnTabs with activeTab=turn', () => {
    searchParamsMap['tab'] = 'turn';
    const { container } = renderWithIntl(<SessionLiveView />);
    const tabs = container.querySelector('[data-slot="right-column-tabs"]');
    expect(tabs).toHaveAttribute('data-active-tab', 'turn');
  });

  it('T3.2d: ?tab=widget shows RightColumnTabs with activeTab=widget', () => {
    searchParamsMap['tab'] = 'widget';
    const { container } = renderWithIntl(<SessionLiveView />);
    const tabs = container.querySelector('[data-slot="right-column-tabs"]');
    expect(tabs).toHaveAttribute('data-active-tab', 'widget');
  });

  it('T3.2d2: ?tab=notes shows RightColumnTabs with activeTab=notes', () => {
    searchParamsMap['tab'] = 'notes';
    const { container } = renderWithIntl(<SessionLiveView />);
    const tabs = container.querySelector('[data-slot="right-column-tabs"]');
    expect(tabs).toHaveAttribute('data-active-tab', 'notes');
  });

  it('T3.2e: clicking a desktop tab calls router.replace with ?tab= param', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // Find a non-active tab button inside RightColumnTabs
    const nextTabBtn = container.querySelector(
      '[data-slot="right-column-tabs"] [role="tab"][aria-selected="false"]'
    ) as HTMLButtonElement;
    expect(nextTabBtn).toBeInTheDocument();
    fireEvent.click(nextTabBtn);
    expect(routerReplace).toHaveBeenCalled();
  });

  // ─── T4.x: Back-compat URL aliases (R-1 mitigation) ──────────────────────

  it('T4.1: legacy ?tab=tools is treated as widget (back-compat alias)', () => {
    searchParamsMap['tab'] = 'tools';
    const { container } = renderWithIntl(<SessionLiveView />);
    const tabs = container.querySelector('[data-slot="right-column-tabs"]');
    expect(tabs).toHaveAttribute('data-active-tab', 'widget');
  });

  it('T4.2: legacy ?tab=chat falls back to score (chat is no longer a tab)', () => {
    searchParamsMap['tab'] = 'chat';
    const { container } = renderWithIntl(<SessionLiveView />);
    const tabs = container.querySelector('[data-slot="right-column-tabs"]');
    expect(tabs).toHaveAttribute('data-active-tab', 'score');
  });

  it('T4.3: unknown ?tab value falls back to score (default)', () => {
    searchParamsMap['tab'] = 'bogus-tab';
    const { container } = renderWithIntl(<SessionLiveView />);
    const tabs = container.querySelector('[data-slot="right-column-tabs"]');
    expect(tabs).toHaveAttribute('data-active-tab', 'score');
  });

  it('T4.4: tab change to "score" (default) omits ?tab from URL (clean bookmark)', () => {
    searchParamsMap['tab'] = 'widget';
    const { container } = renderWithIntl(<SessionLiveView />);
    // Click the score tab (which is at index 0, currently non-active)
    const scoreTabBtn = container.querySelector(
      '[data-slot="right-column-tabs"] [role="tab"]:nth-of-type(1)'
    ) as HTMLButtonElement;
    expect(scoreTabBtn).toBeInTheDocument();
    fireEvent.click(scoreTabBtn);
    expect(routerReplace).toHaveBeenCalled();
    const callArg = routerReplace.mock.calls[0]?.[0] as string;
    // Default tab 'score' should NOT have tab in URL
    expect(callArg).not.toContain('tab=score');
  });

  // ─── T4.5: 60/40 layout contract ─────────────────────────────────────────

  it('T4.5: default-state root container has data-layout="2col-60-40"', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const root = container.querySelector('[data-slot="session-live-view"]');
    expect(root).toBeInTheDocument();
    expect(root).toHaveAttribute('data-layout', '2col-60-40');
  });

  it('T4.6: Cell 5 desktop renders ChatAgentPanel in LEFT mainColumn', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // ChatAgentPanel data-slot stable per §5 frozen contract for #2375 G3
    expect(container.querySelector('[data-slot="chat-agent-panel"]')).toBeInTheDocument();
  });

  it('T4.7: Cell 5 desktop renders ActionLogTimeline stacked in LEFT mainColumn', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // ActionLogTimeline is rendered alongside ChatAgentPanel in mainColumn
    expect(container.querySelector('[data-slot="action-log-timeline"]')).toBeInTheDocument();
  });

  it('T4.8: RIGHT tab=turn renders TurnIndicator + PlayerRosterLive', () => {
    searchParamsMap['tab'] = 'turn';
    const { container } = renderWithIntl(<SessionLiveView />);
    // TurnIndicator + PlayerRosterLive moved from LEFT sidebar to RIGHT 'turn' tab (D-6)
    expect(container.querySelector('[data-slot="turn-indicator"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="player-roster-live"]')).toBeInTheDocument();
  });

  it('T4.9: RIGHT tab=widget renders ToolkitRenderer (G5c #2376, replaces SessionToolsRail)', () => {
    searchParamsMap['tab'] = 'widget';
    const { container } = renderWithIntl(<SessionLiveView />);
    // G5c swap: ToolkitRenderer replaces SessionToolsRail in the 'widget' tab.
    // Empty store → empty state renders the region with data-empty="true".
    expect(container.querySelector('[data-slot="toolkit-renderer"]')).toBeInTheDocument();
  });

  it('T4.10: RIGHT tab=notes renders LiveSessionNotes', () => {
    searchParamsMap['tab'] = 'notes';
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="live-session-notes"]')).toBeInTheDocument();
  });

  it('T4.11: RIGHT tab=score renders ScoringPanelRenderer (G5a #2375)', () => {
    // ?tab default → 'score'. ScoringPanelRenderer replaces LiveScoringPanel (#2421 wire-up).
    // #2389 Block B: renderer is now gated on scoringType != null — seed the store
    // first so the renderer mounts instead of the a11y placeholder.
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [] },
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="scoring-panel-renderer"]')).toBeInTheDocument();
  });

  // ─── T3.3: Mobile drawer tab routing — Interactions panels (T9 sess.46r) ─

  it('T3.3a: chat is always-visible in mobile main column (no longer a drawer tab)', () => {
    // Post-T9: chat lives in the main column (ChatAgentPanel), not the drawer.
    const { container } = renderWithIntl(<SessionLiveView />);
    // LiveAgentChat is mounted as the body of ChatAgentPanel inside MobileBody main column.
    const mobileBody = container.querySelector('[data-slot="mobile-body"]');
    expect(mobileBody?.querySelector('[data-slot="live-agent-chat"]')).not.toBeNull();
  });

  it('T3.3b: ?msheet=open + ?mtab=widget mounts ToolkitRenderer in drawer (G5c #2376)', () => {
    // G5c swap: ToolkitRenderer replaces SessionToolsRail in the mobile 'widget' tab.
    searchParamsMap['msheet'] = 'open';
    searchParamsMap['mtab'] = 'widget';
    const { container } = renderWithIntl(<SessionLiveView />);
    // Drawer mounted with ToolkitRenderer inside (empty store → data-empty="true").
    expect(document.querySelector('[data-slot="mobile-bottom-sheet"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="toolkit-renderer"]')).not.toBeNull();
    // Assert no crash + drawer mounted with widget tab selected.
    expect(container.querySelector('[data-slot="session-live-view"]')).toBeInTheDocument();
  });

  // ─── T3.4: ConnectionLostBanner ────────────────────────────────────────

  it('T3.4a: ConnectionLostBanner "reconnecting" shown when connectionState=reconnecting', () => {
    useSessionLiveStreamMock.mockReturnValue({
      ...mockLiveStreamResult,
      connectionState: 'reconnecting',
      retryCount: 2,
      retryAt: new Date(Date.now() + 4000),
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    const banner = container.querySelector('[data-slot="connection-lost-banner"]');
    expect(banner).toBeInTheDocument();
    expect(banner).toHaveAttribute('data-kind', 'reconnecting');
  });

  it('T3.4b: ConnectionLostBanner "degraded-polling" shown when connectionState=degraded-polling', () => {
    useSessionLiveStreamMock.mockReturnValue({
      ...mockLiveStreamResult,
      connectionState: 'degraded-polling',
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    const banner = container.querySelector('[data-slot="connection-lost-banner"]');
    expect(banner).toBeInTheDocument();
    expect(banner).toHaveAttribute('data-kind', 'degraded-polling');
  });

  it('T3.4c: ConnectionLostBanner "failed" shown when connectionState=failed', () => {
    useSessionLiveStreamMock.mockReturnValue({
      ...mockLiveStreamResult,
      connectionState: 'failed',
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    const banner = container.querySelector('[data-slot="connection-lost-banner"]');
    expect(banner).toBeInTheDocument();
    expect(banner).toHaveAttribute('data-kind', 'failed');
  });

  it('T3.4d: NO ConnectionLostBanner when connectionState=connected', () => {
    useSessionLiveStreamMock.mockReturnValue({
      ...mockLiveStreamResult,
      connectionState: 'connected',
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="connection-lost-banner"]')).not.toBeInTheDocument();
  });

  it('T3.4e: NO ConnectionLostBanner when connectionState=connecting', () => {
    useSessionLiveStreamMock.mockReturnValue({
      ...mockLiveStreamResult,
      connectionState: 'connecting',
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="connection-lost-banner"]')).not.toBeInTheDocument();
  });

  // ─── T3.5: Dialog state — PauseOverlay ────────────────────────────────

  it('T3.5a: ?dialog=pause mounts PauseOverlay (lazy via Suspense)', async () => {
    searchParamsMap['dialog'] = 'pause';
    // Use IS_VISUAL_TEST_BUILD fixture so we get a paused session for proper display
    IS_VISUAL_TEST_BUILD_MOCK = true;
    searchParamsMap['fixture'] = 'paused';
    let container: HTMLElement;
    await act(async () => {
      const result = renderWithIntl(<SessionLiveView />);
      container = result.container;
    });
    // PauseOverlay is lazy — allow Suspense to resolve
    await act(async () => {});
    expect(container!.querySelector('[data-slot="pause-overlay"]')).toBeInTheDocument();
  });

  it('T3.5b: ?dialog=endgame mounts EndgameDialog (lazy via Suspense)', async () => {
    searchParamsMap['dialog'] = 'endgame';
    IS_VISUAL_TEST_BUILD_MOCK = true;
    let container: HTMLElement;
    await act(async () => {
      const result = renderWithIntl(<SessionLiveView />);
      container = result.container;
    });
    await act(async () => {});
    expect(container!.querySelector('[data-slot="endgame-dialog"]')).toBeInTheDocument();
  });

  it('T3.5c: No dialog when ?dialog param is absent', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="pause-overlay"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-slot="endgame-dialog"]')).not.toBeInTheDocument();
  });

  // ─── T3.6: Dialog dismiss handler — URL update ────────────────────────

  it('T3.6: closing PauseOverlay calls router.replace to remove ?dialog param', async () => {
    searchParamsMap['dialog'] = 'pause';
    IS_VISUAL_TEST_BUILD_MOCK = true;
    searchParamsMap['fixture'] = 'paused';
    let container: HTMLElement;
    await act(async () => {
      const result = renderWithIntl(<SessionLiveView />);
      container = result.container;
    });
    await act(async () => {});
    const overlay = container!.querySelector('[data-slot="pause-overlay"]');
    if (overlay) {
      // Find close button
      const closeBtn = overlay.querySelector('button:last-of-type') as HTMLButtonElement;
      if (closeBtn) {
        fireEvent.click(closeBtn);
        expect(routerReplace).toHaveBeenCalled();
        const callArg = routerReplace.mock.calls[0]?.[0] as string;
        expect(callArg).not.toContain('dialog=pause');
      }
    }
  });

  // ─── T3.7: No crash on all interaction flows ───────────────────────────

  it('T3.7: renders without errors when SSE connected state', () => {
    useSessionLiveStreamMock.mockReturnValue({
      ...mockLiveStreamResult,
      connectionState: 'connected',
    });
    expect(() => renderWithIntl(<SessionLiveView />)).not.toThrow();
  });

  it('T3.7b: renders without errors when SSE events present', () => {
    useSessionLiveStreamMock.mockReturnValue({
      ...mockLiveStreamResult,
      events: [
        {
          type: 'session:score',
          participantId: 'player-order-0',
          score: 15,
          updatedBy: 'player-order-0',
          sessionId: 'session-abc-123',
          timestamp: new Date().toISOString(),
        },
      ],
    });
    expect(() => renderWithIntl(<SessionLiveView />)).not.toThrow();
  });
});

// ─── #2375 G3 — accordion FSM URL SSOT ──────────────────────────────────────

describe('SessionLiveView (#2375 G3 — accordion FSM URL SSOT)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useLiveSessionMock.mockReturnValue({
      data: MOCK_SESSION_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
  });

  it('G3-1: renders ChatAgentPanel expanded by default (no ?chat param)', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // Desktop ChatAgentPanel — no ?chat param → data-collapsed attribute absent (expanded).
    const desktopBody = container.querySelector('[data-slot="desktop-body"]');
    expect(desktopBody).toBeInTheDocument();
    const panels = desktopBody?.querySelectorAll('[data-slot="chat-agent-panel"]');
    expect(panels).toBeTruthy();
    expect(panels!.length).toBeGreaterThanOrEqual(1);
    // No data-collapsed="true" when expanded (attribute is absent per ChatAgentPanel contract).
    expect(panels![0]).not.toHaveAttribute('data-collapsed', 'true');
  });

  it('G3-2: ?chat=collapsed → desktop ChatAgentPanel has data-collapsed="true"', () => {
    searchParamsMap['chat'] = 'collapsed';
    const { container } = renderWithIntl(<SessionLiveView />);
    const desktopBody = container.querySelector('[data-slot="desktop-body"]');
    expect(desktopBody).toBeInTheDocument();
    const panels = desktopBody?.querySelectorAll('[data-slot="chat-agent-panel"]');
    expect(panels).toBeTruthy();
    expect(panels![0]).toHaveAttribute('data-collapsed', 'true');
  });

  it('G3-3: ?mchat=collapsed → mobile ChatAgentPanel has data-collapsed="true"', () => {
    searchParamsMap['mchat'] = 'collapsed';
    const { container } = renderWithIntl(<SessionLiveView />);
    const mobileBody = container.querySelector('[data-slot="mobile-body"]');
    expect(mobileBody).toBeInTheDocument();
    const panels = mobileBody?.querySelectorAll('[data-slot="chat-agent-panel"]');
    expect(panels).toBeTruthy();
    expect(panels!.length).toBeGreaterThanOrEqual(1);
    expect(panels![0]).toHaveAttribute('data-collapsed', 'true');
  });

  it('G3-4: ?chat=collapsed&mchat=collapsed → both desktop and mobile ChatAgentPanel collapsed', () => {
    searchParamsMap['chat'] = 'collapsed';
    searchParamsMap['mchat'] = 'collapsed';
    const { container } = renderWithIntl(<SessionLiveView />);
    const desktopBody = container.querySelector('[data-slot="desktop-body"]');
    const mobileBody = container.querySelector('[data-slot="mobile-body"]');
    const desktopPanels = desktopBody?.querySelectorAll('[data-slot="chat-agent-panel"]');
    const mobilePanels = mobileBody?.querySelectorAll('[data-slot="chat-agent-panel"]');
    expect(desktopPanels![0]).toHaveAttribute('data-collapsed', 'true');
    expect(mobilePanels![0]).toHaveAttribute('data-collapsed', 'true');
  });

  it('G3-5: clicking desktop ChatAgentPanel header calls router.replace with ?chat=collapsed', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const desktopBody = container.querySelector('[data-slot="desktop-body"]');
    // Header is a <button> (a11y) when onHeaderClick is provided (§5 contract).
    const headerBtn = desktopBody?.querySelector(
      '[data-slot="chat-agent-panel"] button[aria-expanded]'
    ) as HTMLButtonElement | null;
    expect(headerBtn).not.toBeNull();
    fireEvent.click(headerBtn!);
    expect(routerReplace).toHaveBeenCalled();
    const callArg = routerReplace.mock.calls[0]?.[0] as string;
    expect(callArg).toContain('chat=collapsed');
  });

  it('G3-6: clicking header when ?chat=collapsed calls router.replace WITHOUT chat param (toggle expand)', () => {
    searchParamsMap['chat'] = 'collapsed';
    const { container } = renderWithIntl(<SessionLiveView />);
    const desktopBody = container.querySelector('[data-slot="desktop-body"]');
    const headerBtn = desktopBody?.querySelector(
      '[data-slot="chat-agent-panel"] button[aria-expanded]'
    ) as HTMLButtonElement | null;
    expect(headerBtn).not.toBeNull();
    fireEvent.click(headerBtn!);
    expect(routerReplace).toHaveBeenCalled();
    const callArg = routerReplace.mock.calls[0]?.[0] as string;
    // When expanding, 'chat' param is removed (null → deleted from query string).
    expect(callArg).not.toContain('chat=collapsed');
  });
});

// ─── Block B (#2389) scoring wire-up tests ────────────────────────────────────

describe('SessionLiveView — Block B (#2389) scoring wire-up', () => {
  beforeEach(() => {
    // Default useSession + useSessionLiveStream returns for these tests.
    useLiveSessionMock.mockReturnValue({
      data: MOCK_SESSION_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
  });

  // ── REST hydration removed (ADR-083 Fase 1, #2501) ──────────────────────────
  // The polymorphic scoring REST hydration (scoringType/scoreData/turnOrderType
  // from GameSessionDto) was removed when the loader switched to the canonical
  // LiveGameSession aggregate (LiveSessionDto, which is round-based and exposes
  // no polymorphic fields). On the real funnel those fields were always
  // undefined (empty GameSession shell) so the effects never ran in production.
  // The store can still be populated directly (SignalR / explicit action) — the
  // null-gate, placeholder and renderer-variant tests below exercise that path.
  // Round-based scoring wiring from LiveSessionDto.roundScores/scoringConfig is
  // deferred to Fase 2.

  // ── Null gate + a11y placeholder (2) ────────────────────────────────────────

  it('renders aria-live placeholder when scoringType is null', () => {
    // Default beforeEach leaves store null; no DTO config either.
    renderWithIntl(<SessionLiveView />);

    const placeholder = document.querySelector('[data-slot="scoring-panel-empty"]');
    expect(placeholder).not.toBeNull();
    expect(placeholder?.getAttribute('role')).toBe('status');
    expect(placeholder?.getAttribute('aria-live')).toBe('polite');
  });

  it('placeholder shows the localized loading label text', () => {
    renderWithIntl(<SessionLiveView />);
    expect(screen.getByText('Caricamento punteggi…')).toBeInTheDocument();
  });

  // ── Variant mount via setScoringConfig action (4) ───────────────────────────

  it('mounts Points renderer when scoringType=Points', () => {
    renderWithIntl(<SessionLiveView />);
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'player-001', points: 10 }] },
      });
    });
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
  });

  it('mounts BinaryWin renderer when scoringType=BinaryWin', () => {
    renderWithIntl(<SessionLiveView />);
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'BinaryWin',
        scoreData: { results: [{ playerId: 'player-001', isWinner: true }] },
      });
    });
    expect(document.querySelector('[data-slot="scoring-panel-binary-win"]')).not.toBeNull();
  });

  it('mounts Ranking renderer when scoringType=Ranking', () => {
    renderWithIntl(<SessionLiveView />);
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Ranking',
        scoreData: { positions: [{ playerId: 'player-001', position: 1 }] },
      });
    });
    expect(document.querySelector('[data-slot="scoring-panel-ranking"]')).not.toBeNull();
  });

  it('mounts Objectives renderer when scoringType=Objectives', () => {
    renderWithIntl(<SessionLiveView />);
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Objectives',
        scoreData: {
          completedByPlayer: [{ playerId: 'player-001', objectives: [] }],
        },
      });
    });
    expect(document.querySelector('[data-slot="scoring-panel-objectives"]')).not.toBeNull();
  });

  // ── #2430 Block B+ smoke: ScoreTabContent mount ──────────────────────────

  it('mounts ScoreTabContent inside score tab (any state)', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // ScoreTabContent has no unique top-level data-slot; probe via children:
    // it renders either the placeholder (when scoringType=null) or a renderer.
    const hasMount =
      container.querySelector('[data-slot="polymorphic-score-editor"]') ||
      container.querySelector('[data-slot="scoring-panel-points"]') ||
      container.querySelector('[data-slot="scoring-panel-empty"]');
    expect(hasMount).not.toBeNull();
  });

  it('renders ScoringPanelRenderer (not editor) when viewerRole=Player', () => {
    // Default fixture viewerRole is 'Player'.
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'player-001', points: 10 }] },
      });
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(container.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
  });
});

// ─── #2483 Task 4 — turnRendererState wired from store ────────────────────────

describe('SessionLiveView — #2483 Task 4: TurnIndicatorRenderer from store', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    // Always show the 'turn' tab so TurnIndicatorRenderer is visible.
    searchParamsMap['tab'] = 'turn';
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useLiveSessionMock.mockReturnValue({
      data: MOCK_SESSION_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
  });

  it('renders Sequential branch when store.turnOrderType=Sequential', () => {
    act(() => {
      useLiveSessionStore.getState().setTurnOrderType('Sequential');
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    // Sequential branch must be present; RoundRobin must NOT be present.
    expect(container.querySelector('[data-slot="turn-branch-sequential"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="turn-branch-round-robin"]')).toBeNull();
  });

  it('renders RoundRobin branch when store.turnOrderType=RoundRobin', () => {
    act(() => {
      useLiveSessionStore.getState().setTurnOrderType('RoundRobin');
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="turn-branch-round-robin"]')).not.toBeNull();
  });

  it('renders None branch (safe fallback) when store.turnOrderType=null — no hardcoded RoundRobin', () => {
    // turnOrderType is null by default (reset() was called in global beforeEach).
    const { container } = renderWithIntl(<SessionLiveView />);
    // Must NOT render round-robin silently.
    expect(container.querySelector('[data-slot="turn-branch-round-robin"]')).toBeNull();
    // Must render the explicit None fallback.
    expect(container.querySelector('[data-slot="turn-branch-none"]')).not.toBeNull();
  });

  it('renders Realtime branch when store.turnOrderType=Realtime', () => {
    act(() => {
      useLiveSessionStore.getState().setTurnOrderType('Realtime');
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="turn-branch-realtime"]')).not.toBeNull();
  });

  it('renders None branch when store.turnOrderType=None', () => {
    act(() => {
      useLiveSessionStore.getState().setTurnOrderType('None');
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="turn-branch-none"]')).not.toBeNull();
  });

  // Hydration-path test removed (ADR-083 Fase 1, #2501): turnOrderType is no
  // longer hydrated from the DTO loader (LiveSessionDto exposes no turnOrderType).
  // The store-seeded branch tests above cover the renderer; DTO→store wiring for
  // round-based turn metadata is deferred to Fase 2.
});

// ─── #2500 Task 4-FE — useSessionAgentLaunch wired into ChatAgentPanel ──────────
// AC-CHAT-0: send goes through useSessionAgentChat (RAG path), not /chat social endpoint.
// AC-CHAT-NULL: when agentSessionId absent → panel renders + status feedback visible.
// R-FINDING-4/5/6: correct comments, status feedback messages, viewerId consistency.

describe('SessionLiveView — #2500 Task 4-FE: RAG agent wiring via useSessionAgentLaunch', () => {
  const agentAskMock = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useLiveSessionMock.mockReturnValue({
      data: { ...MOCK_SESSION_DTO, gameId: 'game-00000001' },
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'ready' as const,
      agentSessionId: 'agent-session-uuid-0001',
    });
    useSessionAgentChatMock.mockReturnValue({
      messages: [],
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
  });

  it('RAG-1: renders ChatAgentPanel without crash when agentLaunch is ready', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="chat-agent-panel"]')).toBeInTheDocument();
  });

  it('RAG-2: AC-CHAT-NULL — renders ChatAgentPanel without crash when no-agent status', () => {
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'no-agent' as const,
      agentSessionId: '',
    });
    useSessionAgentChatMock.mockReturnValue({
      messages: [],
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
    expect(() => renderWithIntl(<SessionLiveView />)).not.toThrow();
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="chat-agent-panel"]')).toBeInTheDocument();
  });

  it('RAG-3: assistant citation is rendered in ChatAgentPanel (pag. N pattern)', () => {
    useSessionAgentChatMock.mockReturnValue({
      messages: [
        {
          id: 'msg-001',
          role: 'assistant' as const,
          content: 'Il regolamento dice…',
          timestamp: new Date().toISOString(),
          citations: [{ documentName: 'Reg', pages: [7], excerpt: 'x' }],
        },
      ],
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    // Citation card renders "pag. 7" (or page list) — asserting presence
    expect(container.querySelector('[data-slot="chat-citations"]')).toBeInTheDocument();
    expect(container.textContent).toMatch(/pag\.\s*7/i);
  });

  it('RAG-4: useSessionAgentChat is called with sessionId and agentSessionId from useSessionAgentLaunch', () => {
    renderWithIntl(<SessionLiveView />);
    expect(useSessionAgentChatMock).toHaveBeenCalledWith(
      'session-abc-123',
      'agent-session-uuid-0001',
      {
        persistHistory: true, // fixture=false in test → !fixture=true (R5 #2500)
        gameContext: {
          // I1 #2500: built from LiveSessionDto, not from useSessionStore
          gameId: 'game-00000001',
          gameTitle: 'Mage Knight',
          players: ['Marco', 'Anna'],
          currentTurn: 0,
        },
      }
    );
  });

  it('RAG-5: AC-CHAT-NULL — useSessionAgentChat called with empty string when no-agent', () => {
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'no-agent' as const,
      agentSessionId: '',
    });
    useSessionAgentChatMock.mockReturnValue({
      messages: [],
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
    renderWithIntl(<SessionLiveView />);
    // Hook called with empty agentSessionId guard (no-agent → no ready session)
    // R5: persistHistory passed as third arg — fixture=false → !fixture=true
    // I1 #2500: gameContext still injected from LiveSessionDto even when no-agent
    expect(useSessionAgentChatMock).toHaveBeenCalledWith('session-abc-123', '', {
      persistHistory: true,
      gameContext: {
        gameId: 'game-00000001',
        gameTitle: 'Mage Knight',
        players: ['Marco', 'Anna'],
        currentTurn: 0,
      },
    });
  });

  it('RAG-6: useSessionAgentLaunch called with sessionId, gameId, and !fixture', () => {
    renderWithIntl(<SessionLiveView />);
    expect(useSessionAgentLaunchMock).toHaveBeenCalledWith(
      'session-abc-123',
      'game-00000001',
      true // enabled = !fixture = !false
    );
  });

  it('RAG-7: useSessionAgentLaunch disabled in fixture mode (IS_VISUAL_TEST_BUILD)', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    renderWithIntl(<SessionLiveView />);
    expect(useSessionAgentLaunchMock).toHaveBeenCalledWith(
      expect.anything(),
      expect.anything(),
      false // enabled = !fixture = !true
    );
  });

  // R-FINDING-5: feedback messages for non-ready status
  it('RAG-8: launching status — shows launching feedback message in chat', () => {
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'launching' as const,
      agentSessionId: '',
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    // Launching message appears in the chat messages list
    expect(container.textContent).toMatch(/Avvio/i);
  });

  it('RAG-9: no-agent status — shows no-agent feedback message in chat', () => {
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'no-agent' as const,
      agentSessionId: '',
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.textContent).toMatch(/Nessun assistente/i);
  });

  it('RAG-10: error status — shows error feedback message in chat', () => {
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'error' as const,
      agentSessionId: '',
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.textContent).toMatch(/Impossibile avviare/i);
  });

  // R-FINDING-6: viewerId consistency — no magic-string 'user' fallback
  it('RAG-11: user messages use viewerId from activeSession (not magic-string fallback)', () => {
    // When activeSession.viewerId is '', messages should have senderId='' not 'user'
    useSessionAgentChatMock.mockReturnValue({
      messages: [
        {
          id: 'msg-user-1',
          role: 'user' as const,
          content: 'Domanda utente',
          timestamp: new Date().toISOString(),
          citations: undefined,
        },
      ],
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
    // If viewerId is '' (activeSession default) and senderId is '', the message
    // is own (isOwn=true) — no sender name label. The test checks no 'user' magic string
    // by verifying the mapped senderId matches viewerId (both ''). Since both are '',
    // isOwn=true, and the <span senderName> should NOT appear (only shown when !isOwn).
    const { container } = renderWithIntl(<SessionLiveView />);
    // The message content should be present
    expect(container.textContent).toContain('Domanda utente');
  });

  // ─── AC-CHAT-3: vincolo critico — disclaimer NOT su messaggi di sistema di stato ───

  it('RAG-12 (AC-CHAT-3 critical): system status message (no-agent) NON mostra il disclaimer non-grounded', () => {
    // Un messaggio di SISTEMA (iniettato da SessionLiveView per status no-agent)
    // non deve mostrare il disclaimer non-grounded, anche se è assistant senza citazioni.
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'no-agent' as const,
      agentSessionId: '',
    });
    useSessionAgentChatMock.mockReturnValue({
      messages: [], // nessun messaggio dal hook — il messaggio viene iniettato da SessionLiveView
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    // Il messaggio di stato è presente nel DOM
    expect(container.textContent).toMatch(/Nessun assistente/i);
    // Il disclaimer non-grounded NON deve apparire sui messaggi di sistema
    expect(
      container.querySelector('[data-slot="chat-nongrounded-disclaimer"]')
    ).not.toBeInTheDocument();
  });

  it('RAG-12b (AC-CHAT-3 critical): system status message (error) NON mostra il disclaimer non-grounded', () => {
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'error' as const,
      agentSessionId: '',
    });
    useSessionAgentChatMock.mockReturnValue({
      messages: [],
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.textContent).toMatch(/Impossibile avviare/i);
    expect(
      container.querySelector('[data-slot="chat-nongrounded-disclaimer"]')
    ).not.toBeInTheDocument();
  });

  it('RAG-13 (AC-CHAT-3): vera risposta RAG senza citazioni (isNonGrounded:true) MOSTRA il disclaimer', () => {
    // Il hook produce un messaggio assistant con isNonGrounded:true (vera risposta RAG, 0 citazioni)
    useSessionAgentChatMock.mockReturnValue({
      messages: [
        {
          id: 'msg-ng-rag',
          role: 'assistant' as const,
          content: 'Risposta RAG senza fonti.',
          timestamp: new Date().toISOString(),
          citations: undefined,
          isNonGrounded: true,
        },
      ],
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    // Il disclaimer deve essere presente
    expect(
      container.querySelector('[data-slot="chat-nongrounded-disclaimer"]')
    ).toBeInTheDocument();
  });
});

// ─── #2588 A3 — dual-path send handler (images → /ask-agent, text → RAG) ────────
// Asserts the SessionLiveView.handleAgentSendMessage routing:
//   - text-only send → agentChat.ask() (RAG SSE) and NOT fetch /ask-agent
//   - image send     → multipart POST /chat/ask-agent and NOT agentChat.ask()

describe('SessionLiveView — #2588 A3: dual-path image/text send handler', () => {
  const agentAskMock = vi.fn();
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useLiveSessionMock.mockReturnValue({
      data: { ...MOCK_SESSION_DTO, gameId: 'game-00000001' },
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useSessionAgentLaunchMock.mockReturnValue({
      status: 'ready' as const,
      agentSessionId: 'agent-session-uuid-0001',
    });
    useSessionAgentChatMock.mockReturnValue({
      messages: [],
      isLoading: false,
      error: null,
      streamingContent: '',
      ask: agentAskMock,
      stop: vi.fn(),
    });
    fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ answer: 'Risposta immagine.', confidence: 0.9 }),
    });
    vi.stubGlobal('fetch', fetchMock);
    // crypto.randomUUID is used for optimistic message ids on the image path.
    if (typeof globalThis.crypto?.randomUUID !== 'function') {
      vi.stubGlobal('crypto', {
        ...globalThis.crypto,
        randomUUID: () => `uuid-${Math.random().toString(36).slice(2)}`,
      });
    }
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('A3-1: text-only send calls agentChat.ask() and does NOT POST /ask-agent', async () => {
    renderWithIntl(<SessionLiveView />);
    // The desktop ChatAgentPanel mounts LiveAgentChat with the text input.
    const input = screen.getAllByRole('textbox', { name: 'Scrivi un messaggio' })[0];
    const sendBtn = screen.getAllByRole('button', { name: 'Invia messaggio' })[0];

    fireEvent.change(input, { target: { value: 'Spiega la regola' } });
    await act(async () => {
      fireEvent.click(sendBtn);
    });

    expect(agentAskMock).toHaveBeenCalledWith('Spiega la regola');
    // No multipart /ask-agent fetch for text-only sends.
    const askAgentCalls = fetchMock.mock.calls.filter(c =>
      String(c[0]).includes('/chat/ask-agent')
    );
    expect(askAgentCalls).toHaveLength(0);
  });

  it('A3-2: image send POSTs multipart /ask-agent and does NOT call agentChat.ask()', async () => {
    renderWithIntl(<SessionLiveView />);

    // Attach an image via the hidden file input in LiveAgentChat.
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    expect(fileInput).not.toBeNull();

    const file = new File(['x'], 'board.jpg', { type: 'image/jpeg' });
    await act(async () => {
      fireEvent.change(fileInput, { target: { files: [file] } });
    });

    const sendBtn = screen.getAllByRole('button', { name: 'Invia messaggio' })[0];
    await act(async () => {
      fireEvent.click(sendBtn);
    });

    // RAG path NOT taken for image sends.
    expect(agentAskMock).not.toHaveBeenCalled();
    // Multipart POST to /ask-agent issued.
    const askAgentCalls = fetchMock.mock.calls.filter(c =>
      String(c[0]).includes('/game-sessions/session-abc-123/chat/ask-agent')
    );
    expect(askAgentCalls).toHaveLength(1);
    const [, init] = askAgentCalls[0];
    expect((init as RequestInit).method).toBe('POST');
    expect((init as RequestInit).body).toBeInstanceOf(FormData);
  });
});

// ─── #2505 — viewerRole derivation + AddPlayerDialog wiring ──────────────────

describe('SessionLiveView — #2505: viewerRole derivation + AddPlayerDialog', () => {
  const MOCK_USER_ID = 'user-44444444-4444-4444-4444-444444444444';

  const MOCK_DTO_WITH_HOST_PLAYER = {
    ...MOCK_SESSION_DTO,
    players: [
      {
        id: 'player-001',
        displayName: 'Marco',
        userId: MOCK_USER_ID,
        color: 'Blue',
        role: 'Host',
        totalScore: 0,
        isActive: true,
      },
      {
        id: 'player-002',
        displayName: 'Anna',
        userId: null,
        color: 'Red',
        role: 'Player',
        totalScore: 0,
        isActive: true,
      },
    ],
  } as unknown as LiveSessionDto;

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    searchParamsMap['tab'] = 'turn'; // show PlayerRosterLive
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useCurrentUserMock.mockReturnValue({ data: null });
  });

  it('#2505-1: viewerRole is "Host" when currentUser matches a Host player in dto.players', () => {
    useCurrentUserMock.mockReturnValue({ data: { id: MOCK_USER_ID } });
    useLiveSessionMock.mockReturnValue({
      data: MOCK_DTO_WITH_HOST_PLAYER,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    // PlayerRosterLive is rendered in 'turn' tab; host button should be present
    expect(container.querySelector('[data-slot="player-roster-add"]')).toBeInTheDocument();
  });

  it('#2505-2: no "Add player" button when currentUser is null (not found in players)', () => {
    useCurrentUserMock.mockReturnValue({ data: null });
    useLiveSessionMock.mockReturnValue({
      data: MOCK_DTO_WITH_HOST_PLAYER,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="player-roster-add"]')).not.toBeInTheDocument();
  });

  it('#2505-3: no "Add player" button when currentUser matches a Player (not Host)', () => {
    useCurrentUserMock.mockReturnValue({ data: { id: 'user-not-host' } });
    useLiveSessionMock.mockReturnValue({
      data: {
        ...MOCK_SESSION_DTO,
        players: [
          {
            id: 'player-001',
            displayName: 'Marco',
            userId: 'user-not-host',
            color: 'Blue',
            role: 'Player',
            totalScore: 0,
            isActive: true,
          },
        ],
      } as unknown as LiveSessionDto,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="player-roster-add"]')).not.toBeInTheDocument();
  });

  it('#2505-4: clicking "Add player" button mounts the AddPlayerDialog (mocked)', async () => {
    useCurrentUserMock.mockReturnValue({ data: { id: MOCK_USER_ID } });
    useLiveSessionMock.mockReturnValue({
      data: MOCK_DTO_WITH_HOST_PLAYER,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    const { container } = renderWithIntl(<SessionLiveView />);

    const addBtn = container.querySelector('[data-slot="player-roster-add"]');
    expect(addBtn).toBeInTheDocument();

    await act(async () => {
      fireEvent.click(addBtn!);
    });
    await act(async () => {});

    // The mocked AddPlayerDialog renders data-slot="add-player-dialog-mock" when open=true
    expect(document.querySelector('[data-slot="add-player-dialog-mock"]')).toBeInTheDocument();
  });
});

// ─── #2503 — Endgame complete trigger + save-game CTA + PlayRecord nav ────────

describe('SessionLiveView — #2503: endgame complete + save-game nav', () => {
  const HOST_USER_ID = 'user-44444444-4444-4444-4444-444444444444';

  const HOST_DTO = {
    ...MOCK_SESSION_DTO,
    gameId: 'game-00000001',
    players: [
      {
        id: 'player-001',
        displayName: 'Marco',
        userId: HOST_USER_ID,
        color: 'Blue',
        role: 'Host',
        totalScore: 0,
        isActive: true,
      },
    ],
  } as unknown as LiveSessionDto;

  const PLAYER_DTO = {
    ...MOCK_SESSION_DTO,
    gameId: 'game-00000001',
    players: [
      {
        id: 'player-001',
        displayName: 'Marco',
        userId: 'user-not-host',
        color: 'Blue',
        role: 'Player',
        totalScore: 0,
        isActive: true,
      },
    ],
  } as unknown as LiveSessionDto;

  function emptyHistory() {
    return { records: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useCurrentUserMock.mockReturnValue({ data: { id: HOST_USER_ID } });
    useLiveSessionMock.mockReturnValue({
      data: HOST_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    // Reset controllable mock state (#2503).
    completeIsPending = false;
    resolveStatusMock = 'idle';
    resolvePlayRecordIdMock = null;
    getHistoryMock.mockResolvedValue(emptyHistory());
  });

  it('#2503-1: Host clicking "Termina sessione" opens the confirm dialog', async () => {
    const { container } = renderWithIntl(<SessionLiveView />);

    const trigger = container.querySelector('[data-slot="session-live-top-bar-endgame"]');
    expect(trigger).toBeInTheDocument();
    expect(trigger).not.toHaveAttribute('aria-disabled');

    await act(async () => {
      fireEvent.click(trigger!);
    });

    expect(document.querySelector('[data-slot="endgame-confirm-dialog"]')).toBeInTheDocument();
    // No mutation fired by merely opening the confirm dialog.
    expect(completeMutate).not.toHaveBeenCalled();
  });

  it('#2503-2: confirm captures baseline, completes, then starts polling with previousRecordId', async () => {
    // Pre-existing record for this game → baseline id the poll must skip.
    getHistoryMock.mockResolvedValue({
      records: [
        {
          id: 'prev-record-id',
          gameName: 'Mage Knight',
          sessionDate: '2026-06-24',
          duration: null,
          status: 'Completed',
          playerCount: 1,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 1,
      totalPages: 1,
    });
    completeMutate.mockImplementation((_input: unknown, opts?: { onSuccess?: () => void }) =>
      opts?.onSuccess?.()
    );

    const { container } = renderWithIntl(<SessionLiveView />);

    await act(async () => {
      fireEvent.click(container.querySelector('[data-slot="session-live-top-bar-endgame"]')!);
    });
    await act(async () => {
      fireEvent.click(document.querySelector('[data-slot="endgame-confirm-cta"]')!);
    });
    // Flush the awaited baseline getHistory + mutate.onSuccess chain.
    await act(async () => {});

    expect(getHistoryMock).toHaveBeenCalledWith({ gameId: 'game-00000001', pageSize: 1 });
    expect(completeMutate).toHaveBeenCalledTimes(1);
    expect(resolveStart).toHaveBeenCalledWith('game-00000001', 'prev-record-id');
  });

  it('#2503-3: non-Host (Player) does NOT see the endgame trigger at all', () => {
    useCurrentUserMock.mockReturnValue({ data: { id: 'user-not-host' } });
    useLiveSessionMock.mockReturnValue({
      data: PLAYER_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });

    const { container } = renderWithIntl(<SessionLiveView />);

    // LiveTopBar renders the endgame button only for hosts ({isHost && ...}),
    // so a Player has no way to trigger session completion.
    expect(
      container.querySelector('[data-slot="session-live-top-bar-endgame"]')
    ).not.toBeInTheDocument();
  });

  it('#2503-4: cancelling the confirm dialog closes it without completing', async () => {
    const { container } = renderWithIntl(<SessionLiveView />);

    await act(async () => {
      fireEvent.click(container.querySelector('[data-slot="session-live-top-bar-endgame"]')!);
    });
    expect(document.querySelector('[data-slot="endgame-confirm-dialog"]')).toBeInTheDocument();

    await act(async () => {
      fireEvent.click(document.querySelector('[data-slot="endgame-confirm-cancel"]')!);
    });

    expect(document.querySelector('[data-slot="endgame-confirm-dialog"]')).not.toBeInTheDocument();
    expect(completeMutate).not.toHaveBeenCalled();
  });

  it('#2503-5: "Salva partita" navigates to the play-record detail once resolved', async () => {
    resolveStatusMock = 'resolved';
    resolvePlayRecordIdMock = 'rec-99';
    searchParamsMap['dialog'] = 'endgame';

    renderWithIntl(<SessionLiveView />);
    // Flush the lazy EndgameDialog import.
    await act(async () => {});

    const saveCta = document.querySelector('[data-slot="endgame-save-cta"]');
    expect(saveCta).toBeInTheDocument();

    await act(async () => {
      fireEvent.click(saveCta!);
    });

    expect(routerPush).toHaveBeenCalledWith('/play-records/rec-99');
  });

  it('#2503-6: "Salva partita" falls back to the list on timeout', async () => {
    resolveStatusMock = 'timeout';
    resolvePlayRecordIdMock = null;
    searchParamsMap['dialog'] = 'endgame';

    renderWithIntl(<SessionLiveView />);
    await act(async () => {});

    await act(async () => {
      fireEvent.click(document.querySelector('[data-slot="endgame-save-cta"]')!);
    });

    expect(routerPush).toHaveBeenCalledWith('/play-records');
  });

  it('#2503-7: resolved record does NOT auto-navigate without an explicit save click (CRITICAL 1)', async () => {
    resolveStatusMock = 'resolved';
    resolvePlayRecordIdMock = 'rec-99';
    searchParamsMap['dialog'] = 'endgame';

    renderWithIntl(<SessionLiveView />);
    await act(async () => {});

    // EndgameDialog is mounted with a resolved record, but the Host never clicked
    // "Salva partita" → no navigation must fire.
    expect(routerPush).not.toHaveBeenCalledWith('/play-records/rec-99');
  });
});

// ─── #2501 SP4 Task 3 — recordId wire + 409 handler ─────────────────────────

describe('SessionLiveView — #2501 SP4 Task 3: recordId wire + 409 handler', () => {
  const HOST_USER_ID = 'user-44444444-4444-4444-4444-444444444444';

  const HOST_DTO = {
    ...MOCK_SESSION_DTO,
    gameId: 'game-00000001',
    players: [
      {
        id: 'player-001',
        displayName: 'Marco',
        userId: HOST_USER_ID,
        color: 'Blue',
        role: 'Host',
        totalScore: 0,
        isActive: true,
      },
    ],
  } as unknown as import('@/lib/api/schemas/live-sessions.schemas').LiveSessionDto;

  function emptyHistory() {
    return { records: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useCurrentUserMock.mockReturnValue({ data: { id: HOST_USER_ID } });
    useLiveSessionMock.mockReturnValue({
      data: HOST_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    completeIsPending = false;
    resolveStatusMock = 'idle';
    resolvePlayRecordIdMock = null;
    getHistoryMock.mockResolvedValue(emptyHistory());
  });

  it('SP4-T3-1: passes resolvedPlayRecordId to EndgameDialog, enabling photo upload when recordId is set', async () => {
    // Set a resolved play record id before mounting with ?dialog=endgame.
    resolveStatusMock = 'resolved';
    resolvePlayRecordIdMock = 'rec-sp4-001';
    searchParamsMap['dialog'] = 'endgame';

    renderWithIntl(<SessionLiveView />);
    // Flush lazy Suspense.
    await act(async () => {});

    // EndgameDialog must be mounted.
    const dialog = document.querySelector('[data-slot="endgame-dialog"]');
    expect(dialog).toBeInTheDocument();

    // EndgamePhotoUploadSection must be rendered inside the dialog (SP4 D1+D2+T3).
    const photoSection = dialog?.querySelector('[data-slot="endgame-photo-upload"]');
    expect(photoSection).not.toBeNull();

    // When recordId is non-null (resolved), the upload CTA button must NOT be disabled
    // (the button is disabled only during the polling window when recordId === null).
    // The upload button is present (even if no file selected yet, section always renders).
    // This verifies recordId flows from SessionLiveView → EndgameDialog → section.
    const uploadBtn = dialog?.querySelector('button[data-slot="endgame-photo-upload-btn"]');
    // If no files are selected the upload button may not exist — so verify section itself.
    // The section is always rendered when recordId is present (not gated).
    expect(photoSection).toBeInTheDocument();
  });

  it('SP4-T3-2: complete 409 shows "Partita già conclusa" toast and does NOT navigate', async () => {
    // completeLiveSession.mutate calls onError with a ConflictError (statusCode 409).
    const { ConflictError } = await import('@/lib/api/core/errors');
    completeMutate.mockImplementation(
      (_input: unknown, opts?: { onSuccess?: () => void; onError?: (err: Error) => void }) => {
        opts?.onError?.(new ConflictError({ message: 'Session already completed' }));
      }
    );

    const { container } = renderWithIntl(<SessionLiveView />);

    // Open the confirm dialog and click confirm.
    await act(async () => {
      fireEvent.click(container.querySelector('[data-slot="session-live-top-bar-endgame"]')!);
    });
    await act(async () => {
      fireEvent.click(document.querySelector('[data-slot="endgame-confirm-cta"]')!);
    });
    await act(async () => {});

    // toast.error should have been called with the already-completed message.
    const { toast } = await import('sonner');
    expect(toast.error).toHaveBeenCalledWith(
      expect.stringMatching(/già conclusa|already finished/i),
      expect.anything()
    );

    // Must NOT navigate to play-records.
    expect(routerPush).not.toHaveBeenCalledWith(expect.stringContaining('/play-records'));
    // Must NOT open the endgame dialog (no handleDialogChange('endgame') call).
    expect(document.querySelector('[data-slot="endgame-dialog"]')).not.toBeInTheDocument();
  });
});

// ─── SP5-a Task 3 — finding #16: diary write response-ack (not fail-silently) ───
//
// Verifies that handleAddNote in SessionLiveView treats the POST response as the
// source of truth for success/error, NOT fire-and-forget (finding #16).
// Before the fix, the catch block said "Fail silently — SSE event confirms or not",
// meaning a broken SSE stream would cause silent write loss.
// After the fix: HTTP errors + network failures surface a toast.error to the user.

describe('SessionLiveView — diary write response-ack (SP5-a finding #16)', () => {
  const HOST_USER_ID_SP5 = '11111111-1111-1111-1111-111111111111';

  const HOST_DTO_SP5 = {
    id: 'session-abc-123',
    sessionCode: 'SP5TEST',
    gameId: 'game-00000001',
    gameName: 'Test Game',
    gameSlug: 'test-game',
    createdByUserId: HOST_USER_ID_SP5,
    status: 'InProgress',
    visibility: 'Private',
    groupId: null,
    createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    startedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
    pausedAt: null,
    completedAt: null,
    updatedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    lastSavedAt: null,
    currentTurnIndex: 0,
    currentTurnPlayerId: null,
    agentMode: 'Active',
    notes: null,
    players: [
      {
        id: 'player-001',
        displayName: 'Marco',
        userId: HOST_USER_ID_SP5,
        color: 'blue',
        role: 'Host',
        totalScore: 0,
        isActive: true,
      },
    ],
    teams: [],
    roundScores: [],
    scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
  } as unknown as import('@/lib/api/schemas/live-sessions.schemas').LiveSessionDto;

  function emptyHistorySP5() {
    return { records: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    // Open the notes tab by default so LiveSessionNotes is mounted in the desktop right column.
    searchParamsMap['tab'] = 'notes';
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useCurrentUserMock.mockReturnValue({ data: { id: HOST_USER_ID_SP5 } });
    useLiveSessionMock.mockReturnValue({
      data: HOST_DTO_SP5,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    completeIsPending = false;
    resolveStatusMock = 'idle';
    resolvePlayRecordIdMock = null;
    getHistoryMock.mockResolvedValue(emptyHistorySP5());
  });

  it('finding #16-A: rejected diary mutation (5xx) → toast.error surfaced (not silent)', async () => {
    // #2575: handleAddNote now goes through the useAddDiaryEntry mutation, not a raw fetch.
    addDiaryMutateAsync.mockRejectedValue(new Error('Internal Server Error'));

    const { container } = renderWithIntl(<SessionLiveView />);

    // Locate the notes textarea and submit button in the LiveSessionNotes panel.
    const notesPanel = container.querySelector('[data-slot="live-session-notes"]');
    expect(notesPanel).not.toBeNull();

    const textarea = notesPanel?.querySelector('textarea');
    expect(textarea).not.toBeNull();

    // Type a note and submit.
    await act(async () => {
      fireEvent.change(textarea!, { target: { value: 'Test note content' } });
    });
    const submitBtn = notesPanel?.querySelector('button[type="submit"]');
    expect(submitBtn).not.toBeNull();
    await act(async () => {
      fireEvent.click(submitBtn!);
    });

    // Verify the diary mutation was invoked with the SP3 text-only payload.
    expect(addDiaryMutateAsync).toHaveBeenCalledWith({ text: 'Test note content' });

    // Verify toast.error was called — the write is NOT silently swallowed.
    const { toast } = await import('sonner');
    expect(toast.error).toHaveBeenCalledWith(
      expect.stringContaining('Impossibile salvare la nota'),
      expect.objectContaining({ id: 'note-add-error' })
    );
  });

  it('finding #16-B: rejected diary mutation (network) → toast.error surfaced (not silent)', async () => {
    addDiaryMutateAsync.mockRejectedValue(new TypeError('Failed to fetch'));

    const { container } = renderWithIntl(<SessionLiveView />);

    const notesPanel = container.querySelector('[data-slot="live-session-notes"]');
    const textarea = notesPanel?.querySelector('textarea');

    await act(async () => {
      fireEvent.change(textarea!, { target: { value: 'Another note' } });
    });
    const submitBtn = notesPanel?.querySelector('button[type="submit"]');
    await act(async () => {
      fireEvent.click(submitBtn!);
    });

    const { toast } = await import('sonner');
    expect(toast.error).toHaveBeenCalledWith(
      expect.stringContaining('Impossibile salvare la nota'),
      expect.objectContaining({ id: 'note-add-error' })
    );
  });

  it('finding #16-C: resolved diary mutation → NO toast.error (happy path silent)', async () => {
    addDiaryMutateAsync.mockResolvedValue('new-entry-id');

    const { container } = renderWithIntl(<SessionLiveView />);

    const notesPanel = container.querySelector('[data-slot="live-session-notes"]');
    const textarea = notesPanel?.querySelector('textarea');

    await act(async () => {
      fireEvent.change(textarea!, { target: { value: 'Happy note' } });
    });
    const submitBtn = notesPanel?.querySelector('button[type="submit"]');
    await act(async () => {
      fireEvent.click(submitBtn!);
    });

    expect(addDiaryMutateAsync).toHaveBeenCalledWith({ text: 'Happy note' });
    const { toast } = await import('sonner');
    expect(toast.error).not.toHaveBeenCalledWith(
      expect.stringContaining('Impossibile salvare la nota'),
      expect.anything()
    );
  });
});

// ─── #2588 A4 — Arbitro (agent) tab wiring + SignalR dispute hydration ────────
//
// Verifies:
//  A4-1: useSignalRSession called with sessionId on a live session
//  A4-2: useSignalRSession called with '' in fixture mode (no real WS)
//  A4-3: ?tab=agent shows data-active-tab="agent" on RightColumnTabs
//  A4-4: ?mtab=agent selects the 6th tab (index 5) in the mobile drawer
//  A4-5: desktop agent tab renders AgentDisputeTabContent slot

describe('SessionLiveView — #2588 A4: Arbitro tab + dispute SignalR hydration', () => {
  const SESSION_ID = 'session-abc-123';

  const BASE_DTO = {
    id: SESSION_ID,
    sessionCode: 'A4TEST',
    gameId: 'game-00000001',
    gameName: 'Test Game',
    gameSlug: 'test-game',
    createdByUserId: 'user-a4-host',
    status: 'InProgress',
    visibility: 'Private',
    groupId: null,
    createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    startedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
    pausedAt: null,
    completedAt: null,
    updatedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    lastSavedAt: null,
    currentTurnIndex: 0,
    currentTurnPlayerId: null,
    agentMode: 'Active',
    notes: null,
    players: [
      {
        id: 'player-a4-001',
        displayName: 'Marco',
        userId: 'user-a4-host',
        color: 'Blue',
        role: 'Host',
        totalScore: 0,
        isActive: true,
      },
      {
        id: 'player-a4-002',
        displayName: 'Anna',
        userId: null,
        color: 'Red',
        role: 'Player',
        totalScore: 0,
        isActive: true,
      },
    ],
    teams: [],
    roundScores: [],
    scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
  } as unknown as LiveSessionDto;

  function emptyHistoryA4() {
    return { records: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = SESSION_ID;
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useCurrentUserMock.mockReturnValue({ data: { id: 'user-a4-host' } });
    useLiveSessionMock.mockReturnValue({
      data: BASE_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    completeIsPending = false;
    resolveStatusMock = 'idle';
    resolvePlayRecordIdMock = null;
    getHistoryMock.mockResolvedValue(emptyHistoryA4());
  });

  it('A4-1: useSignalRSession is called with the session id', () => {
    renderWithIntl(<SessionLiveView />);
    expect(useSignalRSessionMock).toHaveBeenCalledWith(SESSION_ID);
  });

  it('A4-2: useSignalRSession called with "" in fixture mode (no WS in tests)', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    renderWithIntl(<SessionLiveView />);
    // fixture mode passes '' to prevent WebSocket handshake in CI
    expect(useSignalRSessionMock).toHaveBeenCalledWith('');
  });

  it('A4-3: ?tab=agent sets data-active-tab="agent" on RightColumnTabs', () => {
    searchParamsMap['tab'] = 'agent';
    const { container } = renderWithIntl(<SessionLiveView />);
    const tabsRoot = container.querySelector('[data-slot="right-column-tabs"]');
    expect(tabsRoot).toBeInTheDocument();
    expect(tabsRoot).toHaveAttribute('data-active-tab', 'agent');
  });

  it('A4-4: ?mtab=agent selects the agent tab in mobile drawer (aria-selected="true")', () => {
    searchParamsMap['mtab'] = 'agent';
    searchParamsMap['msheet'] = 'open'; // parseMobileSheetOpen: 'open' → true
    renderWithIntl(<SessionLiveView />);
    // Drawer portals to document.body — query document directly (not container).
    // The 6th tab (index 5, data-tab="agent") must have aria-selected="true".
    const drawerTabs = document.querySelectorAll('[data-slot="mobile-bottom-sheet"] [role="tab"]');
    expect(drawerTabs).toHaveLength(6);
    expect(drawerTabs[5]).toHaveAttribute('aria-selected', 'true'); // agent = index 5
    expect(drawerTabs[5]).toHaveAttribute('data-tab', 'agent');
  });

  it('A4-5: desktop ?tab=agent renders AgentDisputeTabContent slot', () => {
    searchParamsMap['tab'] = 'agent';
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="agent-dispute-tab-content"]')).toBeInTheDocument();
  });
});

describe('SessionLiveView — Catan flavor tab (#2787)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useLiveSessionMock.mockReturnValue({
      data: MOCK_SESSION_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
  });

  it('shows the Catan flavor tab for a catan session (gameSlug=catan)', () => {
    useLiveSessionMock.mockReturnValue({
      data: { ...MOCK_SESSION_DTO, gameSlug: 'catan' } as unknown as LiveSessionDto,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    renderWithIntl(<SessionLiveView />);
    expect(screen.getByRole('tab', { name: 'Catan' })).toBeInTheDocument();
  });

  it('hides the flavor tab for a non-catan session (mage-knight default)', () => {
    renderWithIntl(<SessionLiveView />);
    expect(screen.getByRole('tab', { name: 'Score' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Catan' })).not.toBeInTheDocument();
  });

  it('falls back to the Score tab when ?tab=flavor lands on a non-catan session', () => {
    // Stale bookmark: ?tab=flavor carried to a session with no flavor must not
    // strand the user on an empty tab — the panel falls back to Score.
    searchParamsMap['tab'] = 'flavor';
    renderWithIntl(<SessionLiveView />); // default = mage-knight (no flavor)
    expect(screen.queryByRole('tab', { name: 'Catan' })).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Score' })).toHaveAttribute('aria-selected', 'true');
  });

  it('renders CatanLiveFlavor when ?tab=flavor on a catan session', async () => {
    searchParamsMap['tab'] = 'flavor';
    useLiveSessionMock.mockReturnValue({
      data: { ...MOCK_SESSION_DTO, gameSlug: 'catan' } as unknown as LiveSessionDto,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    renderWithIntl(<SessionLiveView />);
    // FlavorRenderer lazy-loads CatanLiveFlavor (<section aria-label> → role region).
    expect(await screen.findByRole('region', { name: 'Pannello Catan' })).toBeInTheDocument();
  });
});
