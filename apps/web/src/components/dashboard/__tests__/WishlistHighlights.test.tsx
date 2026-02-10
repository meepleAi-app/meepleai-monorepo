/**
 * WishlistHighlights Tests (Issue #3920)
 *
 * Test coverage for wishlist highlights widget:
 * - Renders top 5 items sorted by priority
 * - Priority badges colored correctly (HIGH=rose, MEDIUM=amber, LOW=emerald)
 * - Empty state shown when no items
 * - Game covers displayed
 * - Target price display
 * - Mark as purchased quick action
 * - Loading skeleton state
 * - Accessibility
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  WishlistHighlights,
  type WishlistHighlightItem,
} from '../WishlistHighlights';

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

// Mock Next.js Image
vi.mock('next/image', () => ({
  default: ({
    src,
    alt,
    ...props
  }: {
    src: string;
    alt: string;
    [key: string]: unknown;
  }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({
      children,
      ...props
    }: React.HTMLAttributes<HTMLDivElement>) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const mockItems: WishlistHighlightItem[] = [
  {
    id: 'wish-1',
    game: { id: 'game-1', name: 'Terraforming Mars', coverUrl: '/covers/terraforming.jpg' },
    priority: 'HIGH',
    targetPrice: 49.99,
  },
  {
    id: 'wish-2',
    game: { id: 'game-2', name: 'Gloomhaven', coverUrl: '/covers/gloomhaven.jpg' },
    priority: 'HIGH',
    targetPrice: 89.99,
  },
  {
    id: 'wish-3',
    game: { id: 'game-3', name: 'Brass Birmingham', coverUrl: '/covers/brass.jpg' },
    priority: 'MEDIUM',
  },
  {
    id: 'wish-4',
    game: { id: 'game-4', name: 'Spirit Island', coverUrl: '/covers/spirit.jpg' },
    priority: 'MEDIUM',
    targetPrice: 59.99,
  },
  {
    id: 'wish-5',
    game: { id: 'game-5', name: 'Root', coverUrl: '/covers/root.jpg' },
    priority: 'LOW',
  },
];

// ============================================================================
// Tests
// ============================================================================

describe('WishlistHighlights', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('renders skeleton when loading', () => {
      render(<WishlistHighlights isLoading />);

      expect(
        screen.getByTestId('wishlist-highlights-skeleton')
      ).toBeInTheDocument();
    });

    it('skeleton has glassmorphic styling', () => {
      render(<WishlistHighlights isLoading />);

      const skeleton = screen.getByTestId('wishlist-highlights-skeleton');
      expect(skeleton).toHaveClass('rounded-2xl');
    });
  });

  describe('Success State', () => {
    it('renders widget with items', () => {
      render(<WishlistHighlights items={mockItems} />);

      expect(
        screen.getByTestId('wishlist-highlights-widget')
      ).toBeInTheDocument();
      expect(
        screen.getByTestId('wishlist-highlights-title')
      ).toHaveTextContent('Wishlist Highlights');
    });

    it('renders all 5 wishlist items', () => {
      render(<WishlistHighlights items={mockItems} />);

      const itemsList = screen.getByTestId('wishlist-items-list');
      const items = itemsList.querySelectorAll(
        '[data-testid^="wishlist-item-"]'
      );
      expect(items.length).toBe(5);
    });

    it('limits to 5 items maximum', () => {
      const manyItems: WishlistHighlightItem[] = [
        ...mockItems,
        {
          id: 'wish-6',
          game: { id: 'game-6', name: 'Extra Game', coverUrl: '/covers/extra.jpg' },
          priority: 'LOW',
        },
        {
          id: 'wish-7',
          game: { id: 'game-7', name: 'Another Game', coverUrl: '/covers/another.jpg' },
          priority: 'LOW',
        },
      ];

      render(<WishlistHighlights items={manyItems} />);

      const itemsList = screen.getByTestId('wishlist-items-list');
      const items = itemsList.querySelectorAll(
        '[data-testid^="wishlist-item-"]'
      );
      expect(items.length).toBe(5);
    });

    it('sorts items by priority descending (HIGH > MEDIUM > LOW)', () => {
      const unsortedItems: WishlistHighlightItem[] = [
        {
          id: 'wish-low',
          game: { id: 'game-1', name: 'Low Priority', coverUrl: '/covers/low.jpg' },
          priority: 'LOW',
        },
        {
          id: 'wish-high',
          game: { id: 'game-2', name: 'High Priority', coverUrl: '/covers/high.jpg' },
          priority: 'HIGH',
        },
        {
          id: 'wish-med',
          game: { id: 'game-3', name: 'Medium Priority', coverUrl: '/covers/med.jpg' },
          priority: 'MEDIUM',
        },
      ];

      render(<WishlistHighlights items={unsortedItems} />);

      // First item should be HIGH priority
      const names = screen.getAllByTestId(/^wishlist-name-/);
      expect(names[0]).toHaveTextContent('High Priority');
      expect(names[1]).toHaveTextContent('Medium Priority');
      expect(names[2]).toHaveTextContent('Low Priority');
    });

    it('displays game names', () => {
      render(<WishlistHighlights items={mockItems} />);

      expect(
        screen.getByTestId('wishlist-name-wish-1')
      ).toHaveTextContent('Terraforming Mars');
      expect(
        screen.getByTestId('wishlist-name-wish-5')
      ).toHaveTextContent('Root');
    });

    it('shows manage wishlist link in header', () => {
      render(<WishlistHighlights items={mockItems} />);

      const link = screen.getByTestId('view-wishlist-link');
      expect(link).toBeInTheDocument();
      expect(link).toHaveAttribute('href', '/wishlist');
    });
  });

  describe('Game Covers', () => {
    it('renders cover images for items with coverUrl', () => {
      render(<WishlistHighlights items={mockItems} />);

      const coverLink = screen.getByTestId('wishlist-cover-wish-1');
      const img = coverLink.querySelector('img');
      expect(img).toBeInTheDocument();
      expect(img).toHaveAttribute('alt', 'Terraforming Mars');
    });

    it('cover links to game detail page', () => {
      render(<WishlistHighlights items={mockItems} />);

      const coverLink = screen.getByTestId('wishlist-cover-wish-1');
      expect(coverLink).toHaveAttribute('href', '/games/game-1');
    });

    it('shows placeholder when no coverUrl', () => {
      const itemsNoCover: WishlistHighlightItem[] = [
        {
          id: 'wish-no-cover',
          game: { id: 'game-nc', name: 'No Cover Game', coverUrl: '' },
          priority: 'HIGH',
        },
      ];

      render(<WishlistHighlights items={itemsNoCover} />);

      const coverLink = screen.getByTestId('wishlist-cover-wish-no-cover');
      const img = coverLink.querySelector('img');
      expect(img).toBeNull();
    });
  });

  describe('Priority Badges', () => {
    it('renders priority badges', () => {
      render(<WishlistHighlights items={mockItems} />);

      const badges = screen.getAllByTestId('priority-badge');
      expect(badges.length).toBe(5);
    });

    it('HIGH priority shows "Alta" label with rose colors', () => {
      const highItems: WishlistHighlightItem[] = [
        {
          id: 'wish-high',
          game: { id: 'game-1', name: 'High Game', coverUrl: '/c.jpg' },
          priority: 'HIGH',
        },
      ];

      render(<WishlistHighlights items={highItems} />);

      const badge = screen.getByTestId('priority-badge');
      expect(badge).toHaveTextContent('Alta');
      expect(badge.className).toContain('bg-rose-100');
    });

    it('MEDIUM priority shows "Media" label with amber colors', () => {
      const medItems: WishlistHighlightItem[] = [
        {
          id: 'wish-med',
          game: { id: 'game-1', name: 'Med Game', coverUrl: '/c.jpg' },
          priority: 'MEDIUM',
        },
      ];

      render(<WishlistHighlights items={medItems} />);

      const badge = screen.getByTestId('priority-badge');
      expect(badge).toHaveTextContent('Media');
      expect(badge.className).toContain('bg-amber-100');
    });

    it('LOW priority shows "Bassa" label with emerald colors', () => {
      const lowItems: WishlistHighlightItem[] = [
        {
          id: 'wish-low',
          game: { id: 'game-1', name: 'Low Game', coverUrl: '/c.jpg' },
          priority: 'LOW',
        },
      ];

      render(<WishlistHighlights items={lowItems} />);

      const badge = screen.getByTestId('priority-badge');
      expect(badge).toHaveTextContent('Bassa');
      expect(badge.className).toContain('bg-emerald-100');
    });
  });

  describe('Target Price', () => {
    it('displays target price when provided', () => {
      render(<WishlistHighlights items={mockItems} />);

      const price = screen.getByTestId('wishlist-price-wish-1');
      expect(price).toHaveTextContent('Target: 49.99');
    });

    it('does not display price when not provided', () => {
      render(<WishlistHighlights items={mockItems} />);

      // wish-3 has no targetPrice
      expect(
        screen.queryByTestId('wishlist-price-wish-3')
      ).not.toBeInTheDocument();
    });
  });

  describe('Mark as Purchased Action', () => {
    it('shows purchase button when onMarkPurchased provided', () => {
      const handlePurchased = vi.fn();

      render(
        <WishlistHighlights
          items={mockItems}
          onMarkPurchased={handlePurchased}
        />
      );

      const btn = screen.getByTestId('wishlist-purchased-wish-1');
      expect(btn).toBeInTheDocument();
    });

    it('calls onMarkPurchased with item id when clicked', () => {
      const handlePurchased = vi.fn();

      render(
        <WishlistHighlights
          items={mockItems}
          onMarkPurchased={handlePurchased}
        />
      );

      const btn = screen.getByTestId('wishlist-purchased-wish-1');
      fireEvent.click(btn);

      expect(handlePurchased).toHaveBeenCalledWith('wish-1');
      expect(handlePurchased).toHaveBeenCalledTimes(1);
    });

    it('does not show purchase button when no callback', () => {
      render(<WishlistHighlights items={mockItems} />);

      expect(
        screen.queryByTestId('wishlist-purchased-wish-1')
      ).not.toBeInTheDocument();
    });

    it('has accessible aria-label on purchase button', () => {
      const handlePurchased = vi.fn();

      render(
        <WishlistHighlights
          items={mockItems}
          onMarkPurchased={handlePurchased}
        />
      );

      const btn = screen.getByTestId('wishlist-purchased-wish-1');
      expect(btn).toHaveAttribute(
        'aria-label',
        'Segna Terraforming Mars come acquistato'
      );
    });
  });

  describe('Empty State', () => {
    it('renders empty state when no items', () => {
      render(<WishlistHighlights items={[]} />);

      expect(
        screen.getByTestId('wishlist-highlights-empty')
      ).toBeInTheDocument();
      expect(screen.getByText('Wishlist vuota')).toBeInTheDocument();
    });

    it('shows explore catalog CTA in empty state', () => {
      render(<WishlistHighlights items={[]} />);

      const cta = screen.getByTestId('explore-catalog-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/games/catalog');
    });

    it('empty state CTA says "Aggiungi primo gioco"', () => {
      render(<WishlistHighlights items={[]} />);

      expect(
        screen.getByText('Aggiungi primo gioco')
      ).toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      render(
        <WishlistHighlights items={mockItems} className="custom-class" />
      );

      expect(
        screen.getByTestId('wishlist-highlights-widget')
      ).toHaveClass('custom-class');
    });

    it('has glassmorphic styling', () => {
      render(<WishlistHighlights items={mockItems} />);

      const widget = screen.getByTestId('wishlist-highlights-widget');
      expect(widget).toHaveClass('rounded-2xl');
      expect(widget).toHaveClass('backdrop-blur-xl');
    });
  });

  describe('Accessibility', () => {
    it('has semantic section element', () => {
      render(<WishlistHighlights items={mockItems} />);

      expect(
        screen.getByTestId('wishlist-highlights-widget').tagName
      ).toBe('SECTION');
    });

    it('has heading element', () => {
      render(<WishlistHighlights items={mockItems} />);

      expect(
        screen.getByRole('heading', { level: 3 })
      ).toBeInTheDocument();
    });

    it('all links are accessible with href', () => {
      render(<WishlistHighlights items={mockItems} />);

      const links = screen.getAllByRole('link');
      links.forEach((link) => {
        expect(link).toHaveAttribute('href');
      });
    });
  });
});
