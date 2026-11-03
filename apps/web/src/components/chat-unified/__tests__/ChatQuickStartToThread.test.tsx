/**
 * #4740 — un Quick Start della pagina di avvio chat apre un thread dove la domanda è già stata
 * fatta e la risposta arriva.
 *
 * Il difetto: `ChatEntryOrchestrator` mandava `initialMessage` a `createThread` e apriva il thread,
 * ma il backend non lo elabora e nessuna vista lo recuperava: il thread si apriva vuoto. Il test
 * che c'era passava perché asseriva solo la chiamata al `createThread` finto.
 *
 * Qui si segue il percorso intero, con l'orchestratore vero e le due viste vere montate insieme come
 * fa `app/(chat)/chat/[threadId]/page.tsx` (una è nascosta col CSS, non smontata): la domanda deve
 * arrivare a `qaStream` una volta sola, qualunque sia la larghezza.
 */

import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';

import { ChatEntryOrchestrator } from '@/components/chat/entry/ChatEntryOrchestrator';
import { stashPendingFirstMessage, takePendingFirstMessage } from '@/components/chat/shared';

import { ChatMobile } from '../ChatMobile';
import { ChatThreadView } from '../ChatThreadView';

const nav = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: nav.push, back: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      createThread: vi.fn(),
      getThreadById: vi.fn(),
      addMessage: vi.fn(),
      updateThreadTitle: vi.fn(),
      deleteThread: vi.fn(),
      switchThreadAgent: vi.fn(),
    },
    games: { getAll: vi.fn(), getById: vi.fn() },
    library: { getPrivateGame: vi.fn(), declareOwnership: vi.fn() },
    knowledgeBase: { submitKbFeedback: vi.fn() },
  },
}));

vi.mock('@/lib/analytics/flywheel-events', () => ({ trackRulesChatStarted: vi.fn() }));

// The real selector fetches and renders cards; here only the game it hands the orchestrator matters.
vi.mock('@/components/chat/entry/GameSelector', () => ({
  GameSelector: ({
    onSelect,
    onGamesLoaded,
  }: {
    onSelect: (id: string) => void;
    onGamesLoaded?: (games: { id: string; title: string; createdAt: string }[]) => void;
  }) => (
    <button
      data-testid="pick-catan"
      onClick={() => {
        onGamesLoaded?.([{ id: 'game-1', title: 'Catan', createdAt: '2026-01-01T00:00:00Z' }]);
        onSelect('game-1');
      }}
    />
  ),
}));

vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: () => ({
    state: {
      statusMessage: null,
      currentAnswer: '',
      followUpQuestions: [],
      isStreaming: false,
      error: null,
      chatThreadId: null,
      totalTokens: 0,
      debugSteps: [],
      modelDowngrade: null,
      strategyTier: null,
      executionId: null,
    },
    sendMessage: vi.fn(),
    stopStreaming: vi.fn(),
    reset: vi.fn(),
  }),
}));

vi.mock('../RagEnhancementsBadge', () => ({
  RagEnhancementsBadge: () => <div data-testid="rag-badge-mock" />,
}));

vi.mock('@/components/chat-unified/ChatInfoPanel', () => ({
  ChatInfoPanel: () => <div data-testid="chat-info-panel" />,
}));

vi.mock('@/components/agent/AgentSelector', () => ({
  AgentSelector: () => <select data-testid="agent-selector" />,
  AGENT_NAMES: { auto: 'Auto' },
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 'user-1', role: 'User', email: 'test@test.com', displayName: 'Test' },
  }),
}));

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

const qa = vi.hoisted(() => ({
  EVENT: { STATE_UPDATE: 0, CITATIONS: 1, COMPLETE: 4, ERROR: 5, TOKEN: 7 } as const,
  /** Una voce per ogni domanda che arriva davvero allo stream di risposta. */
  asked: [] as Array<{ gameId?: string; query: string; chatId?: string }>,
}));

vi.mock('@/lib/api/clients/chatClient', () => ({
  QA_EVENT_TYPES: {
    ...qa.EVENT,
    INLINE_CITATION: 28,
    CONTINUATION_AVAILABLE: 29,
  },
  qaStream: async function* qaStreamMock(params: {
    gameId?: string;
    query: string;
    chatId?: string;
  }) {
    qa.asked.push(params);
    yield { type: qa.EVENT.TOKEN, data: { token: 'Si tirano i dadi.' } };
    yield { type: qa.EVENT.COMPLETE, data: { answer: 'Si tirano i dadi.', snippets: [] } };
  },
}));

