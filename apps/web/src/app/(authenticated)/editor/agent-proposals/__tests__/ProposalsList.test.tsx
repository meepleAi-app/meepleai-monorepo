/**
 * ProposalsList Component Tests - Issue #3182
 *
 * Tests for proposals list with filters and status management.
 * Updated after agent system simplification: Typology → AgentDefinitionDto.
 */

import { render, screen, waitFor } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';

import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';
import type { AgentDefinitionDto } from '@/lib/api/schemas/agent-definitions.schemas';

import { ProposalsList } from '../_components/ProposalsList';

// Mock Next.js router
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({
    push: vi.fn(),
    back: vi.fn(),
  })),
}));

// Mock API
vi.mock('@/lib/api/agent-typologies.api', () => ({
  agentTypologiesApi: {
    getMyProposals: vi.fn(),
  },
}));

const makeDefinition = (overrides: Partial<AgentDefinitionDto> = {}): AgentDefinitionDto => ({
  id: 'def-1',
  name: 'Test Agent',
  description: 'Test description',
  type: 'RagAgent',
  config: { model: 'test-model', maxTokens: 1024, temperature: 0.7 },
  strategyName: 'HybridSearch',
  strategyParameters: {},
  prompts: [],
  tools: [],
  kbCardIds: [],
  chatLanguage: 'auto',
  isActive: true,
  status: 0,
  createdAt: new Date().toISOString(),
  updatedAt: null,
  ...overrides,
});

const mockProposals: AgentDefinitionDto[] = [
  makeDefinition({
    id: '1',
    name: 'Rules Assistant',
    description: 'Helps with game rules',
    isActive: false,
  }),
  makeDefinition({
    id: '2',
    name: 'Setup Helper',
    description: 'Assists with game setup',
    isActive: true,
  }),
  makeDefinition({
    id: '3',
    name: 'Strategy Guide',
    description: 'Provides strategy tips',
    isActive: true,
  }),
];

function renderComponent() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <ProposalsList />
    </QueryClientProvider>
  );
}

describe('ProposalsList', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders loading state initially', () => {
    vi.mocked(agentTypologiesApi.getMyProposals).mockReturnValue(
      new Promise(() => {}) // Never resolves
    );

    renderComponent();

    // Multiple skeleton divs have role="generic", check any has animate-pulse
    const loadingElements = screen.getAllByRole('generic');
    const hasAnimatePulse = loadingElements.some(el => el.classList.contains('animate-pulse'));
    expect(hasAnimatePulse).toBe(true);
  });

  it('renders proposals after loading', async () => {
    vi.mocked(agentTypologiesApi.getMyProposals).mockResolvedValue(mockProposals);

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('Rules Assistant')).toBeInTheDocument();
      expect(screen.getByText('Setup Helper')).toBeInTheDocument();
      expect(screen.getByText('Strategy Guide')).toBeInTheDocument();
    });
  });

  it('filters proposals by status', async () => {
    vi.mocked(agentTypologiesApi.getMyProposals).mockResolvedValue(mockProposals);
    const user = userEvent.setup();

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('Rules Assistant')).toBeInTheDocument();
    });

    // Filter by Draft (isActive: false)
    const draftButton = screen.getByRole('button', { name: 'Draft' });
    await user.click(draftButton);

    await waitFor(() => {
      expect(screen.getByText('Rules Assistant')).toBeInTheDocument();
      expect(screen.queryByText('Setup Helper')).not.toBeInTheDocument();
      expect(screen.queryByText('Strategy Guide')).not.toBeInTheDocument();
    });
  });

  it('filters proposals by search query', async () => {
    vi.mocked(agentTypologiesApi.getMyProposals).mockResolvedValue(mockProposals);
    const user = userEvent.setup();

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('Rules Assistant')).toBeInTheDocument();
    });

    // Search for "setup"
    const searchInput = screen.getByPlaceholderText('Search proposals...');
    await user.type(searchInput, 'setup');

    await waitFor(() => {
      expect(screen.queryByText('Rules Assistant')).not.toBeInTheDocument();
      expect(screen.getByText('Setup Helper')).toBeInTheDocument();
      expect(screen.queryByText('Strategy Guide')).not.toBeInTheDocument();
    });
  });

  it('shows empty state when no proposals exist', async () => {
    vi.mocked(agentTypologiesApi.getMyProposals).mockResolvedValue([]);

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('No proposals yet')).toBeInTheDocument();
      expect(screen.getByText(/Create your first agent typology proposal/)).toBeInTheDocument();
    });
  });

  it('shows error state on API failure', async () => {
    vi.mocked(agentTypologiesApi.getMyProposals).mockRejectedValue(new Error('API Error'));

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('Failed to load proposals')).toBeInTheDocument();
      expect(screen.getByText('API Error')).toBeInTheDocument();
    });
  });

  it('displays result count correctly', async () => {
    vi.mocked(agentTypologiesApi.getMyProposals).mockResolvedValue(mockProposals);

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText('Showing 3 of 3 proposals')).toBeInTheDocument();
    });
  });
});
