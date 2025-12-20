import type { Meta, StoryObj } from '@storybook/react';
import { ChatHistory } from './ChatHistory';
import { ChatContext } from '@/store/chat/StorybookContext';

/**
 * Chat History - List of chat threads
 *
 * ## Features
 * - **Thread List**: Active and archived threads
 * - **Loading State**: Skeleton loaders while fetching
 * - **Empty State**: Prompt to start first conversation
 * - **Thread Selection**: Click to switch between threads
 * - **Thread Deletion**: Remove unwanted threads
 * - **Status Grouping**: Separates active from archived
 *
 * ## Integration
 * Uses ChatProvider context for state management.
 */
const meta = {
  title: 'Chat/ChatHistory',
  component: ChatHistory,
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
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
} satisfies Meta<typeof ChatHistory>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock thread data
const mockThreads = [
  {
    id: 'thread-1',
    gameId: 'catan',
    title: 'Settlement placement rules',
    status: 'Active',
    createdAt: '2025-12-05T10:00:00Z',
    updatedAt: '2025-12-05T12:30:00Z',
  },
  {
    id: 'thread-2',
    gameId: 'wingspan',
    title: 'Bird card activation order',
    status: 'Active',
    createdAt: '2025-12-04T15:00:00Z',
    updatedAt: '2025-12-05T09:00:00Z',
  },
  {
    id: 'thread-3',
    gameId: 'ticket-to-ride',
    title: 'Longest route scoring',
    status: 'Active',
    createdAt: '2025-12-03T14:00:00Z',
    updatedAt: '2025-12-04T18:00:00Z',
  },
  {
    id: 'thread-4',
    gameId: 'catan',
    title: 'Robber movement clarification',
    status: 'Closed',
    createdAt: '2025-12-01T10:00:00Z',
    updatedAt: '2025-12-01T10:30:00Z',
  },
];

/**
 * Default chat history with active and archived threads.
 */
export const Default: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        chats: mockThreads as any,
        activeChatId: 'thread-1',
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * Loading state while fetching threads.
 */
export const Loading: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        loading: { ...createMockContext().loading, chats: true },
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * Empty state with no threads.
 */
export const Empty: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({ chats: [] });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * Only active threads (no archived).
 */
export const OnlyActive: Story = {
  decorators: [
    Story => {
      const activeThreads = mockThreads.filter(t => t.status === 'Active');
      const mockContext = createMockContext({
        chats: activeThreads as any,
        activeChatId: 'thread-1',
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * With selected thread highlighted.
 */
export const SelectedThread: Story = {
  decorators: [
    Story => {
      const mockContext = createMockContext({
        chats: mockThreads as any,
        activeChatId: 'thread-2',
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * Long list of threads (scrollable).
 */
export const LongList: Story = {
  decorators: [
    Story => {
      const manyThreads = Array.from({ length: 20 }, (_, i) => ({
        id: `thread-${i + 1}`,
        gameId: 'catan',
        title: `Thread ${i + 1}: Question about game rules`,
        status: i < 15 ? 'Active' : 'Closed',
        createdAt: new Date(2025, 11, 5 - i).toISOString(),
        updatedAt: new Date(2025, 11, 5 - i).toISOString(),
      }));

      const mockContext = createMockContext({
        chats: manyThreads as any,
        activeChatId: 'thread-1',
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
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
  decorators: [
    Story => {
      const mockContext = createMockContext({
        chats: mockThreads as any,
        activeChatId: 'thread-1',
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-full border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
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
  decorators: [
    Story => {
      const mockContext = createMockContext({
        chats: mockThreads as any,
        activeChatId: 'thread-1',
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
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
  decorators: [
    Story => {
      const mockContext = createMockContext({
        chats: mockThreads as any,
        activeChatId: 'thread-1',
      });
      return (
        <ChatContext.Provider value={mockContext}>
          <div className="h-[600px] w-[300px] border border-gray-200 rounded-lg overflow-hidden">
            <Story />
          </div>
        </ChatContext.Provider>
      );
    },
  ],
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => {
      const mockContext = createMockContext({
        chats: mockThreads as any,
        activeChatId: 'thread-1',
      });
      return (
        <div className="dark">
          <ChatContext.Provider value={mockContext}>
            <div className="h-[600px] w-[300px] border border-gray-700 rounded-lg overflow-hidden bg-background">
              <Story />
            </div>
          </ChatContext.Provider>
        </div>
      );
    },
  ],
};
