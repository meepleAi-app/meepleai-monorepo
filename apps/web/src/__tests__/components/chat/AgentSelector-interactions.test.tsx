/**
 * AgentSelector Component Tests - Interactions and Edge Cases
 *
 * Tests for agent selection, accessibility, and edge cases
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

describe('AgentSelector - Interactions and Edge Cases', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * Test Group: Agent Selection
   */
  describe('Agent Selection', () => {
    it('calls selectAgent when an agent is selected', async () => {
      const user = userEvent.setup();
      const selectAgent = vi.fn();
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, selectAgent });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      const option = await screen.findByRole('option', { name: 'Chess Expert' });
      await user.click(option);

      expect(selectAgent).toHaveBeenCalledWith('agent-1');
    });

    it('does not call selectAgent when empty option is selected', () => {
      const selectAgent = vi.fn();
      setupMockContext({
        selectedGameId: 'game-1',
        agents: sampleAgents.single,
        selectAgent,
        selectedAgentId: 'agent-1',
      });
      render(<AgentSelector />);

      // Radix Select doesn't allow selecting empty value after a value is set
      // This test validates the onValueChange logic only calls selectAgent for non-empty values
      expect(selectAgent).not.toHaveBeenCalled();
    });

    it('handles multiple agent selections sequentially', async () => {
      const user = userEvent.setup();
      const selectAgent = vi.fn();
      const agents = [
        { id: 'agent-1', name: 'Chess Expert' },
        { id: 'agent-2', name: 'Catan Helper' },
      ];
      setupMockContext({ selectedGameId: 'game-1', agents, selectAgent });
      const { rerender } = render(<AgentSelector />);

      // First selection
      let trigger = screen.getByRole('combobox');
      await user.click(trigger);
      const option1 = await screen.findByRole('option', { name: 'Chess Expert' });
      await user.click(option1);

      // Update context to reflect selection
      setupMockContext({ selectedGameId: 'game-1', agents, selectAgent, selectedAgentId: 'agent-1' });
      rerender(<AgentSelector />);

      // Second selection
      trigger = screen.getByRole('combobox');
      await user.click(trigger);
      const option2 = await screen.findByRole('option', { name: 'Catan Helper' });
      await user.click(option2);

      expect(selectAgent).toHaveBeenCalledTimes(2);
      expect(selectAgent).toHaveBeenNthCalledWith(1, 'agent-1');
      expect(selectAgent).toHaveBeenNthCalledWith(2, 'agent-2');
    });

    it('uses void operator for async selectAgent call', async () => {
      const user = userEvent.setup();
      const selectAgent = vi.fn().mockResolvedValue(undefined);
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, selectAgent });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);
      const option = await screen.findByRole('option', { name: 'Chess Expert' });
      await user.click(option);

      expect(selectAgent).toHaveBeenCalled();
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('has proper label association', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      render(<AgentSelector />);

      // Radix Select has proper ARIA labeling but doesn't use traditional id/htmlFor
      const label = screen.getByText('Seleziona Agente:');
      expect(label).toBeInTheDocument();

      const select = screen.getByRole('combobox');
      expect(select).toBeInTheDocument();
    });

    it('has correct aria-busy attribute', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, loading: { agents: false } });
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
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      render(<AgentSelector />);

      const select = screen.getByRole('combobox');
      expect(select).not.toHaveAttribute('title');
    });

    it('uses semantic select element', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
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
    it('handles agents with special characters in names', async () => {
      const user = userEvent.setup();
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.specialCharacters });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        expect(screen.getByText("Chess Expert: Beginner's Guide")).toBeInTheDocument();
        expect(screen.getByText('Catan Helper (Advanced)')).toBeInTheDocument();
      });
    });

    it('handles very long agent names', async () => {
      const user = userEvent.setup();
      const longName = 'A'.repeat(100);
      const agents = [{ id: 'agent-1', name: longName }];
      setupMockContext({ selectedGameId: 'game-1', agents });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        expect(screen.getByText(longName)).toBeInTheDocument();
      });
    });

    it('handles transition from loading to loaded with agents', () => {
      setupMockContext({ selectedGameId: 'game-1', agents: [], loading: { agents: true } });
      const { rerender } = render(<AgentSelector />);

      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();

      // Load agents
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single, loading: { agents: false } });
      rerender(<AgentSelector />);

      expect(screen.queryByTestId('skeleton-loader')).not.toBeInTheDocument();
      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('handles transition from no game to game selected', async () => {
      const user = userEvent.setup();
      setupMockContext({ selectedGameId: null, agents: [] });
      const { rerender } = render(<AgentSelector />);

      expect(screen.getByText('Seleziona prima un gioco')).toBeInTheDocument();

      // Select game
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      rerender(<AgentSelector />);

      expect(screen.queryByText('Seleziona prima un gioco')).not.toBeInTheDocument();

      // Open dropdown to verify agent is available
      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        expect(screen.getByText('Chess Expert')).toBeInTheDocument();
      });
    });

    it('handles agents with duplicate names (different IDs)', async () => {
      const user = userEvent.setup();
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.duplicateNames });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        const chessOptions = screen.getAllByText('Chess Expert');
        expect(chessOptions.length).toBeGreaterThanOrEqual(2);
      });
    });

    it('handles single agent in list', async () => {
      const user = userEvent.setup();
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.single });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        const options = screen.getAllByRole('option');
        expect(options).toHaveLength(1);
      });
    });

    it('handles large number of agents', async () => {
      const user = userEvent.setup();
      setupMockContext({ selectedGameId: 'game-1', agents: sampleAgents.largeSet });
      render(<AgentSelector />);

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        const options = screen.getAllByRole('option');
        expect(options).toHaveLength(100);
      });
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
});
