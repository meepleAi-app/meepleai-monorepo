/**
 * Chat Page - Authentication Tests
 *
 * Tests for authentication state handling in the chat interface.
 * This is a pilot split from the original chat.test.tsx to validate
 * the split approach and improve test performance.
 *
 * Related files:
 * - chat-test-utils.ts: Shared setup and utilities
 * - ../../../pages/chat.tsx: Component under test
 */

import { render, screen, waitFor } from '@testing-library/react';
import ChatPage from '../../../pages/chat';
import { useChatStreaming } from '../../../lib/hooks/useChatStreaming';
import {
  mockApi,
  mockStartStreaming,
  mockStopStreaming,
  mockOnComplete,
  mockOnError,
  setMockOnComplete,
  setMockOnError,
  resetAllMocks,
  setupAuthenticatedState
} from './shared/chat-test-utils';
import { createWrapper } from '../../utils/test-providers';

// Mock the useChatStreaming hook
jest.mock('../../../lib/hooks/useChatStreaming', () => ({
  useChatStreaming: jest.fn((callbacks?: { onComplete?: any; onError?: any }) => {
    // Capture callbacks for later use using setter functions
    const { setMockOnComplete: setComplete, setMockOnError: setError } = require('./shared/chat-test-utils');
    if (callbacks?.onComplete) {
      setComplete(callbacks.onComplete);
    }
    if (callbacks?.onError) {
      setError(callbacks.onError);
    }

    return [
      {
        isStreaming: false,
        currentAnswer: '',
        snippets: [],
        state: null,
        error: null
      },
      {
        startStreaming: mockStartStreaming,
        stopStreaming: mockStopStreaming
      }
    ];
  })
}));

// Mock the API client
jest.mock('../../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
    delete: jest.fn(),
    chat: {
      exportChat: jest.fn(),
      updateMessage: jest.fn(),
      deleteMessage: jest.fn()
    }
  }
}));

describe('ChatPage - Authentication', () => {
  beforeEach(() => {
    resetAllMocks();

    // Reset useChatStreaming mock to default state with callback capturing
    const mockStreamingState = {
      isStreaming: false,
      currentAnswer: '',
      snippets: [],
      state: null,
      error: null
    };

    (useChatStreaming as jest.Mock).mockImplementation((callbacks?: { onComplete?: any; onError?: any }) => {
      // Capture callbacks for later use using setter functions
      if (callbacks?.onComplete) {
        setMockOnComplete(callbacks.onComplete);
      }
      if (callbacks?.onError) {
        setMockOnError(callbacks.onError);
      }

      return [
        mockStreamingState,
        {
          startStreaming: mockStartStreaming,
          stopStreaming: mockStopStreaming
        }
      ];
    });
  });

  it('shows loading state initially', async () => {
    // Mock API to never resolve, keeping providers in loading state
    mockApi.get.mockImplementation(() => new Promise(() => {}));

    render(<ChatPage />, { wrapper: createWrapper() });

    // With the full provider tree, AuthProvider starts in loading state
    // ChatPage shows "Loading..." while auth check is pending
    await waitFor(() => {
      expect(screen.getByText(/Loading.../i)).toBeInTheDocument();
    });
  });

  it('shows unauthenticated state when user is not logged in', async () => {
    // Setup mocks for full provider tree
    mockApi.get.mockImplementation((path: string) => {
      if (path === '/api/v1/auth/me') {
        return Promise.resolve(null); // No user
      }
      if (path === '/api/v1/games') {
        return Promise.resolve([]); // Empty games
      }
      return Promise.resolve([]);
    });

    render(<ChatPage />, { wrapper: createWrapper() });

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me'));

    expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();
    expect(screen.getByText(/Devi effettuare l'accesso per utilizzare la chat/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Vai al Login/i })).toBeInTheDocument();
  });

  it('shows authenticated interface when user is logged in', async () => {
    const testData = {
      mockAuthResponse: {
        id: 'user-1',
        email: 'user@example.com',
        displayName: 'Test User',
        role: 'User',
        expiresAt: new Date(Date.now() + 3600000).toISOString()
      },
      mockGames: [{ id: 'game-1', name: 'Chess', createdAt: new Date().toISOString() }],
      mockAgents: [{ id: 'agent-1', gameId: 'game-1', name: 'Chess Expert', kind: 'qa', createdAt: new Date().toISOString() }],
      mockChats: []
    };

    // Setup comprehensive mocks for provider hierarchy
    mockApi.get.mockImplementation((path: string) => {
      if (path === '/api/v1/auth/me') {
        return Promise.resolve(testData.mockAuthResponse);
      }
      if (path === '/api/v1/games') {
        return Promise.resolve(testData.mockGames);
      }
      if (path === `/api/v1/games/${testData.mockGames[0].id}/agents`) {
        return Promise.resolve(testData.mockAgents);
      }
      if (path.startsWith('/api/v1/chats?gameId=')) {
        return Promise.resolve(testData.mockChats);
      }
      return Promise.resolve([]);
    });

    render(<ChatPage />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /MeepleAI Chat/i })).toBeInTheDocument();
    }, { timeout: 3000 });

    expect(screen.queryByText(/Accesso richiesto/i)).not.toBeInTheDocument();
  });

  it('handles authentication check failure gracefully', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

    // Setup mocks - auth fails, other providers get empty data
    mockApi.get.mockImplementation((path: string) => {
      if (path === '/api/v1/auth/me') {
        return Promise.reject(new Error('Auth failed'));
      }
      if (path === '/api/v1/games') {
        return Promise.resolve([]);
      }
      return Promise.resolve([]);
    });

    render(<ChatPage />, { wrapper: createWrapper() });

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me'));

    expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();

    consoleErrorSpy.mockRestore();
  });
});
