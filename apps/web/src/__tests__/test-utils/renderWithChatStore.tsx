/**
 * Test utility for rendering components with Zustand chat store
 *
 * Provides:
 * - Pre-configured chat store with initial state
 * - AuthProvider wrapper for authentication context
 * - Flexible state overrides for test scenarios
 *
 * Usage:
 *   renderWithChatStore(<Component />, {
 *     initialState: { games: [...], selectedGameId: 'game-1' }
 *   })
 */

import React, { PropsWithChildren } from 'react';
import { render, RenderOptions } from '@testing-library/react';
import { create, StoreApi } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';

import { ChatStore, LoadingState } from '@/store/chat/types';
import { createSessionSlice } from '@/store/chat/slices/sessionSlice';
import { createGameSlice } from '@/store/chat/slices/gameSlice';
import { createChatSlice } from '@/store/chat/slices/chatSlice';
import { createMessagesSlice } from '@/store/chat/slices/messagesSlice';
import { createUISlice } from '@/store/chat/slices/uiSlice';

// ============================================================================
// Test Store Creation
// ============================================================================

/**
 * Creates a test store instance with optional initial state
 * Simplified middleware stack (no persistence or devtools)
 */
export function createTestChatStore(initialState: Partial<ChatStore> = {}): StoreApi<ChatStore> {
  const store = create<ChatStore>()(
    subscribeWithSelector(
      immer((set, get, storeApi) => {
        const baseStore = {
          ...createSessionSlice(set, get, storeApi),
          ...createGameSlice(set, get, storeApi),
          ...createChatSlice(set, get, storeApi),
          ...createMessagesSlice(set, get, storeApi),
          ...createUISlice(set, get, storeApi),
        };

        // Apply initial state overrides
        return { ...baseStore, ...initialState };
      })
    )
  );

  return store;
}

// ============================================================================
// Store Provider
// ============================================================================

/**
 * Test context provider that exposes the store for assertions
 */
export const TestChatStoreContext = React.createContext<StoreApi<ChatStore> | null>(null);

interface TestChatStoreProviderProps extends PropsWithChildren {
  store: StoreApi<ChatStore>;
}

export function TestChatStoreProvider({ children, store }: TestChatStoreProviderProps) {
  return (
    <TestChatStoreContext.Provider value={store}>
      {children}
    </TestChatStoreContext.Provider>
  );
}

// ============================================================================
// Mock useChatStore Hook
// ============================================================================

let testStore: StoreApi<ChatStore> | null = null;

/**
 * Mock implementation of useChatStore that uses the test store
 * This is set globally so all components use the same test store instance
 */
export function mockUseChatStore<T>(selector: (state: ChatStore) => T): T {
  if (!testStore) {
    throw new Error('Test store not initialized. Call renderWithChatStore first.');
  }
  return selector(testStore.getState());
}

// ============================================================================
// Render Helper
// ============================================================================

export interface RenderWithChatStoreOptions extends Omit<RenderOptions, 'wrapper'> {
  initialState?: Partial<ChatStore>;
}

/**
 * Renders a component with a test chat store
 *
 * @param ui - Component to render
 * @param options - Render options with optional initial state
 * @returns Render result with store access
 */
export function renderWithChatStore(
  ui: React.ReactElement,
  options: RenderWithChatStoreOptions = {}
) {
  const { initialState = {}, ...renderOptions } = options;

  // Create test store with initial state
  const store = createTestChatStore(initialState);
  testStore = store;

  // Mock the useChatStore hook globally
  jest.mock('@/store/chat/ChatStoreProvider', () => ({
    useChatStore: mockUseChatStore,
    ChatStoreProvider: ({ children }: PropsWithChildren) => (
      <TestChatStoreProvider store={store}>{children}</TestChatStoreProvider>
    ),
  }));

  const Wrapper = ({ children }: PropsWithChildren) => (
    <TestChatStoreProvider store={store}>{children}</TestChatStoreProvider>
  );

  const result = render(ui, { wrapper: Wrapper, ...renderOptions });

  return {
    ...result,
    store,
    getState: () => store.getState(),
    setState: (state: Partial<ChatStore>) => store.setState(state as any),
  };
}

// ============================================================================
// Default Test State Fixtures
// ============================================================================

export const DEFAULT_LOADING_STATE: LoadingState = {
  chats: false,
  messages: false,
  sending: false,
  creating: false,
  updating: false,
  deleting: false,
  games: false,
  agents: false,
};

export const DEFAULT_CHAT_STORE_STATE: Partial<ChatStore> = {
  // Session
  selectedGameId: null,
  selectedAgentId: null,
  sidebarCollapsed: false,

  // Games
  games: [],
  agents: [],

  // Chats
  chatsByGame: {},
  activeChatIds: {},

  // Messages
  messagesByChat: {},

  // UI
  loading: DEFAULT_LOADING_STATE,
  error: null,
  inputValue: '',
  editingMessageId: null,
  editContent: '',
  searchMode: 'hybrid',
};

/**
 * Creates a complete chat state with sample data for integration tests
 */
export function createFullChatState(): Partial<ChatStore> {
  const gameId = 'game-1';
  const agentId = 'agent-1';
  const chatId = 'chat-1';

  return {
    selectedGameId: gameId,
    selectedAgentId: agentId,
    sidebarCollapsed: false,

    games: [
      {
        id: gameId,
        title: 'Chess',
        createdAt: new Date().toISOString(),
      },
    ],

    agents: [
      {
        id: agentId,
        name: 'QA Agent',
        type: 'qa',
        strategyName: 'default',
        strategyParameters: {},
        isActive: true,
        createdAt: new Date().toISOString(),
        lastInvokedAt: null,
        invocationCount: 0,
        isRecentlyUsed: false,
        isIdle: true,
      },
    ],

    chatsByGame: {
      [gameId]: [
        {
          id: chatId,
          gameId,
          userId: 'user-1',
          title: 'Chess Rules',
          createdAt: new Date().toISOString(),
          lastMessageAt: new Date().toISOString(),
          messageCount: 2,
          messages: [],
        },
      ],
    },

    activeChatIds: {
      [gameId]: chatId,
    },

    messagesByChat: {
      [chatId]: [
        {
          id: 'msg-1',
          role: 'user',
          content: 'How does the knight move?',
          timestamp: new Date(),
        },
        {
          id: 'msg-2',
          role: 'assistant',
          content: 'The knight moves in an L-shape.',
          timestamp: new Date(),
        },
      ],
    },

    loading: DEFAULT_LOADING_STATE,
    error: null,
    inputValue: '',
    editingMessageId: null,
    editContent: '',
    searchMode: 'hybrid',
  };
}
