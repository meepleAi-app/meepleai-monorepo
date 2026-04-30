/**
 * Wave B.2 (Issue #634) — AgentsLibraryView orchestrator tests.
 *
 * Mirrors Wave B.1 GamesLibraryView tests: stub `useAgents` + `next/navigation`
 * search params + i18n via IntlProvider seeded with the actual `pages.agents.*`
 * keys from `it.json`. The orchestrator is the only stateful piece — the 4
 * v2 components (AgentsHero, AgentFilters, AgentsResultsGrid, EmptyAgents) are
 * pure label-driven (covered separately).
 *
 * Contract under test (spec §3.4 + plan §5.1):
 *   - 5-state FSM: default | loading | empty | filtered-empty | error
 *   - `?state=...` URL override gated by NODE_ENV !== 'production'
 *   - `clearFilters` resets `query=''` + `status='all'` (sort preserved per AC-7)
 *   - Stats derived from agents via `deriveStats` (3 attivo, 2 inSetup, 1 archiviato, 306 totalInvocations)
 *   - AgentsHero/AgentFilters/AgentsResultsGrid/EmptyAgents wired with resolved labels
 *   - `onCreateAgent` callback prop invoked when hero CTA clicked (AC-13: page.tsx hosts modal)
 */

import { render, screen, fireEvent, within } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// ─── next/navigation mocks ────────────────────────────────────────────────

const searchParamsState = { value: '' };
const routerPush = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => {
      if (key === 'state') return searchParamsState.value || null;
      return null;
    },
  }),
  useRouter: () => ({ push: routerPush, replace: vi.fn() }),
  usePathname: () => '/agents',
}));

// ─── useAgents mock ───────────────────────────────────────────────────────

type MockAgentsReturn = {
  data?: AgentDto[];
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
  refetch?: () => void;
};

const useAgentsMock = vi.fn<[], MockAgentsReturn>();

vi.mock('@/hooks/queries/useAgents', () => ({
  useAgents: () => useAgentsMock(),
}));

// ─── react-intl messages (subset matching it.json `pages.agents.*`) ───────

const MESSAGES: Record<string, string> = {
  'pages.agents.hero.eyebrow': 'I tuoi agenti AI',
  'pages.agents.hero.title': 'Studio agenti',
  'pages.agents.hero.subtitle':
    'Crea, gestisci e affina i tuoi agenti AI specializzati per ogni gioco.',
  'pages.agents.hero.cta.create': 'Crea agente',
  'pages.agents.hero.stats.attivo': 'Attivi',
  'pages.agents.hero.stats.inSetup': 'In setup',
  'pages.agents.hero.stats.archiviato': 'Archiviati',
  'pages.agents.hero.stats.totalInvocations': 'Invocazioni totali',
  'pages.agents.filters.search.placeholder': 'Cerca per nome, tipo o strategia…',
  'pages.agents.filters.search.ariaLabel': 'Cerca tra gli agenti',
  'pages.agents.filters.search.clearAriaLabel': 'Cancella la ricerca',
  'pages.agents.filters.status.label': 'Stato',
  'pages.agents.filters.status.all': 'Tutti',
  'pages.agents.filters.status.attivo': 'Attivi',
  'pages.agents.filters.status.in-setup': 'In setup',
  'pages.agents.filters.status.archiviato': 'Archiviati',
  'pages.agents.filters.sort.label': 'Ordina per',
  'pages.agents.filters.sort.recent': 'Più recenti',
  'pages.agents.filters.sort.alpha': 'A-Z',
  'pages.agents.filters.sort.used': 'Più usati',
  'pages.agents.filters.resultCount':
    '{count, plural, =0 {Nessun risultato} =1 {1 agente} other {# agenti}}',
  'pages.agents.empty.default.title': 'Nessun agente ancora',
  'pages.agents.empty.default.subtitle':
    "Crea il tuo primo agente AI per iniziare a giocare con l'assistente.",
  'pages.agents.empty.default.cta': 'Crea agente',
  'pages.agents.empty.filtered.title': 'Nessun agente trovato',
  'pages.agents.empty.filtered.subtitle': 'Prova a modificare i filtri o la ricerca.',
  'pages.agents.empty.filtered.cta': 'Cancella filtri',
  'pages.agents.empty.error.title': 'Impossibile caricare gli agenti',
  'pages.agents.empty.error.subtitle': 'Si è verificato un errore. Riprova tra qualche istante.',
  'pages.agents.empty.error.cta': 'Riprova',
};

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

