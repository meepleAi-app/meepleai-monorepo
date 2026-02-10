/**
 * LibrarySnapshot Unit Tests (Issue #3310)
 *
 * Coverage areas:
 * - Rendering with data
 * - Empty state
 * - Loading state
 * - Quota progress bar with dynamic colors
 * - Top games display (cover, title, rating, plays)
 * - Navigation links
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibrarySnapshot, type TopGame, type LibraryQuota } from '../LibrarySnapshot';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, onClick, ...props }: { children: React.ReactNode; href: string; onClick?: () => void; [key: string]: unknown }) => (
    <a href={href} onClick={onClick} {...props}>{children}</a>
  ),
}));

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string; [key: string]: unknown }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const mockQuota: LibraryQuota = {
  used: 127,
  total: 200,
};

const mockTopGames: TopGame[] = [
  {
    id: 'game-1',
    title: 'Catan',
    coverUrl: '/images/catan.jpg',
    rating: 5,
    playCount: 45,
  },
  {
    id: 'game-2',
    title: 'Ticket to Ride',
    coverUrl: '/images/ticket.jpg',
    rating: 4,
    playCount: 32,
  },
  {
    id: 'game-3',
    title: 'Azul',
    coverUrl: '/images/azul.jpg',
    rating: 4,
    playCount: 28,
  },
];

const lowQuota: LibraryQuota = { used: 30, total: 200 }; // 15% - green
const mediumQuota: LibraryQuota = { used: 140, total: 200 }; // 70% - amber
const highQuota: LibraryQuota = { used: 180, total: 200 }; // 90% - red

describe('LibrarySnapshot', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders widget container', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      expect(screen.getByTestId('library-snapshot-widget')).toBeInTheDocument();
    });

    it('renders widget title', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      expect(screen.getByTestId('library-snapshot-title')).toHaveTextContent('La Mia Collezione');
    });

    it('renders max 3 game cards', () => {
      const fourGames = [
        ...mockTopGames,
        { id: 'game-4', title: 'Extra Game', rating: 3, playCount: 10 },
      ];
      render(<LibrarySnapshot quota={mockQuota} topGames={fourGames} />);

      expect(screen.getByTestId('top-game-game-1')).toBeInTheDocument();
      expect(screen.getByTestId('top-game-game-2')).toBeInTheDocument();
      expect(screen.getByTestId('top-game-game-3')).toBeInTheDocument();
      expect(screen.queryByTestId('top-game-game-4')).not.toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(
        <LibrarySnapshot quota={mockQuota} topGames={mockTopGames} className="custom-widget" />
      );

      expect(container.querySelector('.custom-widget')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Quota Progress Bar Tests
  // ============================================================================

  describe('Quota Progress Bar', () => {
    it('displays quota text correctly', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      const quotaText = screen.getByTestId('quota-text');
      expect(quotaText).toHaveTextContent('127/200 (64%)');
    });

    it('renders progress bar', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      expect(screen.getByTestId('quota-progress')).toBeInTheDocument();
    });

    it('displays green color for low quota (<50%)', () => {
      render(<LibrarySnapshot quota={lowQuota} topGames={mockTopGames} />);

      const quotaText = screen.getByTestId('quota-text');
      expect(quotaText).toHaveClass('text-emerald-600');
    });

    it('displays amber color for medium quota (50-80%)', () => {
      render(<LibrarySnapshot quota={mediumQuota} topGames={mockTopGames} />);

      const quotaText = screen.getByTestId('quota-text');
      expect(quotaText).toHaveClass('text-amber-600');
    });

    it('displays red color for high quota (>80%)', () => {
      render(<LibrarySnapshot quota={highQuota} topGames={mockTopGames} />);

      const quotaText = screen.getByTestId('quota-text');
      expect(quotaText).toHaveClass('text-red-600');
    });

    it('applies emerald color to progress bar indicator for low quota', () => {
      render(<LibrarySnapshot quota={lowQuota} topGames={mockTopGames} />);

      const progress = screen.getByTestId('quota-progress');
      expect(progress).toHaveClass('[&>div]:bg-emerald-500');
    });

    it('applies amber color to progress bar indicator for medium quota', () => {
      render(<LibrarySnapshot quota={mediumQuota} topGames={mockTopGames} />);

      const progress = screen.getByTestId('quota-progress');
      expect(progress).toHaveClass('[&>div]:bg-amber-500');
    });

    it('applies red color to progress bar indicator for high quota', () => {
      render(<LibrarySnapshot quota={highQuota} topGames={mockTopGames} />);

      const progress = screen.getByTestId('quota-progress');
      expect(progress).toHaveClass('[&>div]:bg-red-500');
    });
  });

  // ============================================================================
  // Top Games Tests
  // ============================================================================

  describe('Top Games', () => {
    it('displays game title', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      expect(screen.getByTestId('game-title-game-1')).toHaveTextContent('Catan');
      expect(screen.getByTestId('game-title-game-2')).toHaveTextContent('Ticket to Ride');
      expect(screen.getByTestId('game-title-game-3')).toHaveTextContent('Azul');
    });

    it('displays play count', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      expect(screen.getByTestId('game-plays-game-1')).toHaveTextContent('45 partite');
      expect(screen.getByTestId('game-plays-game-2')).toHaveTextContent('32 partite');
      expect(screen.getByTestId('game-plays-game-3')).toHaveTextContent('28 partite');
    });

    it('displays rating stars', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      // Check that rating containers exist
      expect(screen.getByTestId('game-rating-game-1')).toBeInTheDocument();
      expect(screen.getByTestId('game-rating-game-2')).toBeInTheDocument();
      expect(screen.getByTestId('game-rating-game-3')).toBeInTheDocument();
    });

    it('renders cover images', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      const images = screen.getAllByRole('img');
      expect(images.length).toBe(3);
      expect(images[0]).toHaveAttribute('src', '/images/catan.jpg');
      expect(images[0]).toHaveAttribute('alt', 'Catan');
    });

    it('renders placeholder for games without cover', () => {
      const gamesWithoutCover = [
        { id: 'game-no-cover', title: 'No Cover Game', rating: 3, playCount: 10 },
      ];
      render(<LibrarySnapshot quota={mockQuota} topGames={gamesWithoutCover} />);

      // Should not have img element for this game
      expect(screen.queryByRole('img')).not.toBeInTheDocument();
      // But should still show game info
      expect(screen.getByTestId('game-title-game-no-cover')).toHaveTextContent('No Cover Game');
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('Navigation', () => {
    it('view library link has correct href', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      const viewLink = screen.getByTestId('view-library-link');
      expect(viewLink).toHaveAttribute('href', '/library');
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('renders empty state when no games', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={[]} />);

      expect(screen.getByTestId('library-snapshot-empty')).toBeInTheDocument();
    });

    it('shows empty message', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={[]} />);

      expect(screen.getByText('Nessun gioco nella collezione')).toBeInTheDocument();
    });

    it('shows add first game CTA', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={[]} />);

      const cta = screen.getByTestId('add-first-game-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/library/add');
    });

    it('does not show game cards in empty state', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={[]} />);

      expect(screen.queryByTestId('top-games-list')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading', () => {
      render(<LibrarySnapshot isLoading />);

      expect(screen.getByTestId('library-snapshot-skeleton')).toBeInTheDocument();
    });

    it('does not render content when loading', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} isLoading />);

      expect(screen.queryByTestId('library-snapshot-widget')).not.toBeInTheDocument();
      expect(screen.queryByTestId('top-game-game-1')).not.toBeInTheDocument();
    });

    it('skeleton has glassmorphic styling', () => {
      render(<LibrarySnapshot isLoading />);

      const skeleton = screen.getByTestId('library-snapshot-skeleton');
      expect(skeleton).toHaveClass('backdrop-blur-xl');
    });
  });

  // ============================================================================
  // Default Props Tests
  // ============================================================================

  describe('Default Props', () => {
    it('shows empty state when props not provided', () => {
      render(<LibrarySnapshot />);

      // Should render with empty defaults (no mock data)
      expect(screen.getByTestId('library-snapshot-widget')).toBeInTheDocument();
      expect(screen.getByTestId('library-snapshot-empty')).toBeInTheDocument();
      expect(screen.getByTestId('quota-text')).toHaveTextContent('0/0 (0%)');
    });

    it('isLoading defaults to false', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      expect(screen.queryByTestId('library-snapshot-skeleton')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Styling Tests
  // ============================================================================

  describe('Styling', () => {
    it('widget has glassmorphic styling', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      const widget = screen.getByTestId('library-snapshot-widget');
      expect(widget).toHaveClass('backdrop-blur-xl');
      expect(widget).toHaveClass('rounded-2xl');
    });

    it('header icon has amber styling', () => {
      const { container } = render(
        <LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />
      );

      const iconContainer = container.querySelector('.bg-amber-500\\/20');
      expect(iconContainer).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('handles 100% quota', () => {
      const fullQuota = { used: 200, total: 200 };
      render(<LibrarySnapshot quota={fullQuota} topGames={mockTopGames} />);

      const quotaText = screen.getByTestId('quota-text');
      expect(quotaText).toHaveTextContent('200/200 (100%)');
      expect(quotaText).toHaveClass('text-red-600');
    });

    it('handles 0% quota', () => {
      const emptyQuota = { used: 0, total: 200 };
      render(<LibrarySnapshot quota={emptyQuota} topGames={mockTopGames} />);

      const quotaText = screen.getByTestId('quota-text');
      expect(quotaText).toHaveTextContent('0/200 (0%)');
      expect(quotaText).toHaveClass('text-emerald-600');
    });

    it('handles exactly 50% quota (amber)', () => {
      const fiftyPercentQuota = { used: 100, total: 200 };
      render(<LibrarySnapshot quota={fiftyPercentQuota} topGames={mockTopGames} />);

      const quotaText = screen.getByTestId('quota-text');
      expect(quotaText).toHaveTextContent('100/200 (50%)');
      expect(quotaText).toHaveClass('text-amber-600');
    });

    it('handles exactly 80% quota (red)', () => {
      const eightyPercentQuota = { used: 160, total: 200 };
      render(<LibrarySnapshot quota={eightyPercentQuota} topGames={mockTopGames} />);

      const quotaText = screen.getByTestId('quota-text');
      expect(quotaText).toHaveTextContent('160/200 (80%)');
      expect(quotaText).toHaveClass('text-red-600');
    });

    it('handles single game', () => {
      const singleGame = [mockTopGames[0]];
      render(<LibrarySnapshot quota={mockQuota} topGames={singleGame} />);

      expect(screen.getByTestId('top-game-game-1')).toBeInTheDocument();
      expect(screen.queryByTestId('top-game-game-2')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Analytics Tracking Tests (Issue #3982)
  // ============================================================================

  describe('Analytics Tracking', () => {
    it('calls onNavigate when clicking view library link', () => {
      const onNavigate = vi.fn();
      render(
        <LibrarySnapshot quota={mockQuota} topGames={mockTopGames} onNavigate={onNavigate} />
      );

      const viewLink = screen.getByTestId('view-library-link');
      fireEvent.click(viewLink);

      expect(onNavigate).toHaveBeenCalledWith('/library', 'library_snapshot');
    });

    it('does not error when onNavigate is not provided', () => {
      render(<LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />);

      const viewLink = screen.getByTestId('view-library-link');
      expect(() => fireEvent.click(viewLink)).not.toThrow();
    });
  });
});
