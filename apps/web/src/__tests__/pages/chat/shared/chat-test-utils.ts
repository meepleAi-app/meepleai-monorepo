/**
 * Shared test utilities for chat test files
 *
 * This module provides common setup, mocks, and test data for all chat test files.
 * Import these utilities to avoid duplication across split test files.
 */

import { api } from '../../../../lib/api';
import { createMockAuthResponse, createMockGame, createMockAgent, createMockChat } from '../../../fixtures/common-fixtures';

// Mock function references that need to be shared across files
export const mockStartStreaming = jest.fn();
export const mockStopStreaming = jest.fn();
export let mockOnComplete: ((answer: string, snippets: any[], metadata: any) => void) | null = null;
export let mockOnError: ((error: string) => void) | null = null;

// Mock API reference
export const mockApi = api as jest.Mocked<typeof api>;

// Original window functions for restoration
export const originalConfirm = window.confirm;

/**
 * Creates standard test data used across chat tests
 */
export const createChatTestData = () => ({
  mockAuthResponse: createMockAuthResponse({
    id: 'user-1',
    email: 'user@example.com',
    displayName: 'Test User',
    role: 'User'
  }),

  mockGames: [
    createMockGame({ id: 'game-1', name: 'Chess' }),
    createMockGame({ id: 'game-2', name: 'Catan' })
  ],

  mockAgents: [
    createMockAgent({ id: 'agent-1', gameId: 'game-1', name: 'Chess Expert', type: 'qa' }),
    createMockAgent({ id: 'agent-2', gameId: 'game-1', name: 'Chess Helper', type: 'qa' })
  ],

  mockEditableAgents: [
    createMockAgent({ id: 'agent-1', gameId: 'game-1', name: 'Editable Agent', type: 'qa' }),
    createMockAgent({ id: 'agent-2', gameId: 'game-1', name: 'Supporting Agent', type: 'qa' })
  ],

  mockChats: [
    createMockChat({
      id: 'chat-1',
      gameId: 'game-1',
      gameName: 'Chess',
      agentId: 'agent-1',
      agentName: 'Chess Expert',
      startedAt: '2025-01-10T10:00:00Z',
      lastMessageAt: '2025-01-10T10:05:00Z'
    }),
    createMockChat({
      id: 'chat-2',
      gameId: 'game-1',
      gameName: 'Chess',
      agentId: 'agent-2',
      agentName: 'Chess Helper',
      startedAt: '2025-01-09T14:00:00Z',
      lastMessageAt: null
    })
  ],

  mockChatWithHistory: {
    id: 'chat-1',
    gameId: 'game-1',
    gameName: 'Chess',
    agentId: 'agent-1',
    agentName: 'Chess Expert',
    startedAt: '2025-01-10T10:00:00Z',
    lastMessageAt: '2025-01-10T10:05:00Z',
    messages: [
      {
        id: 'msg-1',
        level: 'user',
        message: 'How do I castle?',
        metadataJson: null,
        createdAt: '2025-01-10T10:00:00Z'
      },
      {
        id: 'msg-2',
        level: 'agent',
        message: 'Castling is a special move...',
        metadataJson: JSON.stringify({
          snippets: [
            { text: 'Castling rules section', source: 'chess-rules.pdf', page: 5, line: null }
          ]
        }),
        createdAt: '2025-01-10T10:05:00Z'
      }
    ]
  }
});

/**
 * Sets up complete authenticated state with all required data
 * Useful for tests that need full app state
 */
export const setupAuthenticatedState = () => {
  const testData = createChatTestData();
  mockApi.get.mockResolvedValueOnce(testData.mockAuthResponse);
  mockApi.get.mockResolvedValueOnce(testData.mockGames);
  mockApi.get.mockResolvedValueOnce(testData.mockAgents);
  mockApi.get.mockResolvedValueOnce(testData.mockChats);
  return testData;
};

/**
 * Resets all mocks to clean state
 * Call this in beforeEach to ensure test isolation
 */
export const resetAllMocks = () => {
  jest.clearAllMocks();

  // Reset API mocks
  mockApi.get.mockReset();
  mockApi.post.mockReset();
  mockApi.put.mockReset();
  mockApi.delete.mockReset();
  (mockApi.chat.updateMessage as jest.Mock).mockReset();
  (mockApi.chat.deleteMessage as jest.Mock).mockReset();

  // Reset streaming mocks
  mockStartStreaming.mockReset();
  mockStopStreaming.mockReset();
  mockOnComplete = null;
  mockOnError = null;

  // Restore window functions
  window.confirm = originalConfirm;
};

/**
 * Sets up the useChatStreaming mock
 * This function should be used to configure streaming behavior in tests
 */
export const setupStreamingMock = (overrides?: {
  isStreaming?: boolean;
  currentAnswer?: string;
  snippets?: any[];
  state?: any;
  error?: any;
}) => {
  return {
    isStreaming: overrides?.isStreaming ?? false,
    currentAnswer: overrides?.currentAnswer ?? '',
    snippets: overrides?.snippets ?? [],
    state: overrides?.state ?? null,
    error: overrides?.error ?? null
  };
};

