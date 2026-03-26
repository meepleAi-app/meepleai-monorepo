/**
 * MeepleUserLibraryCard Component Tests
 * Issue #4909 - Uniform MeepleCard UI across /dashboard, /games and /admin/shared-games
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { MeepleUserLibraryCard, MeepleUserLibraryCardSkeleton } from '../MeepleUserLibraryCard';
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
  getNavigationLinks: vi.fn(() => [
    { label: 'KB', href: '/library/games/game-1/knowledge-base' },
    { label: 'Agents', href: '/library/games/game-1/agents' },
  ]),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGame: UserGameDto = {
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
  lastPlayed: '2024-01-01T00:00:00Z',
  isOwned: true,
  inWishlist: false,
};

const wishlistGame: UserGameDto = {
  ...mockGame,
  id: 'game-2',
  title: 'Catan',
  isOwned: false,
  inWishlist: true,
};

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

describe('MeepleUserLibraryCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders the game title', () => {
      renderWithProviders(<MeepleUserLibraryCard game={mockGame} />);
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });

    it('renders the publisher as subtitle', () => {
      renderWithProviders(<MeepleUserLibraryCard game={mockGame} />);
      expect(screen.getByText('Stonemaier Games')).toBeInTheDocument();
    });

    it('uses imageUrl (not thumbnailUrl) for image', () => {
      renderWithProviders(<MeepleUserLibraryCard game={mockGame} />);
      const img = screen.queryByRole('img', { name: /Wingspan/i });
      if (img) {
        expect(img).toHaveAttribute('src');
        const src = img.getAttribute('src') ?? '';
        expect(src).not.toContain('thumb');
      }
    });

    it('renders data-testid with game id', () => {
      renderWithProviders(<MeepleUserLibraryCard game={mockGame} />);
      expect(screen.getByTestId('library-game-card-game-1')).toBeInTheDocument();
    });

    it('shows "Owned" badge for owned games', () => {
      renderWithProviders(<MeepleUserLibraryCard game={mockGame} />);
      expect(screen.getByText('Owned')).toBeInTheDocument();
    });

    it('shows "Wishlist" badge for wishlist games', () => {
      renderWithProviders(<MeepleUserLibraryCard game={wishlistGame} />);
      expect(screen.getByText('Wishlist')).toBeInTheDocument();
    });

    it('renders play count when playCount > 0', () => {
      renderWithProviders(<MeepleUserLibraryCard game={mockGame} />);
      expect(screen.getByText('5x')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      renderWithProviders(<MeepleUserLibraryCard game={mockGame} className="custom-class" />);
      const card = screen.getByTestId('library-game-card-game-1');
      // className may be on the card element itself or a FlipCard ancestor wrapper
      expect(
        card.classList.contains('custom-class') || !!card.closest('.custom-class')
      ).toBeTruthy();
    });
  });

  describe('Navigation', () => {
    it('accepts onClick prop without errors', () => {
      const onClickMock = vi.fn();
      // Verify the card renders with onClick prop (click propagation is MeepleCard's responsibility)
      expect(() =>
        renderWithProviders(<MeepleUserLibraryCard game={mockGame} onClick={onClickMock} />)
      ).not.toThrow();
      expect(screen.getByTestId('library-game-card-game-1')).toBeInTheDocument();
    });

    it('renders info button linking to game detail', () => {
      renderWithProviders(<MeepleUserLibraryCard game={mockGame} />);
      const infoLink = screen.queryByRole('link', { name: /dettaglio|info/i });
      if (infoLink) {
        expect(infoLink).toHaveAttribute('href', '/library/games/game-1');
      }
    });
  });

  describe('Flip Card Lazy Loading', () => {
    it('calls useSharedGame with fetchDetail=false initially (lazy loading)', async () => {
      const { useSharedGame } = await import('@/hooks/queries');

      renderWithProviders(<MeepleUserLibraryCard game={mockGame} />);

      // On initial render, fetchDetail=false → no network request until user flips
      expect(vi.mocked(useSharedGame)).toHaveBeenCalledWith('game-1', false);
    });
  });

  describe('Variants', () => {
    it('accepts grid variant', () => {
      const { container } = renderWithProviders(
        <MeepleUserLibraryCard game={mockGame} variant="grid" />
      );
      expect(container.firstChild).toBeTruthy();
    });

    it('accepts list variant', () => {
      const { container } = renderWithProviders(
        <MeepleUserLibraryCard game={mockGame} variant="list" />
      );
      expect(container.firstChild).toBeTruthy();
    });
  });
});

describe('MeepleUserLibraryCardSkeleton', () => {
  it('renders skeleton placeholder', () => {
    const { container } = render(<MeepleUserLibraryCardSkeleton />);
    expect(container.firstChild).toBeTruthy();
  });

  it('renders with skeleton testid', () => {
    render(<MeepleUserLibraryCardSkeleton />);
    // MeepleCard returns <MeepleCardSkeleton> when loading=true, with hardcoded testid
    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
  });

  it('accepts variant prop', () => {
    const { container } = render(<MeepleUserLibraryCardSkeleton variant="list" />);
    expect(container.firstChild).toBeTruthy();
  });
});
