/**
 * Game Chat Page Tests (Task #2)
 */

import { render, screen, waitFor } from '@testing-library/react';
import { useParams, useRouter } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useAuth } from '@/components/auth/AuthProvider';
import { api } from '@/lib/api';

import GameChatPage from '../page';

// Mock dependencies
vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
  useRouter: vi.fn(),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getById: vi.fn(),
    },
  },
}));

vi.mock('@/components/agent/AgentChat', () => ({
  AgentChat: ({ gameName, agentName }: { gameName?: string; agentName?: string }) => (
    <div data-testid="agent-chat">
      Chat with {agentName} about {gameName}
    </div>
  ),
}));

describe('GameChatPage', () => {
  const mockRouter = {
    push: vi.fn(),
  };

  const mockGameDetail = {
    id: 'game-123',
    title: 'Catan',
    description: 'A strategy game',
    imageUrl: '/catan.jpg',
    publishers: [{ name: 'Kosmos' }],
    yearPublished: 1995,
    averageRating: 8.5,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    (useRouter as ReturnType<typeof vi.fn>).mockReturnValue(mockRouter);
    (useParams as ReturnType<typeof vi.fn>).mockReturnValue({ id: 'game-123' });
  });

  it('redirects unauthenticated users to login', () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: null });

    render(<GameChatPage />);

    expect(mockRouter.push).toHaveBeenCalledWith('/login?redirect=/games/game-123/chat');
  });

  it('renders chat interface for authenticated users', async () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: { id: 'user-1' } });
    (api.sharedGames.getById as ReturnType<typeof vi.fn>).mockResolvedValue(mockGameDetail);

    render(<GameChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
    });

    expect(screen.getByText(/Chat with MeepleAI Assistant about Catan/i)).toBeInTheDocument();
  });

  it('shows error when game not found', async () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: { id: 'user-1' } });
    (api.sharedGames.getById as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Not found')
    );

    render(<GameChatPage />);

    await waitFor(() => {
      expect(screen.getByText(/Failed to load game/i)).toBeInTheDocument();
    });
  });

  it('shows loading skeleton initially', () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: { id: 'user-1' } });
    (api.sharedGames.getById as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(() => {}) // Never resolves
    );

    render(<GameChatPage />);

    // Should show skeleton while loading
    const skeletons = screen.getAllByTestId(/skeleton/i);
    expect(skeletons.length).toBeGreaterThan(0);
  });
});
