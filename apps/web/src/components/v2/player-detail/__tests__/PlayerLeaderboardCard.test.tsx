/**
 * PlayerLeaderboardCard unit tests — Wave 3 /players/[id] v2 (Task 2)
 *
 * Tests:
 *   1. Renders rank with interpolated label when rank is provided
 *   2. Renders noRank text when rank is null
 *   3. data-slot and aria-label attributes are present
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { PlayerLeaderboardCard } from '../PlayerLeaderboardCard';
import type { PlayerLeaderboardCardLabels } from '../PlayerLeaderboardCard';

const labels: PlayerLeaderboardCardLabels = {
  title: 'Leaderboard',
  rank: 'Rank #{rank}',
  rankAriaLabel: 'Leaderboard position: {rank}',
  noRank: 'Not ranked yet',
};

describe('PlayerLeaderboardCard', () => {
  it('renders interpolated rank label when rank is provided', () => {
    render(<PlayerLeaderboardCard rank={3} labels={labels} />);
    expect(screen.getByText('Rank #3')).toBeInTheDocument();
  });

  it('renders noRank text when rank is null', () => {
    render(<PlayerLeaderboardCard rank={null} labels={labels} />);
    expect(screen.getByText('Not ranked yet')).toBeInTheDocument();
  });

  it('has correct data-slot and aria-label for E2E and a11y', () => {
    const { container } = render(<PlayerLeaderboardCard rank={5} labels={labels} />);
    expect(container.querySelector('[data-slot="player-detail-leaderboard"]')).toBeInTheDocument();
    // The rank element should have aria-label with rank value
    expect(container.querySelector('[aria-label="Leaderboard position: 5"]')).toBeInTheDocument();
  });
});
