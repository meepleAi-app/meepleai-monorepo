/**
 * #4998 — «Continua» chiede la seconda metà di una risposta sul thread aperto.
 *
 * Quando il modello si ferma per lunghezza, il backend manda un `continuationToken` e la vista
 * mostra «Continua». Il server ricava intento e cronologia dal thread (`chatId`): una continuazione
 * senza `chatId` veniva generata come `auto`, senza cronologia, anche su un thread `tutor` o `secca`.
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
  /** Le richieste ricevute da `qaStream`, nell'ordine. */
  requests: [] as Array<Record<string, unknown>>,
}));

vi.mock('@/lib/api/clients/chatClient', () => ({
  QA_EVENT_TYPES: qa.EVENT,
  qaStream: async function* qaStreamMock(request: Record<string, unknown>) {
    qa.requests.push(request);
    for (const ev of qa.script) yield ev as QaEvent;
  },
}));

const thread = {
  id: 'thread-1',
  title: 'Catan',
  gameId: 'game-1',
  agentId: null,
  agentType: 'tutor',
  status: 'Active',
  messages: [] as unknown[],
};

describe('ChatThreadView — «Continua» resta sul thread (#4998)', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    qa.script = [];
    qa.requests = [];

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
    (api.chat.getThreadById as Mock).mockResolvedValue(thread);
    (api.games.getById as Mock).mockResolvedValue({ id: 'game-1', title: 'Catan' });
    (api.chat.addMessage as Mock).mockResolvedValue(thread);
  });

  it('the continuation request carries the open thread as chatId, like the question', async () => {
    qa.script = [
      { type: qa.EVENT.TOKEN, data: { token: 'Ogni giocatore pesca' } },
      { type: qa.EVENT.CONTINUATION_AVAILABLE, data: { continuationToken: 'tok-1' } },
      { type: qa.EVENT.COMPLETE, data: { answer: 'Ogni giocatore pesca', snippets: [] } },
    ];
    const user = userEvent.setup();
    render(<ChatThreadView threadId="thread-1" />);
    await waitFor(() => expect(screen.getByTestId('message-input')).toBeInTheDocument());
    await user.type(screen.getByTestId('message-input'), 'Quante carte si pescano?');
    await user.click(screen.getByTestId('send-btn'));
    const continueButton = await screen.findByTestId('continue-button');

    qa.script = [{ type: qa.EVENT.TOKEN, data: { token: ' due carte.' } }];
    await user.click(continueButton);

    await waitFor(() => expect(qa.requests).toHaveLength(2));
    expect(qa.requests[0]).toMatchObject({ chatId: 'thread-1' });
    expect(qa.requests[1]).toMatchObject({
      gameId: 'game-1',
      query: '',
      continuationToken: 'tok-1',
      chatId: 'thread-1',
    });
  });
});
