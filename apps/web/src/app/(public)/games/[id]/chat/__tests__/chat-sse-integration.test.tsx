/**
 * Chat SSE Integration Tests (Task #6)
 *
 * Tests the full SSE streaming flow:
 * - User sends message
 * - Backend streams response via SSE
 * - Frontend updates UI in real-time
 * - Message persisted with metadata
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { api } from '@/lib/api';

import GameChatPage from '../page';

// Mock SSE stream
function createMockSSEStream() {
  const events = [
    { type: 'Token', data: { text: 'Hello ' } },
    { type: 'Token', data: { text: 'from ' } },
    { type: 'Token', data: { text: 'agent!' } },
    {
      type: 'Complete',
      data: {
        messageId: 'msg-123',
        tokenCount: 150,
        confidence: 0.92,
        citations: [
          {
            documentId: 'doc-1',
            pageNumber: 5,
            text: 'Rule excerpt',
            confidence: 0.88,
          },
        ],
      },
    },
  ];

  async function* generator() {
    for (const event of events) {
      yield event;
    }
  }

  return generator();
}

// Mock dependencies
vi.mock('next/navigation', () => ({
  useParams: () => ({ id: 'game-123' }),
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({ user: { id: 'user-1', username: 'testuser' } }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getById: vi.fn(),
    },
    agents: {
      chat: vi.fn(),
    },
  },
}));

describe('Chat SSE Integration', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    // Mock game data
    (api.sharedGames.getById as ReturnType<typeof vi.fn>).mockResolvedValue({
      id: 'game-123',
      title: 'Catan',
      description: 'A strategy game',
      imageUrl: '/catan.jpg',
    });
  });

  it('streams agent response token by token', async () => {
    (api.agents.chat as ReturnType<typeof vi.fn>).mockReturnValue(createMockSSEStream());

    render(<GameChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
    });

    // Type and send message
    const input = screen.getByPlaceholderText(/Chiedi a/i);
    await userEvent.type(input, 'Test question');

    const sendButton = screen.getByRole('button', { name: /send/i });
    await userEvent.click(sendButton);

    // Wait for streaming to complete
    await waitFor(
      () => {
        expect(screen.getByText(/Hello from agent!/i)).toBeInTheDocument();
      },
      { timeout: 3000 }
    );
  });

  it('displays citations after response completes', async () => {
    (api.agents.chat as ReturnType<typeof vi.fn>).mockReturnValue(createMockSSEStream());

    render(<GameChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/Chiedi a/i);
    await userEvent.type(input, 'Test question');

    const sendButton = screen.getByRole('button', { name: /send/i });
    await userEvent.click(sendButton);

    // Wait for citations
    await waitFor(() => {
      expect(screen.getByText(/Sources:/i)).toBeInTheDocument();
      expect(screen.getByText('Page 5')).toBeInTheDocument();
    });
  });

  it('handles SSE errors gracefully', async () => {
    const errorStream = async function* () {
      yield { type: 'Token', data: { text: 'Start...' } };
      yield { type: 'Error', data: { message: 'Connection lost' } };
    };

    (api.agents.chat as ReturnType<typeof vi.fn>).mockReturnValue(errorStream());

    render(<GameChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/Chiedi a/i);
    await userEvent.type(input, 'Test question');

    const sendButton = screen.getByRole('button', { name: /send/i });
    await userEvent.click(sendButton);

    // Should show error message
    await waitFor(() => {
      expect(screen.getByText(/failed/i)).toBeInTheDocument();
    });
  });

  it('shows typing indicator during streaming', async () => {
    (api.agents.chat as ReturnType<typeof vi.fn>).mockReturnValue(createMockSSEStream());

    render(<GameChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/Chiedi a/i);
    await userEvent.type(input, 'Test question');

    const sendButton = screen.getByRole('button', { name: /send/i });
    await userEvent.click(sendButton);

    // Should show typing indicator during stream
    await waitFor(() => {
      expect(screen.getByTestId(/typing-indicator/i)).toBeInTheDocument();
    });
  });
});