// ─── fixture helpers ──────────────────────────────────────────────────────

const NOW = '2026-04-30T10:00:00.000Z';

function makeAgent(overrides: Partial<AgentDto> & Pick<AgentDto, 'id' | 'name'>): AgentDto {
  return {
    type: 'rag',
    strategyName: 'HybridSearch',
    strategyParameters: {},
    isActive: true,
    createdAt: NOW,
    lastInvokedAt: null,
    invocationCount: 0,
    isRecentlyUsed: false,
    isIdle: false,
    gameId: null,
    gameName: null,
    createdByUserId: null,
    ...overrides,
  };
}

// 6 agents → 3 attivo + 2 in-setup + 1 archiviato, totalInvocations = 306.
const AGENTS_6: AgentDto[] = [
  makeAgent({
    id: '00000000-0000-4000-8000-000000000001',
    name: 'Catan Coach',
    type: 'Strategist',
    strategyName: 'HybridSearch',
    isActive: true,
    invocationCount: 142,
    gameName: 'Catan',
  }),
  makeAgent({
    id: '00000000-0000-4000-8000-000000000002',
    name: 'Wingspan Rules Expert',
    type: 'Rules',
    strategyName: 'VectorOnly',
    isActive: true,
    invocationCount: 87,
    gameName: 'Wingspan',
  }),
  makeAgent({
    id: '00000000-0000-4000-8000-000000000003',
    name: 'Terraforming Mars Setup',
    type: 'Setup',
    strategyName: 'CitationValidation',
    isActive: true,
    invocationCount: 24,
    gameName: 'Terraforming Mars',
  }),
  makeAgent({
    id: '00000000-0000-4000-8000-000000000004',
    name: 'Azul Tutor',
    type: 'Tutor',
    strategyName: 'HybridSearch',
    isActive: true,
    invocationCount: 0,
    gameName: 'Azul',
  }),
  makeAgent({
    id: '00000000-0000-4000-8000-000000000005',
    name: 'Carcassonne Strategist',
    type: 'Strategist',
    strategyName: 'HybridSearch',
    isActive: true,
    invocationCount: 0,
    gameName: 'Carcassonne',
  }),
  makeAgent({
    id: '00000000-0000-4000-8000-000000000006',
    name: 'Old Pandemic Helper',
    type: 'Rules',
    strategyName: 'VectorOnly',
    isActive: false,
    invocationCount: 53,
    gameName: 'Pandemic',
  }),
];

import { AgentsLibraryView } from '../AgentsLibraryView';

