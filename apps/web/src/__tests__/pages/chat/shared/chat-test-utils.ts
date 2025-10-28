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
