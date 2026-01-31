/**
 * Message Storybook Stories
 * Issue #1676: Chromatic visual regression testing
 */

import { ChatContext, type ChatContextValue } from '@/store/chat/StorybookContext';
import type { Message as MessageType } from '@/types';

import { Message } from './Message';

import type { Meta, StoryObj } from '@storybook/react';

const userMessage: MessageType = {
  id: 'msg-1',
  role: 'user',
  content: 'How many players can play Catan?',
  timestamp: new Date('2025-01-15T10:00:00Z'),
  endpoint: 'qa',
};

const assistantMessage: MessageType = {
  id: 'msg-2',
  role: 'assistant',
  content: 'Catan can be played with 3-4 players in the base game.',
  timestamp: new Date('2025-01-15T10:00:05Z'),
  endpoint: 'qa-stream',
};

const createMockContext = (overrides: Partial<ChatContextValue> = {}): ChatContextValue => ({
  editingMessageId: null,
  startEditMessage: () => {},
  deleteMessage: async () => {},
  setMessageFeedback: async () => {},
  setInputValue: () => {},
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
  // Required props
  authUser: null,
  games: [],
  selectedGameId: null,
  agents: [],
  selectedAgentId: null,
  selectGame: () => {},
  selectAgent: () => {},
  chats: [],
  activeChatId: null,
  messages: [],
  createChat: async () => {},
  deleteChat: async () => {},
  selectChat: async () => {},
  sendMessage: async () => {},
  editMessage: async () => {},
  errorMessage: '',
  sidebarCollapsed: false,
  toggleSidebar: () => {},
  editContent: '',
  setEditContent: () => {},
  cancelEdit: () => {},
  saveEdit: async () => {},
  inputValue: '',
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
  title: 'Chat/Message',
  component: Message,
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
          <div className="max-w-3xl">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
} satisfies Meta<typeof Message>;

export default meta;
type Story = StoryObj<typeof meta>;

/** User message */
export const UserMessage: Story = {
  args: {
    message: userMessage,
    isUser: true,
  },
};

/** Assistant message */
export const AssistantMessage: Story = {
  args: {
    message: assistantMessage,
    isUser: false,
  },
};

/** Message in edit mode */
export const EditingMessage: Story = {
  args: {
    message: userMessage,
    isUser: true,
  },
  decorators: [
    Story => {
      const mockContext = createMockContext({ editingMessageId: 'msg-1' });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="max-w-3xl">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/** Deleted message */
export const DeletedMessage: Story = {
  args: {
    message: { ...userMessage, isDeleted: true },
    isUser: true,
  },
};
