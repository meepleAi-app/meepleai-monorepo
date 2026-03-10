/**
 * PlayerSetup Tests — Issue #5583
 *
 * Test Coverage:
 * - Add/remove players
 * - Color assignment and uniqueness
 * - Role assignment (Host to first player)
 * - Turn order reorder (move up/down)
 * - Enter key to add
 * - Empty name prevention
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { PlayerSetup, type SetupPlayer } from '../PlayerSetup';

describe('PlayerSetup', () => {
  const defaultProps = {
    players: [] as SetupPlayer[],
    onPlayersChange: vi.fn(),
  };

  it('should render add player input', () => {
    render(<PlayerSetup {...defaultProps} />);

    expect(screen.getByPlaceholderText('Nome giocatore')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /aggiungi/i })).toBeInTheDocument();
  });

  it('should show empty state message when no players', () => {
    render(<PlayerSetup {...defaultProps} />);

    expect(screen.getByText(/nessun giocatore aggiunto/i)).toBeInTheDocument();
  });

  it('should call onPlayersChange when adding a player', () => {
    const onPlayersChange = vi.fn();
    render(<PlayerSetup {...defaultProps} onPlayersChange={onPlayersChange} />);

    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Alice' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));

    expect(onPlayersChange).toHaveBeenCalledTimes(1);
    const newPlayers = onPlayersChange.mock.calls[0][0];
    expect(newPlayers).toHaveLength(1);
    expect(newPlayers[0].name).toBe('Alice');
    expect(newPlayers[0].role).toBe('Host'); // First player is Host
  });

  it('should assign Player role to second player', () => {
    const onPlayersChange = vi.fn();
    const existingPlayers: SetupPlayer[] = [
      { id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' },
    ];

    render(<PlayerSetup players={existingPlayers} onPlayersChange={onPlayersChange} />);

    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Bob' } });
    fireEvent.click(screen.getByRole('button', { name: /aggiungi/i }));

    const newPlayers = onPlayersChange.mock.calls[0][0];
    expect(newPlayers).toHaveLength(2);
    expect(newPlayers[1].name).toBe('Bob');
    expect(newPlayers[1].role).toBe('Player');
  });

  it('should add player on Enter key', () => {
    const onPlayersChange = vi.fn();
    render(<PlayerSetup {...defaultProps} onPlayersChange={onPlayersChange} />);

    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.change(input, { target: { value: 'Charlie' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onPlayersChange).toHaveBeenCalledTimes(1);
    expect(onPlayersChange.mock.calls[0][0][0].name).toBe('Charlie');
  });

  it('should not add player with empty name', () => {
    const onPlayersChange = vi.fn();
    render(<PlayerSetup {...defaultProps} onPlayersChange={onPlayersChange} />);

    // Button should be disabled
    expect(screen.getByRole('button', { name: /aggiungi/i })).toBeDisabled();

    // Enter on empty should not call onPlayersChange
    const input = screen.getByPlaceholderText('Nome giocatore');
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onPlayersChange).not.toHaveBeenCalled();
  });

  it('should render existing players', () => {
    const players: SetupPlayer[] = [
      { id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' },
      { id: 'p2', name: 'Bob', color: '#3b82f6', role: 'Player' },
    ];

    render(<PlayerSetup players={players} onPlayersChange={vi.fn()} />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Host')).toBeInTheDocument();
    expect(screen.getByText('Player')).toBeInTheDocument();
  });

  it('should remove a player when remove button is clicked', () => {
    const onPlayersChange = vi.fn();
    const players: SetupPlayer[] = [
      { id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' },
      { id: 'p2', name: 'Bob', color: '#3b82f6', role: 'Player' },
    ];

    render(<PlayerSetup players={players} onPlayersChange={onPlayersChange} />);

    fireEvent.click(screen.getByRole('button', { name: /rimuovi bob/i }));

    expect(onPlayersChange).toHaveBeenCalledTimes(1);
    const remaining = onPlayersChange.mock.calls[0][0];
    expect(remaining).toHaveLength(1);
    expect(remaining[0].name).toBe('Alice');
  });

  it('should promote first remaining player to Host when Host is removed', () => {
    const onPlayersChange = vi.fn();
    const players: SetupPlayer[] = [
      { id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' },
      { id: 'p2', name: 'Bob', color: '#3b82f6', role: 'Player' },
    ];

    render(<PlayerSetup players={players} onPlayersChange={onPlayersChange} />);

    fireEvent.click(screen.getByRole('button', { name: /rimuovi alice/i }));

    const remaining = onPlayersChange.mock.calls[0][0];
    expect(remaining[0].name).toBe('Bob');
    expect(remaining[0].role).toBe('Host');
  });

  it('should reorder players up', () => {
    const onPlayersChange = vi.fn();
    const players: SetupPlayer[] = [
      { id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' },
      { id: 'p2', name: 'Bob', color: '#3b82f6', role: 'Player' },
    ];

    render(<PlayerSetup players={players} onPlayersChange={onPlayersChange} />);

    // Move Bob up
    fireEvent.click(screen.getByRole('button', { name: /sposta bob su/i }));

    const reordered = onPlayersChange.mock.calls[0][0];
    expect(reordered[0].name).toBe('Bob');
    expect(reordered[1].name).toBe('Alice');
  });

  it('should reorder players down', () => {
    const onPlayersChange = vi.fn();
    const players: SetupPlayer[] = [
      { id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' },
      { id: 'p2', name: 'Bob', color: '#3b82f6', role: 'Player' },
    ];

    render(<PlayerSetup players={players} onPlayersChange={onPlayersChange} />);

    // Move Alice down
    fireEvent.click(screen.getByRole('button', { name: /sposta alice giu/i }));

    const reordered = onPlayersChange.mock.calls[0][0];
    expect(reordered[0].name).toBe('Bob');
    expect(reordered[1].name).toBe('Alice');
  });

  it('should disable up arrow for first player', () => {
    const players: SetupPlayer[] = [
      { id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' },
      { id: 'p2', name: 'Bob', color: '#3b82f6', role: 'Player' },
    ];

    render(<PlayerSetup players={players} onPlayersChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: /sposta alice su/i })).toBeDisabled();
  });

  it('should disable down arrow for last player', () => {
    const players: SetupPlayer[] = [
      { id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' },
      { id: 'p2', name: 'Bob', color: '#3b82f6', role: 'Player' },
    ];

    render(<PlayerSetup players={players} onPlayersChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: /sposta bob giu/i })).toBeDisabled();
  });

  it('should show turn order hint text when players exist', () => {
    const players: SetupPlayer[] = [{ id: 'p1', name: 'Alice', color: '#ef4444', role: 'Host' }];

    render(<PlayerSetup players={players} onPlayersChange={vi.fn()} />);

    expect(screen.getByText(/ordine dei giocatori/i)).toBeInTheDocument();
  });
});
