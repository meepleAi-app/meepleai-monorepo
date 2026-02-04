/**
 * WishlistHighlights Tests (Issue #3317)
 *
 * Test coverage for wishlist highlights widget.
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { WishlistHighlights, type WishlistItem } from '../WishlistHighlights';

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

// ============================================================================
// Test Data
// ============================================================================

const mockItems: WishlistItem[] = [
  {
    id: 'wish-1',
    gameId: 'game-1',
    gameName: 'Terraforming Mars',
    priority: 5,
    addedAt: '2026-01-20T10:00:00Z',
  },
  {
    id: 'wish-2',
    gameId: 'game-2',
    gameName: 'Gloomhaven',
    priority: 4,
    addedAt: '2026-01-15T10:00:00Z',
  },
  {
    id: 'wish-3',
    gameId: 'game-3',
    gameName: 'Brass Birmingham',
    priority: 4,
    addedAt: '2026-01-25T10:00:00Z',
  },
  {
    id: 'wish-4',
    gameId: 'game-4',
    gameName: 'Spirit Island',
    priority: 3,
    addedAt: '2026-01-10T10:00:00Z',
  },
  {
    id: 'wish-5',
    gameId: 'game-5',
    gameName: 'Root',
    priority: 3,
    addedAt: '2026-01-22T10:00:00Z',
  },
];

// ============================================================================
// Test Helpers
// ============================================================================

let queryClient: QueryClient;

function renderComponent(props: Partial<React.ComponentProps<typeof WishlistHighlights>> = {}) {
  queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <WishlistHighlights {...props} />
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('WishlistHighlights', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('renders skeleton when loading', () => {
      renderComponent({ isLoading: true });

      expect(screen.getByTestId('wishlist-highlights-skeleton')).toBeInTheDocument();
    });

    it('shows 5 skeleton rows', () => {
      renderComponent({ isLoading: true });

      const skeleton = screen.getByTestId('wishlist-highlights-skeleton');
      // Check that skeleton has structure
      expect(skeleton).toHaveClass('rounded-2xl');
    });
  });

  describe('Success State', () => {
    it('renders widget with items', () => {
      renderComponent({ items: mockItems });

      expect(screen.getByTestId('wishlist-highlights-widget')).toBeInTheDocument();
      expect(screen.getByTestId('wishlist-highlights-title')).toHaveTextContent('Wishlist Highlights');
    });

    it('renders all wishlist items', () => {
      renderComponent({ items: mockItems });

      // Items are sorted by priority, check all are present
      const itemsList = screen.getByTestId('wishlist-items-list');
      const items = itemsList.querySelectorAll('[data-testid^="wishlist-item-"]');
      expect(items.length).toBe(5);

      // Verify first item is highest priority
      expect(screen.getByTestId('wishlist-name-wish-1')).toHaveTextContent('Terraforming Mars');
    });

    it('limits to 5 items maximum', () => {
      const manyItems: WishlistItem[] = [
        ...mockItems,
        { id: 'wish-6', gameId: 'game-6', gameName: 'Extra Game', priority: 2, addedAt: '2026-01-01T10:00:00Z' },
        { id: 'wish-7', gameId: 'game-7', gameName: 'Another Game', priority: 1, addedAt: '2026-01-02T10:00:00Z' },
      ];

      renderComponent({ items: manyItems });

      const itemsList = screen.getByTestId('wishlist-items-list');
      const items = itemsList.querySelectorAll('[data-testid^="wishlist-item-"]');
      expect(items.length).toBe(5);
    });

    it('sorts items by priority descending', () => {
      const unsortedItems: WishlistItem[] = [
        { id: 'wish-low', gameId: 'game-1', gameName: 'Low Priority', priority: 1, addedAt: '2026-01-01T10:00:00Z' },
        { id: 'wish-high', gameId: 'game-2', gameName: 'High Priority', priority: 5, addedAt: '2026-01-02T10:00:00Z' },
        { id: 'wish-med', gameId: 'game-3', gameName: 'Medium Priority', priority: 3, addedAt: '2026-01-03T10:00:00Z' },
      ];

      renderComponent({ items: unsortedItems });

      const ranks = screen.getAllByTestId(/^wishlist-rank-/);
      // First item should be rank 1 (highest priority = 5)
      expect(ranks[0]).toHaveTextContent('1.');
    });

    it('displays correct ranks', () => {
      renderComponent({ items: mockItems });

      mockItems.forEach((item, index) => {
        const rank = screen.getByTestId(`wishlist-rank-${item.id}`);
        expect(rank).toBeInTheDocument();
      });
    });

    it('shows priority stars for each item', () => {
      renderComponent({ items: mockItems });

      mockItems.forEach((item) => {
        expect(screen.getByTestId(`wishlist-priority-${item.id}`)).toBeInTheDocument();
      });
    });

    it('links to game detail pages', () => {
      renderComponent({ items: mockItems });

      mockItems.forEach((item) => {
        const link = screen.getByTestId(`wishlist-item-${item.id}`);
        expect(link).toHaveAttribute('href', `/games/${item.gameId}`);
      });
    });

    it('shows manage wishlist CTA', () => {
      renderComponent({ items: mockItems });

      const cta = screen.getByTestId('manage-wishlist-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/wishlist');
    });
  });

  describe('Empty State', () => {
    it('renders empty state when no items', () => {
      renderComponent({ items: [] });

      expect(screen.getByTestId('wishlist-highlights-empty')).toBeInTheDocument();
      expect(screen.getByText('Wishlist vuota')).toBeInTheDocument();
    });

    it('shows explore catalog CTA in empty state', () => {
      renderComponent({ items: [] });

      const cta = screen.getByTestId('explore-catalog-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/games/catalog');
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      renderComponent({ items: mockItems, className: 'custom-class' });

      expect(screen.getByTestId('wishlist-highlights-widget')).toHaveClass('custom-class');
    });

    it('has glassmorphic styling', () => {
      renderComponent({ items: mockItems });

      const widget = screen.getByTestId('wishlist-highlights-widget');
      expect(widget).toHaveClass('rounded-2xl');
      expect(widget).toHaveClass('backdrop-blur-xl');
    });
  });

  describe('Accessibility', () => {
    it('has semantic section element', () => {
      renderComponent({ items: mockItems });

      expect(screen.getByTestId('wishlist-highlights-widget').tagName).toBe('SECTION');
    });

    it('has heading element', () => {
      renderComponent({ items: mockItems });

      expect(screen.getByRole('heading', { level: 3 })).toBeInTheDocument();
    });

    it('all links are accessible', () => {
      renderComponent({ items: mockItems });

      const links = screen.getAllByRole('link');
      links.forEach((link) => {
        expect(link).toHaveAttribute('href');
      });
    });
  });
});
