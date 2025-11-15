import type { Meta, StoryObj } from '@storybook/react';
import { ChatHistoryItem } from './ChatHistoryItem';
import { ChatThread } from '@/types';
import { fn } from '@storybook/test';

/**
 * ChatHistoryItem - Individual chat item in the history list.
 * Displays a chat session with agent name, timestamp, and delete button.
 * Highlights active chat and supports keyboard navigation.
 */
const meta = {
  title: 'Chat/ChatHistoryItem',
  component: ChatHistoryItem,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    isActive: {
      control: 'boolean',
      description: 'Whether this chat is currently active',
    },
  },
  args: {
    onSelect: fn(),
    onDelete: fn(),
  },
} satisfies Meta<typeof ChatHistoryItem>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock chat data
const mockChat: ChatThread = {
  id: 'thread-1',
  title: 'Gloomhaven Rules Questions',
  gameId: 'game-1',
  agentId: 'agent-1',
  createdAt: new Date('2024-01-15T10:30:00'),
  lastMessageAt: new Date('2024-01-15T14:45:00'),
  messageCount: 12,
};

/**
 * Default chat history item (inactive)
 */
export const Default: Story = {
  args: {
    chat: mockChat,
    isActive: false,
  },
};

/**
 * Active chat (highlighted)
 */
export const Active: Story = {
  args: {
    chat: mockChat,
    isActive: true,
  },
};

/**
 * Recent chat (today)
 */
export const RecentChat: Story = {
  args: {
    chat: {
      ...mockChat,
      title: 'Wingspan Setup Questions',
      createdAt: new Date(),
      lastMessageAt: new Date(),
    },
    isActive: false,
  },
};

/**
 * Old chat (last week)
 */
export const OldChat: Story = {
  args: {
    chat: {
      ...mockChat,
      title: 'Spirit Island Phase Clarification',
      createdAt: new Date('2024-01-08T09:00:00'),
      lastMessageAt: new Date('2024-01-08T10:30:00'),
    },
    isActive: false,
  },
};

/**
 * Chat without title (fallback to "Chat")
 */
export const NoTitle: Story = {
  args: {
    chat: {
      ...mockChat,
      title: undefined,
    },
    isActive: false,
  },
};

/**
 * Long title text
 */
export const LongTitle: Story = {
  args: {
    chat: {
      ...mockChat,
      title: 'Comprehensive Guide to Understanding Complex Gloomhaven Battle Mechanics and Card Interactions',
    },
    isActive: false,
  },
};

/**
 * Multiple chat items list
 */
export const ChatList: Story = {
  render: () => {
    const chats: ChatThread[] = [
      {
        id: 'thread-1',
        title: 'Gloomhaven Rules',
        gameId: 'game-1',
        agentId: 'agent-1',
        createdAt: new Date(),
        lastMessageAt: new Date(),
        messageCount: 5,
      },
      {
        id: 'thread-2',
        title: 'Wingspan Setup',
        gameId: 'game-2',
        agentId: 'agent-2',
        createdAt: new Date('2024-01-14T14:00:00'),
        lastMessageAt: new Date('2024-01-14T15:30:00'),
        messageCount: 8,
      },
      {
        id: 'thread-3',
        title: 'Terraforming Mars Strategy',
        gameId: 'game-3',
        agentId: 'agent-1',
        createdAt: new Date('2024-01-12T10:00:00'),
        lastMessageAt: new Date('2024-01-12T12:00:00'),
        messageCount: 15,
      },
      {
        id: 'thread-4',
        title: 'Spirit Island Powers',
        gameId: 'game-4',
        agentId: 'agent-3',
        createdAt: new Date('2024-01-10T09:00:00'),
        lastMessageAt: new Date('2024-01-10T11:00:00'),
        messageCount: 10,
      },
    ];

    return (
      <ul className="space-y-2 max-w-md">
        {chats.map((chat, idx) => (
          <ChatHistoryItem
            key={chat.id}
            chat={chat}
            isActive={idx === 1}
            onSelect={() => console.log('Selected:', chat.id)}
            onDelete={() => console.log('Delete:', chat.id)}
          />
        ))}
      </ul>
    );
  },
};

/**
 * Interactive selection demo
 */
export const Interactive: Story = {
  render: () => {
    const [activeId, setActiveId] = React.useState('thread-2');
    const [chats, setChats] = React.useState<ChatThread[]>([
      {
        id: 'thread-1',
        title: 'Gloomhaven Rules',
        gameId: 'game-1',
        agentId: 'agent-1',
        createdAt: new Date(),
        lastMessageAt: new Date(),
        messageCount: 5,
      },
      {
        id: 'thread-2',
        title: 'Wingspan Setup',
        gameId: 'game-2',
        agentId: 'agent-2',
        createdAt: new Date('2024-01-14T14:00:00'),
        lastMessageAt: new Date('2024-01-14T15:30:00'),
        messageCount: 8,
      },
      {
        id: 'thread-3',
        title: 'Terraforming Mars',
        gameId: 'game-3',
        agentId: 'agent-1',
        createdAt: new Date('2024-01-12T10:00:00'),
        lastMessageAt: new Date('2024-01-12T12:00:00'),
        messageCount: 15,
      },
    ]);

    const handleDelete = (id: string) => {
      setChats(chats.filter((c) => c.id !== id));
      if (activeId === id) {
        setActiveId(chats[0]?.id || '');
      }
    };

    return (
      <ul className="space-y-2 max-w-md">
        {chats.map((chat) => (
          <ChatHistoryItem
            key={chat.id}
            chat={chat}
            isActive={activeId === chat.id}
            onSelect={() => setActiveId(chat.id)}
            onDelete={() => handleDelete(chat.id)}
          />
        ))}
      </ul>
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
        <Story />
      </div>
    ),
  ],
  args: {
    chat: mockChat,
    isActive: true,
  },
};

// Import React for interactive stories
import React from 'react';
