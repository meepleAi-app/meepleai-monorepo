/**
 * #4838 — la vista mobile della chat salva la risposta dell'assistente, come `ChatThreadView`.
 *
 * La pagina `/chat/[threadId]` monta due viste, una per larghezza. Entrambe salvano la domanda con
 * `addMessage`; solo il desktop salvava anche la risposta. Su telefono la risposta si vedeva mentre
 * arrivava e spariva ricaricando il thread, che mostrava solo la domanda.
 */

import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';

import { ChatMobile } from '../ChatMobile';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadById: vi.fn(),
      addMessage: vi.fn(),
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
  /** Vero quando lo stream finto ha finito: per fine naturale o perché la vista è uscita dal ciclo. */
  finished: false,
}));

vi.mock('@/lib/api/clients/chatClient', () => ({
  QA_EVENT_TYPES: qa.EVENT,
  qaStream: async function* qaStreamMock() {
    try {
      for (const ev of qa.script) yield ev as QaEvent;
    } finally {
      qa.finished = true;
    }
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

const QUESTION = 'Quante carte si pescano?';

async function ask(user: ReturnType<typeof userEvent.setup>) {
  render(<ChatMobile threadId="thread-1" />);
  await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());
  await user.type(screen.getByTestId('message-input'), QUESTION);
  await user.click(screen.getByTestId('send-btn'));
}

/** Le chiamate a `addMessage` fatte dalla vista, nell'ordine. */
async function savedMessages() {
  const { api } = await import('@/lib/api');
  return (api.chat.addMessage as Mock).mock.calls.map(([threadId, body]) => ({ threadId, body }));
}

/**
 * Aspetta che lo stream sia finito e che la vista abbia eseguito ciò che segue il ciclo. Serve alle
 * asserzioni «non è stato salvato niente»: senza aspettare, passerebbero anche se il salvataggio
 * partisse un istante dopo.
 */
async function theStreamHasEnded() {
  await waitFor(() => expect(qa.finished).toBe(true));
  await act(async () => {});
}

describe('ChatMobile — salva la risposta dell’assistente (#4838)', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    qa.script = [];
    qa.finished = false;

    const { api } = await import('@/lib/api');
    (api.chat.getThreadById as Mock).mockResolvedValue(thread);
    (api.chat.addMessage as Mock).mockResolvedValue(thread);
  });

  it('saves the question and then the answer, with the profile version and the language', async () => {
    qa.script = [
      { type: qa.EVENT.TOKEN, data: { token: 'Cinque ' } },
      { type: qa.EVENT.TOKEN, data: { token: 'carte.' } },
      {
        type: qa.EVENT.COMPLETE,
        data: { answer: 'Cinque carte.', snippets: [], profileVersionId: 'pv-7', language: 'it' },
      },
    ];
    const user = userEvent.setup();

    await ask(user);

    await waitFor(async () => expect(await savedMessages()).toHaveLength(2));
    expect(await savedMessages()).toEqual([
      { threadId: 'thread-1', body: { content: QUESTION, role: 'user' } },
      {
        threadId: 'thread-1',
        body: {
          content: 'Cinque carte.',
          role: 'assistant',
          profileVersionId: 'pv-7',
          language: 'it',
        },
      },
    ]);
  });

  it('saves the text of the Complete event when no token streamed', async () => {
    qa.script = [{ type: qa.EVENT.COMPLETE, data: { answer: 'Cinque carte.', snippets: [] } }];
    const user = userEvent.setup();

    await ask(user);

    await waitFor(async () => expect(await savedMessages()).toHaveLength(2));
    expect((await savedMessages())[1].body).toMatchObject({
      content: 'Cinque carte.',
      role: 'assistant',
    });
  });

  it('does not save a partial answer when the stream stops on an error', async () => {
    // Come sul desktop: il testo parziale resta nella bolla, ma non diventa un messaggio del thread.
    qa.script = [
      { type: qa.EVENT.TOKEN, data: { token: 'Cin' } },
      {
        type: qa.EVENT.ERROR,
        data: { errorCode: 'AI_PROVIDER_UNAVAILABLE', errorMessage: 'Provider unavailable' },
      },
    ];
    const user = userEvent.setup();

    await ask(user);

    expect(await screen.findByText('Provider unavailable')).toBeInTheDocument();
    await theStreamHasEnded();
    expect(await savedMessages()).toEqual([
      { threadId: 'thread-1', body: { content: QUESTION, role: 'user' } },
    ]);
  });

  it('does not save an empty answer', async () => {
    qa.script = [{ type: qa.EVENT.COMPLETE, data: { answer: '', snippets: [] } }];
    const user = userEvent.setup();

    await ask(user);

    await theStreamHasEnded();
    expect(await savedMessages()).toEqual([
      { threadId: 'thread-1', body: { content: QUESTION, role: 'user' } },
    ]);
  });
});
