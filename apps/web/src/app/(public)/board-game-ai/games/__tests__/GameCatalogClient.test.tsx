/**
 * Tests for GameCatalogClient component
 * Issue #1017: BGAI-078 Game catalog page
 *
 * Coverage:
 * - Rendering and UI structure
 * - Search functionality (debounce, clear)
 * - View toggle (grid/list)
 * - Pagination controls
 * - Loading and error states
 * - Game card interactions
 * - Empty state
 */

import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { GameCatalogClient } from '../GameCatalogClient';

// Mock Next.js router
const mockPush = vi.fn();
const mockPathname = '/board-game-ai/games';
const mockSearchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
  usePathname: () => mockPathname,
  useSearchParams: () => mockSearchParams,
}));

// Mock api
const mockGetAll = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getAll: (...args: unknown[]) => mockGetAll(...args),
    },
  },
}));

// Mock logger
vi.mock('@/lib/logger', () => ({
  logger: {
    error: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    debug: vi.fn(),
  },
}));

vi.mock('@/lib/errors', () => ({
  createErrorContext: vi.fn(() => ({})),
}));

// Mock MeepleGameCard to simplify tests (component uses MeepleGameCard, not GameCard)
vi.mock('@/components/games/MeepleGameCard', () => ({
  MeepleGameCard: ({
    game,
    onClick,
    variant,
  }: {
    game: { id: string; title: string };
    onClick?: (id: string) => void;
    variant?: string;
  }) => (
    <div
      data-testid={`game-card-${game.id}`}
      data-variant={variant}
      onClick={() => onClick?.(game.id)}
      role="button"
    >
      {game.title}
    </div>
  ),
}));

// Mock data
const mockGames = [
  {
    id: 'game-1',
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    bggId: 13,
    createdAt: '2024-01-01T00:00:00Z',
    imageUrl: null,
    faqCount: 15,
    averageRating: 7.2,
  },
  {
    id: 'game-2',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    yearPublished: 2019,
    minPlayers: 1,
    maxPlayers: 5,
    minPlayTimeMinutes: 40,
    maxPlayTimeMinutes: 70,
    bggId: 266192,
    createdAt: '2024-01-02T00:00:00Z',
    imageUrl: null,
    faqCount: 8,
    averageRating: 8.1,
  },
  {
    id: 'game-3',
    title: 'Azul',
    publisher: 'Plan B Games',
    yearPublished: 2017,
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 45,
    bggId: 230802,
    createdAt: '2024-01-03T00:00:00Z',
    imageUrl: null,
    faqCount: 12,
    averageRating: 7.9,
  },
];

