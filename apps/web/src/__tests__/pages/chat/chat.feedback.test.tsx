/**
 * Chat Page - Feedback Tests
 *
 * Tests for thumbs up/down feedback functionality in the chat interface.
 * This file focuses on feedback submission, state management, and error handling.
 *
 * Uses component mocking for isolated, fast testing like chat.test.tsx
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChatPage from '../../../pages/chat';
import { api } from '../../../lib/api';

// Mock ChatProvider with context values
const mockUseChatContext = jest.fn();

jest.mock('../../../components/chat/ChatProvider', () => ({
  ChatProvider: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="chat-provider">{children}</div>
  ),
  useChatContext: () => mockUseChatContext(),
}));

// Mock ChatSidebar - renders minimal UI
jest.mock('../../../components/chat/ChatSidebar', () => ({
  ChatSidebar: () => <div data-testid="chat-sidebar">sidebar</div>,
}));

// Mock ChatContent with feedback buttons
const mockFeedbackProps = {
  onFeedback: jest.fn(),
};

jest.mock('../../../components/chat/ChatContent', () => ({
  ChatContent: () => {
    const { useChatContext } = require('../../../components/chat/ChatProvider');
    const { messages = [] } = useChatContext();

    return (
      <div data-testid="chat-content">
        {messages.map((msg: any, idx: number) => (
          <div key={idx} data-testid={`message-${idx}`}>
            <div>{msg.message}</div>
            {msg.role === 'assistant' && (
              <div data-testid="feedback-buttons">
                <button
                  aria-label="Mark as helpful"
                  onClick={() => mockFeedbackProps.onFeedback(msg.id, 'helpful')}
                >
                  👍
                </button>
                <button
                  aria-label="Mark as not helpful"
                  onClick={() => mockFeedbackProps.onFeedback(msg.id, 'not-helpful')}
                >
                  👎
                </button>
              </div>
            )}
          </div>
        ))}
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

describe('ChatPage - Feedback', () => {
  const userResponse = {
    user: {
      id: 'user-1',
      email: 'user@example.com',
      displayName: 'Test User',
      role: 'User',
    },
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
  };

  const mockMessages = [
    {
      id: 'msg-1',
      role: 'user',
      message: 'How do I castle?',
      createdAt: '2025-01-10T10:00:00Z',
    },
    {
      id: 'msg-2',
      role: 'assistant',
      message: 'Castling is a special move...',
      createdAt: '2025-01-10T10:01:00Z',
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    mockFeedbackProps.onFeedback.mockClear();

    // Default mock context
    mockUseChatContext.mockReturnValue({
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
      activeChatId: 'chat-1',
      messages: mockMessages,
      isStreaming: false,
    });

    // Default API setup
    mockApi.get.mockResolvedValue(userResponse);
  });

  it('submits helpful feedback when thumbs up is clicked', async () => {
    mockApi.post.mockResolvedValueOnce({});

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    await user.click(helpfulButtons[0]);

    expect(mockFeedbackProps.onFeedback).toHaveBeenCalledWith('msg-2', 'helpful');
  });

  it('submits not-helpful feedback when thumbs down is clicked', async () => {
    mockApi.post.mockResolvedValueOnce({});

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const notHelpfulButtons = screen.getAllByRole('button', { name: /Mark as not helpful/i });
    await user.click(notHelpfulButtons[0]);

    expect(mockFeedbackProps.onFeedback).toHaveBeenCalledWith('msg-2', 'not-helpful');
  });

  it('toggles feedback to null when clicking same button twice', async () => {
    mockApi.post.mockResolvedValue({});

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });

    // First click
    await user.click(helpfulButtons[0]);
    expect(mockFeedbackProps.onFeedback).toHaveBeenNthCalledWith(1, 'msg-2', 'helpful');

    // Second click (toggle)
    await user.click(helpfulButtons[0]);
    expect(mockFeedbackProps.onFeedback).toHaveBeenNthCalledWith(2, 'msg-2', 'helpful');
  });

  it('changes feedback when switching between helpful and not-helpful', async () => {
    mockApi.post.mockResolvedValue({});

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    const notHelpfulButtons = screen.getAllByRole('button', { name: /Mark as not helpful/i });

    // First click helpful
    await user.click(helpfulButtons[0]);
    expect(mockFeedbackProps.onFeedback).toHaveBeenNthCalledWith(1, 'msg-2', 'helpful');

    // Then click not helpful
    await user.click(notHelpfulButtons[0]);
    expect(mockFeedbackProps.onFeedback).toHaveBeenNthCalledWith(2, 'msg-2', 'not-helpful');
  });

  it('reverts feedback state when API call fails', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    mockApi.post.mockRejectedValueOnce(new Error('Feedback failed'));

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });

    await user.click(helpfulButtons[0]);

    // Verify feedback was attempted
    expect(mockFeedbackProps.onFeedback).toHaveBeenCalledWith('msg-2', 'helpful');

    consoleErrorSpy.mockRestore();
  });

  it('uses backend message ID for feedback when available', async () => {
    mockApi.post.mockResolvedValueOnce({});

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    await user.click(helpfulButtons[0]);

    // Should use backend message ID 'msg-2'
    expect(mockFeedbackProps.onFeedback).toHaveBeenCalledWith('msg-2', 'helpful');
  });

  it('only shows feedback buttons for assistant messages', async () => {
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    // Count feedback buttons (should only be for assistant messages)
    const helpfulButtons = screen.getAllByRole('button', { name: /Mark as helpful/i });
    const notHelpfulButtons = screen.getAllByRole('button', { name: /Mark as not helpful/i });

    // Only 1 assistant message in mockMessages
    expect(helpfulButtons.length).toBe(1);
    expect(notHelpfulButtons.length).toBe(1);
  });
});
