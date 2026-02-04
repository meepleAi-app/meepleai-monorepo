/**
 * CatalogTrending Tests (Issue #3318)
 *
 * Test coverage for catalog trending widget.
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { CatalogTrending, type TrendingGame } from '../CatalogTrending';

// Mock Next.js Link - preserve all props including data-testid
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// Mock date-fns
vi.mock('date-fns', () => ({
  formatDistanceToNow: vi.fn(() => '5 minuti fa'),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGames: TrendingGame[] = [
  {
    id: 'game-1',
    name: 'Ark Nova',
    trend: 15,
    rank: 1,
    previousRank: 2,
  },
  {
    id: 'game-2',
    name: 'Wingspan',
    trend: 12,
    rank: 2,
    previousRank: 3,
  },
  {
    id: 'game-3',
    name: 'Dune: Imperium',
    trend: 5,
    rank: 3,
    previousRank: 1,
  },
  {
    id: 'game-4',
    name: 'Earth',
    trend: -3,
    rank: 4,
    previousRank: 6,
  },
  {
    id: 'game-5',
    name: 'Cascadia',
    trend: 0,
    rank: 5,
    previousRank: 5,
  },
];

// ============================================================================
// Test Helpers
// ============================================================================

let queryClient: QueryClient;

function renderComponent(props: Partial<React.ComponentProps<typeof CatalogTrending>> = {}) {
  queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <CatalogTrending {...props} />
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('CatalogTrending', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('renders skeleton when loading', () => {
      renderComponent({ isLoading: true });

      expect(screen.getByTestId('catalog-trending-skeleton')).toBeInTheDocument();
    });

    it('shows 5 skeleton rows', () => {
      renderComponent({ isLoading: true });

      const skeleton = screen.getByTestId('catalog-trending-skeleton');
      expect(skeleton).toHaveClass('rounded-2xl');
    });
  });

  describe('Success State', () => {
    it('renders widget with games', () => {
      renderComponent({ games: mockGames });

      expect(screen.getByTestId('catalog-trending-widget')).toBeInTheDocument();
      expect(screen.getByTestId('catalog-trending-title')).toHaveTextContent('Catalog Trending');
    });

    it('renders all trending games', () => {
      renderComponent({ games: mockGames });

      const gamesList = screen.getByTestId('trending-games-list');
      const games = gamesList.querySelectorAll('[data-testid^="trending-game-"]');
      expect(games.length).toBe(5);
    });

    it('limits to 5 games maximum', () => {
      const manyGames: TrendingGame[] = [
        ...mockGames,
        { id: 'game-6', name: 'Extra Game', trend: 2, rank: 6, previousRank: 7 },
        { id: 'game-7', name: 'Another Game', trend: 1, rank: 7, previousRank: 8 },
      ];

      renderComponent({ games: manyGames });

      const gamesList = screen.getByTestId('trending-games-list');
      const games = gamesList.querySelectorAll('[data-testid^="trending-game-"]');
      expect(games.length).toBe(5);
    });

    it('sorts games by rank ascending', () => {
      const unsortedGames: TrendingGame[] = [
        { id: 'game-low', name: 'Low Rank', trend: 5, rank: 3, previousRank: 4 },
        { id: 'game-high', name: 'High Rank', trend: 10, rank: 1, previousRank: 2 },
        { id: 'game-med', name: 'Medium Rank', trend: 7, rank: 2, previousRank: 3 },
      ];

      renderComponent({ games: unsortedGames });

      const ranks = screen.getAllByTestId(/^trending-rank-/);
      expect(ranks[0]).toHaveTextContent('1.');
      expect(ranks[1]).toHaveTextContent('2.');
      expect(ranks[2]).toHaveTextContent('3.');
    });

    it('displays correct ranks', () => {
      renderComponent({ games: mockGames });

      mockGames.forEach((game) => {
        const rank = screen.getByTestId(`trending-rank-${game.id}`);
        expect(rank).toBeInTheDocument();
      });
    });

    it('shows trend badges for each game', () => {
      renderComponent({ games: mockGames });

      mockGames.forEach((game) => {
        expect(screen.getByTestId(`trending-trend-${game.id}`)).toBeInTheDocument();
      });
    });

    it('links to game detail pages', () => {
      renderComponent({ games: mockGames });

      mockGames.forEach((game) => {
        const link = screen.getByTestId(`trending-game-${game.id}`);
        expect(link).toHaveAttribute('href', `/games/${game.id}`);
      });
    });

    it('shows view catalog CTA', () => {
      renderComponent({ games: mockGames });

      const cta = screen.getByTestId('view-catalog-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/games/catalog');
    });
  });

  describe('Trend Indicators', () => {
    it('shows hot icon for trends >= 10%', () => {
      const hotGame: TrendingGame[] = [
        { id: 'hot-game', name: 'Hot Game', trend: 15, rank: 1, previousRank: 2 },
      ];

      renderComponent({ games: hotGame });

      expect(screen.getByTestId('trend-icon-hot')).toBeInTheDocument();
    });

    it('shows up icon for positive trends < 10%', () => {
      const upGame: TrendingGame[] = [
        { id: 'up-game', name: 'Up Game', trend: 5, rank: 1, previousRank: 2 },
      ];

      renderComponent({ games: upGame });

      expect(screen.getByTestId('trend-icon-up')).toBeInTheDocument();
    });

    it('shows down icon for negative trends', () => {
      const downGame: TrendingGame[] = [
        { id: 'down-game', name: 'Down Game', trend: -5, rank: 1, previousRank: 2 },
      ];

      renderComponent({ games: downGame });

      expect(screen.getByTestId('trend-icon-down')).toBeInTheDocument();
    });

    it('shows stable icon for zero trend', () => {
      const stableGame: TrendingGame[] = [
        { id: 'stable-game', name: 'Stable Game', trend: 0, rank: 1, previousRank: 1 },
      ];

      renderComponent({ games: stableGame });

      expect(screen.getByTestId('trend-icon-stable')).toBeInTheDocument();
    });

    it('displays positive trends with plus sign', () => {
      const positiveGame: TrendingGame[] = [
        { id: 'positive-game', name: 'Positive Game', trend: 8, rank: 1, previousRank: 2 },
      ];

      renderComponent({ games: positiveGame });

      const badge = screen.getByTestId('trend-badge');
      expect(badge).toHaveTextContent('+8%');
    });

    it('displays negative trends without plus sign', () => {
      const negativeGame: TrendingGame[] = [
        { id: 'negative-game', name: 'Negative Game', trend: -3, rank: 1, previousRank: 2 },
      ];

      renderComponent({ games: negativeGame });

      const badge = screen.getByTestId('trend-badge');
      expect(badge).toHaveTextContent('-3%');
    });
  });

  describe('Last Updated', () => {
    it('shows last updated timestamp when provided', () => {
      renderComponent({
        games: mockGames,
        lastUpdated: '2026-02-04T10:00:00Z',
      });

      expect(screen.getByTestId('last-updated')).toBeInTheDocument();
    });

    it('does not show last updated when not provided', () => {
      renderComponent({ games: mockGames });

      expect(screen.queryByTestId('last-updated')).not.toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('renders empty state when no games', () => {
      renderComponent({ games: [] });

      expect(screen.getByTestId('catalog-trending-empty')).toBeInTheDocument();
      expect(screen.getByText('Nessun dato trending')).toBeInTheDocument();
    });

    it('shows explore catalog CTA in empty state', () => {
      renderComponent({ games: [] });

      const cta = screen.getByTestId('explore-catalog-empty-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/games/catalog');
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      renderComponent({ games: mockGames, className: 'custom-class' });

      expect(screen.getByTestId('catalog-trending-widget')).toHaveClass('custom-class');
    });

    it('has glassmorphic styling', () => {
      renderComponent({ games: mockGames });

      const widget = screen.getByTestId('catalog-trending-widget');
      expect(widget).toHaveClass('rounded-2xl');
      expect(widget).toHaveClass('backdrop-blur-xl');
    });

    it('has gold color for rank 1', () => {
      const topGame: TrendingGame[] = [
        { id: 'top-game', name: 'Top Game', trend: 20, rank: 1, previousRank: 2 },
      ];

      renderComponent({ games: topGame });

      const rank = screen.getByTestId('trending-rank-top-game');
      expect(rank).toHaveClass('text-amber-500');
    });
  });

  describe('Accessibility', () => {
    it('has semantic section element', () => {
      renderComponent({ games: mockGames });

      expect(screen.getByTestId('catalog-trending-widget').tagName).toBe('SECTION');
    });

    it('has heading element', () => {
      renderComponent({ games: mockGames });

      expect(screen.getByRole('heading', { level: 3 })).toBeInTheDocument();
    });

    it('all links are accessible', () => {
      renderComponent({ games: mockGames });

      const links = screen.getAllByRole('link');
      links.forEach((link) => {
        expect(link).toHaveAttribute('href');
      });
    });
  });
});
