/**
 * ChatThreadView Tests - Issue #4364
 *
 * Tests for the split view chat page:
 * 1. Loads and renders thread data
 * 2. Renders messages
 * 3. Sends messages via API
 * 4. Shows empty state for new threads
 * 5. Shows loading state
 * 6. Shows error when thread not found
 * 7. Renders ChatInfoPanel with citations
 * 8. Inline title editing
 * 9. Delete thread
 *
 * Pattern: Vitest + React Testing Library
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import React from 'react';

import { ChatThreadView } from '../ChatThreadView';

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadById: vi.fn(),
      addMessage: vi.fn(),
      updateThreadTitle: vi.fn(),
      deleteThread: vi.fn(),
    },
    games: {
      getAll: vi.fn(),
    },
  },
}));

// Mock ChatInfoPanel
vi.mock('@/components/chat/ChatInfoPanel', () => ({
  ChatInfoPanel: ({ citations, suggestedQuestions }: any) => (
    <div data-testid="chat-info-panel">
      <span data-testid="citation-count">{citations?.length ?? 0}</span>
      <span data-testid="question-count">{suggestedQuestions?.length ?? 0}</span>
    </div>
  ),
}));

// Mock store
vi.mock('@/store/chat-info/store', () => ({
  useChatInfoStore: () => ({
    isCollapsed: false,
    isMobileOpen: false,
    toggleCollapsed: vi.fn(),
    setMobileOpen: vi.fn(),
    setCollapsed: vi.fn(),
    toggleMobileOpen: vi.fn(),
  }),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockThread = {
  id: 'thread-1',
  title: 'Test Chat',
  gameId: 'game-1',
  agentId: 'agent-1',
  status: 'Active',
  messages: [
    { id: 'msg-1', role: 'user', content: 'Hello!', timestamp: '2024-01-01T00:00:00Z' },
    {
      id: 'msg-2',
      role: 'assistant',
      content: 'Hi there!',
      timestamp: '2024-01-01T00:01:00Z',
      citations: [{ documentId: 'doc-1', pageNumber: 5, snippet: 'Rule text', relevanceScore: 0.9 }],
      followUpQuestions: ['What else?', 'How about this?'],
    },
  ],
};

const mockGames = {
  games: [{ id: 'game-1', title: 'Catan' }],
};

// ============================================================================
// Helpers
// ============================================================================

let apiMock: any;

async function renderView(threadId = 'thread-1') {
  const result = render(<ChatThreadView threadId={threadId} />);
  return result;
}

// ============================================================================
// Tests
// ============================================================================

describe('ChatThreadView', () => {
  beforeEach(async () => {
    vi.clearAllMocks();

    // Ensure matchMedia is available
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: query === '(min-width: 1024px)',
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });

    const { api } = await import('@/lib/api');
    apiMock = api;

    (apiMock.chat.getThreadById as Mock).mockResolvedValue(mockThread);
    (apiMock.games.getAll as Mock).mockResolvedValue(mockGames);
    (apiMock.chat.addMessage as Mock).mockResolvedValue(mockThread);
    (apiMock.chat.updateThreadTitle as Mock).mockResolvedValue({});
    (apiMock.chat.deleteThread as Mock).mockResolvedValue(undefined);
  });

  // --------------------------------------------------------------------------
  // Loading & Rendering
  // --------------------------------------------------------------------------

  it('shows loading state initially', () => {
    renderView();
    expect(screen.getByTestId('chat-loading')).toBeInTheDocument();
  });

  it('loads and renders thread', async () => {
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('chat-thread-view')).toBeInTheDocument();
    });

    expect(apiMock.chat.getThreadById).toHaveBeenCalledWith('thread-1');
  });

  it('renders messages', async () => {
    await renderView();

    await waitFor(() => {
      expect(screen.getByText('Hello!')).toBeInTheDocument();
      expect(screen.getByText('Hi there!')).toBeInTheDocument();
    });
  });

  it('renders user and assistant message styles', async () => {
    await renderView();

    await waitFor(() => {
      const userMsg = screen.getByTestId('message-user');
      const assistantMsg = screen.getByTestId('message-assistant');
      expect(userMsg).toHaveClass('ml-auto');
      expect(assistantMsg).toHaveClass('mr-auto');
    });
  });

  // --------------------------------------------------------------------------
  // Empty & Error States
  // --------------------------------------------------------------------------

  it('shows empty state for thread with no messages', async () => {
    (apiMock.chat.getThreadById as Mock).mockResolvedValue({
      ...mockThread,
      messages: [],
    });

    await renderView();

    await waitFor(() => {
      expect(screen.getByText('Inizia la conversazione')).toBeInTheDocument();
    });
  });

  it('shows error when thread not found', async () => {
    (apiMock.chat.getThreadById as Mock).mockResolvedValue(null);

    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('chat-error')).toBeInTheDocument();
      expect(screen.getByText('Thread non trovato')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Message Sending
  // --------------------------------------------------------------------------

  it('sends message on button click', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });

    const input = screen.getByTestId('message-input');
    await user.type(input, 'New message');
    await user.click(screen.getByTestId('send-btn'));

    await waitFor(() => {
      expect(apiMock.chat.addMessage).toHaveBeenCalledWith('thread-1', {
        content: 'New message',
        role: 'user',
      });
    });
  });

  it('disables send button when input empty', async () => {
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('send-btn')).toBeDisabled();
    });
  });

  // --------------------------------------------------------------------------
  // ChatInfoPanel Integration
  // --------------------------------------------------------------------------

  it('passes citations to ChatInfoPanel', async () => {
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('chat-info-panel')).toBeInTheDocument();
      expect(screen.getByTestId('citation-count')).toHaveTextContent('1');
    });
  });

  it('passes suggested questions to ChatInfoPanel', async () => {
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('question-count')).toHaveTextContent('2');
    });
  });

  // --------------------------------------------------------------------------
  // Header Actions
  // --------------------------------------------------------------------------

  it('renders header with title', async () => {
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('chat-thread-header')).toBeInTheDocument();
      expect(screen.getByText('Test Chat')).toBeInTheDocument();
    });
  });

  it('deletes thread and redirects', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('header-delete-btn')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('header-delete-btn'));

    await waitFor(() => {
      expect(apiMock.chat.deleteThread).toHaveBeenCalledWith('thread-1');
      expect(mockPush).toHaveBeenCalledWith('/chat/new');
    });
  });
});
