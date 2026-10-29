/**
 * ChatEntryOrchestrator — the start page of a chat.
 *
 * #4715: a conversation with no game saves the user's message and never answers, because the answer
 * path needs the game to delimit retrieval. The start page used to offer "Continua senza gioco" and
 * quick starts that make no sense without one; it now starts a chat only with a game chosen.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ChatEntryOrchestrator } from '@/components/chat/entry/ChatEntryOrchestrator';

const mockPush = vi.fn();
const mockCreateThread = vi.fn();
let searchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  useSearchParams: () => searchParams,
}));

vi.mock('@/lib/api', () => ({
  api: { chat: { createThread: (...args: unknown[]) => mockCreateThread(...args) } },
}));

vi.mock('@/lib/analytics/flywheel-events', () => ({ trackRulesChatStarted: vi.fn() }));

// The real selectors fetch and render cards; here only what they hand the orchestrator matters.
vi.mock('@/components/chat/entry/GameSelector', () => ({
  GameSelector: ({
    onSelect,
    onGamesLoaded,
  }: {
    onSelect: (id: string) => void;
    onGamesLoaded?: (games: { id: string; title: string; createdAt: string }[]) => void;
  }) => (
    <div>
      <button
        data-testid="pick-catan"
        onClick={() => {
          onGamesLoaded?.([{ id: 'g1', title: 'Catan', createdAt: '2026-01-01T00:00:00Z' }]);
          onSelect('g1');
        }}
      />
      <button data-testid="pick-empty" onClick={() => onSelect('')} />
    </div>
  ),
}));

vi.mock('@/components/chat/entry/AgentSelector', () => ({
  AgentSelector: ({ onSelectSystemAgent }: { onSelectSystemAgent: (type: string) => void }) => (
    <button data-testid="pick-agent" onClick={() => onSelectSystemAgent('qa')} />
  ),
}));

beforeEach(() => {
  vi.clearAllMocks();
  searchParams = new URLSearchParams();
  mockCreateThread.mockResolvedValue({ id: 'thread-1' });
});

describe('ChatEntryOrchestrator — a chat needs a game (#4715)', () => {
  it('starts nothing and says why while no game is chosen', () => {
    render(<ChatEntryOrchestrator />);

    expect(screen.getByTestId('start-chat-btn')).toBeDisabled();
    expect(screen.getByTestId('start-needs-game')).toBeInTheDocument();
    expect(screen.queryByTestId('quick-start-section')).not.toBeInTheDocument();
  });

  it('does not create a thread when the start button is forced without a game', () => {
    render(<ChatEntryOrchestrator />);

    fireEvent.click(screen.getByTestId('start-chat-btn'));

    expect(mockCreateThread).not.toHaveBeenCalled();
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('keeps the start disabled when a selector reports an empty game id', () => {
    render(<ChatEntryOrchestrator />);

    fireEvent.click(screen.getByTestId('pick-empty'));

    expect(screen.getByTestId('start-chat-btn')).toBeDisabled();
    expect(screen.queryByTestId('quick-start-section')).not.toBeInTheDocument();
  });

  it('enables the start and shows the quick starts once a game is chosen', () => {
    render(<ChatEntryOrchestrator />);

    fireEvent.click(screen.getByTestId('pick-catan'));

    expect(screen.getByTestId('start-chat-btn')).toBeEnabled();
    expect(screen.queryByTestId('start-needs-game')).not.toBeInTheDocument();
    expect(screen.getByTestId('quick-start-section')).toBeInTheDocument();
    expect(screen.getByText('Come si gioca a Catan')).toBeInTheDocument();
  });

  it('creates the thread with the game and opens it', async () => {
    render(<ChatEntryOrchestrator />);
    fireEvent.click(screen.getByTestId('pick-catan'));

    fireEvent.click(screen.getByTestId('start-chat-btn'));

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith('/chat/thread-1'));
    expect(mockCreateThread).toHaveBeenCalledWith(
      expect.objectContaining({ gameId: 'g1', title: 'Chat: Catan' })
    );
  });

  it('starts from a quick start with the game and the message', async () => {
    render(<ChatEntryOrchestrator />);
    fireEvent.click(screen.getByTestId('pick-catan'));

    fireEvent.click(screen.getByText('Come si gioca a Catan'));

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith('/chat/thread-1'));
    expect(mockCreateThread).toHaveBeenCalledWith(
      expect.objectContaining({ gameId: 'g1', initialMessage: 'Come si gioca a Catan?' })
    );
  });

  it('with the game already in the URL, offers the quick starts and starts once an agent is picked', async () => {
    searchParams = new URLSearchParams('game=g9');
    render(<ChatEntryOrchestrator />);

    expect(screen.getByTestId('quick-start-section')).toBeInTheDocument();
    expect(screen.queryByTestId('start-needs-game')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('pick-agent'));
    fireEvent.click(screen.getByTestId('start-chat-btn'));

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith('/chat/thread-1'));
    expect(mockCreateThread).toHaveBeenCalledWith(expect.objectContaining({ gameId: 'g9' }));
  });
});
