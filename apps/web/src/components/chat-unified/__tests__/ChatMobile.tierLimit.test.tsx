/**
 * #4168 (5g-b) — la vista mobile della chat davanti alla quota del piano.
 *
 * La pagina monta due viste, una per larghezza: questa e `ChatThreadView`. Il backend chiude lo
 * stream allo stesso modo per entrambe, con un evento Error di codice `TIER_LIMIT_EXCEEDED`. Qui
 * la spiegazione sta nella bolla dell'assistente, in italiano e con i numeri del piano, al posto
 * del messaggio grezzo del backend.
 */

import { render, screen, waitFor } from '@testing-library/react';
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

async function ask(user: ReturnType<typeof userEvent.setup>) {
  render(<ChatMobile threadId="thread-1" />);
  await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());
  await user.type(screen.getByTestId('message-input'), 'Quante carte si pescano?');
  await user.click(screen.getByTestId('send-btn'));
}

describe('ChatMobile — la quota del piano (#4168, 5g-b)', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    qa.script = [];
    // jsdom non implementa lo scorrimento: la vista porta in vista l'ultimo messaggio.
    Element.prototype.scrollIntoView = vi.fn();

    const { api } = await import('@/lib/api');
    (api.chat.getThreadById as Mock).mockResolvedValue(thread);
    (api.chat.addMessage as Mock).mockResolvedValue(thread);
  });

  it('explains the plan quota in Italian, with the numbers the backend sends', async () => {
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

    expect(
      await screen.findByText(
        'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3). Il conteggio riparte a mezzanotte UTC.'
      )
    ).toBeInTheDocument();
    expect(screen.queryByText(/Tier limit exceeded/)).not.toBeInTheDocument();
  });

  /** Gli altri codici restano com'erano: questa vista mostra il messaggio del backend. */
  it('still shows the backend message for another error', async () => {
    qa.script = [
      {
        type: qa.EVENT.ERROR,
        data: { errorCode: 'AI_PROVIDER_UNAVAILABLE', errorMessage: 'Provider unavailable' },
      },
    ];
    const user = userEvent.setup();

    await ask(user);

    expect(await screen.findByText('Provider unavailable')).toBeInTheDocument();
  });
});
