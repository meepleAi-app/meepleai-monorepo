/**
 * SessionDetailModal Component Tests (Issue #3026)
 *
 * Test Coverage:
 * - Modal open/close behavior
 * - Session metadata display (date, participants, status, type)
 * - Scoreboard integration
 * - Participants list rendering
 * - Winner badge display
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { SessionDetailModal } from '../SessionDetailModal';
import {
  createMockSession,
  createMockSessionParticipant,
  createMockSessionParticipantList,
} from '@/__tests__/fixtures/common-fixtures';
import type { Session, ScoreboardData, Participant } from '../types';

// Mock Scoreboard component
vi.mock('../Scoreboard', () => ({
  Scoreboard: ({ data, isRealTime }: { data: ScoreboardData; isRealTime: boolean }) => (
    <div data-testid="scoreboard" data-realtime={isRealTime}>
      Scoreboard Mock - {data.participants.length} participants
    </div>
  ),
}));

describe('SessionDetailModal', () => {
  const createSession = (overrides?: Partial<Session>): Session => {
    const base = createMockSession(overrides);
    return {
      ...base,
      sessionDate: base.sessionDate,
    };
  };

  const createParticipants = (count: number = 4): Participant[] => {
    return createMockSessionParticipantList(count) as Participant[];
  };

  const createScoreboard = (participants: Participant[]): ScoreboardData => ({
    participants,
    scores: [],
    rounds: [1, 2],
    categories: [],
  });

  const defaultProps = {
    session: createSession({
      id: 'session-1',
      sessionCode: 'XYZ789',
      gameName: 'Chess',
      gameIcon: '♟️',
      status: 'Finalized' as const,
      participantCount: 4,
      sessionType: 'GameSpecific' as const,
    }),
    open: true,
    onOpenChange: vi.fn(),
  };

  describe('Modal Behavior', () => {
    it('renders when open is true', () => {
      render(<SessionDetailModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('does not render content when open is false', () => {
      render(<SessionDetailModal {...defaultProps} open={false} />);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('calls onOpenChange when closing', () => {
      const onOpenChange = vi.fn();
      render(<SessionDetailModal {...defaultProps} onOpenChange={onOpenChange} />);

      const closeButton = screen.getByRole('button', { name: /close/i });
      fireEvent.click(closeButton);

      expect(onOpenChange).toHaveBeenCalledWith(false);
    });
  });

  describe('Session Header', () => {
    it('displays game name and session code in title', () => {
      render(<SessionDetailModal {...defaultProps} />);

      expect(screen.getByText('Chess - XYZ789')).toBeInTheDocument();
    });

    it('displays game icon when provided', () => {
      render(<SessionDetailModal {...defaultProps} />);

      expect(screen.getByText('♟️')).toBeInTheDocument();
    });

    it('displays "Session" when no game name', () => {
      const session = createSession({ gameName: undefined, sessionCode: 'ABC123' });
      render(<SessionDetailModal {...defaultProps} session={session} />);

      expect(screen.getByText('Session - ABC123')).toBeInTheDocument();
    });

    it('displays dialog description', () => {
      render(<SessionDetailModal {...defaultProps} />);

      expect(screen.getByText('Session details and final scores')).toBeInTheDocument();
    });
  });

  describe('Session Metadata', () => {
    it('displays session date', () => {
      render(<SessionDetailModal {...defaultProps} />);

      expect(screen.getByText('Date')).toBeInTheDocument();
    });

    it('displays participant count', () => {
      render(<SessionDetailModal {...defaultProps} />);

      expect(screen.getByText('Participants')).toBeInTheDocument();
      expect(screen.getByText('4 players')).toBeInTheDocument();
    });

    it('displays session status as badge', () => {
      render(<SessionDetailModal {...defaultProps} />);

      expect(screen.getByText('Finalized')).toBeInTheDocument();
    });

    it('displays session type', () => {
      render(<SessionDetailModal {...defaultProps} />);

      expect(screen.getByText('Type')).toBeInTheDocument();
      expect(screen.getByText('GameSpecific')).toBeInTheDocument();
    });

    it('shows Active status correctly', () => {
      const session = createSession({ status: 'Active' });
      render(<SessionDetailModal {...defaultProps} session={session} />);

      expect(screen.getByText('Active')).toBeInTheDocument();
    });
  });

  describe('Scoreboard Integration', () => {
    it('renders scoreboard component', () => {
      const participants = createParticipants(3);
      const scoreboard = createScoreboard(participants);
      render(
        <SessionDetailModal {...defaultProps} scoreboard={scoreboard} participants={participants} />
      );

      expect(screen.getByTestId('scoreboard')).toBeInTheDocument();
    });

    it('passes isRealTime=false to scoreboard', () => {
      const participants = createParticipants(3);
      const scoreboard = createScoreboard(participants);
      render(
        <SessionDetailModal {...defaultProps} scoreboard={scoreboard} participants={participants} />
      );

      expect(screen.getByTestId('scoreboard')).toHaveAttribute('data-realtime', 'false');
    });

    it('shows Final Scores heading', () => {
      const participants = createParticipants(2);
      const scoreboard = createScoreboard(participants);
      render(
        <SessionDetailModal {...defaultProps} scoreboard={scoreboard} participants={participants} />
      );

      expect(screen.getByText('Final Scores')).toBeInTheDocument();
    });

    it('creates default scoreboard when not provided', () => {
      const participants = createParticipants(2);
      render(<SessionDetailModal {...defaultProps} participants={participants} />);

      expect(screen.getByTestId('scoreboard')).toBeInTheDocument();
      expect(screen.getByText('Scoreboard Mock - 2 participants')).toBeInTheDocument();
    });
  });

  describe('Participants List', () => {
    it('renders participants section when participants provided', () => {
      const participants = createParticipants(3);
      render(<SessionDetailModal {...defaultProps} participants={participants} />);

      // Two "Participants" texts - one in metadata, one as section heading
      const participantTexts = screen.getAllByText('Participants');
      expect(participantTexts.length).toBeGreaterThanOrEqual(2);
    });

    it('displays all participant names', () => {
      const participants = createParticipants(3);
      render(<SessionDetailModal {...defaultProps} participants={participants} />);

      expect(screen.getByText('Alice')).toBeInTheDocument();
      expect(screen.getByText('Bob')).toBeInTheDocument();
      expect(screen.getByText('Charlie')).toBeInTheDocument();
    });

    it('displays participant scores', () => {
      const participants = [
        createMockSessionParticipant({ id: 'p1', displayName: 'Alice', totalScore: 150 }),
        createMockSessionParticipant({ id: 'p2', displayName: 'Bob', totalScore: 120 }),
      ] as Participant[];
      render(<SessionDetailModal {...defaultProps} participants={participants} />);

      expect(screen.getByText('150')).toBeInTheDocument();
      expect(screen.getByText('120')).toBeInTheDocument();
    });

    it('displays rank badges', () => {
      const participants = [
        createMockSessionParticipant({ id: 'p1', displayName: 'Alice', rank: 1 }),
        createMockSessionParticipant({ id: 'p2', displayName: 'Bob', rank: 2 }),
      ] as Participant[];
      render(<SessionDetailModal {...defaultProps} participants={participants} />);

      expect(screen.getByText(/🥇.*#1/)).toBeInTheDocument();
      expect(screen.getByText('#2')).toBeInTheDocument();
    });

    it('displays first initial in avatar', () => {
      const participants = [
        createMockSessionParticipant({ displayName: 'alice' }),
      ] as Participant[];
      render(<SessionDetailModal {...defaultProps} participants={participants} />);

      expect(screen.getByText('A')).toBeInTheDocument();
    });

    it('renders avatar with participant initial', () => {
      const participants = [
        createMockSessionParticipant({ id: 'p1', displayName: 'Alice', avatarColor: '#ef4444' }),
      ] as Participant[];
      render(<SessionDetailModal {...defaultProps} participants={participants} />);

      // Check that we have an avatar with the initial (already tested in displays first initial)
      expect(screen.getByText('A')).toBeInTheDocument();
      // Also check the participant name is displayed
      expect(screen.getByText('Alice')).toBeInTheDocument();
    });

    it('sorts participants by rank', () => {
      const participants = [
        createMockSessionParticipant({ id: 'p3', displayName: 'Third', rank: 3 }),
        createMockSessionParticipant({ id: 'p1', displayName: 'First', rank: 1 }),
        createMockSessionParticipant({ id: 'p2', displayName: 'Second', rank: 2 }),
      ] as Participant[];
      render(<SessionDetailModal {...defaultProps} participants={participants} />);

      const names = screen.getAllByText(/First|Second|Third/);
      expect(names[0]).toHaveTextContent('First');
      expect(names[1]).toHaveTextContent('Second');
      expect(names[2]).toHaveTextContent('Third');
    });

    it('does not render participants section when empty', () => {
      render(<SessionDetailModal {...defaultProps} participants={[]} />);

      // Should only show one "Participants" text (the metadata one)
      const participantTexts = screen.getAllByText('Participants');
      expect(participantTexts).toHaveLength(1);
    });

    it('does not render participants section when undefined', () => {
      render(<SessionDetailModal {...defaultProps} participants={undefined} />);

      // Should only show one "Participants" text (the metadata one)
      const participantTexts = screen.getAllByText('Participants');
      expect(participantTexts).toHaveLength(1);
    });
  });
});
