/**
 * AgentDetailViewV2 integration tests — Wave C.2 (Issue #581).
 *
 * Covers all 10 FSM cells from Phase 0.5 contract sez. 3, plus URL override
 * hatch, visual fixture short-circuit, variant matrix, and the CRITICAL assertion:
 *   useAgentKbDocs MUST NEVER be called with 'undefined', 'null', or '' as gameId
 *   when enabled=true. Cell 10: agent.gameId === null → standalone, fetch never fires.
 *
 * Pattern mirrors Wave C.1 GameDetailViewV2.test.tsx:
 *   - vi.mock for hook stubs (not MSW — orchestrator tests stub at hook boundary)
 *   - react-intl IntlProvider with minimal MESSAGES subset
 *   - searchParamsState mutable for URL override simulation
 *
 * CRITICAL assertion (contract sez. 5.2):
 *   assertKbDocsNeverCalledWithBadGameId() runs in EVERY test.
 *   When enabled=true, gameId must match UUID pattern — never string literals
 *   'undefined', 'null', or empty string.
 */

import { act, fireEvent, render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import type { ReactElement } from 'react';

// ─── next/navigation mocks ────────────────────────────────────────────────────

const searchParamsState = { state: null as string | null, fixture: null as string | null };
const routerPush = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => {
      if (key === 'state') return searchParamsState.state;
      if (key === 'fixture') return searchParamsState.fixture;
      return null;
    },
  }),
  useRouter: () => ({ push: routerPush }),
  usePathname: () => '/agents/test-agent-id',
}));

// ─── useAgent mock ────────────────────────────────────────────────────────────

type MockAgentReturn = {
  data?: import('@/lib/api/schemas/agents.schemas').AgentDto | null;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  refetch: Mock;
};

const agentMockState: MockAgentReturn = {
  data: undefined,
  isLoading: false,
  isError: false,
  isSuccess: false,
  refetch: vi.fn(),
};

const useAgentSpy = vi.fn<[string | null], MockAgentReturn>();

vi.mock('@/hooks/queries/useAgent', () => ({
  useAgent: (agentId: string | null) => useAgentSpy(agentId),
}));

// ─── useAgentKbDocs mock ──────────────────────────────────────────────────────

type MockKbDocsReturn = {
  data?: import('@/hooks/queries/useAgentData').KbDocumentPreview[];
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  refetch: Mock;
};

const kbDocsMockState: MockKbDocsReturn = {
  data: undefined,
  isLoading: false,
  isError: false,
  isSuccess: false,
  refetch: vi.fn(),
};

// Spy captures ALL calls to verify gameId integrity
const useAgentKbDocsSpy = vi.fn<[string | undefined], MockKbDocsReturn>();

vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentKbDocs: (gameId: string | undefined) => useAgentKbDocsSpy(gameId),
  useAgentThreads: (agentId: string) => useAgentThreadsSpy(agentId),
}));

// ─── useAgentThreads mock ─────────────────────────────────────────────────────

type MockThreadsReturn = {
  data?: import('@/hooks/queries/useAgentData').ChatThreadPreview[];
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  refetch: Mock;
};

const threadsMockState: MockThreadsReturn = {
  data: undefined,
  isLoading: false,
  isError: false,
  isSuccess: false,
  refetch: vi.fn(),
};

const useAgentThreadsSpy = vi.fn<[string], MockThreadsReturn>();

// ─── useAgentConfig mock ──────────────────────────────────────────────────────

type MockConfigReturn = {
  data?: import('@/lib/api/schemas/agent-config.schemas').AgentConfigDto | null;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  refetch: Mock;
};

const configMockState: MockConfigReturn = {
  data: undefined,
  isLoading: false,
  isError: false,
  isSuccess: false,
  refetch: vi.fn(),
};

const useAgentConfigSpy = vi.fn<[string, boolean?], MockConfigReturn>();

vi.mock('@/hooks/queries/useAgentConfig', () => ({
  useAgentConfig: (gameId: string, enabled?: boolean) => useAgentConfigSpy(gameId, enabled),
}));

