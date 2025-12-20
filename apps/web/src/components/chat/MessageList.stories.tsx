import { fn } from 'storybook/test';

import { ChatContext, type ChatContextValue } from '@/store/chat/StorybookContext';

import { MessageList } from './MessageList';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Message List - Scrollable list of chat messages
 * Issue #1676 Phase 3: Migrated from ChatProvider to ChatContext mock
 */

// Helper to create mock context
const createMockContext = (overrides: Partial<ChatContextValue> = {}): ChatContextValue => ({
  messages: [],
  activeChatId: null,
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
  // Required defaults
  authUser: null,
  games: [],
  selectedGameId: null,
  agents: [],
  selectedAgentId: null,
  selectGame: () => {},
  selectAgent: () => {},
  chats: [],
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

/**
 * Message List - Scrollable list of chat messages
 *
 * ## Features
 * - **Virtualized Rendering**: Efficient for large message lists (>50 messages)
 * - **Loading State**: Skeleton loaders while fetching
 * - **Empty State**: Prompt to start conversation
 * - **Streaming Support**: Real-time message updates
 * - **Citation Click**: PDF page jump integration
 * - **Auto-scroll**: Scroll to bottom on new messages
 *
 * ## Integration
 * Uses ChatProvider context and VirtualizedMessageList for rendering.
 */
const meta = {
  title: 'Chat/MessageList',
  component: MessageList,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    onCitationClick: {
      action: 'citation-clicked',
      description: 'Callback when citation is clicked for PDF page jump',
    },
  },
  decorators: [
    Story => (
      <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof MessageList>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock message data
const mockMessages = [
  {
    id: 'msg-1',
    threadId: 'thread-1',
    content: 'How many resource cards can I hold in Catan?',
    role: 'user',
    createdAt: '2025-12-05T10:00:00Z',
  },
  {
    id: 'msg-2',
    threadId: 'thread-1',
    content:
      'In Catan, you can hold up to 7 resource cards before the robber forces you to discard half.',
    role: 'assistant',
    createdAt: '2025-12-05T10:00:15Z',
    confidence: 0.95,
    citations: [
      { id: 'cit-1', pageNumber: 5, text: 'Resource card limit: 7 cards' },
      { id: 'cit-2', pageNumber: 8, text: 'Robber discard rule' },
    ],
  },
  {
    id: 'msg-3',
    threadId: 'thread-1',
    content: 'What happens if I roll a 7?',
    role: 'user',
    createdAt: '2025-12-05T10:01:00Z',
  },
  {
    id: 'msg-4',
    threadId: 'thread-1',
    content:
      'When you roll a 7: 1) Move the robber to a new hex, 2) Steal a card from a player with a settlement adjacent to that hex, 3) All players with >7 cards discard half.',
    role: 'assistant',
    createdAt: '2025-12-05T10:01:20Z',
    confidence: 0.88,
    citations: [{ id: 'cit-3', pageNumber: 8, text: 'Rolling a 7 triggers robber' }],
  },
];

/**
 * Default message list with conversation.
 */
export const Default: Story = {
  args: {
    onCitationClick: fn(),
  },
  decorators: [
    Story => (
      <ChatContext.Provider
        value={createMockContext({
          initialMessages: mockMessages,
          activeChatId: 'thread-1',
          loading: { chats: false, messages: false, submit: false },
        })}
      >
        <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
          <Story />
        </div>
      </ChatContext.Provider>
    ),
  ],
};

/**
 * Loading state while fetching messages.
 */
export const Loading: Story = {
  decorators: [
    Story => (
      <ChatContext.Provider
        value={createMockContext({
          initialMessages: [],
          loading: { chats: false, messages: true, submit: false },
        })}
      >
        <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
          <Story />
        </div>
      </ChatContext.Provider>
    ),
  ],
};

/**
 * Empty state with no messages.
 */
export const Empty: Story = {
  decorators: [
    Story => (
      <ChatContext.Provider
        value={createMockContext({
          initialMessages: [],
          activeChatId: 'thread-1',
          loading: { chats: false, messages: false, submit: false },
        })}
      >
        <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
          <Story />
        </div>
      </ChatContext.Provider>
    ),
  ],
};

/**
 * Streaming message in progress.
 */
export const Streaming: Story = {
  args: {
    onCitationClick: fn(),
  },
  decorators: [
    Story => (
      <ChatContext.Provider
        value={createMockContext({
          initialMessages: mockMessages,
          activeChatId: 'thread-1',
          loading: { chats: false, messages: false, submit: false },
          isStreaming: true,
          streamingAnswer: 'This is a streaming answer being generated in real-time...',
          streamingState: 'Generating response...',
        })}
      >
        <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
          <Story />
        </div>
      </ChatContext.Provider>
    ),
  ],
};

/**
 * Long conversation (many messages).
 */
export const LongConversation: Story = {
  args: {
    onCitationClick: fn(),
  },
  decorators: [
    Story => {
      const manyMessages = Array.from({ length: 50 }, (_, i) => ({
        id: `msg-${i + 1}`,
        threadId: 'thread-1',
        content:
          i % 2 === 0
            ? `User question ${Math.floor(i / 2) + 1}`
            : `Assistant answer ${Math.floor(i / 2) + 1} with detailed explanation`,
        role: i % 2 === 0 ? 'user' : 'assistant',
        createdAt: new Date(2025, 11, 5, 10, i).toISOString(),
        confidence: i % 2 === 1 ? 0.85 + Math.random() * 0.15 : undefined,
      }));

      return (
        <ChatContext.Provider
          value={createMockContext({
            initialMessages: manyMessages,
            activeChatId: 'thread-1',
            loading: { chats: false, messages: false, submit: false },
          })}
        >
          <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * Messages with citations.
 */
export const WithCitations: Story = {
  args: {
    onCitationClick: fn(),
  },
  decorators: [
    Story => (
      <ChatContext.Provider
        value={createMockContext({
          initialMessages: mockMessages,
          activeChatId: 'thread-1',
          loading: { chats: false, messages: false, submit: false },
        })}
      >
        <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
          <Story />
        </div>
      </ChatContext.Provider>
    ),
  ],
};

/**
 * Mobile viewport (375px).
 */
export const Mobile: Story = {
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
    chromatic: { viewports: [375] },
  },
  args: {
    onCitationClick: fn(),
  },
  decorators: [
    Story => (
      <ChatContext.Provider
        value={createMockContext({
          initialMessages: mockMessages,
          activeChatId: 'thread-1',
          loading: { chats: false, messages: false, submit: false },
        })}
      >
        <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
          <Story />
        </div>
      </ChatContext.Provider>
    ),
  ],
};

/**
 * Tablet viewport (768px).
 */
export const Tablet: Story = {
  parameters: {
    viewport: { defaultViewport: 'tablet' },
    chromatic: { viewports: [768] },
  },
  args: {
    onCitationClick: fn(),
  },
  decorators: [
    Story => (
      <ChatContext.Provider
        value={createMockContext({
          initialMessages: mockMessages,
          activeChatId: 'thread-1',
          loading: { chats: false, messages: false, submit: false },
        })}
      >
        <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
          <Story />
        </div>
      </ChatContext.Provider>
    ),
  ],
};

/**
 * Desktop viewport (1024px).
 */
export const Desktop: Story = {
  parameters: {
    viewport: { defaultViewport: 'desktop' },
    chromatic: { viewports: [1024] },
  },
  args: {
    onCitationClick: fn(),
  },
  decorators: [
    Story => (
      <ChatContext.Provider
        value={createMockContext({
          initialMessages: mockMessages,
          activeChatId: 'thread-1',
          loading: { chats: false, messages: false, submit: false },
        })}
      >
        <div className="h-[600px] border border-gray-200 rounded-lg overflow-hidden">
          <Story />
        </div>
      </ChatContext.Provider>
    ),
  ],
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  args: {
    onCitationClick: fn(),
  },
  decorators: [
    Story => (
      <div className="dark">
        <ChatContext.Provider
          value={createMockContext({
            initialMessages: mockMessages,
            activeChatId: 'thread-1',
            loading: { chats: false, messages: false, submit: false },
          })}
        >
          <div className="h-[600px] border border-gray-700 rounded-lg overflow-hidden bg-background">
            <Story />
          </div>
        </ChatContext.Provider>
      </div>
    ),
  ],
};
