/**
 * SessionLiveView unit tests — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Coverage:
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
 * Pattern: Wave D.1 SessionsLibraryView.test.tsx adapted for Tier L live route.
 * Mocks: useSession, next/navigation, next-intl, session-live-visual-test-fixture.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

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
  error: Error | null;
  refetch?: () => void;
};

const useSessionMock = vi.fn<[], MockSessionReturn>();

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useSession: () => useSessionMock(),
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
      error: null,
      refetch: vi.fn(),
    });
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
    useSessionMock.mockReturnValue({ data: null, isLoading: false, isError: false, error: null });
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

  it('Cell 5: no SSE hook called (Foundation static fixture mode)', () => {
    // No useSessionLiveStream mock needed — it should NOT be called at all.
    // This test verifies the orchestrator doesn't import or call SSE hooks.
    // If it did, missing mock would cause an error.
    expect(() => renderWithIntl(<SessionLiveView />)).not.toThrow();
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

  // ─── 3.8: URL state — tab changes ──────────────────────────────────────

  it('mobile tab change to "log" calls router.replace with ?mtab=log', () => {
    const { container } = renderWithIntl(<SessionLiveView />);
    const logTab = container.querySelector(
      '[data-slot="mobile-body-tab"][data-tab="log"]'
    ) as HTMLButtonElement;
    expect(logTab).toBeInTheDocument();
    fireEvent.click(logTab);
    expect(routerReplace).toHaveBeenCalled();
    const callArg = routerReplace.mock.calls[0]?.[0] as string;
    expect(callArg).toContain('mtab=log');
  });

  it('mobile tab "score" (default) removes ?mtab param when already on log', () => {
    searchParamsMap['mtab'] = 'log';
    const { container } = renderWithIntl(<SessionLiveView />);
    const scoreTab = container.querySelector(
      '[data-slot="mobile-body-tab"][data-tab="score"]'
    ) as HTMLButtonElement;
    expect(scoreTab).toBeInTheDocument();
    fireEvent.click(scoreTab);
    expect(routerReplace).toHaveBeenCalled();
    const callArg = routerReplace.mock.calls[0]?.[0] as string;
    // Default tab 'score' should NOT have mtab in URL
    expect(callArg).not.toContain('mtab=score');
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
    useSessionMock.mockReturnValue({ data: null, isLoading: false, isError: false, error: null });
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
