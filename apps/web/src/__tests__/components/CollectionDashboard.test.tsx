/**
 * Collection Dashboard Components Unit Tests - Issue #3476
 *
 * Tests for CollectionStats, MeepleCard, CollectionGrid components
 *
 * NOTE: MeepleCard tests are skipped - component refactored in Epic #3820
 * with new API. Tests need to be rewritten for new MeepleCard interface.
 * See: docs/frontend/components/meeple-card.md
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CollectionStats } from '@/components/collection/CollectionStats';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { CollectionGrid } from '@/components/collection/CollectionGrid';

import type { CollectionStats as CollectionStatsType, CollectionGame } from '@/types/collection';
import type { ActivityEvent } from '@/components/dashboard/ActivityFeed';

// ============================================================================
// Mock Data
// ============================================================================

const mockStats: CollectionStatsType = {
  totalGames: 10,
  privatePdfsCount: 5,
  activeChats: 3,
  totalReadingMinutes: 120,
  recentActivity: [],
};

const mockGame: CollectionGame = {
  id: 'test-game-1',
  bggId: 12345,
  title: 'Test Game',
  imageUrl: '/test-image.jpg',
  thumbnailUrl: '/test-thumb.jpg',
  yearPublished: 2020,
  minPlayers: 2,
  maxPlayers: 4,
  playingTime: 60,
  complexity: 2.5,
  rating: 8.0,
  addedAt: new Date().toISOString(),
  lastPlayedAt: new Date().toISOString(),
  playCount: 5,
  hasPdf: true,
  hasActiveChat: true,
  chatCount: 2,
  category: 'Strategy',
  tags: ['test'],
};

const mockGames: CollectionGame[] = [
  mockGame,
  {
    ...mockGame,
    id: 'test-game-2',
    title: 'Test Game 2',
    hasPdf: false,
    hasActiveChat: false,
  },
];

// ============================================================================
// CollectionStats Tests
// ============================================================================

describe('CollectionStats', () => {
  it('should render all stat cards', () => {
    render(<CollectionStats stats={mockStats} />);

    expect(screen.getByTestId('collection-stats-section')).toBeInTheDocument();
    expect(screen.getByTestId('stat-card-giochi-totali')).toBeInTheDocument();
    expect(screen.getByTestId('stat-card-pdf-privati')).toBeInTheDocument();
    expect(screen.getByTestId('stat-card-chat-attive')).toBeInTheDocument();
    expect(screen.getByTestId('stat-card-tempo-lettura')).toBeInTheDocument();
  });

  it('should display correct stat values', () => {
    render(<CollectionStats stats={mockStats} />);

    expect(screen.getByTestId('stat-value-giochi-totali')).toHaveTextContent('10');
    expect(screen.getByTestId('stat-value-pdf-privati')).toHaveTextContent('5');
    expect(screen.getByTestId('stat-value-chat-attive')).toHaveTextContent('3');
    expect(screen.getByTestId('stat-value-tempo-lettura')).toHaveTextContent('2h');
  });

  it('should show skeleton when loading', () => {
    render(<CollectionStats isLoading={true} />);

    expect(screen.getByTestId('collection-stats-skeleton')).toBeInTheDocument();
  });

  it('should show skeleton when stats are undefined', () => {
    render(<CollectionStats stats={undefined} />);

    expect(screen.getByTestId('collection-stats-skeleton')).toBeInTheDocument();
  });
});

// ============================================================================
// MeepleCard Tests - Updated for Epic #3820 New API
// ============================================================================

describe('MeepleCard', () => {
  it('should render game card with basic info', () => {
    render(
      <MeepleCard
        entity="game"
        variant="grid"
        id="test-game-1"
        title="Test Game"
        subtitle="Test Publisher · 2020"
        data-testid="meeple-card-test-game-1"
      />
    );

    expect(screen.getByTestId('meeple-card-test-game-1')).toBeInTheDocument();
    expect(screen.getByText('Test Game')).toBeInTheDocument();
    expect(screen.getByText(/2020/)).toBeInTheDocument();
  });

  it('should render with rating', () => {
    render(
      <MeepleCard
        entity="game"
        title="Rated Game"
        rating={7.5}
        ratingMax={10}
      />
    );

    expect(screen.getByText(/7\.5/)).toBeInTheDocument();
  });

  it('should handle click events', async () => {
    const onClick = vi.fn();
    const user = userEvent.setup();

    render(
      <MeepleCard
        entity="game"
        title="Clickable Game"
        onClick={onClick}
        data-testid="clickable-card"
      />
    );

    await user.click(screen.getByTestId('clickable-card'));
    expect(onClick).toHaveBeenCalled();
  });

  it('should display loading state', () => {
    render(
      <MeepleCard
        entity="game"
        title="Loading Game"
        loading={true}
      />
    );

    // Loading state shows skeleton placeholder with animation
    const skeleton = screen.getByTestId('meeple-card-skeleton');
    expect(skeleton).toBeInTheDocument();
    expect(skeleton.className).toContain('animate-pulse');
  });

  it('should support different entity types', () => {
    const { rerender } = render(
      <MeepleCard entity="game" title="Game Entity" />
    );
    expect(screen.getByRole('article')).toHaveAttribute('data-entity', 'game');

    rerender(<MeepleCard entity="player" title="Player Entity" />);
    expect(screen.getByRole('article')).toHaveAttribute('data-entity', 'player');
  });
});

// ============================================================================
// CollectionGrid Tests
// ============================================================================

describe('CollectionGrid', () => {
  const mockOnSortChange = vi.fn();
  const mockOnFilterChange = vi.fn();
  const mockOnGameClick = vi.fn();

  it('should render games grid', () => {
    render(
      <CollectionGrid
        games={mockGames}
        sortBy="date-added-desc"
        filters={{}}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    expect(screen.getByTestId('collection-grid-section')).toBeInTheDocument();
    expect(screen.getByTestId('games-grid')).toBeInTheDocument();
  });

  it('should render all game cards', () => {
    render(
      <CollectionGrid
        games={mockGames}
        sortBy="date-added-desc"
        filters={{}}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    // Verify both game cards are rendered by their titles
    expect(screen.getByText('Test Game')).toBeInTheDocument();
    expect(screen.getByText('Test Game 2')).toBeInTheDocument();

    // Verify correct number of cards (using data-entity attribute)
    const cards = document.querySelectorAll('[data-entity="game"]');
    expect(cards.length).toBe(2);
  });

  it('should show empty state when no games', () => {
    render(
      <CollectionGrid
        games={[]}
        sortBy="date-added-desc"
        filters={{}}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    expect(screen.getByTestId('collection-grid-empty')).toBeInTheDocument();
    expect(screen.getByText('Nessun gioco trovato')).toBeInTheDocument();
  });

  it('should show loading skeleton when loading', () => {
    render(
      <CollectionGrid
        games={[]}
        sortBy="date-added-desc"
        filters={{}}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
        isLoading={true}
      />
    );

    expect(screen.getByTestId('collection-grid-skeleton')).toBeInTheDocument();
  });

  it('should render sort select', () => {
    render(
      <CollectionGrid
        games={mockGames}
        sortBy="date-added-desc"
        filters={{}}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    expect(screen.getByTestId('sort-select')).toBeInTheDocument();
  });

  it('should render filter toggle button', () => {
    render(
      <CollectionGrid
        games={mockGames}
        sortBy="date-added-desc"
        filters={{}}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    expect(screen.getByTestId('filter-toggle')).toBeInTheDocument();
  });

  it('should show filter panel when toggle clicked', async () => {
    const user = userEvent.setup();

    render(
      <CollectionGrid
        games={mockGames}
        sortBy="date-added-desc"
        filters={{}}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    const filterToggle = screen.getByTestId('filter-toggle');
    await user.click(filterToggle);

    await waitFor(() => {
      expect(screen.getByTestId('filter-panel')).toBeInTheDocument();
    });
  });

  it('should display active filters count', () => {
    render(
      <CollectionGrid
        games={mockGames}
        sortBy="date-added-desc"
        filters={{ hasPdf: true, hasActiveChat: true }}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    expect(screen.getByText('2')).toBeInTheDocument(); // Filter count badge
  });

  it('should show active filter tags', () => {
    render(
      <CollectionGrid
        games={mockGames}
        sortBy="date-added-desc"
        filters={{ hasPdf: true }}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    expect(screen.getByTestId('active-filters')).toBeInTheDocument();
    expect(screen.getByTestId('filter-tag-has-pdf-true')).toBeInTheDocument();
  });

  it('should call onFilterChange when filter removed', async () => {
    const user = userEvent.setup();

    render(
      <CollectionGrid
        games={mockGames}
        sortBy="date-added-desc"
        filters={{ hasPdf: true }}
        onSortChange={mockOnSortChange}
        onFilterChange={mockOnFilterChange}
      />
    );

    const removeButton = screen.getByRole('button', { name: /Rimuovi filtro Ha PDF/i });
    await user.click(removeButton);

    expect(mockOnFilterChange).toHaveBeenCalledWith({ hasPdf: undefined });
  });
});
