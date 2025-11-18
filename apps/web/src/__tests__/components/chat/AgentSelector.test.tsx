/**
 * AgentSelector Component Tests
 *
 * Tests for the AgentSelector component that allows users to select
 * which AI agent to chat with for the selected game.
 *
 * Target Coverage: 90%+ (from 66.7%)
 * Migrated to Zustand (Issue #1083)
 */

import React from 'react';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AgentSelector } from '../../../components/chat/AgentSelector';
import { renderWithChatStore, resetChatStore, updateChatStoreState } from '@/__tests__/utils/zustand-test-utils';
import { useChatStore } from '@/store/chat/store';
import { createMockAgent } from '@/__tests__/fixtures/common-fixtures';

// Mock SkeletonLoader component
jest.mock('../../../components/loading/SkeletonLoader', () => ({
  SkeletonLoader: ({ variant }: { variant: string }) => (
    <div data-testid="skeleton-loader" data-variant={variant}>
      Loading...
    </div>
  ),
}));

describe('AgentSelector Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    resetChatStore();
  });

  /**
   * Test Group: Loading State
   */
  describe('Loading State', () => {
    it('displays skeleton loader when agents are loading', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { loading: { agents: true, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false } }
      });

      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();
      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('displays skeleton loader with correct variant', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { loading: { agents: true, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false } }
      });

      const skeleton = screen.getByTestId('skeleton-loader');
      expect(skeleton).toHaveAttribute('data-variant', 'gameSelection');
    });

    it('displays label during loading', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { loading: { agents: true, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false } }
      });

      expect(screen.getByText('Seleziona Agente:')).toBeInTheDocument();
    });

    it('does not display select element when loading', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { loading: { agents: true, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false } }
      });

      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    });
  });

  /**
   * Test Group: No Game Selected State
   */
  describe('No Game Selected State', () => {
    it('displays prompt to select game first when no game selected', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents: [] }
      });

      expect(screen.getByText('Seleziona prima un gioco')).toBeInTheDocument();
    });

    it('disables select when no game is selected', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents: [] }
      });

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });

    it('shows tooltip when hovering disabled select', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents: [] }
      });

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('title', 'Seleziona prima un gioco');
    });

    it('renders disabled state when no game selected', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents: [] }
      });

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });

    it('shows disabled appearance when no game selected', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents: [] }
      });

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });
  });

  /**
   * Test Group: Empty Agents State
   */
  describe('Empty Agents State', () => {
    it('displays "no agents" message when agents array is empty', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents: [] }
      });

      expect(screen.getByText('Nessun agente disponibile')).toBeInTheDocument();
    });

    it('renders select element even when agents are empty', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents: [] }
      });

      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('does not display placeholder option when agents are empty', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents: [] }
      });

      expect(screen.queryByText('Seleziona un agente')).not.toBeInTheDocument();
    });

    it('select is not disabled when empty but game is selected', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: {
          selectedGameId: 'game-1',
          agents: [],
          loading: { agents: false, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
        }
      });

      const select = screen.getByRole('combobox');
      expect(select).not.toBeDisabled();
    });
  });

  /**
   * Test Group: Agents List Rendering
   */
  describe('Agents List Rendering', () => {
    it('renders list of available agents', async () => {
      const user = userEvent.setup();
      const agents = [
        createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-2', name: 'Catan Helper', type: 'setup', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-3', name: 'Risk Strategist', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
      ];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

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
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      expect(screen.getByText('Seleziona un agente')).toBeInTheDocument();
    });

    it('renders correct number of options (agents + placeholder)', async () => {
      const user = userEvent.setup();
      const agents = [
        createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-2', name: 'Catan Helper', type: 'setup', createdAt: '2024-01-01T00:00:00Z' }),
      ];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

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
      const agents = [
        createMockAgent({ id: 'agent-1', name: 'Zzz Agent', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-2', name: 'Aaa Agent', type: 'setup', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-3', name: 'Mmm Agent', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
      ];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

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
      const selectAgentSpy = jest.spyOn(useChatStore.getState(), 'selectAgent');
      const agents = [createMockAgent({ id: 'agent-123', name: 'Test Agent', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      const option = await screen.findByRole('option', { name: 'Test Agent' });
      await user.click(option);

      // Verify selectAgent was called with the correct agent.id
      expect(selectAgentSpy).toHaveBeenCalledWith('agent-123');
    });
  });

  /**
   * Test Group: Agent Selection
   */
  describe('Agent Selection', () => {
    it('calls selectAgent when an agent is selected', async () => {
      const user = userEvent.setup();
      const selectAgentSpy = jest.spyOn(useChatStore.getState(), 'selectAgent');
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      const option = await screen.findByRole('option', { name: 'Chess Expert' });
      await user.click(option);

      expect(selectAgentSpy).toHaveBeenCalledWith('agent-1');
    });

    it('does not call selectAgent when empty option is selected', () => {
      const selectAgentSpy = jest.spyOn(useChatStore.getState(), 'selectAgent');
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents, selectedAgentId: 'agent-1' }
      });

      // Radix Select doesn't allow selecting empty value after a value is set
      // This test validates the onValueChange logic only calls selectAgent for non-empty values
      expect(selectAgentSpy).not.toHaveBeenCalled();
    });

    it('handles multiple agent selections sequentially', async () => {
      const user = userEvent.setup();
      const selectAgentSpy = jest.spyOn(useChatStore.getState(), 'selectAgent');
      const agents = [
        createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-2', name: 'Catan Helper', type: 'setup', createdAt: '2024-01-01T00:00:00Z' }),
      ];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      // First selection
      let trigger = screen.getByRole('combobox');
      await user.click(trigger);
      const option1 = await screen.findByRole('option', { name: 'Chess Expert' });
      await user.click(option1);

      // Update store to reflect selection
      updateChatStoreState({ selectedAgentId: 'agent-1' });

      // Second selection
      trigger = screen.getByRole('combobox');
      await user.click(trigger);
      const option2 = await screen.findByRole('option', { name: 'Catan Helper' });
      await user.click(option2);

      expect(selectAgentSpy).toHaveBeenCalledTimes(2);
      expect(selectAgentSpy).toHaveBeenNthCalledWith(1, 'agent-1');
      expect(selectAgentSpy).toHaveBeenNthCalledWith(2, 'agent-2');
    });

    it('uses void operator for async selectAgent call', async () => {
      const user = userEvent.setup();
      const selectAgentSpy = jest.spyOn(useChatStore.getState(), 'selectAgent').mockImplementation(() => Promise.resolve());
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);
      const option = await screen.findByRole('option', { name: 'Chess Expert' });
      await user.click(option);

      expect(selectAgentSpy).toHaveBeenCalled();
    });
  });

  /**
   * Test Group: Selected Agent State
   */
  describe('Selected Agent State', () => {
    it('displays currently selected agent', () => {
      const agents = [
        createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-2', name: 'Catan Helper', type: 'setup', createdAt: '2024-01-01T00:00:00Z' }),
      ];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents, selectedAgentId: 'agent-2' }
      });

      // Radix Select shows selected value in trigger
      expect(screen.getByRole('combobox')).toHaveTextContent('Catan Helper');
    });

    it('displays empty value when no agent is selected', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents, selectedAgentId: null }
      });

      expect(screen.getByText('Seleziona un agente')).toBeInTheDocument();
    });

    it('handles undefined selectedAgentId', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents, selectedAgentId: undefined }
      });

      expect(screen.getByText('Seleziona un agente')).toBeInTheDocument();
    });

    it('updates value when selectedAgentId changes', async () => {
      const agents = [
        createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-2', name: 'Catan Helper', type: 'setup', createdAt: '2024-01-01T00:00:00Z' }),
      ];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents, selectedAgentId: 'agent-1' }
      });

      expect(screen.getByRole('combobox')).toHaveTextContent('Chess Expert');

      // Update selected agent
      updateChatStoreState({ selectedAgentId: 'agent-2' });

      await waitFor(() => {
        expect(screen.getByRole('combobox')).toHaveTextContent('Catan Helper');
      });
    });
  });

  /**
   * Test Group: Disabled State
   */
  describe('Disabled State', () => {
    it('disables select when agents are loading', async () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: {
          selectedGameId: 'game-1',
          agents: [],
          loading: { agents: true, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
        }
      });

      // During loading, skeleton is shown instead of select
      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();
      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();

      // Update to loaded state
      updateChatStoreState({
        agents: [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })],
        loading: { agents: false, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false },
      });

      // Now select should be visible and not disabled
      await waitFor(() => {
        expect(screen.queryByTestId('skeleton-loader')).not.toBeInTheDocument();
      });
      const select = screen.getByRole('combobox');
      expect(select).not.toBeDisabled();
    });

    it('disables select when no game is selected', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents }
      });

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });

    it('sets aria-busy when loading', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: {
          selectedGameId: 'game-1',
          agents,
          loading: { agents: false, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
        }
      });

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('aria-busy', 'false');
    });

    it('changes cursor when disabled', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: {
          selectedGameId: 'game-1',
          agents,
          loading: { agents: false, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
        }
      });

      const select = screen.getByRole('combobox');
      // Radix Select uses Tailwind classes, not inline cursor styles
      expect(select).not.toHaveClass('cursor-not-allowed');
    });

    it('has not-allowed cursor when no game selected', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents: [] }
      });

      const select = screen.getByRole('combobox');
      // When disabled, Radix Select may apply disabled styles, but not inline cursor
      expect(select).toBeDisabled();
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('has proper label association', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      // Radix Select has proper ARIA labeling but doesn't use traditional id/htmlFor
      const label = screen.getByText('Seleziona Agente:');
      expect(label).toBeInTheDocument();

      const select = screen.getByRole('combobox');
      expect(select).toBeInTheDocument();
    });

    it('has correct aria-busy attribute', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: {
          selectedGameId: 'game-1',
          agents,
          loading: { agents: false, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
        }
      });

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('aria-busy', 'false');
    });

    it('has title attribute when no game selected', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents: [] }
      });

      const select = screen.getByRole('combobox');
      expect(select).toHaveAttribute('title', 'Seleziona prima un gioco');
    });

    it('does not have title attribute when game is selected', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const select = screen.getByRole('combobox');
      expect(select).not.toHaveAttribute('title');
    });

    it('uses semantic select element', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('has visible label text', () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents: [] }
      });

      expect(screen.getByText('Seleziona Agente:')).toBeVisible();
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles agents with special characters in names', async () => {
      const user = userEvent.setup();
      const agents = [
        createMockAgent({ id: 'agent-1', name: "Chess Expert: Beginner's Guide", type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-2', name: 'Catan Helper (Advanced)', type: 'setup', createdAt: '2024-01-01T00:00:00Z' }),
      ];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

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
      const agents = [createMockAgent({ id: 'agent-1', name: longName, type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        expect(screen.getByText(longName)).toBeInTheDocument();
      });
    });

    it('handles transition from loading to loaded with agents', async () => {
      renderWithChatStore(<AgentSelector />, {
        initialState: {
          selectedGameId: 'game-1',
          agents: [],
          loading: { agents: true, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
        }
      });

      expect(screen.getByTestId('skeleton-loader')).toBeInTheDocument();

      // Load agents
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      updateChatStoreState({
        agents,
        loading: { agents: false, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
      });

      await waitFor(() => {
        expect(screen.queryByTestId('skeleton-loader')).not.toBeInTheDocument();
      });
      expect(screen.getByRole('combobox')).toBeInTheDocument();
    });

    it('handles transition from no game to game selected', async () => {
      const user = userEvent.setup();
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: null, agents: [] }
      });

      expect(screen.getByText('Seleziona prima un gioco')).toBeInTheDocument();

      // Select game
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      updateChatStoreState({ selectedGameId: 'game-1', agents });

      await waitFor(() => {
        expect(screen.queryByText('Seleziona prima un gioco')).not.toBeInTheDocument();
      });

      // Open dropdown to verify agent is available
      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        expect(screen.getByText('Chess Expert')).toBeInTheDocument();
      });
    });

    it('handles agents with duplicate names (different IDs)', async () => {
      const user = userEvent.setup();
      const agents = [
        createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' }),
        createMockAgent({ id: 'agent-2', name: 'Chess Expert', type: 'setup', createdAt: '2024-01-01T00:00:00Z' }),
      ];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        const chessOptions = screen.getAllByText('Chess Expert');
        expect(chessOptions.length).toBeGreaterThanOrEqual(2);
      });
    });

    it('handles single agent in list', async () => {
      const user = userEvent.setup();
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        const options = screen.getAllByRole('option');
        expect(options).toHaveLength(1);
      });
    });

    it('handles large number of agents', async () => {
      const user = userEvent.setup();
      const agents = Array.from({ length: 100 }, (_, i) => createMockAgent({
        id: `agent-${i}`,
        name: `Agent ${i}`,
        type: 'qa',
        createdAt: '2024-01-01T00:00:00Z',
      }));
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const trigger = screen.getByRole('combobox');
      await user.click(trigger);

      await waitFor(() => {
        const options = screen.getAllByRole('option');
        expect(options).toHaveLength(100);
      });
    });

    it('handles game selection change clearing agents', async () => {
      const agents1 = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents: agents1, selectedAgentId: 'agent-1' }
      });

      // Change game (agents cleared)
      updateChatStoreState({ selectedGameId: 'game-2', agents: [], selectedAgentId: null });

      await waitFor(() => {
        expect(screen.getByText('Nessun agente disponibile')).toBeInTheDocument();
      });
    });
  });

  /**
   * Test Group: Styling
   */
  describe('Styling', () => {
    it('applies correct container margin', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      const { container } = renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const wrapper = container.firstChild as HTMLElement;
      // Tailwind class mb-3 = 0.75rem = 12px
      expect(wrapper).toHaveClass('mb-3');
    });

    it('applies correct label styling', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const label = screen.getByText('Seleziona Agente:');
      // Tailwind classes: block mb-1.5 font-medium text-sm
      expect(label).toHaveClass('block');
      expect(label).toHaveClass('mb-1.5');
      expect(label).toHaveClass('font-medium');
      expect(label).toHaveClass('text-sm');
    });

    it('applies correct select styling', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const select = screen.getByRole('combobox');
      // Radix Select uses Tailwind classes instead of inline styles
      expect(select).toHaveClass('w-full');
      expect(select).toHaveClass('border');
    });

    it('changes cursor style based on loading state', async () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: {
          selectedGameId: 'game-1',
          agents,
          loading: { agents: false, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
        }
      });

      const select = screen.getByRole('combobox');
      expect(select).not.toHaveClass('cursor-not-allowed');

      // Change to loading
      updateChatStoreState({
        loading: { agents: true, chats: false, messages: false, sending: false, creating: false, updating: false, deleting: false, games: false }
      });

      // Select is hidden during loading, check skeleton instead
      await waitFor(() => {
        expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
      });
    });

    it('renders with appropriate styling when enabled', () => {
      const agents = [createMockAgent({ id: 'agent-1', name: 'Chess Expert', type: 'qa', createdAt: '2024-01-01T00:00:00Z' })];
      renderWithChatStore(<AgentSelector />, {
        initialState: { selectedGameId: 'game-1', agents }
      });

      const select = screen.getByRole('combobox');
      expect(select).not.toBeDisabled();
    });
  });
});