/**
 * Sets up a complete chat environment with all necessary mocks
 * This is the primary setup function for chat tests - provides full flexibility
 *
 * @param options Configuration options for the environment
 * @param options.user User data (defaults to standard test user)
 * @param options.game Game data (defaults to Chess)
 * @param options.agent Agent data (defaults to Chess Expert)
 * @param options.chats Array of chat objects (defaults to single chat)
 * @param options.messages Array of messages for the active chat
 * @param options.activeChat Whether to set an active chat (defaults to false)
 * @param options.activeChatId ID of the active chat (defaults to first chat)
 * @param options.sessionMinutes Remaining session minutes (defaults to 30)
 */
export const setupFullChatEnvironment = (options?: {
  user?: Partial<{id: string; email: string; displayName: string; role: string}>;
  game?: Partial<{id: string; name: string}>;
  agent?: Partial<{id: string; gameId: string; name: string; type: string}>;
  chats?: Array<Partial<{id: string; gameId: string; gameName: string; agentId: string; agentName: string; startedAt: string; lastMessageAt: string | null}>>;
  messages?: Array<{id?: string; role: 'user' | 'assistant'; content: string; followUpQuestions?: string[]; feedback?: boolean | null; metadataJson?: any}>;
  activeChat?: boolean;
  activeChatId?: string;
  sessionMinutes?: number;
  additionalGames?: Array<Partial<{id: string; name: string}>>;
  additionalAgents?: Array<Partial<{id: string; gameId: string; name: string; type: string}>>;
}) => {
  const testData = createChatTestData();

  // Setup user (with overrides)
  const mockUser = {
    ...testData.mockAuthResponse,
    ...options?.user
  };

  // Setup game (with overrides)
  const mockGame = createMockGame({
    id: options?.game?.id ?? 'game-1',
    name: options?.game?.name ?? 'Chess',
    ...options?.game
  });

  // Setup agent (with overrides)
  const mockAgent = createMockAgent({
    id: options?.agent?.id ?? 'agent-1',
    gameId: options?.agent?.gameId ?? mockGame.id,
    name: options?.agent?.name ?? 'Chess Expert',
    type: options?.agent?.type ?? 'qa',
    ...options?.agent
  });

  // Setup chats (with overrides or defaults)
  let mockChats;
  if (options?.chats) {
    mockChats = options.chats.map(chat => createMockChat({
      id: chat.id ?? 'chat-1',
      gameId: chat.gameId ?? mockGame.id,
      gameName: chat.gameName ?? mockGame.name,
      agentId: chat.agentId ?? mockAgent.id,
      agentName: chat.agentName ?? mockAgent.name,
      startedAt: chat.startedAt ?? new Date().toISOString(),
      lastMessageAt: chat.lastMessageAt === undefined ? new Date().toISOString() : chat.lastMessageAt,
      ...chat
    }));
  } else {
    mockChats = [createMockChat({
      id: 'chat-1',
      gameId: mockGame.id,
      gameName: mockGame.name,
      agentId: mockAgent.id,
      agentName: mockAgent.name,
      startedAt: new Date().toISOString(),
      lastMessageAt: new Date().toISOString()
    })];
  }

  // Setup messages (with overrides)
  const mockMessages = options?.messages?.map((msg, idx) => ({
    id: msg.id ?? `msg-${idx + 1}`,
    level: msg.role === 'user' ? 'user' : 'agent',
    message: msg.content,
    metadataJson: msg.metadataJson ?? (msg.followUpQuestions ? JSON.stringify({ followUpQuestions: msg.followUpQuestions }) : null),
    feedback: msg.feedback ?? null,
    createdAt: new Date(Date.now() - (100 - idx) * 1000).toISOString()
  })) ?? [];

  // Setup additional games if provided
  const allGames = [mockGame, ...(options?.additionalGames?.map(g => createMockGame(g)) ?? [])];

  // Setup additional agents if provided
  const allAgents = [mockAgent, ...(options?.additionalAgents?.map(a => createMockAgent(a)) ?? [])];

  // Session status
  const mockSessionStatus = { remainingMinutes: options?.sessionMinutes ?? 30 };

  // Mock API responses in order
  mockApi.get.mockResolvedValueOnce(mockUser); // Auth check
  mockApi.get.mockResolvedValueOnce(mockSessionStatus); // Session status
  mockApi.get.mockResolvedValueOnce(allGames); // Games list
  mockApi.get.mockResolvedValueOnce(allAgents); // Agents list
  mockApi.get.mockResolvedValueOnce(mockChats); // Chats list

  // If activeChat requested, setup chat history load
  if (options?.activeChat && mockMessages.length > 0) {
    const activeChatId = options?.activeChatId ?? mockChats[0].id;
    mockApi.get.mockResolvedValueOnce({
      ...mockChats.find(c => c.id === activeChatId),
      messages: mockMessages
    });
  }

  return {
    user: mockUser,
    game: mockGame,
    agent: mockAgent,
    chats: mockChats,
    messages: mockMessages,
    games: allGames,
    agents: allAgents
  };
};