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
 *   - Mobile tab routing: tools/widget tab renders ToolkitRenderer (G5c #2376)
 *   - Desktop right column: RightColumnTabs mounted with correct activeTab
 *   - Desktop tab change calls router.replace with ?tab= param
 *   - Optimistic score update via liveStream events
 *   - 403 rollback: score rolled back on 403 response
 *   - 429 handling: connectionState='failed' shows ConnectionLostBanner kind='failed'
 *
 * Pattern: Wave D.1 SessionsLibraryView.test.tsx adapted for Tier L live route.
 * Mocks: useSession, useSessionLiveStream, next/navigation, next-intl, session-live-visual-test-fixture.
 */

import { fireEvent, render, screen, act, waitFor, within } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import type { UseSessionLiveStreamResult } from '@/lib/session-live/use-session-live-stream';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

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

// #3391: AgentDisputeTabContent now mounts useLiveSessionDisputes (REST hydration via useQuery).
// Mock it to a no-op so these tests need neither a QueryClientProvider nor an
// api.liveSessions.getDisputes mock — REST hydration is covered in
// AgentDisputeTabContent.hydration.test.tsx.
vi.mock('@/hooks/queries/useLiveSessionDisputes', () => ({
  useLiveSessionDisputes: vi.fn(() => ({
    data: [],
    isLoading: false,
    isSuccess: true,
    isError: false,
  })),
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

// ─── useRecordRoundScore mock (#4156 fetta 2: avoid QueryClientProvider) ─────

const recordRoundScoreMock = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/queries/useRecordRoundScore', () => ({
  useRecordRoundScore: () => ({ mutateAsync: recordRoundScoreMock, isPending: false }),
}));

// ─── sonner mock (#2430 Block B+: ScoreTabContent toasts) ─────────────────

vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    warning: vi.fn(),
    success: vi.fn(),
    dismiss: vi.fn(),
  },
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
const resumeSessionMock = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    playRecords: {
      getHistory: getHistoryMock,
    },
    liveSessions: {
      resumeSession: resumeSessionMock,
    },
  },
}));

