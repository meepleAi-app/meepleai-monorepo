/**
 * RecentAgentsSidebar — Unit Tests
 * Dashboard v2 "Il Tavolo"
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { RecentAgentsSidebar } from '../RecentAgentsSidebar';
import type { RecentAgentItem } from '../RecentAgentsSidebar';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const makeAgent = (id: string, name: string, isReady = true): RecentAgentItem => ({
  id,
  name,
  lastUsedAt: new Date(Date.now() - 5 * 60000).toISOString(), // 5 min ago
  isReady,
});

const MOCK_AGENTS: RecentAgentItem[] = [
  makeAgent('a1', 'Agente Regole', true),
  makeAgent('a2', 'Agente Strategia', false),
  makeAgent('a3', 'Agente FAQ', true),
];

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RecentAgentsSidebar', () => {
  it('renders section title with correct text', () => {
    render(<RecentAgentsSidebar agents={MOCK_AGENTS} />);

    expect(screen.getByTestId('sidebar-agents')).toBeInTheDocument();
    expect(screen.getByText('⚡ Agenti Recenti')).toBeInTheDocument();
  });

  it('renders correct number of items (max 3)', () => {
    const fourAgents = [...MOCK_AGENTS, makeAgent('a4', 'Extra Agent')];
    render(<RecentAgentsSidebar agents={fourAgents} />);

    expect(screen.getByText('Agente Regole')).toBeInTheDocument();
    expect(screen.getByText('Agente Strategia')).toBeInTheDocument();
    expect(screen.getByText('Agente FAQ')).toBeInTheDocument();
    expect(screen.queryByText('Extra Agent')).not.toBeInTheDocument();
  });

  it('renders all 3 items when exactly 3 agents provided', () => {
    render(<RecentAgentsSidebar agents={MOCK_AGENTS} />);

    expect(screen.getByText('Agente Regole')).toBeInTheDocument();
    expect(screen.getByText('Agente Strategia')).toBeInTheDocument();
    expect(screen.getByText('Agente FAQ')).toBeInTheDocument();
  });

  it('hides section when agents array is empty', () => {
    const { container } = render(<RecentAgentsSidebar agents={[]} />);

    expect(screen.queryByTestId('sidebar-agents')).not.toBeInTheDocument();
    expect(container.firstChild).toBeNull();
  });

  it('renders skeletons when loading', () => {
    render(<RecentAgentsSidebar agents={[]} loading />);

    expect(screen.getByTestId('sidebar-agents')).toBeInTheDocument();
    const skeletons = screen
      .getAllByRole('generic')
      .filter(el => el.className.includes('animate-pulse'));
    expect(skeletons.length).toBeGreaterThanOrEqual(2);
    expect(screen.queryByText('Agente Regole')).not.toBeInTheDocument();
  });

  it('shows section title even when loading', () => {
    render(<RecentAgentsSidebar agents={[]} loading />);

    expect(screen.getByText('⚡ Agenti Recenti')).toBeInTheDocument();
  });

  it('renders relative time for each agent', () => {
    render(<RecentAgentsSidebar agents={[makeAgent('x', 'Test Agent')]} />);

    expect(screen.getByText('5 min fa')).toBeInTheDocument();
  });
});
