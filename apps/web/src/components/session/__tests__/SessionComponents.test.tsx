/**
 * Tests for Session UI Components
 * Issue #4767 - SSE Client + Player/Spectator Mode UI
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ConnectionStatusBadge } from '../ConnectionStatusBadge';
import { SessionJoinForm } from '../SessionJoinForm';
import { PlayerModeCard } from '../PlayerModeCard';
import { SpectatorModeCard } from '../SpectatorModeCard';
import { SessionLobby } from '../SessionLobby';

import type { Participant, ScoreEntry } from '@/components/session/types';

// ============ Test Helpers ============

const mockParticipants: Participant[] = [
  {
    id: 'p-1',
    displayName: 'Alice',
    isOwner: true,
    isCurrentUser: false,
    avatarColor: '#6366f1',
    totalScore: 42,
    rank: 1,
  },
  {
    id: 'p-2',
    displayName: 'Bob',
    isOwner: false,
    isCurrentUser: true,
    avatarColor: '#ec4899',
    totalScore: 35,
    rank: 2,
  },
];

const mockScores: ScoreEntry[] = [
  {
    id: 'se-1',
    participantId: 'p-1',
    roundNumber: 1,
    category: null,
    scoreValue: 42,
    timestamp: new Date(),
    createdBy: 'p-1',
  },
];

// ============ ConnectionStatusBadge Tests ============

describe('ConnectionStatusBadge', () => {
  it('shows connected state', () => {
    render(<ConnectionStatusBadge status="connected" />);
    expect(screen.getByText('Connected')).toBeInTheDocument();
  });

  it('shows connecting state with animation', () => {
    render(<ConnectionStatusBadge status="connecting" />);
    expect(screen.getByText('Connecting...')).toBeInTheDocument();
  });

  it('shows reconnecting state with count', () => {
    render(<ConnectionStatusBadge status="reconnecting" reconnectCount={3} />);
    expect(screen.getByText('Reconnecting... (3)')).toBeInTheDocument();
  });

  it('shows offline state', () => {
    render(<ConnectionStatusBadge status="disconnected" />);
    expect(screen.getByText('Offline')).toBeInTheDocument();
  });

  it('shows failed state', () => {
    render(<ConnectionStatusBadge status="failed" />);
    expect(screen.getByText('Connection Failed')).toBeInTheDocument();
  });

  it('has accessible role', () => {
    render(<ConnectionStatusBadge status="connected" />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });
});

// ============ SessionJoinForm Tests ============

describe('SessionJoinForm', () => {
  const mockOnJoin = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    mockOnJoin.mockClear();
  });

  it('renders session code and display name inputs', () => {
    render(<SessionJoinForm onJoin={mockOnJoin} />);
    expect(screen.getByLabelText('Session Code')).toBeInTheDocument();
    expect(screen.getByLabelText('Your Name')).toBeInTheDocument();
  });

  it('auto-uppercases and limits session code to 6 chars', async () => {
    const user = userEvent.setup();
    render(<SessionJoinForm onJoin={mockOnJoin} />);

    const codeInput = screen.getByLabelText('Session Code');
    await user.type(codeInput, 'abc123xyz');
    expect(codeInput).toHaveValue('ABC123');
  });

  it('disables submit when code is incomplete', () => {
    render(<SessionJoinForm onJoin={mockOnJoin} />);
    const button = screen.getByRole('button', { name: /join session/i });
    expect(button).toBeDisabled();
  });

  it('calls onJoin with code and name', async () => {
    const user = userEvent.setup();
    render(<SessionJoinForm onJoin={mockOnJoin} />);

    await user.type(screen.getByLabelText('Session Code'), 'ABC123');
    await user.type(screen.getByLabelText('Your Name'), 'Alice');
    await user.click(screen.getByRole('button', { name: /join session/i }));

    expect(mockOnJoin).toHaveBeenCalledWith('ABC123', 'Alice');
  });

  it('shows error message', () => {
    render(<SessionJoinForm onJoin={mockOnJoin} error="Session not found" />);
    expect(screen.getByText('Session not found')).toBeInTheDocument();
  });

  it('shows loading state', () => {
    render(<SessionJoinForm onJoin={mockOnJoin} isLoading />);
    expect(screen.getByText('Joining...')).toBeInTheDocument();
  });

  it('strips special characters from code', async () => {
    const user = userEvent.setup();
    render(<SessionJoinForm onJoin={mockOnJoin} />);

    const codeInput = screen.getByLabelText('Session Code');
    await user.type(codeInput, 'AB-C1!2@3');
    expect(codeInput).toHaveValue('ABC123');
  });
});

// ============ PlayerModeCard Tests ============

describe('PlayerModeCard', () => {
  const defaultProps = {
    sessionId: 'session-1',
    currentParticipant: mockParticipants[1],
    participants: mockParticipants,
    scores: mockScores,
    sessionStatus: 'Active' as const,
    isHost: false,
  };

  it('renders player mode card with participants', () => {
    render(<PlayerModeCard {...defaultProps} />);
    expect(screen.getByTestId('player-mode-card')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('shows score input and dice button when active', () => {
    render(<PlayerModeCard {...defaultProps} />);
    expect(screen.getByLabelText('Score value')).toBeInTheDocument();
    expect(screen.getByLabelText('Roll dice')).toBeInTheDocument();
  });

  it('shows ready button for non-host players', () => {
    render(<PlayerModeCard {...defaultProps} />);
    expect(screen.getByLabelText('Mark as ready')).toBeInTheDocument();
  });

  it('calls onMarkReady when ready button is clicked', async () => {
    const onMarkReady = vi.fn();
    const user = userEvent.setup();
    render(<PlayerModeCard {...defaultProps} onMarkReady={onMarkReady} />);

    await user.click(screen.getByLabelText('Mark as ready'));
    expect(onMarkReady).toHaveBeenCalled();
    expect(screen.getByText('Ready')).toBeInTheDocument();
  });

  it('shows host controls for hosts', () => {
    render(<PlayerModeCard {...defaultProps} isHost />);
    expect(screen.getByLabelText('Pause session')).toBeInTheDocument();
    expect(screen.queryByLabelText('Mark as ready')).not.toBeInTheDocument();
  });

  it('shows resume button when paused for host', () => {
    render(
      <PlayerModeCard {...defaultProps} isHost sessionStatus="Paused" />
    );
    expect(screen.getByLabelText('Resume session')).toBeInTheDocument();
  });

  it('hides interactive controls when finalized', () => {
    render(<PlayerModeCard {...defaultProps} sessionStatus="Finalized" />);
    expect(screen.queryByLabelText('Score value')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Roll dice')).not.toBeInTheDocument();
  });

  it('calls onUpdateScore when submitting score', async () => {
    const onUpdateScore = vi.fn();
    const user = userEvent.setup();
    render(<PlayerModeCard {...defaultProps} onUpdateScore={onUpdateScore} />);

    await user.type(screen.getByLabelText('Score value'), '10');
    await user.click(screen.getByLabelText('Submit score'));

    expect(onUpdateScore).toHaveBeenCalledWith('p-2', 10);
  });

  it('highlights current user', () => {
    render(<PlayerModeCard {...defaultProps} />);
    expect(screen.getByText('(you)')).toBeInTheDocument();
  });

  it('shows Host badge for owners', () => {
    render(<PlayerModeCard {...defaultProps} />);
    expect(screen.getByText('Host')).toBeInTheDocument();
  });
});

// ============ SpectatorModeCard Tests ============

describe('SpectatorModeCard', () => {
  const defaultProps = {
    sessionId: 'session-1',
    participants: mockParticipants,
    scores: mockScores,
    sessionStatus: 'Active' as const,
  };

  it('renders spectator mode card', () => {
    render(<SpectatorModeCard {...defaultProps} />);
    expect(screen.getByTestId('spectator-mode-card')).toBeInTheDocument();
    expect(screen.getByText('Spectator Mode')).toBeInTheDocument();
  });

  it('shows read-only scoreboard', () => {
    render(<SpectatorModeCard {...defaultProps} />);
    expect(screen.getByText('Live Scoreboard')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
  });

  it('shows request to play button', () => {
    render(<SpectatorModeCard {...defaultProps} />);
    expect(screen.getByText('Request to Play')).toBeInTheDocument();
  });

  it('calls onRequestToPlay and shows sent state', async () => {
    const onRequestToPlay = vi.fn();
    const user = userEvent.setup();
    render(<SpectatorModeCard {...defaultProps} onRequestToPlay={onRequestToPlay} />);

    await user.click(screen.getByText('Request to Play'));
    expect(onRequestToPlay).toHaveBeenCalled();
    expect(screen.getByText('Request sent')).toBeInTheDocument();
    expect(screen.queryByText('Request to Play')).not.toBeInTheDocument();
  });

  it('hides request button when finalized', () => {
    render(<SpectatorModeCard {...defaultProps} sessionStatus="Finalized" />);
    expect(screen.queryByText('Request to Play')).not.toBeInTheDocument();
  });

  it('shows chat availability hint', () => {
    render(<SpectatorModeCard {...defaultProps} />);
    expect(screen.getByText(/chat is available/i)).toBeInTheDocument();
  });

  it('sorts participants by score descending', () => {
    render(<SpectatorModeCard {...defaultProps} />);
    const names = screen.getAllByText(/Alice|Bob/);
    // Alice has 42, Bob has 35 → Alice first
    expect(names[0]).toHaveTextContent('Alice');
    expect(names[1]).toHaveTextContent('Bob');
  });

  it('does not show score input or dice controls', () => {
    render(<SpectatorModeCard {...defaultProps} />);
    expect(screen.queryByLabelText('Score value')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Roll dice')).not.toBeInTheDocument();
  });
});

// ============ SessionLobby Tests ============

describe('SessionLobby', () => {
  const defaultProps = {
    sessionCode: 'ABC123',
    participants: mockParticipants,
    connectionStatus: 'connected' as const,
  };

  it('renders session lobby with code', () => {
    render(<SessionLobby {...defaultProps} />);
    expect(screen.getByTestId('session-lobby')).toBeInTheDocument();
    expect(screen.getByText('ABC123')).toBeInTheDocument();
  });

  it('shows session name when provided', () => {
    render(<SessionLobby {...defaultProps} sessionName="Catan Night" />);
    expect(screen.getByText('Catan Night')).toBeInTheDocument();
  });

  it('displays participant count', () => {
    render(<SessionLobby {...defaultProps} />);
    expect(screen.getByText('2 players joined')).toBeInTheDocument();
  });

  it('shows singular form for 1 player', () => {
    render(
      <SessionLobby {...defaultProps} participants={[mockParticipants[0]]} />
    );
    expect(screen.getByText('1 player joined')).toBeInTheDocument();
  });

  it('shows waiting message when no participants', () => {
    render(<SessionLobby {...defaultProps} participants={[]} />);
    expect(screen.getByText(/waiting for players/i)).toBeInTheDocument();
  });

  it('displays connection status badge', () => {
    render(<SessionLobby {...defaultProps} connectionStatus="reconnecting" reconnectCount={2} />);
    expect(screen.getByText('Reconnecting... (2)')).toBeInTheDocument();
  });

  it('has copy button for session code', () => {
    render(<SessionLobby {...defaultProps} />);
    expect(screen.getByLabelText(/copy session code/i)).toBeInTheDocument();
  });

  it('shows Host badge on owner', () => {
    render(<SessionLobby {...defaultProps} />);
    expect(screen.getByText('Host')).toBeInTheDocument();
  });
});