describe('AgentsLibraryView (Wave B.2)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    searchParamsState.value = '';
    useAgentsMock.mockReturnValue({
      data: AGENTS_6,
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
  });

  // ─── FSM: default ──────────────────────────────────────────────────────

  it('renders AgentsHero + AgentFilters + AgentsResultsGrid in default state', () => {
    const { container } = renderWithIntl(<AgentsLibraryView />);
    expect(container.querySelector('[data-slot="agents-library-hero"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="agents-filters"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="agents-results-grid"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="agents-empty-state"]')).not.toBeInTheDocument();
  });

  it('derives hero stats from agents via deriveStats (3 attivo, 2 inSetup, 1 archiviato, 306 totalInvocations)', () => {
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const stats = container.querySelectorAll('[data-slot="agents-hero-stat-value"]');
    expect(stats).toHaveLength(4);
    // Order = attivo, inSetup, archiviato, totalInvocations (per spec §3.2 stat ordering)
    expect(stats[0]).toHaveTextContent('3');
    expect(stats[1]).toHaveTextContent('2');
    expect(stats[2]).toHaveTextContent('1');
    expect(stats[3]).toHaveTextContent('306');
  });

  // ─── FSM: loading ──────────────────────────────────────────────────────

  it('renders kind="loading" EmptyAgents when useAgents.isLoading=true', () => {
    useAgentsMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const empty = container.querySelector('[data-slot="agents-empty-state"]');
    expect(empty).not.toBeNull();
    expect(empty?.getAttribute('data-kind')).toBe('loading');
    expect(container.querySelector('[data-slot="agents-results-grid"]')).not.toBeInTheDocument();
  });

  // ─── FSM: error ────────────────────────────────────────────────────────

  it('renders kind="error" EmptyAgents when useAgents.isError=true', () => {
    useAgentsMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error('boom'),
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const empty = container.querySelector('[data-slot="agents-empty-state"]');
    expect(empty?.getAttribute('data-kind')).toBe('error');
    expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
  });

  // ─── FSM: empty (no agents at all) ─────────────────────────────────────

  it('renders kind="empty" EmptyAgents when data is empty array', () => {
    useAgentsMock.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const empty = container.querySelector('[data-slot="agents-empty-state"]') as HTMLElement;
    expect(empty?.getAttribute('data-kind')).toBe('empty');
    // Scope to empty state — Hero also renders a "Crea agente" CTA (same i18n in production).
    expect(within(empty).getByRole('button', { name: 'Crea agente' })).toBeInTheDocument();
  });

  // ─── State override (NODE_ENV !== 'production') ───────────────────────

  it('?state=loading override forces kind="loading" surface (NODE_ENV=test)', () => {
    searchParamsState.value = 'loading';
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const empty = container.querySelector('[data-slot="agents-empty-state"]');
    expect(empty?.getAttribute('data-kind')).toBe('loading');
  });

  it('?state=empty override forces kind="empty" surface', () => {
    searchParamsState.value = 'empty';
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const empty = container.querySelector('[data-slot="agents-empty-state"]');
    expect(empty?.getAttribute('data-kind')).toBe('empty');
  });

  it('?state=filtered-empty override forces kind="filtered-empty" surface', () => {
    searchParamsState.value = 'filtered-empty';
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const empty = container.querySelector('[data-slot="agents-empty-state"]');
    expect(empty?.getAttribute('data-kind')).toBe('filtered-empty');
  });

  it('?state=error override forces kind="error" surface', () => {
    searchParamsState.value = 'error';
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const empty = container.querySelector('[data-slot="agents-empty-state"]');
    expect(empty?.getAttribute('data-kind')).toBe('error');
  });

  it('ignores unknown ?state= values and falls back to real FSM', () => {
    searchParamsState.value = 'totally-bogus';
    const { container } = renderWithIntl(<AgentsLibraryView />);
    expect(container.querySelector('[data-slot="agents-results-grid"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="agents-empty-state"]')).not.toBeInTheDocument();
  });

  // ─── clearFilters semantics ─────────────────────────────────────────────

  it('clearFilters CTA from filtered-empty resets query+status (sort preserved) and drops ?state=', () => {
    searchParamsState.value = 'filtered-empty';
    const { container, rerender } = renderWithIntl(<AgentsLibraryView />);
    const cta = screen.getByRole('button', { name: 'Cancella filtri' });
    fireEvent.click(cta);

    // Orchestrator should call router.push to drop the ?state= override.
    expect(routerPush).toHaveBeenCalledWith('/agents');

    // After clearing override, surface returns to default (with all 6 agents).
    searchParamsState.value = '';
    rerender(
      <IntlProvider locale="it" messages={MESSAGES}>
        <AgentsLibraryView />
      </IntlProvider>
    );
    expect(container.querySelector('[data-slot="agents-results-grid"]')).toBeInTheDocument();
  });

  // ─── results count + grid wiring ────────────────────────────────────────

  it('renders one MeepleCard per agent in default state (6 cards)', () => {
    const { container } = renderWithIntl(<AgentsLibraryView />);
    const cards = container.querySelectorAll('[data-entity="agent"]');
    expect(cards).toHaveLength(6);
  });

  // ─── onCreateAgent callback prop (AC-13: page.tsx hosts modal v1) ──────

  it('invokes onCreateAgent prop when Hero CTA clicked', () => {
    const onCreateAgent = vi.fn();
    renderWithIntl(<AgentsLibraryView onCreateAgent={onCreateAgent} />);
    // Hero CTA label resolves to "Crea agente". In default state Hero is the
    // only place this label appears (EmptyAgents not rendered).
    fireEvent.click(screen.getByRole('button', { name: 'Crea agente' }));
    expect(onCreateAgent).toHaveBeenCalledTimes(1);
  });
});
