/**
 * Chat Page - Authentication Tests
 *
 * Tests for authentication state handling in the chat interface.
 * Migrated to Zustand with real AuthProvider integration.
 *
 * Related files:
 * - chat-test-utils.ts: Shared setup and utilities
 * - ../../../pages/chat.tsx: Component under test
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import ChatPage from '../../../components/pages/ChatPage';
import { api } from '../../../lib/api';
import { AuthProvider } from '../../../components/auth/AuthProvider';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Mock Zustand store - must mock before importing ChatPage
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

// Mock BottomNav to avoid loading state issues
jest.mock('../../../components/chat/BottomNav', () => ({
  BottomNav: () => null,
}));

// Mock ChatSidebar
jest.mock('../../../components/chat/ChatSidebar', () => ({
  ChatSidebar: () => <div data-testid="chat-sidebar">Sidebar</div>,
}));

// Mock ChatContent with auth-aware rendering
jest.mock('../../../components/chat/ChatContent', () => ({
  ChatContent: () => {
    const { useAuth } = require('../../../components/auth/AuthProvider');
    const { user, loading } = useAuth();

    if (loading) {
      return <div>Loading...</div>;
    }

    if (!user) {
      return (
        <div>
          <h1>Accesso richiesto</h1>
          <p>Devi effettuare l'accesso per utilizzare la chat</p>
          <a href="/login">Vai al Login</a>
        </div>
      );
    }

    return (
      <div data-testid="chat-content">
        <h1>MeepleAI Chat</h1>
      </div>
    );
  },
}));

// Mock ExportChatModal
jest.mock('../../../components/ExportChatModal', () => ({
  ExportChatModal: () => null,
}));

// Mock API
jest.mock('../../../lib/api');

const mockApi = api as jest.Mocked<typeof api>;

// Get the mocked function
const { useChatStore } = require('@/store/chat');
const mockUseChatStoreFn = useChatStore as jest.MockedFunction<typeof useChatStore>;

// Helper to render with QueryClientProvider and AuthProvider
function renderWithAuth(component: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
      },
    },
    logger: {
      log: () => {},
      warn: () => {},
      error: () => {},
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

describe('ChatPage - Authentication', () => {
  beforeEach(() => {
    jest.clearAllMocks();

    // Default mock context
    mockUseChatStoreFn.mockReturnValue({
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
  });

  it('shows loading state initially', async () => {
    // Mock API to never resolve, keeping providers in loading state
    mockApi.get.mockImplementation(() => new Promise(() => {}));

    renderWithAuth(<ChatPage />);

    // With the full provider tree, AuthProvider starts in loading state
    // ChatPage shows "Loading..." while auth check is pending
    await waitFor(() => {
      expect(screen.getByText(/Loading.../i)).toBeInTheDocument();
    });
  });

  it('shows unauthenticated state when user is not logged in', async () => {
    // Mock API to return no user (unauthenticated)
    mockApi.get.mockResolvedValue(null);

    renderWithAuth(<ChatPage />);

    // Wait for auth check to complete
    await waitFor(
      () => {
        expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();
      },
      { timeout: 3000 }
    );

    expect(screen.getByText(/Devi effettuare l'accesso per utilizzare la chat/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Vai al Login/i })).toBeInTheDocument();
  });

  it('shows authenticated interface when user is logged in', async () => {
    const userResponse = {
      user: {
        id: 'user-1',
        email: 'user@example.com',
        displayName: 'Test User',
        role: 'User',
      },
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    };

    // Mock API to return authenticated user
    mockApi.get.mockResolvedValue(userResponse);

    renderWithAuth(<ChatPage />);

    // Wait for auth check to complete and authenticated UI to render
    await waitFor(
      () => {
        expect(screen.getByRole('heading', { name: /MeepleAI Chat/i })).toBeInTheDocument();
      },
      { timeout: 3000 }
    );

    // Verify unauthenticated UI is not shown
    expect(screen.queryByText(/Accesso richiesto/i)).not.toBeInTheDocument();
  });

  it('handles authentication check failure gracefully', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

    // Mock API to reject auth check
    mockApi.get.mockRejectedValue(new Error('Auth check failed'));

    renderWithAuth(<ChatPage />);

    // Wait for auth failure to be handled (should show unauthenticated state)
    await waitFor(
      () => {
        expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();
      },
      { timeout: 3000 }
    );

    consoleErrorSpy.mockRestore();
  });
});
