/**
 * Tests for PrivateGamesClient Component
 * Issue #4052: Private Games CRUD interface
 *
 * Coverage:
 * - Loading state
 * - Error state with retry
 * - Empty state (no games / no search results)
 * - Games grid rendering
 * - Search functionality
 * - Sort controls
 * - Pagination
 * - Add game dialog
 * - Edit game dialog
 * - Delete game dialog
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, Mock } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { api } from '@/lib/api';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({
    push: mockPush,
    back: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
    refresh: vi.fn(),
  })),
  usePathname: vi.fn(() => '/test'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));
import type { PrivateGameDto, PaginatedPrivateGamesResponse } from '@/lib/api/schemas/private-games.schemas';

import PrivateGamesClient from '../PrivateGamesClient';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getPrivateGames: vi.fn(),
      addPrivateGame: vi.fn(),
      updatePrivateGame: vi.fn(),
      deletePrivateGame: vi.fn(),
    },
  },
}));

// AddPrivateGameWithBgg mock removed — Add flow now navigates to /library/private/add

// Keep AddPrivateGameForm mock for edit dialog
vi.mock('@/components/library/AddPrivateGameForm', () => ({
  AddPrivateGameForm: () => <div data-testid="add-private-game-form" />,
}));

// Mock PrivateGameCard
vi.mock('@/components/library/PrivateGameCard', () => ({
  PrivateGameCard: ({
    game,
    onEdit,
    onDelete,
  }: {
    game: PrivateGameDto;
    onEdit?: (game: PrivateGameDto) => void;
    onDelete?: (gameId: string) => void;
  }) => (
    <div data-testid={`game-card-${game.id}`}>
      <span>{game.title}</span>
      {onEdit && (
        <button data-testid={`edit-btn-${game.id}`} onClick={() => onEdit(game)}>
          Edit
        </button>
      )}
      {onDelete && (
        <button data-testid={`delete-btn-${game.id}`} onClick={() => onDelete(game.id)}>
          Delete
        </button>
      )}
    </div>
  ),
}));

const mockGetPrivateGames = api.library.getPrivateGames as Mock;
const mockAddPrivateGame = api.library.addPrivateGame as Mock;
const mockUpdatePrivateGame = api.library.updatePrivateGame as Mock;
const mockDeletePrivateGame = api.library.deletePrivateGame as Mock;

// Test data factory
function createMockGame(overrides?: Partial<PrivateGameDto>): PrivateGameDto {
  const id = overrides?.id ?? 'game-1';
  return {
    id,
    userId: 'user-1',
    source: 'Manual',
    bggId: null,
    title: `Test Game ${id}`,
    minPlayers: 2,
    maxPlayers: 4,
    yearPublished: 2024,
    description: 'A test game',
    playingTimeMinutes: 60,
    minAge: 10,
    complexityRating: 2.5,
    imageUrl: null,
    thumbnailUrl: null,
    createdAt: '2026-01-15T10:00:00Z',
    updatedAt: '2026-01-15T10:00:00Z',
    ...overrides,
  };
}

function createPaginatedResponse(
  games: PrivateGameDto[],
  overrides?: Partial<PaginatedPrivateGamesResponse>
): PaginatedPrivateGamesResponse {
  return {
    items: games,
    page: 1,
    pageSize: 12,
    totalCount: games.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
    ...overrides,
  };
}

const mockGames = [
  createMockGame({ id: 'game-1', title: 'Catan' }),
  createMockGame({ id: 'game-2', title: 'Azul' }),
  createMockGame({ id: 'game-3', title: 'Wingspan' }),
];

describe('PrivateGamesClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('should show loading spinner on initial render', () => {
      mockGetPrivateGames.mockReturnValue(new Promise(() => {})); // Never resolves

      renderWithQuery(<PrivateGamesClient />);

      expect(screen.getByTestId('loading-state')).toBeInTheDocument();
      expect(screen.getByText('Loading games...')).toBeInTheDocument();
    });

    it('should show header and controls during loading', () => {
      mockGetPrivateGames.mockReturnValue(new Promise(() => {}));

      renderWithQuery(<PrivateGamesClient />);

      expect(screen.getByText('Private Games')).toBeInTheDocument();
      expect(screen.getByTestId('add-private-game-btn')).toBeInTheDocument();
      expect(screen.getByTestId('search-input')).toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('should show error message when API fails', async () => {
      mockGetPrivateGames.mockRejectedValueOnce(new Error('Network error'));

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('error-state')).toBeInTheDocument();
      });

      expect(
        screen.getByText('Unable to load private games. Please try again.')
      ).toBeInTheDocument();
    });

    it('should retry loading when clicking Retry button', async () => {
      mockGetPrivateGames
        .mockRejectedValueOnce(new Error('Network error'))
        .mockResolvedValueOnce(createPaginatedResponse(mockGames));

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('error-state')).toBeInTheDocument();
      });

      await user.click(screen.getByText('Refresh'));

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      expect(mockGetPrivateGames).toHaveBeenCalledTimes(2);
    });
  });

  describe('Empty State', () => {
    it('should show empty state when no games exist', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(createPaginatedResponse([]));

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('empty-state')).toBeInTheDocument();
      });

      expect(screen.getByText('No Private Games Yet')).toBeInTheDocument();
      expect(
        screen.getByText(
          'Start building your personal collection by adding your first private game.'
        )
      ).toBeInTheDocument();
      expect(screen.getByText('Add Your First Game')).toBeInTheDocument();
    });

    it('should show search empty state when search has no results', async () => {
      // First load with games
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      // All subsequent calls return empty (handleSearch fires per keystroke)
      mockGetPrivateGames.mockResolvedValue(createPaginatedResponse([]));
      await user.type(screen.getByTestId('search-input'), 'zzzzz');

      await waitFor(() => {
        expect(screen.getByText('No Games Found')).toBeInTheDocument();
      });
    });
  });

  describe('Games Grid', () => {
    it('should render game cards after loading', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames)
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      expect(screen.getByTestId('game-card-game-1')).toBeInTheDocument();
      expect(screen.getByTestId('game-card-game-2')).toBeInTheDocument();
      expect(screen.getByTestId('game-card-game-3')).toBeInTheDocument();
    });

    it('should display total game count in subtitle', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames, { totalCount: 3 })
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByText(/3 games/)).toBeInTheDocument();
      });
    });

    it('should show singular "game" for count of 1', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse([mockGames[0]], { totalCount: 1 })
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByText(/1 game\b/)).toBeInTheDocument();
      });
    });
  });

  describe('Search', () => {
    it('should call API with search parameter', async () => {
      mockGetPrivateGames.mockResolvedValue(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      await user.type(screen.getByTestId('search-input'), 'Catan');

      await waitFor(() => {
        const lastCall =
          mockGetPrivateGames.mock.calls[
            mockGetPrivateGames.mock.calls.length - 1
          ];
        expect(lastCall[0]).toEqual(
          expect.objectContaining({ search: expect.stringContaining('C') })
        );
      });
    });

    it('should reset to page 1 when searching', async () => {
      mockGetPrivateGames.mockResolvedValue(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      await user.type(screen.getByTestId('search-input'), 'a');

      await waitFor(() => {
        const lastCall =
          mockGetPrivateGames.mock.calls[
            mockGetPrivateGames.mock.calls.length - 1
          ];
        expect(lastCall[0]).toEqual(expect.objectContaining({ page: 1 }));
      });
    });

    it('should have search input with correct aria-label', () => {
      mockGetPrivateGames.mockReturnValue(new Promise(() => {}));

      renderWithQuery(<PrivateGamesClient />);

      expect(
        screen.getByLabelText('Search private games')
      ).toBeInTheDocument();
    });
  });

  describe('Sort Controls', () => {
    it('should toggle sort direction on button click', async () => {
      mockGetPrivateGames.mockResolvedValue(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      // Default is 'desc', click to toggle to 'asc'
      await user.click(screen.getByTestId('sort-direction-btn'));

      await waitFor(() => {
        const lastCall =
          mockGetPrivateGames.mock.calls[
            mockGetPrivateGames.mock.calls.length - 1
          ];
        expect(lastCall[0]).toEqual(
          expect.objectContaining({ sortDirection: 'asc' })
        );
      });
    });

    it('should have accessible sort direction button', async () => {
      mockGetPrivateGames.mockResolvedValue(
        createPaginatedResponse(mockGames)
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      // Default desc → button says "Sort ascending"
      expect(
        screen.getByLabelText('Sort ascending')
      ).toBeInTheDocument();
    });

    it('should have sort by select with correct aria-label', () => {
      mockGetPrivateGames.mockReturnValue(new Promise(() => {}));

      renderWithQuery(<PrivateGamesClient />);

      expect(screen.getByLabelText('Sort by')).toBeInTheDocument();
    });
  });

  describe('Pagination', () => {
    it('should show pagination when totalPages > 1', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames, {
          totalPages: 3,
          hasNextPage: true,
          hasPreviousPage: false,
          page: 1,
        })
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('pagination')).toBeInTheDocument();
      });

      expect(screen.getByText('Page 1 of 3')).toBeInTheDocument();
    });

    it('should not show pagination when totalPages is 1', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames, { totalPages: 1 })
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      expect(screen.queryByTestId('pagination')).not.toBeInTheDocument();
    });

    it('should disable Previous button on first page', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames, {
          totalPages: 3,
          hasNextPage: true,
          hasPreviousPage: false,
        })
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('pagination')).toBeInTheDocument();
      });

      expect(screen.getByLabelText('Previous page')).toBeDisabled();
      expect(screen.getByLabelText('Next page')).not.toBeDisabled();
    });

    it('should disable Next button on last page', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames, {
          totalPages: 3,
          hasNextPage: false,
          hasPreviousPage: true,
        })
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('pagination')).toBeInTheDocument();
      });

      expect(screen.getByLabelText('Next page')).toBeDisabled();
      expect(screen.getByLabelText('Previous page')).not.toBeDisabled();
    });

    it('should navigate to next page', async () => {
      mockGetPrivateGames.mockResolvedValue(
        createPaginatedResponse(mockGames, {
          totalPages: 3,
          hasNextPage: true,
          hasPreviousPage: false,
        })
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('pagination')).toBeInTheDocument();
      });

      await user.click(screen.getByLabelText('Next page'));

      await waitFor(() => {
        const lastCall =
          mockGetPrivateGames.mock.calls[
            mockGetPrivateGames.mock.calls.length - 1
          ];
        expect(lastCall[0]).toEqual(expect.objectContaining({ page: 2 }));
      });
    });
  });

  describe('Add Game Navigation', () => {
    it('should navigate to add page when clicking Add Game button', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('add-private-game-btn'));

      expect(mockPush).toHaveBeenCalledWith('/library/private/add');
    });

    it('should navigate to add page from empty state', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(createPaginatedResponse([]));

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('empty-state')).toBeInTheDocument();
      });

      await user.click(screen.getByText('Add Your First Game'));

      expect(mockPush).toHaveBeenCalledWith('/library/private/add');
    });
  });

  describe('Edit Game Dialog', () => {
    it('should open edit dialog when clicking edit on a game card', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('edit-btn-game-1'));

      await waitFor(() => {
        expect(screen.getByText('Edit Game')).toBeInTheDocument();
      });
    });
  });

  describe('Delete Game Dialog', () => {
    it('should open delete dialog when clicking delete on a game card', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('delete-btn-game-1'));

      await waitFor(() => {
        expect(screen.getByText('Delete Game')).toBeInTheDocument();
        expect(
          screen.getByText(/Are you sure you want to delete/)
        ).toBeInTheDocument();
      });
    });

    it('should call API and reload when confirming delete', async () => {
      mockGetPrivateGames.mockResolvedValue(
        createPaginatedResponse(mockGames)
      );
      mockDeletePrivateGame.mockResolvedValueOnce({});

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('delete-btn-game-1'));

      await waitFor(() => {
        expect(screen.getByTestId('confirm-delete-btn')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('confirm-delete-btn'));

      await waitFor(() => {
        expect(mockDeletePrivateGame).toHaveBeenCalledWith('game-1');
      });
    });

    it('should show cancel button in delete dialog', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('delete-btn-game-2'));

      await waitFor(() => {
        expect(screen.getByText('Cancel')).toBeInTheDocument();
      });
    });
  });

  describe('API Integration', () => {
    it('should call getPrivateGames with default params on initial load', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames)
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(mockGetPrivateGames).toHaveBeenCalledWith({
          page: 1,
          pageSize: 12,
          search: undefined,
          sortBy: 'createdAt',
          sortDirection: 'desc',
        });
      });
    });

    it('should navigate to add page on Add Game click', async () => {
      mockGetPrivateGames.mockResolvedValue(
        createPaginatedResponse(mockGames)
      );

      const user = userEvent.setup();
      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      await user.click(screen.getByTestId('add-private-game-btn'));

      expect(mockPush).toHaveBeenCalledWith('/library/private/add');
    });
  });

  describe('Accessibility', () => {
    it('should have proper heading hierarchy', async () => {
      mockGetPrivateGames.mockResolvedValueOnce(
        createPaginatedResponse(mockGames)
      );

      renderWithQuery(<PrivateGamesClient />);

      await waitFor(() => {
        expect(screen.getByTestId('games-grid')).toBeInTheDocument();
      });

      expect(
        screen.getByRole('heading', { level: 1, name: 'Private Games' })
      ).toBeInTheDocument();
    });

    it('should have search icon as aria-hidden', () => {
      mockGetPrivateGames.mockReturnValue(new Promise(() => {}));

      const { container } = renderWithQuery(<PrivateGamesClient />);

      const searchContainer = container.querySelector('.relative.flex-1');
      const svg = searchContainer?.querySelector('svg');
      expect(svg).toHaveAttribute('aria-hidden', 'true');
    });
  });
});
