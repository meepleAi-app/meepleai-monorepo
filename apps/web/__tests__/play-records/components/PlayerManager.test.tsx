/**
 * PlayerManager Component Tests
 *
 * Tests player addition, removal, and validation.
 * Issue #3892: Play Records Frontend UI
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PlayerManager } from '@/components/play-records/PlayerManager';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';

const mockPlayers: SessionPlayer[] = [
  {
    id: '1',
    userId: 'user-1',
    displayName: 'Alice',
    scores: [],
  },
  {
    id: '2',
    userId: null,
    displayName: 'Bob (Guest)',
    scores: [],
  },
];

describe('PlayerManager', () => {
  const mockOnAddPlayer = vi.fn();
  const mockOnRemovePlayer = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders player list', () => {
    render(
      <PlayerManager
        players={mockPlayers}
        onAddPlayer={mockOnAddPlayer}
        onRemovePlayer={mockOnRemovePlayer}
      />
    );

    expect(screen.getByText('Players (2/20)')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob (Guest)')).toBeInTheDocument();
  });

  it('shows registered user badge', () => {
    render(
      <PlayerManager
        players={mockPlayers}
        onAddPlayer={mockOnAddPlayer}
      />
    );

    const registeredBadges = screen.getAllByText('Registered');
    expect(registeredBadges).toHaveLength(1);
  });

  it('shows guest badge for non-registered players', () => {
    render(
      <PlayerManager
        players={mockPlayers}
        onAddPlayer={mockOnAddPlayer}
      />
    );

    const guestBadges = screen.getAllByText('Guest');
    expect(guestBadges).toHaveLength(1);
  });

  it('shows empty state when no players', () => {
    render(
      <PlayerManager
        players={[]}
        onAddPlayer={mockOnAddPlayer}
      />
    );

    expect(screen.getByText('No players added yet')).toBeInTheDocument();
    expect(screen.getByText(/Click "Add Player" to start/)).toBeInTheDocument();
  });

  it('opens add player form when clicking Add Player button', async () => {
    render(
      <PlayerManager
        players={[]}
        onAddPlayer={mockOnAddPlayer}
      />
    );

    const addButton = screen.getByRole('button', { name: /Add Player/ });
    fireEvent.click(addButton);

    await waitFor(() => {
      // RadioGroup doesn't have labelable element, check for text instead
      expect(screen.getByText('Player Type')).toBeInTheDocument();
    });
  });

  it('allows adding guest player', async () => {
    render(
      <PlayerManager
        players={[]}
        onAddPlayer={mockOnAddPlayer}
      />
    );

    // Open form
    fireEvent.click(screen.getByRole('button', { name: /Add Player/ }));

    // Select guest
    const guestRadio = screen.getByLabelText('Guest');
    fireEvent.click(guestRadio);

    // Enter name
    const nameInput = screen.getByPlaceholderText('Enter player name...');
    fireEvent.change(nameInput, { target: { value: 'Charlie' } });

    // Submit
    const submitButton = screen.getByRole('button', { name: 'Add Player' });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(mockOnAddPlayer).toHaveBeenCalledWith({
        displayName: 'Charlie',
      });
    });
  });

  it('calls onRemovePlayer when remove button clicked', () => {
    render(
      <PlayerManager
        players={mockPlayers}
        onAddPlayer={mockOnAddPlayer}
        onRemovePlayer={mockOnRemovePlayer}
      />
    );

    const removeButtons = screen.getAllByRole('button', { name: '' }); // X buttons
    fireEvent.click(removeButtons[0]);

    expect(mockOnRemovePlayer).toHaveBeenCalledWith('1');
  });

  it('respects max players limit', () => {
    const maxedPlayers = Array.from({ length: 20 }, (_, i) => ({
      id: `${i}`,
      userId: null,
      displayName: `Player ${i}`,
      scores: [],
    }));

    render(
      <PlayerManager
        players={maxedPlayers}
        onAddPlayer={mockOnAddPlayer}
        maxPlayers={20}
      />
    );

    expect(screen.getByText('Players (20/20)')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Add Player/ })).not.toBeInTheDocument();
  });
});
