/**
 * AgentSelector Storybook Stories
 * Issue #1676: Chromatic visual regression testing
 */

import type { Meta, StoryObj } from '@storybook/react';
import { AgentSelector } from './AgentSelector';
import { ChatContext, type ChatContextValue } from '@/store/chat/StorybookContext';

const createMockContext = (overrides: Partial<ChatContextValue> = {}): ChatContextValue => ({
  agents: [
    {
      id: '1',
      name: 'Rules Expert',
      description: 'Expert on game rules',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
    {
      id: '2',
      name: 'Strategy Guide',
      description: 'Strategic advice',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  ],
  selectedAgentId: null,
  selectedGameId: 'game-123',
  selectAgent: () => {},
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
  games: [],
  selectGame: () => {},
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
  title: 'Chat/AgentSelector',
  component: AgentSelector,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
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
} satisfies Meta<typeof AgentSelector>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default state with 2 agents, none selected */
export const Default: Story = {};

/** With selected agent */
export const WithSelection: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({ selectedAgentId: '1' });
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

/** Disabled state (no game selected) */
export const Disabled: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({ selectedGameId: null });
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
        loading: { ...createMockContext().loading, agents: true },
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
