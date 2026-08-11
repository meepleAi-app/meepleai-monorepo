/**
 * Tests for RankingEditor.
 *
 * Visual drag-and-drop simulation is not exercised here: jsdom does not
 * implement HTMLElement#setPointerCapture / hasPointerCapture, which
 * `@dnd-kit/core`'s PointerSensor requires. Full drag interactions are
 * covered in the E2E suite. The tests below focus on rendering, ordering,
 * and accessibility surface — the contract the dispatcher depends on.
 *
 * Asse D follow-up P1 (#1899) T3.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { RankingEditor } from '../RankingEditor';
import type { PlayerOption } from '../types';

const PLAYERS: PlayerOption[] = [
  { id: 'p1', displayName: 'Anna' },
  { id: 'p2', displayName: 'Bob' },
  { id: 'p3', displayName: 'Carla' },
];

describe('RankingEditor', () => {
  it('renders players in default order and emits sequential positions', () => {
    const onChange = vi.fn();
    render(<RankingEditor players={PLAYERS} onChange={onChange} />);

    expect(screen.getByTestId('ranking-position-p1')).toHaveTextContent('1');
    expect(screen.getByTestId('ranking-position-p2')).toHaveTextContent('2');
    expect(screen.getByTestId('ranking-position-p3')).toHaveTextContent('3');

    expect(onChange).toHaveBeenLastCalledWith({
      positions: [
        { playerId: 'p1', position: 1 },
        { playerId: 'p2', position: 2 },
        { playerId: 'p3', position: 3 },
      ],
    });
  });

  it('uses initialData to seed order, sorted by position ascending', () => {
    const onChange = vi.fn();
    const initialData = {
      positions: [
        { playerId: 'p2', position: 1 },
        { playerId: 'p3', position: 2 },
        { playerId: 'p1', position: 3 },
      ],
    };
    render(<RankingEditor players={PLAYERS} initialData={initialData} onChange={onChange} />);

    expect(screen.getByTestId('ranking-position-p2')).toHaveTextContent('1');
    expect(screen.getByTestId('ranking-position-p3')).toHaveTextContent('2');
    expect(screen.getByTestId('ranking-position-p1')).toHaveTextContent('3');

    expect(onChange).toHaveBeenLastCalledWith({
      positions: [
        { playerId: 'p2', position: 1 },
        { playerId: 'p3', position: 2 },
        { playerId: 'p1', position: 3 },
      ],
    });
  });

  it('falls back to player list order when initialData.positions is empty', () => {
    const onChange = vi.fn();
    render(<RankingEditor players={PLAYERS} initialData={{ positions: [] }} onChange={onChange} />);
    expect(onChange).toHaveBeenLastCalledWith({
      positions: [
        { playerId: 'p1', position: 1 },
        { playerId: 'p2', position: 2 },
        { playerId: 'p3', position: 3 },
      ],
    });
  });

  it('exposes accessible drag handles per player', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} />);
    PLAYERS.forEach(p => {
      const handle = screen.getByTestId(`ranking-handle-${p.id}`);
      expect(handle).toBeInTheDocument();
      expect(handle).toHaveAttribute('aria-label', `Trascina ${p.displayName}`);
    });
  });

  it('disables every drag handle when disabled prop is set', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} disabled />);
    PLAYERS.forEach(p => {
      expect(screen.getByTestId(`ranking-handle-${p.id}`)).toBeDisabled();
    });
  });

  it('renders nothing for a player id missing from the players prop', () => {
    const onChange = vi.fn();
    const partialPlayers: PlayerOption[] = [{ id: 'p1', displayName: 'Anna' }];
    const initialData = {
      positions: [
        { playerId: 'p1', position: 1 },
        // Note: p2 is in initialData but NOT in players
        { playerId: 'p2', position: 2 },
      ],
    };
    render(
      <RankingEditor players={partialPlayers} initialData={initialData} onChange={onChange} />
    );
    expect(screen.getByTestId('ranking-item-p1')).toBeInTheDocument();
    expect(screen.queryByTestId('ranking-item-p2')).not.toBeInTheDocument();
  });

  // #3196: mobile touch-targets — enlarged handle + touch-friendly up/down reorder.
  it('enlarges the drag handle to a 44px touch target', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} />);
    const handle = screen.getByTestId('ranking-handle-p1');
    expect(handle).toHaveClass('min-h-[44px]');
    expect(handle).toHaveClass('min-w-[44px]');
  });

  it('reorders via the mobile up arrow', () => {
    const onChange = vi.fn();
    render(<RankingEditor players={PLAYERS} onChange={onChange} />);
    fireEvent.click(screen.getByTestId('ranking-up-p2'));
    expect(onChange).toHaveBeenLastCalledWith({
      positions: [
        { playerId: 'p2', position: 1 },
        { playerId: 'p1', position: 2 },
        { playerId: 'p3', position: 3 },
      ],
    });
  });

  it('reorders via the mobile down arrow', () => {
    const onChange = vi.fn();
    render(<RankingEditor players={PLAYERS} onChange={onChange} />);
    fireEvent.click(screen.getByTestId('ranking-down-p1'));
    expect(onChange).toHaveBeenLastCalledWith({
      positions: [
        { playerId: 'p2', position: 1 },
        { playerId: 'p1', position: 2 },
        { playerId: 'p3', position: 3 },
      ],
    });
  });

  it('disables up on the first item and down on the last', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} />);
    expect(screen.getByTestId('ranking-up-p1')).toBeDisabled();
    expect(screen.getByTestId('ranking-down-p3')).toBeDisabled();
  });

  it('disables reorder arrows when the disabled prop is set', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} disabled />);
    expect(screen.getByTestId('ranking-up-p2')).toBeDisabled();
    expect(screen.getByTestId('ranking-down-p2')).toBeDisabled();
  });

  it('hides the reorder arrows on desktop (md:hidden wrapper)', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} />);
    // The md:hidden lives on the wrapper div, not the buttons.
    expect(screen.getByTestId('ranking-up-p2').closest('div')).toHaveClass('md:hidden');
  });
});
