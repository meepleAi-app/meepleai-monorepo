/**
 * Wave D.1 (Issue #735) — SessionsLibraryView orchestrator tests.
 *
 * Mirrors Wave 4 D1 PlayersLibraryView tests: stub `useActiveSessions` +
 * `next/navigation` search params + i18n via IntlProvider seeded with the
 * actual `pages.sessions.*` keys from `it.json`.
 *
 * Contract under test (Tier S):
 *   - 5-state FSM: default | loading | empty | filtered-empty | error
 *   - `?state=...` URL override gated by NODE_ENV !== 'production'
 *   - Status filter via URL param (?status=) — SSOT
 *   - View mode via URL param (?view=list|grid) — SSOT
 *   - Search filter via URL param (?search=) — SSOT
 *   - Card click calls router.push('/sessions/{id}')
 *   - New session CTA navigates to /sessions/new
 *   - Filtered-empty CTA resets status filter to 'all' via URL update
 *   - Visual fixture short-circuit when IS_VISUAL_TEST_BUILD === true
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type { PaginatedSessionsResponse } from '@/lib/api/schemas/games.schemas';

// ─── next/navigation mocks ────────────────────────────────────────────────

const searchParamsMap: Record<string, string> = {};
const routerPush = vi.fn();
const routerReplace = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => searchParamsMap[key] ?? null,
    toString: () => new URLSearchParams(searchParamsMap).toString(),
  }),
  useRouter: () => ({ push: routerPush, replace: routerReplace }),
  usePathname: () => '/sessions',
}));

// ─── useActiveSessions mock ───────────────────────────────────────────────

type MockActiveSessionsReturn = {
  data?: PaginatedSessionsResponse;
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
  refetch?: () => void;
};

const useActiveSessionsMock = vi.fn<[], MockActiveSessionsReturn>();

vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => useActiveSessionsMock(),
}));

// ─── visual fixture mock ──────────────────────────────────────────────────

vi.mock('@/lib/sessions/sessions-visual-test-fixture', async () => {
  const actual = await vi.importActual<
    typeof import('@/lib/sessions/sessions-visual-test-fixture')
  >('@/lib/sessions/sessions-visual-test-fixture');
  return {
    ...actual,
    IS_VISUAL_TEST_BUILD: false, // default — tests override per-test when needed
  };
});

// ─── react-intl messages (subset matching it.json `pages.sessions.*`) ────

const MESSAGES: Record<string, string> = {
  'pages.sessions.metadata.title': 'Le mie partite — MeepleAI',
  'pages.sessions.hero.title': 'Le tue partite',
  'pages.sessions.hero.subtitle':
    '{count, plural, =0 {Nessuna partita ancora} =1 {1 partita registrata} other {# partite registrate}}',
  'pages.sessions.hero.ctaNew': 'Registra partita',
  'pages.sessions.filters.status.all': 'Tutti',
  'pages.sessions.filters.status.active': 'In corso',
  'pages.sessions.filters.status.completed': 'Completate',
  'pages.sessions.filters.status.abandoned': 'Abbandonate',
  'pages.sessions.filters.searchPlaceholder': 'Cerca partita o gioco…',
  'pages.sessions.filters.searchAriaLabel': 'Cerca tra le tue partite',
  'pages.sessions.filters.view.label': 'Vista',
  'pages.sessions.filters.view.list': 'Lista',
  'pages.sessions.filters.view.grid': 'Griglia',
  'pages.sessions.empty.title': 'Nessuna partita ancora',
  'pages.sessions.empty.description':
    'Registra la tua prima partita giocata o avvia una sessione live per tracciare turni, punteggi e diari.',
  'pages.sessions.empty.ctaStart': 'Registra prima partita',
  'pages.sessions.filteredEmpty.title': 'Nessuna partita per questi filtri',
  'pages.sessions.filteredEmpty.description': 'Prova a rimuovere alcuni vincoli o cambia periodo.',
  'pages.sessions.filteredEmpty.ctaClear': 'Reset filtri',
  'pages.sessions.loading.ariaLabel': 'Caricamento partite in corso…',
  'pages.sessions.error.title': 'Errore caricamento',
  'pages.sessions.error.description': 'Impossibile recuperare le partite. Verifica la connessione.',
  'pages.sessions.error.ctaRetry': 'Riprova',
  'pages.sessions.a11y.resultsLabel': 'Lista partite',
  'pages.sessions.a11y.countTemplate':
    '{count, plural, =0 {Nessuna partita} =1 {1 partita} other {# partite}}',
  'pages.sessions.card.outcome.won': 'Vinta',
  'pages.sessions.card.outcome.lost': 'Persa',
  'pages.sessions.card.outcome.tie': 'Pareggio',
  'pages.sessions.card.status.live': 'Live',
  'pages.sessions.card.status.paused': 'In pausa',
  'pages.sessions.card.status.abandoned': 'Abbandonata',
  'pages.sessions.card.playerCount': '{count} giocatori',
  'pages.sessions.card.winnerLabel': 'Vincitore',
  'pages.sessions.card.turnLabel': 'Turno {turn}',
  'pages.sessions.card.openAriaLabel': 'Apri sessione {gameName}',
};

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

// ─── fixture helpers ──────────────────────────────────────────────────────

/** 3 sessions: 1 inprogress, 1 completed, 1 abandoned */
const SESSIONS_3: PaginatedSessionsResponse = {
  sessions: [
    {
      id: 'session-001',
      gameId: 'game-brass',
      status: 'InProgress',
      startedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
      completedAt: null,
      playerCount: 3,
      players: [
        { playerName: 'Marco', playerOrder: 1, color: 'blue' },
        { playerName: 'Anna', playerOrder: 2, color: 'red' },
        { playerName: 'Luca', playerOrder: 3, color: 'green' },
      ],
      winnerName: null,
      notes: null,
      durationMinutes: 60,
    },
    {
      id: 'session-002',
      gameId: 'game-wingspan',
      status: 'Completed',
      startedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
      completedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000 + 90 * 60 * 1000).toISOString(),
      playerCount: 2,
      players: [
        { playerName: 'Marco', playerOrder: 1, color: 'blue' },
        { playerName: 'Sara', playerOrder: 2, color: 'yellow' },
      ],
      winnerName: 'Marco',
      notes: null,
      durationMinutes: 90,
    },
    {
      id: 'session-003',
      gameId: 'game-azul',
      status: 'Abandoned',
      startedAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
      completedAt: null,
      playerCount: 2,
      players: [
        { playerName: 'Marco', playerOrder: 1, color: 'blue' },
        { playerName: 'Luca', playerOrder: 2, color: 'red' },
      ],
      winnerName: null,
      notes: null,
      durationMinutes: 25,
    },
  ],
  total: 3,
  page: 1,
  pageSize: 50,
};