// ─── Visual fixture mock ──────────────────────────────────────────────────────

let mockIsVisualTestBuild = false;
let mockFixtureData: import('@/lib/api/schemas/agents.schemas').AgentDto | null = null;

vi.mock('@/lib/agents/agent-detail-visual-test-fixture', () => ({
  get IS_VISUAL_TEST_BUILD() {
    return mockIsVisualTestBuild;
  },
  tryLoadVisualTestFixture: (_state?: string) => mockFixtureData,
}));

// ─── i18n messages (subset of pages.agentDetail.* from it.json) ───────────────
// Use simple static strings — tests verify structure, not ICU formatting.

const MESSAGES: Record<string, string> = {
  'pages.agentDetail.tabs.ariaLabel': 'Navigazione schede agente',
  'pages.agentDetail.tabs.identity': 'Identità',
  'pages.agentDetail.tabs.knowledge': 'Conoscenza',
  'pages.agentDetail.tabs.performance': 'Prestazioni',
  'pages.agentDetail.tabs.history': 'Storico',
  'pages.agentDetail.tabs.settings': 'Impostazioni',
  'pages.agentDetail.states.loading.ariaLabel': 'Caricamento agente',
  'pages.agentDetail.states.notFound.title': 'Agente non trovato',
  'pages.agentDetail.states.notFound.subtitle': "L'agente non esiste.",
  'pages.agentDetail.states.notFound.cta': 'Torna agli agenti',
  'pages.agentDetail.states.error.title': 'Errore di caricamento',
  'pages.agentDetail.states.error.subtitle': 'Si è verificato un errore. Riprova.',
  'pages.agentDetail.states.error.cta': 'Riprova',
  'pages.agentDetail.hero.back': 'Torna agli agenti',
  'pages.agentDetail.hero.backAriaLabel': 'Torna alla lista degli agenti',
  'pages.agentDetail.hero.activeBadge': 'Attivo',
  'pages.agentDetail.hero.draftBadge': 'In setup',
  'pages.agentDetail.hero.archivedBadge': 'Archiviato',
  'pages.agentDetail.hero.metaLastUsedNever': 'Mai utilizzato',
  'pages.agentDetail.hero.metaGameNone': 'Agente standalone',
  'pages.agentDetail.hero.ctaPlay': 'Avvia chat',
  'pages.agentDetail.hero.ctaSetup': 'Continua setup',
  'pages.agentDetail.hero.ctaUnarchive': 'Riattiva',
  'pages.agentDetail.hero.ctaShare': 'Condividi',
  'pages.agentDetail.hero.ctaShareAriaLabel': 'Condividi questo agente',
  'pages.agentDetail.hero.setupBannerTitle': 'Questo agente è in fase di setup',
  'pages.agentDetail.hero.setupBannerSubtitle': 'Completa la configurazione.',
  'pages.agentDetail.hero.archivedBannerTitle': 'Questo agente è archiviato',
  'pages.agentDetail.hero.archivedBannerSubtitle': 'Riattivalo per usarlo.',
  'pages.agentDetail.identity.personaTitle': 'Persona',
  'pages.agentDetail.identity.personaEmpty': 'Nessuna persona configurata.',
  'pages.agentDetail.identity.systemPromptTitle': 'System Prompt',
  'pages.agentDetail.identity.systemPromptEmpty': 'Nessun system prompt.',
  'pages.agentDetail.identity.systemPromptHidden': 'System prompt non disponibile.',
  'pages.agentDetail.knowledge.title': 'Documenti Knowledge Base',
  'pages.agentDetail.knowledge.subtitle': 'Documenti indicizzati.',
  'pages.agentDetail.knowledge.empty': 'Nessun documento.',
  'pages.agentDetail.knowledge.emptySubtitle': 'Carica un PDF.',
  'pages.agentDetail.knowledge.uploadCta': 'Carica documento',
  'pages.agentDetail.knowledge.standaloneTitle': 'Agente standalone',
  'pages.agentDetail.knowledge.standaloneSubtitle': 'Nessuna KB associata.',
  'pages.agentDetail.knowledge.standaloneCta': 'Associa un gioco',
  'pages.agentDetail.knowledge.statusIndexed': 'INDICIZZATO',
  'pages.agentDetail.knowledge.statusProcessing': 'ELABORAZIONE',
  'pages.agentDetail.knowledge.statusFailed': 'ERRORE',
  'pages.agentDetail.performance.lockedDraftTitle': 'Prestazioni non disponibili',
  'pages.agentDetail.performance.lockedDraftSubtitle': 'Completa il setup.',
  'pages.agentDetail.performance.empty': 'Nessuna statistica disponibile.',
  'pages.agentDetail.history.title': 'Storico chat',
  'pages.agentDetail.history.empty': 'Nessuna conversazione.',
  'pages.agentDetail.history.emptySubtitle': 'Le conversazioni appariranno qui.',
  'pages.agentDetail.history.lockedDraftTitle': 'Storico non disponibile',
  'pages.agentDetail.history.lockedDraftSubtitle': 'Completa il setup.',
  'pages.agentDetail.settings.title': 'Impostazioni agente',
  'pages.agentDetail.settings.strategyLabel': 'Strategia RAG',
  'pages.agentDetail.settings.parametersLabel': 'Parametri',
  'pages.agentDetail.settings.readOnlyBanner': 'Impostazioni in sola lettura.',
  'pages.agentDetail.settings.saveCta': 'Salva',
  'pages.agentDetail.settings.cancelCta': 'Annulla',
  'pages.agentDetail.settings.saveSuccess': 'Salvato.',
  'pages.agentDetail.settings.saveError': 'Errore nel salvataggio.',
  'pages.agentDetail.settings.dangerZoneTitle': 'Zona pericolosa',
  'pages.agentDetail.settings.archiveCta': 'Archivia agente',
  'pages.agentDetail.settings.archiveConfirmTitle': 'Archivia?',
  'pages.agentDetail.settings.archiveConfirmSubtitle': "L'agente verrà disattivato.",
  'pages.agentDetail.settings.archiveConfirm': 'Archivia',
  'pages.agentDetail.settings.archiveCancel': 'Annulla',
};

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

