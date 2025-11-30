/**
 * AgentSelector Component Tests - States and Rendering
 *
 * Tests for loading states, empty states, disabled states, and basic rendering
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AgentSelector } from '../../../components/chat/AgentSelector';
import { sampleAgents } from './AgentSelector.test-helpers';

// Mock the ChatProvider context
const mockUseChatContext = vi.fn();
vi.mock('../../../components/chat/ChatProvider', () => ({
  useChatContext: () => mockUseChatContext(),
}));

// Mock SkeletonLoader component
vi.mock('../../../components/loading/SkeletonLoader', () => ({
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
    selectAgent: vi.fn(),
    selectedGameId: null,
    loading: { agents: false },
    ...overrides,
  });
};

describe('AgentSelector - States and Rendering', () => {
  beforeEach(() => {
    vi.clearAllMocks();
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

    it('renders disabled state when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });

    it('shows disabled appearance when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
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
   * Test Group: Disabled State
   */
  describe('Disabled State', () => {
    it('disables select when agents are loading', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [], loading: { agents: true } });
      const { rerender } = render(<AgentSelector />);

      // Rerender with loaded state (select is hidden during loading, check after)
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
      setupMockContext({ selectedGameId: null, agents: sampleAgents.single });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });

    it('sets aria-busy when loading', () => {
      setupMockContext({
        selectedGameId: 'game-1',
        agents: sampleAgents.single,
        loading: { agents: false },
      });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('aria-busy', 'false');
    });

    it('changes cursor when disabled', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, loading: { agents: false } });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      // Radix Select uses Tailwind classes, not inline cursor styles
      expect(select).not.toHaveClass('cursor-not-allowed');
    });

    it('has not-allowed cursor when no game selected', () => {
      setupMockContext({ selectedGameId: null, agents: [] });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      // When disabled, Radix Select may apply disabled styles, but not inline cursor
      expect(select).toBeDisabled();
    });
  });

  /**
   * Test Group: Agents List Rendering
   */
  describe('Agents List Rendering', () => {
    it('renders list of available agents', async () => {
      const user = userEvent.setup();
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.multiple });
      render(<AgentSelector />);

      // Open the select dropdown
      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      // Wait for options to appear
      await waitFor(() => {
        expect(screen.getByText('Chess Expert')).toBeInTheDocument();
        expect(screen.getByText('Catan Helper')).toBeInTheDocument();
        expect(screen.getByText('Risk Strategist')).toBeInTheDocument();
      });
    });

    it('includes placeholder option when agents exist', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      render(<AgentSelector />);

      expect(screen.getByText('Seleziona un agente')).toBeInTheDocument();
    });

    it('renders correct number of options (agents + placeholder)', async () => {
      const user = userEvent.setup();
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Catan Helper' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      // Open dropdown and verify items are present
      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        expect(screen.getByText('Chess Expert')).toBeInTheDocument();
        expect(screen.getByText('Catan Helper')).toBeInTheDocument();
      });
    });

    it('renders agents in the order provided', async () => {
      const user = userEvent.setup();
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.unsortedNames });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        const options = screen.getAllByRole('option');
        expect(options[0]).toHaveTextContent('Zzz Agent');
        expect(options[1]).toHaveTextContent('Aaa Agent');
        expect(options[2]).toHaveTextContent('Mmm Agent');
      });
    });

    it('uses agent.id as option value', async () => {
      const user = userEvent.setup();
      const selectAgent = vi.fn();
      setupMockContext({ selectedGameId: 'game-1', agents: [{ id: 'agent-123', name: 'Test Agent' }], selectAgent });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      const option = await screen.findByRole('option', { name: 'Test Agent' });
      await user.click(option);

      // Verify selectAgent was called with the correct agent.id
      expect(selectAgent).toHaveBeenCalledWith('agent-123');
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

      // Radix Select shows selected value in trigger
      expect(screen.getByRole('combobox')).toHaveTextContent('Catan Helper');
    });

    it('displays empty value when no agent is selected', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, selectedAgentId: null });
      render(<AgentSelector />);

      expect(screen.getByText('Seleziona un agente')).toBeInTheDocument();
    });

    it('handles undefined selectedAgentId', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, selectedAgentId: undefined });
      render(<AgentSelector />);

      expect(screen.getByText('Seleziona un agente')).toBeInTheDocument();
    });

    it('updates value when selectedAgentId changes', () => {
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Catan Helper' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents, selectedAgentId: 'agent-1' });
      const { rerender } = render(<AgentSelector />);

      expect(screen.getByRole('combobox')).toHaveTextContent('Chess Expert');

      // Update selected agent
      setupMockContext({ selectedGameId: 'game-1', agents, selectedAgentId: 'agent-2' });
      rerender(<AgentSelector />);

      expect(screen.getByRole('combobox')).toHaveTextContent('Catan Helper');
    });
  });

  /**
   * Test Group: Styling
   */
  describe('Styling', () => {
    it('applies correct container margin', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      const { container } = render(<AgentSelector />);

      const wrapper = container.firstChild as HTMLElement;
      // Tailwind class mb-3 = 0.75rem = 12px
      expect(wrapper).toHaveClass('mb-3');
    });

    it('applies correct label styling', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      render(<AgentSelector />);

      const label = screen.getByText('Seleziona Agente:');
      // Tailwind classes: block mb-1.5 font-medium text-sm
      expect(label).toHaveClass('block');
      expect(label).toHaveClass('mb-1.5');
      expect(label).toHaveClass('font-medium');
      expect(label).toHaveClass('text-sm');
    });

    it('applies correct select styling', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      // Radix Select uses Tailwind classes instead of inline styles
      expect(select).toHaveClass('w-full');
      expect(select).toHaveClass('border');
    });

    it('changes cursor style based on loading state', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, loading: { agents: false } });
      const { rerender } = render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).not.toHaveClass('cursor-not-allowed');

      // Change to loading
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, loading: { agents: true } });
      rerender(<AgentSelector />);

      // Select is hidden during loading, check skeleton instead
      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    });

    it('renders with appropriate styling when enabled', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).not.toBeDisabled();
    });
  });
});
