/**
 * Tests for Session-specific MeepleCard components
 * Issue #4751 - MeepleCard Session Front + Relationship Links Footer
 */

import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { SessionActionButtons } from '../SessionActionButtons';
import { SessionScoreModal } from '../SessionScoreModal';
import { SessionScoreTable } from '../SessionScoreTable';
import { SessionStatusBadge } from '../SessionStatusBadge';
import { SessionTurnSequence } from '../SessionTurnSequence';
import { SessionPlayerPopup } from '../SessionPlayerPopup';
import type {
  SessionActionHandlers,
  SessionPlayerInfo,
  SessionRoundScore,
  SessionScoringConfig,
  SessionTurnInfo,
} from '../session-types';

// ============================================================================
// Test Data
// ============================================================================

const mockPlayers: SessionPlayerInfo[] = [
  {
    id: 'p1',
    displayName: 'Alice',
    color: 'red',
    role: 'host',
    totalScore: 42,
    currentRank: 1,
    isActive: true,
  },
  {
    id: 'p2',
    displayName: 'Bob',
    color: 'blue',
    role: 'player',
    totalScore: 35,
    currentRank: 2,
    isActive: true,
  },
  {
    id: 'p3',
    displayName: 'Carol',
    color: 'green',
    role: 'player',
    totalScore: 28,
    currentRank: 3,
    isActive: true,
  },
];

const mockRoundScores: SessionRoundScore[] = [
  { playerId: 'p1', round: 1, dimension: 'default', value: 15 },
  { playerId: 'p1', round: 2, dimension: 'default', value: 27 },
  { playerId: 'p2', round: 1, dimension: 'default', value: 18 },
  { playerId: 'p2', round: 2, dimension: 'default', value: 17 },
  { playerId: 'p3', round: 1, dimension: 'default', value: 12 },
  { playerId: 'p3', round: 2, dimension: 'default', value: 16 },
];

const mockScoringConfig: SessionScoringConfig = {
  enabledDimensions: ['default'],
  dimensionUnits: {},
};

const mockTurn: SessionTurnInfo = {
  currentIndex: 0,
  currentPlayerId: 'p1',
};

// ============================================================================
// SessionStatusBadge Tests
// ============================================================================

describe('SessionStatusBadge', () => {
  it('renders setup status with blue color', () => {
    render(<SessionStatusBadge status="setup" />);
    const badge = screen.getByTestId('session-status-setup');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveTextContent('Setup');
  });

  it('renders inProgress status with pulsating dot', () => {
    render(<SessionStatusBadge status="inProgress" />);
    const badge = screen.getByTestId('session-status-inProgress');
    expect(badge).toHaveTextContent('In Corso');
    // Check for pulsating dot (animate-ping)
    const dots = badge.querySelectorAll('.animate-ping');
    expect(dots.length).toBe(1);
  });

  it('renders paused status', () => {
    render(<SessionStatusBadge status="paused" />);
    expect(screen.getByTestId('session-status-paused')).toHaveTextContent('In Pausa');
  });

  it('renders completed status', () => {
    render(<SessionStatusBadge status="completed" />);
    expect(screen.getByTestId('session-status-completed')).toHaveTextContent('Completata');
  });

  it('hides label when showLabel is false', () => {
    render(<SessionStatusBadge status="setup" showLabel={false} />);
    const badge = screen.getByTestId('session-status-setup');
    expect(badge).not.toHaveTextContent('Setup');
  });
});

// ============================================================================
// SessionScoreTable Tests
// ============================================================================

describe('SessionScoreTable', () => {
  it('renders score matrix with players and rounds', () => {
    render(
      <SessionScoreTable
        players={mockPlayers}
        roundScores={mockRoundScores}
      />,
    );
    const table = screen.getByTestId('session-score-table');
    expect(table).toBeInTheDocument();

    // Check all players are rendered
    expect(screen.getByTestId('score-row-p1')).toBeInTheDocument();
    expect(screen.getByTestId('score-row-p2')).toBeInTheDocument();
    expect(screen.getByTestId('score-row-p3')).toBeInTheDocument();
  });

  it('highlights leader with crown', () => {
    render(
      <SessionScoreTable
        players={mockPlayers}
        roundScores={mockRoundScores}
      />,
    );
    // Alice has highest score (42)
    const aliceRow = screen.getByTestId('score-row-p1');
    expect(aliceRow.querySelector('[aria-label="Leader"]')).toBeInTheDocument();
  });

  it('renders edit button when onEditScore provided', () => {
    const onEdit = vi.fn();
    render(
      <SessionScoreTable
        players={mockPlayers}
        roundScores={mockRoundScores}
        onEditScore={onEdit}
      />,
    );
    const editButton = screen.getByTestId('edit-score-button');
    fireEvent.click(editButton);
    expect(onEdit).toHaveBeenCalledOnce();
  });

  it('does not render edit button when onEditScore not provided', () => {
    render(
      <SessionScoreTable
        players={mockPlayers}
        roundScores={mockRoundScores}
      />,
    );
    expect(screen.queryByTestId('edit-score-button')).not.toBeInTheDocument();
  });

  it('renders nothing for empty players', () => {
    const { container } = render(
      <SessionScoreTable players={[]} roundScores={[]} />,
    );
    expect(container.firstChild).toBeNull();
  });
});

