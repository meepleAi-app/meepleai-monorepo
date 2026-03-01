/**
 * CatalogSearchStep Component Tests — Issue #5169
 *
 * Tests for the catalog search step in AddGameDrawer.
 * Covers:
 * - Renders search input and grid
 * - Shows loading skeletons while fetching
 * - Shows game cards with results
 * - Shows empty state when no results
 * - Calls onSelect + mutation on game selection
 * - Disables already-in-library cards
 * - Shows pagination when multiple pages
 * - Back button calls onBack
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';

import { CatalogSearchStep } from '../CatalogSearchStep';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/queries', () => ({
  useSharedGames: vi.fn(),
  useGameInLibraryStatus: vi.fn(),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useAddGameToLibrary: vi.fn(),
}));

vi.mock('@/components/layout', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('@/components/catalog/CatalogPagination', () => ({
  CatalogPagination: ({
    currentPage,
    totalPages,
    onPageChange,
  }: {
    currentPage: number;
    totalPages: number;
    onPageChange: (page: number) => void;
    totalResults?: number;
  }) => (
    <div data-testid="catalog-pagination">
      <span data-testid="pagination-current">{currentPage}</span>
      <span data-testid="pagination-total">{totalPages}</span>
      <button data-testid="pagination-next" onClick={() => onPageChange(currentPage + 1)}>
        Next
      </button>
    </div>
  ),
}));

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

import { useSharedGames, useGameInLibraryStatus } from '@/hooks/queries';
import { useAddGameToLibrary } from '@/hooks/queries/useLibrary';
import { toast } from '@/components/layout';

const mockUseSharedGames = useSharedGames as Mock;
const mockUseGameInLibraryStatus = useGameInLibraryStatus as Mock;
const mockUseAddGameToLibrary = useAddGameToLibrary as Mock;

// ── Shared fixtures ────────────────────────────────────────────────────────────

const GAME_1 = {
  id: 'g1',
  title: 'Catan',
  yearPublished: 1995,
  thumbnailUrl: 'https://example.com/catan.jpg',
};
const GAME_2 = {
  id: 'g2',
  title: 'Ticket to Ride',
  yearPublished: 2004,
  thumbnailUrl: null,
};

function makeGamesData(items = [GAME_1, GAME_2], total = 2) {
  return { items, total };
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function renderStep(props: {
  onSelect?: (id: string, name: string) => void;
  onBack?: () => void;
} = {}) {
  const onSelect = props.onSelect ?? vi.fn();
  const onBack = props.onBack ?? vi.fn();
  const result = render(<CatalogSearchStep onSelect={onSelect} onBack={onBack} />);
  return { onSelect, onBack, ...result };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CatalogSearchStep', () => {
  const mutateAsync = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseSharedGames.mockReturnValue({ data: makeGamesData(), isLoading: false });
    mockUseGameInLibraryStatus.mockReturnValue({ data: { inLibrary: false } });
    mockUseAddGameToLibrary.mockReturnValue({ mutateAsync });
  });

  describe('Layout', () => {
    it('renders the search step container', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-step')).toBeInTheDocument();
    });

    it('renders the search input', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-input')).toBeInTheDocument();
    });

    it('renders the results grid', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-grid')).toBeInTheDocument();
    });

    it('renders the back button', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-back')).toBeInTheDocument();
    });

    it('calls onBack when back button is clicked', async () => {
      const user = userEvent.setup();
      const { onBack } = renderStep();

      await user.click(screen.getByTestId('catalog-search-back'));

      expect(onBack).toHaveBeenCalledTimes(1);
    });
  });

  describe('Loading state', () => {
    it('shows skeletons while loading', () => {
      mockUseSharedGames.mockReturnValue({ data: undefined, isLoading: true });
      renderStep();

      // No game cards should be rendered
      expect(screen.queryByTestId('catalog-card-g1')).not.toBeInTheDocument();
      // Grid should still be present
      expect(screen.getByTestId('catalog-search-grid')).toBeInTheDocument();
    });
  });

  describe('Results', () => {
    it('shows game cards for each result', () => {
      renderStep();
      expect(screen.getByTestId('catalog-card-g1')).toBeInTheDocument();
      expect(screen.getByTestId('catalog-card-g2')).toBeInTheDocument();
    });

    it('shows result count', () => {
      renderStep();
      expect(screen.getByTestId('catalog-search-count')).toHaveTextContent('2 games found');
    });

    it('shows singular "game" when total is 1', () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([GAME_1], 1),
        isLoading: false,
      });
      renderStep();
      expect(screen.getByTestId('catalog-search-count')).toHaveTextContent('1 game found');
    });

    it('shows empty state when no results', () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([], 0),
        isLoading: false,
      });
      renderStep();
      expect(screen.getByTestId('catalog-search-empty')).toBeInTheDocument();
    });

    it('shows game title on card', () => {
      renderStep();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('shows year published when > 0', () => {
      renderStep();
      expect(screen.getByText('1995')).toBeInTheDocument();
    });

    it('shows "No image" placeholder when no thumbnail', () => {
      renderStep();
      expect(screen.getByText('No image')).toBeInTheDocument();
    });
  });

  describe('Game selection', () => {
    it('shows Select button on each game card', () => {
      renderStep();
      expect(screen.getByTestId('catalog-card-g1-select-btn')).toBeInTheDocument();
    });

    it('calls mutation and onSelect when Select is clicked', async () => {
      const user = userEvent.setup();
      const { onSelect } = renderStep();

      await user.click(screen.getByTestId('catalog-card-g1-select-btn'));

      await waitFor(() => {
        expect(mutateAsync).toHaveBeenCalledWith({ gameId: 'g1' });
        expect(onSelect).toHaveBeenCalledWith('g1', 'Catan');
      });
    });

    it('shows success toast after selection', async () => {
      const user = userEvent.setup();
      renderStep();

      await user.click(screen.getByTestId('catalog-card-g1-select-btn'));

      await waitFor(() => {
        expect(toast.success).toHaveBeenCalledWith('"Catan" added to your library!');
      });
    });

    it('shows error toast when mutation fails', async () => {
      mutateAsync.mockRejectedValueOnce(new Error('Network error'));
      const user = userEvent.setup();
      renderStep();

      await user.click(screen.getByTestId('catalog-card-g1-select-btn'));

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Could not add "Catan". Please try again.');
      });
    });
  });

  describe('Already in library', () => {
    it('disables Select button for games already in library', () => {
      mockUseGameInLibraryStatus.mockImplementation((gameId: string) => ({
        data: { inLibrary: gameId === 'g1' },
      }));
      renderStep();

      expect(screen.getByTestId('catalog-card-g1-select-btn')).toBeDisabled();
      expect(screen.getByTestId('catalog-card-g2-select-btn')).not.toBeDisabled();
    });

    it('shows "Already in library" button label for in-library games', () => {
      mockUseGameInLibraryStatus.mockReturnValue({ data: { inLibrary: true } });
      renderStep();

      expect(screen.getAllByText('Already in library').length).toBeGreaterThan(0);
    });

    it('shows "In library" badge on in-library cards', () => {
      mockUseGameInLibraryStatus.mockImplementation((gameId: string) => ({
        data: { inLibrary: gameId === 'g1' },
      }));
      renderStep();

      expect(screen.getByTestId('catalog-card-g1-in-library-badge')).toBeInTheDocument();
      expect(
        screen.queryByTestId('catalog-card-g2-in-library-badge'),
      ).not.toBeInTheDocument();
    });
  });

  describe('Pagination', () => {
    it('does not show pagination when only one page', () => {
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([GAME_1], 1),
        isLoading: false,
      });
      renderStep();
      expect(screen.queryByTestId('catalog-pagination')).not.toBeInTheDocument();
    });

    it('shows pagination when total exceeds page size', () => {
      // PAGE_SIZE = 9, total = 20 → 3 pages
      mockUseSharedGames.mockReturnValue({
        data: makeGamesData([GAME_1, GAME_2], 20),
        isLoading: false,
      });
      renderStep();
      expect(screen.getByTestId('catalog-pagination')).toBeInTheDocument();
    });
  });
});
