/**
 * GameCatalogCard Component Tests
 *
 * Issue #2763: Sprint 3 - Catalog & Shared Games Components (0% → 85%)
 *
 * Tests:
 * - Card rendering (image, title, metadata)
 * - Complexity display
 * - Add to library functionality
 * - "Already in library" state
 * - Loading states
 * - Categories and mechanics tags
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { GameCatalogCard, GameCatalogCardSkeleton } from '../GameCatalogCard';
import { useGameInLibraryStatus, useAddGameToLibrary } from '@/hooks/queries';
import { toast } from '@/components/layout/Toast';
import type { SharedGame, SharedGameDetail } from '@/lib/api';

// Mock hooks
vi.mock('@/hooks/queries', () => ({
  useGameInLibraryStatus: vi.fn(),
  useAddGameToLibrary: vi.fn(),
}));

// Mock toast
vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Test data
const mockGame: SharedGame = {
  id: 'game-1',
  title: 'Catan',
  bggId: 13,
  description: 'A game of trading and building',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  complexityRating: 2.32,
  imageUrl: 'https://example.com/catan.jpg',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  yearPublished: 1995,
  averageRating: 7.2,
  status: 1,
};

const mockGameDetail: SharedGameDetail = {
  ...mockGame,
  categories: [
    { id: 'strategy', name: 'Strategy' },
    { id: 'economic', name: 'Economic' },
  ],
  mechanics: [
    { id: 'trading', name: 'Trading' },
    { id: 'modular-board', name: 'Modular Board' },
  ],
  designers: [{ id: 'd1', name: 'Klaus Teuber' }],
  publishers: [{ id: 'p1', name: 'Kosmos' }],
  rules: null,
  faqs: [],
  erratas: [],
  minAge: 10,
};

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
}

function renderWithQuery(ui: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
  );
}

describe('GameCatalogCard', () => {
  const mockMutateAsync = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    // Default mock implementations
    (useGameInLibraryStatus as Mock).mockReturnValue({
      data: { inLibrary: false },
      isLoading: false,
    });

    (useAddGameToLibrary as Mock).mockReturnValue({
      mutateAsync: mockMutateAsync,
      isPending: false,
    });
  });

  // ==========================================================================
  // Basic Rendering
  // ==========================================================================

  describe('Basic Rendering', () => {
    it('renders game title', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders game image when imageUrl is provided', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      const image = screen.getByAltText('Catan');
      expect(image).toBeInTheDocument();
      expect(image).toHaveAttribute('src', 'https://example.com/catan.jpg');
    });

    it('renders placeholder when no image', () => {
      const gameWithoutImage = { ...mockGame, imageUrl: undefined };
      renderWithQuery(<GameCatalogCard game={gameWithoutImage} />);
      // Should show Library icon as placeholder
      expect(screen.queryByAltText('Catan')).not.toBeInTheDocument();
    });

    it('renders BGG ID', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      expect(screen.getByText('BGG ID: 13')).toBeInTheDocument();
    });

    it('renders players range', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      expect(screen.getByText(/3-4 giocatori/)).toBeInTheDocument();
    });

    it('renders single player count correctly', () => {
      const soloGame = { ...mockGame, minPlayers: 2, maxPlayers: 2 };
      renderWithQuery(<GameCatalogCard game={soloGame} />);
      expect(screen.getByText(/2 giocatori/)).toBeInTheDocument();
    });

    it('renders playtime', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      expect(screen.getByText(/90 min/)).toBeInTheDocument();
    });

    it('renders description', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      expect(screen.getByText('A game of trading and building')).toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Complexity Display
  // ==========================================================================

  describe('Complexity Display', () => {
    it('renders complexity stars (rounded)', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      // Complexity 2.32 rounds to 2 filled, 3 empty
      expect(screen.getByText(/Complessità:/)).toBeInTheDocument();
      expect(screen.getByText('●●○○○')).toBeInTheDocument();
    });

    it('shows N/A when no complexity', () => {
      const gameWithoutComplexity = { ...mockGame, complexityRating: undefined };
      renderWithQuery(<GameCatalogCard game={gameWithoutComplexity} />);
      expect(screen.getByText('N/A')).toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Categories and Mechanics Tags
  // ==========================================================================

  describe('Categories and Mechanics Tags', () => {
    it('renders category badges for SharedGameDetail', () => {
      renderWithQuery(<GameCatalogCard game={mockGameDetail} />);
      expect(screen.getByText('Strategy')).toBeInTheDocument();
      expect(screen.getByText('Economic')).toBeInTheDocument();
    });

    it('renders mechanic badges for SharedGameDetail', () => {
      renderWithQuery(<GameCatalogCard game={mockGameDetail} />);
      expect(screen.getByText('Trading')).toBeInTheDocument();
      expect(screen.getByText('Modular Board')).toBeInTheDocument();
    });

    it('shows overflow badge when more than 4 tags', () => {
      const gameWithManyTags: SharedGameDetail = {
        ...mockGameDetail,
        categories: [
          { id: 'c1', name: 'Category 1' },
          { id: 'c2', name: 'Category 2' },
          { id: 'c3', name: 'Category 3' },
        ],
        mechanics: [
          { id: 'm1', name: 'Mechanic 1' },
          { id: 'm2', name: 'Mechanic 2' },
          { id: 'm3', name: 'Mechanic 3' },
        ],
      };
      renderWithQuery(<GameCatalogCard game={gameWithManyTags} />);
      // Shows first 2 categories + first 2 mechanics = 4 shown
      // Total 6, so +2 overflow
      expect(screen.getByText('+2')).toBeInTheDocument();
    });

    it('does not render tags for basic SharedGame', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      // Basic SharedGame doesn't have categories/mechanics arrays
      expect(screen.queryByText('Strategy')).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Library Status
  // ==========================================================================

  describe('Library Status', () => {
    it('shows "Add to Library" button when not in library', () => {
      renderWithQuery(<GameCatalogCard game={mockGame} />);
      expect(screen.getByText('Aggiungi alla Libreria')).toBeInTheDocument();
    });

    it('shows "Already in Library" button when in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });

      renderWithQuery(<GameCatalogCard game={mockGame} />);
      expect(screen.getByText('Già nella Libreria')).toBeInTheDocument();
    });

    it('shows "In Library" badge on image when in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });

      renderWithQuery(<GameCatalogCard game={mockGame} />);
      expect(screen.getByText('Nella Libreria')).toBeInTheDocument();
    });

    it('shows loading skeleton while checking library status', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: undefined,
        isLoading: true,
      });

      renderWithQuery(<GameCatalogCard game={mockGame} />);
      // Button area should show skeleton
      expect(screen.queryByText('Aggiungi alla Libreria')).not.toBeInTheDocument();
      expect(screen.queryByText('Già nella Libreria')).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Add to Library
  // ==========================================================================

  describe('Add to Library', () => {
    it('calls mutation when add button is clicked', async () => {
      mockMutateAsync.mockResolvedValue({});

      renderWithQuery(<GameCatalogCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi alla Libreria');
      await act(async () => {
        fireEvent.click(addButton);
      });

      expect(mockMutateAsync).toHaveBeenCalledWith({ gameId: 'game-1' });
    });

    it('shows success toast on successful add', async () => {
      mockMutateAsync.mockResolvedValue({});

      renderWithQuery(<GameCatalogCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi alla Libreria');
      await act(async () => {
        fireEvent.click(addButton);
      });

      await waitFor(() => {
        expect(toast.success).toHaveBeenCalledWith('Catan aggiunto alla libreria!');
      });
    });

    it('shows error toast on failed add', async () => {
      mockMutateAsync.mockRejectedValue(new Error('Network error'));

      renderWithQuery(<GameCatalogCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi alla Libreria');
      await act(async () => {
        fireEvent.click(addButton);
      });

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Network error');
      });
    });

    it('shows generic error message for non-Error rejection', async () => {
      mockMutateAsync.mockRejectedValue('Unknown error');

      renderWithQuery(<GameCatalogCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi alla Libreria');
      await act(async () => {
        fireEvent.click(addButton);
      });

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith("Errore durante l'aggiunta alla libreria");
      });
    });

    it('shows loading text while adding', async () => {
      let resolvePromise: () => void;
      const pendingPromise = new Promise<void>((resolve) => {
        resolvePromise = resolve;
      });
      mockMutateAsync.mockImplementation(() => pendingPromise);

      renderWithQuery(<GameCatalogCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi alla Libreria');
      fireEvent.click(addButton);

      await waitFor(() => {
        expect(screen.getByText('Aggiunta in corso...')).toBeInTheDocument();
      });

      // Resolve the promise and wait for cleanup to complete
      await act(async () => {
        resolvePromise!();
        await pendingPromise;
      });
    });

    it('disables add button when already in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });

      renderWithQuery(<GameCatalogCard game={mockGame} />);

      const button = screen.getByText('Già nella Libreria');
      expect(button.closest('button')).toBeDisabled();
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('handles missing BGG ID', () => {
      const gameWithoutBgg = { ...mockGame, bggId: undefined };
      renderWithQuery(<GameCatalogCard game={gameWithoutBgg} />);
      expect(screen.queryByText(/BGG ID:/)).not.toBeInTheDocument();
    });

    it('handles missing description', () => {
      const gameWithoutDesc = { ...mockGame, description: undefined };
      renderWithQuery(<GameCatalogCard game={gameWithoutDesc} />);
      expect(screen.queryByText('A game of trading and building')).not.toBeInTheDocument();
    });

    it('handles missing players info', () => {
      const gameWithoutPlayers = {
        ...mockGame,
        minPlayers: undefined,
        maxPlayers: undefined,
      };
      renderWithQuery(<GameCatalogCard game={gameWithoutPlayers} />);
      expect(screen.getByText(/N\/A/)).toBeInTheDocument();
    });

    it('handles missing playtime', () => {
      const gameWithoutPlaytime = { ...mockGame, playingTimeMinutes: undefined };
      renderWithQuery(<GameCatalogCard game={gameWithoutPlaytime} />);
      // N/A should appear for playtime (use regex for partial match)
      expect(screen.getByText(/N\/A/)).toBeInTheDocument();
    });
  });
});

// ==========================================================================
// Skeleton Component
// ==========================================================================

describe('GameCatalogCardSkeleton', () => {
  it('renders skeleton placeholders', () => {
    render(<GameCatalogCardSkeleton />);
    // Skeleton uses animate-pulse class from shadcn/ui
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });
});
