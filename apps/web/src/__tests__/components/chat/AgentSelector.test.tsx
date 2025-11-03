/**
 * AgentSelector Component Tests
 *
 * Tests for the AgentSelector component that allows users to select
 * which AI agent to chat with for the selected game.
 *
 * Target Coverage: 90%+ (from 66.7%)
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { AgentSelector } from '../../../components/chat/AgentSelector';

// Mock the ChatProvider context
const mockUseChatContext = jest.fn();
jest.mock('../../../components/chat/ChatProvider', () => ({
  useChatContext: () => mockUseChatContext(),
}));

// Mock SkeletonLoader component
jest.mock('../../../components/loading/SkeletonLoader', () => ({
  SkeletonLoader: ({ variant }: { variant: string }) => (
    <div data-testid="skeleton-loader" data-variant={variant}>
      Loading...
    </div>
  ),
}));

/**
 * Helper to setup mock context with default values
 */
const setupMockContext = (overrides?: any) => {
  mockUseChatContext.mockReturnValue({
    agents: [],
    selectedAgentId: null,
    selectAgent: jest.fn(),
    selectedGameId: null,
    loading: { agents: false },
    ...overrides,
  });
};

describe('AgentSelector Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  /**
   * Test Group: Loading State
   */
  describe('Loading State', () => {
    it('displays skeleton loader when agents are loading', () => {
      setupMockContext({ loading: { agents: true } });
      render(<AgentSelector />);

      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();
      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('displays skeleton loader with correct variant', () => {
      setupMockContext({ loading: { agents: true } });
      render(<AgentSelector />);

      const skeleton = screen.getByTestId('skeleton-loader');
      expect(skeleton).toHaveAttribute('data-variant', 'gameSelection');
    });

    it('displays label during loading', () => {
      setupMockContext({ loading: { agents: true } });
      render(<AgentSelector />);

      expect(screen.getByText('Seleziona Agente:')).toBeInTheDocument();
    });

    it('does not display select element when loading', () => {
      setupMockContext({ loading: { agents: true } });
      render(<AgentSelector />);

      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    });
  });

  /**
   * Test Group: No Game Selected State
   */
  describe('No Game Selected State', () => {
    it('displays prompt to select game first when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      expect(screen.getByText('Seleziona prima un gioco')).toBeInTheDocument();
    });

    it('disables select when no game is selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });

    it('shows tooltip when hovering disabled select', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('title', 'Seleziona prima un gioco');
    });

    it('reduces opacity when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({ opacity: 0.6 });
    });

    it('shows not-allowed cursor when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({ cursor: 'not-allowed' });
    });
  });

  /**
   * Test Group: Empty Agents State
   */
  describe('Empty Agents State', () => {
    it('displays "no agents" message when agents array is empty', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [] });
      render(<AgentSelector />);

      expect(screen.getByText('Nessun agente disponibile')).toBeInTheDocument();
    });

    it('renders select element even when agents are empty', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [] });
      render(<AgentSelector />);

      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('does not display placeholder option when agents are empty', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [] });
      render(<AgentSelector />);

      expect(screen.queryByText('Seleziona un agente')).not.toBeInTheDocument();
    });

    it('select is not disabled when empty but game is selected', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [], loading: { agents: false } });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).not.toBeDisabled();
    });
  });

  /**
   * Test Group: Agents List Rendering
   */
  describe('Agents List Rendering', () => {
    it('renders list of available agents', () => {
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Catan Helper' },
        { id: 'agent-3', name: 'Risk Strategist' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      expect(screen.getByText('Chess Expert')).toBeInTheDocument();
      expect(screen.getByText('Catan Helper')).toBeInTheDocument();
      expect(screen.getByText('Risk Strategist')).toBeInTheDocument();
    });

    it('includes placeholder option when agents exist', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      expect(screen.getByText('Seleziona un agente')).toBeInTheDocument();
    });

    it('renders correct number of options (agents + placeholder)', () => {
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Catan Helper' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.options).toHaveLength(3); // 2 agents + 1 placeholder
    });

    it('renders agents in the order provided', () => {
      const agents = [
        { id: 'agent-1', name: 'Zzz Agent' },
        { id: 'agent-2', name: 'Aaa Agent' },
        { id: 'agent-3', name: 'Mmm Agent' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      // Skip first option (placeholder)
      expect(select.options[1].textContent).toBe('Zzz Agent');
      expect(select.options[2].textContent).toBe('Aaa Agent');
      expect(select.options[3].textContent).toBe('Mmm Agent');
    });

    it('uses agent.id as option value', () => {
      const agents = [{ id: 'agent-123', name: 'Test Agent' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const option = screen.getByText('Test Agent') as HTMLOptionElement;
      expect(option.value).toBe('agent-123');
    });
  });

  /**
   * Test Group: Agent Selection
   */
  describe('Agent Selection', () => {
    it('calls selectAgent when an agent is selected', () => {
      const selectAgent = jest.fn();
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents, selectAgent });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: 'agent-1' } });

      expect(selectAgent).toHaveBeenCalledWith('agent-1');
    });

    it('calls selectAgent with null when empty option is selected', () => {
      const selectAgent = jest.fn();
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({
        selectedGameId: 'game-1',
        agents,
        selectAgent,
        selectedAgentId: 'agent-1',
      });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: '' } });

      expect(selectAgent).toHaveBeenCalledWith(null);
    });

    it('handles multiple agent selections sequentially', () => {
      const selectAgent = jest.fn();
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Catan Helper' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents, selectAgent });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: 'agent-1' } });
      fireEvent.change(select, { target: { value: 'agent-2' } });

      expect(selectAgent).toHaveBeenCalledTimes(2);
      expect(selectAgent).toHaveBeenNthCalledWith(1, 'agent-1');
      expect(selectAgent).toHaveBeenNthCalledWith(2, 'agent-2');
    });

    it('allows deselecting by choosing placeholder', () => {
      const selectAgent = jest.fn();
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({
        selectedGameId: 'game-1',
        agents,
        selectAgent,
        selectedAgentId: 'agent-1',
      });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      fireEvent.change(select, { target: { value: '' } });

      expect(selectAgent).toHaveBeenCalledWith(null);
    });
  });

  /**
   * Test Group: Selected Agent State
   */
  describe('Selected Agent State', () => {
    it('displays currently selected agent', () => {
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Catan Helper' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents, selectedAgentId: 'agent-2' });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('agent-2');
    });

    it('displays empty value when no agent is selected', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents, selectedAgentId: null });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('');
    });

    it('handles undefined selectedAgentId', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents, selectedAgentId: undefined });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('');
    });

    it('updates value when selectedAgentId changes', () => {
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Catan Helper' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents, selectedAgentId: 'agent-1' });
      const { rerender } = render(<AgentSelector />);

      let select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('agent-1');

      // Update selected agent
      setupMockContext({ selectedGameId: 'game-1', agents, selectedAgentId: 'agent-2' });
      rerender(<AgentSelector />);

      select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.value).toBe('agent-2');
    });
  });

  /**
   * Test Group: Disabled State
   */
  describe('Disabled State', () => {
    it('disables select when agents are loading', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [], loading: { agents: true } });
      const { rerender } = render(<AgentSelector />);

      // Rerender with loaded state
      setupMockContext({
        selectedGameId: 'game-1',
        agents: [{ id: 'agent-1', name: 'Chess Expert' }],
        loading: { agents: false },
      });
      rerender(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).not.toBeDisabled();
    });

    it('disables select when no game is selected', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: null, agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });

    it('sets aria-busy when loading', () => {
      setupMockContext({
        selectedGameId: 'game-1',
        agents: [{ id: 'agent-1', name: 'Chess Expert' }],
        loading: { agents: false },
      });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('aria-busy', 'false');
    });

    it('changes cursor when disabled', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents, loading: { agents: false } });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({ cursor: 'pointer' });
    });

    it('has not-allowed cursor when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({ cursor: 'not-allowed' });
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('has proper label association', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('id', 'agentSelect');

      const label = screen.getByLabelText('Seleziona Agente:');
      expect(label).toBe(select);
    });

    it('has correct aria-busy attribute', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents, loading: { agents: false } });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('aria-busy', 'false');
    });

    it('has title attribute when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('title', 'Seleziona prima un gioco');
    });

    it('does not have title attribute when game is selected', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).not.toHaveAttribute('title');
    });

    it('uses semantic select element', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('has visible label text', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [] });
      render(<AgentSelector />);

      expect(screen.getByText('Seleziona Agente:')).toBeVisible();
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles agents with special characters in names', () => {
      const agents = [
        { id: 'agent-1', name: "Chess Expert: Beginner's Guide" },
        { id: 'agent-2', name: 'Catan Helper (Advanced)' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      expect(screen.getByText("Chess Expert: Beginner's Guide")).toBeInTheDocument();
      expect(screen.getByText('Catan Helper (Advanced)')).toBeInTheDocument();
    });

    it('handles very long agent names', () => {
      const longName = 'A'.repeat(100);
      const agents = [{ id: 'agent-1', name: longName }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      expect(screen.getByText(longName)).toBeInTheDocument();
    });

    it('handles transition from loading to loaded with agents', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [], loading: { agents: true } });
      const { rerender } = render(<AgentSelector />);

      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();

      // Load agents
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents, loading: { agents: false } });
      rerender(<AgentSelector />);

      expect(screen.queryByTestId('skeleton-loader')).not.toBeInTheDocument();
      expect(screen.getByText('Chess Expert')).toBeInTheDocument();
    });

    it('handles transition from no game to game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      const { rerender } = render(<AgentSelector />);

      expect(screen.getByText('Seleziona prima un gioco')).toBeInTheDocument();

      // Select game
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      rerender(<AgentSelector />);

      expect(screen.queryByText('Seleziona prima un gioco')).not.toBeInTheDocument();
      expect(screen.getByText('Chess Expert')).toBeInTheDocument();
    });

    it('handles agents with duplicate names (different IDs)', () => {
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Chess Expert' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const chessOptions = screen.getAllByText('Chess Expert');
      expect(chessOptions).toHaveLength(2);
    });

    it('handles single agent in list', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.options).toHaveLength(2); // 1 agent + 1 placeholder
    });

    it('handles large number of agents', () => {
      const agents = Array.from({ length: 100 }, (_, i) => ({
        id: `agent-${i}`,
        name: `Agent ${i}`,
      }));
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox') as HTMLSelectElement;
      expect(select.options).toHaveLength(101); // 100 agents + 1 placeholder
    });

    it('handles game selection change clearing agents', () => {
      const agents1 = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents: agents1, selectedAgentId: 'agent-1' });
      const { rerender } = render(<AgentSelector />);

      // Change game (agents cleared)
      setupMockContext({ selectedGameId: 'game-2', agents: [], selectedAgentId: null });
      rerender(<AgentSelector />);

      expect(screen.getByText('Nessun agente disponibile')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Styling
   */
  describe('Styling', () => {
    it('applies correct container margin', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      const { container } = render(<AgentSelector />);

      const wrapper = container.firstChild as HTMLElement;
      expect(wrapper).toHaveStyle({ marginBottom: '12px' });
    });

    it('applies correct label styling', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const label = screen.getByText('Seleziona Agente:');
      expect(label).toHaveStyle({
        display: 'block',
        marginBottom: '6px',
        fontWeight: '500',
        fontSize: '13px',
      });
    });

    it('applies correct select styling', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({
        width: '100%',
        padding: '8px',
        fontSize: '13px',
        borderRadius: '4px',
        border: '1px solid #dadce0',
      });
    });

    it('applies reduced opacity when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({ opacity: 0.6 });
    });

    it('applies full opacity when game is selected', () => {
      const agents = [{ id: 'agent-1', name: 'Chess Expert' }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveStyle({ opacity: 1 });
    });
  });
});
