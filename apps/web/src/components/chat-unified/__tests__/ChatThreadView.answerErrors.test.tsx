/**
 * #4168 — la chat quando il backend chiude una risposta senza darla.
 *
 * Il gateway e la pipeline di risposta (ADR-095) mandano un evento Error con un codice: tetto di
 * spesa, nessun modello disponibile, profilo dell'agente assente, risposta interrotta dopo il primo
 * token. La chat mostra il motivo in italiano invece del messaggio grezzo del backend, e una
 * risposta interrotta non finisce nella cronologia salvata come se fosse completa.
 *
 * Una risposta completa, invece, si salva con la versione del profilo e la lingua che il
 * completamento le ha dato (ADR-095 D2): il feedback e le valutazioni si riferiscono a quella.
 * Il feedback su quella risposta parte con l'id che il backend le ha dato salvandola, non con
 * l'id provvisorio della bolla: con quello il backend non trova la risposta.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';

import { ChatThreadView } from '../ChatThreadView';
import { qaStreamErrorMessage } from '../qaStreamErrors';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

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
      declareOwnership: vi.fn(),
    },
    knowledgeBase: {
      submitKbFeedback: vi.fn(),
    },
  },
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

type QaEvent = { type: number; data: unknown };
const qa = vi.hoisted(() => ({
  EVENT: {
    STATE_UPDATE: 0,
    CITATIONS: 1,
    COMPLETE: 4,
    ERROR: 5,
    TOKEN: 7,
    INLINE_CITATION: 28,
    CONTINUATION_AVAILABLE: 29,
  } as const,
  script: [] as Array<{ type: number; data: unknown }>,
}));

vi.mock('@/lib/api/clients/chatClient', () => ({
  QA_EVENT_TYPES: qa.EVENT,
  qaStream: async function* qaStreamMock() {
    for (const ev of qa.script) yield ev as QaEvent;
  },
}));

const thread = {
  id: 'thread-1',
  title: 'Catan',
  gameId: 'game-1',
  agentId: null,
  status: 'Active',
  messages: [] as unknown[],
};

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let apiMock: any;

async function ask(user: ReturnType<typeof userEvent.setup>) {
  render(<ChatThreadView threadId="thread-1" />);
  await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());
  await user.type(screen.getByTestId('message-input'), 'Quante carte si pescano?');
  await user.click(screen.getByTestId('send-btn'));
}

function savedAssistantMessages() {
  return (apiMock.chat.addMessage as Mock).mock.calls.filter(
    ([, message]) => message.role === 'assistant'
  );
}

describe('ChatThreadView — risposta chiusa con un codice (#4168)', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    qa.script = [];

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
    (apiMock.chat.getThreadById as Mock).mockResolvedValue(thread);
    (apiMock.games.getById as Mock).mockResolvedValue({ id: 'game-1', title: 'Catan' });
    (apiMock.chat.addMessage as Mock).mockResolvedValue(thread);
  });

  it('an answer interrupted after the first token is explained and not saved as complete', async () => {
    qa.script = [
      { type: qa.EVENT.TOKEN, data: { token: 'Ogni giocatore pesca' } },
      {
        type: qa.EVENT.ERROR,
        data: {
          errorCode: 'AI_STREAM_INTERRUPTED',
          errorMessage: 'The answer was interrupted before it was complete. Please ask again.',
        },
      },
    ];
    const user = userEvent.setup();

    await ask(user);

    expect(await screen.findByText(/si è interrotta prima della fine/)).toBeInTheDocument();
    expect(screen.queryByText(/The answer was interrupted/)).not.toBeInTheDocument();
    expect(savedAssistantMessages()).toHaveLength(0);
  });

  /**
   * #4168 (5g-b): la quota del piano. Non è un guasto, quindi non usa il banner d'errore: spiega
   * quante domande sono state usate, quando riparte il conteggio, e porta ai piani. La bolla vuota
   * dell'assistente sparisce e niente viene salvato come risposta.
   */
  it('a question stopped by the plan quota is explained, with the way to the plans', async () => {
    qa.script = [
      {
        type: qa.EVENT.ERROR,
        data: {
          errorCode: 'TIER_LIMIT_EXCEEDED',
          errorMessage: 'Tier limit exceeded: AgentQuery (3/3)',
          limitType: 'AgentQuery',
          current: 3,
          max: 3,
          upgradeUrl: '/pricing',
        },
      },
    ];
    const user = userEvent.setup();

    await ask(user);

    const notice = await screen.findByTestId('tier-limit-exceeded');
    expect(notice).toHaveTextContent(
      'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3)'
    );
    expect(notice).toHaveTextContent(/riparte alle \d{2}:\d{2} \(mezzanotte UTC\)/);
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
    expect(screen.queryByText(/Tier limit exceeded/)).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    // La domanda resta nel thread, la bolla vuota dell'assistente no.
    expect(screen.getAllByTestId('message-user')).toHaveLength(1);
    expect(screen.queryAllByTestId('message-assistant')).toHaveLength(0);
    expect(savedAssistantMessages()).toHaveLength(0);
  });

  /**
   * Il posto dell'avviso è nella pagina da prima, vuoto: è così che uno screen reader si accorge
   * di quando si riempie. Montato insieme all'avviso, non tutti lo annuncerebbero.
   */
  it('the place for the quota notice is in the page before any question, empty', async () => {
    render(<ChatThreadView threadId="thread-1" />);
    await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());

    const region = screen.getByTestId('tier-limit-region');
    expect(region).toHaveAttribute('role', 'status');
    expect(region).toBeEmptyDOMElement();
  });

  /** L'avviso riguarda la domanda appena fermata: con la successiva se ne va. */
  it('the quota notice goes away with the next question', async () => {
    qa.script = [
      {
        type: qa.EVENT.ERROR,
        data: { errorCode: 'TIER_LIMIT_EXCEEDED', current: 3, max: 3, upgradeUrl: '/pricing' },
      },
    ];
    const user = userEvent.setup();
    await ask(user);
    await screen.findByTestId('tier-limit-exceeded');

    qa.script = [
      { type: qa.EVENT.TOKEN, data: { token: 'Cinque carte.' } },
      { type: qa.EVENT.COMPLETE, data: { answer: 'Cinque carte.', snippets: [] } },
    ];
    await user.type(screen.getByTestId('message-input'), 'E adesso?');
    await user.click(screen.getByTestId('send-btn'));

    // La domanda successiva è passata davvero: la risposta è arrivata, e l'avviso non c'è più.
    expect(await screen.findByText('Cinque carte.')).toBeInTheDocument();
    expect(screen.queryByTestId('tier-limit-exceeded')).not.toBeInTheDocument();
  });

  /**
   * La pagina non rimonta la vista quando si passa da un thread all'altro: l'avviso di un thread
   * non deve restare sull'altro.
   */
  it('the quota notice does not follow the user to another thread', async () => {
    qa.script = [
      {
        type: qa.EVENT.ERROR,
        data: { errorCode: 'TIER_LIMIT_EXCEEDED', current: 3, max: 3, upgradeUrl: '/pricing' },
      },
    ];
    const user = userEvent.setup();
    const { rerender } = render(<ChatThreadView threadId="thread-1" />);
    await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());
    await user.type(screen.getByTestId('message-input'), 'Quante carte si pescano?');
    await user.click(screen.getByTestId('send-btn'));
    await screen.findByTestId('tier-limit-exceeded');

    (apiMock.chat.getThreadById as Mock).mockResolvedValue({
      ...thread,
      id: 'thread-2',
      title: 'Azul',
    });
    rerender(<ChatThreadView threadId="thread-2" />);

    // Mentre il thread nuovo si carica la vista mostra solo l'attesa, e l'avviso non c'è comunque:
    // l'asserzione vale a thread caricato.
    await waitFor(() => expect(apiMock.chat.getThreadById).toHaveBeenCalledWith('thread-2'));
    await screen.findByTestId('message-input');
    expect(screen.queryByTestId('tier-limit-exceeded')).not.toBeInTheDocument();
  });

  it.each([
    ['AI_BUDGET_DAILY_EXHAUSTED', /in pausa fino a domani/],
    ['AI_PROVIDER_UNAVAILABLE', /temporaneamente non disponibile/],
    ['AGENT_PROFILE_NOT_PUBLISHED', /non è ancora configurato/],
  ])('%s is shown in Italian', async (errorCode, expected) => {
    qa.script = [
      { type: qa.EVENT.ERROR, data: { errorCode, errorMessage: 'English backend message' } },
    ];
    const user = userEvent.setup();

    await ask(user);

    expect(await screen.findByText(expected)).toBeInTheDocument();
    expect(screen.queryByText('English backend message')).not.toBeInTheDocument();
    expect(savedAssistantMessages()).toHaveLength(0);
  });
});

