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

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
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
      getById: vi.fn(),
    },
    library: {
      getPrivateGame: vi.fn(),
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
  debugSteps: [] as import('@/hooks/useAgentChatStream').DebugStep[],
  modelDowngrade: null as any,
  strategyTier: null as string | null,
  executionId: null as string | null,
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

// Mock RagEnhancementsBadge (uses useQuery which requires QueryClientProvider)
vi.mock('../RagEnhancementsBadge', () => ({
  RagEnhancementsBadge: () => <div data-testid="rag-badge-mock" />,
}));

// Mock ChatInfoPanel — path is `chat-unified/ChatInfoPanel` (the actual relative
// import in ChatThreadView). The previous `@/components/chat/ChatInfoPanel`
// target did not exist, so the mock never intercepted — the real component
// loaded, invoked `useTranslation()` without an `<IntlProvider>` ancestor, and
// crashed the render. Found in #2010 PR CI 2026-06-08.
vi.mock('@/components/chat-unified/ChatInfoPanel', () => ({
  ChatInfoPanel: ({ citations, suggestedQuestions, agent }: any) => (
    <div data-testid="chat-info-panel" data-agent-typology={agent?.typology ?? ''}>
      <span data-testid="citation-count">{citations?.length ?? 0}</span>
      <span data-testid="question-count">{suggestedQuestions?.length ?? 0}</span>
    </div>
  ),
}));

// Mock AgentSelector (Issue #4465)
vi.mock('@/components/agent/AgentSelector', () => ({
  AgentSelector: ({ value, onChange, disabled }: any) => (
    <>
      <select
        data-testid="agent-selector"
        data-value={value}
        value={value}
        onChange={(e: any) => onChange(e.target.value)}
        disabled={disabled}
      >
        <option value="auto">Auto</option>
        <option value="tutor">Tutor</option>
        <option value="secca">Risposta secca</option>
      </select>
      {/* A select does not fire onChange for the value it already shows; this does. */}
      <button
        type="button"
        data-testid="agent-selector-choose-auto"
        onClick={() => onChange('auto')}
      >
        choose auto
      </button>
    </>
  ),
  AGENT_NAMES: {
    auto: 'Auto',
    tutor: 'Tutor',
    secca: 'Risposta secca',
  },
}));

// Mock AuthProvider (Issue #4916: ChatThreadView now uses useAuth for admin detection)
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 'user-1', role: 'User', email: 'test@test.com', displayName: 'Test' },
  }),
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
      citations: [
        {
          documentId: 'doc-1',
          pageNumber: 5,
          snippet: 'Rule text',
          relevanceScore: 0.9,
          copyrightTier: 'full' as const,
        },
      ],
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
    (apiMock.games.getById as Mock).mockResolvedValue(mockGames.games[0]);
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

  // A conversation without a game saves what the user writes and never answers (there is nothing to scope the
  // retrieval by, and the per-agent chat route does not exist, #4139). It said so nowhere: the message just sat there.
  describe('conversation without a game', () => {
    it('says that messages are saved but not answered, and what to do', async () => {
      (apiMock.chat.getThreadById as Mock).mockResolvedValue({
        ...mockThread,
        gameId: null,
        agentId: null,
      });
      await renderView();

      const notice = await screen.findByTestId('chat-no-game-notice');
      expect(notice).toHaveTextContent(/non ricevono risposta/i);
      expect(notice).toHaveTextContent(/scegliendo un gioco/i);
    });

    it('does not show it when the conversation has a game', async () => {
      await renderView();
      await screen.findByTestId('chat-thread-view');

      expect(screen.queryByTestId('chat-no-game-notice')).not.toBeInTheDocument();
    });
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
      // After commit 67f7192b3 (refactor(chat): compose ChatMessageList with ChatMessage atom),
      // message styling lives inside the ChatMessage atom:
      // - User: outer wrapper has `flex-row-reverse` + inner bubble has `ml-auto`
      // - Assistant: outer wrapper has `flex-row` (not reversed)
      expect(userMsg.querySelector('.flex-row-reverse')).toBeInTheDocument();
      expect(userMsg.querySelector('.ml-auto')).toBeInTheDocument();
      expect(assistantMsg.querySelector('.flex-row-reverse')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Empty & Error States
  // --------------------------------------------------------------------------

  it('shows empty state for thread with no messages and no agent', async () => {
    (apiMock.chat.getThreadById as Mock).mockResolvedValue({
      ...mockThread,
      agentId: null,
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

  it('sends message via QA stream when gameId is present (even with agentId)', async () => {
    // #415: gameId takes priority over agentId — QA stream fires, not SSE agent path
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });

    const input = screen.getByTestId('message-input');
    await user.type(input, 'SSE message');
    await user.click(screen.getByTestId('send-btn'));

    await waitFor(() => {
      // With gameId present, message is saved via REST (QA stream path)
      expect(apiMock.chat.addMessage).toHaveBeenCalled();
    });

    // SSE agent path should NOT be called when gameId is available
    expect(mockSendViaSSE).not.toHaveBeenCalled();
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

  // --------------------------------------------------------------------------
  // Welcome Message (Issue #4780)
  // --------------------------------------------------------------------------

  // #5001 (ADR-098 §5): one welcome for every intent, about the rules. There was one per typology, keyed in
  // capitals while threads store lower case, so `auto`, `tutor` and `secca` landed on a fallback promising strategy.
  it.each(['auto', 'tutor', 'secca', null, 'Tutor', 'Arbitro', 'Stratega'])(
    'a new thread stored with %j gets the same welcome, about the rules',
    async stored => {
      (apiMock.chat.getThreadById as Mock).mockResolvedValue({
        ...mockThread,
        messages: [],
        agentType: stored,
      });

      await renderView();

      await screen.findByText(/Sono il tuo assistente per le regole di Catan/);
      expect(screen.getByText(/Cosa posso fare/)).toBeInTheDocument();
      // The thread is empty: the messages area holds the welcome and its questions, and nothing else.
      expect(screen.getByTestId('messages-area').textContent).not.toMatch(/strateg|arbitr|mosse/i);
    }
  );

  // #4158: il gioco del thread si risolve per id. Scorrendo la prima pagina del catalogo
  // (20 giochi in ordine alfabetico) un gioco oltre quella pagina restava senza nome.
  it('names the thread game even when it is beyond the first catalog page', async () => {
    (apiMock.chat.getThreadById as Mock).mockResolvedValue({
      ...mockThread,
      messages: [],
      agentType: 'Tutor',
    });
    (apiMock.games.getAll as Mock).mockResolvedValue({
      games: [],
      total: 165,
      page: 1,
      pageSize: 20,
      totalPages: 9,
    });
    (apiMock.games.getById as Mock).mockResolvedValue({ id: 'game-1', title: 'Catan' });

    await renderView();

    await waitFor(() => {
      expect(screen.getByText(/Sono il tuo assistente per le regole di Catan/)).toBeInTheDocument();
    });
    expect(apiMock.games.getById).toHaveBeenCalledWith('game-1');
  });

  // #4244: il gioco di un thread può essere privato. `/games/{id}` risolve solo il catalogo, quindi
  // il titolo si chiede alla rotta dei giochi privati; senza titolo non parte nemmeno il benvenuto.
  it('names the thread game when it is a private game', async () => {
    (apiMock.chat.getThreadById as Mock).mockResolvedValue({
      ...mockThread,
      messages: [],
      agentType: 'Tutor',
      gameId: 'private-1',
      privateGameId: 'private-1',
    });
    (apiMock.games.getById as Mock).mockRejectedValue(new Error('404'));
    (apiMock.library.getPrivateGame as Mock).mockResolvedValue({
      id: 'private-1',
      title: 'Il mio prototipo',
    });

    await renderView();

    await waitFor(() => {
      expect(
        screen.getByText(/Sono il tuo assistente per le regole di Il mio prototipo/)
      ).toBeInTheDocument();
    });
    expect(apiMock.library.getPrivateGame).toHaveBeenCalledWith('private-1');
    expect(apiMock.games.getById).not.toHaveBeenCalled();
  });

  describe('the intent of the answers (#4404)', () => {
    it.each([
      ['Arbitro', 'auto'],
      ['decisore', 'auto'],
      ['Stratega', 'auto'],
      ['whatever', 'auto'],
      ['Tutor', 'tutor'],
      ['secca', 'secca'],
    ])(
      'a thread stored with %j shows %j in the selector, never an empty one',
      async (stored, shown) => {
        (apiMock.chat.getThreadById as Mock).mockResolvedValue({
          ...mockThread,
          messages: [],
          agentType: stored,
        });

        await renderView();

        await waitFor(() => {
          expect(screen.getByTestId('agent-selector')).toHaveAttribute('data-value', shown);
        });
      }
    );

    it.each([
      ['Arbitro', 'auto'],
      ['secca', 'secca'],
    ])(
      'the info panel shows the same intent as the selector for a thread stored with %j',
      async (stored, shown) => {
        // The panel used to print the stored value as it came («Tipo: ARBITRO» next to a selector on Auto).
        (apiMock.chat.getThreadById as Mock).mockResolvedValue({
          ...mockThread,
          messages: [],
          agentType: stored,
        });

        await renderView();

        await waitFor(() => {
          expect(screen.getByTestId('chat-info-panel')).toHaveAttribute(
            'data-agent-typology',
            shown
          );
        });
      }
    );

    it('choosing what the selector already shows is not a change, even when the stored value is a retired id', async () => {
      (apiMock.chat.getThreadById as Mock).mockResolvedValue({
        ...mockThread,
        messages: [],
        agentType: 'Arbitro',
      });
      await renderView();
      await screen.findByTestId('agent-selector');

      fireEvent.click(screen.getByTestId('agent-selector-choose-auto'));

      expect(screen.queryByTestId('agent-switch-confirm')).not.toBeInTheDocument();
      expect(apiMock.chat.switchThreadAgent).not.toHaveBeenCalled();
    });

    // #5001 (ADR-098 §5): changing intent costs nothing and keeps the history, so there is no confirmation. The
    // banner was a leftover of the «distinct agents» model, and an alertdialog with no name and no focus.
    it('choosing an intent tells the server at once, with no step in between', async () => {
      (apiMock.chat.getThreadById as Mock).mockResolvedValue({
        ...mockThread,
        messages: [],
        agentType: 'Arbitro',
      });
      (apiMock.chat.switchThreadAgent as Mock).mockResolvedValue(undefined);
      await renderView();
      const selector = await screen.findByTestId('agent-selector');

      fireEvent.change(selector, { target: { value: 'tutor' } });

      await waitFor(() => {
        expect(apiMock.chat.switchThreadAgent).toHaveBeenCalledWith('thread-1', 'tutor');
      });
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
      expect(screen.queryByTestId('agent-switch-confirm')).not.toBeInTheDocument();
      await waitFor(() =>
        expect(screen.getByTestId('agent-selector')).toHaveAttribute('data-value', 'tutor')
      );
    });

    // From the review of #5058: with no confirmation, nothing kept two choices from racing on the server.
    it('a second choice waits for the first: the selector is disabled while the change is in flight', async () => {
      (apiMock.chat.getThreadById as Mock).mockResolvedValue({
        ...mockThread,
        messages: [],
        agentType: 'auto',
      });
      let finish!: () => void;
      (apiMock.chat.switchThreadAgent as Mock).mockReturnValueOnce(
        new Promise<void>(resolve => {
          finish = resolve;
        })
      );
      await renderView();
      const selector = await screen.findByTestId('agent-selector');

      fireEvent.change(selector, { target: { value: 'tutor' } });
      await waitFor(() => expect(screen.getByTestId('agent-selector')).toBeDisabled());
      fireEvent.change(screen.getByTestId('agent-selector'), { target: { value: 'secca' } });

      expect(apiMock.chat.switchThreadAgent).toHaveBeenCalledTimes(1);
      finish();
      await waitFor(() => expect(screen.getByTestId('agent-selector')).not.toBeDisabled());
      expect(screen.getByTestId('agent-selector')).toHaveAttribute('data-value', 'tutor');
    });

    it('a change that succeeds after a failed one takes the error away', async () => {
      (apiMock.chat.getThreadById as Mock).mockResolvedValue({
        ...mockThread,
        messages: [],
        agentType: 'auto',
      });
      (apiMock.chat.switchThreadAgent as Mock)
        .mockRejectedValueOnce(new Error('500'))
        .mockResolvedValueOnce(undefined);
      await renderView();
      const selector = await screen.findByTestId('agent-selector');

      fireEvent.change(selector, { target: { value: 'tutor' } });
      expect(await screen.findByText('Errore nel cambio del tipo di risposta')).toBeInTheDocument();
      await waitFor(() => expect(screen.getByTestId('agent-selector')).not.toBeDisabled());

      fireEvent.change(screen.getByTestId('agent-selector'), { target: { value: 'tutor' } });

      await waitFor(() =>
        expect(screen.getByTestId('agent-selector')).toHaveAttribute('data-value', 'tutor')
      );
      expect(screen.queryByText('Errore nel cambio del tipo di risposta')).not.toBeInTheDocument();
    });

    it('a change that answers after the user moved to another thread does not touch the new one', async () => {
      (apiMock.chat.getThreadById as Mock).mockImplementation(async (id: string) => ({
        ...mockThread,
        id,
        messages: [],
        agentType: 'auto',
      }));
      let finish!: () => void;
      (apiMock.chat.switchThreadAgent as Mock).mockReturnValueOnce(
        new Promise<void>(resolve => {
          finish = resolve;
        })
      );
      const { rerender } = await renderView('thread-1');
      fireEvent.change(await screen.findByTestId('agent-selector'), { target: { value: 'tutor' } });
      await waitFor(() =>
        expect(apiMock.chat.switchThreadAgent).toHaveBeenCalledWith('thread-1', 'tutor')
      );

      rerender(<ChatThreadView threadId="thread-2" />);
      await waitFor(() => expect(apiMock.chat.getThreadById).toHaveBeenCalledWith('thread-2'));
      finish();

      await waitFor(() => expect(screen.getByTestId('agent-selector')).not.toBeDisabled());
      expect(screen.getByTestId('agent-selector')).toHaveAttribute('data-value', 'auto');
    });

    it('a failed change leaves the selector on the stored intent and says so', async () => {
      (apiMock.chat.getThreadById as Mock).mockResolvedValue({
        ...mockThread,
        messages: [],
        agentType: 'secca',
      });
      (apiMock.chat.switchThreadAgent as Mock).mockRejectedValue(new Error('500'));
      await renderView();
      const selector = await screen.findByTestId('agent-selector');

      fireEvent.change(selector, { target: { value: 'tutor' } });

      expect(await screen.findByText('Errore nel cambio del tipo di risposta')).toBeInTheDocument();
      expect(apiMock.chat.switchThreadAgent).toHaveBeenCalledWith('thread-1', 'tutor');
      expect(screen.getByTestId('agent-selector')).toHaveAttribute('data-value', 'secca');
    });
  });

  it('does not show welcome message when thread has existing messages', async () => {
    (apiMock.chat.getThreadById as Mock).mockResolvedValue({
      ...mockThread,
      agentType: 'Tutor',
    });

    await renderView();

    await waitFor(() => {
      expect(screen.getByText('Hello!')).toBeInTheDocument();
    });
    expect(screen.queryByText(/Sono il tuo assistente per le regole/)).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Proxy Game Context (Issue #4780)
  // --------------------------------------------------------------------------

  it('uses QA stream (not SSE) when agentTypology is set but gameId is present', async () => {
    // #415: gameId takes priority — QA stream handles game-context AI responses
    (apiMock.chat.getThreadById as Mock).mockResolvedValue({
      ...mockThread,
      agentType: 'Tutor',
    });

    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });

    const input = screen.getByTestId('message-input');
    await user.type(input, 'Help with rules');
    await user.click(screen.getByTestId('send-btn'));

    await waitFor(() => {
      // QA stream path saves message via REST first
      expect(apiMock.chat.addMessage).toHaveBeenCalled();
    });

    // SSE agent path should NOT be called when gameId is available
    expect(mockSendViaSSE).not.toHaveBeenCalled();
  });

  // --------------------------------------------------------------------------
  // Debug Tab (Issue #4916)
  // --------------------------------------------------------------------------

  it('shows Chat and Debug tabs', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('tab-chat')).toBeInTheDocument();
      expect(screen.getByTestId('tab-debug')).toBeInTheDocument();
    });

    // Tabs have correct ARIA attributes
    expect(screen.getByTestId('tab-chat')).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('tab-debug')).toHaveAttribute('aria-selected', 'false');

    // Switch to Debug tab
    await user.click(screen.getByTestId('tab-debug'));
    expect(screen.getByTestId('tab-debug')).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('tab-chat')).toHaveAttribute('aria-selected', 'false');
  });

  it('shows empty debug panel when no steps', async () => {
    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('tab-debug')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('tab-debug'));
    expect(screen.getByTestId('debug-panel')).toBeInTheDocument();
    expect(screen.getByText(/Invia un messaggio per visualizzare il debug/)).toBeInTheDocument();
  });

  it('shows debug steps when debugSteps is non-empty', async () => {
    mockStreamState.debugSteps = [
      {
        type: 13,
        name: 'Retrieval Results',
        payload: { filteredCount: 5, totalResults: 10, latencyMs: 80 },
        timestamp: '2026-01-01T12:00:00.000Z',
        latencyMs: 80,
      },
    ];

    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('tab-debug')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('tab-debug'));
    expect(screen.getByTestId('debug-panel')).toBeInTheDocument();
    // DebugStepCard renders the step name
    expect(screen.getByText(/Retrieval Results/)).toBeInTheDocument();

    // Reset mock state
    mockStreamState.debugSteps = [];
  });

  it('passes isAdmin=false to DebugStepCard for regular user (systemPrompt redacted server-side)', async () => {
    // The AuthProvider mock returns role: 'User', isAdminOrAbove returns false
    // After server-side fix, systemPrompt arrives as [redacted] for non-admins
    // Frontend just renders what server sends - no additional client-side redaction needed for security
    mockStreamState.debugSteps = [
      {
        type: 16,
        name: 'Prompt Context',
        payload: {
          systemPrompt: '[redacted]',
          userPromptPreview: 'test',
          estimatedPromptTokens: 100,
        },
        timestamp: '2026-01-01T12:00:00.000Z',
      },
    ];

    const user = userEvent.setup();
    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('tab-debug')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('tab-debug'));
    await user.click(screen.getByRole('button', { name: /Prompt Context/i }));

    expect(screen.getByText(/\[redacted\]/)).toBeInTheDocument();

    // Reset mock state
    mockStreamState.debugSteps = [];
  });

  // ── Issue #5481: ResponseMetaBadge ──────────────────────────────────

  it('shows strategy badge on last assistant message when strategyTier is set', async () => {
    mockStreamState.strategyTier = 'Balanced';

    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('response-meta-badge')).toBeInTheDocument();
      expect(screen.getByText('Risposta bilanciata')).toBeInTheDocument();
    });

    // Reset
    mockStreamState.strategyTier = null;
  });

  it('does not show strategy badge when strategyTier is null', async () => {
    mockStreamState.strategyTier = null;

    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('message-assistant')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('response-meta-badge')).not.toBeInTheDocument();
  });

  it('does not show strategy badge while streaming', async () => {
    mockStreamState.strategyTier = 'Fast';
    mockStreamState.isStreaming = true;

    await renderView();

    await waitFor(() => {
      expect(screen.getByTestId('message-assistant')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('response-meta-badge')).not.toBeInTheDocument();

    // Reset
    mockStreamState.strategyTier = null;
    mockStreamState.isStreaming = false;
  });
});
