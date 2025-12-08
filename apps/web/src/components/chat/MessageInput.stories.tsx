import type { Meta, StoryObj } from '@storybook/react';
import { MessageInput } from './MessageInput';
import { ChatContext, type ChatContextValue } from '@/store/chat/StorybookContext';

/**
 * MessageInput component for sending chat messages.
 *
 * ## Features
 * - **Input handling**: Text input with character limit
 * - **Send button**: Disabled when input empty or invalid
 * - **Search mode toggle**: Vector vs Hybrid search (AI-14)
 * - **Streaming support**: Stop button during streaming (Issue #1007)
 * - **Loading states**: Disabled controls during send
 * - **Accessibility**: ARIA labels, keyboard navigation
 *
 * ## Visual Testing Coverage
 * - Default state (empty input)
 * - With text input
 * - Loading state during send
 * - Streaming state with stop button
 * - Disabled state (no game/agent selected)
 * - Search mode variations (Vector, Hybrid)
 * - Dark theme variant
 */

/**
 * Issue #1676: Mock context helper for Storybook isolation
 * Uses StorybookContext (temporary, will migrate to ChatStoreProvider in PR #3)
 */
const createMockContext = (overrides: Record<string, unknown> = {}): ChatContextValue => ({
  inputValue: '',
  setInputValue: () => {},
  sendMessage: async () => {},
  selectedGameId: 'game-123',
  selectedAgentId: 'agent-456',
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
  searchMode: 'vector',
  setSearchMode: () => {},
  isStreaming: false,
  streamingAnswer: '',
  streamingState: 'idle',
  streamingCitations: [],
  stopStreaming: () => {},
  authUser: null,
  games: [],
  agents: [],
  selectGame: () => {},
  selectAgent: () => {},
  chats: [],
  activeChatId: null,
  messages: [],
  createChat: async () => {},
  deleteChat: async () => {},
  selectChat: async () => {},
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
  ...overrides,
});

const meta = {
  title: 'Chat/MessageInput',
  component: MessageInput,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Message input form with send button and search mode toggle. Uses Zustand store for state management.',
      },
    },
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
          <div className="w-full max-w-2xl">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
} satisfies Meta<typeof MessageInput>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default state with empty input.
 * Send button is disabled when input is empty.
 */
export const Default: Story = {};

/**
 * With text input.
 * Shows enabled send button when message is typed.
 */
export const WithInput: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        inputValue: 'What are the rules for building settlements?',
      });

      return (
        <ChatContext.Provider value={mockContext as never}>
          <div className="w-full max-w-2xl">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * Loading state during message send.
 * Shows "Invio..." loading text and disabled controls.
 */
export const LoadingState: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        inputValue: 'How many victory points do I need?',
        loading: {
          games: false,
          agents: false,
          chats: false,
          messages: false,
          sending: true,
          creating: false,
          updating: false,
          deleting: false,
        },
      });

      return (
        <ChatContext.Provider value={mockContext as never}>
          <div className="w-full max-w-2xl">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * Streaming state with stop button.
 * Shows red stop button instead of send button.
 */
export const StreamingState: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        inputValue: 'Can I trade resources with the bank?',
        searchMode: 'hybrid',
        isStreaming: true,
        streamingAnswer: 'Yes, you can trade with the bank...',
        streamingState: 'streaming',
      });

      return (
        <ChatContext.Provider value={mockContext as never}>
          <div className="w-full max-w-2xl">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story:
          'Streaming state shows red stop button to abort streaming response. Input and search mode are disabled.',
      },
    },
  },
};

/**
 * Hybrid search mode selected.
 * Shows hybrid mode toggle active.
 */
export const HybridSearchMode: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        searchMode: 'hybrid',
      });

      return (
        <ChatContext.Provider value={mockContext as never}>
          <div className="w-full max-w-2xl">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Search mode toggle showing hybrid mode selected (AI-14 feature).',
      },
    },
  },
};

/**
 * Disabled state when no game selected.
 * All controls disabled until game and agent are selected.
 */
export const DisabledState: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        selectedGameId: null,
        selectedAgentId: null,
      });

      return (
        <ChatContext.Provider value={mockContext as never}>
          <div className="w-full max-w-2xl">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Disabled state when no game or agent is selected. User must select game first.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows message input on dark background.
 */
export const DarkTheme: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        inputValue: 'Come si costruisce una strada?',
      });

      return (
        <ChatContext.Provider value={mockContext as never}>
          <div className="dark">
            <div className="w-full max-w-2xl bg-background">
              <Story />
            </div>
          </div>
        </ChatContext.Provider>
      );
    },
  ],
  parameters: {
    backgrounds: { default: 'dark' },
  },
};
