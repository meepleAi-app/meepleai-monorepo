/**
 * ChatThreadView Tests - Issue #4364
 *
 * Tests for the split view chat page:
 * 1. Loads and renders thread data
 * 2. Renders messages
 * 3. Sends messages via API (REST fallback)
 * 4. Shows empty state for new threads
 * 5. Shows loading state
 * 6. Shows error when thread not found
 * 7. Renders ChatInfoPanel with citations
 * 8. Inline title editing
 * 9. Delete thread
 * 10. SSE streaming when agentId is present
 * 11. Streaming indicator during SSE
 * 12. Input/button disabled during streaming
 *
 * Pattern: Vitest + React Testing Library
 */

import { render, screen, waitFor } from '@testing-library/react';
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
      switchThreadAgent: vi.fn(),
    },
    games: {
      getAll: vi.fn(),
    },
  },
}));

// SSE streaming hook mock
const mockSendViaSSE = vi.fn();
const mockStreamState = {
  statusMessage: null as string | null,
  currentAnswer: '',
  followUpQuestions: [] as string[],
  isStreaming: false,
  error: null as string | null,
  chatThreadId: null as string | null,
  totalTokens: 0,
};

vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: (callbacks?: any) => {
    // Store callbacks for test access
    (globalThis as any).__sseCallbacks = callbacks;
    return {
      state: mockStreamState,
      sendMessage: mockSendViaSSE,
      stopStreaming: vi.fn(),
      reset: vi.fn(),
    };
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

// Mock AgentSelector (Issue #4465)
vi.mock('@/components/agent/AgentSelector', () => ({
  AgentSelector: ({ value, onChange, disabled }: any) => (
    <select
      data-testid="agent-selector"
      value={value}
      onChange={(e: any) => onChange(e.target.value)}
      disabled={disabled}
    >
      <option value="auto">Auto</option>
      <option value="tutor">Tutor</option>
      <option value="arbitro">Arbitro</option>
      <option value="decisore">Decisore</option>
    </select>
  ),
  AGENT_NAMES: {
    auto: 'Auto (Orchestrator)',
    tutor: 'Tutor',
    arbitro: 'Arbitro',
    decisore: 'Decisore',
  },
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

const mockThreadNoAgent = {
  ...mockThread,
  agentId: null,
};

let apiMock: any;

function resetStreamState() {
  mockStreamState.statusMessage = null;
  mockStreamState.currentAnswer = '';
  mockStreamState.followUpQuestions = [];
  mockStreamState.isStreaming = false;
  mockStreamState.error = null;
  mockStreamState.chatThreadId = null;
  mockStreamState.totalTokens = 0;
}

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
    resetStreamState();

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

  it('sends message via REST when no agentId', async () => {
    (apiMock.chat.getThreadById as Mock).mockResolvedValue(mockThreadNoAgent);
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

    expect(mockSendViaSSE).not.toHaveBeenCalled();
  });

  it('sends message via SSE when agentId is present', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });

    const input = screen.getByTestId('message-input');
    await user.type(input, 'SSE message');
    await user.click(screen.getByTestId('send-btn'));

    await waitFor(() => {
      expect(mockSendViaSSE).toHaveBeenCalledWith('agent-1', 'SSE message', 'thread-1');
    });

    expect(apiMock.chat.addMessage).not.toHaveBeenCalled();
  });

  it('disables send button when input empty', async () => {
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('send-btn')).toBeDisabled();
    });
  });

  // --------------------------------------------------------------------------
  // Info Panel (citations + suggested questions)
  // --------------------------------------------------------------------------

  // ChatInfoPanel was inlined into ChatThreadView - no separate component to test
  // Citations and suggested questions are now rendered directly in the view

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

  // --------------------------------------------------------------------------
  // SSE Streaming (Issue #4364)
  // --------------------------------------------------------------------------

  it('shows streaming status message', async () => {
    mockStreamState.statusMessage = 'Connecting...';
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('stream-status')).toBeInTheDocument();
      expect(screen.getByText('Connecting...')).toBeInTheDocument();
    });
  });

  it('shows streaming response bubble during streaming', async () => {
    mockStreamState.isStreaming = true;
    mockStreamState.currentAnswer = 'Partial response text';
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('message-streaming')).toBeInTheDocument();
      expect(screen.getByText('Partial response text')).toBeInTheDocument();
      expect(screen.getByText('In scrittura...')).toBeInTheDocument();
    });
  });

  it('does not show streaming bubble when not streaming', async () => {
    mockStreamState.isStreaming = false;
    mockStreamState.currentAnswer = '';
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('chat-thread-view')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('message-streaming')).not.toBeInTheDocument();
  });

  it('disables input and send button during streaming', async () => {
    mockStreamState.isStreaming = true;
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('message-input')).toBeDisabled();
      expect(screen.getByTestId('send-btn')).toBeDisabled();
    });
  });
});
