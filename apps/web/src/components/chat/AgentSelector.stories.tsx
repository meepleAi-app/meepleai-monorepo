import type { Meta, StoryObj } from '@storybook/react';
import { AgentSelector } from './AgentSelector';
import { ChatContext } from './ChatProvider';
import React from 'react';

/**
 * AgentSelector - Dropdown for selecting AI agent.
 * Allows users to select which AI agent to chat with for the selected game.
 * Integrates with ChatProvider for state management.
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

// Mock ChatProvider for stories
const MockChatProvider: React.FC<{
  children: React.ReactNode;
  mockAgents?: Array<{ id: string; name: string }>;
  mockSelectedAgentId?: string | null;
  mockSelectedGameId?: string | null;
  mockLoadingAgents?: boolean;
}> = ({
  children,
  mockAgents = [],
  mockSelectedAgentId = null,
  mockSelectedGameId = null,
  mockLoadingAgents = false
}) => {
  const [selectedAgentId, setSelectedAgentId] = React.useState(mockSelectedAgentId);

  const mockContextValue = {
    agents: mockAgents,
    selectedAgentId,
    selectAgent: (id: string | null) => setSelectedAgentId(id),
    selectedGameId: mockSelectedGameId,
    loading: {
      agents: mockLoadingAgents,
      games: false,
      sending: false,
      updating: false,
      deleting: false,
    },
    // Add other required context properties with dummy values
    games: [],
    selectedGame: null,
    selectGame: async () => {},
    threads: [],
    selectedThreadId: null,
    messages: [],
    inputValue: '',
    setInputValue: () => {},
    editingMessageId: null,
    startEditMessage: () => {},
    sendMessage: async () => {},
    deleteMessage: async () => {},
    setMessageFeedback: async () => {},
    createNewChat: async () => {},
    selectThread: async () => {},
    deleteThread: async () => {},
    searchMode: 'hybrid',
    setSearchMode: () => {},
  };

  return (
    <ChatContext.Provider value={mockContextValue as any}>
      {children}
    </ChatContext.Provider>
  );
};

/**
 * Default state with agents available and game selected
 */
export const Default: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockAgents={[
          { id: 'agent-1', name: 'Rules Expert' },
          { id: 'agent-2', name: 'Strategy Guide' },
          { id: 'agent-3', name: 'General Assistant' },
        ]}
        mockSelectedGameId="game-1"
        mockSelectedAgentId="agent-1"
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * Loading state while fetching agents
 */
export const Loading: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockSelectedGameId="game-1"
        mockLoadingAgents={true}
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * No game selected - disabled state
 */
export const NoGameSelected: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockAgents={[
          { id: 'agent-1', name: 'Rules Expert' },
          { id: 'agent-2', name: 'Strategy Guide' },
        ]}
        mockSelectedGameId={null}
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * No agents available for selected game
 */
export const NoAgents: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockAgents={[]}
        mockSelectedGameId="game-1"
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * Multiple agents available
 */
export const MultipleAgents: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockAgents={[
          { id: 'agent-1', name: 'Gloomhaven Expert' },
          { id: 'agent-2', name: 'Wingspan Specialist' },
          { id: 'agent-3', name: 'Terraforming Mars Guide' },
          { id: 'agent-4', name: 'Spirit Island Helper' },
          { id: 'agent-5', name: 'General Board Game Assistant' },
        ]}
        mockSelectedGameId="game-1"
        mockSelectedAgentId="agent-2"
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * Interactive agent selection demo
 */
const InteractiveAgentSelectorComponent = () => {
  const [selectedGameId, setSelectedGameId] = React.useState('game-1');
  const [agents, setAgents] = React.useState([
    { id: 'agent-1', name: 'Rules Expert' },
    { id: 'agent-2', name: 'Strategy Guide' },
    { id: 'agent-3', name: 'General Assistant' },
  ]);

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <button
          onClick={() => setSelectedGameId('game-1')}
          className="px-3 py-1 bg-blue-500 text-white rounded text-sm"
        >
          Game 1 (3 agents)
        </button>
        <button
          onClick={() => {
            setSelectedGameId('game-2');
            setAgents([
              { id: 'agent-4', name: 'Expert Helper' },
              { id: 'agent-5', name: 'Quick Guide' },
            ]);
          }}
          className="px-3 py-1 bg-green-500 text-white rounded text-sm"
        >
          Game 2 (2 agents)
        </button>
        <button
          onClick={() => {
            setSelectedGameId(null as any);
          }}
          className="px-3 py-1 bg-gray-500 text-white rounded text-sm"
        >
          No Game
        </button>
      </div>

      <MockChatProvider
        mockAgents={agents}
        mockSelectedGameId={selectedGameId}
      >
        <AgentSelector />
      </MockChatProvider>
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
        <MockChatProvider
          mockAgents={[
            { id: 'agent-1', name: 'Rules Expert' },
            { id: 'agent-2', name: 'Strategy Guide' },
            { id: 'agent-3', name: 'General Assistant' },
          ]}
          mockSelectedGameId="game-1"
          mockSelectedAgentId="agent-1"
        >
          <Story />
        </MockChatProvider>
      </div>
    ),
  ],
};