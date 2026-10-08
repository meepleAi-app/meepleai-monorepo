/**
 * GameDetailView integration tests — Wave C.1 (Issue #581).
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
 * Issue #4138: le celle FSM 6-9 verificavano il lazy-gating della sub-query
 * useGameAgents, uscita con la lista agenti. La tab e' ora 'chat'.
 */

import { render, screen, fireEvent, act } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import type { ReactElement } from 'react';
import type { RuleSpec } from '@/lib/api/schemas';

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
  useAddGameToLibrary: () => ({
    mutate: vi.fn(),
    mutateAsync: vi.fn(),
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    data: undefined,
    reset: vi.fn(),
  }),
}));

// #2309 — Documents tab uses lazy useSharedGameDetail. Default: empty kbs list,
// not loading. Tests focused on Documents-tab behaviour can override per-test
// via `sharedGameDetailState.kbs = [...]` below.
const sharedGameDetailState: {
  kbs: ReadonlyArray<{
    id: string;
    language: string;
    totalChunks: number;
    indexedAt: string;
  }>;
} = { kbs: [] };

const useSharedGameDetailSpy = vi.fn();
vi.mock('@/hooks/useSharedGameDetail', () => ({
  useSharedGameDetail: (args: { id: string; enabled?: boolean }) => {
    useSharedGameDetailSpy(args);
    if (args.enabled === false) {
      return { data: undefined, isLoading: false, isFetching: false, isError: false };
    }
    return {
      data: { kbs: sharedGameDetailState.kbs },
      isLoading: false,
      isFetching: false,
      isError: false,
    };
  },
}));

// #4084 — Rules tab uses lazy useGameRules. Default: no rule specs (the table is empty
// on real data today — see #4084). Tests focused on Rules-tab behaviour override via
// `gameRulesState.ruleSpecs = [...]` below. `mapRuleSpecsToSections` is re-exported from
// the REAL module (vi.importActual): mocking only the hook, not the pure mapper, keeps
// the mapping logic under its own dedicated unit tests instead of duplicated here.
const gameRulesState: { ruleSpecs: RuleSpec[] } = { ruleSpecs: [] };

const useGameRulesSpy = vi.fn();
vi.mock('@/lib/domain-hooks/useGameRules', async () => {
  const actual = await vi.importActual<typeof import('@/lib/domain-hooks/useGameRules')>(
    '@/lib/domain-hooks/useGameRules'
  );
  return {
    ...actual,
    useGameRules: (gameId: string, opts?: { enabled?: boolean }) => {
      useGameRulesSpy(gameId, opts);
      if (opts?.enabled === false) {
        return { data: undefined, isLoading: false, isFetching: false, isError: false };
      }
      return {
        data: gameRulesState.ruleSpecs,
        isLoading: false,
        isFetching: false,
        isError: false,
      };
    },
  };
});

const trackEventSpy = vi.fn();
vi.mock('@/lib/analytics/track-event', () => ({
  trackEvent: (...args: unknown[]) => trackEventSpy(...args),
}));

// Issue #1466 — Leaderboard hook mock (Stats tab). Default empty/success; tests
// focused on FSM/Phase 0.5 do not care about leaderboard data.
vi.mock('@/lib/domain-hooks/useGameLeaderboard', () => ({
  useGameLeaderboard: () => ({
    data: null,
    isLoading: false,
    isError: false,
    isSuccess: true,
  }),
  GAME_LEADERBOARD_QUERY_KEY: (gameId: string) => ['game-leaderboard', gameId, null, 10] as const,
}));

