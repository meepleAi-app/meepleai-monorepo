/**
 * CatalogGameCard Component Tests
 *
 * Issue #2874: [Shared Catalog] Catalog Game Cards with Community Stats
 *
 * Tests:
 * - Card rendering (image, title, metadata, community stats)
 * - "Already in Library" badge display
 * - Hover overlay with "Aggiungi" button
 * - Add to library functionality
 * - Navigation on card click
 * - Loading states (skeleton)
 * - Edge cases (missing data)
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { CatalogGameCard, CatalogGameCardSkeleton, type CommunityStats } from '../CatalogGameCard';
import { useGameInLibraryStatus, useAddGameToLibrary, useLibraryQuota } from '@/hooks/queries';
import { toast } from '@/components/layout/Toast';
import type { SharedGame } from '@/lib/api';

// Mock hooks
vi.mock('@/hooks/queries', () => ({
  useGameInLibraryStatus: vi.fn(),
  useAddGameToLibrary: vi.fn(),
  useLibraryQuota: vi.fn(),
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

// Mock next/link
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    className,
  }: {
    href: string;
    children: React.ReactNode;
    className?: string;
  }) => (
    <a href={href} className={className}>
      {children}
    </a>
  ),
}));

// Test data
const mockGame: SharedGame = {
  id: 'game-123',
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
  status: 'Published',
  minAge: 10,
  createdAt: '2024-01-01T00:00:00Z',
  modifiedAt: null,
};

const mockCommunityStats: CommunityStats = {
  totalPlays: 1234,
  contributorCount: 5,
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
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('CatalogGameCard', () => {
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

    // Default: quota not exceeded
    (useLibraryQuota as Mock).mockReturnValue({
      data: { remainingSlots: 10, currentCount: 5, maxAllowed: 15, userTier: 'normal', percentageUsed: 33.3 },
      isLoading: false,
    });
  });

  // ==========================================================================
  // Basic Rendering
  // ==========================================================================

  describe('Basic Rendering', () => {
    it('renders game title', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders game image when imageUrl is provided', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      const image = screen.getByAltText('Catan');
      expect(image).toBeInTheDocument();
      expect(image).toHaveAttribute('src', 'https://example.com/catan.jpg');
    });

    it('renders placeholder icon when no image', () => {
      const gameWithoutImage = { ...mockGame, imageUrl: '' };
      renderWithQuery(<CatalogGameCard game={gameWithoutImage} />);
      expect(screen.queryByAltText('Catan')).not.toBeInTheDocument();
    });

    it('renders as a link to game detail page', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      const link = screen.getByRole('link');
      expect(link).toHaveAttribute('href', '/shared-games/game-123');
    });
  });

  // ==========================================================================
  // Game Info Display
  // ==========================================================================

  describe('Game Info Display', () => {
    it('renders player count range', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.getByText('3-4')).toBeInTheDocument();
    });

    it('renders single player count when min equals max', () => {
      const soloGame = { ...mockGame, minPlayers: 2, maxPlayers: 2 };
      renderWithQuery(<CatalogGameCard game={soloGame} />);
      expect(screen.getByText('2')).toBeInTheDocument();
    });

    it('renders complexity as dots pattern', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      // Complexity 2.32 rounds to 2 filled, 3 empty
      expect(screen.getByText('●●○○○')).toBeInTheDocument();
    });

    it('shows N/A when no complexity rating', () => {
      const gameWithoutComplexity = { ...mockGame, complexityRating: null };
      renderWithQuery(<CatalogGameCard game={gameWithoutComplexity} />);
      expect(screen.getByText('N/A')).toBeInTheDocument();
    });

    it('renders playtime', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.getByText('90 min')).toBeInTheDocument();
    });

    it('shows N/A when no playtime', () => {
      const gameWithoutPlaytime = { ...mockGame, playingTimeMinutes: 0 };
      renderWithQuery(<CatalogGameCard game={gameWithoutPlaytime} />);
      // Multiple N/A might appear - use getAllByText
      const naElements = screen.getAllByText('N/A');
      expect(naElements.length).toBeGreaterThan(0);
    });
  });

  // ==========================================================================
  // Community Stats
  // ==========================================================================

  describe('Community Stats', () => {
    it('renders rating stars when averageRating is provided', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      // RatingStars should show the rating value
      expect(screen.getByText('7.2')).toBeInTheDocument();
    });

    it('renders total plays when provided in communityStats', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} communityStats={mockCommunityStats} />);
      expect(screen.getByText('1.2K')).toBeInTheDocument(); // 1234 formatted as 1.2K
    });

    it('renders contributor count when provided in communityStats', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} communityStats={mockCommunityStats} />);
      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('formats large numbers with K suffix', () => {
      const largeStats: CommunityStats = {
        totalPlays: 15000,
        contributorCount: 2500,
      };
      renderWithQuery(<CatalogGameCard game={mockGame} communityStats={largeStats} />);
      expect(screen.getByText('15.0K')).toBeInTheDocument();
      expect(screen.getByText('2.5K')).toBeInTheDocument();
    });

    it('formats very large numbers with M suffix', () => {
      const veryLargeStats: CommunityStats = {
        totalPlays: 1500000,
        contributorCount: 100,
      };
      renderWithQuery(<CatalogGameCard game={mockGame} communityStats={veryLargeStats} />);
      expect(screen.getByText('1.5M')).toBeInTheDocument();
    });

    it('shows fallback message when no stats available', () => {
      const gameWithoutRating = { ...mockGame, averageRating: null };
      renderWithQuery(<CatalogGameCard game={gameWithoutRating} />);
      expect(screen.getByText('Statistiche non disponibili')).toBeInTheDocument();
    });

    it('does not show plays/contributors when count is 0', () => {
      const zeroStats: CommunityStats = {
        totalPlays: 0,
        contributorCount: 0,
      };
      renderWithQuery(<CatalogGameCard game={mockGame} communityStats={zeroStats} />);
      // Should not render the Gamepad2 or UserPlus icons with 0 values
      const gamepadIcons = document.querySelectorAll('[title="Partite totali"]');
      const userPlusIcons = document.querySelectorAll('[title="Contributori"]');
      expect(gamepadIcons.length).toBe(0);
      expect(userPlusIcons.length).toBe(0);
    });
  });

  // ==========================================================================
  // Library Status Badge
  // ==========================================================================

  describe('Library Status Badge', () => {
    it('shows "Nella Libreria" badge when in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.getByText('Nella Libreria')).toBeInTheDocument();
    });

    it('does not show badge when not in library', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.queryByText('Nella Libreria')).not.toBeInTheDocument();
    });

    it('does not show badge while loading library status', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: undefined,
        isLoading: true,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.queryByText('Nella Libreria')).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Hover Overlay with Add Button
  // ==========================================================================

  describe('Hover Overlay', () => {
    it('renders "Aggiungi" button in hover overlay when not in library', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.getByText('Aggiungi')).toBeInTheDocument();
    });

    it('does not render overlay button when already in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.queryByText('Aggiungi')).not.toBeInTheDocument();
    });

    it('calls mutation when "Aggiungi" button is clicked', async () => {
      mockMutateAsync.mockResolvedValue({});

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi');
      await act(async () => {
        fireEvent.click(addButton);
      });

      expect(mockMutateAsync).toHaveBeenCalledWith({ gameId: 'game-123' });
    });

    it('shows success toast on successful add', async () => {
      mockMutateAsync.mockResolvedValue({});

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi');
      await act(async () => {
        fireEvent.click(addButton);
      });

      await waitFor(() => {
        expect(toast.success).toHaveBeenCalledWith('Catan aggiunto alla libreria!');
      });
    });

    it('shows error toast on failed add', async () => {
      mockMutateAsync.mockRejectedValue(new Error('Network error'));

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi');
      await act(async () => {
        fireEvent.click(addButton);
      });

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Network error');
      });
    });

    it('shows generic error for non-Error rejection', async () => {
      mockMutateAsync.mockRejectedValue('Unknown error');

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi');
      await act(async () => {
        fireEvent.click(addButton);
      });

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith("Errore durante l'aggiunta alla libreria");
      });
    });

    it('shows loading text while adding', async () => {
      mockMutateAsync.mockImplementation(
        () => new Promise((resolve) => setTimeout(resolve, 100))
      );

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi');
      fireEvent.click(addButton);

      await waitFor(() => {
        expect(screen.getByText('Aggiunta...')).toBeInTheDocument();
      });
    });

    it('disables button while mutation is pending', () => {
      (useAddGameToLibrary as Mock).mockReturnValue({
        mutateAsync: mockMutateAsync,
        isPending: true,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByText('Aggiunta...');
      expect(addButton.closest('button')).toBeDisabled();
    });

    it('prevents link navigation when clicking add button', async () => {
      mockMutateAsync.mockResolvedValue({});

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByText('Aggiungi');
      const clickEvent = new MouseEvent('click', { bubbles: true, cancelable: true });
      const preventDefaultSpy = vi.spyOn(clickEvent, 'preventDefault');
      const stopPropagationSpy = vi.spyOn(clickEvent, 'stopPropagation');

      await act(async () => {
        addButton.dispatchEvent(clickEvent);
      });

      expect(preventDefaultSpy).toHaveBeenCalled();
      expect(stopPropagationSpy).toHaveBeenCalled();
    });
  });

  // ==========================================================================
  // Quota Exceeded (Issue #2875)
  // ==========================================================================

  describe('Quota Exceeded (Issue #2875)', () => {
    it('disables button when quota is exceeded (remainingSlots = 0)', () => {
      (useLibraryQuota as Mock).mockReturnValue({
        data: { remainingSlots: 0, currentCount: 15, maxAllowed: 15, userTier: 'normal', percentageUsed: 100 },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByTestId('add-to-library-button');
      expect(addButton).toBeDisabled();
    });

    it('shows "Quota esaurita" text when quota is exceeded', () => {
      (useLibraryQuota as Mock).mockReturnValue({
        data: { remainingSlots: 0, currentCount: 15, maxAllowed: 15, userTier: 'normal', percentageUsed: 100 },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      expect(screen.getByText('Quota esaurita')).toBeInTheDocument();
    });

    it('shows tooltip explaining quota exceeded', () => {
      (useLibraryQuota as Mock).mockReturnValue({
        data: { remainingSlots: 0, currentCount: 15, maxAllowed: 15, userTier: 'normal', percentageUsed: 100 },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByTestId('add-to-library-button');
      expect(addButton).toHaveAttribute('title', 'Hai raggiunto il limite di giochi in libreria');
    });

    it('applies gray styling when quota is exceeded', () => {
      (useLibraryQuota as Mock).mockReturnValue({
        data: { remainingSlots: 0, currentCount: 15, maxAllowed: 15, userTier: 'normal', percentageUsed: 100 },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByTestId('add-to-library-button');
      expect(addButton).toHaveClass('bg-gray-400');
    });

    it('does not call mutation when clicking disabled quota exceeded button', async () => {
      (useLibraryQuota as Mock).mockReturnValue({
        data: { remainingSlots: 0, currentCount: 15, maxAllowed: 15, userTier: 'normal', percentageUsed: 100 },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByTestId('add-to-library-button');
      fireEvent.click(addButton);

      expect(mockMutateAsync).not.toHaveBeenCalled();
    });

    it('enables button when quota has remaining slots', () => {
      (useLibraryQuota as Mock).mockReturnValue({
        data: { remainingSlots: 5, currentCount: 10, maxAllowed: 15, userTier: 'normal', percentageUsed: 66.7 },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByTestId('add-to-library-button');
      expect(addButton).not.toBeDisabled();
      expect(screen.getByText('Aggiungi')).toBeInTheDocument();
    });

    it('disables button while quota is loading', () => {
      (useLibraryQuota as Mock).mockReturnValue({
        data: undefined,
        isLoading: true,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByTestId('add-to-library-button');
      expect(addButton).toBeDisabled();
    });

    it('handles undefined quota data gracefully (button not disabled)', () => {
      (useLibraryQuota as Mock).mockReturnValue({
        data: undefined,
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);

      const addButton = screen.getByTestId('add-to-library-button');
      expect(addButton).not.toBeDisabled();
      expect(screen.getByText('Aggiungi')).toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('handles missing averageRating gracefully', () => {
      const gameWithoutRating = { ...mockGame, averageRating: null };
      renderWithQuery(<CatalogGameCard game={gameWithoutRating} />);
      // Should show fallback message
      expect(screen.getByText('Statistiche non disponibili')).toBeInTheDocument();
    });

    it('handles undefined communityStats', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} communityStats={undefined} />);
      // Should still render without crashing
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('handles empty title gracefully', () => {
      const gameWithEmptyTitle = { ...mockGame, title: '' };
      renderWithQuery(<CatalogGameCard game={gameWithEmptyTitle} />);
      // Should render without crashing
      expect(screen.getByRole('link')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = renderWithQuery(
        <CatalogGameCard game={mockGame} className="custom-class" />
      );
      const card = container.querySelector('.custom-class');
      expect(card).toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Data Test IDs (E2E Selector Support)
  // ==========================================================================

  describe('Data Test IDs', () => {
    it('renders card with data-testid', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.getByTestId('catalog-game-card')).toBeInTheDocument();
    });

    it('renders library badge with data-testid when in library', () => {
      (useGameInLibraryStatus as Mock).mockReturnValue({
        data: { inLibrary: true },
        isLoading: false,
      });

      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.getByTestId('library-badge')).toBeInTheDocument();
    });

    it('renders add button with data-testid when not in library', () => {
      renderWithQuery(<CatalogGameCard game={mockGame} />);
      expect(screen.getByTestId('add-to-library-button')).toBeInTheDocument();
    });
  });
});

// ==========================================================================
// Skeleton Component
// ==========================================================================

describe('CatalogGameCardSkeleton', () => {
  it('renders skeleton placeholders', () => {
    render(<CatalogGameCardSkeleton />);
    // Skeleton uses animate-pulse class from shadcn/ui
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders without crashing', () => {
    const { container } = render(<CatalogGameCardSkeleton />);
    expect(container.firstChild).toBeInTheDocument();
  });
});
