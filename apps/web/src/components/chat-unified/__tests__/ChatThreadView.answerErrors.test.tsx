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
