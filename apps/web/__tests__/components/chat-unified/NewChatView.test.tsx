/**
 * NewChatView Component Tests
 *
 * Covers:
 * - Private games tab renders by default
 * - Shared tab lazy-loads and filters by hasKb
 * - ?gameId= activates direct mode (same as ?game=)
 * - Empty states for both tabs
 * - Tab switching preserves selection
 * - Agent selection + thread creation
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { CHAT_TEST_IDS } from '@/lib/test-ids';
import { api } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/lib/api');

const mockPush = vi.fn();
const mockReplace = vi.fn();
let mockSearchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: mockReplace,
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn(),
  }),
  useSearchParams: () => mockSearchParams,
}));

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Stub window.matchMedia for responsive components
vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockPrivateGamesResponse = {
  items: [
    {
      id: 'pg-1',
      ownerId: 'user-1',
      source: 'Manual' as const,
      title: 'My Private Game',
      minPlayers: 2,
      maxPlayers: 4,
      createdAt: '2026-01-01T00:00:00Z',
    },
    {
      id: 'pg-2',
      ownerId: 'user-1',
      source: 'Manual' as const,
      title: 'Another Private Game',
      minPlayers: 1,
      maxPlayers: 6,
      createdAt: '2026-01-02T00:00:00Z',
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
      userId: 'user-1',
      gameId: 'shared-1',
      gameTitle: 'Shared Game With KB',
      addedAt: '2026-01-01T00:00:00Z',
      isFavorite: false,
      currentState: 'Owned' as const,
      hasKb: true,
      kbCardCount: 1,
      kbIndexedCount: 1,
      kbProcessingCount: 0,
      agentIsOwned: true,
    },
    {
      id: 'lib-2',
      userId: 'user-1',
      gameId: 'shared-2',
      gameTitle: 'Another KB Game',
      addedAt: '2026-01-02T00:00:00Z',
      isFavorite: false,
      currentState: 'Owned' as const,
      hasKb: true,
      kbCardCount: 2,
      kbIndexedCount: 2,
      kbProcessingCount: 0,
      agentIsOwned: true,
    },
    {
      id: 'lib-3',
      userId: 'user-1',
      gameId: 'shared-3',
      gameTitle: 'No KB Game',
      addedAt: '2026-01-03T00:00:00Z',
      isFavorite: false,
      currentState: 'Owned' as const,
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
  { id: 'agent-tutor', name: 'Tutor', type: 'qa', strategyName: 'rag', strategyParameters: {}, isActive: true, createdAt: '2026-01-01T00:00:00Z', lastInvokedAt: null, invocationCount: 0, isRecentlyUsed: false, isIdle: true },
  { id: 'agent-arbitro', name: 'Arbitro', type: 'rules', strategyName: 'rag', strategyParameters: {}, isActive: true, createdAt: '2026-01-01T00:00:00Z', lastInvokedAt: null, invocationCount: 0, isRecentlyUsed: false, isIdle: true },
];

const mockCustomAgents = [
  { id: 'custom-1', name: 'My RAG Agent', type: 'RAG', strategyName: 'rag', strategyParameters: {}, isActive: true, createdAt: '2026-01-01T00:00:00Z', lastInvokedAt: null, invocationCount: 0, isRecentlyUsed: false, isIdle: false, gameId: 'pg-1', createdByUserId: 'user-1' },
  { id: 'custom-2', name: 'Expert Agent', type: 'RAG', strategyName: 'rag', strategyParameters: {}, isActive: true, createdAt: '2026-01-01T00:00:00Z', lastInvokedAt: null, invocationCount: 0, isRecentlyUsed: false, isIdle: false, gameId: 'pg-1', createdByUserId: 'user-1' },
];

const mockThread = {
  id: 'thread-new-1',
  gameId: 'pg-1',
  agentId: null,
  agentType: null,
  title: 'Nuova conversazione',
  createdAt: '2026-01-01T00:00:00Z',
  lastMessageAt: null,
  messageCount: 0,
  messages: [],
};

// ============================================================================
// Helpers
// ============================================================================

async function renderNewChatView(params?: URLSearchParams) {
  if (params) mockSearchParams = params;

  // Dynamic import to pick up mocks
  const { NewChatView } = await import('@/components/chat-unified/NewChatView');
  return render(<NewChatView />);
}

// ============================================================================
// Tests
// ============================================================================

describe('NewChatView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSearchParams = new URLSearchParams();

    // Default mocks — private games load on mount
    vi.mocked(api.library.getPrivateGames).mockResolvedValue(mockPrivateGamesResponse);
    vi.mocked(api.library.getLibrary).mockResolvedValue(mockLibraryResponse);
    vi.mocked(api.agents.getAvailable).mockResolvedValue(mockAgents);
    vi.mocked(api.agents.getUserAgentsForGame).mockResolvedValue([]);
    vi.mocked(api.chat.createThread).mockResolvedValue(mockThread);
  });

  // ---------- Tab rendering ----------

  describe('Game source tabs', () => {
    it('renders private games tab as default', async () => {
      await renderNewChatView();

      await waitFor(() => {
        expect(screen.getByTestId(CHAT_TEST_IDS.tabPrivateGames)).toBeInTheDocument();
        expect(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames)).toBeInTheDocument();
      });

      // Private tab is selected
      expect(screen.getByTestId(CHAT_TEST_IDS.tabPrivateGames)).toHaveAttribute('aria-selected', 'true');
      expect(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames)).toHaveAttribute('aria-selected', 'false');
    });

    it('shows private games on mount', async () => {
      await renderNewChatView();

      await waitFor(() => {
        expect(screen.getByText('My Private Game')).toBeInTheDocument();
        expect(screen.getByText('Another Private Game')).toBeInTheDocument();
      });

      // Shared games API not called yet
      expect(api.library.getLibrary).not.toHaveBeenCalled();
    });

    it('lazy-loads shared games on tab switch and filters by hasKb', async () => {
      await renderNewChatView();

      // Wait for initial load
      await waitFor(() => {
        expect(screen.getByText('My Private Game')).toBeInTheDocument();
      });

      // Switch to shared tab
      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames));

      // Now getLibrary should be called
      await waitFor(() => {
        expect(api.library.getLibrary).toHaveBeenCalledWith({ pageSize: 100 });
      });

      // Only KB-ready games shown (2 out of 3)
      await waitFor(() => {
        expect(screen.getByText('Shared Game With KB')).toBeInTheDocument();
        expect(screen.getByText('Another KB Game')).toBeInTheDocument();
      });

      // "No KB Game" (hasKb: false) should NOT appear
      expect(screen.queryByText('No KB Game')).not.toBeInTheDocument();
    });

    it('does not re-fetch shared games on repeated tab switches', async () => {
      await renderNewChatView();

      await waitFor(() => {
        expect(screen.getByText('My Private Game')).toBeInTheDocument();
      });

      // Switch to shared
      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames));
      await waitFor(() => {
        expect(api.library.getLibrary).toHaveBeenCalledTimes(1);
      });

      // Switch back to private
      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.tabPrivateGames));

      // Switch to shared again
      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames));

      // Still only 1 call
      expect(api.library.getLibrary).toHaveBeenCalledTimes(1);
    });
  });

  // ---------- Empty states ----------

  describe('Empty states', () => {
    it('shows empty state when no private games', async () => {
      vi.mocked(api.library.getPrivateGames).mockResolvedValue({
        ...mockPrivateGamesResponse,
        items: [],
        totalCount: 0,
      });

      await renderNewChatView();

      await waitFor(() => {
        expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
      });
    });

    it('shows empty state when no shared games have KB', async () => {
      vi.mocked(api.library.getLibrary).mockResolvedValue({
        ...mockLibraryResponse,
        items: mockLibraryResponse.items.map(item => ({ ...item, hasKb: false })),
      });

      await renderNewChatView();

      // Wait for initial load, then switch to shared
      await waitFor(() => {
        expect(screen.getByTestId(CHAT_TEST_IDS.tabPrivateGames)).toBeInTheDocument();
      });

      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.tabSharedGames));

      await waitFor(() => {
        expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
      });
    });
  });

  // ---------- Direct game mode ----------

  describe('Direct game mode', () => {
    it('activates with ?gameId= query param', async () => {
      vi.mocked(api.agents.getUserAgentsForGame).mockResolvedValue(mockCustomAgents);

      await renderNewChatView(new URLSearchParams('gameId=pg-1'));

      // Direct mode shows agent picker, not game grid
      await waitFor(() => {
        expect(screen.getByTestId(CHAT_TEST_IDS.agentSelectionSection)).toBeInTheDocument();
      });

      // Game selection section should be hidden in direct mode
      expect(screen.queryByTestId(CHAT_TEST_IDS.gameSelectionSection)).not.toBeInTheDocument();
    });

    it('activates with ?game= query param (backward compat)', async () => {
      vi.mocked(api.agents.getUserAgentsForGame).mockResolvedValue(mockCustomAgents);

      await renderNewChatView(new URLSearchParams('game=pg-1'));

      await waitFor(() => {
        expect(screen.getByTestId(CHAT_TEST_IDS.agentSelectionSection)).toBeInTheDocument();
      });

      expect(screen.queryByTestId(CHAT_TEST_IDS.gameSelectionSection)).not.toBeInTheDocument();
    });

    it('redirects to agent creation when 0 custom agents', async () => {
      vi.mocked(api.agents.getUserAgentsForGame).mockResolvedValue([]);

      await renderNewChatView(new URLSearchParams('gameId=pg-1'));

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith('/chat/agents/create?gameId=pg-1');
      });
    });

    it('auto-creates thread when exactly 1 custom agent', async () => {
      const singleAgent = [mockCustomAgents[0]];
      vi.mocked(api.agents.getUserAgentsForGame).mockResolvedValue(singleAgent);

      await renderNewChatView(new URLSearchParams('gameId=pg-1'));

      await waitFor(() => {
        expect(api.chat.createThread).toHaveBeenCalledWith(
          expect.objectContaining({
            gameId: 'pg-1',
            agentId: 'custom-1',
          })
        );
      });

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith(`/chat/${mockThread.id}`);
      });
    });

    it('shows agent picker when 2+ custom agents', async () => {
      vi.mocked(api.agents.getUserAgentsForGame).mockResolvedValue(mockCustomAgents);

      await renderNewChatView(new URLSearchParams('gameId=pg-1'));

      // Should show agent selection, not redirect
      await waitFor(() => {
        expect(screen.getByTestId(CHAT_TEST_IDS.agentSelectionSection)).toBeInTheDocument();
      });

      expect(mockReplace).not.toHaveBeenCalled();
      expect(mockPush).not.toHaveBeenCalled();
    });
  });

  // ---------- Game search ----------

  describe('Game search', () => {
    it('filters games by search input', async () => {
      await renderNewChatView();

      await waitFor(() => {
        expect(screen.getByText('My Private Game')).toBeInTheDocument();
      });

      const searchInput = screen.getByTestId(CHAT_TEST_IDS.gameSearchInput);
      fireEvent.change(searchInput, { target: { value: 'Another' } });

      expect(screen.queryByText('My Private Game')).not.toBeInTheDocument();
      expect(screen.getByText('Another Private Game')).toBeInTheDocument();
    });
  });

  // ---------- Thread creation ----------

  describe('Thread creation', () => {
    it('creates thread with selected game and agent', async () => {
      await renderNewChatView();

      // Wait for games to load
      await waitFor(() => {
        expect(screen.getByText('My Private Game')).toBeInTheDocument();
      });

      // Select a game
      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.gameCard('pg-1')));

      // Auto agent type should already be selected
      // Click start chat
      const startBtn = screen.getByTestId(CHAT_TEST_IDS.startChatBtn);
      fireEvent.click(startBtn);

      await waitFor(() => {
        expect(api.chat.createThread).toHaveBeenCalledWith(
          expect.objectContaining({
            gameId: 'pg-1',
            title: 'Chat: My Private Game',
          })
        );
      });

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith(`/chat/${mockThread.id}`);
      });
    });

    it('creates thread without game (skip game)', async () => {
      await renderNewChatView();

      await waitFor(() => {
        expect(screen.getByTestId(CHAT_TEST_IDS.skipGameBtn)).toBeInTheDocument();
      });

      // Skip game selection
      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.skipGameBtn));

      // Click start
      const startBtn = screen.getByTestId(CHAT_TEST_IDS.startChatBtn);
      fireEvent.click(startBtn);

      await waitFor(() => {
        expect(api.chat.createThread).toHaveBeenCalledWith(
          expect.objectContaining({
            gameId: null,
            title: 'Nuova conversazione',
          })
        );
      });
    });

    it('shows error on thread creation failure', async () => {
      vi.mocked(api.chat.createThread).mockRejectedValue(new Error('Network error'));

      await renderNewChatView();

      await waitFor(() => {
        expect(screen.getByText('My Private Game')).toBeInTheDocument();
      });

      // Select game + click start
      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.gameCard('pg-1')));
      fireEvent.click(screen.getByTestId(CHAT_TEST_IDS.startChatBtn));

      await waitFor(() => {
        expect(screen.getByTestId(CHAT_TEST_IDS.newChatError)).toBeInTheDocument();
      });
    });
  });

  // ---------- Data loading ----------

  describe('Data loading', () => {
    it('loads private games and agents in parallel on mount', async () => {
      await renderNewChatView();

      await waitFor(() => {
        expect(api.library.getPrivateGames).toHaveBeenCalledWith({ pageSize: 100 });
        expect(api.agents.getAvailable).toHaveBeenCalled();
      });

      // getAll (old shared catalog) should NOT be called
      expect(api.games?.getAll).not.toHaveBeenCalled?.();
    });
  });
});
