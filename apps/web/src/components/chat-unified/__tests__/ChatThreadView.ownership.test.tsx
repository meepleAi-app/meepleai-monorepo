/**
 * #4155 — la chat al primo diniego per possesso non dichiarato.
 *
 * Il backend respinge la domanda con un evento Error `RAG_OWNERSHIP_REQUIRED` (cancello RAG,
 * #443). La chat non deve mostrarlo come un guasto: spiega il motivo, offre la dichiarazione
 * per il gioco del thread e, dichiarato il possesso, rinvia la stessa domanda. Se il gioco non
 * ha un manuale caricato lo dice, invece di rinviare una domanda che non può ricevere citazioni.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';

import { ChatThreadView } from '../ChatThreadView';

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

// Ogni invocazione di qaStream consuma il copione successivo della coda, così un test può
// descrivere «prima il diniego, poi la risposta» e contare quante domande sono partite.
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
  scripts: [] as Array<Array<{ type: number; data: unknown }>>,
  requests: [] as Array<{ gameId: string; query: string }>,
}));

vi.mock('@/lib/api/clients/chatClient', () => ({
  QA_EVENT_TYPES: qa.EVENT,
  qaStream: async function* qaStreamMock(request: { gameId: string; query: string }) {
    qa.requests.push(request);
    for (const ev of qa.scripts.shift() ?? []) yield ev as QaEvent;
  },
}));

const OWNERSHIP_DENIAL: QaEvent = {
  type: qa.EVENT.ERROR,
  data: {
    errorMessage:
      'Per consultare il manuale di questo gioco devi dichiarare di possederne una copia.',
    errorCode: 'RAG_OWNERSHIP_REQUIRED',
    gameId: 'game-1',
  },
};

const ANSWER: QaEvent[] = [
  { type: qa.EVENT.TOKEN, data: { token: 'Vince chi arriva a 10 punti.' } },
  { type: qa.EVENT.COMPLETE, data: { answer: 'Vince chi arriva a 10 punti.', snippets: [] } },
];

const thread = {
  id: 'thread-1',
  title: 'Catan',
  gameId: 'game-1',
  agentId: null,
  status: 'Active',
  messages: [] as unknown[],
};

function ownershipResult(kbCardCount: number) {
  return {
    gameState: 'Owned',
    ownershipDeclaredAt: '2026-10-11T10:00:00Z',
    hasRagAccess: true,
    kbCardCount,
    isRagPublic: false,
  };
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let apiMock: any;

async function askAndGetDenied(user: ReturnType<typeof userEvent.setup>) {
  render(<ChatThreadView threadId="thread-1" />);
  await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());
  await user.type(screen.getByTestId('message-input'), 'Come si vince?');
  await user.click(screen.getByTestId('send-btn'));
  return screen.findByTestId('rag-ownership-required');
}

async function declareOwnership(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Dichiara di possedere Catan' }));
  await user.click(await screen.findByTestId('ownership-checkbox'));
  await user.click(screen.getByRole('button', { name: 'Conferma Possesso' }));
}

describe('ChatThreadView — diniego per possesso non dichiarato (#4155)', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    qa.scripts.length = 0;
    qa.requests.length = 0;

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

  it('explains the denial and offers the declaration instead of a generic error', async () => {
    qa.scripts.push([OWNERSHIP_DENIAL]);
    const user = userEvent.setup();

    const panel = await askAndGetDenied(user);

    expect(panel).toHaveTextContent('dichiarare di possederne una copia');
    expect(screen.getByRole('button', { name: 'Dichiara di possedere Catan' })).toBeInTheDocument();
    // Non è un guasto: niente banner d'errore generico.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    // Il segnaposto vuoto dell'assistente se ne va: resta solo la domanda.
    expect(screen.queryAllByTestId('message-assistant')).toHaveLength(0);
  });

  it('after the declaration resends the same question once, without duplicating it', async () => {
    qa.scripts.push([OWNERSHIP_DENIAL], ANSWER);
    (apiMock.library.declareOwnership as Mock).mockResolvedValue(ownershipResult(3));
    const user = userEvent.setup();

    await askAndGetDenied(user);
    await declareOwnership(user);

    expect(await screen.findByText('Vince chi arriva a 10 punti.')).toBeInTheDocument();
    expect(apiMock.library.declareOwnership).toHaveBeenCalledWith('game-1');
    expect(qa.requests.map(r => r.query)).toEqual(['Come si vince?', 'Come si vince?']);
    // La domanda dell'utente è già nel thread: il rinvio non la salva una seconda volta.
    const savedUserMessages = (apiMock.chat.addMessage as Mock).mock.calls.filter(
      ([, message]) => message.role === 'user'
    );
    expect(savedUserMessages).toHaveLength(1);
    expect(screen.queryByTestId('rag-ownership-required')).not.toBeInTheDocument();
    expect(screen.getAllByTestId('message-assistant')).toHaveLength(1);
  });

  it('with no manual loaded says so and does not resend the question', async () => {
    qa.scripts.push([OWNERSHIP_DENIAL]);
    (apiMock.library.declareOwnership as Mock).mockResolvedValue(ownershipResult(0));
    const user = userEvent.setup();

    await askAndGetDenied(user);
    await declareOwnership(user);

    expect(
      await screen.findByText(/per Catan non c'è ancora un manuale caricato/i)
    ).toBeInTheDocument();
    expect(qa.requests).toHaveLength(1);
  });
});
