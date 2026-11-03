/**
 * #4730 — the chat button of the agent card.
 *
 * The card knows which game the agent belongs to (`data.gameId`) and used to open a thread WITHOUT it:
 * the server then has no rulebook to answer from, and the thread opens as a dead end («i messaggi vengono
 * salvati ma non ricevono risposta»). With a game the thread is created for that game; without one there is
 * no thread to create, so the button is not offered and the card says why.
 */
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { api } from '@/lib/api';

import { AgentExtraMeepleCard } from '../AgentExtraMeepleCard';

import type { AgentDetailData } from '../../types';

vi.mock('@/lib/api', () => ({
  api: { chat: { createThread: vi.fn() } },
}));
vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentThreads: vi.fn(() => ({ data: [], isLoading: false })),
  useAgentKbDocs: vi.fn(() => ({ data: [], isLoading: false })),
}));
vi.mock('@/hooks/useAgentStatus', () => ({
  useAgentStatus: () => ({
    status: { isReady: true, documentCount: 3, ragStatus: 'Ready', blockingReason: null },
    isLoading: false,
    error: null,
  }),
}));
vi.mock('@/components/chat-unified/ChatThreadView', () => ({
  ChatThreadView: ({ threadId }: { threadId: string }) => (
    <div data-testid="chat-thread-view" data-thread-id={threadId} />
  ),
}));

const agent: AgentDetailData = {
  id: 'agent-1',
  name: 'Azul Assistant',
  type: 'qa',
  strategyName: 'hybrid-rag',
  strategyParameters: {},
  isActive: true,
  isIdle: false,
  invocationCount: 3,
  lastInvokedAt: null,
  createdAt: '2024-01-01T00:00:00Z',
  gameId: 'game-1',
  gameName: 'Azul',
};

describe('AgentExtraMeepleCard — chat tab (#4730)', () => {
  beforeEach(() => {
    vi.mocked(api.chat.createThread).mockReset();
    vi.mocked(api.chat.createThread).mockResolvedValue({ id: 'thread-1' } as never);
  });

  it('creates the thread for the game of the agent, so that it has a rulebook to answer from', async () => {
    const user = userEvent.setup();
    render(<AgentExtraMeepleCard data={agent} enableChat />);

    await user.click(screen.getByRole('button', { name: 'Inizia Conversazione' }));

    await waitFor(() => expect(api.chat.createThread).toHaveBeenCalledTimes(1));
    expect(api.chat.createThread).toHaveBeenCalledWith(
      expect.objectContaining({ agentId: 'agent-1', gameId: 'game-1' })
    );
    // …and the conversation that starts is the one the card shows.
    await waitFor(() =>
      expect(screen.getByTestId('chat-thread-view')).toHaveAttribute('data-thread-id', 'thread-1')
    );
  });

  it('does not offer a conversation to an agent with no game: it would open as a dead end', () => {
    render(
      <AgentExtraMeepleCard
        data={{ ...agent, gameId: undefined, gameName: undefined }}
        enableChat
      />
    );

    expect(screen.queryByRole('button', { name: 'Inizia Conversazione' })).not.toBeInTheDocument();
    expect(screen.getByTestId('chat-no-game')).toHaveTextContent('Nessun gioco collegato');
    expect(api.chat.createThread).not.toHaveBeenCalled();
  });
});