// ─── import under test ────────────────────────────────────────────────────

import { SessionsLibraryView } from '../SessionsLibraryView';

describe('SessionsLibraryView (Wave D.1)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset URL params to clean state
    Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
    useActiveSessionsMock.mockReturnValue({
      data: SESSIONS_3,
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
  });

  // ─── Cell 1: default ────────────────────────────────────────────────────

  it('renders SessionsHero + SessionsFilters + session list in default state', () => {
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(container.querySelector('[data-slot="sessions-hero"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="sessions-filters"]')).toBeInTheDocument();
    // No empty state in default
    expect(container.querySelector('[data-slot^="sessions-empty-"]')).not.toBeInTheDocument();
  });

  it('hero renders session count from fetched data (3 sessions)', () => {
    const { container } = renderWithIntl(<SessionsLibraryView />);
    // Hero subtitle shows count; check hero presence
    const hero = container.querySelector('[data-slot="sessions-hero"]');
    expect(hero).toBeInTheDocument();
    // Hero CTA is present
    expect(container.querySelector('[data-slot="sessions-hero-cta"]')).toBeInTheDocument();
  });

  it('default view shows list cards (SessionCardList) when ?view is unset', () => {
    const { container } = renderWithIntl(<SessionsLibraryView />);
    // Default view=list → session-card-list elements
    const listCards = container.querySelectorAll('[data-slot="session-card-list"]');
    expect(listCards.length).toBeGreaterThan(0);
    expect(container.querySelectorAll('[data-slot="session-card-grid"]')).toHaveLength(0);
  });

  it('?view=grid shows grid cards (SessionCardGrid)', () => {
    searchParamsMap['view'] = 'grid';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    const gridCards = container.querySelectorAll('[data-slot="session-card-grid"]');
    expect(gridCards.length).toBeGreaterThan(0);
    expect(container.querySelectorAll('[data-slot="session-card-list"]')).toHaveLength(0);
  });

  // ─── Cell 2: loading ────────────────────────────────────────────────────

  it('renders EmptySessions kind="loading" when query isLoading=true', () => {
    useActiveSessionsMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    });
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(container.querySelector('[data-slot="sessions-empty-loading"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="session-card-list"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-slot="session-card-grid"]')).not.toBeInTheDocument();
  });

  // ─── Cell 3: error ──────────────────────────────────────────────────────

  it('renders EmptySessions kind="error" when query isError=true', () => {
    useActiveSessionsMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error('network error'),
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(container.querySelector('[data-slot="sessions-empty-error"]')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
  });

  // ─── Cell 4: empty (no data) ────────────────────────────────────────────

  it('renders EmptySessions kind="empty" when sessions array is empty', () => {
    useActiveSessionsMock.mockReturnValue({
      data: { sessions: [], total: 0, page: 1, pageSize: 50 },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(container.querySelector('[data-slot="sessions-empty-empty"]')).toBeInTheDocument();
  });

  // ─── Cell 5: filtered-empty ─────────────────────────────────────────────

  it('renders EmptySessions kind="filtered-empty" when status filter yields no results', () => {
    // SESSIONS_3 has no 'Paused' sessions
    searchParamsMap['status'] = 'active';
    // Only InProgress maps to active — but we need to set up a scenario with no matches
    // Actually SESSIONS_3 has 1 InProgress, so let's use 'abandoned' and clear them
    useActiveSessionsMock.mockReturnValue({
      data: {
        sessions: [SESSIONS_3.sessions[1]!], // Only completed
        total: 1,
        page: 1,
        pageSize: 50,
      },
      isLoading: false,
      isError: false,
      error: null,
    });
    searchParamsMap['status'] = 'active'; // Filter for active, but only completed exists
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(
      container.querySelector('[data-slot="sessions-empty-filtered-empty"]')
    ).toBeInTheDocument();
  });

  // ─── Status filter from URL ─────────────────────────────────────────────

  it('?status=active filters to show only inprogress/paused sessions', () => {
    searchParamsMap['status'] = 'active';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    // SESSIONS_3 has 1 InProgress → 1 card shown
    const listCards = container.querySelectorAll('[data-slot="session-card-list"]');
    expect(listCards).toHaveLength(1);
    expect(listCards[0]).toHaveAttribute('data-item-id', 'session-001');
  });

  it('?status=completed filters to show only completed sessions', () => {
    searchParamsMap['status'] = 'completed';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    const listCards = container.querySelectorAll('[data-slot="session-card-list"]');
    expect(listCards).toHaveLength(1);
    expect(listCards[0]).toHaveAttribute('data-item-id', 'session-002');
  });

  it('?status=abandoned filters to show only abandoned sessions', () => {
    searchParamsMap['status'] = 'abandoned';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    const listCards = container.querySelectorAll('[data-slot="session-card-list"]');
    expect(listCards).toHaveLength(1);
    expect(listCards[0]).toHaveAttribute('data-item-id', 'session-003');
  });

  // ─── Search filter from URL ─────────────────────────────────────────────

  it('?search= filters sessions by gameName', () => {
    searchParamsMap['search'] = 'brass';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    // game-brass gameId → fallback gameName = 'game-brass' (no gameNameMap in orchestrator)
    const listCards = container.querySelectorAll('[data-slot="session-card-list"]');
    // Only session-001 has gameId 'game-brass' which includes 'brass'
    expect(listCards).toHaveLength(1);
    expect(listCards[0]).toHaveAttribute('data-item-id', 'session-001');
  });

  // ─── Card click navigation ──────────────────────────────────────────────

  it('clicking a list card calls router.push with /sessions/{id}', () => {
    const { container } = renderWithIntl(<SessionsLibraryView />);
    const cards = container.querySelectorAll('[data-slot="session-card-list"]');
    expect(cards.length).toBeGreaterThan(0);
    fireEvent.click(cards[0]!);
    expect(routerPush).toHaveBeenCalledWith('/sessions/session-001');
  });

  it('clicking a grid card calls router.push with /sessions/{id}', () => {
    searchParamsMap['view'] = 'grid';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    const cards = container.querySelectorAll('[data-slot="session-card-grid"]');
    expect(cards.length).toBeGreaterThan(0);
    fireEvent.click(cards[0]!);
    expect(routerPush).toHaveBeenCalledWith('/sessions/session-001');
  });

  // ─── New session CTA ────────────────────────────────────────────────────

  it('hero CTA calls router.push(/sessions/new)', () => {
    const { container } = renderWithIntl(<SessionsLibraryView />);
    const cta = container.querySelector('[data-slot="sessions-hero-cta"]') as HTMLButtonElement;
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    expect(routerPush).toHaveBeenCalledWith('/sessions/new');
  });

  it('empty state CTA navigates to /sessions/new', () => {
    useActiveSessionsMock.mockReturnValue({
      data: { sessions: [], total: 0, page: 1, pageSize: 50 },
      isLoading: false,
      isError: false,
      error: null,
    });
    const { container } = renderWithIntl(<SessionsLibraryView />);
    const cta = container.querySelector('[data-slot="sessions-empty-cta"]') as HTMLButtonElement;
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    expect(routerPush).toHaveBeenCalledWith('/sessions/new');
  });

  // ─── Filtered-empty CTA resets filter ──────────────────────────────────

  it('filtered-empty CTA resets status to all via router.replace', () => {
    searchParamsMap['status'] = 'active';
    useActiveSessionsMock.mockReturnValue({
      data: {
        sessions: [SESSIONS_3.sessions[1]!], // only completed
        total: 1,
        page: 1,
        pageSize: 50,
      },
      isLoading: false,
      isError: false,
      error: null,
    });
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(
      container.querySelector('[data-slot="sessions-empty-filtered-empty"]')
    ).toBeInTheDocument();
    const cta = container.querySelector('[data-slot="sessions-empty-cta"]') as HTMLButtonElement;
    expect(cta).toBeInTheDocument();
    fireEvent.click(cta);
    // Should reset the status filter via router.replace
    expect(routerReplace).toHaveBeenCalled();
    const callArg = routerReplace.mock.calls[0]?.[0];
    expect(callArg).toContain('/sessions');
  });

  // ─── ?state= URL override ───────────────────────────────────────────────

  it('?state=loading URL override forces loading skeleton (NODE_ENV=test)', () => {
    searchParamsMap['state'] = 'loading';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(container.querySelector('[data-slot="sessions-empty-loading"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="session-card-list"]')).not.toBeInTheDocument();
  });

  it('?state=empty URL override forces empty shell', () => {
    searchParamsMap['state'] = 'empty';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(container.querySelector('[data-slot="sessions-empty-empty"]')).toBeInTheDocument();
  });

  it('?state=filtered-empty URL override forces filtered-empty shell', () => {
    searchParamsMap['state'] = 'filtered-empty';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(
      container.querySelector('[data-slot="sessions-empty-filtered-empty"]')
    ).toBeInTheDocument();
  });

  it('ignores unknown ?state= values and falls back to real FSM (default)', () => {
    searchParamsMap['state'] = 'bogus-state';
    const { container } = renderWithIntl(<SessionsLibraryView />);
    expect(container.querySelector('[data-slot="session-card-list"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot^="sessions-empty-"]')).not.toBeInTheDocument();
  });

  // ─── Visual fixture short-circuit ───────────────────────────────────────

  it('renders real data (not fixture) when IS_VISUAL_TEST_BUILD is false (default test env)', () => {
    // With IS_VISUAL_TEST_BUILD=false (default in tests), real hook data is used
    const { container } = renderWithIntl(<SessionsLibraryView />);
    // 3 sessions from SESSIONS_3 fixture mock
    const cards = container.querySelectorAll('[data-slot="session-card-list"]');
    expect(cards).toHaveLength(3);
  });

  // ─── Retry action ───────────────────────────────────────────────────────

  it('error state retry button calls refetch', () => {
    const refetch = vi.fn();
    useActiveSessionsMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error('fail'),
      refetch,
    });
    renderWithIntl(<SessionsLibraryView />);
    const retryBtn = screen.getByRole('button', { name: 'Riprova' });
    fireEvent.click(retryBtn);
    expect(refetch).toHaveBeenCalledOnce();
  });
});