describe('ChatThreadView — risposta completa (#4168)', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { api } = await import('@/lib/api');
    apiMock = api;
    (apiMock.chat.getThreadById as Mock).mockResolvedValue(thread);
    (apiMock.games.getById as Mock).mockResolvedValue({ id: 'game-1', title: 'Catan' });
    (apiMock.chat.addMessage as Mock).mockResolvedValue(thread);
  });

  it('saves the answer with the profile version and the language of the completion', async () => {
    qa.script = [
      { type: qa.EVENT.TOKEN, data: { token: 'Ogni giocatore pesca due carte.' } },
      {
        type: qa.EVENT.COMPLETE,
        data: {
          answer: 'Ogni giocatore pesca due carte.',
          snippets: [],
          profileVersionId: 'b4c5d6e7-0000-4000-8000-000000000001',
          language: 'it',
        },
      },
    ];
    const user = userEvent.setup();

    await ask(user);

    await waitFor(() => expect(savedAssistantMessages()).toHaveLength(1));
    expect(savedAssistantMessages()[0][1]).toMatchObject({
      content: 'Ogni giocatore pesca due carte.',
      profileVersionId: 'b4c5d6e7-0000-4000-8000-000000000001',
      language: 'it',
    });
  });
});

describe('ChatThreadView — feedback sulla risposta appena salvata (#4168)', () => {
  const SAVED_ID = '6f1c2a3b-4d5e-4f60-8a71-b2c3d4e5f607';

  beforeEach(async () => {
    vi.clearAllMocks();
    const { api } = await import('@/lib/api');
    apiMock = api;
    (apiMock.chat.getThreadById as Mock).mockResolvedValue(thread);
    (apiMock.games.getById as Mock).mockResolvedValue({ id: 'game-1', title: 'Catan' });
    (apiMock.knowledgeBase.submitKbFeedback as Mock).mockResolvedValue(undefined);
    (apiMock.chat.addMessage as Mock).mockImplementation(async (_threadId, message) =>
      message.role === 'assistant'
        ? {
            ...thread,
            messages: [
              {
                backendMessageId: SAVED_ID,
                role: 'assistant',
                content: message.content,
                timestamp: '2026-10-12T08:01:00Z',
              },
            ],
          }
        : thread
    );
  });

  it('sends the feedback with the id the backend gave the answer', async () => {
    qa.script = [
      { type: qa.EVENT.TOKEN, data: { token: 'Ogni giocatore pesca due carte.' } },
      {
        type: qa.EVENT.COMPLETE,
        data: { answer: 'Ogni giocatore pesca due carte.', snippets: [] },
      },
    ];
    const user = userEvent.setup();

    await ask(user);
    await waitFor(() => expect(savedAssistantMessages()).toHaveLength(1));
    await user.click(await screen.findByRole('button', { name: 'Rate as helpful' }));

    await waitFor(() =>
      expect(apiMock.knowledgeBase.submitKbFeedback).toHaveBeenCalledWith(
        'game-1',
        expect.objectContaining({ chatSessionId: 'thread-1', messageId: SAVED_ID })
      )
    );
  });
});

describe('qaStreamErrorMessage', () => {
  it('keeps the backend message for a code it does not know', () => {
    expect(qaStreamErrorMessage('SOMETHING_NEW', 'Backend says why')).toBe('Backend says why');
  });

  it('falls back to a generic message when there is nothing to show', () => {
    expect(qaStreamErrorMessage(undefined, undefined)).toBe('Errore nella risposta AI');
  });

  it('does not treat inherited object keys as codes', () => {
    expect(qaStreamErrorMessage('toString', 'Backend says why')).toBe('Backend says why');
  });
});
