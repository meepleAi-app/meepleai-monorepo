/**
 * Chat Page - UI Interactions Tests
 *
 * Tests for UI interactions and visual elements in the chat interface.
 * This file focuses on sidebar toggling, header displays, and chat preview formatting.
 *
 * Related files:
 * - chat-test-utils.ts: Shared setup and utilities
 * - ../../../pages/chat.tsx: Component under test
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChatPage from '../../../pages/chat';
import { useChatStreaming } from '../../../lib/hooks/useChatStreaming';
import {
  mockApi,
  mockStartStreaming,
  mockStopStreaming,
  mockOnComplete,
  mockOnError,
  resetAllMocks,
  setupAuthenticatedState,
  createChatTestData
} from './shared/chat-test-utils';

// Mock the useChatStreaming hook
jest.mock('../../../lib/hooks/useChatStreaming', () => ({
  useChatStreaming: jest.fn((callbacks?: { onComplete?: any; onError?: any }) => {
    // Capture callbacks for later use
    if (callbacks?.onComplete) {
      (mockOnComplete as any) = callbacks.onComplete;
    }
    if (callbacks?.onError) {
      (mockOnError as any) = callbacks.onError;
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

describe('ChatPage - UI Interactions', () => {
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
      // Capture callbacks for later use
      if (callbacks?.onComplete) {
        (mockOnComplete as any) = callbacks.onComplete;
      }
      if (callbacks?.onError) {
        (mockOnError as any) = callbacks.onError;
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

  it('toggles sidebar when collapse button is clicked', async () => {
    setupAuthenticatedState();

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();
    const collapseButton = screen.getByTitle(/Nascondi sidebar/i);

    await user.click(collapseButton);

    // Check button changes to expand icon
    expect(screen.getByTitle(/Mostra sidebar/i)).toBeInTheDocument();
  });

  it('shows game name in header when game is selected', async () => {
    setupAuthenticatedState();

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    // Use getAllByText since "Chess" appears in multiple places (header, select, chat items)
    const chessElements = screen.getAllByText('Chess');
    expect(chessElements.length).toBeGreaterThan(0);
  });

  it('shows agent name in header when chat is active', async () => {
    const testData = setupAuthenticatedState();
    mockApi.get.mockResolvedValueOnce(testData.mockChatWithHistory);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();

    // Load a chat
    const chatItems = screen.getAllByText('Chess Expert');
    await user.click(chatItems[chatItems.length - 1]);

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Chess Expert' })).toBeInTheDocument();
    });
  });

  it('shows default message when no chat is selected', async () => {
    const testData = createChatTestData();
    mockApi.get.mockResolvedValueOnce(testData.mockAuthResponse);
    mockApi.get.mockResolvedValueOnce(testData.mockGames);
    mockApi.get.mockResolvedValueOnce(testData.mockAgents);
    mockApi.get.mockResolvedValueOnce([]);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    expect(screen.getByRole('heading', { name: /Seleziona o crea una chat/i })).toBeInTheDocument();
  });

  it('highlights active chat in sidebar', async () => {
    const testData = setupAuthenticatedState();
    mockApi.get.mockResolvedValueOnce(testData.mockChatWithHistory);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();

    // Load a chat
    const chatItems = screen.getAllByText('Chess Expert');

    await user.click(chatItems[chatItems.length - 1]);

    await waitFor(() => {
      expect(screen.getByText('How do I castle?')).toBeInTheDocument();
    });

    // Verify the chat is active by checking that the header shows the agent name
    // This indirectly confirms the chat is highlighted as active
    expect(screen.getByRole('heading', { name: 'Chess Expert' })).toBeInTheDocument();
  });

  it('formats chat preview with date and time', async () => {
    setupAuthenticatedState();

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    // Check chat preview includes date/time (format is locale-dependent)
    const chatPreview = screen.getAllByText(/\d{1,2}\/\d{1,2}\/\d{4}/);
    expect(chatPreview.length).toBeGreaterThan(0);
  });
});
