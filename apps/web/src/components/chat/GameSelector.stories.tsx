/**
 * GameSelector Storybook Stories
 * Issue #1676: Chromatic visual regression testing
 */

import type { Meta, StoryObj } from '@storybook/react';
import { GameSelector } from './GameSelector';
import { ChatContext, type ChatContextValue } from '@/store/chat/StorybookContext';

const createMockContext = (overrides: Partial<ChatContextValue> = {}): ChatContextValue => ({
  games: [
    {
      id: '1',
      title: 'Catan',
      description: 'Settlers game',
      imageUrl: null,
      createdAt: new Date(),
      updatedAt: new Date(),
    },
    {
      id: '2',
      title: 'Carcassonne',
      description: 'Tile game',
      imageUrl: null,
      createdAt: new Date(),
      updatedAt: new Date(),
    },
    {
      id: '3',
      title: 'Ticket to Ride',
      description: 'Train game',
      imageUrl: null,
      createdAt: new Date(),
      updatedAt: new Date(),
    },
  ],
  selectedGameId: null,
  selectGame: () => {},
  loading: {
    games: false,
    agents: false,
    chats: false,
    messages: false,
    sending: false,
    creating: false,
    updating: false,
    deleting: false,
  },
  // Default required props
  authUser: null,
  agents: [],
  selectedAgentId: null,
  selectAgent: () => {},
  chats: [],
  activeChatId: null,
  messages: [],
  createChat: async () => {},
  deleteChat: async () => {},
  selectChat: async () => {},
  sendMessage: async () => {},
  setMessageFeedback: async () => {},
  editMessage: async () => {},
  deleteMessage: async () => {},
  errorMessage: '',
  sidebarCollapsed: false,
  toggleSidebar: () => {},
  editingMessageId: null,
  editContent: '',
  setEditContent: () => {},
  startEditMessage: () => {},
  cancelEdit: () => {},
  saveEdit: async () => {},
  inputValue: '',
  setInputValue: () => {},
  searchMode: 'vector',
  setSearchMode: () => {},
  isStreaming: false,
  streamingAnswer: '',
  streamingState: 'idle',
  streamingCitations: [],
  stopStreaming: () => {},
  ...overrides,
});

const meta = {
  title: 'Chat/GameSelector',
  component: GameSelector,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
      modes: {
        light: {},
        dark: {},
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => {
      const mockContext = createMockContext();
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="w-[300px]">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
} satisfies Meta<typeof GameSelector>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default state with 3 games, none selected */
export const Default: Story = {};

/** With selected game */
export const WithSelection: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({ selectedGameId: '1' });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="w-[300px]">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/** Loading state */
export const Loading: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        loading: { ...createMockContext().loading, games: true },
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="w-[300px]">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/** Empty state (no games) */
export const Empty: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({ games: [] });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="w-[300px]">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};
