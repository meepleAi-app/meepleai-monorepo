/**
 * ProposalsTable Component Tests - Issue #3182
 *
 * Tests for proposals table with View action and status-based UI.
 * Updated after agent system simplification: Typology → AgentDefinitionDto.
 */

import { render, screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { vi } from 'vitest';

import type { AgentDefinitionDto } from '@/lib/api/schemas/agent-definitions.schemas';

import { ProposalsTable } from '../_components/ProposalsTable';

// Mock router
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
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

const activeDefinition = makeDefinition({ id: 'active-1', name: 'Active Agent', isActive: true });
const inactiveDefinition = makeDefinition({ id: 'draft-1', name: 'Draft Agent', isActive: false });

function renderComponent(proposals: AgentDefinitionDto[]) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  const mockRouter = { push: vi.fn() };
  vi.mocked(useRouter).mockReturnValue(mockRouter as any);

  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <ProposalsTable proposals={proposals} />
      </QueryClientProvider>
    ),
    mockRouter,
  };
}

describe('ProposalsTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows View button for active definitions', () => {
    renderComponent([activeDefinition]);

    expect(screen.getByRole('button', { name: /View/ })).toBeInTheDocument();
  });

  it('shows View button for inactive definitions', () => {
    renderComponent([inactiveDefinition]);

    expect(screen.getByRole('button', { name: /View/ })).toBeInTheDocument();
  });

  it('navigates to admin agent-definitions page on View click', async () => {
    const user = userEvent.setup();
    const { mockRouter } = renderComponent([activeDefinition]);

    const viewButton = screen.getByRole('button', { name: /View/ });
    await user.click(viewButton);

    expect(mockRouter.push).toHaveBeenCalledWith('/admin/agent-definitions/active-1');
  });

  it('shows Approved badge for active definitions', () => {
    renderComponent([activeDefinition]);

    expect(screen.getByText('Approved')).toBeInTheDocument();
  });

  it('shows Draft badge for inactive definitions', () => {
    renderComponent([inactiveDefinition]);

    expect(screen.getByText('Draft')).toBeInTheDocument();
  });

  it('shows empty state when no definitions match filter', () => {
    renderComponent([]);

    expect(screen.getByText('No definitions match your filters')).toBeInTheDocument();
  });

  it('displays strategy name when present', () => {
    renderComponent([activeDefinition]);

    expect(screen.getByText(/HybridSearch/)).toBeInTheDocument();
  });

  it('displays definition name', () => {
    renderComponent([activeDefinition, inactiveDefinition]);

    expect(screen.getByText('Active Agent')).toBeInTheDocument();
    expect(screen.getByText('Draft Agent')).toBeInTheDocument();
  });
});