// ─── Fixture helpers ───────────────────────────────────────────────────────────

const VALID_AGENT_ID = '00000000-0000-4000-8000-000000000001';
const VALID_GAME_ID = '00000000-0000-4000-8000-000000000002';

function makeAgent(
  overrides: Partial<import('@/lib/api/schemas/agents.schemas').AgentDto> = {}
): import('@/lib/api/schemas/agents.schemas').AgentDto {
  return {
    id: VALID_AGENT_ID,
    name: 'Catan Strategist',
    type: 'Strategist',
    strategyName: 'HybridSearch',
    strategyParameters: {},
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    lastInvokedAt: '2026-04-01T12:00:00Z',
    invocationCount: 42,
    isRecentlyUsed: true,
    isIdle: false,
    gameId: VALID_GAME_ID,
    gameName: 'Catan',
    createdByUserId: '00000000-0000-4000-8000-000000000usr',
    ...overrides,
  };
}

// ─── CRITICAL: assert kbDocs mock never received bad gameId ──────────────────

/**
 * Asserts that useAgentKbDocs was NEVER called with 'undefined', 'null', or ''
 * as gameId. This is the contract-enforcing assertion from Phase 0.5 sez. 5.2.
 *
 * When enabled (non-undefined, non-empty gameId), the value must be a valid UUID.
 * This prevents the /api/v1/knowledge-base/undefined/documents cascade failure.
 */
function assertKbDocsNeverCalledWithBadGameId() {
  for (const call of useAgentKbDocsSpy.mock.calls) {
    const gameId = call[0];
    if (gameId !== undefined && gameId !== '') {
      // When a real gameId is passed, it must never be the string literal 'undefined' or 'null'
      expect(gameId, 'useAgentKbDocs called with string literal "undefined"').not.toBe('undefined');
      expect(gameId, 'useAgentKbDocs called with string literal "null"').not.toBe('null');
      // Must match UUID pattern
      expect(gameId, 'useAgentKbDocs called with invalid UUID').toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
      );
    }
    // gameId === undefined or '' → hook disabled internally — this is CORRECT behavior
  }
}

