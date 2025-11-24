import { Chat, ChatWithHistory } from '../useMultiGameChat';

/**
 * Shared test helpers and mock data for useMultiGameChat tests
 */

/**
 * Mock chats for Game 1 (Catan)
 */
export const mockChatsGame1: Chat[] = [
  {
    id: 'chat-1',
    gameId: 'game-1',
    gameName: 'Catan',
    agentId: 'agent-1',
    agentName: 'Rules Agent',
    startedAt: '2025-01-01T00:00:00Z',
    lastMessageAt: '2025-01-01T01:00:00Z',
  },
  {
    id: 'chat-2',
    gameId: 'game-1',
    gameName: 'Catan',
    agentId: 'agent-1',
    agentName: 'Rules Agent',
    startedAt: '2025-01-01T02:00:00Z',
    lastMessageAt: null,
  },
];

/**
 * Mock chats for Game 2 (Pandemic)
 */
export const mockChatsGame2: Chat[] = [
  {
    id: 'chat-3',
    gameId: 'game-2',
    gameName: 'Pandemic',
    agentId: 'agent-1',
    agentName: 'Rules Agent',
    startedAt: '2025-01-01T03:00:00Z',
    lastMessageAt: '2025-01-01T04:00:00Z',
  },
];

/**
 * Mock chat with full message history
 */
export const mockChatWithHistory: ChatWithHistory = {
  id: 'chat-1',
  gameId: 'game-1',
  gameName: 'Catan',
  agentId: 'agent-1',
  agentName: 'Rules Agent',
  startedAt: '2025-01-01T00:00:00Z',
  lastMessageAt: '2025-01-01T01:00:00Z',
  messages: [
    {
      id: 'msg-1',
      level: 'user',
      message: 'How do I play?',
      metadataJson: null,
      createdAt: '2025-01-01T00:00:00Z',
    },
    {
      id: 'msg-2',
      level: 'assistant',
      message: 'Here are the rules...',
      metadataJson: JSON.stringify({
        snippets: [
          { text: 'Rule 1', source: 'rules.pdf', page: 1, line: null },
        ],
      }),
      createdAt: '2025-01-01T00:01:00Z',
    },
  ],
};

/**
 * Mock chat with history for Game 1
 */
export const chatGame1: ChatWithHistory = {
  id: 'chat-1',
  gameId: 'game-1',
  gameName: 'Catan',
  agentId: 'agent-1',
  agentName: 'Rules Agent',
  startedAt: '2025-01-01T00:00:00Z',
  lastMessageAt: '2025-01-01T01:00:00Z',
  messages: [
    {
      id: 'msg-1',
      level: 'user',
      message: 'Game 1 message',
      metadataJson: null,
      createdAt: '2025-01-01T00:00:00Z',
    },
  ],
};

/**
 * Mock chat with history for Game 2
 */
export const chatGame2: ChatWithHistory = {
  id: 'chat-2',
  gameId: 'game-2',
  gameName: 'Pandemic',
  agentId: 'agent-1',
  agentName: 'Rules Agent',
  startedAt: '2025-01-01T02:00:00Z',
  lastMessageAt: '2025-01-01T03:00:00Z',
  messages: [
    {
      id: 'msg-2',
      level: 'user',
      message: 'Game 2 message',
      metadataJson: null,
      createdAt: '2025-01-01T02:00:00Z',
    },
  ],
};

/**
 * Mock chat with malformed JSON metadata
 */
export const chatWithBadJson: ChatWithHistory = {
  ...mockChatWithHistory,
  messages: [
    {
      id: 'msg-1',
      level: 'assistant',
      message: 'Test message',
      metadataJson: '{invalid json}',
      createdAt: '2025-01-01T00:00:00Z',
    },
  ],
};

/**
 * Mock new chat for creation tests
 */
export const mockNewChat: Chat = {
  id: 'chat-new',
  gameId: 'game-1',
  gameName: 'Catan',
  agentId: 'agent-1',
  agentName: 'Rules Agent',
  startedAt: '2025-01-01T05:00:00Z',
  lastMessageAt: null,
};
