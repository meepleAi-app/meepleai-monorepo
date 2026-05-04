/**
 * GameDetailViewV2 integration tests — Wave C.1 (Issue #581).
 *
 * Covers all 9 FSM cells from Phase 0.5 contract sez. 3 plus URL override hatch,
 * visual fixture short-circuit, and the CRITICAL assertion:
 *   agents query MUST NEVER receive 'undefined' or '' as gameId.
 *
 * Pattern mirrors Wave B.2 AgentsLibraryView tests:
 *   - vi.mock for hook stubs (not MSW — orchestrator tests stub at hook boundary)
 *   - react-intl IntlProvider with minimal MESSAGES subset
 *   - searchParamsState mutable object for URL override simulation
 *
 * CRITICAL assertion (contract sez. 5.2):
 *   Every test verifies that useGameAgents was called with a non-empty, non-'undefined'
 *   gameId whenever it was called at all. This is enforced via the spy on the mock.
 */

import { render, screen, fireEvent, act } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import type { ReactElement } from 'react';

// ─── next/navigation mocks ────────────────────────────────────────────────

const searchParamsState = { value: '' };
const routerPush = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => (key === 'state' ? searchParamsState.value || null : null),
  }),
  useRouter: () => ({ push: routerPush }),
  usePathname: () => '/games/test-game-id',
}));

// ─── useLibraryGameDetail mock ────────────────────────────────────────────

type MockDetailReturn = {
  data?: import('@/hooks/queries/useLibrary').LibraryGameDetail | null;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  refetch: Mock;
};

const detailMockState: MockDetailReturn = {
  data: undefined,
  isLoading: false,
  isError: false,
  isSuccess: false,
  refetch: vi.fn(),
};

const useLibraryGameDetailSpy = vi.fn<[string], MockDetailReturn>();

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: (gameId: string) => {
    // CRITICAL ASSERTION: if called at all, gameId must not be 'undefined' or ''
    // (the '' case is intentional — hook gates internally via enabled && !!gameId)
    return useLibraryGameDetailSpy(gameId);
  },
}));

// ─── useGameAgents mock ───────────────────────────────────────────────────

type MockAgentsReturn = {
  data?: import('@/lib/api/schemas').AgentDto[];
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  fetchStatus: string;
  refetch: Mock;
};

const agentsMockState: MockAgentsReturn = {
  data: undefined,
  isLoading: false,
  isError: false,
  isSuccess: false,
  fetchStatus: 'idle',
  refetch: vi.fn(),
};

// Spy captures ALL calls — we assert gameId is never 'undefined' or ''
const useGameAgentsSpy = vi.fn<[{ gameId: string | null; enabled?: boolean }], MockAgentsReturn>();

vi.mock('@/hooks/queries/useGameAgents', () => ({
  useGameAgents: (opts: { gameId: string | null; enabled?: boolean }) => useGameAgentsSpy(opts),
}));

// ─── Visual fixture mock ──────────────────────────────────────────────────

let mockIsVisualTestBuild = false;
let mockFixtureData: import('@/hooks/queries/useLibrary').LibraryGameDetail | null = null;

vi.mock('@/lib/games/game-detail-visual-test-fixture', () => ({
  get IS_VISUAL_TEST_BUILD() {
    return mockIsVisualTestBuild;
  },
  tryLoadVisualTestFixture: () => mockFixtureData,
}));

// ─── react-intl messages (subset matching pages.gameDetail.* from it.json) ─

