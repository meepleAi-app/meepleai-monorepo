/**
 * Unit tests for TurnManagerWidget.
 * Issue #5156 — Epic B13.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

const mockBroadcastState = vi.fn();
vi.mock('@/lib/hooks/useWidgetSync', () => ({
  useWidgetSync: () => ({ broadcastState: mockBroadcastState, isConnected: false }),
}));

import { TurnManagerWidget } from '../TurnManagerWidget';

describe('TurnManagerWidget', () => {
  const defaultPlayers = [
    { id: '1', name: 'Alice' },
    { id: '2', name: 'Bob' },
  ];

  it('renders player list', () => {
    render(<TurnManagerWidget isEnabled={true} players={defaultPlayers} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('highlights first player as current turn', () => {
    render(<TurnManagerWidget isEnabled={true} players={defaultPlayers} />);
    const aliceRow = screen.getByTestId('player-row-0');
    expect(aliceRow).toHaveClass('bg-primary');
  });

  it('advances turn on Next Turn click', () => {
    render(<TurnManagerWidget isEnabled={true} players={defaultPlayers} />);
    fireEvent.click(screen.getByRole('button', { name: /next turn/i }));
    expect(screen.getByTestId('player-row-1')).toHaveClass('bg-primary');
    expect(screen.getByTestId('player-row-0')).not.toHaveClass('bg-primary');
  });

  it('increments round when cycling back to first player', () => {
    render(<TurnManagerWidget isEnabled={true} players={defaultPlayers} />);
    expect(screen.getByText('Round 1')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /next turn/i })); // Bob
    fireEvent.click(screen.getByRole('button', { name: /next turn/i })); // back to Alice → Round 2
    expect(screen.getByText('Round 2')).toBeInTheDocument();
  });

  it('resets to round 1 and first player', () => {
    render(<TurnManagerWidget isEnabled={true} players={defaultPlayers} />);
    fireEvent.click(screen.getByRole('button', { name: /next turn/i }));
    fireEvent.click(screen.getByRole('button', { name: /reset turns/i }));
    expect(screen.getByTestId('player-row-0')).toHaveClass('bg-primary');
    expect(screen.getByText('Round 1')).toBeInTheDocument();
  });

  it('adds a new player', () => {
    render(<TurnManagerWidget isEnabled={true} players={defaultPlayers} />);
    const input = screen.getByPlaceholderText(/add player/i);
    fireEvent.change(input, { target: { value: 'Charlie' } });
    fireEvent.click(screen.getByRole('button', { name: /add player/i }));
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });

  it('calls onStateChange when advancing turns', () => {
    const onStateChange = vi.fn();
    render(
      <TurnManagerWidget isEnabled={true} players={defaultPlayers} onStateChange={onStateChange} />
    );
    fireEvent.click(screen.getByRole('button', { name: /next turn/i }));
    expect(onStateChange).toHaveBeenCalledTimes(1);
    expect(onStateChange).toHaveBeenCalledWith(expect.stringContaining('currentIndex'));
  });
});
