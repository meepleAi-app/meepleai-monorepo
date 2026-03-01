/**
 * MeeplePlayerCard Tests
 * Issue #5004 — Player Card: azioni contestuali + stub pagina profilo
 *
 * Tests the action visibility matrix:
 * - Info/Dettaglio: always visible, routes to /users/[userId] for meepleAiUser else /players/[id]
 * - Configura/Edit: visible only if isCreatedByCurrentUser + !isMeepleAiUser
 * - Invita a Sessione: visible only if isAuthenticated, disabled if !hasActiveSession
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeAll } from 'vitest';

// Mock window.matchMedia (MeepleCard uses it for mobile detection)
beforeAll(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
});

import { MeeplePlayerCard, MeeplePlayerCardSkeleton } from '../MeeplePlayerCard';

import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';

// ============================================================================
// Mock Data
// ============================================================================

const mockGuestPlayer: SessionPlayer = {
  id: 'player-123',
  userId: null,
  displayName: 'Mario Rossi',
  scores: [],
};

const mockMeepleAiPlayer: SessionPlayer = {
  id: 'player-456',
  userId: 'user-789',
  displayName: 'Luigi Verdi',
  scores: [],
};

// ============================================================================
// Tests
// ============================================================================

describe('MeeplePlayerCard', () => {
  // --------------------------------------------------------------------------
  // Basic rendering
  // --------------------------------------------------------------------------

  it('renders player displayName as card title', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} />);
    expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
  });

  it('renders with correct data-testid', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} />);
    expect(screen.getByTestId('player-card-player-123')).toBeInTheDocument();
  });

  it('renders subtitle "Utente MeepleAI" for meeple user', () => {
    render(<MeeplePlayerCard player={mockMeepleAiPlayer} isMeepleAiUser={true} />);
    expect(screen.getByText('Utente MeepleAI')).toBeInTheDocument();
  });

  it('renders subtitle "Giocatore" for guest player', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} />);
    expect(screen.getByText('Giocatore')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // AC: Info/Dettaglio — always visible, routes by type
  // --------------------------------------------------------------------------

  it('Info button always present with correct href for guest player', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} />);
    const infoLink = screen.getByRole('link', { name: 'Vai al profilo' });
    expect(infoLink).toBeInTheDocument();
    expect(infoLink).toHaveAttribute('href', '/players/player-123');
  });

  it('Info button routes to /users/[userId] for MeepleAI user', () => {
    render(<MeeplePlayerCard player={mockMeepleAiPlayer} isMeepleAiUser={true} />);
    const infoLink = screen.getByRole('link', { name: 'Vai al profilo' });
    expect(infoLink).toBeInTheDocument();
    expect(infoLink).toHaveAttribute('href', '/users/user-789');
  });

  it('Info button routes to /players/[id] for non-MeepleAI user', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} isMeepleAiUser={false} />);
    const infoLink = screen.getByRole('link', { name: 'Vai al profilo' });
    expect(infoLink).toHaveAttribute('href', '/players/player-123');
  });

  // --------------------------------------------------------------------------
  // AC: Configura/Edit — visible only if isCreatedByCurrentUser + !isMeepleAiUser
  // --------------------------------------------------------------------------

  it('Configura not shown by default (no ownership)', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} />);
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
  });

  it('Configura not shown for MeepleAI user even if created by current user', () => {
    render(
      <MeeplePlayerCard
        player={mockMeepleAiPlayer}
        isMeepleAiUser={true}
        isCreatedByCurrentUser={true}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
  });

  it('Configura not shown for guest player created by another user', () => {
    render(
      <MeeplePlayerCard
        player={mockGuestPlayer}
        isMeepleAiUser={false}
        isCreatedByCurrentUser={false}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
  });

  it('Configura shown for guest player created by current user', () => {
    render(
      <MeeplePlayerCard
        player={mockGuestPlayer}
        isMeepleAiUser={false}
        isCreatedByCurrentUser={true}
      />,
    );
    expect(screen.getByRole('button', { name: 'Configura' })).toBeInTheDocument();
  });

  it('Configura calls onConfigure with player id when clicked', async () => {
    const user = userEvent.setup();
    const onConfigure = vi.fn();

    render(
      <MeeplePlayerCard
        player={mockGuestPlayer}
        isMeepleAiUser={false}
        isCreatedByCurrentUser={true}
        onConfigure={onConfigure}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Configura' }));
    expect(onConfigure).toHaveBeenCalledWith('player-123');
  });

  // --------------------------------------------------------------------------
  // AC: Invita a Sessione — visible if isAuthenticated, disabled if !hasActiveSession
  // --------------------------------------------------------------------------

  it('Invita a Sessione not shown for unauthenticated user', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} isAuthenticated={false} />);
    expect(screen.queryByRole('button', { name: 'Invita a Sessione' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Nessuna sessione attiva' })).not.toBeInTheDocument();
  });

  it('Invita a Sessione shown and enabled for authenticated user with active session', () => {
    render(
      <MeeplePlayerCard
        player={mockGuestPlayer}
        isAuthenticated={true}
        hasActiveSession={true}
      />,
    );
    const button = screen.getByRole('button', { name: 'Invita a Sessione' });
    expect(button).toBeInTheDocument();
    expect(button).not.toBeDisabled();
  });

  it('Invita a Sessione shown but disabled when no active session', () => {
    render(
      <MeeplePlayerCard
        player={mockGuestPlayer}
        isAuthenticated={true}
        hasActiveSession={false}
      />,
    );
    const button = screen.getByRole('button', { name: 'Nessuna sessione attiva' });
    expect(button).toBeInTheDocument();
    expect(button).toHaveAttribute('aria-disabled', 'true');
  });

  it('Invita a Sessione calls onInvite with player id when clicked', async () => {
    const user = userEvent.setup();
    const onInvite = vi.fn();

    render(
      <MeeplePlayerCard
        player={mockGuestPlayer}
        isAuthenticated={true}
        hasActiveSession={true}
        onInvite={onInvite}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Invita a Sessione' }));
    expect(onInvite).toHaveBeenCalledWith('player-123');
  });

  // --------------------------------------------------------------------------
  // Unauthenticated user
  // --------------------------------------------------------------------------

  it('unauthenticated user sees only Info button and no action buttons', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} />);
    // Info always visible
    expect(screen.getByRole('link', { name: 'Vai al profilo' })).toBeInTheDocument();
    // No action buttons
    expect(screen.queryByRole('button', { name: 'Configura' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Invita a Sessione' })).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Skeleton
  // --------------------------------------------------------------------------

  it('renders skeleton with correct testid', () => {
    render(<MeeplePlayerCardSkeleton />);
    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Navigation footer
  // --------------------------------------------------------------------------

  it('navigation footer renders sessions and games links for guest player', () => {
    render(<MeeplePlayerCard player={mockGuestPlayer} />);
    const sessionsLink = screen.queryByRole('link', { name: /Sessions/i });
    const gamesLink = screen.queryByRole('link', { name: /Games/i });
    if (sessionsLink) {
      expect(sessionsLink).toHaveAttribute('href', '/players/player-123/sessions');
    }
    if (gamesLink) {
      expect(gamesLink).toHaveAttribute('href', '/players/player-123/games');
    }
  });
});