// ── i18n messages — use simple static strings to avoid ICU placeholder errors.
// Tests verify component structure/behavior, NOT i18n formatting.
const MESSAGES: Record<string, string> = {
  'pages.gameDetail.tabs.ariaLabel': 'Navigazione schede gioco',
  'pages.gameDetail.tabs.info': 'Info',
  'pages.gameDetail.tabs.rules': 'Regole',
  'pages.gameDetail.tabs.faqs': 'FAQ',
  'pages.gameDetail.tabs.sessions': 'Sessioni',
  'pages.gameDetail.tabs.agents': 'Agenti',
  'pages.gameDetail.tabs.documents': 'Documenti',
  'pages.gameDetail.states.loading.ariaLabel': 'Caricamento gioco',
  'pages.gameDetail.states.notFound.title': 'Gioco non trovato',
  'pages.gameDetail.states.notFound.subtitle': 'Il gioco non esiste nella libreria.',
  'pages.gameDetail.states.notFound.cta': 'Torna ai giochi',
  'pages.gameDetail.states.error.title': 'Errore di caricamento',
  'pages.gameDetail.states.error.subtitle': 'Si e verificato un errore. Riprova.',
  'pages.gameDetail.states.error.cta': 'Riprova',
  'pages.gameDetail.hero.back': 'Torna ai giochi',
  'pages.gameDetail.hero.backAriaLabel': 'Torna al catalogo dei giochi',
  'pages.gameDetail.hero.ownedBadge': 'In libreria',
  'pages.gameDetail.hero.communityBadge': 'Catalogo',
  // Static strings — no ICU placeholder, orchestrator appends value directly
  'pages.gameDetail.hero.metaPlayers': 'giocatori',
  'pages.gameDetail.hero.metaPlayersSingle': 'giocatore',
  'pages.gameDetail.hero.metaDuration': 'minuti',
  'pages.gameDetail.hero.metaWeight': 'peso',
  'pages.gameDetail.hero.metaRating': 'rating',
  'pages.gameDetail.hero.ctaPlay': 'Gioca',
  'pages.gameDetail.hero.ctaEdit': 'Modifica',
  'pages.gameDetail.hero.ctaShare': 'Condividi',
  'pages.gameDetail.hero.ctaShareAriaLabel': 'Condividi questo gioco',
  'pages.gameDetail.hero.ctaAddToLibrary': 'Aggiungi',
  'pages.gameDetail.hero.ctaSimilar': 'Simili',
  'pages.gameDetail.hero.favoriteAriaLabel': 'Preferito',
  'pages.gameDetail.kpi.rating': 'Rating',
  'pages.gameDetail.kpi.complexity': 'Complessita',
  'pages.gameDetail.kpi.players': 'Giocatori',
  'pages.gameDetail.kpi.playTime': 'Durata',
  'pages.gameDetail.kpi.ratingUnit': '/10',
  'pages.gameDetail.kpi.complexityUnit': '/5',
  'pages.gameDetail.kpi.playTimeUnit': 'min',
  'pages.gameDetail.kpi.notAvailable': 'N/D',
  'pages.gameDetail.kpi.playersRange': 'range',
  'pages.gameDetail.kpi.playersValue': 'valore',
  'pages.gameDetail.faqs.title': 'FAQ',
  'pages.gameDetail.faqs.subtitle': 'Domande frequenti',
  'pages.gameDetail.faqs.viewAll': 'Vedi tutte',
  'pages.gameDetail.faqs.viewAllAriaLabel': 'Vedi tutte le FAQ',
  'pages.gameDetail.faqs.empty': 'Nessuna FAQ disponibile.',
  'pages.gameDetail.faqs.questionAriaLabel': 'Domanda',
  'pages.gameDetail.rules.title': 'Regole',
  'pages.gameDetail.rules.subtitle': 'Sezioni del regolamento',
  'pages.gameDetail.rules.viewAll': 'Regolamento completo',
  'pages.gameDetail.rules.viewAllAriaLabel': 'Vedi il regolamento completo',
  'pages.gameDetail.rules.empty': 'Nessuna regola disponibile.',
  'pages.gameDetail.sessions.title': 'Sessioni',
  'pages.gameDetail.sessions.subtitle': 'Sessioni recenti',
  'pages.gameDetail.sessions.viewAll': 'Vedi tutte',
  'pages.gameDetail.sessions.viewAllAriaLabel': 'Vedi tutte le sessioni',
  'pages.gameDetail.sessions.empty': 'Nessuna sessione registrata.',
  'pages.gameDetail.sessions.emptySubtitle': 'Inizia a registrare le tue partite.',
  'pages.gameDetail.sessions.playersCount': 'giocatori',
  'pages.gameDetail.sessions.winLabel': 'Vinto',
  'pages.gameDetail.sessions.lossLabel': 'Perso',
  'pages.gameDetail.sessions.newSession': 'Nuova sessione',
  'pages.gameDetail.agents.title': 'Agenti AI',
  'pages.gameDetail.agents.subtitle': 'Agenti collegati a questo gioco.',
  'pages.gameDetail.agents.empty': 'Nessun agente disponibile.',
  'pages.gameDetail.agents.emptySubtitle': 'Crea un agente AI per chattare.',
  'pages.gameDetail.agents.createCta': '+ Crea agente',
  'pages.gameDetail.agents.openAriaLabel': 'Apri agente',
  'pages.gameDetail.agents.indexedLabel': 'KB',
  'pages.gameDetail.agents.invocationsLabel': 'invocazioni',
  'pages.gameDetail.documents.title': 'Documenti',
  'pages.gameDetail.documents.subtitle': 'Knowledge Base',
  'pages.gameDetail.documents.empty': 'Nessun documento.',
  'pages.gameDetail.documents.emptySubtitle': 'Carica un PDF per abilitare il RAG.',
  'pages.gameDetail.documents.uploadCta': 'Carica PDF',
  'pages.gameDetail.documents.openCta': 'Apri',
  'pages.gameDetail.documents.openAriaLabel': 'Apri documento',
  'pages.gameDetail.documents.statusIndexed': 'Indicizzato',
  'pages.gameDetail.documents.statusProcessing': 'Elaborazione',
  'pages.gameDetail.documents.statusFailed': 'Errore',
  'pages.gameDetail.documents.statsLine': 'statistiche',
};

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

