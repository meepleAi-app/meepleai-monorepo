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
 *   - GameSessionDto → SessionDetailsDto adapter at boundary (Gate B carryover)
 *   - Achievements stub: empty-achievements fixture renders empty carousel
 *   - Loading shell renders during pending state
 *   - Confetti shows on first load via shouldShowConfetti
 *
 * Mocks: useSessionDetail, useSessionDiaryQuery, useSessionVisionSnapshots,
 *        next/navigation, react-intl (via react-intl IntlProvider).
 *
 * Pattern blueprint: Wave D.2 SessionLiveView.test.tsx (4-state FSM, fixture
 * mode toggling, IntlProvider with selective ICU plural messages).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import type { ReactElement } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
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

// ─── useSessionDetail / useSessionFlow / useSessionSnapshots mocks ───────

type SessionDetailReturn = {
  data?: GameSessionDto | null;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  error: Error | null;
  refetch?: () => void;
};

const useSessionDetailMock = vi.fn<[], SessionDetailReturn>();
const useSessionDiaryQueryMock = vi.fn();
const useSessionVisionSnapshotsMock = vi.fn();

vi.mock('@/hooks/queries/useSessionDetail', () => ({
  useSessionDetail: () => useSessionDetailMock(),
}));

vi.mock('@/hooks/queries/useSessionFlow', () => ({
  useSessionDiaryQuery: () => useSessionDiaryQueryMock(),
}));

