/**
 * CollectionStatsBar Unit Tests (Issue #3649: User Collection Dashboard Enhancement)
 *
 * Coverage areas:
 * - Stats rendering (totalGames, privatePdfsCount, totalSessions)
 * - Loading/skeleton state
 * - Accessibility attributes
 * - Responsive layout
 *
 * Target: 85%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CollectionStatsBar } from '../CollectionStatsBar';
import type { CollectionHeroStats } from '@/types/collection';

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => children,
}));

// ============================================================================
// Test Data
// ============================================================================

const mockStats: CollectionHeroStats = {
  totalGames: 42,
  privatePdfsCount: 15,
  totalSessions: 128,
  gamesPlayedThisMonth: 8,
  totalPlayTime: 3600,
};

const mockStatsZero: CollectionHeroStats = {
  totalGames: 0,
  privatePdfsCount: 0,
  totalSessions: 0,
};

const mockStatsLarge: CollectionHeroStats = {
  totalGames: 1234,
  privatePdfsCount: 567,
  totalSessions: 8901,
};

describe('CollectionStatsBar', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders stats bar section', () => {
      render(<CollectionStatsBar stats={mockStats} />);

      expect(screen.getByTestId('collection-stats-bar')).toBeInTheDocument();
    });

    it('renders all three stat items', () => {
      render(<CollectionStatsBar stats={mockStats} />);

      expect(screen.getByTestId('stat-item-giochi')).toBeInTheDocument();
      expect(screen.getByTestId('stat-item-pdf-privati')).toBeInTheDocument();
      expect(screen.getByTestId('stat-item-sessioni')).toBeInTheDocument();
    });

    it('displays correct values for each stat', () => {
      render(<CollectionStatsBar stats={mockStats} />);

      expect(screen.getByTestId('stat-value-giochi')).toHaveTextContent('42');
      expect(screen.getByTestId('stat-value-pdf-privati')).toHaveTextContent('15');
      expect(screen.getByTestId('stat-value-sessioni')).toHaveTextContent('128');
    });

    it('handles zero values correctly', () => {
      render(<CollectionStatsBar stats={mockStatsZero} />);

      expect(screen.getByTestId('stat-value-giochi')).toHaveTextContent('0');
      expect(screen.getByTestId('stat-value-pdf-privati')).toHaveTextContent('0');
      expect(screen.getByTestId('stat-value-sessioni')).toHaveTextContent('0');
    });

    it('handles large values correctly', () => {
      render(<CollectionStatsBar stats={mockStatsLarge} />);

      expect(screen.getByTestId('stat-value-giochi')).toHaveTextContent('1234');
      expect(screen.getByTestId('stat-value-pdf-privati')).toHaveTextContent('567');
      expect(screen.getByTestId('stat-value-sessioni')).toHaveTextContent('8901');
    });

    it('applies custom className', () => {
      const { container } = render(
        <CollectionStatsBar stats={mockStats} className="custom-stats-bar" />
      );

      const section = container.querySelector('.custom-stats-bar');
      expect(section).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading is true', () => {
      render(<CollectionStatsBar isLoading />);

      expect(screen.getByTestId('collection-stats-bar-skeleton')).toBeInTheDocument();
      expect(screen.queryByTestId('collection-stats-bar')).not.toBeInTheDocument();
    });

    it('renders skeleton when stats is null', () => {
      render(<CollectionStatsBar stats={null} />);

      expect(screen.getByTestId('collection-stats-bar-skeleton')).toBeInTheDocument();
    });

    it('renders skeleton when stats is undefined', () => {
      render(<CollectionStatsBar />);

      expect(screen.getByTestId('collection-stats-bar-skeleton')).toBeInTheDocument();
    });

    it('skeleton has three placeholder items', () => {
      render(<CollectionStatsBar isLoading />);

      const skeleton = screen.getByTestId('collection-stats-bar-skeleton');
      const placeholders = skeleton.querySelectorAll('.bg-muted\\/50');
      expect(placeholders).toHaveLength(3);
    });

    it('renders content when isLoading is false', () => {
      render(<CollectionStatsBar stats={mockStats} isLoading={false} />);

      expect(screen.queryByTestId('collection-stats-bar-skeleton')).not.toBeInTheDocument();
      expect(screen.getByTestId('collection-stats-bar')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has correct aria-label on section', () => {
      render(<CollectionStatsBar stats={mockStats} />);

      const section = screen.getByTestId('collection-stats-bar');
      expect(section).toHaveAttribute('aria-label', 'Statistiche collezione');
    });

    it('is rendered as a section element', () => {
      render(<CollectionStatsBar stats={mockStats} />);

      const section = screen.getByTestId('collection-stats-bar');
      expect(section.tagName).toBe('SECTION');
    });

    it('stat labels are visible', () => {
      render(<CollectionStatsBar stats={mockStats} />);

      expect(screen.getByText('Giochi')).toBeInTheDocument();
      expect(screen.getByText('PDF Privati')).toBeInTheDocument();
      expect(screen.getByText('Sessioni')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Layout Tests
  // ============================================================================

  describe('Layout', () => {
    it('uses flex layout with gap', () => {
      render(<CollectionStatsBar stats={mockStats} />);

      const section = screen.getByTestId('collection-stats-bar');
      expect(section).toHaveClass('flex');
      expect(section).toHaveClass('gap-3');
    });

    it('allows flex wrapping', () => {
      render(<CollectionStatsBar stats={mockStats} />);

      const section = screen.getByTestId('collection-stats-bar');
      expect(section).toHaveClass('flex-wrap');
    });
  });
});
