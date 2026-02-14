/**
 * ChatHistoryDrawer Tests - Issue #4366
 *
 * Tests for the thread history drawer:
 * 1. Renders when open
 * 2. Groups threads by game
 * 3. Highlights active thread
 * 4. Search filters threads
 * 5. Thread click navigation
 * 6. Delete thread
 * 7. Empty state
 * 8. Loading state
 * 9. New conversation button
 *
 * Pattern: Vitest + React Testing Library
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import React from 'react';

import { ChatHistoryDrawer } from '../ChatHistoryDrawer';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, onClick, ...props }: any) => (
    <a href={href} onClick={onClick} {...props}>{children}</a>
  ),
}));

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn(),
      deleteThread: vi.fn(),
    },
    games: {
      getAll: vi.fn(),
    },
  },
}));

// Mock timeUtils
vi.mock('@/lib/utils/timeUtils', () => ({
  formatRelativeTime: vi.fn(() => '2 ore fa'),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGames = {
  games: [
    { id: 'game-1', title: 'Catan' },
    { id: 'game-2', title: 'Ticket to Ride' },
  ],
};

const catanThreads = [
  {
    id: 'thread-1',
    title: 'Rules clarification',
    gameId: 'game-1',
    messageCount: 5,
    lastMessageAt: '2024-01-01T10:00:00Z',
    createdAt: '2024-01-01T08:00:00Z',
  },
  {
    id: 'thread-2',
    title: 'Setup help',
    gameId: 'game-1',
    messageCount: 3,
    lastMessageAt: '2024-01-01T09:00:00Z',
    createdAt: '2024-01-01T07:00:00Z',
  },
];

const ticketThreads = [
  {
    id: 'thread-3',
    title: 'Strategy questions',
    gameId: 'game-2',
    messageCount: 12,
    lastMessageAt: '2024-01-02T15:00:00Z',
    createdAt: '2024-01-02T14:00:00Z',
  },
];

// ============================================================================
// Helpers
// ============================================================================

let apiMock: any;

async function setupMocks() {
  const { api } = await import('@/lib/api');
  apiMock = api;

  (apiMock.games.getAll as Mock).mockResolvedValue(mockGames);
  (apiMock.chat.getThreadsByGame as Mock).mockImplementation(async (gameId: string) => {
    if (gameId === 'game-1') return catanThreads;
    if (gameId === 'game-2') return ticketThreads;
    return [];
  });
  (apiMock.chat.deleteThread as Mock).mockResolvedValue(undefined);
}

// ============================================================================
// Tests
// ============================================================================

describe('ChatHistoryDrawer', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await setupMocks();
  });

  // --------------------------------------------------------------------------
  // Rendering
  // --------------------------------------------------------------------------

  it('renders drawer when open', async () => {
    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('chat-history-drawer')).toBeInTheDocument();
      expect(screen.getByText('Storia Conversazioni')).toBeInTheDocument();
    });
  });

  it('shows loading state while fetching', () => {
    // Make API hang
    (apiMock.games.getAll as Mock).mockReturnValue(new Promise(() => {}));

    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    expect(screen.getByTestId('history-loading')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Thread Groups
  // --------------------------------------------------------------------------

  it('groups threads by game', async () => {
    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('game-group-game-1')).toBeInTheDocument();
      expect(screen.getByTestId('game-group-game-2')).toBeInTheDocument();
    });

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('renders thread items with metadata', async () => {
    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Rules clarification')).toBeInTheDocument();
      expect(screen.getByText('Setup help')).toBeInTheDocument();
      expect(screen.getByText('Strategy questions')).toBeInTheDocument();
    });
  });

  it('shows total thread count', async () => {
    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('3 thread')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Active Thread
  // --------------------------------------------------------------------------

  it('highlights active thread', async () => {
    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
        activeThreadId="thread-1"
      />
    );

    await waitFor(() => {
      const activeItem = screen.getByTestId('thread-item-thread-1');
      expect(activeItem).toHaveAttribute('aria-current', 'true');
      expect(activeItem).toHaveClass('bg-amber-50');
    });
  });

  // --------------------------------------------------------------------------
  // Search
  // --------------------------------------------------------------------------

  it('filters threads by search query', async () => {
    const user = userEvent.setup();

    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('history-search-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('history-search-input'), 'Rules');

    await waitFor(() => {
      expect(screen.getByText('Rules clarification')).toBeInTheDocument();
      expect(screen.queryByText('Setup help')).not.toBeInTheDocument();
      expect(screen.queryByText('Strategy questions')).not.toBeInTheDocument();
    });
  });

  it('shows empty state when search has no results', async () => {
    const user = userEvent.setup();

    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('history-search-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('history-search-input'), 'nonexistent');

    await waitFor(() => {
      expect(screen.getByTestId('history-empty')).toBeInTheDocument();
      expect(screen.getByText('Nessun risultato per la ricerca')).toBeInTheDocument();
    });
  });

  it('clears search with clear button', async () => {
    const user = userEvent.setup();

    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('history-search-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('history-search-input'), 'Rules');

    await waitFor(() => {
      expect(screen.getByTestId('history-search-clear')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('history-search-clear'));

    await waitFor(() => {
      // All threads visible again
      expect(screen.getByText('Rules clarification')).toBeInTheDocument();
      expect(screen.getByText('Setup help')).toBeInTheDocument();
      expect(screen.getByText('Strategy questions')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Navigation
  // --------------------------------------------------------------------------

  it('calls onThreadSelect and closes on thread click', async () => {
    const user = userEvent.setup();
    const onThreadSelect = vi.fn();
    const onClose = vi.fn();

    render(
      <ChatHistoryDrawer
        open={true}
        onClose={onClose}
        onThreadSelect={onThreadSelect}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('thread-item-thread-1')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('thread-item-thread-1'));

    expect(onThreadSelect).toHaveBeenCalledWith('thread-1');
    expect(onClose).toHaveBeenCalled();
  });

  it('renders new conversation button', async () => {
    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      const btn = screen.getByTestId('history-new-chat-btn');
      expect(btn).toBeInTheDocument();
      expect(btn).toHaveAttribute('href', '/chat/new');
    });
  });

  // --------------------------------------------------------------------------
  // Delete Thread
  // --------------------------------------------------------------------------

  it('deletes a thread', async () => {
    const user = userEvent.setup();

    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('thread-delete-thread-2')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('thread-delete-thread-2'));

    await waitFor(() => {
      expect(apiMock.chat.deleteThread).toHaveBeenCalledWith('thread-2');
      expect(screen.queryByText('Setup help')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Empty State
  // --------------------------------------------------------------------------

  it('shows empty state when no threads', async () => {
    (apiMock.chat.getThreadsByGame as Mock).mockResolvedValue([]);

    render(
      <ChatHistoryDrawer
        open={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('history-empty')).toBeInTheDocument();
      expect(screen.getByText('Nessuna conversazione trovata')).toBeInTheDocument();
    });
  });
});
