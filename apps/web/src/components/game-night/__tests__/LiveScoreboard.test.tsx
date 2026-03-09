/**
 * LiveScoreboard Component Tests
 * Issue #5587 — Live Game Session UI
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LiveScoreboard, type LiveScoreboardPlayer } from '../LiveScoreboard';

describe('LiveScoreboard', () => {
  const players: LiveScoreboardPlayer[] = [
    { id: 'p1', displayName: 'Marco', totalScore: 7, avatarColor: '#ef4444' },
    { id: 'p2', displayName: 'Luca', totalScore: 5, avatarColor: '#3b82f6' },
    { id: 'p3', displayName: 'Sara', totalScore: 6, avatarColor: '#22c55e' },
  ];

  it('should render all players', () => {
    render(<LiveScoreboard players={players} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('Luca')).toBeInTheDocument();
    expect(screen.getByText('Sara')).toBeInTheDocument();
  });

  it('should sort players by score descending', () => {
    render(<LiveScoreboard players={players} />);
    const rows = screen.getAllByTestId(/^scoreboard-row-/);
    expect(rows).toHaveLength(3);
    // Marco (7) should be first
    expect(rows[0]).toHaveAttribute('data-testid', 'scoreboard-row-p1');
    // Sara (6) second
    expect(rows[1]).toHaveAttribute('data-testid', 'scoreboard-row-p3');
    // Luca (5) third
    expect(rows[2]).toHaveAttribute('data-testid', 'scoreboard-row-p2');
  });

  it('should highlight leader with crown', () => {
    render(<LiveScoreboard players={players} />);
    // Crown icon has aria-label="Leader"
    expect(screen.getByLabelText('Leader')).toBeInTheDocument();
  });

  it('should display scores', () => {
    render(<LiveScoreboard players={players} />);
    expect(screen.getByText('7')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('6')).toBeInTheDocument();
  });

  it('should not show crown when all scores are 0', () => {
    const zeroPlayers = players.map(p => ({ ...p, totalScore: 0 }));
    render(<LiveScoreboard players={zeroPlayers} />);
    expect(screen.queryByLabelText('Leader')).not.toBeInTheDocument();
  });

  it('should render avatar initials', () => {
    render(<LiveScoreboard players={players} />);
    // Single-word names produce single-letter initials
    expect(screen.getByText('M')).toBeInTheDocument();
    expect(screen.getByText('L')).toBeInTheDocument();
    expect(screen.getByText('S')).toBeInTheDocument();
  });

  it('should have data-testid', () => {
    render(<LiveScoreboard players={players} />);
    expect(screen.getByTestId('live-scoreboard')).toBeInTheDocument();
  });
});