// ============================================================================
// SessionTurnSequence Tests
// ============================================================================

describe('SessionTurnSequence', () => {
  it('renders player chips in turn order', () => {
    render(
      <SessionTurnSequence players={mockPlayers} turn={mockTurn} />,
    );
    const sequence = screen.getByTestId('session-turn-sequence');
    expect(sequence).toBeInTheDocument();

    // Check all non-spectator players are shown
    expect(screen.getByTestId('turn-chip-p1')).toBeInTheDocument();
    expect(screen.getByTestId('turn-chip-p2')).toBeInTheDocument();
    expect(screen.getByTestId('turn-chip-p3')).toBeInTheDocument();
  });

  it('highlights active player with aria-current', () => {
    render(
      <SessionTurnSequence players={mockPlayers} turn={mockTurn} />,
    );
    const activeChip = screen.getByTestId('turn-chip-p1');
    expect(activeChip).toHaveAttribute('aria-current', 'step');
  });

  it('shows prev/next buttons for host', () => {
    const onPrev = vi.fn();
    const onNext = vi.fn();
    render(
      <SessionTurnSequence
        players={mockPlayers}
        turn={mockTurn}
        isHost={true}
        onPrevTurn={onPrev}
        onNextTurn={onNext}
      />,
    );
    const prev = screen.getByTestId('turn-prev');
    const next = screen.getByTestId('turn-next');
    fireEvent.click(prev);
    fireEvent.click(next);
    expect(onPrev).toHaveBeenCalledOnce();
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('hides nav buttons for non-host', () => {
    render(
      <SessionTurnSequence players={mockPlayers} turn={mockTurn} isHost={false} />,
    );
    expect(screen.queryByTestId('turn-prev')).not.toBeInTheDocument();
    expect(screen.queryByTestId('turn-next')).not.toBeInTheDocument();
  });

  it('filters out spectators', () => {
    const playersWithSpectator: SessionPlayerInfo[] = [
      ...mockPlayers,
      {
        id: 'p4',
        displayName: 'Dave',
        color: 'yellow',
        role: 'spectator',
        totalScore: 0,
        currentRank: 4,
        isActive: true,
      },
    ];
    render(
      <SessionTurnSequence players={playersWithSpectator} turn={mockTurn} />,
    );
    expect(screen.queryByTestId('turn-chip-p4')).not.toBeInTheDocument();
  });
});

// ============================================================================
// SessionActionButtons Tests
// ============================================================================

describe('SessionActionButtons', () => {
  it('renders setup actions', () => {
    const actions: SessionActionHandlers = {
      onAddPlayer: vi.fn(),
      onStart: vi.fn(),
    };
    render(<SessionActionButtons status="setup" actions={actions} />);
    expect(screen.getByTestId('session-action-aggiungi-player')).toBeInTheDocument();
    expect(screen.getByTestId('session-action-inizia-partita')).toBeInTheDocument();
  });

  it('renders inProgress actions', () => {
    const actions: SessionActionHandlers = {
      onPause: vi.fn(),
      onSave: vi.fn(),
    };
    render(<SessionActionButtons status="inProgress" actions={actions} />);
    expect(screen.getByTestId('session-action-pausa')).toBeInTheDocument();
    expect(screen.getByTestId('session-action-salva')).toBeInTheDocument();
  });

  it('renders paused actions', () => {
    const actions: SessionActionHandlers = {
      onResume: vi.fn(),
      onSave: vi.fn(),
    };
    render(<SessionActionButtons status="paused" actions={actions} />);
    expect(screen.getByTestId('session-action-riprendi')).toBeInTheDocument();
    expect(screen.getByTestId('session-action-salva')).toBeInTheDocument();
  });

  it('renders completed actions', () => {
    const actions: SessionActionHandlers = {
      onViewPlayRecord: vi.fn(),
      onRematch: vi.fn(),
    };
    render(<SessionActionButtons status="completed" actions={actions} />);
    expect(screen.getByTestId('session-action-playrecord')).toBeInTheDocument();
    expect(screen.getByTestId('session-action-rivincita')).toBeInTheDocument();
  });

  it('calls handler on click', () => {
    const onPause = vi.fn();
    render(
      <SessionActionButtons
        status="inProgress"
        actions={{ onPause, onSave: vi.fn() }}
      />,
    );
    fireEvent.click(screen.getByTestId('session-action-pausa'));
    expect(onPause).toHaveBeenCalledOnce();
  });
});

// ============================================================================
// SessionScoreModal Tests
// ============================================================================

describe('SessionScoreModal', () => {
  it('does not render when closed', () => {
    render(
      <SessionScoreModal
        open={false}
        onClose={vi.fn()}
        players={mockPlayers}
        roundScores={mockRoundScores}
        scoringConfig={mockScoringConfig}
        onSave={vi.fn()}
      />,
    );
    expect(screen.queryByTestId('score-modal')).not.toBeInTheDocument();
  });

  it('renders when open', () => {
    render(
      <SessionScoreModal
        open={true}
        onClose={vi.fn()}
        players={mockPlayers}
        roundScores={mockRoundScores}
        scoringConfig={mockScoringConfig}
        onSave={vi.fn()}
      />,
    );
    expect(screen.getByTestId('score-modal')).toBeInTheDocument();
    expect(screen.getByText('Modifica Punteggio')).toBeInTheDocument();
  });

  it('renders score inputs for each player and round', () => {
    render(
      <SessionScoreModal
        open={true}
        onClose={vi.fn()}
        players={mockPlayers}
        roundScores={mockRoundScores}
        scoringConfig={mockScoringConfig}
        onSave={vi.fn()}
      />,
    );
    // Alice round 1 input
    expect(screen.getByTestId('score-input-p1-r1')).toBeInTheDocument();
    expect(screen.getByTestId('score-input-p1-r2')).toBeInTheDocument();
  });

  it('calls onClose when cancel clicked', () => {
    const onClose = vi.fn();
    render(
      <SessionScoreModal
        open={true}
        onClose={onClose}
        players={mockPlayers}
        roundScores={mockRoundScores}
        scoringConfig={mockScoringConfig}
        onSave={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByText('Annulla'));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onSave with updated scores', () => {
    const onSave = vi.fn();
    const onClose = vi.fn();
    render(
      <SessionScoreModal
        open={true}
        onClose={onClose}
        players={mockPlayers}
        roundScores={mockRoundScores}
        scoringConfig={mockScoringConfig}
        onSave={onSave}
      />,
    );
    // Change a score
    const input = screen.getByTestId('score-input-p1-r1');
    fireEvent.change(input, { target: { value: '20' } });
    // Save
    fireEvent.click(screen.getByTestId('score-modal-save'));
    expect(onSave).toHaveBeenCalledOnce();
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('validates numeric input only', () => {
    render(
      <SessionScoreModal
        open={true}
        onClose={vi.fn()}
        players={mockPlayers}
        roundScores={mockRoundScores}
        scoringConfig={mockScoringConfig}
        onSave={vi.fn()}
      />,
    );
    const input = screen.getByTestId('score-input-p1-r1') as HTMLInputElement;
    // Try non-numeric
    fireEvent.change(input, { target: { value: 'abc' } });
    // Should stay at original value (15) since 'abc' is rejected
    expect(input.value).toBe('15');
  });
});

// ============================================================================
// SessionPlayerPopup Tests
// ============================================================================

describe('SessionPlayerPopup', () => {
  it('renders children (trigger element)', () => {
    render(
      <SessionPlayerPopup players={mockPlayers}>
        <span data-testid="trigger">Players</span>
      </SessionPlayerPopup>,
    );
    expect(screen.getByTestId('trigger')).toBeInTheDocument();
  });

  it('shows popup on hover', () => {
    render(
      <SessionPlayerPopup players={mockPlayers}>
        <span>Players</span>
      </SessionPlayerPopup>,
    );
    fireEvent.mouseEnter(screen.getByTestId('session-player-popup-trigger'));
    expect(screen.getByTestId('session-player-popup')).toBeInTheDocument();
    expect(screen.getByTestId('player-popup-p1')).toBeInTheDocument();
    expect(screen.getByTestId('player-popup-p2')).toBeInTheDocument();
  });

  it('renders player info with roles', () => {
    render(
      <SessionPlayerPopup players={mockPlayers}>
        <span>Players</span>
      </SessionPlayerPopup>,
    );
    fireEvent.mouseEnter(screen.getByTestId('session-player-popup-trigger'));

    const aliceCard = screen.getByTestId('player-popup-p1');
    expect(within(aliceCard).getByText('Alice')).toBeInTheDocument();
    expect(within(aliceCard).getByText(/Host/)).toBeInTheDocument();
    expect(within(aliceCard).getByText(/42 pts/)).toBeInTheDocument();
  });
});