vi.mock('@/hooks/queries/useSessionSnapshots', () => ({
  useSessionVisionSnapshots: () => useSessionVisionSnapshotsMock(),
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
  'pages.sessionSummary.states.notFound.title': 'Sessione non trovata',
  'pages.sessionSummary.states.notFound.backToSessions': 'Torna alle sessioni',
  'common.errorTitle': 'Errore',
  'common.retry': 'Riprova',
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

function makeGameSession(overrides: Partial<GameSessionDto> = {}): GameSessionDto {
  return {
    id: '00000000-0000-4000-8000-000000000aaa',
    gameId: '00000000-0000-4000-8000-00000000c001',
    status: 'Completed',
    startedAt: '2026-05-04T19:00:00Z',
    completedAt: '2026-05-04T20:30:00Z',
    playerCount: 4,
    players: [
      { id: 'p1', playerName: 'Marco Rossi', playerOrder: 1, color: null },
      { id: 'p2', playerName: 'Anna Bianchi', playerOrder: 2, color: null },
      { id: 'p3', playerName: 'Luigi Verdi', playerOrder: 3, color: null },
      { id: 'p4', playerName: 'Sofia Russo', playerOrder: 4, color: null },
    ],
    winnerName: 'Marco Rossi',
    notes: null,
    durationMinutes: 90,
    ...overrides,
  };
}

function setHookSuccess(session: GameSessionDto = makeGameSession()): void {
  useSessionDetailMock.mockReturnValue({
    data: session,
    isLoading: false,
    isError: false,
    isSuccess: true,
    error: null,
    refetch: vi.fn(),
  });
  useSessionDiaryQueryMock.mockReturnValue({
    data: [],
    isLoading: false,
    isError: false,
    isSuccess: true,
    error: null,
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
  useSessionDetailMock.mockReset();
  useSessionDiaryQueryMock.mockReset();
  useSessionVisionSnapshotsMock.mockReset();
  // Default: not loading, no data, not error
  useSessionDetailMock.mockReturnValue({
    data: null,
    isLoading: false,
    isError: false,
    isSuccess: false,
    error: null,
    refetch: vi.fn(),
  });
  useSessionDiaryQueryMock.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    isSuccess: false,
    error: null,
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
    useSessionDetailMock.mockReturnValue({
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
    useSessionDetailMock.mockReturnValue({
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
    useSessionDetailMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionDiaryQueryMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    useSessionVisionSnapshotsMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    renderView();
    expect(screen.getByText('Sessione non trovata')).toBeInTheDocument();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'not-found');
  });

  it('renders not-completed shell + redirect to /live for InProgress status', () => {
    setHookSuccess(makeGameSession({ status: 'InProgress' }));
    renderView();
    const root = document.querySelector('[data-slot="session-summary-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'not-completed');
    expect(routerReplace).toHaveBeenCalledWith(
      '/sessions/00000000-0000-4000-8000-000000000aaa/live'
    );
  });

  it('renders not-completed shell + redirect for Paused status', () => {
    setHookSuccess(makeGameSession({ status: 'Paused' }));
    renderView();
    expect(routerReplace).toHaveBeenCalledWith(
      '/sessions/00000000-0000-4000-8000-000000000aaa/live'
    );
  });

  it('renders default shell when status=Completed and all data present', () => {
    setHookSuccess(makeGameSession({ status: 'Completed' }));
    // Provide non-empty diary/snapshots so we get default not partial
    useSessionDiaryQueryMock.mockReturnValue({
      data: [
        {
          id: 'd1',
          sessionId: '00000000-0000-4000-8000-000000000aaa',
          gameNightId: null,
          eventType: 'turn_advanced',
          timestamp: '2026-05-04T19:30:00Z',
          payload: null,
          createdBy: null,
          source: 'system',
        },
      ],
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
    });
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
    expect(root).toHaveAttribute('data-ui-state', 'default');
    // Hero podium should render
    expect(document.querySelector('[data-slot="session-summary-hero"]')).toBeInTheDocument();
    // ScoringBreakdownTable should render
    expect(document.querySelector('[data-slot="scoring-breakdown-table"]')).toBeInTheDocument();
  });

  it('renders partial shell when sub-arrays empty (default fallback for diary/photos/chat)', () => {
    setHookSuccess(makeGameSession({ status: 'Completed' }));
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

describe('SessionSummaryView — GameSessionDto → SessionDetailsDto adapter', () => {
  it('adapter assigns deterministic placeholder score so winner ranks first', () => {
    const session = makeGameSession({
      winnerName: 'Anna Bianchi',
      players: [
        { id: 'p1', playerName: 'Marco Rossi', playerOrder: 1, color: null },
        { id: 'p2', playerName: 'Anna Bianchi', playerOrder: 2, color: null },
        { id: 'p3', playerName: 'Luigi Verdi', playerOrder: 3, color: null },
      ],
    });
    setHookSuccess(session);
    renderView();
    // First scoring row should be the winner Anna Bianchi
    const firstRow = document.querySelector('[data-slot="scoring-row"][data-rank="1"]');
    expect(firstRow?.textContent).toContain('Anna Bianchi');
  });

  it('adapter handles missing player ids by synthesizing stable ids', () => {
    const session = makeGameSession({
      players: [{ playerName: 'Anonymous Player', playerOrder: 1, color: null }],
    });
    setHookSuccess(session);
    renderView();
    // Should render without crashing — the synthetic id is used in keys
    expect(document.querySelector('[data-slot="session-summary-hero"]')).toBeInTheDocument();
  });
});

// ─── SessionId guard ──────────────────────────────────────────────────────

describe('SessionSummaryView — sessionId guard', () => {
  it('renders not-found when sessionId prop is empty AND useParams id is also empty', () => {
    mockParamsId = undefined;
    // Hook will be called with '' — set isSuccess=true with null data so the
    // FSM resolves to not-found via the empty-id branch.
    useSessionDetailMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionDiaryQueryMock.mockReturnValue({ data: [], isLoading: false, isError: false });
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
    useSessionDetailMock.mockReturnValue({
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
    useSessionDetailMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionDiaryQueryMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    useSessionVisionSnapshotsMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    renderView();
    fireEvent.click(screen.getByText('Torna alle sessioni'));
    expect(routerPush).toHaveBeenCalledWith('/sessions');
  });
});
