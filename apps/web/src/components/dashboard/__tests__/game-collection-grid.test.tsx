/**
 * GameCollectionGrid Component Tests
 * Issue #4909 - Uniform MeepleCard UI across /dashboard, /games and /admin/shared-games
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { GameCollectionGrid } from '../game-collection-grid';
import type { UserGameDto } from '@/lib/api/dashboard-client';

// Mock window.matchMedia (MeepleCard uses it for mobile detection)
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
// Mock Dependencies
// ============================================================================

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/hooks/queries', () => ({
  useSharedGame: vi.fn(() => ({ data: undefined })),
  useRemoveGameFromLibrary: vi.fn(() => ({
    mutate: vi.fn(),
    isPending: false,
  })),
}));

vi.mock('@/config/entity-navigation', () => ({
  getNavigationLinks: vi.fn(() => []),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGames: UserGameDto[] = [
  {
    id: 'game-1',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    imageUrl: 'https://example.com/wingspan.jpg',
    thumbnailUrl: 'https://example.com/wingspan-thumb.jpg',
    averageRating: 8.0,
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    complexityRating: 2.4,
    playCount: 5,
    isOwned: true,
    inWishlist: false,
  },
  {
    id: 'game-2',
    title: 'Catan',
    publisher: 'Kosmos',
    imageUrl: 'https://example.com/catan.jpg',
    thumbnailUrl: 'https://example.com/catan-thumb.jpg',
    averageRating: 7.5,
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    complexityRating: 2.3,
    playCount: 12,
    isOwned: true,
    inWishlist: false,
  },
];

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
}

function renderWithProviders(ui: React.ReactElement) {
  const client = createTestQueryClient();
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

// ============================================================================
// Tests
// ============================================================================

describe('GameCollectionGrid', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders game cards for each game', () => {
      renderWithProviders(<GameCollectionGrid games={mockGames} />);

      expect(screen.getByText('Wingspan')).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders using MeepleUserLibraryCard (has testids)', () => {
      renderWithProviders(<GameCollectionGrid games={mockGames} />);

      expect(screen.getByTestId('library-game-card-game-1')).toBeInTheDocument();
      expect(screen.getByTestId('library-game-card-game-2')).toBeInTheDocument();
    });

    it('uses imageUrl for card images (not thumbnailUrl)', () => {
      renderWithProviders(<GameCollectionGrid games={mockGames} />);

      // Verify the grid renders
      const cards = screen.getAllByTestId(/library-game-card-/);
      expect(cards.length).toBe(2);
    });
  });

  describe('Loading State', () => {
    it('renders skeleton cards when isLoading is true', () => {
      renderWithProviders(<GameCollectionGrid games={[]} isLoading={true} />);

      // MeepleCard renders its own skeleton with data-testid="meeple-card-skeleton"
      // The skeleton wrapper uses data-testid="library-game-card-skeleton" on the outer MeepleCard
      // but MeepleCard returns <MeepleCardSkeleton> which has its own testid
      const skeletons = screen.getAllByTestId('meeple-card-skeleton');
      expect(skeletons.length).toBeGreaterThan(0);
    });

    it('does not render game cards when loading', () => {
      renderWithProviders(<GameCollectionGrid games={mockGames} isLoading={true} />);
      // While loading, game titles should not be shown
      expect(screen.queryByText('Wingspan')).not.toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('renders empty state message when no games', () => {
      renderWithProviders(<GameCollectionGrid games={[]} />);

      expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
    });

    it('does not render skeletons in empty state (not loading)', () => {
      renderWithProviders(<GameCollectionGrid games={[]} isLoading={false} />);

      // MeepleCardSkeleton always uses data-testid="meeple-card-skeleton" (hardcoded)
      expect(screen.queryByTestId('meeple-card-skeleton')).not.toBeInTheDocument();
    });
  });

  describe('Grid Layout', () => {
    it('renders games in a grid container', () => {
      const { container } = renderWithProviders(<GameCollectionGrid games={mockGames} />);
      const grid = container.querySelector('.grid');
      expect(grid).toBeInTheDocument();
    });

    it('renders all provided games', () => {
      const manyGames = Array.from({ length: 8 }, (_, i) => ({
        ...mockGames[0],
        id: `game-${i + 1}`,
        title: `Game ${i + 1}`,
      }));

      renderWithProviders(<GameCollectionGrid games={manyGames} />);

      for (let i = 1; i <= 8; i++) {
        expect(screen.getByText(`Game ${i}`)).toBeInTheDocument();
      }
    });
  });
});
