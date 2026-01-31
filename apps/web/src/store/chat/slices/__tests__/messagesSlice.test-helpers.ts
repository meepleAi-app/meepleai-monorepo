/**
 * Test Helpers for messagesSlice Tests
 *
 * Shared setup utilities, mocks, and helper functions for messagesSlice test suite.
 * Extracted from monolithic test file for better organization.
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';
import { createMessagesSlice } from '../messagesSlice';
import { ChatStore } from '../../types';
import { ChatThread } from '@/types';
import { api } from '@/lib/api';

// Export mocked API
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadById: vi.fn(),
      createThread: vi.fn(),
      addMessage: vi.fn(),
      updateMessage: vi.fn(),
      deleteMessage: vi.fn(),
      submitAgentFeedback: vi.fn(),
    },
  },
}));

export const mockApi = api as Mocked<typeof api>;
export const mockChat = mockApi.chat as Mocked<typeof api.chat>;

/**
 * Create test store with minimal slices for messagesSlice testing
 */
export const createTestStore = () => {
  return create<ChatStore>()(
    subscribeWithSelector(
      immer((...a) => ({
        // Messages slice (what we're testing)
        ...createMessagesSlice(...a),

        // Minimal session slice
        selectedGameId: null,
        selectedAgentId: null,
        sidebarCollapsed: false,
        selectGame: (gameId: string | null) => {},
        selectAgent: (agentId: string | null) => {},
        toggleSidebar: () => {},
        setSidebarCollapsed: (collapsed: boolean) => {},

        // Minimal game slice
        games: [],
        agents: [],
        setGames: (games) => {},
        setAgents: (agents) => {},
        loadGames: async () => {},
        loadAgents: async () => {}, // Issue #868: Agents are global, no gameId parameter

        // Minimal chat slice
        chatsByGame: {},
        activeChatIds: {},
        loadChats: async (gameId: string) => {},
        createChat: async () => {},
        deleteChat: async (chatId: string) => {},
        selectChat: async (chatId: string) => {},
        updateChatTitle: (chatId: string, title: string) => {},

        // Minimal UI slice
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
        inputValue: '',
        editingMessageId: null,
        editContent: '',
        searchMode: 'semantic',
        setLoading: (key, value) => {},
        setError: (error) => {},
        clearError: () => {},
        setInputValue: (value) => {},
        startEdit: (messageId, content) => {},
        cancelEdit: () => {},
        saveEdit: async (editMessageFn) => {},
        setEditContent: (content) => {},
        setSearchMode: (mode) => {},
      }))
    )
  );
};

/**
 * Helper to create mock ChatThread with default values
 */
export const createMockThread = (overrides?: Partial<ChatThread>): ChatThread => ({
  id: 'aa0e8400-e29b-41d4-a716-000000000001',
  gameId: '770e8400-e29b-41d4-a716-000000000001',
  title: 'Test Thread',
  createdAt: '2025-01-01T00:00:00Z',
  lastMessageAt: null,
  messageCount: 0,
  messages: [],
  ...overrides,
});

/**
 * Helper to create mock ChatMessageResponse with default values
 */
export const createMockMessageResponse = (overrides?: Partial<any>): any => ({
  id: 'msg-1',
  chatId: 'aa0e8400-e29b-41d4-a716-000000000001',
  userId: '990e8400-e29b-41d4-a716-000000000001',
  level: 'info',
  content: 'Test message',
  sequenceNumber: 1,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: null,
  isDeleted: false,
  deletedAt: null,
  ...overrides,
});

/**
 * Setup test environment before each test
 */
export const setupTestEnvironment = () => {
  const store = createTestStore();
  const setState = vi.spyOn(store, 'setState');
  const setLoading = vi.fn();
  const setError = vi.fn();
  const updateChatTitle = vi.fn();

  // Override store methods with mocks
  store.setState({
    setLoading,
    setError,
    updateChatTitle,
  });

  return {
    store,
    setState,
    setLoading,
    setError,
    updateChatTitle,
  };
};
