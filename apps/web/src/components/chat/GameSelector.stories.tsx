import type { Meta, StoryObj } from '@storybook/react';
import { GameSelector } from './GameSelector';
import { ChatContext } from './ChatProvider';
import React from 'react';

/**
 * GameSelector - Dropdown for selecting game context.
 * Allows users to select which board game they want to chat about.
 * Integrates with ChatProvider for state management.
 */
const meta = {
  title: 'Chat/GameSelector',
  component: GameSelector,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof GameSelector>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock ChatProvider
const MockChatProvider: React.FC<{
  children: React.ReactNode;
  mockGames?: Array<{ id: string; name: string }>;
  mockSelectedGameId?: string | null;
  mockLoadingGames?: boolean;
}> = ({
  children,
  mockGames = [],
  mockSelectedGameId = null,
  mockLoadingGames = false
}) => {
  const [selectedGameId, setSelectedGameId] = React.useState(mockSelectedGameId);

  const mockContextValue = {
    games: mockGames,
    selectedGameId,
    selectGame: async (id: string) => { setSelectedGameId(id); },
    loading: {
      games: mockLoadingGames,
      agents: false,
      sending: false,
      updating: false,
      deleting: false,
    },
    agents: [],
    selectedAgentId: null,
    selectedGame: null,
    selectAgent: () => {},
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
 * Default state with games available
 */
export const Default: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockGames={[
          { id: 'game-1', name: 'Gloomhaven' },
          { id: 'game-2', name: 'Wingspan' },
          { id: 'game-3', name: 'Terraforming Mars' },
        ]}
        mockSelectedGameId="game-1"
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * Loading state
 */
export const Loading: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockLoadingGames={true}
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * No games available
 */
export const NoGames: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockGames={[]}
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * Many games available
 */
export const ManyGames: Story = {
  decorators: [
    (Story) => (
      <MockChatProvider
        mockGames={[
          { id: 'game-1', name: 'Gloomhaven' },
          { id: 'game-2', name: 'Wingspan' },
          { id: 'game-3', name: 'Terraforming Mars' },
          { id: 'game-4', name: 'Spirit Island' },
          { id: 'game-5', name: 'Scythe' },
          { id: 'game-6', name: 'Pandemic Legacy' },
          { id: 'game-7', name: 'Catan' },
          { id: 'game-8', name: 'Ticket to Ride' },
        ]}
        mockSelectedGameId="game-3"
      >
        <Story />
      </MockChatProvider>
    ),
  ],
};

/**
 * Interactive game selection
 */
export const Interactive: Story = {
  render: () => {
    const games = [
      { id: 'game-1', name: 'Gloomhaven' },
      { id: 'game-2', name: 'Wingspan' },
      { id: 'game-3', name: 'Terraforming Mars' },
    ];

    return (
      <MockChatProvider mockGames={games}>
        <GameSelector />
      </MockChatProvider>
    );
  },
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <MockChatProvider
          mockGames={[
            { id: 'game-1', name: 'Gloomhaven' },
            { id: 'game-2', name: 'Wingspan' },
            { id: 'game-3', name: 'Terraforming Mars' },
          ]}
          mockSelectedGameId="game-1"
        >
          <Story />
        </MockChatProvider>
      </div>
    ),
  ],
};
