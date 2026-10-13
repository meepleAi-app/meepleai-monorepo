/**
 * #4168 (5g-b) — la vista mobile della chat davanti alla quota del piano.
 *
 * La pagina monta due viste, una per larghezza: questa e `ChatThreadView`. Il backend chiude lo
 * stream allo stesso modo per entrambe, con un evento Error di codice `TIER_LIMIT_EXCEEDED`. Anche
 * qui compare l'avviso con i numeri del piano e il link ai piani, al posto del messaggio grezzo del
 * backend dentro la bolla dell'assistente.
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

    const { api } = await import('@/lib/api');
    (api.chat.getThreadById as Mock).mockResolvedValue(thread);
    (api.chat.addMessage as Mock).mockResolvedValue(thread);
  });

  it('explains the plan quota and links to the plans, like the desktop view', async () => {
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
    // La domanda resta; la bolla vuota dell'assistente no.
    expect(screen.getByText('Quante carte si pescano?')).toBeInTheDocument();
    expect(screen.getAllByTestId('message-user')).toHaveLength(1);
    expect(screen.queryAllByTestId('message-assistant')).toHaveLength(0);
  });

  const QUOTA_STOP = {
    type: qa.EVENT.ERROR,
    data: { errorCode: 'TIER_LIMIT_EXCEEDED', current: 3, max: 3, upgradeUrl: '/pricing' },
  };

  /** Il posto dell'avviso è nella pagina da prima, vuoto, come sul desktop. */
  it('the place for the quota notice is in the page before any question, empty', async () => {
    render(<ChatMobile threadId="thread-1" />);
    await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());

    const region = screen.getByTestId('tier-limit-region');
    expect(region).toHaveAttribute('role', 'status');
    expect(region).toBeEmptyDOMElement();
  });

  /** L'avviso riguarda la domanda appena fermata: con la successiva se ne va. */
  it('the quota notice goes away with the next question', async () => {
    qa.script = [QUOTA_STOP];
    const user = userEvent.setup();
    await ask(user);
    await screen.findByTestId('tier-limit-exceeded');

    qa.script = [
      { type: qa.EVENT.TOKEN, data: { token: 'Cinque carte.' } },
      { type: qa.EVENT.COMPLETE, data: { answer: 'Cinque carte.', snippets: [] } },
    ];
    await user.type(screen.getByTestId('message-input'), 'E adesso?');
    await user.click(screen.getByTestId('send-btn'));

    expect(await screen.findByText('Cinque carte.')).toBeInTheDocument();
    expect(screen.queryByTestId('tier-limit-exceeded')).not.toBeInTheDocument();
  });

  /** La pagina non rimonta la vista cambiando thread: l'avviso non deve restare sull'altro. */
  it('the quota notice does not follow the user to another thread', async () => {
    qa.script = [QUOTA_STOP];
    const user = userEvent.setup();
    const { rerender } = render(<ChatMobile threadId="thread-1" />);
    await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());
    await user.type(screen.getByTestId('message-input'), 'Quante carte si pescano?');
    await user.click(screen.getByTestId('send-btn'));
    await screen.findByTestId('tier-limit-exceeded');

    const { api } = await import('@/lib/api');
    (api.chat.getThreadById as Mock).mockResolvedValue({
      ...thread,
      id: 'thread-2',
      title: 'Azul',
    });
    rerender(<ChatMobile threadId="thread-2" />);

    // L'asserzione vale a thread caricato: durante il caricamento la vista mostra solo l'attesa.
    await waitFor(() => expect(api.chat.getThreadById).toHaveBeenCalledWith('thread-2'));
    await screen.findByTestId('message-input');
    expect(screen.queryByTestId('tier-limit-exceeded')).not.toBeInTheDocument();
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
