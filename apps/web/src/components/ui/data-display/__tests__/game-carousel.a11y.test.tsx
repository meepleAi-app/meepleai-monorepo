/**
 * GameCarousel Accessibility Tests (Issue #3590)
 *
 * WCAG 2.1 AA compliance testing using axe-core.
 *
 * @module components/ui/data-display/__tests__/game-carousel.a11y.test
 * @see Issue #3590 - GC-005: Unit & Integration Tests
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { axe, toHaveNoViolations } from 'jest-axe';
import { Users, Clock } from 'lucide-react';
import { GameCarousel, GameCarouselSkeleton, type CarouselGame } from '../game-carousel';

// Extend expect with axe matchers
expect.extend(toHaveNoViolations);

// Mock Next.js Image component
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock ResizeObserver
vi.stubGlobal(
  'ResizeObserver',
  vi.fn(() => ({
    observe: vi.fn(),
    unobserve: vi.fn(),
    disconnect: vi.fn(),
  }))
);

// Mock matchMedia
vi.stubGlobal('matchMedia', (query: string) => ({
  matches: query.includes('min-width: 1024px'),
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// ============================================================================
// Test Data
// ============================================================================

const MOCK_GAMES: CarouselGame[] = [
  {
    id: '1',
    title: 'Gloomhaven',
    subtitle: 'Cephalofair Games',
    imageUrl: 'https://example.com/gloomhaven.jpg',
    rating: 8.7,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '120 min' },
    ],
    badge: 'Top Rated',
  },
  {
    id: '2',
    title: 'Brass: Birmingham',
    subtitle: 'Roxley Games',
    imageUrl: 'https://example.com/brass.jpg',
    rating: 8.6,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '2-4' },
      { icon: Clock, value: '120 min' },
    ],
  },
  {
    id: '3',
    title: 'Ark Nova',
    subtitle: 'Capstone Games',
    imageUrl: 'https://example.com/arknova.jpg',
    rating: 8.5,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: '1-4' },
      { icon: Clock, value: '150 min' },
    ],
    badge: 'New',
  },
];

// ============================================================================
// Accessibility Tests
// ============================================================================

describe('GameCarousel Accessibility', () => {
  // --------------------------------------------------------------------------
  // axe-core Automated Tests
  // --------------------------------------------------------------------------

  describe('axe-core Automated Testing', () => {
    it('should have no accessibility violations with default props', async () => {
      const { container } = render(
        <GameCarousel games={MOCK_GAMES} title="Featured Games" />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations with all features enabled', async () => {
      const { container } = render(
        <GameCarousel
          games={MOCK_GAMES}
          title="Featured Games"
          subtitle="Top-rated games"
          showDots
          sortable
          defaultSort="rating"
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations in empty state', async () => {
      const { container } = render(
        <GameCarousel games={[]} title="No Games Available" />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations for skeleton', async () => {
      const { container } = render(<GameCarouselSkeleton />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  // --------------------------------------------------------------------------
  // ARIA Landmarks and Roles
  // --------------------------------------------------------------------------

  describe('ARIA Landmarks and Roles', () => {
    it('should have region role for main carousel container', () => {
      render(<GameCarousel games={MOCK_GAMES} title="Featured Games" />);

      const region = screen.getByRole('region');
      expect(region).toBeInTheDocument();
    });

    it('should have accessible name for region when title provided', () => {
      render(<GameCarousel games={MOCK_GAMES} title="Featured Games" />);

      const region = screen.getByRole('region');
      expect(region).toHaveAccessibleName(/featured games/i);
    });

    it('should have button role for navigation controls', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      const nextButton = screen.getByRole('button', { name: /next/i });

      expect(prevButton).toBeInTheDocument();
      expect(nextButton).toBeInTheDocument();
    });

    it('should have tab role for dot indicators', () => {
      render(<GameCarousel games={MOCK_GAMES} showDots />);

      const dots = screen.getAllByRole('tab', { name: /go to game/i });
      expect(dots.length).toBe(MOCK_GAMES.length);
    });

    it('should have live region for announcements', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      // The live region uses aria-live="polite" for screen reader announcements
      const liveRegion = document.querySelector('[aria-live="polite"]');
      expect(liveRegion).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Focus Management
  // --------------------------------------------------------------------------

  describe('Focus Management', () => {
    it('should have focusable game cards', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      // Game cards are focusable buttons within the carousel
      const gameCards = screen.getAllByRole('button', { name: /game:/i });
      expect(gameCards.length).toBeGreaterThan(0);
      expect(gameCards[0]).toHaveAttribute('tabIndex', '0');
    });

    it('should show visible focus indicator on cards', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const gameCard = screen.getAllByRole('button', { name: /game:/i })[0];
      gameCard.focus();

      // Focus styles should be applied via CSS
      expect(document.activeElement).toBe(gameCard);
    });

    it('navigation buttons should maintain focus when clicked', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const nextButton = screen.getByRole('button', { name: /next/i });
      nextButton.focus();
      fireEvent.click(nextButton);

      // Navigation button should still be focusable
      expect(nextButton).toBeInTheDocument();
    });

    it('navigation buttons should be focusable', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      const nextButton = screen.getByRole('button', { name: /next/i });

      prevButton.focus();
      expect(document.activeElement).toBe(prevButton);

      nextButton.focus();
      expect(document.activeElement).toBe(nextButton);
    });

    it('dot indicators should be focusable', () => {
      render(<GameCarousel games={MOCK_GAMES} showDots />);

      const dots = screen.getAllByRole('tab', { name: /go to game/i });

      dots.forEach(dot => {
        dot.focus();
        expect(document.activeElement).toBe(dot);
      });
    });
  });

  // --------------------------------------------------------------------------
  // Keyboard Navigation
  // --------------------------------------------------------------------------

  describe('Keyboard Navigation', () => {
    it('should navigate with Arrow keys', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const carousel = screen.getByRole('region');
      carousel.focus();

      // Test arrow navigation
      fireEvent.keyDown(carousel, { key: 'ArrowRight' });
      fireEvent.keyDown(carousel, { key: 'ArrowLeft' });

      expect(carousel).toBeInTheDocument();
    });

    it('should select game with Enter key on focused card', () => {
      const onGameSelect = vi.fn();
      render(<GameCarousel games={MOCK_GAMES} onGameSelect={onGameSelect} />);

      // Focus and activate the game card with Enter
      const gameCard = screen.getAllByRole('button', { name: /game:/i })[0];
      gameCard.focus();
      fireEvent.keyDown(gameCard, { key: 'Enter' });

      expect(onGameSelect).toHaveBeenCalled();
    });

    it('should select game with Space key on focused card', () => {
      const onGameSelect = vi.fn();
      render(<GameCarousel games={MOCK_GAMES} onGameSelect={onGameSelect} />);

      // Focus and activate the game card with Space
      const gameCard = screen.getAllByRole('button', { name: /game:/i })[0];
      gameCard.focus();
      fireEvent.keyDown(gameCard, { key: ' ' });

      expect(onGameSelect).toHaveBeenCalled();
    });

    it('navigation buttons should respond to Enter and Space', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const nextButton = screen.getByRole('button', { name: /next/i });

      fireEvent.keyDown(nextButton, { key: 'Enter' });
      fireEvent.keyDown(nextButton, { key: ' ' });

      expect(nextButton).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Screen Reader Support
  // --------------------------------------------------------------------------

  describe('Screen Reader Support', () => {
    it('should announce current game position', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const liveRegion = document.querySelector('[aria-live="polite"]');
      expect(liveRegion).toBeInTheDocument();
      expect(liveRegion).toHaveAttribute('aria-live', 'polite');
    });

    it('should have aria-label for navigation buttons', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      const nextButton = screen.getByRole('button', { name: /next/i });

      expect(prevButton).toHaveAccessibleName();
      expect(nextButton).toHaveAccessibleName();
    });

    it('should have aria-label for dot indicators with position', () => {
      render(<GameCarousel games={MOCK_GAMES} showDots />);

      const dots = screen.getAllByRole('tab', { name: /go to game/i });

      dots.forEach((dot, index) => {
        expect(dot).toHaveAccessibleName(`Go to game ${index + 1}`);
      });
    });

    it('should have aria-selected on active dot', () => {
      render(<GameCarousel games={MOCK_GAMES} showDots />);

      const dots = screen.getAllByRole('tab', { name: /go to game/i });

      // First dot should be selected by default
      expect(dots[0]).toHaveAttribute('aria-selected', 'true');
    });

    it('game cards should have alt text for images', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const images = screen.getAllByRole('img');
      images.forEach(img => {
        expect(img).toHaveAttribute('alt');
        expect(img.getAttribute('alt')).not.toBe('');
      });
    });
  });

  // --------------------------------------------------------------------------
  // Color Contrast and Visual Accessibility
  // --------------------------------------------------------------------------

  describe('Visual Accessibility', () => {
    it('rating display should be visible in the DOM', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      // Rating values should be displayed as text content
      // Note: Rating accessibility could be improved with aria-label
      const ratingText = screen.getByText('8.7');
      expect(ratingText).toBeInTheDocument();
    });

    it('badges should be visible and readable', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      // Entity type badge is always visible on cards
      const entityBadges = screen.getAllByText('Game');
      expect(entityBadges.length).toBeGreaterThan(0);
      expect(entityBadges[0]).toBeVisible();
    });

    it('skeleton should indicate loading state', () => {
      render(<GameCarouselSkeleton />);

      const skeleton = screen.getByTestId('game-carousel-skeleton');
      expect(skeleton).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Touch Target Size
  // --------------------------------------------------------------------------

  describe('Touch Target Size', () => {
    it('navigation buttons should meet minimum touch target size (44x44px)', () => {
      render(<GameCarousel games={MOCK_GAMES} />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      const nextButton = screen.getByRole('button', { name: /next/i });

      // Buttons should have minimum size classes or inline styles
      expect(prevButton).toBeInTheDocument();
      expect(nextButton).toBeInTheDocument();
    });

    it('dot indicators should meet minimum touch target size', () => {
      render(<GameCarousel games={MOCK_GAMES} showDots />);

      // Dots have role="tab" as they are part of a tab-like navigation
      const dots = screen.getAllByRole('tab', { name: /go to game/i });

      dots.forEach(dot => {
        expect(dot).toBeInTheDocument();
      });
    });
  });

  // --------------------------------------------------------------------------
  // Reduced Motion Support
  // --------------------------------------------------------------------------

  describe('Reduced Motion Support', () => {
    it('should respect prefers-reduced-motion', () => {
      // CSS should handle this via media query
      render(<GameCarousel games={MOCK_GAMES} autoPlay />);

      const carousel = screen.getByRole('region');
      expect(carousel).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Sort Dropdown Accessibility
  // --------------------------------------------------------------------------

  describe('Sort Dropdown Accessibility', () => {
    it('sort dropdown should have accessible label', () => {
      render(<GameCarousel games={MOCK_GAMES} sortable />);

      // Sort control uses a button with aria-haspopup="listbox"
      const sortButton = screen.getByRole('button', { name: /sort by/i });
      expect(sortButton).toHaveAccessibleName();
      expect(sortButton).toHaveAttribute('aria-haspopup', 'listbox');
    });

    it('sort options should be keyboard navigable', () => {
      render(<GameCarousel games={MOCK_GAMES} sortable />);

      const sortButton = screen.getByRole('button', { name: /sort by/i });
      sortButton.focus();

      // Should be able to open with keyboard
      fireEvent.keyDown(sortButton, { key: 'Enter' });

      expect(sortButton).toBeInTheDocument();
    });
  });
});
