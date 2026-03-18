/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

import { AgentsSidebar } from '../AgentsSidebar';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// ---------------------------------------------------------------------------
// Mock useAgents hook
// ---------------------------------------------------------------------------

const mockUseAgents = vi.fn();

vi.mock('@/hooks/queries/useAgents', () => ({
  useAgents: (...args: unknown[]) => mockUseAgents(...args),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() }),
  usePathname: () => '/dashboard',
}));

// ---------------------------------------------------------------------------
// Mock MeepleCard (avoid rendering full component tree)
// ---------------------------------------------------------------------------

vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({
    title,
    entity,
    variant,
    agentStatus,
  }: {
    title: string;
    entity: string;
    variant: string;
    agentStatus?: string;
  }) => (
    <div
      data-testid="meeple-card"
      data-entity={entity}
      data-variant={variant}
      data-agent-status={agentStatus}
    >
      {title}
    </div>
  ),
}));

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function createAgent(overrides: Partial<AgentDto> = {}): AgentDto {
  return {
    id: crypto.randomUUID(),
    name: 'Test Agent',
    type: 'qa',
    strategyName: 'rag-hybrid',
    strategyParameters: {},
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    lastInvokedAt: null,
    invocationCount: 0,
    isRecentlyUsed: false,
    isIdle: false,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('AgentsSidebar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('has correct data-testid on root element', () => {
    mockUseAgents.mockReturnValue({ data: [], isLoading: false });
    render(<AgentsSidebar />);
    expect(screen.getByTestId('agents-sidebar')).toBeInTheDocument();
  });

  it('shows skeleton when loading', () => {
    mockUseAgents.mockReturnValue({ data: undefined, isLoading: true });
    render(<AgentsSidebar />);
    expect(screen.getByTestId('agents-sidebar-skeleton')).toBeInTheDocument();
    expect(screen.queryByTestId('meeple-card')).not.toBeInTheDocument();
  });

  it('shows empty state when no agents', () => {
    mockUseAgents.mockReturnValue({ data: [], isLoading: false });
    render(<AgentsSidebar />);
    expect(screen.getByTestId('agents-sidebar-empty')).toBeInTheDocument();
    expect(screen.getByText('Nessun agente configurato')).toBeInTheDocument();
  });

  it('renders agent cards from API data', () => {
    const agents = [
      createAgent({ id: 'a1', name: 'Rules Agent', type: 'rules' }),
      createAgent({ id: 'a2', name: 'Strategy Agent', type: 'strategy' }),
    ];
    mockUseAgents.mockReturnValue({ data: agents, isLoading: false });

    render(<AgentsSidebar />);

    const cards = screen.getAllByTestId('meeple-card');
    expect(cards).toHaveLength(2);
    expect(screen.getByText('Rules Agent')).toBeInTheDocument();
    expect(screen.getByText('Strategy Agent')).toBeInTheDocument();
  });

  it('renders MeepleCard with entity="agent" and variant="compact"', () => {
    const agents = [createAgent({ id: 'a1', name: 'My Agent' })];
    mockUseAgents.mockReturnValue({ data: agents, isLoading: false });

    render(<AgentsSidebar />);

    const card = screen.getByTestId('meeple-card');
    expect(card).toHaveAttribute('data-entity', 'agent');
    expect(card).toHaveAttribute('data-variant', 'compact');
  });

  it('maps active agent to "active" status', () => {
    const agents = [createAgent({ isActive: true, isIdle: false })];
    mockUseAgents.mockReturnValue({ data: agents, isLoading: false });

    render(<AgentsSidebar />);

    const card = screen.getByTestId('meeple-card');
    expect(card).toHaveAttribute('data-agent-status', 'active');
  });

  it('maps inactive agent to "idle" status', () => {
    const agents = [createAgent({ isActive: false, isIdle: false })];
    mockUseAgents.mockReturnValue({ data: agents, isLoading: false });

    render(<AgentsSidebar />);

    const card = screen.getByTestId('meeple-card');
    expect(card).toHaveAttribute('data-agent-status', 'idle');
  });

  it('maps idle agent to "idle" status', () => {
    const agents = [createAgent({ isActive: true, isIdle: true })];
    mockUseAgents.mockReturnValue({ data: agents, isLoading: false });

    render(<AgentsSidebar />);

    const card = screen.getByTestId('meeple-card');
    expect(card).toHaveAttribute('data-agent-status', 'idle');
  });

  it('displays section title "I tuoi Agenti"', () => {
    mockUseAgents.mockReturnValue({ data: [], isLoading: false });
    render(<AgentsSidebar />);
    expect(screen.getByText('I tuoi Agenti')).toBeInTheDocument();
  });
});
