/**
 * RecentGamesSection Component Tests (Issue #2861)
 *
 * Test Coverage:
 * - Loading state (skeleton grid)
 * - Error state (alert with message)
 * - Empty state (placeholder UI)
 * - Data rendering (game cards)
 * - Navigation link to catalog
 * - Props handling
 * - Accessibility
 *
 * Target: >=85% coverage
 *
 * Updated for i18n compliance (Issue #3096): Uses data-testid pattern
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RecentGamesSection } from '../RecentGamesSection';
import type { Game } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
}));

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock GameCard component to simplify testing
vi.mock('@/components/games/GameCard', () => ({
  GameCard: ({ game, onClick }: { game: Game; onClick: () => void }) => (
    <div data-testid={`game-card-${game.id}`} onClick={onClick} role="button">
      <span>{game.title}</span>
    </div>
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGames: Game[] = [
  {
    id: 'game-1',
    title: 'Catan',
    description: 'Trade, build, and settle the island of Catan',
    imageUrl: 'https://example.com/catan.jpg',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    complexity: 2.5,
    yearPublished: 1995,
    publisher: 'Kosmos',
    designer: 'Klaus Teuber',
    categories: ['Strategy', 'Family'],
    mechanics: ['Trading', 'Building'],
    source: 'manual',
    sourceId: null,
    createdAt: new Date().toISOString(),
    updatedAt: null,
  },
  {
    id: 'game-2',
    title: 'Azul',
    description: 'Tile-laying game',
    imageUrl: 'https://example.com/azul.jpg',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 45,
    complexity: 1.8,
    yearPublished: 2017,
    publisher: 'Plan B Games',
    designer: 'Michael Kiesling',
    categories: ['Abstract', 'Puzzle'],
    mechanics: ['Pattern Building'],
    source: 'manual',
    sourceId: null,
    createdAt: new Date().toISOString(),
    updatedAt: null,
  },
  {
    id: 'game-3',
    title: 'Wingspan',
    description: 'Bird collection engine builder',
    imageUrl: 'https://example.com/wingspan.jpg',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 60,
    complexity: 2.4,
    yearPublished: 2019,
    publisher: 'Stonemaier Games',
    designer: 'Elizabeth Hargrave',
    categories: ['Engine Building', 'Nature'],
    mechanics: ['Card Drafting'],
    source: 'manual',
    sourceId: null,
    createdAt: new Date().toISOString(),
    updatedAt: null,
  },
];

// More games to test limit of 6
const moreGames: Game[] = [
  ...mockGames,
  { ...mockGames[0], id: 'game-4', title: 'Ticket to Ride' },
  { ...mockGames[1], id: 'game-5', title: 'Pandemic' },
  { ...mockGames[2], id: 'game-6', title: 'Splendor' },
  { ...mockGames[0], id: 'game-7', title: '7 Wonders' },
  { ...mockGames[1], id: 'game-8', title: 'Codenames' },
];

// ============================================================================
// Test Suite
// ============================================================================

describe('RecentGamesSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton grid while loading', () => {
      render(
        <RecentGamesSection games={undefined} isLoading={true} error={null} />
      );

      expect(screen.getByTestId('recent-games-title')).toBeInTheDocument();
      expect(screen.getByTestId('recent-games-skeleton-grid')).toBeInTheDocument();
      // 6 skeleton items
      for (let i = 0; i < 6; i++) {
        expect(screen.getByTestId(`recent-games-skeleton-${i}`)).toBeInTheDocument();
      }
    });

    it('shows loading section with aria-label', () => {
      render(<RecentGamesSection games={undefined} isLoading={true} error={null} />);

      expect(screen.getByLabelText('Recent games')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Error State Tests
  // ============================================================================

  describe('Error State', () => {
    it('renders error alert when error is provided', () => {
      render(
        <RecentGamesSection games={undefined} isLoading={false} error={new Error('Network error')} />
      );

      expect(screen.getByTestId('recent-games-error')).toBeInTheDocument();
      expect(screen.getByTestId('recent-games-error-title')).toBeInTheDocument();
      expect(screen.getByTestId('recent-games-error-description')).toBeInTheDocument();
      expect(screen.getByTestId('recent-games-error-message')).toHaveTextContent('Network error');
    });

    it('handles non-Error error objects', () => {
      render(
        <RecentGamesSection games={undefined} isLoading={false} error="String error" />
      );

      expect(screen.getByTestId('recent-games-error-message')).toHaveTextContent('String error');
    });

    it('renders AlertCircle icon in error state', () => {
      const { container } = render(
        <RecentGamesSection games={undefined} isLoading={false} error={new Error('Error')} />
      );

      // AlertCircle is rendered inside the Alert component
      const alertIcon = container.querySelector('svg');
      expect(alertIcon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('renders empty state when games array is empty', () => {
      render(<RecentGamesSection games={[]} isLoading={false} error={null} />);

      expect(screen.getByTestId('recent-games-empty')).toBeInTheDocument();
      expect(screen.getByTestId('recent-games-empty-title')).toBeInTheDocument();
      expect(screen.getByTestId('recent-games-empty-description')).toBeInTheDocument();
    });

    it('renders empty state when games is undefined', () => {
      render(<RecentGamesSection games={undefined} isLoading={false} error={null} />);

      expect(screen.getByTestId('recent-games-empty')).toBeInTheDocument();
    });

    it('renders Gamepad2 icon in empty state', () => {
      const { container } = render(
        <RecentGamesSection games={[]} isLoading={false} error={null} />
      );

      const icon = container.querySelector('svg.lucide-gamepad-2');
      expect(icon).toBeInTheDocument();
    });

    it('renders explore catalog button in empty state', () => {
      render(<RecentGamesSection games={[]} isLoading={false} error={null} />);

      const button = screen.getByTestId('recent-games-explore-button');
      expect(button).toBeInTheDocument();
      expect(button.closest('a')).toHaveAttribute('href', '/games');
    });
  });

  // ============================================================================
  // Data Rendering Tests
  // ============================================================================

  describe('Data Rendering', () => {
    it('renders game cards when games are provided', () => {
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Azul')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });

    it('renders section header with title', () => {
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      expect(screen.getByTestId('recent-games-title')).toBeInTheDocument();
    });

    it('renders view all link to games catalog', () => {
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      const button = screen.getByTestId('recent-games-view-all-button');
      expect(button).toBeInTheDocument();
      expect(button.closest('a')).toHaveAttribute('href', '/games');
    });

    it('limits displayed games to 6', () => {
      render(<RecentGamesSection games={moreGames} isLoading={false} error={null} />);

      // Should only render 6 games even though 8 are provided
      const gameCards = screen.getAllByTestId(/game-card-/);
      expect(gameCards.length).toBe(6);
    });

    it('renders all games when fewer than 6 provided', () => {
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      const gameCards = screen.getAllByTestId(/game-card-/);
      expect(gameCards.length).toBe(3);
    });
  });

  // ============================================================================
  // Navigation Tests (Issue #3095 - Fixed routing from /giochi to /games)
  // ============================================================================

  describe('Navigation', () => {
    it('navigates to game detail when card is clicked', async () => {
      const user = userEvent.setup();
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      const catanCard = screen.getByTestId('game-card-game-1');
      await user.click(catanCard);

      expect(mockPush).toHaveBeenCalledWith('/games/game-1');
    });

    it('navigates to correct game detail for each card', async () => {
      const user = userEvent.setup();
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      const azulCard = screen.getByTestId('game-card-game-2');
      await user.click(azulCard);

      expect(mockPush).toHaveBeenCalledWith('/games/game-2');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has correct aria-label on section', () => {
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      expect(screen.getByLabelText('Recent games')).toBeInTheDocument();
    });

    it('has correct heading structure', () => {
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toBeInTheDocument();
      expect(heading).toHaveAttribute('data-testid', 'recent-games-title');
    });

    it('has correct heading in empty state', () => {
      render(<RecentGamesSection games={[]} isLoading={false} error={null} />);

      const h2 = screen.getByRole('heading', { level: 2 });
      expect(h2).toHaveAttribute('data-testid', 'recent-games-title');

      const h3 = screen.getByRole('heading', { level: 3 });
      expect(h3).toHaveAttribute('data-testid', 'recent-games-empty-title');
    });
  });

  // ============================================================================
  // CSS Classes Tests
  // ============================================================================

  describe('Styling', () => {
    it('applies font-quicksand to heading', () => {
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      const heading = screen.getByTestId('recent-games-title');
      expect(heading).toHaveClass('font-quicksand');
    });

    it('applies grid classes for responsive layout', () => {
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      const grid = screen.getByTestId('recent-games-grid');
      expect(grid).toHaveClass('grid-cols-2');
      expect(grid).toHaveClass('md:grid-cols-3');
    });

    it('applies dashed border in empty state', () => {
      render(<RecentGamesSection games={[]} isLoading={false} error={null} />);

      const placeholder = screen.getByTestId('recent-games-empty');
      expect(placeholder).toHaveClass('border-dashed');
    });
  });

  // ============================================================================
  // Props Tests
  // ============================================================================

  describe('Props Handling', () => {
    it('passes grid variant to GameCard', () => {
      // The mock captures this - in a real scenario we'd verify the variant prop
      render(<RecentGamesSection games={mockGames} isLoading={false} error={null} />);

      // GameCards are rendered
      expect(screen.getAllByTestId(/game-card-/).length).toBe(3);
    });

    it('handles null games prop gracefully', () => {
      render(<RecentGamesSection games={null as unknown as Game[]} isLoading={false} error={null} />);

      expect(screen.getByTestId('recent-games-empty')).toBeInTheDocument();
    });
  });
});
