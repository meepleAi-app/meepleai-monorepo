/**
 * #4999 (ADR-098 §5) — sul telefono l'intento del thread si mostra in sola lettura.
 *
 * `ChatMobile` caricava il thread con `getThreadById`, che da #4404 riporta `agentType`, ma non lo
 * mostrava: chi apriva un thread «Tutor» dal telefono non sapeva in che forma avrebbe risposto.
 * Per decisione del committente (2026-11-12) sul telefono l'intento non si sceglie: si legge.
 */

import { render, screen, within } from '@testing-library/react';
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

vi.mock('@/lib/api/clients/chatClient', () => ({
  QA_EVENT_TYPES: { TOKEN: 7, COMPLETE: 4, ERROR: 5 },
  // Nessuna domanda parte in questi test.
  qaStream: async function* qaStreamMock() {},
}));

function threadWith(agentType: string | null) {
  return {
    id: 'thread-1',
    title: 'Catan',
    gameId: 'game-1',
    agentId: null,
    agentType,
    status: 'Active',
    messages: [] as unknown[],
  };
}

async function renderThread(agentType: string | null): Promise<HTMLElement> {
  const { api } = await import('@/lib/api');
  (api.chat.getThreadById as Mock).mockResolvedValue(threadWith(agentType));
  render(<ChatMobile threadId="thread-1" />);
  return screen.findByTestId('thread-intent');
}

describe('ChatMobile — l intento del thread, in sola lettura (#4999)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('un thread «tutor» mostra «Tutor», e nessun controllo permette di cambiarlo', async () => {
    const intent = await renderThread('tutor');

    expect(intent).toHaveTextContent('Tutor');
    expect(within(intent).queryAllByRole('button')).toHaveLength(0);
    // L'intestazione ha il solo «Torna indietro»: niente selettore, menu o gruppo di scelta.
    const header = screen.getByRole('banner');
    expect(
      within(header)
        .getAllByRole('button')
        .map(b => b.getAttribute('aria-label'))
    ).toEqual(['Torna indietro']);
    for (const role of ['combobox', 'listbox', 'radiogroup', 'menu'] as const) {
      expect(screen.queryByRole(role)).not.toBeInTheDocument();
    }
  });

  it('un thread «secca» mostra «Risposta secca»', async () => {
    expect(await renderThread('secca')).toHaveTextContent('Risposta secca');
  });

  it('un thread con un intento ritirato mostra «Auto», come lo legge il server', async () => {
    expect(await renderThread('arbitro')).toHaveTextContent('Auto');
    expect(screen.queryByText(/arbitro/i)).not.toBeInTheDocument();
  });
});
