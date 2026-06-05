/**
 * Tests for PointsEditor.
 *
 * Asse D follow-up P1 (#1899) T2.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PointsEditor } from '../PointsEditor';
import type { PlayerOption } from '../types';

const PLAYERS: PlayerOption[] = [
  { id: 'p1', displayName: 'Anna' },
  { id: 'p2', displayName: 'Bob' },
];

describe('PointsEditor', () => {
  it('renders one input per player defaulting to 0', () => {
    render(<PointsEditor players={PLAYERS} onChange={vi.fn()} />);
    expect(screen.getByTestId('points-input-p1')).toHaveValue(0);
    expect(screen.getByTestId('points-input-p2')).toHaveValue(0);
  });

  it('uses initialData when provided', () => {
    const onChange = vi.fn();
    const initialData = {
      scores: [
        { playerId: 'p1', points: 42 },
        { playerId: 'p2', points: 30 },
      ],
    };
    render(<PointsEditor players={PLAYERS} initialData={initialData} onChange={onChange} />);
    expect(screen.getByTestId('points-input-p1')).toHaveValue(42);
    expect(screen.getByTestId('points-input-p2')).toHaveValue(30);

    // Initial onChange should mirror initialData
    expect(onChange).toHaveBeenLastCalledWith({
      scores: [
        { playerId: 'p1', points: 42 },
        { playerId: 'p2', points: 30 },
      ],
    });
  });

  it('emits onChange with PointsScoreData on input change', () => {
    const onChange = vi.fn();
    render(<PointsEditor players={PLAYERS} onChange={onChange} />);
    fireEvent.change(screen.getByTestId('points-input-p1'), { target: { value: '50' } });
    expect(onChange).toHaveBeenLastCalledWith({
      scores: [
        { playerId: 'p1', points: 50 },
        { playerId: 'p2', points: 0 },
      ],
    });
  });

  it('rejects negative values (input keeps previous)', () => {
    const onChange = vi.fn();
    render(<PointsEditor players={PLAYERS} onChange={onChange} />);
    const input = screen.getByTestId('points-input-p1') as HTMLInputElement;
    onChange.mockClear();
    fireEvent.change(input, { target: { value: '-5' } });
    expect(input).toHaveValue(0);
    // Negative changes must not propagate as a new payload
    expect(onChange).not.toHaveBeenCalled();
  });

  it('respects the disabled prop', () => {
    render(<PointsEditor players={PLAYERS} onChange={vi.fn()} disabled />);
    expect(screen.getByTestId('points-input-p1')).toBeDisabled();
    expect(screen.getByTestId('points-input-p2')).toBeDisabled();
  });

  it('emits an initial snapshot on mount even without initialData', () => {
    const onChange = vi.fn();
    render(<PointsEditor players={PLAYERS} onChange={onChange} />);
    expect(onChange).toHaveBeenCalledWith({
      scores: [
        { playerId: 'p1', points: 0 },
        { playerId: 'p2', points: 0 },
      ],
    });
  });
});
