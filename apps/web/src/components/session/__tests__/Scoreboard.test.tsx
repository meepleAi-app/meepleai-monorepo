/**
 * Scoreboard Component Tests (Issue #3026)
 *
 * Test Coverage:
 * - Compact variant rendering
 * - Full variant rendering with podium
 * - Score table with rounds and categories
 * - Real-time mode indicator
 * - Score trends
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { Scoreboard } from '../Scoreboard';
import type { ScoreboardData, Participant, ScoreEntry } from '../types';

// Mock ParticipantCard
vi.mock('../ParticipantCard', () => ({
  ParticipantCard: ({ participant, variant }: { participant: Participant; variant: string }) => (
    <div data-testid={`participant-${participant.id}`} data-variant={variant}>
      {participant.displayName} - Score: {participant.totalScore}
    </div>
  ),
}));

describe('Scoreboard', () => {
  const createParticipant = (overrides?: Partial<Participant>): Participant => ({
    id: overrides?.id || 'p1',
    displayName: overrides?.displayName || 'Alice',
    isOwner: overrides?.isOwner ?? false,
    isCurrentUser: overrides?.isCurrentUser ?? false,
    avatarColor: overrides?.avatarColor || '#3b82f6',
    totalScore: overrides?.totalScore ?? 100,
    rank: overrides?.rank,
  });

  const createScoreEntry = (overrides?: Partial<ScoreEntry>): ScoreEntry => ({
    id: overrides?.id || 'score-1',
    participantId: overrides?.participantId || 'p1',
    roundNumber: overrides?.roundNumber ?? 1,
    category: overrides?.category ?? null,
    scoreValue: overrides?.scoreValue ?? 10,
    timestamp: overrides?.timestamp || new Date('2024-01-15T10:00:00Z'),
    createdBy: overrides?.createdBy || 'user-1',
  });

  const createScoreboardData = (overrides?: Partial<ScoreboardData>): ScoreboardData => ({
    participants: overrides?.participants || [
      createParticipant({ id: 'p1', displayName: 'Alice', totalScore: 150, rank: 1 }),
      createParticipant({ id: 'p2', displayName: 'Bob', totalScore: 120, rank: 2 }),
      createParticipant({ id: 'p3', displayName: 'Charlie', totalScore: 90, rank: 3 }),
    ],
    scores: overrides?.scores || [
      createScoreEntry({ participantId: 'p1', roundNumber: 1, scoreValue: 50 }),
      createScoreEntry({ participantId: 'p1', roundNumber: 2, scoreValue: 100 }),
      createScoreEntry({ participantId: 'p2', roundNumber: 1, scoreValue: 60 }),
      createScoreEntry({ participantId: 'p2', roundNumber: 2, scoreValue: 60 }),
    ],
    rounds: overrides?.rounds || [1, 2],
    categories: overrides?.categories || [],
  });

  describe('Compact Variant', () => {
    it('renders all participants', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} variant="compact" />);

      expect(screen.getByTestId('participant-p1')).toBeInTheDocument();
      expect(screen.getByTestId('participant-p2')).toBeInTheDocument();
      expect(screen.getByTestId('participant-p3')).toBeInTheDocument();
    });

    it('passes compact variant to ParticipantCard', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} variant="compact" />);

      const card = screen.getByTestId('participant-p1');
      expect(card).toHaveAttribute('data-variant', 'compact');
    });
  });

  describe('Full Variant', () => {
    it('renders leader podium for top 3', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} variant="full" />);

      expect(screen.getByText('Current Leaders')).toBeInTheDocument();
    });

    it('renders participants with full variant', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} variant="full" />);

      // The podium renders with full variant
      const aliceCard = screen.getAllByTestId('participant-p1')[0];
      expect(aliceCard).toHaveAttribute('data-variant', 'full');
    });

    it('shows score breakdown table when rounds exist', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} variant="full" />);

      expect(screen.getByText('Score Breakdown')).toBeInTheDocument();
      expect(screen.getByText('Player')).toBeInTheDocument();
      expect(screen.getByText('Total')).toBeInTheDocument();
    });

    it('shows round headers', () => {
      const data = createScoreboardData({ rounds: [1, 2, 3] });
      render(<Scoreboard data={data} variant="full" />);

      expect(screen.getByText('R1')).toBeInTheDocument();
      expect(screen.getByText('R2')).toBeInTheDocument();
      expect(screen.getByText('R3')).toBeInTheDocument();
    });

    it('shows category headers when categories exist', () => {
      const data = createScoreboardData({ categories: ['Points', 'Bonus'] });
      render(<Scoreboard data={data} variant="full" />);

      expect(screen.getByText('Points')).toBeInTheDocument();
      expect(screen.getByText('Bonus')).toBeInTheDocument();
    });

    it('does not show podium when no top 3 players', () => {
      const data = createScoreboardData({
        participants: [createParticipant({ rank: 4 })],
      });
      render(<Scoreboard data={data} variant="full" />);

      expect(screen.queryByText('Current Leaders')).not.toBeInTheDocument();
    });

    it('shows other players section for rank > 3', () => {
      const data = createScoreboardData({
        participants: [
          createParticipant({ id: 'p1', displayName: 'Alice', rank: 1 }),
          createParticipant({ id: 'p4', displayName: 'Dave', rank: 4 }),
          createParticipant({ id: 'p5', displayName: 'Eve', rank: 5 }),
        ],
      });
      render(<Scoreboard data={data} variant="full" />);

      expect(screen.getByText('Other Players')).toBeInTheDocument();
    });
  });

  describe('Real-time Mode', () => {
    it('shows live indicator when isRealTime is true', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} isRealTime={true} />);

      expect(screen.getByText('Live updates enabled')).toBeInTheDocument();
    });

    it('hides live indicator when isRealTime is false', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} isRealTime={false} />);

      expect(screen.queryByText('Live updates enabled')).not.toBeInTheDocument();
    });
  });

  describe('Score Display', () => {
    it('displays participant scores', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} variant="full" />);

      // Total score should be displayed
      expect(screen.getByText('150')).toBeInTheDocument();
      expect(screen.getByText('120')).toBeInTheDocument();
    });

    it('handles empty rounds gracefully', () => {
      const data = createScoreboardData({
        rounds: [],
        scores: [],
      });
      render(<Scoreboard data={data} variant="full" />);

      // Should still show podium
      expect(screen.getByText('Current Leaders')).toBeInTheDocument();
      // But no score breakdown table
      expect(screen.queryByText('Score Breakdown')).not.toBeInTheDocument();
    });
  });

  describe('Default Props', () => {
    it('uses full variant by default', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} />);

      expect(screen.getByText('Current Leaders')).toBeInTheDocument();
    });

    it('is not real-time by default', () => {
      const data = createScoreboardData();
      render(<Scoreboard data={data} />);

      expect(screen.queryByText('Live updates enabled')).not.toBeInTheDocument();
    });
  });
});