// Issue #1464 — Game memory hooks (house rules CRUD). Default: no memory, no-op
// mutations; FSM/Phase 0.5 tests don't exercise the house-rules flow.
vi.mock('@/lib/domain-hooks/useGameMemory', () => ({
  useGameMemory: () => ({
    data: null,
    isLoading: false,
    isError: false,
    isSuccess: true,
  }),
  useAddHouseRule: () => ({ mutate: vi.fn() }),
  useUpdateHouseRule: () => ({ mutate: vi.fn() }),
  useRemoveHouseRule: () => ({ mutate: vi.fn() }),
  GAME_MEMORY_QUERY_KEY: (gameId: string) => ['game-memory', gameId] as const,
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
  'pages.gameDetail.tabs.stats': 'Statistiche',
  'pages.gameDetail.tabs.chat': 'Chat AI',
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
  // Issue #1463 — GameDetailSpecsCard (rendered inline in info tab)
  'pages.gameDetail.info.specsTitle': 'Specifiche',
  'pages.gameDetail.info.specsPlayers': 'Giocatori',
  'pages.gameDetail.info.specsDuration': 'Durata',
  'pages.gameDetail.info.specsAge': 'Eta',
  'pages.gameDetail.info.specsComplexity': 'Complessita',
  'pages.gameDetail.info.specsYear': 'Anno',
  'pages.gameDetail.info.specsDesigner': 'Designer',
  'pages.gameDetail.info.specsPublisher': 'Editore',
  'pages.gameDetail.info.specsRatingBgg': 'Rating BGG',
  'pages.gameDetail.info.specsMinutesUnit': 'min',
  // Issue #1466 — Stats tab (play-KPI + leaderboard + community gate)
  'pages.gameDetail.stats.winRate': 'Win rate',
  'pages.gameDetail.stats.timesPlayed': 'Partite',
  'pages.gameDetail.stats.lastPlayed': 'Ultima',
  'pages.gameDetail.stats.lastPlayedNever': 'Mai',
  'pages.gameDetail.stats.lastPlayedDaysAgoUnit': 'g fa',
  'pages.gameDetail.stats.leaderboardTitle': 'Classifica giocatori',
  'pages.gameDetail.stats.leaderboardPlays': 'partite',
  'pages.gameDetail.stats.leaderboardAvg': 'avg',
  'pages.gameDetail.stats.leaderboardWins': 'vittorie',
  'pages.gameDetail.stats.leaderboardEmpty': 'Nessuna classifica disponibile',
  'pages.gameDetail.stats.communityGateTitle': 'Aggiungi alla tua libreria',
  'pages.gameDetail.stats.communityGateDescription':
    'Aggiungi questo gioco per sbloccare statistiche e classifica giocatori.',
  'pages.gameDetail.stats.communityGateCta': '+ Aggiungi a libreria',
  // Issue #1464 — House rules CRUD (Info tab)
  'pages.gameDetail.houseRules.title': 'House rules',
  'pages.gameDetail.houseRules.addCta': '+ Aggiungi',
  'pages.gameDetail.houseRules.addPlaceholder': 'Scrivi una regola...',
  'pages.gameDetail.houseRules.addSubmit': 'Salva',
  'pages.gameDetail.houseRules.editLabel': 'Modifica',
  'pages.gameDetail.houseRules.editSubmit': 'Aggiorna',
  'pages.gameDetail.houseRules.cancel': 'Annulla',
  'pages.gameDetail.houseRules.deleteLabel': 'Elimina',
  'pages.gameDetail.houseRules.deleteConfirmTitle': 'Elimina house rule?',
  'pages.gameDetail.houseRules.deleteConfirmMessage': 'Questa azione non e reversibile.',
  'pages.gameDetail.houseRules.deleteConfirm': 'Elimina',
  'pages.gameDetail.houseRules.empty': 'Nessuna regola',
  // Issue #1471 — Chat preview (Agents tab)
  'pages.gameDetail.chat.title': 'Chat',
  'pages.gameDetail.chat.empty': 'Nessun messaggio',
  'pages.gameDetail.chat.openCta': 'Apri chat',
  'pages.gameDetail.chat.userPrefix': 'Tu',
  'pages.gameDetail.chat.assistantPrefix': 'Agente',
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

// ─── Setup & teardown ─────────────────────────────────────────────────────

import { GameDetailView } from '../GameDetailView';

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

  // Reset fixture flags
  mockIsVisualTestBuild = false;
  mockFixtureData = null;

  // #2309 — Reset shared-game-detail mock state + spies
  sharedGameDetailState.kbs = [];
  useSharedGameDetailSpy.mockClear();
  trackEventSpy.mockClear();

  // #4084 — Reset rules mock state + spy
  gameRulesState.ruleSpecs = [];
  useGameRulesSpy.mockClear();
}

describe('GameDetailView — FSM integration tests (Phase 0.5 contract)', () => {
  beforeEach(resetAll);

  // ─── Cell 1: gameId=null → not-found shell, NO sub-hook fetch ──────────
  it('Cell 1: gameId=null renders not-found shell, agents query NOT called with enabled', () => {
    renderWithIntl(<GameDetailView gameId={null} />);

    expect(screen.getByTestId !== undefined, 'RTL available').toBe(true);
    expect(screen.getByRole('heading', { name: /gioco non trovato/i })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /Wingspan/i })).not.toBeInTheDocument();
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

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    // Loading shell has aria-busy and aria-label
    const loadingEl = document.querySelector('[data-slot="game-detail-loading"]');
    expect(loadingEl).toBeInTheDocument();
    expect(loadingEl).toHaveAttribute('aria-busy', 'true');
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

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    expect(screen.getByRole('heading', { name: /errore di caricamento/i })).toBeInTheDocument();
    const retryCta = document.querySelector('[data-slot="game-detail-error-retry"]');
    expect(retryCta).toBeInTheDocument();

    // Click retry → refetch called
    fireEvent.click(retryCta!);
    expect(refetch).toHaveBeenCalledTimes(1);
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

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    // Not-found shell: same UI but triggered by success(null), not null gameId
    expect(screen.getByRole('heading', { name: /gioco non trovato/i })).toBeInTheDocument();
    const notFoundEl = document.querySelector('[data-slot="game-detail-not-found"]');
    expect(notFoundEl).toBeInTheDocument();
  });

  it('Cell 5: detail success + tab=info → default render, chat panel hidden', () => {
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    // Default render: hero present
    const heroEl = document.querySelector('[data-slot="game-detail-hero"]');
    expect(heroEl).toBeInTheDocument();

    // Tab panel for info visible
    const infoPanel = document.querySelector('[data-slot="game-detail-panel-info"]');
    expect(infoPanel).toBeInTheDocument();

    // KPI cards inside info panel
    const kpiCards = document.querySelector('[data-slot="game-detail-kpi-cards"]');
    expect(kpiCards).toBeInTheDocument();

    // Specs card inside info panel (Issue #1463 — regression guard)
    const specsCard = document.querySelector('[data-slot="game-detail-specs-card"]');
    expect(specsCard).toBeInTheDocument();

    // Issue #4138: il pannello della tab e' ora 'chat' e non porta piu` la lista
    // agenti. Resta nascosto con tab=info.
    const chatPanel = document.querySelector('[data-slot="game-detail-panel-chat"]');
    expect(chatPanel).toBeInTheDocument();
    expect(chatPanel).toHaveAttribute('hidden');
  });

  it('?state=loading URL override → loading shell regardless of real query state', () => {
    searchParamsState.value = 'loading';
    useLibraryGameDetailSpy.mockImplementation(() => ({
      data: makeDetail(), // Real data exists but override wins
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    expect(document.querySelector('[data-slot="game-detail-loading"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-hero"]')).not.toBeInTheDocument();
  });

  // ─── ?state=error URL override ───────────────────────────────────────────
  it('?state=error URL override → error shell', () => {
    searchParamsState.value = 'error';

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    expect(document.querySelector('[data-slot="game-detail-error"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-loading"]')).not.toBeInTheDocument();
  });

  // ─── ?state=not-found URL override ──────────────────────────────────────
  it('?state=not-found URL override → not-found shell', () => {
    searchParamsState.value = 'not-found';

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    expect(document.querySelector('[data-slot="game-detail-not-found"]')).toBeInTheDocument();
  });

  // ─── ?state=empty URL override (alias for not-found) ────────────────────
  it('?state=empty URL override → not-found shell (alias)', () => {
    searchParamsState.value = 'empty';

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

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

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

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

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    // Fixture should bypass FSM — hero renders with fixture data
    expect(document.querySelector('[data-slot="game-detail-hero"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="game-detail-loading"]')).not.toBeInTheDocument();
  });

  it('Tab state preserved across re-renders (no reset on detail refetch)', () => {
    const detailState = {
      data: makeDetail(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    };
    useLibraryGameDetailSpy.mockImplementation(() => ({ ...detailState }));

    const { rerender } = renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    // Click the chat tab
    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="chat"]')!);
    });

    // Verify the chat panel is now visible (not hidden)
    const chatPanel = document.querySelector('[data-slot="game-detail-panel-chat"]');
    expect(chatPanel).not.toHaveAttribute('hidden');

    // Re-render (simulating detail refetch completing)
    rerender(
      <IntlProvider locale="it" messages={MESSAGES}>
        <GameDetailView gameId={VALID_GAME_ID} />
      </IntlProvider>
    );

    // Tab state still on chat (preserved)
    const chatPanelAfterRerender = document.querySelector('[data-slot="game-detail-panel-chat"]');
    expect(chatPanelAfterRerender).not.toHaveAttribute('hidden');

    // Info panel hidden (tab was changed to chat)
    const infoPanelAfterRerender = document.querySelector('[data-slot="game-detail-panel-info"]');
    expect(infoPanelAfterRerender).toHaveAttribute('hidden');
  });

  // ─── Issue #1466 — Stats tab + community gate ────────────────────────────

  it('Issue #1466: community variant renders GameDetailCommunityGate inside Sessions and Stats panels', () => {
    detailMockState.data = makeDetail({ libraryEntryId: '' });
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    // Two gates rendered: one inside sessions panel, one inside stats panel.
    const gates = document.querySelectorAll('[data-slot="game-detail-community-gate"]');
    expect(gates).toHaveLength(2);

    // The non-locked content must NOT be present in those panels.
    const sessionsPanel = document.querySelector('[data-slot="game-detail-panel-sessions"]');
    expect(sessionsPanel?.querySelector('[data-slot="game-detail-sessions-rail"]')).toBeNull();
  });

  it('Issue #1466: own variant exposes the Stats tab with KpiCards (no gate)', () => {
    detailMockState.data = makeDetail(); // default own variant
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    // Stats tab button is present and NOT locked
    const statsTab = document.querySelector('[data-tab-key="stats"]');
    expect(statsTab).toBeInTheDocument();
    expect(statsTab).toHaveAttribute('data-locked', 'false');

    // Stats panel exists in the DOM (hidden until clicked, but its content is the own-variant flow,
    // not the gate).
    const statsPanel = document.querySelector('[data-slot="game-detail-panel-stats"]');
    expect(statsPanel).toBeInTheDocument();
    expect(statsPanel?.querySelector('[data-slot="game-detail-community-gate"]')).toBeNull();
  });

  // ─── Issue #1464 — House rules CRUD visibility per variant ───────────────

  it('Issue #1464: own variant renders GameDetailHouseRulesList inside Info panel', () => {
    detailMockState.data = makeDetail(); // own variant
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    const infoPanel = document.querySelector('[data-slot="game-detail-panel-info"]');
    expect(infoPanel).toBeInTheDocument();
    expect(
      infoPanel?.querySelector('[data-slot="game-detail-house-rules-list"]')
    ).toBeInTheDocument();
  });

  it('Issue #1464: community variant skips the house-rules list', () => {
    detailMockState.data = makeDetail({ libraryEntryId: '' });
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    expect(document.querySelector('[data-slot="game-detail-house-rules-list"]')).toBeNull();
  });

  // ─── #2309 — Documents tab wiring (lazy useSharedGameDetail) ──────────────

  it('#2309 DEC-B: does NOT fetch SharedGameDetail when default tab=info', () => {
    detailMockState.data = makeDetail();
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    // Default tab is 'info'; lazy gate must NOT fetch.
    expect(useSharedGameDetailSpy).toHaveBeenCalledWith(
      expect.objectContaining({ id: VALID_GAME_ID, enabled: false })
    );
  });

  it('#2309 DEC-B: fetches SharedGameDetail when user clicks Documents tab', async () => {
    detailMockState.data = makeDetail();
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    const docsTabButton = document.getElementById('game-detail-tab-documents');
    expect(docsTabButton).not.toBeNull();
    act(() => {
      fireEvent.click(docsTabButton as HTMLElement);
    });

    // Post-click the hook re-renders with enabled=true.
    expect(useSharedGameDetailSpy).toHaveBeenCalledWith(
      expect.objectContaining({ id: VALID_GAME_ID, enabled: true })
    );
  });

  it('#2309 DEC-A: maps PublishedKbPreview to GameDetailKbDocEntry on Documents tab', () => {
    detailMockState.data = makeDetail();
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);
    sharedGameDetailState.kbs = [
      {
        id: 'kb-azul-ita',
        language: 'it',
        totalChunks: 42,
        indexedAt: '2026-01-15T00:00:00Z',
      },
    ];

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);
    const docsTabButton = document.getElementById('game-detail-tab-documents');
    act(() => {
      fireEvent.click(docsTabButton as HTMLElement);
    });

    // The list component renders the title we mapped (`KB IT`).
    expect(document.body.textContent).toContain('KB IT');
  });

  it('#2309 DEC-C: fires telemetry when kbStatus=ready but kbs[] empty (Documents tab open)', async () => {
    detailMockState.data = makeDetail(); // kbStatus default likely undefined; override:
    detailMockState.data = { ...detailMockState.data!, kbStatus: 'ready' };
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);
    sharedGameDetailState.kbs = []; // empty list — DEC-C edge

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);
    const docsTabButton = document.getElementById('game-detail-tab-documents');
    act(() => {
      fireEvent.click(docsTabButton as HTMLElement);
    });

    expect(trackEventSpy).toHaveBeenCalledWith('documents_tab_empty_when_kb_ready', {
      gameId: VALID_GAME_ID,
    });
  });

  it('#2309 DEC-C: does NOT fire telemetry when kbStatus=indexing (transient state, not a real gap)', () => {
    detailMockState.data = { ...makeDetail(), kbStatus: 'indexing' };
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);
    sharedGameDetailState.kbs = [];

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);
    const docsTabButton = document.getElementById('game-detail-tab-documents');
    act(() => {
      fireEvent.click(docsTabButton as HTMLElement);
    });

    expect(trackEventSpy).not.toHaveBeenCalledWith(
      'documents_tab_empty_when_kb_ready',
      expect.anything()
    );
  });

  // ─── #4084 — Rules tab wiring (lazy useGameRules, real data not a literal) ───
  //
  // Before this fix GameDetailView passed `sections={[]}` straight to
  // GameDetailRulesAccordion — no hook, no query, no gate. These tests guard the
  // fix the same way #2309's DEC-B tests guard the Documents tab: gating on open,
  // AND that opening it actually surfaces real data, not a dead literal.

  it('#4084: does NOT fetch rules when default tab=info', () => {
    detailMockState.data = makeDetail();
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    expect(useGameRulesSpy).toHaveBeenCalledWith(
      VALID_GAME_ID,
      expect.objectContaining({ enabled: false })
    );
  });

  it('#4084: fetches rules when user clicks the Rules tab', () => {
    detailMockState.data = makeDetail();
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    const rulesTabButton = document.getElementById('game-detail-tab-rules');
    expect(rulesTabButton).not.toBeNull();
    act(() => {
      fireEvent.click(rulesTabButton as HTMLElement);
    });

    expect(useGameRulesSpy).toHaveBeenCalledWith(
      VALID_GAME_ID,
      expect.objectContaining({ enabled: true })
    );
  });

  it('#4084: renders a real rule section on the Rules tab instead of the dead literal', () => {
    detailMockState.data = makeDetail();
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);
    gameRulesState.ruleSpecs = [
      {
        id: 'spec-1',
        gameId: VALID_GAME_ID,
        version: 'v1',
        createdAt: '2026-01-01T00:00:00.000Z',
        createdByUserId: null,
        parentVersionId: null,
        atoms: [
          {
            id: 'a1',
            text: 'Mescola il mazzo prima di iniziare.',
            section: 'Setup',
            page: null,
            line: null,
          },
        ],
      },
    ];

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);
    const rulesTabButton = document.getElementById('game-detail-tab-rules');
    act(() => {
      fireEvent.click(rulesTabButton as HTMLElement);
    });

    // Regression guard: `sections={[]}` can never render this, since [] has no section
    // to group into — this text can ONLY appear if real data reached the component.
    expect(document.body.textContent).toContain('Setup');
  });

  it('#4084: the "view all rules" link survives a genuinely empty rule-spec list', () => {
    detailMockState.data = makeDetail();
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);
    gameRulesState.ruleSpecs = []; // real backend "no rules published" — not the old literal

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);
    const rulesTabButton = document.getElementById('game-detail-tab-rules');
    act(() => {
      fireEvent.click(rulesTabButton as HTMLElement);
    });

    const rulesPanel = document.querySelector('[data-slot="game-detail-panel-rules"]');
    expect(
      rulesPanel?.querySelector('[data-slot="game-detail-rules-view-all"]')
    ).toBeInTheDocument();
  });

  // ─── Issue #1471 — Chat preview in the chat panel, regression guard ───

  it('Issue #1471: chat panel contains GameDetailChatTab inline preview', () => {
    detailMockState.data = makeDetail(); // own variant
    detailMockState.isSuccess = true;
    useLibraryGameDetailSpy.mockReturnValue(detailMockState);

    renderWithIntl(<GameDetailView gameId={VALID_GAME_ID} />);

    const chatPanel = document.querySelector('[data-slot="game-detail-panel-chat"]');
    expect(chatPanel).toBeInTheDocument();
    expect(chatPanel?.querySelector('[data-slot="game-detail-chat-tab"]')).toBeInTheDocument();
    // Issue #4138: e la lista agenti NON c'e' piu`.
    expect(document.querySelector('[data-slot="game-detail-agents-list"]')).not.toBeInTheDocument();
  });
});
