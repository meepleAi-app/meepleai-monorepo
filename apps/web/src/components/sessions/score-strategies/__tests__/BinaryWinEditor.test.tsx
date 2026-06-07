/**
 * Tests for BinaryWinEditor.
 *
 * Asse D follow-up P1 (#1899) T2.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { BinaryWinEditor } from '../BinaryWinEditor';
import type { PlayerOption } from '../types';

const PLAYERS: PlayerOption[] = [
  { id: 'p1', displayName: 'Anna' },
  { id: 'p2', displayName: 'Bob' },
  { id: 'p3', displayName: 'Carla' },
];

describe('BinaryWinEditor', () => {
  it('defaults every player to Lose when no initialData', () => {
    const onChange = vi.fn();
    render(<BinaryWinEditor players={PLAYERS} onChange={onChange} />);
    PLAYERS.forEach(p => {
      expect(screen.getByTestId(`binary-lose-${p.id}`)).toBeChecked();
      expect(screen.getByTestId(`binary-win-${p.id}`)).not.toBeChecked();
    });
    expect(onChange).toHaveBeenLastCalledWith({
      results: PLAYERS.map(p => ({ playerId: p.id, isWinner: false })),
    });
  });

  it('uses initialData when provided', () => {
    const onChange = vi.fn();
    const initialData = {
      results: [
        { playerId: 'p1', isWinner: true },
        { playerId: 'p2', isWinner: false },
        { playerId: 'p3', isWinner: true },
      ],
    };
    render(<BinaryWinEditor players={PLAYERS} initialData={initialData} onChange={onChange} />);
    expect(screen.getByTestId('binary-win-p1')).toBeChecked();
    expect(screen.getByTestId('binary-lose-p2')).toBeChecked();
    expect(screen.getByTestId('binary-win-p3')).toBeChecked();
  });

  it('emits onChange when a player is flipped to Win', () => {
    const onChange = vi.fn();
    render(<BinaryWinEditor players={PLAYERS} onChange={onChange} />);
    fireEvent.click(screen.getByTestId('binary-win-p1'));
    expect(onChange).toHaveBeenLastCalledWith({
      results: [
        { playerId: 'p1', isWinner: true },
        { playerId: 'p2', isWinner: false },
        { playerId: 'p3', isWinner: false },
      ],
    });
  });

  it('supports multi-winner cooperative outcome', () => {
    const onChange = vi.fn();
    render(<BinaryWinEditor players={PLAYERS} onChange={onChange} />);
    fireEvent.click(screen.getByTestId('binary-win-p1'));
    fireEvent.click(screen.getByTestId('binary-win-p2'));
    fireEvent.click(screen.getByTestId('binary-win-p3'));
    expect(onChange).toHaveBeenLastCalledWith({
      results: [
        { playerId: 'p1', isWinner: true },
        { playerId: 'p2', isWinner: true },
        { playerId: 'p3', isWinner: true },
      ],
    });
  });

  it('respects the disabled prop on every fieldset', () => {
    render(<BinaryWinEditor players={PLAYERS} onChange={vi.fn()} disabled />);
    PLAYERS.forEach(p => {
      expect(screen.getByTestId(`binary-win-${p.id}`)).toBeDisabled();
      expect(screen.getByTestId(`binary-lose-${p.id}`)).toBeDisabled();
    });
  });

  it('flipping a winner back to Lose updates the snapshot', () => {
    const onChange = vi.fn();
    const initialData = {
      results: [
        { playerId: 'p1', isWinner: true },
        { playerId: 'p2', isWinner: false },
        { playerId: 'p3', isWinner: false },
      ],
    };
    render(<BinaryWinEditor players={PLAYERS} initialData={initialData} onChange={onChange} />);
    fireEvent.click(screen.getByTestId('binary-lose-p1'));
    expect(onChange).toHaveBeenLastCalledWith({
      results: [
        { playerId: 'p1', isWinner: false },
        { playerId: 'p2', isWinner: false },
        { playerId: 'p3', isWinner: false },
      ],
    });
  });
});
