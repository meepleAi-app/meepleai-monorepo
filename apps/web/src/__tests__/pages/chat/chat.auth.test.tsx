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

// Mock Zustand store with minimal context
const mockUseChatStore = jest.fn();

jest.mock('@/store/chat/store', () => ({
  useChatStore: () => mockUseChatStore(),
}));

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

describe('ChatPage - Authentication', () => {
  beforeEach(() => {
    jest.clearAllMocks();

    // Default mock context
    mockUseChatStore.mockReturnValue({
      selectedGameId: null,
      selectedAgentId: null,
      activeChatId: null,
      messages: [],
      isStreaming: false,
    });
  });

  it('shows loading state initially', async () => {
    // Mock API to never resolve, keeping providers in loading state
    mockApi.get.mockImplementation(() => new Promise(() => {}));

    render(<ChatPage />);

    // With the full provider tree, AuthProvider starts in loading state
    // ChatPage shows "Loading..." while auth check is pending
    await waitFor(() => {
      expect(screen.getByText(/Loading.../i)).toBeInTheDocument();
    });
  });

  it('shows unauthenticated state when user is not logged in', async () => {
    // Mock API to return no user (unauthenticated)
    mockApi.get.mockResolvedValue(null);

    render(<ChatPage />);

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

    render(<ChatPage />);

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

    render(<ChatPage />);

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
