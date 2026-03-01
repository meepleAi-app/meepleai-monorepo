/**
 * MeepleSessionCard Tests
 * Issue #5003 — Session Card: azioni contestuali per stato e ruolo
 *
 * Tests the action visibility matrix:
 * - Configura: owner/admin, disabled if completed
 * - Avvia: owner + status = setup
 * - Pausa: owner + status = inProgress
 * - Riprendi: owner + status = paused
 * - Partecipa: !participant + status ≠ completed, disabled if !hasSlots
 * - Lascia: participant + !owner
 * - Esporta: status = completed
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
// Tests
// ============================================================================

describe('MeepleSessionCard', () => {
  // --------------------------------------------------------------------------
  // Basic rendering
  // --------------------------------------------------------------------------

  it('renders with correct data-testid', () => {
    render(<MeepleSessionCard session={mockSetupSession} />);
    expect(screen.getByTestId('session-card-session-001')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // AC: Configura — owner/admin, disabled if completed
  // --------------------------------------------------------------------------

  it('Configura not shown for non-owner non-admin user', () => {
    render(<MeepleSessionCard session={mockSetupSession} isOwner={false} isAdmin={false} />);
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'La sessione è completata' })).not.toBeInTheDocument();
  });

  it('Configura shown and enabled for owner when not completed', () => {
    render(<MeepleSessionCard session={mockSetupSession} isOwner={true} />);
    const button = screen.getByRole('button', { name: 'Configura' });
    expect(button).toBeInTheDocument();
    expect(button).not.toBeDisabled();
  });

  it('Configura shown and enabled for admin when not completed', () => {
    render(<MeepleSessionCard session={mockInProgressSession} isOwner={false} isAdmin={true} />);
    const button = screen.getByRole('button', { name: 'Configura' });
    expect(button).toBeInTheDocument();
    expect(button).not.toBeDisabled();
  });

  it('Configura shown but disabled for owner when session is completed', () => {
    render(<MeepleSessionCard session={mockCompletedSession} isOwner={true} />);
    const button = screen.getByRole('button', { name: 'La sessione è completata' });
    expect(button).toBeInTheDocument();
    // MeepleCardQuickActions uses aria-disabled for disabled buttons (to keep Radix Tooltip working)
    expect(button).toHaveAttribute('aria-disabled', 'true');
  });

  it('Configura calls onConfigure with session id when clicked', async () => {
    const user = userEvent.setup();
    const onConfigure = vi.fn();

    render(
      <MeepleSessionCard
        session={mockSetupSession}
        isOwner={true}
        onConfigure={onConfigure}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Configura' }));
    expect(onConfigure).toHaveBeenCalledWith('session-001');
  });

  // --------------------------------------------------------------------------
  // AC: Avvia — owner + setup
  // --------------------------------------------------------------------------

  it('Avvia not shown for non-owner', () => {
    render(<MeepleSessionCard session={mockSetupSession} isOwner={false} />);
    expect(screen.queryByRole('button', { name: 'Avvia' })).not.toBeInTheDocument();
  });

  it('Avvia not shown for owner when not in setup state', () => {
    render(<MeepleSessionCard session={mockInProgressSession} isOwner={true} />);
    expect(screen.queryByRole('button', { name: 'Avvia' })).not.toBeInTheDocument();
  });

  it('Avvia shown for owner when in setup state', () => {
    render(<MeepleSessionCard session={mockSetupSession} isOwner={true} />);
    expect(screen.getByRole('button', { name: 'Avvia' })).toBeInTheDocument();
  });

  it('Avvia calls onStart with session id when clicked', async () => {
    const user = userEvent.setup();
    const onStart = vi.fn();

    render(
      <MeepleSessionCard
        session={mockSetupSession}
        isOwner={true}
        onStart={onStart}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Avvia' }));
    expect(onStart).toHaveBeenCalledWith('session-001');
  });

  // --------------------------------------------------------------------------
  // AC: Pausa — owner + inProgress
  // --------------------------------------------------------------------------

  it('Pausa not shown for non-owner', () => {
    render(<MeepleSessionCard session={mockInProgressSession} isOwner={false} />);
    expect(screen.queryByRole('button', { name: 'Pausa' })).not.toBeInTheDocument();
  });

  it('Pausa not shown for owner when not in progress', () => {
    render(<MeepleSessionCard session={mockSetupSession} isOwner={true} />);
    expect(screen.queryByRole('button', { name: 'Pausa' })).not.toBeInTheDocument();
  });

  it('Pausa shown for owner when in progress', () => {
    render(<MeepleSessionCard session={mockInProgressSession} isOwner={true} />);
    expect(screen.getByRole('button', { name: 'Pausa' })).toBeInTheDocument();
  });

  it('Pausa calls onPause with session id when clicked', async () => {
    const user = userEvent.setup();
    const onPause = vi.fn();

    render(
      <MeepleSessionCard
        session={mockInProgressSession}
        isOwner={true}
        onPause={onPause}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Pausa' }));
    expect(onPause).toHaveBeenCalledWith('session-002');
  });

  // --------------------------------------------------------------------------
  // AC: Riprendi — owner + paused
  // --------------------------------------------------------------------------

  it('Riprendi not shown for non-owner', () => {
    render(<MeepleSessionCard session={mockPausedSession} isOwner={false} />);
    expect(screen.queryByRole('button', { name: 'Riprendi' })).not.toBeInTheDocument();
  });

  it('Riprendi not shown for owner when not paused', () => {
    render(<MeepleSessionCard session={mockInProgressSession} isOwner={true} />);
    expect(screen.queryByRole('button', { name: 'Riprendi' })).not.toBeInTheDocument();
  });

  it('Riprendi shown for owner when paused', () => {
    render(<MeepleSessionCard session={mockPausedSession} isOwner={true} />);
    expect(screen.getByRole('button', { name: 'Riprendi' })).toBeInTheDocument();
  });

  it('Riprendi calls onResume with session id when clicked', async () => {
    const user = userEvent.setup();
    const onResume = vi.fn();

    render(
      <MeepleSessionCard
        session={mockPausedSession}
        isOwner={true}
        onResume={onResume}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Riprendi' }));
    expect(onResume).toHaveBeenCalledWith('session-003');
  });

  // --------------------------------------------------------------------------
  // AC: Partecipa — !participant + !completed, disabled if !hasSlots
  // --------------------------------------------------------------------------

  it('Partecipa not shown for existing participant', () => {
    render(<MeepleSessionCard session={mockSetupSession} isParticipant={true} />);
    expect(screen.queryByRole('button', { name: 'Partecipa' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Sessione piena' })).not.toBeInTheDocument();
  });

  it('Partecipa not shown when session is completed', () => {
    render(<MeepleSessionCard session={mockCompletedSession} isParticipant={false} />);
    expect(screen.queryByRole('button', { name: 'Partecipa' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Sessione piena' })).not.toBeInTheDocument();
  });

  it('Partecipa shown and enabled for non-participant with available slots', () => {
    render(
      <MeepleSessionCard
        session={mockSetupSession}
        isParticipant={false}
        hasSlots={true}
      />,
    );
    const button = screen.getByRole('button', { name: 'Partecipa' });
    expect(button).toBeInTheDocument();
    expect(button).not.toBeDisabled();
  });

  it('Partecipa shown but disabled for non-participant when no slots', () => {
    render(
      <MeepleSessionCard
        session={mockSetupSession}
        isParticipant={false}
        hasSlots={false}
      />,
    );
    const button = screen.getByRole('button', { name: 'Sessione piena' });
    expect(button).toBeInTheDocument();
    // MeepleCardQuickActions uses aria-disabled for disabled buttons (to keep Radix Tooltip working)
    expect(button).toHaveAttribute('aria-disabled', 'true');
  });

  it('Partecipa calls onJoin with session id when clicked', async () => {
    const user = userEvent.setup();
    const onJoin = vi.fn();

    render(
      <MeepleSessionCard
        session={mockInProgressSession}
        isParticipant={false}
        hasSlots={true}
        onJoin={onJoin}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Partecipa' }));
    expect(onJoin).toHaveBeenCalledWith('session-002');
  });

  // --------------------------------------------------------------------------
  // AC: Lascia — participant + !owner
  // --------------------------------------------------------------------------

  it('Lascia not shown for non-participant', () => {
    render(<MeepleSessionCard session={mockInProgressSession} isParticipant={false} />);
    expect(screen.queryByRole('button', { name: 'Lascia' })).not.toBeInTheDocument();
  });

  it('Lascia not shown for owner (even if participant)', () => {
    render(
      <MeepleSessionCard
        session={mockInProgressSession}
        isParticipant={true}
        isOwner={true}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Lascia' })).not.toBeInTheDocument();
  });

  it('Lascia shown for participant who is not the owner', () => {
    render(
      <MeepleSessionCard
        session={mockInProgressSession}
        isParticipant={true}
        isOwner={false}
      />,
    );
    expect(screen.getByRole('button', { name: 'Lascia' })).toBeInTheDocument();
  });

  it('Lascia calls onLeave with session id when clicked', async () => {
    const user = userEvent.setup();
    const onLeave = vi.fn();

    render(
      <MeepleSessionCard
        session={mockPausedSession}
        isParticipant={true}
        isOwner={false}
        onLeave={onLeave}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Lascia' }));
    expect(onLeave).toHaveBeenCalledWith('session-003');
  });

  // --------------------------------------------------------------------------
  // AC: Esporta — only when completed
  // --------------------------------------------------------------------------

  it('Esporta not shown for non-completed sessions', () => {
    render(<MeepleSessionCard session={mockSetupSession} />);
    expect(screen.queryByRole('button', { name: 'Esporta' })).not.toBeInTheDocument();
  });

  it('Esporta not shown for in-progress session', () => {
    render(<MeepleSessionCard session={mockInProgressSession} />);
    expect(screen.queryByRole('button', { name: 'Esporta' })).not.toBeInTheDocument();
  });

  it('Esporta shown when session is completed', () => {
    render(<MeepleSessionCard session={mockCompletedSession} />);
    expect(screen.getByRole('button', { name: 'Esporta' })).toBeInTheDocument();
  });

  it('Esporta calls onExport with session id when clicked', async () => {
    const user = userEvent.setup();
    const onExport = vi.fn();

    render(
      <MeepleSessionCard
        session={mockCompletedSession}
        onExport={onExport}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Esporta' }));
    expect(onExport).toHaveBeenCalledWith('session-004');
  });

  // --------------------------------------------------------------------------
  // AC: No Rivincita action
  // --------------------------------------------------------------------------

  it('does not render Rivincita action', () => {
    render(
      <MeepleSessionCard
        session={mockCompletedSession}
        isOwner={true}
        isAdmin={true}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Rivincita' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Unauthenticated / no-role user
  // --------------------------------------------------------------------------

  it('unauthenticated user sees no owner/admin/participant actions for active session', () => {
    render(<MeepleSessionCard session={mockInProgressSession} />);
    // No owner actions
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Pausa' })).not.toBeInTheDocument();
    // No participant-only actions
    expect(screen.queryByRole('button', { name: 'Lascia' })).not.toBeInTheDocument();
    // Partecipa IS shown for unauthenticated (since isParticipant defaults to false and status != completed)
    expect(screen.getByRole('button', { name: 'Partecipa' })).toBeInTheDocument();
  });

  it('unauthenticated user sees only Esporta for completed session', () => {
    render(<MeepleSessionCard session={mockCompletedSession} />);
    expect(screen.getByRole('button', { name: 'Esporta' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Partecipa' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Lascia' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Skeleton
  // --------------------------------------------------------------------------

  it('renders skeleton with correct testid', () => {
    render(<MeepleSessionCardSkeleton />);
    // MeepleCard in loading mode hardcodes data-testid="meeple-card-skeleton"
    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
  });
});
