/**
 * Chat Page - UI Interactions Tests
 *
 * Tests for UI interactions and visual elements in the chat interface.
 * This file focuses on sidebar toggling, header displays, and chat preview formatting.
 *
 * Migrated to Zustand (Issue #1083)
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChatPage from '../../../components/pages/ChatPage';
import { api } from '../../../lib/api';
import { useChatStore } from '@/store/chat';
import { AuthProvider } from '../../../components/auth/AuthProvider';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Mock BottomNav to avoid loading state issues
jest.mock('../../../components/chat/BottomNav', () => ({
  BottomNav: () => null,
}));

// Mock ChatSidebar with collapse functionality using Zustand
const mockSidebarProps = {
  isCollapsed: false,
  onToggleCollapse: jest.fn(),
};

jest.mock('../../../components/chat/ChatSidebar', () => ({
  ChatSidebar: () => {
    const React = require('react');
    const { useChatStore } = require('@/store/chat');
    const [isCollapsed, setIsCollapsed] = React.useState(mockSidebarProps.isCollapsed);
    const { selectedGameId } = useChatStore();

    React.useEffect(() => {
      setIsCollapsed(mockSidebarProps.isCollapsed);
    }, []);

    return (
      <div data-testid="chat-sidebar">
        <button
          title={isCollapsed ? 'Mostra sidebar' : 'Nascondi sidebar'}
          onClick={() => {
            const newCollapsed = !isCollapsed;
            setIsCollapsed(newCollapsed);
            mockSidebarProps.isCollapsed = newCollapsed;
            mockSidebarProps.onToggleCollapse();
          }}
        >
          {isCollapsed ? '→' : '←'}
        </button>
        {!isCollapsed && selectedGameId && (
          <div data-testid="sidebar-content">Sidebar Content</div>
        )}
      </div>
    );
  },
}));

// Mock ChatContent with header using Zustand
jest.mock('../../../components/chat/ChatContent', () => ({
  ChatContent: () => {
    const { useChatStore } = require('@/store/chat');
    const { useActiveChat, useSelectedGame, useSelectedAgent } = require('@/store/chat');

    const selectedGame = useSelectedGame();
    const selectedAgent = useSelectedAgent();
    const activeChat = useActiveChat();

    const gameName = selectedGame?.name;
    const agentName = selectedAgent && activeChat ? selectedAgent.name : undefined;

    return (
      <div data-testid="chat-content">
        <h1>MeepleAI Chat</h1>
        {agentName ? (
          <h2>{agentName}</h2>
        ) : gameName ? (
          <div>{gameName}</div>
        ) : (
          <h2>Seleziona o crea una chat</h2>
        )}
      </div>
    );
  },
}));

// Mock ExportChatModal
jest.mock('../../../components/ExportChatModal', () => ({
  ExportChatModal: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="export-modal">modal</div> : null,
}));

// Mock API
jest.mock('../../../lib/api');

const mockApi = api as jest.Mocked<typeof api>;

// Mock Zustand store
jest.mock('@/store/chat', () => {
  const actual = jest.requireActual('@/store/chat');
  return {
    ...actual,
    useChatStore: jest.fn(),
    useSelectedGame: jest.fn(() => undefined),
    useSelectedAgent: jest.fn(() => undefined),
    useActiveChat: jest.fn(() => null),
  };
});

const mockUseChatStore = useChatStore as jest.MockedFunction<typeof useChatStore>;

describe('ChatPage - UI Interactions', () => {
  const userResponse = {
    user: {
      id: '990e8400-e29b-41d4-a716-000000000001',
      email: 'user@example.com',
      displayName: 'Test User',
      role: 'User',
    },
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
  };

  const defaultStoreState = {
    // Session state
    selectedGameId: null,
    selectedAgentId: null,
    sidebarCollapsed: false,
    selectGame: jest.fn(),
    selectAgent: jest.fn(),
    toggleSidebar: jest.fn(),
    setSidebarCollapsed: jest.fn(),

    // Game state
    games: [],
    agents: [],
    setGames: jest.fn(),
    setAgents: jest.fn(),
    loadGames: jest.fn(),
    loadAgents: jest.fn(),

    // Chat state
    chatsByGame: {},
    activeChatIds: {},
    loadChats: jest.fn(),
    createChat: jest.fn(),
    deleteChat: jest.fn(),
    selectChat: jest.fn(),
    updateChatTitle: jest.fn(),

    // Messages state
    messagesByChat: {},
    loadMessages: jest.fn(),
    sendMessage: jest.fn(),
    editMessage: jest.fn(),
    deleteMessage: jest.fn(),
    setMessageFeedback: jest.fn(),
    addOptimisticMessage: jest.fn(),
    removeOptimisticMessage: jest.fn(),
    updateMessageInThread: jest.fn(),

    // UI state
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
    searchMode: 'hybrid',
    setLoading: jest.fn(),
    setError: jest.fn(),
    clearError: jest.fn(),
    setInputValue: jest.fn(),
    startEdit: jest.fn(),
    cancelEdit: jest.fn(),
    saveEdit: jest.fn(),
    setEditContent: jest.fn(),
    setSearchMode: jest.fn(),
  };

  // Helper to render with QueryClientProvider and AuthProvider
  function renderWithAuth(component: React.ReactElement) {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
          staleTime: 0,
        },
      },
    });

    return render(
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          {component}
        </AuthProvider>
      </QueryClientProvider>
    );
  }

  beforeEach(() => {
    jest.clearAllMocks();
    mockSidebarProps.isCollapsed = false;
    mockSidebarProps.onToggleCollapse.mockClear();

    // Default mock context with loading state
    mockUseChatStore.mockReturnValue({
      selectedGameId: null,
      selectedAgentId: null,
      activeChatId: null,
      messages: [],
      isStreaming: false,
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
      createChat: jest.fn(),
    });

    // Default Zustand store state
    mockUseChatStore.mockReturnValue(defaultStoreState);

    // Default API setup
    mockApi.get.mockResolvedValue(userResponse);
  });

  it('toggles sidebar when collapse button is clicked', async () => {
    renderWithAuth(<ChatPage />);

    // Wait for chat page to be fully rendered
    await waitFor(() => {
      expect(screen.getByTestId('chat-sidebar')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const collapseButton = screen.getByTitle(/Nascondi sidebar/i);

    await user.click(collapseButton);

    // Check button changes to expand icon
    await waitFor(() => {
      expect(screen.getByTitle(/Mostra sidebar/i)).toBeInTheDocument();
    });

    expect(mockSidebarProps.onToggleCollapse).toHaveBeenCalled();
  });

  it('shows game name in header when game is selected', async () => {
    const { useSelectedGame } = require('@/store/chat');

    // Mock selected game
    const mockGame = {
      id: '770e8400-e29b-41d4-a716-000000000001',
      name: 'Chess',
      createdAt: new Date().toISOString()
    };

    useSelectedGame.mockReturnValue(mockGame);

    mockUseChatStore.mockReturnValue({
      ...defaultStoreState,
      selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
      games: [mockGame],
    });

    mockUseChatStore.mockReturnValue({
      selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
      selectedAgentId: null,
      activeChatId: null,
      messages: [],
      isStreaming: false,
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
      createChat: jest.fn(),
    });

    renderWithAuth(<ChatPage />);

    // Wait for chat page to be fully rendered
    await waitFor(() => {
      expect(screen.getByTestId('chat-content')).toBeInTheDocument();
    });

    // Wait for game data to load
    await waitFor(() => {
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });
  });

  it('shows agent name in header when chat is active', async () => {
    const { useSelectedGame, useSelectedAgent, useActiveChat } = require('@/store/chat');

    const mockGame = {
      id: '770e8400-e29b-41d4-a716-000000000001',
      name: 'Chess',
      createdAt: new Date().toISOString()
    };

    const mockAgent = {
      id: 'agent-1',
      gameId: '770e8400-e29b-41d4-a716-000000000001',
      name: 'Chess Expert',
      kind: 'qa',
      createdAt: new Date().toISOString()
    };

    const mockChat = {
      id: 'chat-1',
      gameId: '770e8400-e29b-41d4-a716-000000000001',
      agentId: 'agent-1',
      title: 'Chess Rules',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    };

    useSelectedGame.mockReturnValue(mockGame);
    useSelectedAgent.mockReturnValue(mockAgent);
    useActiveChat.mockReturnValue(mockChat);

    mockUseChatStore.mockReturnValue({
      ...defaultStoreState,
      selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
      selectedAgentId: 'agent-1',
      games: [mockGame],
      agents: [mockAgent],
      activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'chat-1' },
      chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [mockChat] },
      messagesByChat: {
        'chat-1': [
          {
            id: 'msg-1',
            role: 'user',
            message: 'How do I castle?',
            createdAt: '2025-01-10T10:00:00Z',
          },
        ],
      },
    });

    mockUseChatStore.mockReturnValue({
      selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
      selectedAgentId: 'agent-1',
      activeChatId: 'chat-1',
      messages: [
        {
          id: 'msg-1',
          role: 'user',
          message: 'How do I castle?',
          createdAt: '2025-01-10T10:00:00Z',
        },
      ],
      isStreaming: false,
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
      createChat: jest.fn(),
    });

    renderWithAuth(<ChatPage />);

    // Wait for chat page to be fully rendered
    await waitFor(() => {
      expect(screen.getByTestId('chat-content')).toBeInTheDocument();
    });

    // Verify agent name in header
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /Chess Expert/i })).toBeInTheDocument();
    });
  });
});