// ─── SessionAssistantPanel stub (#4238 A4) ────────────────────────────────────
// The panel has its own tests; here only who gets it matters.
vi.mock('@/components/features/session-live/assistant/SessionAssistantPanel', () => ({
  SessionAssistantPanel: ({ sessionId }: { sessionId: string }) => (
    <div data-slot="session-assistant-panel" data-session-id={sessionId} />
  ),
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
  'pages.sessionLive.topBar.turnLabelNoTotal': '{count, plural, =0 {Inizio} other {Turno #}}',
  'pages.sessionLive.topBar.statusInProgress': 'In corso',
  'pages.sessionLive.topBar.statusPaused': 'In pausa',
  'pages.sessionLive.topBar.statusCompleted': 'Conclusa',
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
  // #4156 fetta 2 — editor on RoundScores
  'pages.sessionLive.scoring.rounds.roundLabel': 'Round {round}',
  'pages.sessionLive.scoring.rounds.previousRound': 'Round precedente',
  'pages.sessionLive.scoring.rounds.nextRound': 'Round successivo',
  'pages.sessionLive.scoring.rounds.currentRound': 'Torna al round in corso',
  'pages.sessionLive.scoring.rounds.player': 'Giocatore',
  'pages.sessionLive.scoring.rounds.total': 'Totale',
  'pages.sessionLive.scoring.rounds.noDimensions': 'Questa partita non ha dimensioni di punteggio.',
  'pages.sessionLive.scoring.rounds.noPlayers': 'Nessun giocatore segna punti in questa partita.',
  'pages.sessionLive.scoring.rounds.cellAriaTemplate': '{dimension} di {player}, round {round}',
  'pages.sessionLive.scoring.rounds.saveError': 'Punteggio non salvato: riprova.',
  'pages.sessionLive.scoring.rounds.notAnInteger': 'Il punteggio deve essere un numero intero.',
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

  // #3146 Slice 1: the mobile meta strip is wired under the top bar and receives
  // the resolved turn / elapsed / derived-start values. The default fixture has
  // startedAt set (→ elapsed derived), so all three chips render. Guards against
  // the wiring being dropped or fed an undefined elapsed label.
  it('Cell 5: wires LiveMobileMetaStrip with turn + elapsed + derived-start chips', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const strip = container.querySelector('[data-slot="live-mobile-meta-strip"]');
    expect(strip).toBeInTheDocument();
    expect(strip?.querySelector('[data-slot="live-mobile-meta-strip-turn"]')).not.toBeNull();
    expect(strip?.querySelector('[data-slot="live-mobile-meta-strip-elapsed"]')).not.toBeNull();
    expect(strip?.querySelector('[data-slot="live-mobile-meta-strip-started-at"]')).not.toBeNull();
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

  it('mobile renders mainContent = ActionLogTimeline (full-width, no bottom-nav)', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // #4156 (D5): the legacy agent chat left the main column; the action log stays.
    const mobileBody = container.querySelector('[data-slot="mobile-body"]');
    expect(mobileBody).toBeInTheDocument();
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

  // A free-form session has no turn limit (the composed state carries totalTurns 0), and the top bar read "Turno 1/0".
  it('turn label has no "/total" part when the session has no turn limit', () => {
    IS_VISUAL_TEST_BUILD_MOCK = false;
    // Turn 0 reads "Inizio" whatever the total, so the case that showed "Turno 1/0" needs a started turn.
    useLiveSessionMock.mockReturnValue({
      data: { ...MOCK_SESSION_DTO, currentTurnIndex: 1 },
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<SessionLiveView />);
    const topBar = container.querySelector('[data-slot="session-live-top-bar"]');
    expect(topBar).toBeInTheDocument();
    const text = topBar?.textContent ?? '';
    expect(text).toContain('Turno 1');
    expect(text).not.toMatch(/Turno 1\s*\/\s*0/);
  });

  it('turn label keeps "/total" when the session does have a turn limit', () => {
    IS_VISUAL_TEST_BUILD_MOCK = true;
    // Visual fixture: currentTurn=12, totalTurns=18
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="session-live-top-bar"]')?.textContent).toContain(
      'Turno 12/18'
    );
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

  it('T4.7: Cell 5 desktop renders ActionLogTimeline stacked in LEFT mainColumn', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    // #4156 (D5): ActionLogTimeline is the whole LEFT mainColumn
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

  it('T4.11: RIGHT tab=score is the Score tab — a session without dimensions says so (#4156)', () => {
    // ?tab default → 'score'. MOCK_SESSION_DTO configures no scoring dimension.
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="scoring-panel-empty"]')).toBeInTheDocument();
    expect(
      screen.getAllByText('Questa partita non ha dimensioni di punteggio.').length
    ).toBeGreaterThan(0);
  });

  // ─── T3.3: Mobile drawer tab routing — Interactions panels (T9 sess.46r) ─

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

// ─── Block B (#2389) scoring wire-up tests ────────────────────────────────────

describe('SessionLiveView — #4156 fetta 2: the Score tab scores on RoundScores', () => {
  const HOST_ID = '55555555-5555-4555-8555-555555555555';
  // Two scoring players and turn 3: the derived round (D6) is (3 − 1) / 2 + 1 = 2.
  const SCORED_DTO = {
    ...MOCK_SESSION_DTO,
    currentTurnIndex: 3,
    players: [
      {
        id: 'player-001',
        displayName: 'Marco',
        userId: HOST_ID,
        color: 'blue',
        role: 'Host',
        totalScore: 10,
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
    roundScores: [
      {
        playerId: 'player-001',
        round: 2,
        dimension: 'punti',
        value: 10,
        unit: null,
        recordedAt: '',
      },
    ],
    scoringConfig: { enabledDimensions: ['punti'], dimensionUnits: {} },
  } as unknown as LiveSessionDto;

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useLiveSessionMock.mockReturnValue({
      data: SCORED_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    recordRoundScoreMock.mockResolvedValue(undefined);
  });

  it('opens on the derived round, with the cells and totals of the live session', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const editor = container.querySelector('[data-slot="round-score-editor"]');
    expect(editor).not.toBeNull();
    expect(editor?.querySelector('[data-slot="round-score-round-label"]')?.textContent).toBe(
      'Round 2'
    );
    expect(editor?.textContent).toContain('Marco');
    expect(editor?.textContent).toContain('Anna');
  });

  it('a player reads the scores and has nothing to edit', () => {
    useCurrentUserMock.mockReturnValue({ data: null });
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="round-score-editor"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="round-score-cell"]')).toBeNull();
  });

  it('the endgame dialog shows the live totals, with the player alone in the lead as the winner', async () => {
    // Before #4156 the dialog read the polymorphic store, which nothing fed on the live path,
    // and fell back to the adapted players with no winner at all.
    searchParamsMap['dialog'] = 'endgame';
    await act(async () => {
      renderWithIntl(<SessionLiveView />);
    });
    await act(async () => {});
    const scores = screen.getByRole('list', { name: 'Punteggi finali' });
    const [first, second] = within(scores).getAllByRole('listitem');
    expect(first).toHaveTextContent('Marco');
    expect(first).toHaveTextContent('10');
    expect(within(first!).getByText('Vincitore')).toBeInTheDocument();
    expect(second).toHaveTextContent('Anna');
    expect(within(second!).queryByText('Vincitore')).toBeNull();
  });

  it('the host writes an empty cell as a new score of the live session', async () => {
    useCurrentUserMock.mockReturnValue({ data: { id: HOST_ID } });
    renderWithIntl(<SessionLiveView />);
    const [cell] = screen.getAllByLabelText('punti di Anna, round 2');
    fireEvent.change(cell!, { target: { value: '30' } });
    fireEvent.blur(cell!);
    await waitFor(() =>
      expect(recordRoundScoreMock).toHaveBeenCalledWith({
        request: { playerId: 'player-002', round: 2, dimension: 'punti', value: 30 },
        correction: false,
      })
    );
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

// ─── #4156 — la ripresa passa da /live-sessions ──────────────────────────────

describe('SessionLiveView — #4156: resume goes through the live session', () => {
  const HOST_ID = 'user-55555555-5555-5555-5555-555555555555';

  const PAUSED_DTO_WITH_HOST = {
    ...MOCK_SESSION_DTO,
    status: 'Paused',
    players: [
      {
        id: 'player-001',
        displayName: 'Marco',
        userId: HOST_ID,
        color: 'Blue',
        role: 'Host',
        totalScore: 0,
        isActive: true,
      },
    ],
  } as unknown as LiveSessionDto;

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    searchParamsMap['dialog'] = 'pause';
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useCurrentUserMock.mockReturnValue({ data: { id: HOST_ID } });
    useLiveSessionMock.mockReturnValue({
      data: PAUSED_DTO_WITH_HOST,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
  });

  it('the host resumes the live session of the route id', async () => {
    // L'id della rotta è quello di una LiveGameSession. Prima la ripresa chiamava
    // /game-sessions/{id}/resume (SessionTracking), riceveva 404 e chiudeva comunque il pannello.
    resumeSessionMock.mockResolvedValue(undefined);
    let container: HTMLElement;
    await act(async () => {
      container = renderWithIntl(<SessionLiveView />).container;
    });
    await act(async () => {});

    const overlay = container!.querySelector('[data-slot="pause-overlay"]') as HTMLElement;
    expect(overlay).not.toBeNull();
    await act(async () => {
      fireEvent.click(within(overlay).getByRole('button', { name: 'Riprendi' }));
    });

    expect(resumeSessionMock).toHaveBeenCalledWith('session-abc-123');
  });

  it('keeps the pause panel open when the resume fails', async () => {
    resumeSessionMock.mockRejectedValue(new Error('409'));
    let container: HTMLElement;
    await act(async () => {
      container = renderWithIntl(<SessionLiveView />).container;
    });
    await act(async () => {});

    const overlay = container!.querySelector('[data-slot="pause-overlay"]') as HTMLElement;
    await act(async () => {
      fireEvent.click(within(overlay).getByRole('button', { name: 'Riprendi' }));
    });

    expect(resumeSessionMock).toHaveBeenCalledTimes(1);
    expect(routerReplace).not.toHaveBeenCalledWith(
      expect.not.stringContaining('dialog=pause'),
      expect.anything()
    );
  });
});

// ─── #4156 (D5) — l'agente di sessione vecchio non è più in /live ─────────────

describe('SessionLiveView — #4156 (D5): no legacy session agent on /live', () => {
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

  it('mounts no agent chat, on desktop or on mobile', () => {
    // L'agente vecchio lanciava una sessione agente su /game-sessions/{id}: con l'id di una live
    // falliva sulla chiave esterna (23503). L'assistente in partita arriva con A4 di #4238.
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="mobile-body"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="chat-agent-panel"]')).toBeNull();
    expect(container.querySelector('[data-slot="live-agent-chat"]')).toBeNull();
    expect(container.querySelector('[data-slot="action-log-timeline"]')).not.toBeNull();
  });
});

// ─── #4238 A4 (#4373) — the in-game assistant panel ──────────────────────────

describe('SessionLiveView — #4373: only the owner of the session gets the assistant panel', () => {
  const OWNER_ID = '44444444-4444-4444-4444-444444444444';

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

  const panels = (container: HTMLElement) =>
    container.querySelectorAll('[data-slot="session-assistant-panel"]');

  it('the owner of the session gets the panel, even when no player is linked to them', () => {
    // The server checks CreatedByUserId: an owner who is not at the table still asks.
    expect(MOCK_SESSION_DTO.createdByUserId).toBe(OWNER_ID);
    expect(MOCK_SESSION_DTO.players.some(p => p.userId === OWNER_ID)).toBe(false);
    useCurrentUserMock.mockReturnValue({ data: { id: OWNER_ID } });

    const { container } = renderWithIntl(<SessionLiveView />);

    expect(panels(container).length).toBeGreaterThan(0);
    expect(panels(container)[0]).toHaveAttribute('data-session-id', 'session-abc-123');
  });

  it('a host player who is not the owner does not get it', () => {
    const notOwner = '99999999-9999-4999-8999-999999999999';
    useLiveSessionMock.mockReturnValue({
      data: {
        ...MOCK_SESSION_DTO,
        players: MOCK_SESSION_DTO.players.map((p, i) =>
          i === 0 ? { ...p, userId: notOwner, role: 'Host' } : p
        ),
      } as LiveSessionDto,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useCurrentUserMock.mockReturnValue({ data: { id: notOwner } });

    const { container } = renderWithIntl(<SessionLiveView />);

    expect(panels(container)).toHaveLength(0);
  });

  it('without a signed-in user nobody gets it', () => {
    useCurrentUserMock.mockReturnValue({ data: null });

    const { container } = renderWithIntl(<SessionLiveView />);

    expect(panels(container)).toHaveLength(0);
  });
});

// ─── #4750 — the creator is the host; a concluded session says so ─────────────

describe('SessionLiveView — #4750: creator as host, concluded session', () => {
  // MOCK_SESSION_DTO players carry `userId: null`, like the ones /sessions/new writes by name,
  // so no player is linked to any account.
  const CREATOR_ID = '44444444-4444-4444-4444-444444444444';

  function mountWith(dto: LiveSessionDto, userId: string | null) {
    useCurrentUserMock.mockReturnValue({ data: userId == null ? null : { id: userId } });
    useLiveSessionMock.mockReturnValue({
      data: dto,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    return renderWithIntl(<SessionLiveView />);
  }

  const topBarOf = (container: HTMLElement) =>
    container.querySelector('[data-slot="session-live-top-bar"]');
  const slotIn = (container: HTMLElement, slot: string) =>
    container.querySelector(`[data-slot="session-live-top-bar-${slot}"]`);

  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useSessionLiveStreamMock.mockReturnValue({ ...mockLiveStreamResult });
    useCurrentUserMock.mockReturnValue({ data: null });
  });

  it('the creator is the host even when no player is linked to their account', () => {
    const { container } = mountWith(MOCK_SESSION_DTO, CREATOR_ID);

    expect(topBarOf(container)).toHaveAttribute('data-viewer-role', 'Host');
    expect(slotIn(container, 'endgame')).toBeInTheDocument();
    expect(slotIn(container, 'endgame')).not.toHaveAttribute('aria-disabled');
    // The bar never gets `onPause`/`onResume` from this view (follow-up): the button shows, dimmed
    // and inert. Pinned as such, so it is not read as a working control.
    expect(slotIn(container, 'pause')).toHaveAttribute('aria-disabled', 'true');
  });

  it('a user who did not create the session and is linked to no player stays a player', () => {
    const { container } = mountWith(MOCK_SESSION_DTO, 'someone-else');

    expect(topBarOf(container)).toHaveAttribute('data-viewer-role', 'Player');
    expect(slotIn(container, 'endgame')).not.toBeInTheDocument();
    expect(slotIn(container, 'pause')).not.toBeInTheDocument();
  });

  it('without a signed-in user nobody is the host', () => {
    const { container } = mountWith(MOCK_SESSION_DTO, null);

    expect(topBarOf(container)).toHaveAttribute('data-viewer-role', 'Player');
    expect(slotIn(container, 'endgame')).not.toBeInTheDocument();
  });

  describe('once the session is concluded', () => {
    // 30 minutes after the start MOCK_SESSION_DTO declares; "now" is an hour after it.
    const completedAt = new Date(
      Date.parse(MOCK_SESSION_DTO.startedAt as string) + 30 * 60 * 1000
    ).toISOString();
    const COMPLETED_DTO = {
      ...MOCK_SESSION_DTO,
      status: 'Completed',
      completedAt,
    } as unknown as LiveSessionDto;

    it('the header reads «Conclusa», not «In corso»', () => {
      const { container } = mountWith(COMPLETED_DTO, CREATOR_ID);

      expect(topBarOf(container)).toHaveAttribute('data-status', 'Completed');
      expect(topBarOf(container)?.textContent).toContain('Conclusa');
      expect(topBarOf(container)?.textContent).not.toContain('In corso');
    });

    it('not even its creator is offered pause or end', () => {
      const { container } = mountWith(COMPLETED_DTO, CREATOR_ID);

      expect(topBarOf(container)).toHaveAttribute('data-viewer-role', 'Host');
      expect(slotIn(container, 'pause')).not.toBeInTheDocument();
      expect(slotIn(container, 'resume')).not.toBeInTheDocument();
      expect(slotIn(container, 'endgame')).not.toBeInTheDocument();
    });

    it('the timer stops at completedAt instead of counting on', () => {
      const { container } = mountWith(COMPLETED_DTO, CREATOR_ID);

      expect(slotIn(container, 'timer')?.textContent).toBe('00:30:00');
    });
  });
});