const mockPaginatedResponse = {
  games: mockGames,
  total: 3,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

const mockPaginatedResponseMultiPage = {
  games: mockGames,
  total: 45,
  page: 1,
  pageSize: 20,
  totalPages: 3,
};

describe('GameCatalogClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAll.mockResolvedValue(mockPaginatedResponse);
    mockSearchParams.delete('search');
    mockSearchParams.delete('view');
    mockSearchParams.delete('page');
  });

  // ============================================================================
  // GROUP 1: RENDERING (3 tests)
  // ============================================================================

  describe('Rendering', () => {
    it('should render search bar, view toggle, and game cards', async () => {
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/cerca giochi/i)).toBeInTheDocument();
        expect(
          screen.getByRole('group', { name: /modalità visualizzazione/i })
        ).toBeInTheDocument();
        expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      });
    });

    it('should display game count after loading', async () => {
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByText(/3 giochi trovati/i)).toBeInTheDocument();
      });
    });

    it('should render in grid variant by default', async () => {
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        const gameCard = screen.getByTestId('game-card-game-1');
        expect(gameCard).toHaveAttribute('data-variant', 'grid');
      });
    });
  });

  // ============================================================================
  // GROUP 2: SEARCH FUNCTIONALITY (4 tests)
  // ============================================================================

  describe('Search Functionality', () => {
    it('should update search input value on typing', async () => {
      const user = userEvent.setup();
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(/cerca giochi/i);
      await user.type(searchInput, 'Catan');

      expect(searchInput).toHaveValue('Catan');
    });

    it('should trigger API call with search param after debounce', async () => {
      const user = userEvent.setup();

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      // Wait for initial load
      await waitFor(() => {
        expect(mockGetAll).toHaveBeenCalledTimes(1);
      });

      const searchInput = screen.getByPlaceholderText(/cerca giochi/i);
      await user.type(searchInput, 'Catan');

      // Wait for debounce to complete and API call to be made
      await waitFor(
        () => {
          expect(mockGetAll).toHaveBeenCalledWith({ search: 'Catan' }, expect.any(Object), 1, 20);
        },
        { timeout: 1000 }
      );
    });

    it('should show clear button when search has value', async () => {
      const user = userEvent.setup();
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText(/cerca giochi/i);
      await user.type(searchInput, 'Test');

      expect(screen.getByRole('button', { name: /cancella ricerca/i })).toBeInTheDocument();
    });

    it('should clear search when clear button clicked', async () => {
      const user = userEvent.setup();
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="Initial" initialPage={1} />);

      const clearButton = await screen.findByRole('button', { name: /cancella ricerca/i });
      await user.click(clearButton);

      const searchInput = screen.getByPlaceholderText(/cerca giochi/i);
      expect(searchInput).toHaveValue('');
    });
  });

  // ============================================================================
  // GROUP 3: VIEW TOGGLE (3 tests)
  // ============================================================================

  describe('View Toggle', () => {
    it('should render grid view toggle as selected by default', async () => {
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        const gridButton = screen.getByRole('radio', { name: /vista griglia/i });
        expect(gridButton).toHaveAttribute('data-state', 'on');
      });
    });

    it('should switch to list view when list toggle clicked', async () => {
      const user = userEvent.setup();
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      });

      const listButton = screen.getByRole('radio', { name: /vista lista/i });
      await user.click(listButton);

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith(
          expect.stringContaining('view=list'),
          expect.any(Object)
        );
      });
    });

    it('should render in list variant when initialView is list', async () => {
      renderWithQuery(<GameCatalogClient initialView="list" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        const gameCard = screen.getByTestId('game-card-game-1');
        expect(gameCard).toHaveAttribute('data-variant', 'list');
      });
    });
  });

  // ============================================================================
  // GROUP 4: LOADING AND ERROR STATES (4 tests)
  // ============================================================================

  describe('Loading and Error States', () => {
    it('should show skeleton loading state initially', async () => {
      // Create a promise that won't resolve until we tell it to
      let resolvePromise: (value: typeof mockPaginatedResponse) => void;
      const delayedPromise = new Promise<typeof mockPaginatedResponse>(resolve => {
        resolvePromise = resolve;
      });
      mockGetAll.mockReturnValue(delayedPromise);

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      // Should show skeletons during loading - use a more specific selector
      const container = document.querySelector('.space-y-6');
      expect(container).toBeInTheDocument();

      // Check for loading state by looking for skeleton elements or the loading grid
      const loadingGrid = document.querySelector('.grid');
      expect(loadingGrid).toBeInTheDocument();

      // Resolve the promise to avoid test cleanup issues
      resolvePromise!(mockPaginatedResponse);
    });

    it('should display error message when API fails', async () => {
      mockGetAll.mockRejectedValue(new Error('Network error'));

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByText(/impossibile caricare i giochi/i)).toBeInTheDocument();
      });
    });

    it('should show retry button on error', async () => {
      mockGetAll.mockRejectedValue(new Error('Network error'));

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
      });
    });

    it('should retry API call when retry button clicked', async () => {
      mockGetAll
        .mockRejectedValueOnce(new Error('Network error'))
        .mockResolvedValueOnce(mockPaginatedResponse);

      const user = userEvent.setup();
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /riprova/i })).toBeInTheDocument();
      });

      const retryButton = screen.getByRole('button', { name: /riprova/i });
      await user.click(retryButton);

      await waitFor(() => {
        expect(mockGetAll).toHaveBeenCalledTimes(2);
        expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // GROUP 5: EMPTY STATE (2 tests)
  // ============================================================================

  describe('Empty State', () => {
    it('should show empty state when no games found', async () => {
      mockGetAll.mockResolvedValue({
        games: [],
        total: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      });

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        // Look for the empty state heading specifically (not the results count)
        const emptyStateHeading = screen.getByRole('heading', { name: /nessun gioco trovato/i });
        expect(emptyStateHeading).toBeInTheDocument();
      });
    });

    it('should show clear search button in empty state when search active', async () => {
      mockGetAll.mockResolvedValue({
        games: [],
        total: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      });

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="NonExistent" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancella ricerca/i })).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // GROUP 6: PAGINATION (4 tests)
  // ============================================================================

  describe('Pagination', () => {
    it('should show pagination when multiple pages exist', async () => {
      mockGetAll.mockResolvedValue(mockPaginatedResponseMultiPage);

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByText(/pagina 1 di 3/i)).toBeInTheDocument();
      });
    });

    it('should navigate to next page when next button clicked', async () => {
      mockGetAll.mockResolvedValue(mockPaginatedResponseMultiPage);

      const user = userEvent.setup();
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /pagina successiva/i })).toBeInTheDocument();
      });

      const nextButton = screen.getByRole('button', { name: /pagina successiva/i });
      await user.click(nextButton);

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith(
          expect.stringContaining('page=2'),
          expect.any(Object)
        );
      });
    });

    it('should disable previous button on first page', async () => {
      mockGetAll.mockResolvedValue(mockPaginatedResponseMultiPage);

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        const prevButton = screen.getByRole('button', { name: /pagina precedente/i });
        expect(prevButton).toBeDisabled();
      });
    });

    it('should not show pagination when only one page', async () => {
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      });

      expect(screen.queryByText(/pagina 1 di/i)).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // GROUP 7: GAME CARD INTERACTIONS (2 tests)
  // ============================================================================

  describe('Game Card Interactions', () => {
    it('should navigate to ask page when game card clicked', async () => {
      const user = userEvent.setup();
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      });

      const gameCard = screen.getByTestId('game-card-game-1');
      await user.click(gameCard);

      expect(mockPush).toHaveBeenCalledWith('/board-game-ai/ask?gameId=game-1');
    });

    it('should render all game cards from response', async () => {
      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={1} />);

      await waitFor(() => {
        expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      });

      // All 3 games from mockGames should be rendered
      expect(screen.getByTestId('game-card-game-2')).toBeInTheDocument();
      expect(screen.getByTestId('game-card-game-3')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // GROUP 8: INITIAL PROPS (2 tests)
  // ============================================================================

  describe('Initial Props', () => {
    it('should initialize with provided search value', async () => {
      mockGetAll.mockResolvedValue(mockPaginatedResponse);

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="Catan" initialPage={1} />);

      const searchInput = screen.getByPlaceholderText(/cerca giochi/i);
      expect(searchInput).toHaveValue('Catan');
    });

    it('should initialize with provided page', async () => {
      mockGetAll.mockResolvedValue(mockPaginatedResponseMultiPage);

      renderWithQuery(<GameCatalogClient initialView="grid" initialSearch="" initialPage={2} />);

      await waitFor(() => {
        expect(mockGetAll).toHaveBeenCalledWith(undefined, expect.any(Object), 2, 20);
      });
    });
  });
});
