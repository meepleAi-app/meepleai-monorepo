/**
 * Collection Dashboard Components Unit Tests - Issue #3476
 *
 * Tests for CollectionStats, MeepleCard components.
 *
 * NOTE: CollectionGrid was removed in Issue #3894 - its functionality
 * is superseded by EntityListView and CollectionDashboard's inline rendering.
 * See: components/ui/data-display/entity-list-view/
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CollectionStats } from '@/components/collection/CollectionStats';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';

import type { CollectionStats as CollectionStatsType } from '@/types/collection';


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