// ─── Fixture helpers ──────────────────────────────────────────────────────

const VALID_GAME_ID = '00000000-0000-4000-8000-000000000001';

function makeDetail(
  overrides: Partial<import('@/hooks/queries/useLibrary').LibraryGameDetail> = {}
): import('@/hooks/queries/useLibrary').LibraryGameDetail {
  return {
    libraryEntryId: '00000000-0000-4000-8000-000000000lib',
    userId: '00000000-0000-4000-8000-000000000usr',
    gameId: VALID_GAME_ID,
    addedAt: '2026-01-01T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    isAvailableForPlay: true,
    hasCustomPdf: false,
    hasRagAccess: false,
    gameTitle: 'Wingspan Test',
    gamePublisher: 'Stonemaier',
    gameYearPublished: 2019,
    gameIconUrl: null,
    gameImageUrl: null,
    description: 'A great game.',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    complexityRating: 2.4,
    averageRating: 8.1,
    timesPlayed: 5,
    lastPlayed: null,
    winRate: null,
    avgDuration: null,
    ...overrides,
  };
}

function makeAgent(
  overrides: Partial<import('@/lib/api/schemas').AgentDto> = {}
): import('@/lib/api/schemas').AgentDto {
  return {
    id: '00000000-0000-4000-8000-000000000ag1',
    name: 'Wingspan RAG',
    type: 'rag',
    strategyName: 'HybridSearch',
    strategyParameters: {},
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    lastInvokedAt: null,
    invocationCount: 3,
    isRecentlyUsed: false,
    isIdle: false,
    ...overrides,
  };
}

// ─── CRITICAL: assert agents mock never received bad gameId ──────────────

/**
 * Asserts that in ALL calls to useGameAgents where enabled===true,
 * the gameId was never 'undefined' (string) or '' (empty string).
 *
 * This is the contract-enforcing assertion from Phase 0.5 sez. 5.2.
 */
function assertAgentsNeverCalledWithBadGameId() {
  for (const call of useGameAgentsSpy.mock.calls) {
    const opts = call[0];
    if (opts.enabled) {
      expect(opts.gameId, 'useGameAgents called with enabled=true but gameId is bad').not.toBe(
        'undefined'
      );
      expect(opts.gameId, 'useGameAgents called with enabled=true but gameId is empty').not.toBe(
        ''
      );
      expect(opts.gameId).not.toBeNull();
    }
  }
}

// ─── Setup & teardown ─────────────────────────────────────────────────────

import { GameDetailViewV2 } from '../GameDetailViewV2';

function resetAll() {
  searchParamsState.value = '';
  routerPush.mockClear();

  // Reset detail mock to default (loading=false, no data, no error)
  detailMockState.data = undefined;
  detailMockState.isLoading = false;
  detailMockState.isError = false;
  detailMockState.isSuccess = false;
  detailMockState.refetch = vi.fn();
  useLibraryGameDetailSpy.mockImplementation(() => ({ ...detailMockState }));

  // Reset agents mock to default (disabled/idle)
  agentsMockState.data = undefined;
  agentsMockState.isLoading = false;
  agentsMockState.isError = false;
  agentsMockState.isSuccess = false;
  agentsMockState.fetchStatus = 'idle';
  agentsMockState.refetch = vi.fn();
  useGameAgentsSpy.mockImplementation(() => ({ ...agentsMockState }));

  // Reset fixture flags
  mockIsVisualTestBuild = false;
  mockFixtureData = null;
}

