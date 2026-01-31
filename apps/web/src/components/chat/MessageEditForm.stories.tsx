/**
 * MessageEditForm Storybook Stories
 * Issue #1676: Chromatic visual regression testing
 */

import { ChatContext, type ChatContextValue } from '@/store/chat/StorybookContext';

import { MessageEditForm } from './MessageEditForm';

import type { Meta, StoryObj } from '@storybook/react';

const createMockContext = (overrides: Partial<ChatContextValue> = {}): ChatContextValue => ({
  editingMessageId: 'msg-1',
  editContent: 'How many resource cards can I have in Catan?',
  setEditContent: () => {},
  saveEdit: async () => {},
  cancelEdit: () => {},
  editMessage: async () => {},
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
  setMessageFeedback: async () => {},
  deleteMessage: async () => {},
  errorMessage: '',
  sidebarCollapsed: false,
  toggleSidebar: () => {},
  startEditMessage: () => {},
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
  title: 'Chat/MessageEditForm',
  component: MessageEditForm,
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
} satisfies Meta<typeof MessageEditForm>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default editing state */
export const Default: Story = {};

/** Empty content (save disabled) */
export const EmptyContent: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({ editContent: '' });
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

/** Saving state */
export const Saving: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        loading: { ...createMockContext().loading, updating: true },
      });
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

/** Not editing (hidden) */
export const NotEditing: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({ editingMessageId: null });
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
