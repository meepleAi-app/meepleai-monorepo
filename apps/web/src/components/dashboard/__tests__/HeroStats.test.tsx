/**
 * HeroStats Unit Tests (Issue #3308: HeroStats Component)
 *
 * Coverage areas:
 * - Greeting with user name
 * - Last access timestamp formatting
 * - 4 KPI cards rendering
 * - Loading/skeleton state
 * - Responsive grid layout
 * - Time-based greetings
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { HeroStats, type DashboardStats } from '../HeroStats';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
  },
  AnimatePresence: ({ children }: any) => children,
}));

// ============================================================================
// Test Data
// ============================================================================

const mockStats: DashboardStats = {
  collection: { total: 127, trend: 3 },
  played: { total: 23, streak: 7 },
  chats: { total: 12 },
  wishlist: { total: 15, trend: 2 },
  lastAccess: new Date(Date.now() - 1000 * 60 * 30).toISOString(), // 30 minutes ago
  userName: 'Marco',
};

const mockStatsNoStreak: DashboardStats = {
  ...mockStats,
  played: { total: 23, streak: 0 },
};

const mockStatsNegativeTrend: DashboardStats = {
  ...mockStats,
  collection: { total: 127, trend: -5 },
  wishlist: { total: 15, trend: -3 },
};

describe('HeroStats', () => {
  // ============================================================================
  // Time Mocking
  // ============================================================================

  beforeEach(() => {
    // Default to afternoon (14:00)
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-01-21T14:00:00'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders hero stats section', () => {
      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-stats')).toBeInTheDocument();
    });

    it('renders greeting with user name', () => {
      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Marco');
    });

    it('renders last access timestamp', () => {
      render(<HeroStats stats={mockStats} />);

      const lastAccess = screen.getByTestId('hero-last-access');
      expect(lastAccess).toHaveTextContent('Ultimo accesso:');
    });

    it('renders 4 KPI cards', () => {
      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('kpi-collection')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-played')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-chats')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-wishlist')).toBeInTheDocument();
    });

    it('renders KPI grid container', () => {
      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-kpi-grid')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(
        <HeroStats stats={mockStats} className="custom-hero" />
      );

      const section = container.querySelector('.custom-hero');
      expect(section).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Greeting Tests
  // ============================================================================

  describe('Time-based Greetings', () => {
    it('shows Buongiorno in the morning (8:00)', () => {
      vi.setSystemTime(new Date('2026-01-21T08:00:00'));

      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Buongiorno');
    });

    it('shows Buon pomeriggio in the afternoon (14:00)', () => {
      vi.setSystemTime(new Date('2026-01-21T14:00:00'));

      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Buon pomeriggio');
    });

    it('shows Buonasera in the evening (20:00)', () => {
      vi.setSystemTime(new Date('2026-01-21T20:00:00'));

      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Buonasera');
    });

    it('shows Buonasera late at night (23:00)', () => {
      vi.setSystemTime(new Date('2026-01-21T23:00:00'));

      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Buonasera');
    });

    it('shows Buonasera very early morning (3:00)', () => {
      vi.setSystemTime(new Date('2026-01-21T03:00:00'));

      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Buonasera');
    });

    it('shows Buongiorno at 5:00 (boundary)', () => {
      vi.setSystemTime(new Date('2026-01-21T05:00:00'));

      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Buongiorno');
    });

    it('shows Buon pomeriggio at 12:00 (boundary)', () => {
      vi.setSystemTime(new Date('2026-01-21T12:00:00'));

      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Buon pomeriggio');
    });

    it('shows Buonasera at 18:00 (boundary)', () => {
      vi.setSystemTime(new Date('2026-01-21T18:00:00'));

      render(<HeroStats stats={mockStats} />);

      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Buonasera');
    });
  });

  // ============================================================================
  // Last Access Formatting Tests
  // ============================================================================

  describe('Last Access Formatting', () => {
    it('formats minutes ago correctly', () => {
      const stats = {
        ...mockStats,
        lastAccess: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
      };

      render(<HeroStats stats={stats} />);

      expect(screen.getByTestId('hero-last-access')).toHaveTextContent('30 minuti fa');
    });

    it('formats today with time', () => {
      vi.setSystemTime(new Date('2026-01-21T16:00:00'));
      const stats = {
        ...mockStats,
        lastAccess: new Date('2026-01-21T14:30:00').toISOString(),
      };

      render(<HeroStats stats={stats} />);

      expect(screen.getByTestId('hero-last-access')).toHaveTextContent('Oggi alle 14:30');
    });

    it('formats yesterday with time', () => {
      vi.setSystemTime(new Date('2026-01-21T14:00:00'));
      const stats = {
        ...mockStats,
        lastAccess: new Date('2026-01-20T18:30:00').toISOString(),
      };

      render(<HeroStats stats={stats} />);

      expect(screen.getByTestId('hero-last-access')).toHaveTextContent('Ieri alle 18:30');
    });

    it('formats days ago correctly', () => {
      vi.setSystemTime(new Date('2026-01-21T14:00:00'));
      const stats = {
        ...mockStats,
        lastAccess: new Date('2026-01-18T14:00:00').toISOString(),
      };

      render(<HeroStats stats={stats} />);

      expect(screen.getByTestId('hero-last-access')).toHaveTextContent('3 giorni fa');
    });

    it('formats date for older accesses', () => {
      vi.setSystemTime(new Date('2026-01-21T14:00:00'));
      const stats = {
        ...mockStats,
        lastAccess: new Date('2026-01-10T14:00:00').toISOString(),
      };

      render(<HeroStats stats={stats} />);

      expect(screen.getByTestId('hero-last-access')).toHaveTextContent('10 gennaio');
    });
  });

  // ============================================================================
  // KPI Cards Values Tests
  // ============================================================================

  describe('KPI Card Values', () => {
    it('displays collection total and trend', () => {
      render(<HeroStats stats={mockStats} />);

      const collectionCard = screen.getByTestId('kpi-collection');
      expect(within(collectionCard).getByTestId('kpi-value')).toHaveTextContent('127');
      expect(within(collectionCard).getByTestId('kpi-label')).toHaveTextContent('Collezione');
      expect(within(collectionCard).getByTestId('kpi-trend')).toHaveTextContent('+3 mese');
    });

    it('displays played total and streak', () => {
      render(<HeroStats stats={mockStats} />);

      const playedCard = screen.getByTestId('kpi-played');
      expect(within(playedCard).getByTestId('kpi-value')).toHaveTextContent('23');
      expect(within(playedCard).getByTestId('kpi-label')).toHaveTextContent('Giocati 30gg');
      expect(within(playedCard).getByTestId('kpi-streak')).toHaveTextContent('7d');
    });

    it('does not display streak when zero', () => {
      render(<HeroStats stats={mockStatsNoStreak} />);

      const playedCard = screen.getByTestId('kpi-played');
      expect(within(playedCard).queryByTestId('kpi-streak')).not.toBeInTheDocument();
    });

    it('displays chats total without trend', () => {
      render(<HeroStats stats={mockStats} />);

      const chatsCard = screen.getByTestId('kpi-chats');
      expect(within(chatsCard).getByTestId('kpi-value')).toHaveTextContent('12');
      expect(within(chatsCard).getByTestId('kpi-label')).toHaveTextContent('Chat AI 7gg');
      expect(within(chatsCard).queryByTestId('kpi-trend')).not.toBeInTheDocument();
    });

    it('displays wishlist total and trend', () => {
      render(<HeroStats stats={mockStats} />);

      const wishlistCard = screen.getByTestId('kpi-wishlist');
      expect(within(wishlistCard).getByTestId('kpi-value')).toHaveTextContent('15');
      expect(within(wishlistCard).getByTestId('kpi-label')).toHaveTextContent('Wishlist');
      expect(within(wishlistCard).getByTestId('kpi-trend')).toHaveTextContent('+2 mese');
    });

    it('displays negative trends correctly', () => {
      render(<HeroStats stats={mockStatsNegativeTrend} />);

      const collectionCard = screen.getByTestId('kpi-collection');
      expect(within(collectionCard).getByTestId('kpi-trend')).toHaveTextContent('-5 mese');
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('KPI Card Navigation', () => {
    it('collection card links to /library', () => {
      render(<HeroStats stats={mockStats} />);

      const collectionCard = screen.getByTestId('kpi-collection');
      const link = collectionCard.closest('a');
      expect(link).toHaveAttribute('href', '/library');
    });

    it('played card links to /sessions/history', () => {
      render(<HeroStats stats={mockStats} />);

      const playedCard = screen.getByTestId('kpi-played');
      const link = playedCard.closest('a');
      expect(link).toHaveAttribute('href', '/sessions/history');
    });

    it('chats card links to /chat', () => {
      render(<HeroStats stats={mockStats} />);

      const chatsCard = screen.getByTestId('kpi-chats');
      const link = chatsCard.closest('a');
      expect(link).toHaveAttribute('href', '/chat/new');
    });

    it('wishlist card links to /wishlist', () => {
      render(<HeroStats stats={mockStats} />);

      const wishlistCard = screen.getByTestId('kpi-wishlist');
      const link = wishlistCard.closest('a');
      expect(link).toHaveAttribute('href', '/wishlist');
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading is true', () => {
      render(<HeroStats isLoading />);

      expect(screen.getByTestId('hero-stats-skeleton')).toBeInTheDocument();
      expect(screen.queryByTestId('hero-stats')).not.toBeInTheDocument();
    });

    it('renders content when isLoading is false', () => {
      render(<HeroStats stats={mockStats} isLoading={false} />);

      expect(screen.queryByTestId('hero-stats-skeleton')).not.toBeInTheDocument();
      expect(screen.getByTestId('hero-stats')).toBeInTheDocument();
    });

    it('skeleton has multiple skeleton elements', () => {
      render(<HeroStats isLoading />);

      const skeleton = screen.getByTestId('hero-stats-skeleton');
      const skeletonCards = skeleton.querySelectorAll('[data-testid="kpi-card-skeleton"]');
      expect(skeletonCards).toHaveLength(4);
    });
  });

  // ============================================================================
  // Default Props Tests
  // ============================================================================

  describe('Default Props', () => {
    it('uses mock data when stats not provided', () => {
      render(<HeroStats />);

      // Should render with default mock data
      expect(screen.getByTestId('hero-stats')).toBeInTheDocument();
      expect(screen.getByTestId('hero-greeting')).toHaveTextContent('Marco');
    });

    it('isLoading defaults to false', () => {
      render(<HeroStats stats={mockStats} />);

      expect(screen.queryByTestId('hero-stats-skeleton')).not.toBeInTheDocument();
      expect(screen.getByTestId('hero-stats')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Responsive Layout Tests
  // ============================================================================

  describe('Responsive Layout', () => {
    it('KPI grid has correct responsive classes', () => {
      render(<HeroStats stats={mockStats} />);

      const grid = screen.getByTestId('hero-kpi-grid');
      expect(grid).toHaveClass('grid-cols-2');
      expect(grid).toHaveClass('lg:grid-cols-4');
    });

    it('KPI cards have proper gap spacing', () => {
      render(<HeroStats stats={mockStats} />);

      const grid = screen.getByTestId('hero-kpi-grid');
      expect(grid).toHaveClass('gap-4');
    });
  });

  // ============================================================================
  // Typography Tests
  // ============================================================================

  describe('Typography', () => {
    it('greeting uses Playfair Display font', () => {
      render(<HeroStats stats={mockStats} />);

      const greeting = screen.getByTestId('hero-greeting');
      expect(greeting).toHaveClass('font-playfair');
    });

    it('greeting has responsive text sizes', () => {
      render(<HeroStats stats={mockStats} />);

      const greeting = screen.getByTestId('hero-greeting');
      expect(greeting).toHaveClass('text-2xl');
      expect(greeting).toHaveClass('sm:text-3xl');
    });

    it('last access text uses muted foreground', () => {
      render(<HeroStats stats={mockStats} />);

      const lastAccess = screen.getByTestId('hero-last-access');
      expect(lastAccess).toHaveClass('text-muted-foreground');
    });
  });
});