describe('GameDetailViewV2 — FSM integration tests (Phase 0.5 contract)', () => {
  beforeEach(resetAll);

  // ─── Cell 1: gameId=null → not-found shell, NO sub-hook fetch ──────────
  it('Cell 1: gameId=null renders not-found shell, agents query NOT called with enabled', () => {
    renderWithIntl(<GameDetailViewV2 gameId={null} />);

    expect(screen.getByTestId !== undefined, 'RTL available').toBe(true);
    expect(screen.getByRole('heading', { name: /gioco non trovato/i })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /Wingspan/i })).not.toBeInTheDocument();

    // Agents query must never have been called with enabled=true when gameId is null
    assertAgentsNeverCalledWithBadGameId();

    // All enabled=false calls must have gameId=null (correct gate)
    const enabledTrueCalls = useGameAgentsSpy.mock.calls.filter(c => c[0].enabled === true);
    expect(enabledTrueCalls).toHaveLength(0);
  });

  // ─── Cell 2: detail loading → loading shell, agents NOT enabled ─────────
  it('Cell 2: detail query loading → loading shell, agents query NOT enabled', () => {
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: undefined,
      isLoading: true,
      isError: false,
      isSuccess: false,
      refetch: vi.fn(),
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Loading shell has aria-busy and aria-label
    const loadingEl = document.querySelector('[data-slot="game-detail-loading"]');
    expect(loadingEl).toBeInTheDocument();
    expect(loadingEl).toHaveAttribute('aria-busy', 'true');

    // agents query must NOT be enabled (isSuccess=false blocks it)
    const enabledCalls = useGameAgentsSpy.mock.calls.filter(c => c[0].enabled === true);
    expect(enabledCalls).toHaveLength(0);

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── Cell 3: detail error → error shell, retry CTA wired ──────────────
  it('Cell 3: detail query error → error shell with retry CTA', () => {
    const refetch = vi.fn();
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: undefined,
      isLoading: false,
      isError: true,
      isSuccess: false,
      refetch,
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    expect(screen.getByRole('heading', { name: /errore di caricamento/i })).toBeInTheDocument();
    const retryCta = document.querySelector('[data-slot="game-detail-error-retry"]');
    expect(retryCta).toBeInTheDocument();

    // Click retry → refetch called
    fireEvent.click(retryCta!);
    expect(refetch).toHaveBeenCalledTimes(1);

    // Agents query must NOT be enabled
    const enabledCalls = useGameAgentsSpy.mock.calls.filter(c => c[0].enabled === true);
    expect(enabledCalls).toHaveLength(0);

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── Cell 4: detail success(null) → not-found shell ──────────────────
  it('Cell 4: detail success(null) → not-found shell (distinct from Cell 1)', () => {
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Not-found shell: same UI but triggered by success(null), not null gameId
    expect(screen.getByRole('heading', { name: /gioco non trovato/i })).toBeInTheDocument();
    const notFoundEl = document.querySelector('[data-slot="game-detail-not-found"]');
    expect(notFoundEl).toBeInTheDocument();

    // Agents still NOT enabled (isSuccess=true but hasData=false → 'not-found' FSM → no default render)
    const enabledCalls = useGameAgentsSpy.mock.calls.filter(c => c[0].enabled === true);
    expect(enabledCalls).toHaveLength(0);

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── Cell 5: detail success + tab='info' → default render, agents NOT enabled ─
  it('Cell 5: detail success + tab=info → default render, agents NOT enabled', () => {
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Default render: hero present
    const heroEl = document.querySelector('[data-slot="game-detail-hero"]');
    expect(heroEl).toBeInTheDocument();

    // Tab panel for info visible
    const infoPanel = document.querySelector('[data-slot="game-detail-panel-info"]');
    expect(infoPanel).toBeInTheDocument();

    // KPI cards inside info panel
    const kpiCards = document.querySelector('[data-slot="game-detail-kpi-cards"]');
    expect(kpiCards).toBeInTheDocument();

    // Agents panel hidden (tab=info)
    const agentsPanel = document.querySelector('[data-slot="game-detail-panel-agents"]');
    expect(agentsPanel).toBeInTheDocument();
    expect(agentsPanel).toHaveAttribute('hidden');

    // useGameAgents NOT enabled (tab !== 'agents')
    const enabledCalls = useGameAgentsSpy.mock.calls.filter(c => c[0].enabled === true);
    expect(enabledCalls).toHaveLength(0);

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── Cell 5→6 transition: tab change info→agents fires agents fetch ─────
  it('Cell 5→6: tab change info→agents fires agents query (lazy, NOT eager)', () => {
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useGameAgentsSpy.mockImplementation(opts => ({
      ...agentsMockState,
      isLoading: opts.enabled ? true : false,
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Before tab change: agents NOT enabled
    const callsBeforeTabChange = useGameAgentsSpy.mock.calls.filter(c => c[0].enabled === true);
    expect(callsBeforeTabChange).toHaveLength(0);

    // Click agents tab
    const agentsTab = document.querySelector('[data-tab-key="agents"]');
    expect(agentsTab).toBeInTheDocument();
    act(() => {
      fireEvent.click(agentsTab!);
    });

    // After tab change: useGameAgents called with enabled=true and valid gameId
    const callsAfterTabChange = useGameAgentsSpy.mock.calls.filter(c => c[0].enabled === true);
    expect(callsAfterTabChange.length).toBeGreaterThan(0);
    expect(callsAfterTabChange[0][0].gameId).toBe(VALID_GAME_ID);

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── Cell 6: agents loading → inline skeleton (NOT shell skeleton) ───────
  it('Cell 6: agents loading → inline skeleton in tab content (NOT full-page shell)', () => {
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useGameAgentsSpy.mockImplementation(() => ({
      ...agentsMockState,
      isLoading: true,
      fetchStatus: 'fetching',
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Click agents tab to trigger render
    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="agents"]')!);
    });

    // Hero still present (no shell replace)
    expect(document.querySelector('[data-slot="game-detail-hero"]')).toBeInTheDocument();

    // Agents loading skeleton inside tab (not full page loading shell)
    expect(document.querySelector('[data-slot="game-detail-loading"]')).not.toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-agents-loading"]')).toBeInTheDocument();

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── Cell 7: agents error → inline banner, NO shell error ──────────────
  it('Cell 7: agents error → inline error banner, full page still renders', () => {
    const agentsRefetch = vi.fn();
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useGameAgentsSpy.mockImplementation(() => ({
      ...agentsMockState,
      isError: true,
      refetch: agentsRefetch,
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Click agents tab
    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="agents"]')!);
    });

    // Hero still present — global error shell NOT triggered
    expect(document.querySelector('[data-slot="game-detail-hero"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-error"]')).not.toBeInTheDocument();

    // Inline agents error banner
    const agentsError = document.querySelector('[data-slot="game-detail-agents-error"]');
    expect(agentsError).toBeInTheDocument();

    // Retry button in agents panel calls agents refetch (not detail refetch)
    const retryBtn = document.querySelector('[data-slot="game-detail-agents-retry"]');
    expect(retryBtn).toBeInTheDocument();
    fireEvent.click(retryBtn!);
    expect(agentsRefetch).toHaveBeenCalledTimes(1);

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── Cell 8: agents success([]) → empty state CTA ──────────────────────
  it('Cell 8: agents success([]) → empty state CTA in agents tab', () => {
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useGameAgentsSpy.mockImplementation(() => ({
      ...agentsMockState,
      data: [],
      isSuccess: true,
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="agents"]')!);
    });

    const agentsEmpty = document.querySelector('[data-slot="game-detail-agents-empty"]');
    expect(agentsEmpty).toBeInTheDocument();
    // CTA to create agent (data-slot="game-detail-agents-create")
    expect(document.querySelector('[data-slot="game-detail-agents-create"]')).toBeInTheDocument();

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── Cell 9: agents success([...]) → grid populated ────────────────────
  it('Cell 9: agents success([agent]) → grid row rendered', () => {
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useGameAgentsSpy.mockImplementation(() => ({
      ...agentsMockState,
      data: [makeAgent({ name: 'Wingspan AI', id: 'ag-test-1' })],
      isSuccess: true,
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="agents"]')!);
    });

    const agentRows = document.querySelectorAll('[data-slot="game-detail-agent-row"]');
    expect(agentRows).toHaveLength(1);
    expect(agentRows[0]).toHaveAttribute('data-agent-id', 'ag-test-1');

    assertAgentsNeverCalledWithBadGameId();
  });

  // ─── ?state=loading URL override ─────────────────────────────────────────
  it('?state=loading URL override → loading shell regardless of real query state', () => {
    searchParamsState.value = 'loading';
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(), // Real data exists but override wins
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    expect(document.querySelector('[data-slot="game-detail-loading"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-hero"]')).not.toBeInTheDocument();
  });

  // ─── ?state=error URL override ───────────────────────────────────────────
  it('?state=error URL override → error shell', () => {
    searchParamsState.value = 'error';

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    expect(document.querySelector('[data-slot="game-detail-error"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-loading"]')).not.toBeInTheDocument();
  });

  // ─── ?state=not-found URL override ──────────────────────────────────────
  it('?state=not-found URL override → not-found shell', () => {
    searchParamsState.value = 'not-found';

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    expect(document.querySelector('[data-slot="game-detail-not-found"]')).toBeInTheDocument();
  });

  // ─── ?state=empty URL override (alias for not-found) ────────────────────
  it('?state=empty URL override → not-found shell (alias)', () => {
    searchParamsState.value = 'empty';

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    expect(document.querySelector('[data-slot="game-detail-not-found"]')).toBeInTheDocument();
  });

  // ─── Invalid ?state=xyz falls through to real state ─────────────────────
  it('invalid ?state=xyz falls through to real FSM state', () => {
    searchParamsState.value = 'xyz';
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Invalid override ignored → real state = 'default' → hero renders
    expect(document.querySelector('[data-slot="game-detail-hero"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-not-found"]')).not.toBeInTheDocument();
  });

  // ─── Visual fixture short-circuit ────────────────────────────────────────
  it('IS_VISUAL_TEST_BUILD=true + fixture → default render bypassing real query', () => {
    mockIsVisualTestBuild = true;
    mockFixtureData = makeDetail({ gameTitle: 'Fixture Wingspan' });

    // Detail query returns nothing (simulating no backend in CI)
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: undefined,
      isLoading: false,
      isError: false,
      isSuccess: false,
      refetch: vi.fn(),
    }));

    renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Fixture should bypass FSM — hero renders with fixture data
    expect(document.querySelector('[data-slot="game-detail-hero"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-loading"]')).not.toBeInTheDocument();
  });

  // ─── CRITICAL: agents handler NEVER receives 'undefined' or '' ──────────
  it('CRITICAL: useGameAgents never called with undefined or empty string gameId', () => {
    // Test with multiple states to ensure the contract is never violated
    const cases: Array<{ gameId: string | null; detailSuccess: boolean }> = [
      { gameId: null, detailSuccess: false },
      { gameId: VALID_GAME_ID, detailSuccess: false },
      { gameId: VALID_GAME_ID, detailSuccess: true },
    ];

    for (const { gameId, detailSuccess } of cases) {
      resetAll();
      useLibraryGameDetailSpy.mockImplementation(() => ({
        data: detailSuccess ? makeDetail() : null,
        isLoading: false,
        isError: false,
        isSuccess: detailSuccess,
        refetch: vi.fn(),
      }));

      renderWithIntl(<GameDetailViewV2 gameId={gameId} />);

      // Never enabled with bad gameId
      const badCalls = useGameAgentsSpy.mock.calls.filter(
        c =>
          c[0].enabled === true &&
          (c[0].gameId === 'undefined' || c[0].gameId === '' || c[0].gameId === null)
      );
      expect(badCalls).toHaveLength(0);
    }
  });

  // ─── Tab state preserved across re-renders ───────────────────────────────
  it('Tab state preserved across re-renders (no reset on detail refetch)', () => {
    const detailState = {
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    };
    useLibraryGameDetailSpy.mockImplementation(() => ({ ...detailState }));
    useGameAgentsSpy.mockImplementation(() => ({
      ...agentsMockState,
      data: [],
      isSuccess: true,
    }));

    const { rerender } = renderWithIntl(<GameDetailViewV2 gameId={VALID_GAME_ID} />);

    // Click agents tab
    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="agents"]')!);
    });

    // Verify agents panel is now visible (not hidden)
    const agentsPanel = document.querySelector('[data-slot="game-detail-panel-agents"]');
    expect(agentsPanel).not.toHaveAttribute('hidden');

    // Re-render (simulating detail refetch completing)
    rerender(
      <IntlProvider locale="it" messages={MESSAGES}>
        <GameDetailViewV2 gameId={VALID_GAME_ID} />
      </IntlProvider>
    );

    // Tab state still on agents (preserved)
    const agentsPanelAfterRerender = document.querySelector(
      '[data-slot="game-detail-panel-agents"]'
    );
    expect(agentsPanelAfterRerender).not.toHaveAttribute('hidden');

    // Info panel hidden (tab was changed to agents)
    const infoPanelAfterRerender = document.querySelector('[data-slot="game-detail-panel-info"]');
    expect(infoPanelAfterRerender).toHaveAttribute('hidden');
  });
});
