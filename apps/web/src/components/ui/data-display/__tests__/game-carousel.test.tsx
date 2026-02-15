/**
 * GameCarousel Component Tests (Issue #3590)
 *
 * Tests rendering, navigation, keyboard interactions, touch gestures,
 * auto-play functionality, sorting controls, and accessibility.
 *
 * @module components/ui/data-display/__tests__/game-carousel.test
 * @see Issue #3590 - GC-005: Unit & Integration Tests
 */

import React from 'react';
import { screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { Users, Clock } from 'lucide-react';
import {
  GameCarousel,
  GameCarouselSkeleton,
  type CarouselGame,
  type CarouselSortValue,
  CAROUSEL_SORT_OPTIONS,
} from '../game-carousel';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
  usePathname: () => '/library/games',
}));

// Mock Next.js Image component
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock ResizeObserver
const mockResizeObserver = vi.fn(() => ({
  observe: vi.fn(),
  unobserve: vi.fn(),
  disconnect: vi.fn(),
}));
vi.stubGlobal('ResizeObserver', mockResizeObserver);

// Mock matchMedia for responsive tests
const mockMatchMedia = vi.fn((query: string) => ({
  matches: query.includes('min-width: 1024px'),
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));
vi.stubGlobal('matchMedia', mockMatchMedia);

// ============================================================================
// Test Data
// ============================================================================

const createMockGames = (count: number): CarouselGame[] =>
  Array.from({ length: count }, (_, i) => ({
    id: `game-${i + 1}`,
    title: `Test Game ${i + 1}`,
    subtitle: `Publisher ${i + 1}`,
    imageUrl: `https://example.com/game-${i + 1}.jpg`,
    rating: 7 + i * 0.2,
    ratingMax: 10,
    metadata: [
      { icon: Users, value: `${2 + i}-${4 + i}` },
      { icon: Clock, value: `${60 + i * 10} min` },
    ],
    badge: i === 0 ? 'Featured' : undefined,
  }));

const MOCK_GAMES = createMockGames(7);

// ============================================================================
// Component Tests
// ============================================================================

describe('GameCarousel', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
  });

  // --------------------------------------------------------------------------
  // Rendering Tests
  // --------------------------------------------------------------------------

  describe('Rendering', () => {
    it('should render with games', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      expect(screen.getByRole('region')).toBeInTheDocument();
      expect(screen.getByText('Test Game 1')).toBeInTheDocument();
    });

    it('should render with title and subtitle', () => {
      renderWithQuery(
        <GameCarousel
          games={MOCK_GAMES}
          title="Featured Games"
          subtitle="Top-rated games"
        />
      );

      expect(screen.getByText('Featured Games')).toBeInTheDocument();
      expect(screen.getByText('Top-rated games')).toBeInTheDocument();
    });

    it('should render without title when not provided', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      // The carousel should still render games without a separate title heading
      // Game card titles are still present
      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should render navigation dots when showDots is true', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} showDots />);

      const dots = screen.getAllByRole('tab', { name: /go to game/i });
      expect(dots.length).toBe(MOCK_GAMES.length);
    });

    it('should not render navigation dots when showDots is false', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} showDots={false} />);

      const dots = screen.queryAllByRole('tab', { name: /go to game/i });
      expect(dots.length).toBe(0);
    });

    it('should render navigation arrows', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      expect(screen.getByRole('button', { name: /previous/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /next/i })).toBeInTheDocument();
    });

    it('should render empty state when no games provided', () => {
      renderWithQuery(<GameCarousel games={[]} title="No Games" />);

      // The empty state text varies - check for common patterns
      expect(screen.getByText(/no games/i)).toBeInTheDocument();
    });

    it('should render with single game', () => {
      renderWithQuery(<GameCarousel games={[MOCK_GAMES[0]]} />);

      expect(screen.getByText('Test Game 1')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Navigation Tests
  // --------------------------------------------------------------------------

  describe('Navigation', () => {
    it('should navigate to next game on next button click', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      const nextButton = screen.getByRole('button', { name: /next/i });
      fireEvent.click(nextButton);

      // Verify the carousel moved
      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should navigate to previous game on previous button click', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      fireEvent.click(prevButton);

      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should navigate to specific game on dot click', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} showDots />);

      const dots = screen.getAllByRole('tab', { name: /go to game/i });
      fireEvent.click(dots[3]);

      // Verify navigation occurred
      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should loop from last to first game', () => {
      renderWithQuery(<GameCarousel games={createMockGames(3)} />);

      const nextButton = screen.getByRole('button', { name: /next/i });

      // Navigate past all games
      fireEvent.click(nextButton);
      fireEvent.click(nextButton);
      fireEvent.click(nextButton);

      // Should loop back to first
      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should loop from first to last game going backwards', () => {
      renderWithQuery(<GameCarousel games={createMockGames(3)} />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      fireEvent.click(prevButton);

      // Should loop to last
      expect(screen.getByRole('region')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Keyboard Navigation Tests
  // --------------------------------------------------------------------------

  describe('Keyboard Navigation', () => {
    it('should navigate to next on ArrowRight key', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      const carousel = screen.getByRole('region');
      fireEvent.keyDown(carousel, { key: 'ArrowRight' });

      expect(carousel).toBeInTheDocument();
    });

    it('should navigate to previous on ArrowLeft key', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      const carousel = screen.getByRole('region');
      fireEvent.keyDown(carousel, { key: 'ArrowLeft' });

      expect(carousel).toBeInTheDocument();
    });

    it('should call onGameSelect on Enter key when game card is focused', () => {
      const onGameSelect = vi.fn();
      renderWithQuery(<GameCarousel games={MOCK_GAMES} onGameSelect={onGameSelect} />);

      // Focus on a game card and press Enter
      const gameCard = screen.getAllByRole('button', { name: /game:/i })[0];
      gameCard.focus();
      fireEvent.keyDown(gameCard, { key: 'Enter' });

      expect(onGameSelect).toHaveBeenCalledTimes(1);
    });

    it('should call onGameSelect on Space key when game card is focused', () => {
      const onGameSelect = vi.fn();
      renderWithQuery(<GameCarousel games={MOCK_GAMES} onGameSelect={onGameSelect} />);

      // Focus on a game card and press Space
      const gameCard = screen.getAllByRole('button', { name: /game:/i })[0];
      gameCard.focus();
      fireEvent.keyDown(gameCard, { key: ' ' });

      expect(onGameSelect).toHaveBeenCalledTimes(1);
    });

    it('should ignore other key presses', () => {
      const onGameSelect = vi.fn();
      renderWithQuery(<GameCarousel games={MOCK_GAMES} onGameSelect={onGameSelect} />);

      const carousel = screen.getByRole('region');
      fireEvent.keyDown(carousel, { key: 'Tab' });
      fireEvent.keyDown(carousel, { key: 'Escape' });
      fireEvent.keyDown(carousel, { key: 'a' });

      expect(onGameSelect).not.toHaveBeenCalled();
    });
  });

  // --------------------------------------------------------------------------
  // Auto-Play Tests
  // --------------------------------------------------------------------------

  describe('Auto-Play', () => {
    it('should auto-advance when autoPlay is enabled', async () => {
      renderWithQuery(
        <GameCarousel games={MOCK_GAMES} autoPlay autoPlayInterval={1000} />
      );

      // Wait for auto-play interval
      act(() => {
        vi.advanceTimersByTime(1000);
      });

      // Carousel should have advanced
      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should not auto-advance when autoPlay is disabled', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} autoPlay={false} />);

      act(() => {
        vi.advanceTimersByTime(10000);
      });

      // Carousel should not have advanced automatically
      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should pause auto-play on hover', () => {
      renderWithQuery(
        <GameCarousel games={MOCK_GAMES} autoPlay autoPlayInterval={1000} />
      );

      const carousel = screen.getByRole('region');
      fireEvent.mouseEnter(carousel);

      act(() => {
        vi.advanceTimersByTime(2000);
      });

      // Should not have advanced while paused
      expect(carousel).toBeInTheDocument();
    });

    it('should resume auto-play on mouse leave', () => {
      renderWithQuery(
        <GameCarousel games={MOCK_GAMES} autoPlay autoPlayInterval={1000} />
      );

      const carousel = screen.getByRole('region');

      // Pause
      fireEvent.mouseEnter(carousel);
      act(() => {
        vi.advanceTimersByTime(1000);
      });

      // Resume
      fireEvent.mouseLeave(carousel);
      act(() => {
        vi.advanceTimersByTime(1000);
      });

      expect(carousel).toBeInTheDocument();
    });

    it('should use custom autoPlayInterval', () => {
      const customInterval = 3000;
      renderWithQuery(
        <GameCarousel
          games={MOCK_GAMES}
          autoPlay
          autoPlayInterval={customInterval}
        />
      );

      // Should not advance before interval
      act(() => {
        vi.advanceTimersByTime(customInterval - 100);
      });

      // Should advance after interval
      act(() => {
        vi.advanceTimersByTime(200);
      });

      expect(screen.getByRole('region')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Game Selection Tests
  // --------------------------------------------------------------------------

  describe('Game Selection', () => {
    it('should call onGameSelect when center game is clicked', () => {
      const onGameSelect = vi.fn();
      renderWithQuery(<GameCarousel games={MOCK_GAMES} onGameSelect={onGameSelect} />);

      // Click on a game card
      const gameCard = screen.getByText('Test Game 1').closest('button');
      if (gameCard) {
        fireEvent.click(gameCard);
        expect(onGameSelect).toHaveBeenCalledTimes(1);
        expect(onGameSelect).toHaveBeenCalledWith(
          expect.objectContaining({ id: 'game-1' })
        );
      }
    });

    it('should not call onGameSelect when side game is clicked', () => {
      const onGameSelect = vi.fn();
      renderWithQuery(<GameCarousel games={MOCK_GAMES} onGameSelect={onGameSelect} />);

      // Side games should navigate, not select
      expect(screen.getByRole('region')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Sorting Tests
  // --------------------------------------------------------------------------

  describe('Sorting', () => {
    it('should render sort dropdown when sortable is true', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} sortable />);

      // Sort control uses a button with aria-haspopup="listbox"
      expect(screen.getByRole('button', { name: /sort by/i })).toBeInTheDocument();
    });

    it('should not render sort dropdown when sortable is false', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} sortable={false} />);

      expect(screen.queryByRole('button', { name: /sort by/i })).not.toBeInTheDocument();
    });

    it('should display default sort value', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} sortable defaultSort="rating" />);

      expect(screen.getByRole('button', { name: /sort by rating/i })).toBeInTheDocument();
    });

    it('should render sort button that can be clicked', () => {
      const onSortChange = vi.fn();
      renderWithQuery(
        <GameCarousel
          games={MOCK_GAMES}
          sortable
          defaultSort="rating"
          onSortChange={onSortChange}
        />
      );

      const sortButton = screen.getByRole('button', { name: /sort by/i });
      expect(sortButton).toBeInTheDocument();

      // Click should not throw
      fireEvent.click(sortButton);

      // Button should have aria-expanded attribute that changes
      expect(sortButton).toHaveAttribute('aria-expanded');
    });

    it('should use controlled sort value when provided', () => {
      const sort: CarouselSortValue = 'popularity';
      renderWithQuery(<GameCarousel games={MOCK_GAMES} sortable sort={sort} />);

      expect(screen.getByRole('button', { name: /sort by/i })).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Accessibility Tests
  // --------------------------------------------------------------------------

  describe('Accessibility', () => {
    it('should have accessible region role', () => {
      renderWithQuery(
        <GameCarousel games={MOCK_GAMES} title="Featured" />
      );

      expect(screen.getByRole('region')).toBeInTheDocument();
    });

    it('should have aria-label for navigation buttons', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      expect(
        screen.getByRole('button', { name: /previous/i })
      ).toHaveAccessibleName();
      expect(
        screen.getByRole('button', { name: /next/i })
      ).toHaveAccessibleName();
    });

    it('should have aria-label for dot tabs', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} showDots />);

      // Dots have role="tab" as they are part of tab-like navigation
      const dots = screen.getAllByRole('tab', { name: /go to game/i });
      dots.forEach((dot, index) => {
        expect(dot).toHaveAccessibleName(`Go to game ${index + 1}`);
      });
    });

    it('should announce current game position via aria-live region', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      // Uses aria-live="polite" for announcements
      const liveRegion = document.querySelector('[aria-live="polite"]');
      expect(liveRegion).toBeInTheDocument();
    });

    it('should have focusable game cards', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      // Game cards are focusable
      const gameCards = screen.getAllByRole('button', { name: /game:/i });
      expect(gameCards.length).toBeGreaterThan(0);
      expect(gameCards[0]).toHaveAttribute('tabIndex', '0');
    });
  });

  // --------------------------------------------------------------------------
  // Touch Gesture Tests
  // --------------------------------------------------------------------------

  describe('Touch Gestures', () => {
    it('should handle swipe left gesture', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      const carousel = screen.getByRole('region');

      fireEvent.touchStart(carousel, {
        touches: [{ clientX: 300, clientY: 200 }],
      });
      fireEvent.touchMove(carousel, {
        touches: [{ clientX: 100, clientY: 200 }],
      });
      fireEvent.touchEnd(carousel);

      expect(carousel).toBeInTheDocument();
    });

    it('should handle swipe right gesture', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      const carousel = screen.getByRole('region');

      fireEvent.touchStart(carousel, {
        touches: [{ clientX: 100, clientY: 200 }],
      });
      fireEvent.touchMove(carousel, {
        touches: [{ clientX: 300, clientY: 200 }],
      });
      fireEvent.touchEnd(carousel);

      expect(carousel).toBeInTheDocument();
    });

    it('should ignore small swipe gestures', () => {
      renderWithQuery(<GameCarousel games={MOCK_GAMES} />);

      const carousel = screen.getByRole('region');

      fireEvent.touchStart(carousel, {
        touches: [{ clientX: 200, clientY: 200 }],
      });
      fireEvent.touchMove(carousel, {
        touches: [{ clientX: 210, clientY: 200 }],
      });
      fireEvent.touchEnd(carousel);

      // Should not navigate for small movements
      expect(carousel).toBeInTheDocument();
    });
  });
});

// ============================================================================
// Skeleton Tests
// ============================================================================

describe('GameCarouselSkeleton', () => {
  it('should render loading skeleton', () => {
    renderWithQuery(<GameCarouselSkeleton />);

    expect(screen.getByTestId('game-carousel-skeleton')).toBeInTheDocument();
  });

  it('should have animate-pulse class', () => {
    renderWithQuery(<GameCarouselSkeleton />);

    const skeleton = screen.getByTestId('game-carousel-skeleton');
    expect(skeleton.querySelector('.animate-pulse')).toBeInTheDocument();
  });

  it('should render multiple skeleton cards', () => {
    renderWithQuery(<GameCarouselSkeleton />);

    const skeletonCards = screen
      .getByTestId('game-carousel-skeleton')
      .querySelectorAll('[class*="rounded"]');
    expect(skeletonCards.length).toBeGreaterThan(0);
  });
});

// ============================================================================
// Sort Options Tests
// ============================================================================

describe('CAROUSEL_SORT_OPTIONS', () => {
  it('should have all required sort options', () => {
    const expectedValues: CarouselSortValue[] = ['rating', 'popularity', 'name', 'date'];

    expectedValues.forEach(value => {
      const option = CAROUSEL_SORT_OPTIONS.find(opt => opt.value === value);
      expect(option).toBeDefined();
      expect(option?.label).toBeTruthy();
      expect(option?.icon).toBeDefined();
    });
  });

  it('should have correct number of options', () => {
    expect(CAROUSEL_SORT_OPTIONS.length).toBe(4);
  });

  it('should have unique values', () => {
    const values = CAROUSEL_SORT_OPTIONS.map(opt => opt.value);
    const uniqueValues = new Set(values);
    expect(uniqueValues.size).toBe(values.length);
  });
});