const QUESTION = 'Come si gioca a Catan?';

const emptyThread = {
  id: 'thread-1',
  title: 'Chat: Catan',
  gameId: 'game-1',
  agentId: null,
  status: 'Active',
  messages: [] as unknown[],
};

function setViewport(desktop: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: desktop && query === '(min-width: 64rem)',
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

/** Il clic sul Quick Start sulla pagina di avvio; poi la navigazione la fa il test. */
async function startFromQuickStart() {
  const { unmount } = render(<ChatEntryOrchestrator />);
  fireEvent.click(screen.getByTestId('pick-catan'));
  fireEvent.click(screen.getByText('Come si gioca a Catan'));
  await waitFor(() => expect(nav.push).toHaveBeenCalledWith('/chat/thread-1'));
  unmount();
}

/** La pagina del thread: le due viste insieme, come in `app/(chat)/chat/[threadId]/page.tsx`. */
function ThreadPage({ threadId }: { threadId: string }) {
  return (
    <>
      <div className="lg:hidden" data-testid="mobile-view">
        <ChatMobile threadId={threadId} />
      </div>
      <div className="hidden lg:block" data-testid="desktop-view">
        <ChatThreadView threadId={threadId} />
      </div>
    </>
  );
}

function openThreadPage(threadId = 'thread-1') {
  return render(<ThreadPage threadId={threadId} />);
}

describe('Quick Start → thread (#4740)', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    window.sessionStorage.clear();
    qa.asked.length = 0;

    const { api } = await import('@/lib/api');
    (api.chat.createThread as Mock).mockResolvedValue({ id: 'thread-1' });
    (api.chat.getThreadById as Mock).mockResolvedValue(emptyThread);
    (api.chat.addMessage as Mock).mockResolvedValue(emptyThread);
    (api.games.getById as Mock).mockResolvedValue({ id: 'game-1', title: 'Catan' });
  });

  it.each([
    ['wide', true, 'desktop-view'],
    ['narrow', false, 'mobile-view'],
  ])(
    'on a %s screen the question is asked once, in the view on screen, and the answer arrives',
    async (_label, desktop, viewOnScreen) => {
      setViewport(desktop);
      const { api } = await import('@/lib/api');
      await startFromQuickStart();

      openThreadPage();

      expect(await screen.findByText('Si tirano i dadi.')).toBeInTheDocument();
      // Una domanda sola all'stream, anche se due viste hanno caricato il thread.
      expect(qa.asked).toEqual([
        expect.objectContaining({ gameId: 'game-1', query: QUESTION, chatId: 'thread-1' }),
      ]);
      // E nella cronologia salvata la domanda c'è una volta sola.
      const saved = (api.chat.addMessage as Mock).mock.calls.map(([, message]) => message);
      expect(saved.filter(m => m.role === 'user')).toEqual([{ content: QUESTION, role: 'user' }]);
      // La domanda si vede nella vista che è sullo schermo; l'altra, nascosta, resta vuota.
      expect(within(screen.getByTestId(viewOnScreen)).getByText(QUESTION)).toBeInTheDocument();
      expect(screen.getAllByText(QUESTION)).toHaveLength(1);
    }
  );

  it('opens a thread with no question when the chat starts from the plain start button', async () => {
    setViewport(true);
    render(<ChatEntryOrchestrator />);
    fireEvent.click(screen.getByTestId('pick-catan'));
    fireEvent.click(screen.getByTestId('start-chat-btn'));
    await waitFor(() => expect(nav.push).toHaveBeenCalledWith('/chat/thread-1'));

    openThreadPage();

    await waitFor(() => expect(screen.getAllByTestId('message-input').length).toBeGreaterThan(0));
    expect(qa.asked).toEqual([]);
  });

  it('does not ask again when the thread is opened a second time (reload, back button)', async () => {
    setViewport(true);
    await startFromQuickStart();
    const first = openThreadPage();
    await screen.findByText('Si tirano i dadi.');
    first.unmount();
    qa.asked.length = 0;

    openThreadPage();

    await waitFor(() => expect(screen.getAllByTestId('message-input').length).toBeGreaterThan(0));
    expect(qa.asked).toEqual([]);
  });

  it('does not ask into a thread that already has messages', async () => {
    setViewport(true);
    const { api } = await import('@/lib/api');
    (api.chat.getThreadById as Mock).mockResolvedValue({
      ...emptyThread,
      messages: [{ role: 'user', content: 'Già chiesto', timestamp: '2026-11-03T10:00:00Z' }],
    });
    await startFromQuickStart();

    openThreadPage();

    await waitFor(() => expect(screen.getAllByTestId('message-input').length).toBeGreaterThan(0));
    expect(qa.asked).toEqual([]);
  });

  it('asks in the thread the question was left for, not in the one that was open when the id changed', async () => {
    setViewport(true);
    const { api } = await import('@/lib/api');
    (api.chat.getThreadById as Mock).mockImplementation(async (id: string) =>
      id === 'thread-2' ? { ...emptyThread, id: 'thread-2', gameId: 'game-2' } : emptyThread
    );
    // La domanda è per thread-2; thread-1 è quello aperto per primo, vuoto e di un altro gioco.
    stashPendingFirstMessage('thread-2', QUESTION);
    const { rerender } = openThreadPage('thread-1');
    await waitFor(() => expect(screen.getAllByTestId('message-input').length).toBeGreaterThan(0));
    expect(qa.asked).toEqual([]);

    rerender(<ThreadPage threadId="thread-2" />);

    await screen.findByText('Si tirano i dadi.');
    expect(qa.asked).toEqual([
      expect.objectContaining({ gameId: 'game-2', query: QUESTION, chatId: 'thread-2' }),
    ]);
  });

  it('does not take the question into a thread that has no game: nothing could answer it', async () => {
    setViewport(true);
    const { api } = await import('@/lib/api');
    (api.chat.getThreadById as Mock).mockResolvedValue({ ...emptyThread, gameId: null });
    stashPendingFirstMessage('thread-1', QUESTION);

    openThreadPage();

    await waitFor(() => expect(screen.getAllByTestId('message-input').length).toBeGreaterThan(0));
    expect(qa.asked).toEqual([]);
    expect(takePendingFirstMessage('thread-1')).toBe(QUESTION);
  });

  it('asks after the welcome message of a new thread with an agent, and keeps it', async () => {
    setViewport(true);
    const { api } = await import('@/lib/api');
    (api.chat.getThreadById as Mock).mockResolvedValue({
      ...emptyThread,
      agentId: 'agent-1',
      agentType: 'tutor',
    });
    stashPendingFirstMessage('thread-1', QUESTION);

    openThreadPage();

    await screen.findByText('Si tirano i dadi.');
    const kinds = within(screen.getByTestId('desktop-view'))
      .getAllByTestId(/^message-(user|assistant)$/)
      .map(el => el.getAttribute('data-testid'));
    // Il benvenuto del thread, la domanda, la risposta: nell'ordine, nessuno cancellato dagli altri.
    expect(kinds).toEqual(['message-assistant', 'message-user', 'message-assistant']);
    expect(qa.asked).toHaveLength(1);
  });

  /**
   * In sviluppo (`reactStrictMode`) ogni vista carica il thread due volte, e le due risposte arrivano
   * una dopo l'altra. La domanda parte alla prima, e la seconda non deve cancellare ciò che la prima ha
   * già messo a schermo: la risposta arriverebbe al server e non si vedrebbe.
   */
  it.each([
    ['wide', true, 'desktop-view'],
    ['narrow', false, 'mobile-view'],
  ])(
    'on a %s screen the question and its answer stay on screen when the thread is loaded twice, one after the other (StrictMode)',
    async (_label, desktop, viewOnScreen) => {
      setViewport(desktop);
      const { api } = await import('@/lib/api');
      let started = 0;
      let resolved = 0;
      (api.chat.getThreadById as Mock).mockImplementation(async () => {
        const order = ++started;
        await new Promise(resolve => setTimeout(resolve, order * 25));
        resolved += 1;
        return emptyThread;
      });
      stashPendingFirstMessage('thread-1', QUESTION);

      render(
        <React.StrictMode>
          <ThreadPage threadId="thread-1" />
        </React.StrictMode>
      );

      // Ogni vista carica due volte: aspettare che tutti i caricamenti siano finiti, perché l'ultimo è
      // quello che potrebbe cancellare le bolle.
      await waitFor(() => expect(started).toBeGreaterThanOrEqual(4));
      await waitFor(() => expect(resolved).toBe(started));
      const view = within(screen.getByTestId(viewOnScreen));
      expect(await view.findByText('Si tirano i dadi.')).toBeInTheDocument();
      expect(view.getByText(QUESTION)).toBeInTheDocument();
      expect(qa.asked).toHaveLength(1);
    }
  );
});
