import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LeaderboardWidget } from '../widgets/LeaderboardWidget';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

const s = (id: string, winner?: string): SessionSummaryDto => ({
  id,
  gameName: 'Catan',
  sessionDate: '2026-04-01T20:00:00Z',
  playerCount: 3,
  winnerName: winner,
});

describe('LeaderboardWidget', () => {
  it('renders empty state when no winners', () => {
    render(<LeaderboardWidget sessions={[s('1'), s('2')]} />);
    expect(
      screen.getByText('Gioca partite con amici per vedere la classifica')
    ).toBeInTheDocument();
  });

  it('renders ranked winners with medals', () => {
    render(<LeaderboardWidget sessions={[s('1', 'Marco'), s('2', 'Marco'), s('3', 'Sara')]} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('Sara')).toBeInTheDocument();
    expect(screen.getByText('🥇')).toBeInTheDocument();
    expect(screen.getByText('🥈')).toBeInTheDocument();
  });

  it('shows win counts', () => {
    render(<LeaderboardWidget sessions={[s('1', 'Marco'), s('2', 'Marco'), s('3', 'Sara')]} />);
    expect(screen.getByText('2 vitt.')).toBeInTheDocument();
    expect(screen.getByText('1 vitt.')).toBeInTheDocument();
  });

  it('limits to 4 players', () => {
    const sessions = ['A', 'B', 'C', 'D', 'E'].map((name, i) => s(String(i), name));
    render(<LeaderboardWidget sessions={sessions} />);
    expect(screen.queryByText('E')).not.toBeInTheDocument();
  });

  it('sorts by wins descending', () => {
    render(
      <LeaderboardWidget sessions={[s('1', 'Rare'), s('2', 'Top'), s('3', 'Top'), s('4', 'Top')]} />
    );
    const items = screen.getAllByText(/vitt\./);
    expect(items[0]?.textContent).toBe('3 vitt.');
  });
});
