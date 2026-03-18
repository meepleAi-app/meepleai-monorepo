/**
 * DisputeVoting Tests
 *
 * Arbitro v2 — Task 15
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { DisputeVoting } from '../DisputeVoting';

// ─── Mock API ─────────────────────────────────────────────────────────────────

const mockCastVote = vi.hoisted(() => vi.fn());
const mockTallyVotes = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      castVote: mockCastVote,
      tallyVotes: mockTallyVotes,
    },
  },
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const mockPlayers = [
  { id: 'player-1', name: 'Alice' },
  { id: 'player-2', name: 'Bob' },
  { id: 'player-3', name: 'Charlie' },
];

const defaultProps = {
  disputeId: 'dispute-1',
  sessionId: 'session-1',
  players: mockPlayers,
  onVotingComplete: vi.fn(),
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('DisputeVoting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockCastVote.mockResolvedValue(undefined);
    mockTallyVotes.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders accept and reject buttons for each player', () => {
    render(<DisputeVoting {...defaultProps} />);

    for (const player of mockPlayers) {
      expect(screen.getByLabelText(`${player.name} accetta`)).toBeInTheDocument();
      expect(screen.getByLabelText(`${player.name} rifiuta`)).toBeInTheDocument();
    }
  });

  it('renders the timer countdown', () => {
    render(<DisputeVoting {...defaultProps} />);

    const timer = screen.getByTestId('vote-timer');
    expect(timer).toHaveTextContent('60s');
  });

  it('renders initial vote tally at 0/0', () => {
    render(<DisputeVoting {...defaultProps} />);

    expect(screen.getByTestId('accept-count')).toHaveTextContent('0');
    expect(screen.getByTestId('reject-count')).toHaveTextContent('0');
  });

  it('displays player names', () => {
    render(<DisputeVoting {...defaultProps} />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });

  it('calls castVote API when clicking accept', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<DisputeVoting {...defaultProps} />);

    await user.click(screen.getByLabelText('Alice accetta'));

    expect(mockCastVote).toHaveBeenCalledWith('session-1', 'dispute-1', 'player-1', true);
  });

  it('calls castVote API when clicking reject', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<DisputeVoting {...defaultProps} />);

    await user.click(screen.getByLabelText('Bob rifiuta'));

    expect(mockCastVote).toHaveBeenCalledWith('session-1', 'dispute-1', 'player-2', false);
  });

  it('updates vote tally after a vote is cast', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<DisputeVoting {...defaultProps} />);

    await user.click(screen.getByLabelText('Alice accetta'));

    await waitFor(() => {
      expect(screen.getByTestId('accept-count')).toHaveTextContent('1');
    });
  });

  it('shows voted badge after a player votes', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<DisputeVoting {...defaultProps} />);

    await user.click(screen.getByLabelText('Alice accetta'));

    await waitFor(() => {
      expect(screen.getByText('Accettato')).toBeInTheDocument();
    });
  });

  it('prevents double voting by the same player', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<DisputeVoting {...defaultProps} />);

    await user.click(screen.getByLabelText('Alice accetta'));

    await waitFor(() => {
      expect(screen.getByText('Accettato')).toBeInTheDocument();
    });

    // The accept/reject buttons for Alice should no longer be visible
    expect(screen.queryByLabelText('Alice accetta')).not.toBeInTheDocument();
  });

  it('timer counts down over time', async () => {
    render(<DisputeVoting {...defaultProps} />);

    const timer = screen.getByTestId('vote-timer');
    expect(timer).toHaveTextContent('60s');

    act(() => {
      vi.advanceTimersByTime(5000);
    });

    await waitFor(() => {
      expect(timer).toHaveTextContent('55s');
    });
  });

  it('calls tallyVotes and onVotingComplete when all players have voted', async () => {
    const onVotingComplete = vi.fn();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    render(<DisputeVoting {...defaultProps} onVotingComplete={onVotingComplete} />);

    // All players vote accept
    await user.click(screen.getByLabelText('Alice accetta'));
    await waitFor(() => expect(screen.getByText('Accettato')).toBeInTheDocument());

    await user.click(screen.getByLabelText('Bob accetta'));
    await waitFor(() => expect(screen.getAllByText('Accettato')).toHaveLength(2));

    await user.click(screen.getByLabelText('Charlie accetta'));

    await waitFor(() => {
      expect(mockTallyVotes).toHaveBeenCalledWith('session-1', 'dispute-1');
    });

    await waitFor(() => {
      expect(onVotingComplete).toHaveBeenCalledWith('VerdictAccepted');
    });
  });

  it('fires VerdictOverridden when majority rejects', async () => {
    const onVotingComplete = vi.fn();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    render(<DisputeVoting {...defaultProps} onVotingComplete={onVotingComplete} />);

    await user.click(screen.getByLabelText('Alice rifiuta'));
    await waitFor(() => expect(screen.getByText('Rifiutato')).toBeInTheDocument());

    await user.click(screen.getByLabelText('Bob rifiuta'));
    await waitFor(() => expect(screen.getAllByText('Rifiutato')).toHaveLength(2));

    await user.click(screen.getByLabelText('Charlie accetta'));

    await waitFor(() => {
      expect(onVotingComplete).toHaveBeenCalledWith('VerdictOverridden');
    });
  });

  it('auto-finalizes when timer reaches zero', async () => {
    const onVotingComplete = vi.fn();
    render(<DisputeVoting {...defaultProps} onVotingComplete={onVotingComplete} />);

    act(() => {
      vi.advanceTimersByTime(61000);
    });

    await waitFor(() => {
      expect(mockTallyVotes).toHaveBeenCalledWith('session-1', 'dispute-1');
    });

    await waitFor(() => {
      expect(onVotingComplete).toHaveBeenCalled();
    });
  });

  it('shows error message when vote API fails', async () => {
    mockCastVote.mockRejectedValueOnce(new Error('Network error'));
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    render(<DisputeVoting {...defaultProps} />);

    await user.click(screen.getByLabelText('Alice accetta'));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Errore durante il voto.');
    });
  });
});
