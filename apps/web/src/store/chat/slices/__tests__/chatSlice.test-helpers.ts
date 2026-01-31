/**
 * Test Helpers for chatSlice
 *
 * Shared utilities, mocks, and test data factories for chatSlice tests
 */

import { ChatThread, Message } from '@/types';

/**
 * Create a mock chat thread with default values
 */
export function createMockChatThread(overrides?: Partial<ChatThread>): ChatThread {
  return {
    id: 'aa0e8400-e29b-41d4-a716-000000000001',
    gameId: '770e8400-e29b-41d4-a716-000000000001',
    userId: '990e8400-e29b-41d4-a716-000000000001',
    title: 'Test Thread',
    status: 'Active',
    createdAt: '2024-01-01T00:00:00Z',
    lastMessageAt: null,
    messageCount: 0,
    messages: [],
    ...overrides,
  };
}

/**
 * Create a mock message with default values
 */
export function createMockMessage(overrides?: Partial<Message>): Message {
  return {
    id: 'msg-1',
    role: 'user' as const,
    content: 'Test message',
    timestamp: new Date('2024-01-01T00:00:00Z'),
    ...overrides,
  };
}

/**
 * Create multiple mock chat threads with sequential IDs
 */
export function createMockChatThreads(count: number, baseOverrides?: Partial<ChatThread>): ChatThread[] {
  return Array.from({ length: count }, (_, i) =>
    createMockChatThread({
      id: `thread-${i}`,
      title: `Thread ${i}`,
      createdAt: `2024-01-0${(i % 9) + 1}T00:00:00Z`,
      ...baseOverrides,
    })
  );
}

/**
 * Setup console mocks to avoid noise in tests
 */
export function setupConsoleMocks(): { mockConsoleError: Mock; restore: () => void } {
  const originalConsoleError = console.error;
  const mockConsoleError = vi.fn();
  console.error = mockConsoleError;

  return {
    mockConsoleError,
    restore: () => {
      console.error = originalConsoleError;
    },
  };
}

/**
 * Reset chat store to clean initial state
 */
export function resetChatStore(useChatStore: any): void {
  useChatStore.setState({
    chatsByGame: {},
    activeChatIds: {},
    messagesByChat: {},
    selectedGameId: null,
    selectedAgentId: null,
    loading: {
      chats: false,
      messages: false,
      sending: false,
      creating: false,
      updating: false,
      deleting: false,
      games: false,
      agents: false,
    },
    error: null,
  });
}

/**
 * Verify loading state transitions
 */
export function expectLoadingTransitions(
  setLoadingSpy: Mock,
  loadingKey: string,
  expectedStates: boolean[] = [true, false]
): void {
  expectedStates.forEach((state) => {
    expect(setLoadingSpy).toHaveBeenCalledWith(loadingKey, state);
  });
}

/**
 * Get chat by ID from store state
 */
export function getChatById(state: any, gameId: string, threadId: string): ChatThread | undefined {
  const chats = state.chatsByGame[gameId];
  return chats?.find((chat: ChatThread) => chat.id === threadId);
}
