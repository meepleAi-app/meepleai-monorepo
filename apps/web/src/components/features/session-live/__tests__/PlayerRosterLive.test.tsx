/**
 * Tests for PlayerRosterLive — onAddPlayer (#2505 addition).
 *
 * Covers:
 *   - "Add player" button NOT rendered when onAddPlayer is undefined
 *   - "Add player" button IS rendered when onAddPlayer is provided
 *   - clicking the button calls onAddPlayer
 *   - button still absent in compact mode even if onAddPlayer is provided
 */

import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { PlayerRosterLive } from '../PlayerRosterLive';
import type { PlayerRosterLiveProps } from '../PlayerRosterLive';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: PlayerRosterLiveProps['labels'] = {
  title: 'Giocatori',
  playerCountResolved: '2 giocatori',
  onlineLabel: 'Online',
  offlineLabel: 'Offline',
  kickAriaLabelTemplate: 'Espelli {playerName}',
  roleSpectator: 'Spettatore',
  rolePlayer: 'Giocatore',
  roleHost: 'Host',
  addPlayerLabel: '+ Aggiungi giocatore',
};

const BASE_PLAYERS: PlayerRosterLiveProps['players'] = [
  { id: 'p1', name: 'Alice', role: 'Host', score: 10, isOnline: true },
  { id: 'p2', name: 'Bob', role: 'Player', score: 5, isOnline: false },
];

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('PlayerRosterLive — onAddPlayer (#2505)', () => {
  it('does NOT render add button when onAddPlayer is undefined', () => {
    render(
      <PlayerRosterLive players={BASE_PLAYERS} viewerId="p1" viewerRole="Host" labels={LABELS} />
    );
    expect(screen.queryByTestId('add-player-roster-button')).toBeNull();
    // Also check the data-slot is absent
    expect(document.querySelector('[data-slot="player-roster-add"]')).toBeNull();
  });

  it('renders add button when onAddPlayer is provided', () => {
    const onAddPlayer = vi.fn();
    render(
      <PlayerRosterLive
        players={BASE_PLAYERS}
        viewerId="p1"
        viewerRole="Host"
        onAddPlayer={onAddPlayer}
        labels={LABELS}
      />
    );
    expect(document.querySelector('[data-slot="player-roster-add"]')).toBeInTheDocument();
    expect(screen.getByText('+ Aggiungi giocatore')).toBeInTheDocument();
  });

  it('calls onAddPlayer when the button is clicked', () => {
    const onAddPlayer = vi.fn();
    render(
      <PlayerRosterLive
        players={BASE_PLAYERS}
        viewerId="p1"
        viewerRole="Host"
        onAddPlayer={onAddPlayer}
        labels={LABELS}
      />
    );
    fireEvent.click(screen.getByText('+ Aggiungi giocatore'));
    expect(onAddPlayer).toHaveBeenCalledOnce();
  });

  it('does NOT render add button in compact mode even when onAddPlayer is provided', () => {
    render(
      <PlayerRosterLive
        players={BASE_PLAYERS}
        viewerId="p1"
        viewerRole="Host"
        onAddPlayer={vi.fn()}
        compact
        labels={LABELS}
      />
    );
    expect(document.querySelector('[data-slot="player-roster-add"]')).toBeNull();
  });
});
