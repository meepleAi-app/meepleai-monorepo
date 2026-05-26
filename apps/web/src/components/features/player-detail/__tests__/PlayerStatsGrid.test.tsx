/**
 * PlayerStatsGrid unit tests — Wave 3 /players/[id] v2 (Task 2)
 *
 * Tests:
 *   1. Renders 4 KPI tiles with correct values
 *   2. Renders all 4 tile labels
 *   3. Win rate displayed as integer percentage (culture-independent)
 *   4. data-slot attribute is present
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, it, expect } from 'vitest';

import { PlayerStatsGrid } from '../PlayerStatsGrid';
import type { PlayerStatsGridLabels } from '../PlayerStatsGrid';

const labels: PlayerStatsGridLabels = {
  sessions: 'Sessions',
  wins: 'Wins',
  winRate: 'Win Rate',
  achievements: 'Achievements',
};

describe('PlayerStatsGrid', () => {
  it('renders 4 KPI tiles with correct numeric values', () => {
    render(
      <PlayerStatsGrid
        totalSessions={88}
        totalWins={54}
        winRate={0.614}
        achievementCount={7}
        labels={labels}
      />
    );
    expect(screen.getByText('88')).toBeInTheDocument();
    expect(screen.getByText('54')).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
  });

  it('renders all 4 tile labels', () => {
    render(
      <PlayerStatsGrid
        totalSessions={10}
        totalWins={5}
        winRate={0.5}
        achievementCount={3}
        labels={labels}
      />
    );
    expect(screen.getByText('Sessions')).toBeInTheDocument();
    expect(screen.getByText('Wins')).toBeInTheDocument();
    expect(screen.getByText('Win Rate')).toBeInTheDocument();
    expect(screen.getByText('Achievements')).toBeInTheDocument();
  });

  it('displays win rate as integer percentage (no decimals)', () => {
    render(
      <PlayerStatsGrid
        totalSessions={10}
        totalWins={7}
        winRate={0.733}
        achievementCount={2}
        labels={labels}
      />
    );
    // Should display "73%" — culture-independent, no decimal
    expect(screen.getByText('73%')).toBeInTheDocument();
  });

  it('has correct data-slot attribute for E2E targeting', () => {
    const { container } = render(
      <PlayerStatsGrid
        totalSessions={5}
        totalWins={2}
        winRate={0.4}
        achievementCount={1}
        labels={labels}
      />
    );
    expect(container.querySelector('[data-slot="player-detail-stats-grid"]')).toBeInTheDocument();
  });

  it('passes axe a11y scan', async () => {
    const { container } = render(
      <PlayerStatsGrid
        totalSessions={42}
        totalWins={28}
        winRate={0.67}
        achievementCount={5}
        labels={labels}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
