/**
 * SessionSummaryView unit tests — Wave D.3 Tier M-L (Issue #756).
 *
 * Coverage (contract §1 §2 §3 §6):
 *   - 6-cell FSM rendering (loading | error | not-found | not-completed |
 *     default | partial)
 *   - Brownfield FORK redirect: InProgress / Paused / Setup → /sessions/[id]/live
 *   - URL state SSOT: ?diary= ?theme= reflected in active filter pill +
 *     theme toggle; handlers update URL via router.replace
 *   - Visual fixture override: ?fixture=tied renders tied podium
 *   - LiveSessionDto → SessionDetailsDto adapter at boundary (#4156: the route id is a
 *     LiveGameSession id; totals and per-round scores are the real ones from RoundScores)
 *   - Achievements stub: empty-achievements fixture renders empty carousel
 *   - Loading shell renders during pending state
 *   - Confetti shows on first load via shouldShowConfetti
 *
 * Mocks: useLiveSession, useSessionVisionSnapshots,
 *        next/navigation, react-intl (via react-intl IntlProvider).
 *
 * Pattern blueprint: Wave D.2 SessionLiveView.test.tsx (4-state FSM, fixture
 * mode toggling, IntlProvider with selective ICU plural messages).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import type { ReactElement } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { clearConfettiFlag } from '@/lib/sessions-summary/confetti-trigger';
import { sessionSummaryFixtures } from '@/lib/sessions-summary/visual-test-fixture';

// ─── next/navigation mocks ────────────────────────────────────────────────

const searchParamsMap: Record<string, string> = {};
const routerPush = vi.fn();
const routerReplace = vi.fn();
let mockParamsId: string | undefined = '00000000-0000-4000-8000-000000000aaa';

vi.mock('next/navigation', () => ({
  useParams: () => ({ id: mockParamsId }),
  useSearchParams: () => ({
    get: (key: string) => searchParamsMap[key] ?? null,
    toString: () => new URLSearchParams(searchParamsMap).toString(),
    [Symbol.iterator]: function* () {
      yield* Object.entries(searchParamsMap);
    },
  }),
  useRouter: () => ({ push: routerPush, replace: routerReplace }),
  usePathname: () => `/sessions/${mockParamsId}`,
}));

// ─── useLiveSession / useSessionSnapshots mocks ───────────────────────────

type SessionDetailReturn = {
  data?: LiveSessionDto | null;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  error: Error | null;
  refetch?: () => void;
};

const useLiveSessionMock = vi.fn<[], SessionDetailReturn>();
const useSessionVisionSnapshotsMock = vi.fn();

vi.mock('@/hooks/queries/useLiveSession', () => ({
  useLiveSession: () => useLiveSessionMock(),
}));

vi.mock('@/hooks/queries/useSessionSnapshots', () => ({
  useSessionVisionSnapshots: () => useSessionVisionSnapshotsMock(),
}));

// #3022: the flavor renderer is a lazy (next/dynamic) module tested on its own
// (CatanSummaryFlavor.test + SummaryFlavorRenderer.test). Here we mock it with a
// synchronous marker so this suite exercises the *wiring* (status/slug gate) without
// the dynamic-import loading race.
vi.mock('@/components/features/session-live/SummaryFlavorRenderer', () => ({
  hasSummaryFlavor: (slug: string | null | undefined) => slug === 'catan',
  SummaryFlavorRenderer: ({
    gameSlug,
    session,
  }: {
    gameSlug: string | null | undefined;
    session: { scoringType?: string | null; scoreData?: string | null; winnerName: string | null };
  }) => (
    <div
      data-testid="summary-flavor-mounted"
      data-slug={gameSlug ?? ''}
      data-scoring-type={session.scoringType ?? ''}
      data-score-data={session.scoreData ?? ''}
      data-winner={session.winnerName ?? ''}
    />
  ),
}));

// ─── visual-test-fixture mock (STATE_OVERRIDE_ENABLED stays true in test) ─

// In test env (NODE_ENV=test), STATE_OVERRIDE_ENABLED = true → ?fixture= active.
// We don't override IS_VISUAL_TEST_BUILD since the orchestrator only consults
// STATE_OVERRIDE_ENABLED via parseStateOverride(). The fixture path is taken
// solely when ?fixture= is set in searchParamsMap.

// ─── i18n messages (subset matching it.json pages.sessionSummary.*) ──────

const MESSAGES: Record<string, string> = {
  'pages.sessionSummary.hero.title': 'Riepilogo partita',
  'pages.sessionSummary.hero.podiumLabel': 'Podio finale',
  'pages.sessionSummary.hero.tiedBanner':
    '{count, plural, =2 {🤝 Pareggio tra {names}} other {🤝 Pareggio a {count} tra {names}}}',
  'pages.sessionSummary.hero.confettiAriaLabel': 'Animazione festeggiamento vittoria',
  'pages.sessionSummary.hero.confettiSkippedLabel': 'Medaglia vittoria',
  'pages.sessionSummary.kpi.duration': 'Durata',
  'pages.sessionSummary.kpi.players': 'Giocatori',
  'pages.sessionSummary.kpi.turns': 'Turni',
  'pages.sessionSummary.kpi.totalScore': 'Punteggio totale',
  'pages.sessionSummary.scoring.title': 'Punteggio dettagliato',
  'pages.sessionSummary.scoring.headerName': 'Giocatore',
  'pages.sessionSummary.scoring.headerScore': 'Punteggio',
  'pages.sessionSummary.scoring.headerRank': 'Posizione',
  'pages.sessionSummary.scoring.tied': 'Pari merito',
  'pages.sessionSummary.connectionBar.title': 'Andamento partita',
  'pages.sessionSummary.connectionBar.emptyEvent': 'Nessun evento registrato',
  'pages.sessionSummary.achievements.title': 'Achievements',
  'pages.sessionSummary.achievements.unlockedAt': 'Sbloccato il {date}',
  'pages.sessionSummary.achievements.locked': 'Ancora bloccato',
  'pages.sessionSummary.achievements.empty': 'Nessun achievement sbloccato in questa partita',
  'pages.sessionSummary.diary.title': 'Diario partita',
  'pages.sessionSummary.diary.filterAll': 'Tutti',
  'pages.sessionSummary.diary.filterScore': 'Punteggio',
  'pages.sessionSummary.diary.filterEvent': 'Eventi',
  'pages.sessionSummary.diary.filterChat': 'Chat',
  'pages.sessionSummary.diary.filterPhoto': 'Foto',
  'pages.sessionSummary.diary.empty': 'Nessun evento per questo filtro',
  'pages.sessionSummary.diary.expand': 'Espandi turno {turn}',
  'pages.sessionSummary.diary.collapse': 'Comprimi turno {turn}',
  'pages.sessionSummary.photos.title': 'Foto sessione',
  'pages.sessionSummary.photos.empty': 'Nessuna foto in questa partita',
  'pages.sessionSummary.photos.viewLarger': 'Apri foto',
  'pages.sessionSummary.chatHighlights.title': 'Momenti chat',
  'pages.sessionSummary.chatHighlights.empty': 'Nessun momento saliente registrato',
  'pages.sessionSummary.share.title': 'Condividi riepilogo',
  'pages.sessionSummary.share.previewLight': 'Tema chiaro',
  'pages.sessionSummary.share.previewDark': 'Tema scuro',
  'pages.sessionSummary.share.copyLink': 'Copia link',
  'pages.sessionSummary.share.shareTwitter': 'Condividi su Twitter',
  'pages.sessionSummary.share.shareInstagram': 'Condividi su Instagram',
  'pages.sessionSummary.share.shareWhatsApp': 'Condividi su WhatsApp',
  'pages.sessionSummary.share.downloadPng': 'Scarica PNG',
  'pages.sessionSummary.share.downloadPngDisabled': 'In arrivo prossimamente',
  'pages.sessionSummary.playAgain.title': 'Pronti per la rivincita?',
  'pages.sessionSummary.playAgain.cta': 'Riavvia con stessi giocatori',
  'pages.sessionSummary.playAgain.description':
    'Stesso gioco, stessi giocatori — agente già caricato',
  'pages.sessionSummary.states.notCompleted.title': 'Sessione in corso',
  'pages.sessionSummary.states.notCompleted.description': 'Questa sessione non è ancora terminata.',
  'pages.sessionSummary.states.notCompleted.continueLive': 'Vai alla sessione live',
  'pages.sessionSummary.states.notFound.title': 'Nessuna sessione attiva',
  'pages.sessionSummary.states.notFound.description':
    'La sessione richiesta non esiste o non è ancora stata creata.',
  'pages.sessionSummary.states.notFound.newSession': 'Inizia nuova sessione',
  'pages.sessionSummary.states.notFound.backToSessions': 'Torna alle sessioni',
  'common.errorTitle': 'Errore',
  'common.retry': 'Riprova',
  // #3022 Catan summary flavor
  'pages.sessionSummary.flavor.catan.winnerTemplate': '{name} vince!',
  'pages.sessionSummary.flavor.catan.vpUnit': 'PV',
  'pages.sessionSummary.flavor.catan.durationTemplate': '{minutes} min',
  'pages.sessionSummary.flavor.catan.standingsTitle': 'Classifica finale',
  'pages.sessionSummary.flavor.catan.empty': 'Riepilogo non disponibile',
};

// ─── Test renderer with IntlProvider ─────────────────────────────────────

import { SessionSummaryView } from '../SessionSummaryView';

function renderView(sessionId: string = '00000000-0000-4000-8000-000000000aaa'): ReactElement {
  return render(
    <IntlProvider locale="it" messages={MESSAGES} defaultLocale="it">
      <SessionSummaryView sessionId={sessionId} />
    </IntlProvider>
  );
}

// ─── Fixture helpers ──────────────────────────────────────────────────────

type LivePlayer = LiveSessionDto['players'][number];

function makePlayer(
  id: string,
  displayName: string,
  totalScore: number,
  overrides: Partial<LivePlayer> = {}
): LivePlayer {
  return {
    id,
    userId: null,
    displayName,
    avatarUrl: null,
    color: 'Red',
    role: 'Player',
    teamId: null,
    totalScore,
    currentRank: 0,
    joinedAt: '2026-05-04T18:55:00Z',
    isActive: true,
    ...overrides,
  };
}

const P1 = '00000000-0000-4000-8000-0000000000a1';
const P2 = '00000000-0000-4000-8000-0000000000a2';
const P3 = '00000000-0000-4000-8000-0000000000a3';
const P4 = '00000000-0000-4000-8000-0000000000a4';

function makeLiveSession(overrides: Partial<LiveSessionDto> = {}): LiveSessionDto {
  return {
    id: '00000000-0000-4000-8000-000000000aaa',
    sessionCode: 'ABC123',
    gameId: '00000000-0000-4000-8000-00000000c001',
    gameName: 'Azul',
    gameSlug: 'azul',
    createdByUserId: '00000000-0000-4000-8000-00000000f001',
    status: 'Completed',
    visibility: 'Private',
    groupId: null,
    createdAt: '2026-05-04T18:50:00Z',
    startedAt: '2026-05-04T19:00:00Z',
    pausedAt: null,
    completedAt: '2026-05-04T20:30:00Z',
    updatedAt: '2026-05-04T20:30:00Z',
    lastSavedAt: null,
    currentTurnIndex: 12,
    currentTurnPlayerId: null,
    agentMode: 'None',
    notes: null,
    players: [
      makePlayer(P1, 'Marco Rossi', 40, { role: 'Host' }),
      makePlayer(P2, 'Anna Bianchi', 25),
      makePlayer(P3, 'Luigi Verdi', 18),
      makePlayer(P4, 'Sofia Russo', 12),
    ],
    teams: [],
    roundScores: [],
    scoringConfig: { enabledDimensions: ['punti'], dimensionUnits: {} },
    ...overrides,
  };
}

function setHookSuccess(session: LiveSessionDto = makeLiveSession()): void {
  useLiveSessionMock.mockReturnValue({
    data: session,
    isLoading: false,
    isError: false,
    isSuccess: true,
    error: null,
    refetch: vi.fn(),
  });
  useSessionVisionSnapshotsMock.mockReturnValue({
    data: [],
    isLoading: false,
    isError: false,
    isSuccess: true,
    error: null,
  });
}

// ─── Setup ────────────────────────────────────────────────────────────────

beforeEach(() => {
  // Reset URL state
  Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
  // Reset router spies
  routerPush.mockClear();
  routerReplace.mockClear();
  // Reset hook mocks
  useLiveSessionMock.mockReset();
  useSessionVisionSnapshotsMock.mockReset();
  // Default: not loading, no data, not error
  useLiveSessionMock.mockReturnValue({
    data: null,
    isLoading: false,
    isError: false,
    isSuccess: false,
    error: null,
    refetch: vi.fn(),
  });
  useSessionVisionSnapshotsMock.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    isSuccess: false,
    error: null,
  });
  mockParamsId = '00000000-0000-4000-8000-000000000aaa';
  // Confetti flag reset to allow first-load check
  clearConfettiFlag('00000000-0000-4000-8000-000000000aaa');
  clearConfettiFlag('00000000-0000-4000-8000-000000000756');
});

// ─── FSM cell tests ───────────────────────────────────────────────────────

describe('SessionSummaryView — 6-cell FSM', () => {
  it('renders loading shell when sessionQuery is pending', () => {
    useLiveSessionMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      isSuccess: false,
      error: null,
      refetch: vi.fn(),
    });
    renderView();
    expect(screen.getByRole('status', { name: 'Riepilogo partita' })).toBeInTheDocument();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'loading');
  });

  it('renders error shell when sessionQuery errored', () => {
    useLiveSessionMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: true,
      isSuccess: false,
      error: new Error('Network 500'),
      refetch: vi.fn(),
    });
    renderView();
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Network 500')).toBeInTheDocument();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'error');
  });

  it('renders not-found shell when session data is null', () => {
    useLiveSessionMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionVisionSnapshotsMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    renderView();
    // Issue #2088: title renamed "Sessione non trovata" → "Nessuna sessione attiva"
    expect(screen.getByText('Nessuna sessione attiva')).toBeInTheDocument();
    expect(
      screen.getByText('La sessione richiesta non esiste o non è ancora stata creata.')
    ).toBeInTheDocument();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'not-found');
  });

  it('not-found shell exposes "Inizia nuova sessione" CTA → /sessions/new (Issue #2088)', () => {
    useLiveSessionMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionVisionSnapshotsMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    renderView();

    const newSessionCta = screen.getByRole('button', { name: /inizia nuova sessione/i });
    expect(newSessionCta).toBeInTheDocument();
    fireEvent.click(newSessionCta);
    expect(routerPush).toHaveBeenCalledWith('/sessions/new');
  });

  it('renders not-completed shell + redirect to /live for InProgress status', () => {
    setHookSuccess(makeLiveSession({ status: 'InProgress' }));
    renderView();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'not-completed');
    expect(routerReplace).toHaveBeenCalledWith(
      '/sessions/00000000-0000-4000-8000-000000000aaa/live'
    );
  });

  it('renders not-completed shell + redirect for Paused status', () => {
    setHookSuccess(makeLiveSession({ status: 'Paused' }));
    renderView();
    expect(routerReplace).toHaveBeenCalledWith(
      '/sessions/00000000-0000-4000-8000-000000000aaa/live'
    );
  });

  it('redirects to /live a session that was created and never started (#4156)', () => {
    setHookSuccess(makeLiveSession({ status: 'Created', startedAt: null, completedAt: null }));
    renderView();
    expect(routerReplace).toHaveBeenCalledWith(
      '/sessions/00000000-0000-4000-8000-000000000aaa/live'
    );
  });

  it('renders the summary of a completed session, partial on real data (#4156)', () => {
    // Sui dati reali non ci sono né diario né achievement, quindi la cella è «partial»: la
    // cella «default» si raggiunge solo con le fixture (vedi i test sotto).
    setHookSuccess(makeLiveSession({ status: 'Completed' }));
    useSessionVisionSnapshotsMock.mockReturnValue({
      data: [
        {
          id: 's1',
          sessionId: '00000000-0000-4000-8000-000000000aaa',
          turnNumber: 1,
          caption: 'Setup',
          hasGameState: true,
          createdAt: '2026-05-04T19:00:00Z',
          images: [],
        },
      ],
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
    renderView();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'partial');
    // Hero podium should render
    expect(document.querySelector('[data-slot="session-summary-hero"]')).toBeInTheDocument();
    // ScoringBreakdownTable should render
    expect(document.querySelector('[data-slot="scoring-breakdown-table"]')).toBeInTheDocument();
  });

  it('renders partial shell when sub-arrays empty (default fallback for diary/photos/chat)', () => {
    setHookSuccess(makeLiveSession({ status: 'Completed' }));
    // Default mock has empty diary/snapshots → partial cell
    renderView();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'partial');
    // Hero still renders
    expect(document.querySelector('[data-slot="session-summary-hero"]')).toBeInTheDocument();
  });
});

// ─── URL state SSOT tests ────────────────────────────────────────────────

describe('SessionSummaryView — URL state SSOT', () => {
  it('reads ?diary=score from URL and reflects active filter', () => {
    setHookSuccess();
    searchParamsMap['diary'] = 'score';
    renderView();
    const scorePill = document.querySelector(
      '[data-slot="diary-filter-pill"][data-filter="score"]'
    );
    expect(scorePill).toHaveAttribute('data-active', 'true');
  });

  it('reads ?theme=dark from URL and reflects active radio', () => {
    setHookSuccess();
    searchParamsMap['theme'] = 'dark';
    renderView();
    const darkOption = document.querySelector(
      '[data-slot="share-theme-option"][data-theme-option="dark"]'
    );
    expect(darkOption).toHaveAttribute('aria-checked', 'true');
  });

  it('clicking a different diary filter pill calls router.replace with new ?diary=', () => {
    setHookSuccess();
    renderView();
    const eventPill = document.querySelector(
      '[data-slot="diary-filter-pill"][data-filter="event"]'
    ) as HTMLButtonElement;
    fireEvent.click(eventPill);
    expect(routerReplace).toHaveBeenCalledWith(
      expect.stringContaining('?diary=event'),
      expect.objectContaining({ scroll: false })
    );
  });

  it('clicking dark theme option calls router.replace with ?theme=dark', () => {
    setHookSuccess();
    renderView();
    const darkOption = document.querySelector(
      '[data-slot="share-theme-option"][data-theme-option="dark"]'
    ) as HTMLButtonElement;
    fireEvent.click(darkOption);
    expect(routerReplace).toHaveBeenCalledWith(
      expect.stringContaining('?theme=dark'),
      expect.objectContaining({ scroll: false })
    );
  });
});

// ─── Visual fixture override tests ───────────────────────────────────────

describe('SessionSummaryView — visual fixture override', () => {
  it('?fixture=tied renders tied podium without calling hooks', () => {
    searchParamsMap['fixture'] = 'tied';
    // Hooks should NOT be consulted; we don't set up data on them.
    renderView();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    // Tied fixture has 2-way tie at rank 1 with non-empty diary/snapshots/achievements
    expect(root?.getAttribute('data-ui-state')).toMatch(/^(default|partial)$/);
    // Tied banner from hero should render
    expect(document.querySelector('[data-slot="hero-tied-banner"]')).toBeInTheDocument();
  });

  it('?fixture=empty-achievements renders empty carousel', () => {
    searchParamsMap['fixture'] = 'empty-achievements';
    renderView();
    const carousel = document.querySelector('[data-slot="achievements-carousel"]');
    expect(carousel).toHaveAttribute('data-empty', 'true');
  });

  it('?fixture=empty-photos renders empty gallery', () => {
    searchParamsMap['fixture'] = 'empty-photos';
    renderView();
    const gallery = document.querySelector('[data-slot="photos-gallery"]');
    expect(gallery).toHaveAttribute('data-empty', 'true');
  });

  it('?fixture=abandoned renders not-completed cell (Abandoned still qualifies as terminal — should render summary)', () => {
    searchParamsMap['fixture'] = 'abandoned';
    renderView();
    // Abandoned status DOES qualify for default/partial per fsm.ts COMPLETED_STATUSES.
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root?.getAttribute('data-ui-state')).toMatch(/^(default|partial)$/);
  });

  it('?fixture=solo renders single-participant podium', () => {
    searchParamsMap['fixture'] = 'solo';
    renderView();
    expect(document.querySelector('[data-slot="podium-solo"]')).toBeInTheDocument();
  });
});

// ─── Adapter tests ────────────────────────────────────────────────────────

describe('SessionSummaryView — LiveSessionDto → SessionDetailsDto adapter (#4156)', () => {
  it('ranks the players by the totals the live session recorded', () => {
    // Lo scenario della spec: Marco segna 10, Giulia 30. Prima il riepilogo leggeva la
    // GameSession e dava 1 punto a chi aveva il nome del vincitore, 0 agli altri.
    setHookSuccess(
      makeLiveSession({
        players: [makePlayer(P1, 'Marco', 10, { role: 'Host' }), makePlayer(P2, 'Giulia', 30)],
      })
    );
    renderView();
    const first = document.querySelector('[data-slot="scoring-row"][data-rank="1"]');
    const second = document.querySelector('[data-slot="scoring-row"][data-rank="2"]');
    expect(first?.textContent).toContain('Giulia');
    expect(first?.textContent).toContain('30');
    expect(second?.textContent).toContain('Marco');
    expect(second?.textContent).toContain('10');
  });

  it('leaves spectators off the scoreboard', () => {
    setHookSuccess(
      makeLiveSession({
        players: [
          makePlayer(P1, 'Marco', 10, { role: 'Host' }),
          makePlayer(P2, 'Giulia', 30),
          makePlayer(P3, 'Chi guarda', 0, { role: 'Spectator' }),
        ],
      })
    );
    renderView();
    const rows = document.querySelectorAll('[data-slot="scoring-row"]');
    expect(rows).toHaveLength(2);
    expect(
      document.querySelector('[data-slot="scoring-breakdown-table"]')?.textContent
    ).not.toContain('Chi guarda');
  });

  it('shows no test achievements on real data', () => {
    // Fino a #4156 il ramo dei dati reali mostrava gli achievement della fixture di default,
    // come se la partita li avesse sbloccati.
    setHookSuccess(makeLiveSession());
    renderView();
    expect(document.querySelector('[data-slot="achievements-carousel"]')).toHaveAttribute(
      'data-empty',
      'true'
    );
  });
});

// ─── SessionId guard ──────────────────────────────────────────────────────

describe('SessionSummaryView — sessionId guard', () => {
  it('renders not-found when sessionId prop is empty AND useParams id is also empty', () => {
    mockParamsId = undefined;
    // Hook will be called with '' — set isSuccess=true with null data so the
    // FSM resolves to not-found via the empty-id branch.
    useLiveSessionMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionVisionSnapshotsMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    renderView('');
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'not-found');
  });
});

// ─── Confetti tests ───────────────────────────────────────────────────────

describe('SessionSummaryView — confetti first-load', () => {
  it('renders confetti elements on first mount when status=Completed', () => {
    searchParamsMap['fixture'] = 'default';
    // Use the visual sentinel id — fixture default has status=Completed.
    renderView('00000000-0000-4000-8000-000000000756');
    // Confetti is mounted inside Hero — look for the confetti slot.
    expect(document.querySelector('[data-slot="confetti"]')).toBeInTheDocument();
  });
});

// ─── Fixture data sanity ──────────────────────────────────────────────────

describe('SessionSummaryView — fixture data sanity', () => {
  it('exposes 6 fixture variants from foundation', () => {
    expect(Object.keys(sessionSummaryFixtures)).toHaveLength(6);
    expect(sessionSummaryFixtures.default.session.status).toBe('Completed');
    expect(
      sessionSummaryFixtures.tied.session.participants.filter(p => p.totalScore === 42)
    ).toHaveLength(2);
  });
});

// ─── Handler tests ────────────────────────────────────────────────────────

describe('SessionSummaryView — handlers', () => {
  it('error retry triggers useSessionDetail.refetch', () => {
    const refetchSpy = vi.fn();
    useLiveSessionMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: true,
      isSuccess: false,
      error: new Error('fail'),
      refetch: refetchSpy,
    });
    renderView();
    fireEvent.click(screen.getByText('Riprova'));
    expect(refetchSpy).toHaveBeenCalled();
  });

  it('not-found back button navigates to /sessions', () => {
    useLiveSessionMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionVisionSnapshotsMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    renderView();
    fireEvent.click(screen.getByText('Torna alle sessioni'));
    expect(routerPush).toHaveBeenCalledWith('/sessions');
  });
});

// ─── #3022 Catan summary flavor wiring ─────────────────────────────────────

describe('SessionSummaryView — Catan summary flavor (#3022)', () => {
  const catanSession = (overrides: Partial<LiveSessionDto> = {}): LiveSessionDto =>
    makeLiveSession({
      gameName: 'Catan',
      gameSlug: 'catan',
      players: [
        makePlayer(P1, 'Marco Rossi', 10, { role: 'Host', color: 'Red' }),
        makePlayer(P2, 'Anna Bianchi', 8, { color: 'Blue' }),
      ],
      ...overrides,
    });

  it('hands the flavor the points of the live totals (#4156)', () => {
    setHookSuccess(catanSession());
    renderView();
    const mounted = screen.getByTestId('summary-flavor-mounted');
    expect(mounted).toHaveAttribute('data-scoring-type', 'Points');
    expect(JSON.parse(mounted.getAttribute('data-score-data') ?? '{}')).toEqual({
      scores: [
        { playerId: P1, points: 10 },
        { playerId: P2, points: 8 },
      ],
    });
    expect(mounted).toHaveAttribute('data-winner', 'Marco Rossi');
  });

  it('mounts the summary flavor for a completed Catan session, forwarding the slug', () => {
    setHookSuccess(catanSession());
    renderView();
    const mounted = screen.getByTestId('summary-flavor-mounted');
    expect(mounted).toBeInTheDocument();
    expect(mounted).toHaveAttribute('data-slug', 'catan');
  });

  it('does NOT mount for a non-Catan game', () => {
    setHookSuccess(catanSession({ gameSlug: 'wingspan' }));
    renderView();
    expect(screen.queryByTestId('summary-flavor-mounted')).toBeNull();
  });

  it('does NOT mount for a non-completed session', () => {
    setHookSuccess(catanSession({ status: 'Paused' }));
    renderView();
    expect(screen.queryByTestId('summary-flavor-mounted')).toBeNull();
  });
});
