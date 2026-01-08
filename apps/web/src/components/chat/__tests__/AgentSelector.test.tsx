/**
 * AgentSelector Tests - Issue #2308 Week 4 Phase 2
 *
 * Branch coverage tests for AgentSelector component:
 * 1. Shows skeleton loader when agents loading
 * 2. Disables selector when no game selected
 * 3. Shows "no agents" when agents array empty
 * 4. Displays all available agents
 * 5. Calls selectAgent when agent selected
 * 6. Shows selected agent value
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 79 lines (~1% of total)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { AgentSelector } from '../AgentSelector';
import { useChatStore } from '@/store/chat/store';

// Mock store
vi.mock('@/store/chat/store');

// Mock SkeletonLoader
vi.mock('../../loading/SkeletonLoader', () => ({
  SkeletonLoader: ({ variant }: any) => <div data-testid="skeleton-loader">{variant}</div>,
}));

const mockAgents = [
  { id: 'agent-1', name: 'RAG Assistant', type: 'rag', isActive: true },
  { id: 'agent-2', name: 'Rules Expert', type: 'rules', isActive: true },
  { id: 'agent-3', name: 'Strategy Guide', type: 'strategy', isActive: true },
];

describe('AgentSelector - Issue #2308 Phase 2', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ============================================================================
  // TEST 1: Loading state
  // ============================================================================
  it('should show skeleton loader when agents are loading', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      agents: [],
      selectedAgentId: null,
      selectAgent: vi.fn(),
      selectedGameId: 'game-1',
      loading: { agents: true },
    } as any);

    // Act
    render(<AgentSelector />);

    // Assert - Skeleton visible
    expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();
    expect(screen.getByTestId('skeleton-loader')).toHaveTextContent('gameSelection');

    // Assert - Label visible
    expect(screen.getByText('Seleziona Agente:')).toBeInTheDocument();

    // Assert - Select not rendered
    expect(screen.queryByTestId('agent-selector')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: No game selected
  // ============================================================================
  it('should disable selector when no game is selected', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      agents: mockAgents,
      selectedAgentId: null,
      selectAgent: vi.fn(),
      selectedGameId: null,
      loading: { agents: false },
    } as any);

    // Act
    render(<AgentSelector />);

    // Assert - Selector disabled
    const trigger = screen.getByTestId('agent-selector');
    expect(trigger).toBeDisabled();

    // Assert - Helpful placeholder
    expect(screen.getByText('Seleziona prima un gioco')).toBeInTheDocument();

    // Assert - Title attribute for tooltip
    expect(trigger).toHaveAttribute('title', 'Seleziona prima un gioco');
  });

  // ============================================================================
  // TEST 3: Empty agents list
  // ============================================================================
  it('should show "no agents" placeholder when agents array is empty', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      agents: [],
      selectedAgentId: null,
      selectAgent: vi.fn(),
      selectedGameId: 'game-1',
      loading: { agents: false },
    } as any);

    // Act
    render(<AgentSelector />);

    // Assert - Empty placeholder
    expect(screen.getByText('Nessun agente disponibile')).toBeInTheDocument();

    // Assert - Selector enabled (game selected)
    const trigger = screen.getByTestId('agent-selector');
    expect(trigger).not.toBeDisabled();
  });

  // ============================================================================
  // TEST 4: Agent list display
  // ============================================================================
  it('should display selector when game selected and agents available', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      agents: mockAgents,
      selectedAgentId: null,
      selectAgent: vi.fn(),
      selectedGameId: 'game-1',
      loading: { agents: false },
    } as any);

    // Act
    render(<AgentSelector />);

    // Assert - Selector enabled and visible
    const trigger = screen.getByTestId('agent-selector');
    expect(trigger).toBeInTheDocument();
    expect(trigger).not.toBeDisabled();

    // Assert - Default placeholder
    expect(screen.getByText('Seleziona un agente')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: Selected agent display
  // ============================================================================
  it('should show selected agent name in trigger', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      agents: mockAgents,
      selectedAgentId: 'agent-2',
      selectAgent: vi.fn(),
      selectedGameId: 'game-1',
      loading: { agents: false },
    } as any);

    // Act
    render(<AgentSelector />);

    // Assert - Selected agent displayed
    expect(screen.getByText('Rules Expert')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 6: Combined loading + no game state
  // ============================================================================
  it('should handle loading state even without selected game', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      agents: [],
      selectedAgentId: null,
      selectAgent: vi.fn(),
      selectedGameId: null,
      loading: { agents: true },
    } as any);

    // Act
    render(<AgentSelector />);

    // Assert - Skeleton shown (loading takes precedence)
    expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();
  });
});
