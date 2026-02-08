/**
 * ScoringInterface Component Tests
 *
 * Tests multi-dimensional score input and validation.
 * Issue #3892: Play Records Frontend UI
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

import { ScoringInterface } from '@/components/play-records/ScoringInterface';
import type { SessionPlayer, SessionScoringConfig } from '@/lib/api/schemas/play-records.schemas';

const mockPlayers: SessionPlayer[] = [
  {
    id: 'player-1',
    userId: 'user-1',
    displayName: 'Alice',
    scores: [{ dimension: 'Points', value: 10, unit: 'VP' }],
  },
  {
    id: 'player-2',
    userId: null,
    displayName: 'Bob',
    scores: [],
  },
];

const mockScoringConfig: SessionScoringConfig = {
  enabledDimensions: ['Points', 'Ranking'],
  dimensionUnits: { Points: 'VP', Ranking: 'Position' },
};

describe('ScoringInterface', () => {
  const mockOnRecordScore = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders scoring table with players and dimensions', () => {
    render(
      <ScoringInterface
        players={mockPlayers}
        scoringConfig={mockScoringConfig}
        onRecordScore={mockOnRecordScore}
      />
    );

    expect(screen.getByText('Record Scores')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Points')).toBeInTheDocument();
    expect(screen.getByText('Ranking')).toBeInTheDocument();
  });

  it('displays dimension units', () => {
    render(
      <ScoringInterface
        players={mockPlayers}
        scoringConfig={mockScoringConfig}
        onRecordScore={mockOnRecordScore}
      />
    );

    expect(screen.getByText('(VP)')).toBeInTheDocument();
    expect(screen.getByText('(Position)')).toBeInTheDocument();
  });

  it('shows existing scores', () => {
    render(
      <ScoringInterface
        players={mockPlayers}
        scoringConfig={mockScoringConfig}
        onRecordScore={mockOnRecordScore}
      />
    );

    const scoreInputs = screen.getAllByRole('spinbutton');
    const alicePointsInput = scoreInputs[0] as HTMLInputElement;
    expect(alicePointsInput.value).toBe('10');
  });

  it('allows entering scores', async () => {
    render(
      <ScoringInterface
        players={mockPlayers}
        scoringConfig={mockScoringConfig}
        onRecordScore={mockOnRecordScore}
      />
    );

    const scoreInputs = screen.getAllByRole('spinbutton');
    const bobPointsInput = scoreInputs[2]; // Bob's Points input

    fireEvent.change(bobPointsInput, { target: { value: '25' } });
    fireEvent.blur(bobPointsInput);

    await waitFor(() => {
      expect(mockOnRecordScore).toHaveBeenCalledWith('player-2', 'Points', 25);
    });
  });

  it('shows guest badge for non-registered players', () => {
    render(
      <ScoringInterface
        players={mockPlayers}
        scoringConfig={mockScoringConfig}
        onRecordScore={mockOnRecordScore}
      />
    );

    const guestBadges = screen.getAllByText('Guest');
    expect(guestBadges).toHaveLength(1);
  });

  it('shows alert when no scoring dimensions configured', () => {
    const emptyConfig: SessionScoringConfig = {
      enabledDimensions: [],
      dimensionUnits: {},
    };

    render(
      <ScoringInterface
        players={mockPlayers}
        scoringConfig={emptyConfig}
        onRecordScore={mockOnRecordScore}
      />
    );

    expect(screen.getByText(/No scoring dimensions configured/)).toBeInTheDocument();
  });

  it('shows alert when no players added', () => {
    render(
      <ScoringInterface
        players={[]}
        scoringConfig={mockScoringConfig}
        onRecordScore={mockOnRecordScore}
      />
    );

    expect(screen.getByText(/Add players first to enable score recording/)).toBeInTheDocument();
  });

  it('handles save all scores button', async () => {
    render(
      <ScoringInterface
        players={mockPlayers}
        scoringConfig={mockScoringConfig}
        onRecordScore={mockOnRecordScore}
      />
    );

    const saveAllButton = screen.getByRole('button', { name: /Save All/ });
    fireEvent.click(saveAllButton);

    // Should save any non-empty scores
    await waitFor(() => {
      expect(mockOnRecordScore).toHaveBeenCalled();
    });
  });
});
