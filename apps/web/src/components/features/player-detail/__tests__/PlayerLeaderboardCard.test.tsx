/**
 * PlayerLeaderboardCard unit tests — Wave 3 /players/[id] v2 (Task 2)
 *
 * Tests:
 *   1. Renders interpolated rank label when rank is provided
 *   2. Renders noRank text when rank is null
 *   3. Exposes the rank to screen readers via a visually-hidden label
 *      (NOT via aria-label on a generic <div>, which ARIA 1.2 prohibits)
 *   4. Passes axe a11y scan when ranked
 *   5. Passes axe a11y scan when not ranked
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
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

  it('exposes the rank to screen readers via a visually-hidden label', () => {
    const { container } = render(<PlayerLeaderboardCard rank={5} labels={labels} />);
    expect(container.querySelector('[data-slot="player-detail-leaderboard"]')).toBeInTheDocument();
    // Accessible name lives in a visually-hidden span, not aria-label on a generic <div>
    // (ARIA 1.2 prohibits naming attributes on generic role; assistive tech ignore them).
    const srLabel = screen.getByText('Leaderboard position: 5');
    expect(srLabel).toHaveClass('sr-only');
  });

  it('passes axe a11y scan when ranked', async () => {
    const { container } = render(<PlayerLeaderboardCard rank={5} labels={labels} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('passes axe a11y scan when not ranked', async () => {
    const { container } = render(<PlayerLeaderboardCard rank={null} labels={labels} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
