/**
 * PlayerHero unit tests — Wave 3 /players/[id] v2 (Task 2)
 *
 * Tests:
 *   1. Renders displayName as heading
 *   2. Renders 3 KPI inline values (sessions / wins / winRate)
 *   3. Back button fires onBack callback
 *   4. Back button absent when onBack is not provided
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { PlayerHero } from '../PlayerHero';
import type { PlayerHeroLabels } from '../PlayerHero';

const labels: PlayerHeroLabels = {
  back: 'Back to Players',
  backAriaLabel: 'Back to players list',
  totalSessions: '{count} sessions',
  totalWins: '{count} wins',
  winRate: '{rate}% win rate',
};

describe('PlayerHero', () => {
  it('renders displayName as heading', () => {
    render(
      <PlayerHero
        displayName="Sara Rossi"
        totalSessions={42}
        totalWins={28}
        winRate={0.67}
        labels={labels}
      />
    );
    expect(screen.getByRole('heading', { name: 'Sara Rossi' })).toBeInTheDocument();
  });

  it('renders 3 KPI inline values with correct interpolation', () => {
    render(
      <PlayerHero
        displayName="Sara Rossi"
        totalSessions={42}
        totalWins={28}
        winRate={0.67}
        labels={labels}
      />
    );
    expect(screen.getByText('42 sessions')).toBeInTheDocument();
    expect(screen.getByText('28 wins')).toBeInTheDocument();
    expect(screen.getByText('67% win rate')).toBeInTheDocument();
  });

  it('fires onBack callback when back button is clicked', () => {
    const onBack = vi.fn();
    render(
      <PlayerHero
        displayName="Sara Rossi"
        totalSessions={10}
        totalWins={5}
        winRate={0.5}
        onBack={onBack}
        labels={labels}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Back to players list' }));
    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('does not render back button when onBack is absent', () => {
    render(
      <PlayerHero
        displayName="Sara Rossi"
        totalSessions={10}
        totalWins={5}
        winRate={0.5}
        labels={labels}
      />
    );
    expect(screen.queryByRole('button', { name: 'Back to players list' })).not.toBeInTheDocument();
  });
});