// ─── Setup & teardown ─────────────────────────────────────────────────────────

import { AgentDetailViewV2 } from '../AgentDetailViewV2';

function resetAll() {
  searchParamsState.state = null;
  searchParamsState.fixture = null;
  routerPush.mockClear();

  agentMockState.data = undefined;
  agentMockState.isLoading = false;
  agentMockState.isError = false;
  agentMockState.isSuccess = false;
  agentMockState.refetch = vi.fn();
  useAgentSpy.mockReset();
  useAgentSpy.mockImplementation(() => ({ ...agentMockState }));

  kbDocsMockState.data = undefined;
  kbDocsMockState.isLoading = false;
  kbDocsMockState.isError = false;
  kbDocsMockState.isSuccess = false;
  kbDocsMockState.refetch = vi.fn();
  useAgentKbDocsSpy.mockReset();
  useAgentKbDocsSpy.mockImplementation(() => ({ ...kbDocsMockState }));

  threadsMockState.data = undefined;
  threadsMockState.isLoading = false;
  threadsMockState.isError = false;
  threadsMockState.isSuccess = false;
  threadsMockState.refetch = vi.fn();
  useAgentThreadsSpy.mockReset();
  useAgentThreadsSpy.mockImplementation(() => ({ ...threadsMockState }));

  configMockState.data = undefined;
  configMockState.isLoading = false;
  configMockState.isError = false;
  configMockState.isSuccess = false;
  configMockState.refetch = vi.fn();
  useAgentConfigSpy.mockReset();
  useAgentConfigSpy.mockImplementation(() => ({ ...configMockState }));

  mockIsVisualTestBuild = false;
  mockFixtureData = null;
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('AgentDetailViewV2 — FSM integration tests (Phase 0.5 contract, Wave C.2)', () => {
  beforeEach(resetAll);

  // ─── Cell 1: agentId=null → not-found shell ────────────────────────────────
  it('Cell 1: agentId=null renders not-found shell, KB query NOT called with enabled gameId', () => {
    renderWithIntl(<AgentDetailViewV2 agentId={null} />);

    // Not-found shell
    expect(screen.getByRole('heading', { name: /agente non trovato/i })).toBeInTheDocument();
    const notFoundEl = document.querySelector('[data-slot="agent-detail-not-found"]');
    expect(notFoundEl).toBeInTheDocument();

    // Agent query never called with enabled=true
    const enabledAgentCalls = useAgentSpy.mock.calls.filter(c => c[0] !== null);
    expect(enabledAgentCalls).toHaveLength(0);

    // KB docs query must never be called with a bad gameId
    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 2: agent loading → loading shell ─────────────────────────────────
  it('Cell 2: agent query loading → loading shell, sub-hooks NOT enabled', () => {
    useAgentSpy.mockImplementation(() => ({
      data: undefined,
      isLoading: true,
      isError: false,
      isSuccess: false,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    const loadingEl = document.querySelector('[data-slot="agent-detail-loading"]');
    expect(loadingEl).toBeInTheDocument();
    expect(loadingEl).toHaveAttribute('aria-busy', 'true');

    // KB docs must not fire when agent is still loading
    const kbCallsWithGameId = useAgentKbDocsSpy.mock.calls.filter(
      c => c[0] !== undefined && c[0] !== ''
    );
    expect(kbCallsWithGameId).toHaveLength(0);

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 3: agent error → error shell with retry CTA ─────────────────────
  it('Cell 3: agent query error → error shell, retry CTA wired to refetch', () => {
    const refetch = vi.fn();
    useAgentSpy.mockImplementation(() => ({
      data: undefined,
      isLoading: false,
      isError: true,
      isSuccess: false,
      refetch,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    expect(screen.getByRole('heading', { name: /errore di caricamento/i })).toBeInTheDocument();
    const retryBtn = document.querySelector('[data-slot="agent-detail-error-retry"]');
    expect(retryBtn).toBeInTheDocument();

    // Click retry → agent refetch called
    fireEvent.click(retryBtn!);
    expect(refetch).toHaveBeenCalledTimes(1);

    // KB docs query must NOT be enabled
    const kbCallsWithGameId = useAgentKbDocsSpy.mock.calls.filter(
      c => c[0] !== undefined && c[0] !== ''
    );
    expect(kbCallsWithGameId).toHaveLength(0);

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 4: agent success(null) → not-found shell (distinct from Cell 1) ──
  it('Cell 4: agent success(null) → not-found shell, KB docs NOT enabled', () => {
    useAgentSpy.mockImplementation(() => ({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // Not-found shell (same UI, distinct trigger: success(null) vs null agentId)
    expect(screen.getByRole('heading', { name: /agente non trovato/i })).toBeInTheDocument();
    const notFoundEl = document.querySelector('[data-slot="agent-detail-not-found"]');
    expect(notFoundEl).toBeInTheDocument();

    // KB docs must never fire when agent data is null
    const kbCallsWithGameId = useAgentKbDocsSpy.mock.calls.filter(
      c => c[0] !== undefined && c[0] !== ''
    );
    expect(kbCallsWithGameId).toHaveLength(0);

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 5: agent success + tab='identity' → default render, NO sub-fetch ─
  it('Cell 5: agent success + tab=identity → default render, KB docs NOT enabled', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // Default render: hero present
    const heroEl = document.querySelector('[data-slot="agent-detail-hero"]');
    expect(heroEl).toBeInTheDocument();

    // Identity tab panel visible
    const identityPanel = document.querySelector('[data-slot="agent-detail-panel-identity"]');
    expect(identityPanel).toBeInTheDocument();
    expect(identityPanel).not.toHaveAttribute('hidden');

    // Knowledge panel hidden (tab=identity)
    const knowledgePanel = document.querySelector('[data-slot="agent-detail-panel-knowledge"]');
    expect(knowledgePanel).toHaveAttribute('hidden');

    // KB docs must NOT be enabled (tab !== 'knowledge')
    const kbCallsWithGameId = useAgentKbDocsSpy.mock.calls.filter(
      c => c[0] !== undefined && c[0] !== ''
    );
    expect(kbCallsWithGameId).toHaveLength(0);

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 5→6: tab change identity→knowledge fires KB fetch ───────────────
  it('Cell 5→6: tab change identity→knowledge fires KB fetch (lazy, not eager)', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ gameId: VALID_GAME_ID }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentKbDocsSpy.mockImplementation(gameId => ({
      ...kbDocsMockState,
      isLoading: gameId === VALID_GAME_ID,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // Before tab change: KB not called with gameId
    const callsBefore = useAgentKbDocsSpy.mock.calls.filter(c => c[0] !== undefined && c[0] !== '');
    expect(callsBefore).toHaveLength(0);

    // Click knowledge tab
    const knowledgeTab = document.querySelector('[data-tab-key="knowledge"]');
    expect(knowledgeTab).toBeInTheDocument();
    act(() => {
      fireEvent.click(knowledgeTab!);
    });

    // After tab change: KB called with valid gameId
    const callsAfter = useAgentKbDocsSpy.mock.calls.filter(c => c[0] !== undefined && c[0] !== '');
    expect(callsAfter.length).toBeGreaterThan(0);
    expect(callsAfter[0][0]).toBe(VALID_GAME_ID);

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 6: knowledge loading → inline skeleton (NOT shell skeleton) ───────
  it('Cell 6: knowledge loading → inline skeleton, hero still present (no shell replace)', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ gameId: VALID_GAME_ID }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentKbDocsSpy.mockImplementation(() => ({
      ...kbDocsMockState,
      isLoading: true,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // Navigate to knowledge tab
    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="knowledge"]')!);
    });

    // Hero still present (full-page loading shell NOT triggered)
    expect(document.querySelector('[data-slot="agent-detail-hero"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="agent-detail-loading"]')).not.toBeInTheDocument();

    // Knowledge panel has inline loading state
    expect(
      document.querySelector('[data-slot="agent-detail-kb-doc-list"][data-kb-kind="loading"]')
    ).toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 7: knowledge error → inline banner, NO shell error ──────────────
  it('Cell 7: knowledge error → inline error banner, page still renders', () => {
    const kbRefetch = vi.fn();
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ gameId: VALID_GAME_ID }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentKbDocsSpy.mockImplementation(() => ({
      ...kbDocsMockState,
      isError: true,
      refetch: kbRefetch,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="knowledge"]')!);
    });

    // Hero still present — no global error shell
    expect(document.querySelector('[data-slot="agent-detail-hero"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="agent-detail-error"]')).not.toBeInTheDocument();

    // Inline KB error banner
    expect(
      document.querySelector('[data-slot="agent-detail-kb-doc-list"][data-kb-kind="error"]')
    ).toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 8: knowledge success([]) → empty state CTA ─────────────────────
  it('Cell 8: knowledge success([]) → empty state (distinct from Cell 10 standalone)', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ gameId: VALID_GAME_ID }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentKbDocsSpy.mockImplementation(() => ({
      ...kbDocsMockState,
      data: [],
      isSuccess: true,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="knowledge"]')!);
    });

    // Empty state — NOT standalone
    const emptyEl = document.querySelector(
      '[data-slot="agent-detail-kb-doc-list"][data-kb-kind="empty"]'
    );
    expect(emptyEl).toBeInTheDocument();
    const standaloneEl = document.querySelector(
      '[data-slot="agent-detail-kb-doc-list"][data-kb-kind="standalone"]'
    );
    expect(standaloneEl).not.toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 9: knowledge success([...]) → grid populated ───────────────────
  it('Cell 9: knowledge success([doc]) → doc list rendered', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ gameId: VALID_GAME_ID }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentKbDocsSpy.mockImplementation(() => ({
      ...kbDocsMockState,
      data: [
        {
          id: 'doc-001',
          fileName: 'Catan Rules.pdf',
          uploadedAt: '2026-01-01T00:00:00Z',
          status: 'indexed' as const,
        },
      ],
      isSuccess: true,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="knowledge"]')!);
    });

    // Success: doc list rows rendered
    const docRows = document.querySelectorAll('[data-slot="agent-detail-kb-doc-list"] li');
    expect(docRows.length).toBeGreaterThan(0);

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 10 NEW: agent.gameId=null + tab=knowledge → standalone empty state ─
  it('Cell 10 NEW: agent.gameId=null → standalone empty state, KB fetch NEVER fires', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ gameId: null }), // standalone agent — no game association
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="knowledge"]')!);
    });

    // CRITICAL: KB docs hook must NEVER have been called with a gameId (enabled=true)
    const kbCallsWithGameId = useAgentKbDocsSpy.mock.calls.filter(
      c => c[0] !== undefined && c[0] !== ''
    );
    expect(
      kbCallsWithGameId,
      'KB fetch fired despite gameId=null (Cell 10 violation)'
    ).toHaveLength(0);

    // Standalone empty state rendered
    const standaloneEl = document.querySelector(
      '[data-slot="agent-detail-kb-doc-list"][data-kb-kind="standalone"]'
    );
    expect(standaloneEl, 'Standalone empty state not rendered for Cell 10').toBeInTheDocument();

    // NOT the generic empty state
    const emptyEl = document.querySelector(
      '[data-slot="agent-detail-kb-doc-list"][data-kb-kind="empty"]'
    );
    expect(emptyEl).not.toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── History tab: loading ──────────────────────────────────────────────────
  it('History tab loading → inline skeleton, hero still present', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ invocationCount: 5 }), // active (not draft)
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentThreadsSpy.mockImplementation(() => ({
      ...threadsMockState,
      isLoading: true,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="history"]')!);
    });

    expect(document.querySelector('[data-slot="agent-detail-hero"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="agent-detail-loading"]')).not.toBeInTheDocument();
    expect(
      document.querySelector(
        '[data-slot="agent-detail-chat-history-timeline"][data-history-kind="loading"]'
      )
    ).toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── History tab: error ────────────────────────────────────────────────────
  it('History tab error → inline error banner, NO shell error', () => {
    const threadsRefetch = vi.fn();
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ invocationCount: 5 }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentThreadsSpy.mockImplementation(() => ({
      ...threadsMockState,
      isError: true,
      refetch: threadsRefetch,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="history"]')!);
    });

    expect(document.querySelector('[data-slot="agent-detail-error"]')).not.toBeInTheDocument();
    expect(
      document.querySelector(
        '[data-slot="agent-detail-chat-history-timeline"][data-history-kind="error"]'
      )
    ).toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── History tab: success ──────────────────────────────────────────────────
  it('History tab success([thread]) → thread list rendered', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ invocationCount: 5 }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentThreadsSpy.mockImplementation(() => ({
      ...threadsMockState,
      data: [
        {
          id: 'thread-001',
          createdAt: '2026-04-01T10:00:00Z',
          messageCount: 3,
          firstMessagePreview: 'Come si gioca a Catan?',
        },
      ],
      isSuccess: true,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="history"]')!);
    });

    const threadEntries = document.querySelectorAll(
      '[data-slot="agent-detail-chat-history-timeline"] li'
    );
    expect(threadEntries.length).toBeGreaterThan(0);

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Settings tab: loading ─────────────────────────────────────────────────
  it('Settings tab loading → inline skeleton', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ invocationCount: 5 }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentConfigSpy.mockImplementation(() => ({
      ...configMockState,
      isLoading: true,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="settings"]')!);
    });

    expect(document.querySelector('[data-slot="agent-detail-hero"]')).toBeInTheDocument();
    expect(
      document.querySelector(
        '[data-slot="agent-detail-settings-form"][data-settings-kind="loading"]'
      )
    ).toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Settings tab: error ───────────────────────────────────────────────────
  it('Settings tab error → inline error banner, NO shell error', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ invocationCount: 5 }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentConfigSpy.mockImplementation(() => ({
      ...configMockState,
      isError: true,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="settings"]')!);
    });

    expect(document.querySelector('[data-slot="agent-detail-error"]')).not.toBeInTheDocument();
    expect(
      document.querySelector('[data-slot="agent-detail-settings-form"][data-settings-kind="error"]')
    ).toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Variant=archived → settings read-only, danger zone hidden ────────────
  it('Variant=archived → settings form read-only, danger zone NOT rendered', () => {
    useAgentSpy.mockImplementation(() => ({
      // isActive=false → archived variant
      data: makeAgent({ isActive: false, invocationCount: 10 }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));
    useAgentConfigSpy.mockImplementation(() => ({
      ...configMockState,
      data: null,
      isSuccess: true,
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    act(() => {
      fireEvent.click(document.querySelector('[data-tab-key="settings"]')!);
    });

    // Archived banner on hero
    expect(
      document.querySelector('[data-slot="agent-detail-hero-archived-banner"]')
    ).toBeInTheDocument();

    // Settings form read-only
    const readOnlyBanner = document.querySelector(
      '[data-slot="agent-detail-settings-form"] [role="status"]'
    );
    expect(readOnlyBanner).toBeInTheDocument();

    // Danger zone NOT rendered for archived variant
    const dangerZone = document.querySelector('[data-slot="agent-detail-danger-zone"]');
    expect(dangerZone).not.toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Variant=draft → performance/history tabs LOCKED ──────────────────────
  it('Variant=draft → performance/history tabs show locked banner', () => {
    useAgentSpy.mockImplementation(() => ({
      // isActive=true + invocationCount=0 → draft variant
      data: makeAgent({ isActive: true, invocationCount: 0 }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // Draft banner on hero
    expect(
      document.querySelector('[data-slot="agent-detail-hero-setup-banner"]')
    ).toBeInTheDocument();

    // Performance tab panel: locked banner
    act(() => {
      const perfTab = document.querySelector('[data-tab-key="performance"]');
      // Locked tab should have disabled attribute
      expect(perfTab).toHaveAttribute('disabled');
    });

    // History tab should also be locked
    const historyTab = document.querySelector('[data-tab-key="history"]');
    expect(historyTab).toHaveAttribute('disabled');

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── ?state=loading URL override ──────────────────────────────────────────
  it('?state=loading URL override → loading shell regardless of real agent data', () => {
    searchParamsState.state = 'loading';
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent(), // Real data exists but override wins
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    expect(document.querySelector('[data-slot="agent-detail-loading"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="agent-detail-hero"]')).not.toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── ?state=error URL override ────────────────────────────────────────────
  it('?state=error URL override → error shell regardless of real agent data', () => {
    searchParamsState.state = 'error';
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent(), // Real data but override wins
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    expect(document.querySelector('[data-slot="agent-detail-error"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="agent-detail-hero"]')).not.toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── ?state=not-found URL override ────────────────────────────────────────
  it('?state=not-found URL override → not-found shell regardless of real agent data', () => {
    searchParamsState.state = 'not-found';
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent(), // Real data but override wins
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    expect(document.querySelector('[data-slot="agent-detail-not-found"]')).toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Visual fixture short-circuit ─────────────────────────────────────────
  it('IS_VISUAL_TEST_BUILD=true → fixture short-circuit, default render immediately', () => {
    mockIsVisualTestBuild = true;
    mockFixtureData = makeAgent({ name: 'Fixture Agent' });

    // Agent query returns loading — fixture should override
    useAgentSpy.mockImplementation(() => ({
      data: undefined,
      isLoading: true,
      isError: false,
      isSuccess: false,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // Fixture bypasses loading shell — default render
    expect(document.querySelector('[data-slot="agent-detail-loading"]')).not.toBeInTheDocument();
    expect(document.querySelector('[data-slot="agent-detail-hero"]')).toBeInTheDocument();

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Cell 10 corollary: Cell 4 guard verified independently ───────────────
  it('Cell 4 + tab=knowledge: agent success(null) → not-found, KB NEVER fires', () => {
    useAgentSpy.mockImplementation(() => ({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // Not-found shell rendered — no default render to interact with
    expect(document.querySelector('[data-slot="agent-detail-not-found"]')).toBeInTheDocument();

    // KB docs must never fire
    const kbCallsWithGameId = useAgentKbDocsSpy.mock.calls.filter(
      c => c[0] !== undefined && c[0] !== ''
    );
    expect(kbCallsWithGameId, 'KB fired despite Cell 4 (success null)').toHaveLength(0);

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Tab a11y contract verification ───────────────────────────────────────
  it('Tab a11y: tablist role + tabpanel role + aria-labelledby wired correctly', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent(),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // Tablist exists
    const tablist = document.querySelector('[role="tablist"]');
    expect(tablist).toBeInTheDocument();

    // Tab buttons have correct roles
    const tabs = document.querySelectorAll('[role="tab"]');
    expect(tabs.length).toBe(5); // identity/knowledge/performance/history/settings

    // Tab panels have aria-labelledby wired to tab ids
    const identityPanel = document.getElementById('agent-detail-panel-identity');
    expect(identityPanel).toBeInTheDocument();
    expect(identityPanel).toHaveAttribute('aria-labelledby', 'agent-detail-tab-identity');

    assertKbDocsNeverCalledWithBadGameId();
  });

  // ─── Variant=active: Play CTA present, no banners ─────────────────────────
  it('Variant=active: Play CTA visible, no setup/archived banners', () => {
    useAgentSpy.mockImplementation(() => ({
      data: makeAgent({ isActive: true, invocationCount: 10 }),
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    }));

    renderWithIntl(<AgentDetailViewV2 agentId={VALID_AGENT_ID} />);

    // No draft/archived banners
    expect(
      document.querySelector('[data-slot="agent-detail-hero-setup-banner"]')
    ).not.toBeInTheDocument();
    expect(
      document.querySelector('[data-slot="agent-detail-hero-archived-banner"]')
    ).not.toBeInTheDocument();

    // data-variant on hero
    const heroEl = document.querySelector('[data-slot="agent-detail-hero"]');
    expect(heroEl).toHaveAttribute('data-variant', 'active');

    assertKbDocsNeverCalledWithBadGameId();
  });
});
