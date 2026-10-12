import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { useChatPanelStore } from '@/lib/stores/chat-panel-store';

// Mock the data hooks so the panel renders synchronously with empty lists
// #4187: spie sugli argomenti, per verificare che a pannello chiuso le query non partano.
const hookArgs = vi.hoisted(() => ({
  recent: [] as unknown[][],
  library: [] as unknown[][],
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

// #4187: la tendina del contesto offre la libreria dell'utente, cercata sul server.
const LIBRARY_ITEMS = [
  {
    gameId: 'wingspan',
    gameTitle: 'Wingspan',
    gameYearPublished: 2019,
    gameImageUrl: null,
    gameIconUrl: null,
    hasKb: true,
  },
  {
    gameId: 'senza-kb',
    gameTitle: 'Gioco senza KB',
    gameYearPublished: null,
    gameImageUrl: null,
    gameIconUrl: null,
    hasKb: false,
  },
];
vi.mock('@/hooks/queries/useLibrarySearch', () => ({
  useLibrarySearch: (...args: unknown[]) => (
    hookArgs.library.push(args),
    {
      items: LIBRARY_ITEMS,
      totalCount: LIBRARY_ITEMS.length,
      isLoading: false,
      resultsMatchQuery: true,
      term: String(args[0] ?? ''),
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
    hookArgs.library.length = 0;
    const { rerender } = render(<ChatSlideOverPanel />);
    expect(hookArgs.recent.at(-1)?.[1]).toBe(false);
    expect((hookArgs.library.at(-1)?.[1] as { enabled?: boolean })?.enabled).toBe(false);

    act(() => useChatPanelStore.getState().open());
    rerender(<ChatSlideOverPanel />);
    expect(hookArgs.recent.at(-1)?.[1]).toBe(true);
    expect((hookArgs.library.at(-1)?.[1] as { enabled?: boolean })?.enabled).toBe(true);
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

  it('sceglie un gioco della libreria dalla tendina del contesto (#4187)', async () => {
    const user = userEvent.setup();
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    await user.click(screen.getByRole('button', { name: /seleziona gioco/i }));
    await user.click(screen.getByRole('option', { name: 'Wingspan' }));

    const context = useChatPanelStore.getState().gameContext;
    expect(context?.id).toBe('wingspan');
    expect(context?.name).toBe('Wingspan');
    expect(context?.year).toBe(2019);
  });

  it('la barra laterale «Giochi con KB» elenca solo i giochi della libreria con la KB pronta', () => {
    useChatPanelStore.getState().open();
    render(<ChatSlideOverPanel />);

    // La tendina e' chiusa: l'unico «Gioco senza KB» possibile sarebbe nella barra laterale.
    expect(screen.getByRole('button', { name: /wingspan/i })).toBeInTheDocument();
    expect(screen.queryByText('Gioco senza KB')).not.toBeInTheDocument();
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
