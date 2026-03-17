/**
 * Unit tests for ScoreTrackerWidget.
 * Issue #5156 — Epic B13.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

vi.mock('@/lib/domain-hooks/useWidgetSync', () => ({
  useWidgetSync: () => ({ broadcastState: vi.fn(), isConnected: false }),
}));

import { ScoreTrackerWidget } from '../ScoreTrackerWidget';

describe('ScoreTrackerWidget', () => {
  const defaultPlayers = [
    { id: '1', name: 'Alice' },
    { id: '2', name: 'Bob' },
  ];

  it('renders player entries', () => {
    render(<ScoreTrackerWidget isEnabled={true} players={defaultPlayers} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('starts with zero total for each player', () => {
    render(<ScoreTrackerWidget isEnabled={true} players={defaultPlayers} />);
    const zeros = screen.getAllByText('0');
    expect(zeros.length).toBeGreaterThanOrEqual(2);
  });

  it('adds score to a player', () => {
    const onStateChange = vi.fn();
    render(
      <ScoreTrackerWidget isEnabled={true} players={defaultPlayers} onStateChange={onStateChange} />
    );
    const aliceInput = screen.getByLabelText('Score for Alice');
    fireEvent.change(aliceInput, { target: { value: '10' } });
    fireEvent.click(screen.getByLabelText('Add score for Alice'));
    expect(screen.getByText('10')).toBeInTheDocument();
    expect(onStateChange).toHaveBeenCalledTimes(1);
  });

  it('adds score via Enter key', () => {
    render(<ScoreTrackerWidget isEnabled={true} players={defaultPlayers} />);
    const aliceInput = screen.getByLabelText('Score for Alice');
    fireEvent.change(aliceInput, { target: { value: '5' } });
    fireEvent.keyDown(aliceInput, { key: 'Enter' });
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('adds a new player', () => {
    render(<ScoreTrackerWidget isEnabled={true} players={defaultPlayers} />);
    const input = screen.getByLabelText('New player name');
    fireEvent.change(input, { target: { value: 'Charlie' } });
    fireEvent.click(screen.getByLabelText('Add player to score tracker'));
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });

  it('does not add score if input is empty', () => {
    const onStateChange = vi.fn();
    render(
      <ScoreTrackerWidget isEnabled={true} players={defaultPlayers} onStateChange={onStateChange} />
    );
    fireEvent.click(screen.getByLabelText('Add score for Alice'));
    expect(onStateChange).not.toHaveBeenCalled();
  });
});
