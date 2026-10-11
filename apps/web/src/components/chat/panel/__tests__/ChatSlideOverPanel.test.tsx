import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { useChatPanelStore } from '@/lib/stores/chat-panel-store';

// Mock the data hooks so the panel renders synchronously with empty lists
// #4187: spie sugli argomenti, per verificare che a pannello chiuso le query non partano.
const hookArgs = vi.hoisted(() => ({
  recent: [] as unknown[][],
  games: [] as unknown[][],
}));

vi.mock('@/hooks/queries/useChatSessions', () => ({
  useRecentChatSessions: (...args: unknown[]) => (
    hookArgs.recent.push(args),
    {
      data: { sessions: [], totalCount: 0 },
      isLoading: false,
      error: null,
    }
  ),
}));

vi.mock('@/hooks/queries/useGames', () => ({
  useGames: (...args: unknown[]) => (
    hookArgs.games.push(args),
    {
      data: { games: [], total: 0, page: 1, pageSize: 50, totalPages: 0 },
      isLoading: false,
      error: null,
    }
  ),
}));

// Mock the streaming hook to surface a stable, inert state
const streamResetMock = vi.fn();
const streamSendMock = vi.fn();
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
      connectionStatus: 'idle',
      retryCount: 0,
    },
    sendMessage: streamSendMock,
    reset: streamResetMock,
    stopStreaming: vi.fn(),
  }),
}));

// Stub the chat client — the panel only calls getThreadById directly from
// inside handleSelectChat, which none of these tests exercise.
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadById: vi.fn().mockResolvedValue(null),
    },
  },
}));

import { ChatSlideOverPanel } from '../ChatSlideOverPanel';

describe('ChatSlideOverPanel', () => {
  beforeEach(() => {
    useChatPanelStore.getState().close();
    useChatPanelStore.getState().clearGameContext();
    streamResetMock.mockClear();
    streamSendMock.mockClear();
  });

  it('renders nothing when panel is closed', () => {
    const { container } = render(<ChatSlideOverPanel />);
    expect(container).toBeEmptyDOMElement();
  });

  // #4187: il pannello è montato su ogni pagina autenticata (DesktopShell). A pannello chiuso le
  // sue query non devono partire: prima scaricavano 50 giochi e 50 sessioni a ogni pagina.
  it('does not run its queries while closed, and runs them once opened', () => {
    hookArgs.recent.length = 0;
    hookArgs.games.length = 0;
    const { rerender } = render(<ChatSlideOverPanel />);
    expect(hookArgs.recent.at(-1)?.[1]).toBe(false);
    expect(hookArgs.games.at(-1)?.[3]).toBe(false);

    act(() => useChatPanelStore.getState().open());
    rerender(<ChatSlideOverPanel />);
    expect(hookArgs.recent.at(-1)?.[1]).toBe(true);
    expect(hookArgs.games.at(-1)?.[3]).toBe(true);
  });

  it('renders panel when open', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);
    expect(screen.getByText(/Chat con l'agente/i)).toBeInTheDocument();
  });

  it('renders game context in switcher when set', () => {
    useChatPanelStore.getState().open({
      id: 'azul',
      name: 'Azul',
      year: 2017,
      pdfCount: 3,
      kbStatus: 'ready',
    });
    render(<ChatSlideOverPanel />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('closes when the ✕ header button is clicked', async () => {
    useChatPanelStore.getState().open();
    const user = userEvent.setup();
    render(<ChatSlideOverPanel />);
    await user.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(useChatPanelStore.getState().isOpen).toBe(false);
  });

  it('closes on Esc keypress', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true });
    window.dispatchEvent(event);
    expect(useChatPanelStore.getState().isOpen).toBe(false);
  });
});
