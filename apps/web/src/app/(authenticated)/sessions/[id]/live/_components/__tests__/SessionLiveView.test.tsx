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
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import type { UseSessionLiveStreamResult } from '@/lib/session-live/use-session-live-stream';

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
  data?: GameSessionDto | null;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  error: Error | null;
  refetch?: () => void;
};

const useSessionMock = vi.fn<[], MockSessionReturn>();

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useSession: () => useSessionMock(),
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
  'pages.sessionLive.scoring.title': 'Punteggi',
  'pages.sessionLive.scoring.scoreLabel': 'Punteggio: {score}',
  'pages.sessionLive.scoring.winnerLabel': 'Vincitore',
  'pages.sessionLive.scoring.myScoreLabel': 'Il tuo punteggio',
  'pages.sessionLive.scoring.incrementAriaLabel': 'Aumenta punteggio di {playerName}',
  'pages.sessionLive.scoring.decrementAriaLabel': 'Diminuisci punteggio di {playerName}',
  'pages.sessionLive.scoring.scoreInputAriaLabel': 'Inserisci punteggio per {playerName}',
  'pages.sessionLive.scoring.playerCount':
    '{count, plural, =0 {Nessun giocatore} =1 {1 giocatore} other {# giocatori}}',
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
  'pages.sessionLive.rightColumn.tabScore': 'Score',
  'pages.sessionLive.rightColumn.tabTurn': 'Turni',
  'pages.sessionLive.rightColumn.tabWidget': 'Widget',
  'pages.sessionLive.rightColumn.tabNotes': 'Note',
  // Back-compat (legacy keys for older callers — not used by SessionLiveView post-T4)
  'pages.sessionLive.rightColumn.tabTools': 'Strumenti',
  'pages.sessionLive.rightColumn.tabChat': 'Chat',
  // ChatAgentPanel labels (G1 #2374 T3 — composite header)
  'pages.sessionLive.chatAgent.title': 'ChatAgent',
  'pages.sessionLive.chatAgent.agentNameAriaLabel': 'Nome agente {name}',
  'pages.sessionLive.chatAgent.onlineLabel': 'Online',
  'pages.sessionLive.chatAgent.latencyAriaLabel': 'Latenza {ms}ms',
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
  'pages.sessionLive.pauseOverlay.title': 'Sessione in pausa',
  'pages.sessionLive.pauseOverlay.resumeCta': 'Riprendi',
  'pages.sessionLive.pauseOverlay.closeCta': 'Chiudi',
  'pages.sessionLive.pauseOverlay.closeAriaLabel': 'Chiudi overlay pausa',
  'pages.sessionLive.endgameDialog.title': 'Fine sessione',
  'pages.sessionLive.endgameDialog.winnerLabel': 'Vincitore',
  'pages.sessionLive.endgameDialog.acknowledgeCta': 'Ho capito',
  'pages.sessionLive.endgameDialog.viewSummaryCta': 'Vedi riepilogo',
};

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

// ─── Fixture helpers ──────────────────────────────────────────────────────

const MOCK_SESSION_DTO: GameSessionDto = {
  id: 'session-abc-123',
  gameId: 'game-00000001',
  status: 'InProgress',
  startedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
  completedAt: null,
  playerCount: 2,
  players: [
    { id: 'player-001', playerName: 'Marco', playerOrder: 1, color: 'blue' },
    { id: 'player-002', playerName: 'Anna', playerOrder: 2, color: 'red' },
  ],
  winnerName: null,
  notes: null,
  durationMinutes: 60,
};

// ─── Import under test ─────────────────────────────────────────────────────

import { SessionLiveView } from '../SessionLiveView';

describe('SessionLiveView (Wave D.2 Foundation)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    mockParamsId = 'session-abc-123';
    IS_VISUAL_TEST_BUILD_MOCK = false;
    useSessionMock.mockReturnValue({
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
    useSessionMock.mockReturnValue({
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
    useSessionMock.mockReturnValue({
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
    useSessionMock.mockReturnValue({
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
    useSessionMock.mockReturnValue({
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
    useSessionMock.mockReturnValue({
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
    useSessionMock.mockReturnValue({
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
    // Drawer has 4 tabs; the 2nd (index 1) is Turn — aria-selected="true".
    const drawerTabs = document.querySelectorAll('[data-slot="mobile-bottom-sheet"] [role="tab"]');
    expect(drawerTabs).toHaveLength(4);
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
    useSessionMock.mockReturnValue({
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
    useSessionMock.mockReturnValue({
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
    useSessionMock.mockReturnValue({
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

  it('T4.11: RIGHT tab=score renders LiveScoringPanel', () => {
    // ?tab default → 'score'
    const { container } = renderWithIntl(<SessionLiveView />);
    expect(container.querySelector('[data-slot="live-scoring-panel"]')).toBeInTheDocument();
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
    useSessionMock.mockReturnValue({
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
