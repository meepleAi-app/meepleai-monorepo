import type { Meta, StoryObj } from '@storybook/react';
import { AgentSelector } from './AgentSelector';
import React, { useEffect } from 'react';
import { useChatStore } from '@/store/chat/store';

/**
 * AgentSelector - Dropdown for selecting AI agent.
 * Allows users to select which AI agent to chat with for the selected game.
 * Migrated to Zustand (Issue #1240)
 */
const meta = {
  title: 'Chat/AgentSelector',
  component: AgentSelector,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof AgentSelector>;

export default meta;
type Story = StoryObj<typeof meta>;

// Zustand Store Initializer for Storybook
function StoreInitializer({
  agents = [],
  selectedAgentId = null,
  selectedGameId = null,
  loadingAgents = false,
  children
}: {
  agents?: Array<{ id: string; gameId: string; name: string; kind: string; createdAt: string }>;
  selectedAgentId?: string | null;
  selectedGameId?: string | null;
  loadingAgents?: boolean;
  children: React.ReactNode;
}) {
  useEffect(() => {
    useChatStore.setState({
      agents: agents,
      selectedAgentId: selectedAgentId,
      selectedGameId: selectedGameId,
      loading: {
        games: false,
        agents: loadingAgents,
        chats: false,
        messages: false,
        sending: false,
        creating: false,
        updating: false,
        deleting: false,
      },
    });
  }, [agents, selectedAgentId, selectedGameId, loadingAgents]);

  return <>{children}</>;
}

/**
 * Default state with agents available and game selected
 */
export const Default: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer
        agents={[
          { id: 'agent-1', gameId: 'game-1', name: 'Rules Expert', kind: 'qa', createdAt: new Date().toISOString() },
          { id: 'agent-2', gameId: 'game-1', name: 'Strategy Guide', kind: 'qa', createdAt: new Date().toISOString() },
          { id: 'agent-3', gameId: 'game-1', name: 'General Assistant', kind: 'qa', createdAt: new Date().toISOString() },
        ]}
        selectedGameId="game-1"
        selectedAgentId="agent-1"
      >
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * Loading state while fetching agents
 */
export const Loading: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer
        selectedGameId="game-1"
        loadingAgents={true}
      >
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * No game selected - disabled state
 */
export const NoGameSelected: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer
        agents={[
          { id: 'agent-1', gameId: 'game-1', name: 'Rules Expert', kind: 'qa', createdAt: new Date().toISOString() },
          { id: 'agent-2', gameId: 'game-1', name: 'Strategy Guide', kind: 'qa', createdAt: new Date().toISOString() },
        ]}
        selectedGameId={null}
      >
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * No agents available for selected game
 */
export const NoAgents: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer
        agents={[]}
        selectedGameId="game-1"
      >
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * Multiple agents available
 */
export const MultipleAgents: Story = {
  decorators: [
    (Story) => (
      <StoreInitializer
        agents={[
          { id: 'agent-1', gameId: 'game-1', name: 'Gloomhaven Expert', kind: 'qa', createdAt: new Date().toISOString() },
          { id: 'agent-2', gameId: 'game-1', name: 'Wingspan Specialist', kind: 'qa', createdAt: new Date().toISOString() },
          { id: 'agent-3', gameId: 'game-1', name: 'Terraforming Mars Guide', kind: 'qa', createdAt: new Date().toISOString() },
          { id: 'agent-4', gameId: 'game-1', name: 'Spirit Island Helper', kind: 'qa', createdAt: new Date().toISOString() },
          { id: 'agent-5', gameId: 'game-1', name: 'General Board Game Assistant', kind: 'qa', createdAt: new Date().toISOString() },
        ]}
        selectedGameId="game-1"
        selectedAgentId="agent-2"
      >
        <Story />
      </StoreInitializer>
    ),
  ],
};

/**
 * Interactive agent selection demo
 */
const InteractiveAgentSelectorComponent = () => {
  const [agents, setAgents] = React.useState([
    { id: 'agent-1', gameId: 'game-1', name: 'Rules Expert', kind: 'qa', createdAt: new Date().toISOString() },
    { id: 'agent-2', gameId: 'game-1', name: 'Strategy Guide', kind: 'qa', createdAt: new Date().toISOString() },
    { id: 'agent-3', gameId: 'game-1', name: 'General Assistant', kind: 'qa', createdAt: new Date().toISOString() },
  ]);

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <button
          onClick={() => useChatStore.setState({ selectedGameId: 'game-1' })}
          className="px-3 py-1 bg-blue-500 text-white rounded text-sm"
        >
          Game 1 (3 agents)
        </button>
        <button
          onClick={() => {
            useChatStore.setState({ selectedGameId: 'game-2' });
            setAgents([
              { id: 'agent-4', gameId: 'game-2', name: 'Expert Helper', kind: 'qa', createdAt: new Date().toISOString() },
              { id: 'agent-5', gameId: 'game-2', name: 'Quick Guide', kind: 'qa', createdAt: new Date().toISOString() },
            ]);
          }}
          className="px-3 py-1 bg-green-500 text-white rounded text-sm"
        >
          Game 2 (2 agents)
        </button>
        <button
          onClick={() => useChatStore.setState({ selectedGameId: null })}
          className="px-3 py-1 bg-gray-500 text-white rounded text-sm"
        >
          No Game
        </button>
      </div>

      <StoreInitializer
        agents={agents}
        selectedGameId="game-1"
      >
        <AgentSelector />
      </StoreInitializer>
    </div>
  );
};

export const Interactive: Story = {
  render: () => <InteractiveAgentSelectorComponent />,
};

/**
 * Dark mode example
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <StoreInitializer
          agents={[
            { id: 'agent-1', gameId: 'game-1', name: 'Rules Expert', kind: 'qa', createdAt: new Date().toISOString() },
            { id: 'agent-2', gameId: 'game-1', name: 'Strategy Guide', kind: 'qa', createdAt: new Date().toISOString() },
            { id: 'agent-3', gameId: 'game-1', name: 'General Assistant', kind: 'qa', createdAt: new Date().toISOString() },
          ]}
          selectedGameId="game-1"
          selectedAgentId="agent-1"
        >
          <Story />
        </StoreInitializer>
      </div>
    ),
  ],
};
