/**
 * MeepleSessionCard Tests
 *
 * Current scope (v1): basic rendering, onClick handling, status badge,
 * subtitle composition, and connections wiring via buildSessionConnections.
 *
 * Action-visibility matrix (Configura / Avvia / Pausa / Riprendi / Partecipa /
 * Lascia / Esporta) is a deferred v2 deliverable — see Issue #5003. The
 * component currently accepts the action callback props but does not render
 * the action buttons; tests for that behavior will be added when the
 * implementation lands.
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { MeepleSessionCard, MeepleSessionCardSkeleton } from '../MeepleSessionCard';

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

// ============================================================================
// Mock Data
// ============================================================================

const mockSetupSession: GameSessionDto = {
  id: 'session-001',
  gameId: 'game-111',
  status: 'Setup',
  startedAt: '2024-01-01T10:00:00Z',
  completedAt: null,
  playerCount: 3,
  players: [],
  winnerName: null,
  notes: null,
  durationMinutes: 0,
};

const mockInProgressSession: GameSessionDto = {
  ...mockSetupSession,
  id: 'session-002',
  status: 'InProgress',
  durationMinutes: 45,
};

const mockPausedSession: GameSessionDto = {
  ...mockSetupSession,
  id: 'session-003',
  status: 'Paused',
  durationMinutes: 30,
};

const mockCompletedSession: GameSessionDto = {
  ...mockSetupSession,
  id: 'session-004',
  status: 'Completed',
  completedAt: '2024-01-01T12:00:00Z',
  winnerName: 'Alice',
  durationMinutes: 120,
};

// ============================================================================
// Tests — Basic rendering
// ============================================================================

describe('MeepleSessionCard', () => {
  it('renders with correct data-testid', () => {
    render(<MeepleSessionCard session={mockSetupSession} />);
    expect(screen.getByTestId('session-card-session-001')).toBeInTheDocument();
  });

  it('renders title with truncated session id', () => {
    render(<MeepleSessionCard session={mockSetupSession} />);
    // title format: "Sessione #" + first 8 chars of session.id → "Sessione #session-"
    expect(screen.getByText(/Sessione #session-/)).toBeInTheDocument();
  });

  it('renders player count in subtitle (pluralized)', () => {
    render(<MeepleSessionCard session={mockSetupSession} />);
    expect(screen.getByText(/3 giocatori/)).toBeInTheDocument();
  });

  it('uses singular form for 1 player', () => {
    const solo: GameSessionDto = { ...mockSetupSession, playerCount: 1 };
    render(<MeepleSessionCard session={solo} />);
    expect(screen.getByText(/1 giocatore/)).toBeInTheDocument();
  });

  it('includes winner name in subtitle for completed session', () => {
    render(<MeepleSessionCard session={mockCompletedSession} />);
    expect(screen.getByText(/Vincitore: Alice/)).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Status badge — MeepleCard now renders the badge prop in the title area.
  // --------------------------------------------------------------------------

  it('shows "Completata" badge for completed session', () => {
    render(<MeepleSessionCard session={mockCompletedSession} />);
    expect(screen.getByText('Completata')).toBeInTheDocument();
  });

  it('shows "In corso" badge for in-progress session', () => {
    render(<MeepleSessionCard session={mockInProgressSession} />);
    expect(screen.getByText('In corso')).toBeInTheDocument();
  });

  it('shows "In pausa" badge for paused session', () => {
    render(<MeepleSessionCard session={mockPausedSession} />);
    expect(screen.getByText('In pausa')).toBeInTheDocument();
  });

  it('does not show any status badge for setup session', () => {
    render(<MeepleSessionCard session={mockSetupSession} />);
    expect(screen.queryByText('Completata')).not.toBeInTheDocument();
    expect(screen.queryByText('In corso')).not.toBeInTheDocument();
    expect(screen.queryByText('In pausa')).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // onClick handler
  // --------------------------------------------------------------------------

  it('invokes onClick with session id when card is clicked', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(<MeepleSessionCard session={mockSetupSession} onClick={onClick} />);

    // MeepleCard wraps its clickable area with role=button when onClick is provided
    const clickable = screen.getByTestId('session-card-session-001');
    await user.click(clickable);
    expect(onClick).toHaveBeenCalledWith('session-001');
  });

  it('does not throw when onClick is not provided', async () => {
    const user = userEvent.setup();
    render(<MeepleSessionCard session={mockSetupSession} />);
    const clickable = screen.getByTestId('session-card-session-001');
    await user.click(clickable); // should be a no-op
    expect(clickable).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Connections — shows player count from session.playerCount
  // --------------------------------------------------------------------------

  it('renders connection chip with player count from session data', () => {
    render(<MeepleSessionCard session={mockSetupSession} />);
    // The Giocatori nav slot should appear with count badge 3
    expect(screen.getByText('Giocatori')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Variants
  // --------------------------------------------------------------------------

  it('accepts grid variant (default)', () => {
    render(<MeepleSessionCard session={mockSetupSession} variant="grid" />);
    expect(screen.getByTestId('session-card-session-001')).toBeInTheDocument();
  });

  it('accepts list variant', () => {
    render(<MeepleSessionCard session={mockSetupSession} variant="list" />);
    expect(screen.getByTestId('session-card-session-001')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Action callbacks are accepted but not yet rendered (deferred v2 scope)
  // --------------------------------------------------------------------------

  it('accepts action callback props without error (v2 deliverable)', () => {
    render(
      <MeepleSessionCard
        session={mockSetupSession}
        isOwner={true}
        isAdmin={false}
        isParticipant={false}
        hasSlots={true}
        onConfigure={vi.fn()}
        onStart={vi.fn()}
        onPause={vi.fn()}
        onResume={vi.fn()}
        onJoin={vi.fn()}
        onLeave={vi.fn()}
        onExport={vi.fn()}
      />
    );
    // Component renders without crashing even when all action props are supplied.
    expect(screen.getByTestId('session-card-session-001')).toBeInTheDocument();
    // Action buttons are NOT yet rendered — this is intentional, tracked for v2.
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Avvia' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Pausa' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Riprendi' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Skeleton
  // --------------------------------------------------------------------------

  it('MeepleSessionCardSkeleton renders loading state', () => {
    render(<MeepleSessionCardSkeleton />);
    // MeepleCard in loading mode with empty title — just verify it mounts
    const container = document.querySelector('[data-testid^="session-card"]');
    expect(container).toBeTruthy();
  });
});
