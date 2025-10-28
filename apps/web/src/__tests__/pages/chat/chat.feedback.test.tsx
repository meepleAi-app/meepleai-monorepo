/**
 * Chat Page - Feedback Tests
 *
 * Tests for thumbs up/down feedback functionality in the chat interface.
 * This file focuses on feedback submission, state management, and error handling.
 *
 * Related files:
 * - chat-test-utils.ts: Shared setup and utilities
 * - ../../../pages/chat.tsx: Component under test
 */

import { render, screen, waitFor } from '@testing-library/react';
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

describe('ChatPage - Feedback', () => {
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

  it('submits helpful feedback when thumbs up is clicked', async () => {
    const testData = setupAuthenticatedState();
    mockApi.get.mockResolvedValueOnce(testData.mockChatWithHistory);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();

    // Load a chat
    const chatItems = screen.getAllByText('Chess Expert');
    await user.click(chatItems[chatItems.length - 1]);

    await waitFor(() => {
      expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
    });

    mockApi.post.mockResolvedValueOnce({});

    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    await user.click(helpfulButtons[0]);

    await waitFor(() => {
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', {
        messageId: 'msg-2',
        endpoint: 'qa',
        outcome: 'helpful',
        userId: 'user-1',
        gameId: 'game-1'
      });
    });
  });

  it('submits not-helpful feedback when thumbs down is clicked', async () => {
    const testData = setupAuthenticatedState();
    mockApi.get.mockResolvedValueOnce(testData.mockChatWithHistory);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();

    // Load a chat
    const chatItems = screen.getAllByText('Chess Expert');
    await user.click(chatItems[chatItems.length - 1]);

    await waitFor(() => {
      expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
    });

    mockApi.post.mockResolvedValueOnce({});

    const notHelpfulButtons = screen.getAllByRole('button', { name: /Mark as not helpful/i });
    await user.click(notHelpfulButtons[0]);

    await waitFor(() => {
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', {
        messageId: 'msg-2',
        endpoint: 'qa',
        outcome: 'not-helpful',
        userId: 'user-1',
        gameId: 'game-1'
      });
    });
  });

  it('toggles feedback to null when clicking same button twice', async () => {
    const testData = setupAuthenticatedState();
    mockApi.get.mockResolvedValueOnce(testData.mockChatWithHistory);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();

    // Load a chat
    const chatItems = screen.getAllByText('Chess Expert');
    await user.click(chatItems[chatItems.length - 1]);

    await waitFor(() => {
      expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
    });

    mockApi.post.mockResolvedValueOnce({});
    mockApi.post.mockResolvedValueOnce({});

    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });

    // First click
    await user.click(helpfulButtons[0]);

    await waitFor(() => {
      expect(mockApi.post).toHaveBeenNthCalledWith(1, '/api/v1/agents/feedback', {
        messageId: 'msg-2',
        endpoint: 'qa',
        outcome: 'helpful',
        userId: 'user-1',
        gameId: 'game-1'
      });
    });

    // Second click
    await user.click(helpfulButtons[0]);

    await waitFor(() => {
      expect(mockApi.post).toHaveBeenNthCalledWith(2, '/api/v1/agents/feedback', {
        messageId: 'msg-2',
        endpoint: 'qa',
        outcome: null,
        userId: 'user-1',
        gameId: 'game-1'
      });
    });
  });

  it('changes feedback when switching between helpful and not-helpful', async () => {
    const testData = setupAuthenticatedState();
    mockApi.get.mockResolvedValueOnce(testData.mockChatWithHistory);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();

    // Load a chat
    const chatItems = screen.getAllByText('Chess Expert');
    await user.click(chatItems[chatItems.length - 1]);

    await waitFor(() => {
      expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
    });

    mockApi.post.mockResolvedValueOnce({});
    mockApi.post.mockResolvedValueOnce({});

    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    const notHelpfulButtons = screen.getAllByRole('button', { name: /Mark as not helpful/i });

    // First click helpful
    await user.click(helpfulButtons[0]);

    await waitFor(() => {
      expect(mockApi.post).toHaveBeenNthCalledWith(1, '/api/v1/agents/feedback', expect.objectContaining({
        outcome: 'helpful'
      }));
    });

    // Then click not helpful
    await user.click(notHelpfulButtons[0]);

    await waitFor(() => {
      expect(mockApi.post).toHaveBeenNthCalledWith(2, '/api/v1/agents/feedback', expect.objectContaining({
        outcome: 'not-helpful'
      }));
    });
  });

  it('reverts feedback state when API call fails', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    const testData = setupAuthenticatedState();
    mockApi.get.mockResolvedValueOnce(testData.mockChatWithHistory);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();

    // Load a chat
    const chatItems = screen.getAllByText('Chess Expert');
    await user.click(chatItems[chatItems.length - 1]);

    await waitFor(() => {
      expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
    });

    mockApi.post.mockRejectedValueOnce(new Error('Feedback failed'));

    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    const originalStyle = helpfulButtons[0].style.background;

    await user.click(helpfulButtons[0]);

    // Should revert after error
    await waitFor(() => {
      expect(helpfulButtons[0]).toHaveStyle(`background: ${originalStyle || '#f1f3f4'}`);
    });

    consoleErrorSpy.mockRestore();
  });

  it('uses backend message ID for feedback when available', async () => {
    const testData = setupAuthenticatedState();
    mockApi.get.mockResolvedValueOnce(testData.mockChatWithHistory);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1'));

    const user = userEvent.setup();

    // Load a chat
    const chatItems = screen.getAllByText('Chess Expert');
    await user.click(chatItems[chatItems.length - 1]);

    await waitFor(() => {
      expect(screen.getByText('Castling is a special move...')).toBeInTheDocument();
    });

    mockApi.post.mockResolvedValueOnce({});

    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    await user.click(helpfulButtons[0]);

    // Should use backend message ID 'msg-2' from mockChatWithHistory
    await waitFor(() => {
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', expect.objectContaining({
        messageId: 'msg-2'
      }));
    });
  });

  it('only shows feedback buttons for assistant messages', async () => {
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

    // Count feedback buttons (should only be for assistant messages)
    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    const notHelpfulButtons = screen.getAllByRole('button', { name: /Mark as not helpful/i });

    // Only 1 assistant message in history
    expect(helpfulButtons.length).toBe(1);
    expect(notHelpfulButtons.length).toBe(1);
  });
});
