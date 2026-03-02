/**
 * NewChatView Tests - Issue #4363
 *
 * Tests for the new chat page with game + agent selection:
 * 1. Full mode (no ?game param): game grid + agent grid + start
 * 2. Direct game mode (?gameId= or ?game=id):
 *    - 0 agents → redirects to agent creation
 *    - 1 agent  → auto-creates thread and redirects
 *    - 2+ agents → shows agent picker only (no game grid)
 * 3. Tabbed game source: private games (default) vs shared library (KB-ready)
 *
 * Pattern: Vitest + React Testing Library
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import React from 'react';

import { CHAT_TEST_IDS } from '@/lib/test-ids';
import { NewChatView } from '../NewChatView';

// Mock next/navigation
const mockPush = vi.fn();
const mockReplace = vi.fn();
let mockSearchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, replace: mockReplace }),
  useSearchParams: () => mockSearchParams,
}));

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getPrivateGames: vi.fn(),
      getLibrary: vi.fn(),
    },
    agents: {
      getAvailable: vi.fn(),
      getUserAgentsForGame: vi.fn(),
    },
    chat: {
      createThread: vi.fn(),
    },
  },
}));

// Mock MeepleCard
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({ title, entity, subtitle, badge, className }: any) => (
    <div data-testid="meeple-card" data-entity={entity} className={className}>
      <span>{title}</span>
      {subtitle && <span>{subtitle}</span>}
      {badge && <span>{badge}</span>}
    </div>
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockPrivateGamesResponse = {
  items: [
    {
      id: 'priv-1',
      title: 'My Custom Game',
      createdAt: '2024-06-01T00:00:00Z',
      ownerId: 'u1',
      source: 'Manual',
      minPlayers: 2,
      maxPlayers: 4,
    },
    {
      id: 'priv-2',
      title: 'Another Private',
      createdAt: '2024-06-02T00:00:00Z',
      ownerId: 'u1',
      source: 'Manual',
      minPlayers: 1,
      maxPlayers: 6,
    },
  ],
  page: 1,
  pageSize: 100,
  totalCount: 2,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

const mockLibraryResponse = {
  items: [
    {
      id: 'lib-1',
      userId: 'u1',
      gameId: 'game-1',
      gameTitle: 'Catan',
      addedAt: '2024-01-01T00:00:00Z',
      isFavorite: false,
      currentState: 'Owned',
      hasKb: true,
      kbCardCount: 1,
      kbIndexedCount: 1,
      kbProcessingCount: 0,
      agentIsOwned: true,
    },
    {
      id: 'lib-2',
      userId: 'u1',
      gameId: 'game-2',
      gameTitle: 'Ticket to Ride',
      addedAt: '2024-01-02T00:00:00Z',
      isFavorite: false,
      currentState: 'Owned',
      hasKb: true,
      kbCardCount: 2,
      kbIndexedCount: 2,
      kbProcessingCount: 0,
      agentIsOwned: true,
    },
    {
      id: 'lib-3',
      userId: 'u1',
      gameId: 'game-3',
      gameTitle: 'Wingspan',
      addedAt: '2024-01-03T00:00:00Z',
      isFavorite: false,
      currentState: 'Owned',
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      agentIsOwned: false,
    },
  ],
  page: 1,
  pageSize: 100,
  totalCount: 3,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

const mockAgents = [
  {
    id: 'agent-1',
    name: 'QA Agent',
    type: 'qa',
    strategyName: 'qa',
    strategyParameters: {},
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    lastInvokedAt: null,
    invocationCount: 0,
    isRecentlyUsed: false,
    isIdle: true,
  },
  {
    id: 'agent-2',
    name: 'Rules Agent',
    type: 'rules',
    strategyName: 'rules',
    strategyParameters: {},
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    lastInvokedAt: null,
    invocationCount: 0,
    isRecentlyUsed: false,
    isIdle: true,
  },
];

const mockCustomAgents = [
  {
    id: 'custom-1',
    name: 'My Catan Expert',
    type: 'custom',
    strategyName: 'default',
    strategyParameters: {},
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    lastInvokedAt: null,
    invocationCount: 0,
    isRecentlyUsed: false,
    isIdle: false,
  },
];

const mockMultipleCustomAgents = [
  {
    id: 'custom-1',
    name: 'My Catan Expert',
    type: 'custom',
    strategyName: 'default',
    strategyParameters: {},
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    lastInvokedAt: null,
    invocationCount: 0,
    isRecentlyUsed: false,
    isIdle: false,
  },
  {
    id: 'custom-2',
    name: 'Strategy Pro',
    type: 'strategy',
    strategyName: 'default',
    strategyParameters: {},
    isActive: true,
    createdAt: '2024-01-01T00:00:00Z',
    lastInvokedAt: null,
    invocationCount: 0,
    isRecentlyUsed: false,
    isIdle: false,
  },
];

// ============================================================================
// Helpers
// ============================================================================

let apiMock: any;

async function renderFullMode() {
  mockSearchParams = new URLSearchParams();
  const result = render(<NewChatView />);
  await waitFor(() => {
    expect(screen.queryByTestId(CHAT_TEST_IDS.gameSelectionSection)).toBeInTheDocument();
  });
  return result;
}

// ============================================================================
// Tests: Full Mode (no ?game param)
// ============================================================================

describe('NewChatView — Full Mode', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    mockSearchParams = new URLSearchParams();

    const { api } = await import('@/lib/api');
    apiMock = api;

    (apiMock.library.getPrivateGames as Mock).mockResolvedValue(mockPrivateGamesResponse);
    (apiMock.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);
    (apiMock.agents.getAvailable as Mock).mockResolvedValue(mockAgents);
    (apiMock.agents.getUserAgentsForGame as Mock).mockResolvedValue([]);
    (apiMock.chat.createThread as Mock).mockResolvedValue({ id: 'thread-new-1' });
  });

  it('renders game selection section', async () => {
    await renderFullMode();
    expect(screen.getByTestId(CHAT_TEST_IDS.gameSelectionSection)).toBeInTheDocument();
    expect(screen.getByText('Seleziona un gioco')).toBeInTheDocument();
  });

  it('renders agent selection with 4 options', async () => {
    await renderFullMode();
    expect(screen.getByTestId(CHAT_TEST_IDS.agentSelectionSection)).toBeInTheDocument();
    expect(screen.getByTestId(CHAT_TEST_IDS.agentCard('auto'))).toBeInTheDocument();
    expect(screen.getByTestId(CHAT_TEST_IDS.agentCard('qa'))).toBeInTheDocument();
    expect(screen.getByTestId(CHAT_TEST_IDS.agentCard('rules'))).toBeInTheDocument();
    expect(screen.getByTestId(CHAT_TEST_IDS.agentCard('strategy'))).toBeInTheDocument();
  });

  it('renders quick start section', async () => {
    await renderFullMode();
    expect(screen.getByTestId(CHAT_TEST_IDS.quickStartSection)).toBeInTheDocument();
  });

  it('renders start chat button', async () => {
    await renderFullMode();
    expect(screen.getByTestId(CHAT_TEST_IDS.startChatBtn)).toBeInTheDocument();
    expect(screen.getByText('Inizia Chat')).toBeInTheDocument();
  });

  it('renders private game cards by default', async () => {
    await renderFullMode();

    await waitFor(() => {
      expect(screen.getByText('My Custom Game')).toBeInTheDocument();
      expect(screen.getByText('Another Private')).toBeInTheDocument();
    });
  });

  it('shows game source tabs', async () => {
    await renderFullMode();
    expect(screen.getByTestId(CHAT_TEST_IDS.gameSourceTabs)).toBeInTheDocument();
    expect(screen.getByTestId(CHAT_TEST_IDS.tabPrivateGames)).toBeInTheDocument();
    expect(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames)).toBeInTheDocument();
  });

  it('has private tab selected by default', async () => {
    await renderFullMode();
    expect(screen.getByTestId(CHAT_TEST_IDS.tabPrivateGames)).toHaveAttribute(
      'aria-selected',
      'true'
    );
    expect(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames)).toHaveAttribute(
      'aria-selected',
      'false'
    );
  });

  it('lazy-loads shared games on tab switch and filters by hasKb', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    // Shared games not loaded yet
    expect(apiMock.library.getLibrary).not.toHaveBeenCalled();

    await user.click(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames));

    await waitFor(() => {
      expect(apiMock.library.getLibrary).toHaveBeenCalledWith({ pageSize: 100 });
    });

    // Only KB-ready games shown (Catan + Ticket to Ride, NOT Wingspan)
    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
      expect(screen.queryByText('Wingspan')).not.toBeInTheDocument();
    });
  });

  it('selects a game card on click', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    await waitFor(() => {
      expect(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1'))).toBeInTheDocument();
    });

    await user.click(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1')));
    expect(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1'))).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  it('filters games with search input', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    await waitFor(() => {
      expect(screen.getByText('My Custom Game')).toBeInTheDocument();
    });

    const searchInput = screen.getByTestId(CHAT_TEST_IDS.gameSearchInput);
    await user.type(searchInput, 'Custom');

    expect(screen.getByText('My Custom Game')).toBeInTheDocument();
    expect(screen.queryByText('Another Private')).not.toBeInTheDocument();
  });

  it('allows skipping game selection', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    await waitFor(() => {
      expect(screen.getByTestId(CHAT_TEST_IDS.skipGameBtn)).toBeInTheDocument();
    });

    await user.click(screen.getByTestId(CHAT_TEST_IDS.skipGameBtn));
    expect(screen.getByText('Continua senza gioco (chat generica)')).toBeInTheDocument();
  });

  it('selects an agent card on click', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    await user.click(screen.getByTestId(CHAT_TEST_IDS.agentCard('rules')));
    expect(screen.getByTestId(CHAT_TEST_IDS.agentCard('rules'))).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  it('has auto agent pre-selected by default', async () => {
    await renderFullMode();
    expect(screen.getByTestId(CHAT_TEST_IDS.agentCard('auto'))).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  it('shows contextual suggestions when game selected', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    await waitFor(() => {
      expect(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1'))).toBeInTheDocument();
    });

    expect(screen.getByText('Consiglia un gioco')).toBeInTheDocument();

    await user.click(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1')));

    await waitFor(() => {
      expect(screen.getByText('Come si gioca a My Custom Game')).toBeInTheDocument();
    });
  });

  it('creates thread and redirects on start', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    await waitFor(() => {
      expect(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1'))).toBeInTheDocument();
    });

    await user.click(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1')));
    await user.click(screen.getByTestId(CHAT_TEST_IDS.startChatBtn));

    await waitFor(() => {
      expect(apiMock.chat.createThread).toHaveBeenCalledWith({
        gameId: 'priv-1',
        agentId: null,
        title: 'Chat: My Custom Game',
        initialMessage: null,
      });
      expect(mockPush).toHaveBeenCalledWith('/chat/thread-new-1');
    });
  });

  it('creates generic thread when no game selected', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    await user.click(screen.getByTestId(CHAT_TEST_IDS.startChatBtn));

    await waitFor(() => {
      expect(apiMock.chat.createThread).toHaveBeenCalledWith({
        gameId: null,
        agentId: null,
        title: 'Nuova conversazione',
        initialMessage: null,
      });
    });
  });

  it('shows error when API fails', async () => {
    (apiMock.library.getPrivateGames as Mock).mockRejectedValue(new Error('Network'));
    render(<NewChatView />);

    await waitFor(() => {
      expect(screen.getByTestId(CHAT_TEST_IDS.newChatError)).toBeInTheDocument();
      expect(screen.getByText('Errore nel caricamento dei dati')).toBeInTheDocument();
    });
  });

  it('shows error when thread creation fails', async () => {
    const user = userEvent.setup();
    (apiMock.chat.createThread as Mock).mockRejectedValue(new Error('Failed'));
    await renderFullMode();

    await user.click(screen.getByTestId('start-chat-btn'));

    await waitFor(() => {
      expect(screen.getByText('Errore nella creazione della conversazione')).toBeInTheDocument();
    });
  });

  it('shows empty state for private games tab', async () => {
    (apiMock.library.getPrivateGames as Mock).mockResolvedValue({
      ...mockPrivateGamesResponse,
      items: [],
      totalCount: 0,
    });
    await renderFullMode();

    await waitFor(() => {
      expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
    });
  });

  it('shows empty state for shared games tab when no KB-ready games', async () => {
    const user = userEvent.setup();
    (apiMock.library.getLibrary as Mock).mockResolvedValue({
      ...mockLibraryResponse,
      items: mockLibraryResponse.items.map(e => ({ ...e, hasKb: false })),
    });
    await renderFullMode();

    await user.click(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames));

    await waitFor(() => {
      expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
    });
  });

  it('preserves selection when switching tabs', async () => {
    const user = userEvent.setup();
    await renderFullMode();

    await waitFor(() => {
      expect(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1'))).toBeInTheDocument();
    });

    // Select a private game
    await user.click(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1')));
    expect(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1'))).toHaveAttribute(
      'aria-pressed',
      'true'
    );

    // Switch to shared tab
    await user.click(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames));

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    // Switch back to private tab — selection still there
    await user.click(screen.getByTestId(CHAT_TEST_IDS.tabPrivateGames));

    await waitFor(() => {
      expect(screen.getByTestId(CHAT_TEST_IDS.gameCard('priv-1'))).toHaveAttribute(
        'aria-pressed',
        'true'
      );
    });
  });
});

// ============================================================================
// Tests: Direct Game Mode (?game=id and ?gameId=id)
// ============================================================================

describe('NewChatView — Direct Game Mode', () => {
  beforeEach(async () => {
    vi.clearAllMocks();

    const { api } = await import('@/lib/api');
    apiMock = api;

    (apiMock.library.getPrivateGames as Mock).mockResolvedValue(mockPrivateGamesResponse);
    (apiMock.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);
    (apiMock.agents.getAvailable as Mock).mockResolvedValue(mockAgents);
    (apiMock.chat.createThread as Mock).mockResolvedValue({ id: 'thread-auto-1' });
  });

  it('redirects to agent creation when 0 custom agents', async () => {
    mockSearchParams = new URLSearchParams('game=priv-1');
    (apiMock.agents.getUserAgentsForGame as Mock).mockResolvedValue([]);

    render(<NewChatView />);

    await waitFor(() => {
      expect(mockReplace).toHaveBeenCalledWith('/chat/agents/create?gameId=priv-1');
    });
  });

  it('auto-creates thread when exactly 1 custom agent', async () => {
    mockSearchParams = new URLSearchParams('game=priv-1');
    (apiMock.agents.getUserAgentsForGame as Mock).mockResolvedValue(mockCustomAgents);

    render(<NewChatView />);

    await waitFor(() => {
      expect(apiMock.chat.createThread).toHaveBeenCalledWith({
        gameId: 'priv-1',
        agentId: 'custom-1',
        title: 'Chat: My Custom Game',
        initialMessage: null,
      });
      expect(mockPush).toHaveBeenCalledWith('/chat/thread-auto-1');
    });
  });

  it('shows agent selection without game grid when 2+ custom agents', async () => {
    mockSearchParams = new URLSearchParams('game=priv-1');
    (apiMock.agents.getUserAgentsForGame as Mock).mockResolvedValue(mockMultipleCustomAgents);

    render(<NewChatView />);

    await waitFor(() => {
      expect(screen.getByTestId(CHAT_TEST_IDS.agentSelectionSection)).toBeInTheDocument();
    });

    // Game grid should NOT be visible
    expect(screen.queryByTestId(CHAT_TEST_IDS.gameSelectionSection)).not.toBeInTheDocument();

    // Custom agents should be shown
    expect(screen.getByText('My Catan Expert')).toBeInTheDocument();
    expect(screen.getByText('Strategy Pro')).toBeInTheDocument();

    // System agents still visible
    expect(screen.getByTestId(CHAT_TEST_IDS.agentCard('auto'))).toBeInTheDocument();
  });

  it('shows contextual header with game name when 2+ agents', async () => {
    mockSearchParams = new URLSearchParams('game=priv-1');
    (apiMock.agents.getUserAgentsForGame as Mock).mockResolvedValue(mockMultipleCustomAgents);

    render(<NewChatView />);

    await waitFor(() => {
      expect(screen.getByText(/Scegli un agente per/)).toBeInTheDocument();
      expect(screen.getByText('My Custom Game')).toBeInTheDocument();
    });
  });

  it('shows loading spinner during auto-start', async () => {
    mockSearchParams = new URLSearchParams('game=priv-1');
    // Delay the agent response to observe loading state
    (apiMock.agents.getUserAgentsForGame as Mock).mockImplementation(
      () => new Promise(resolve => setTimeout(() => resolve(mockCustomAgents), 100))
    );

    render(<NewChatView />);

    expect(screen.getByText('Preparazione...')).toBeInTheDocument();
  });

  it('activates direct mode with ?gameId= param (same as ?game=)', async () => {
    mockSearchParams = new URLSearchParams('gameId=priv-1');
    (apiMock.agents.getUserAgentsForGame as Mock).mockResolvedValue([]);

    render(<NewChatView />);

    await waitFor(() => {
      expect(mockReplace).toHaveBeenCalledWith('/chat/agents/create?gameId=priv-1');
    });
  });

  it('prefers ?gameId= over ?game= when both present', async () => {
    mockSearchParams = new URLSearchParams('gameId=priv-1&game=priv-2');
    (apiMock.agents.getUserAgentsForGame as Mock).mockResolvedValue([]);

    render(<NewChatView />);

    await waitFor(() => {
      expect(mockReplace).toHaveBeenCalledWith('/chat/agents/create?gameId=priv-1');
    });
  });
});
