/**
 * Tests for ObjectivesEditor.
 *
 * Asse D follow-up P1 (#1899) T3.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ObjectivesEditor } from '../ObjectivesEditor';
import type { PlayerOption } from '../types';

const PLAYERS: PlayerOption[] = [
  { id: 'p1', displayName: 'Anna' },
  { id: 'p2', displayName: 'Bob' },
];

const OBJECTIVES = ['Temple', 'Treasure', 'Pyramid'];

describe('ObjectivesEditor', () => {
  it('starts with no objective checked when no initialData', () => {
    const onChange = vi.fn();
    render(
      <ObjectivesEditor players={PLAYERS} availableObjectives={OBJECTIVES} onChange={onChange} />
    );
    PLAYERS.forEach(p =>
      OBJECTIVES.forEach(o => expect(screen.getByTestId(`obj-${p.id}-${o}`)).not.toBeChecked())
    );
    expect(onChange).toHaveBeenLastCalledWith({
      completedByPlayer: [
        { playerId: 'p1', objectives: [] },
        { playerId: 'p2', objectives: [] },
      ],
    });
  });

  it('toggling a single objective updates the snapshot', () => {
    const onChange = vi.fn();
    render(
      <ObjectivesEditor players={PLAYERS} availableObjectives={OBJECTIVES} onChange={onChange} />
    );
    fireEvent.click(screen.getByTestId('obj-p1-Temple'));
    expect(onChange).toHaveBeenLastCalledWith({
      completedByPlayer: [
        { playerId: 'p1', objectives: ['Temple'] },
        { playerId: 'p2', objectives: [] },
      ],
    });
  });

  it('toggles multiple objectives independently per player', () => {
    const onChange = vi.fn();
    render(
      <ObjectivesEditor players={PLAYERS} availableObjectives={OBJECTIVES} onChange={onChange} />
    );
    fireEvent.click(screen.getByTestId('obj-p1-Temple'));
    fireEvent.click(screen.getByTestId('obj-p1-Pyramid'));
    fireEvent.click(screen.getByTestId('obj-p2-Treasure'));

    const lastCall = onChange.mock.calls.at(-1)?.[0];
    expect(lastCall.completedByPlayer).toEqual([
      { playerId: 'p1', objectives: expect.arrayContaining(['Temple', 'Pyramid']) },
      { playerId: 'p2', objectives: ['Treasure'] },
    ]);
    expect(lastCall.completedByPlayer[0].objectives).toHaveLength(2);
  });

  it('clicking the same checkbox twice removes the objective (toggle off)', () => {
    const onChange = vi.fn();
    render(
      <ObjectivesEditor players={PLAYERS} availableObjectives={OBJECTIVES} onChange={onChange} />
    );
    fireEvent.click(screen.getByTestId('obj-p1-Temple'));
    fireEvent.click(screen.getByTestId('obj-p1-Temple'));
    expect(onChange).toHaveBeenLastCalledWith({
      completedByPlayer: [
        { playerId: 'p1', objectives: [] },
        { playerId: 'p2', objectives: [] },
      ],
    });
  });

  it('honors initialData and prefills checked boxes', () => {
    const onChange = vi.fn();
    const initialData = {
      completedByPlayer: [
        { playerId: 'p1', objectives: ['Temple', 'Pyramid'] },
        { playerId: 'p2', objectives: ['Treasure'] },
      ],
    };
    render(
      <ObjectivesEditor
        players={PLAYERS}
        availableObjectives={OBJECTIVES}
        initialData={initialData}
        onChange={onChange}
      />
    );
    expect(screen.getByTestId('obj-p1-Temple')).toBeChecked();
    expect(screen.getByTestId('obj-p1-Pyramid')).toBeChecked();
    expect(screen.getByTestId('obj-p1-Treasure')).not.toBeChecked();
    expect(screen.getByTestId('obj-p2-Treasure')).toBeChecked();
  });

  it('respects the disabled prop', () => {
    render(
      <ObjectivesEditor
        players={PLAYERS}
        availableObjectives={OBJECTIVES}
        onChange={vi.fn()}
        disabled
      />
    );
    OBJECTIVES.forEach(o => expect(screen.getByTestId(`obj-p1-${o}`)).toBeDisabled());
  });

  // #3196: mobile touch-target on the objective chips.
  it('gives each objective chip a 44px touch target', () => {
    render(
      <ObjectivesEditor players={PLAYERS} availableObjectives={OBJECTIVES} onChange={vi.fn()} />
    );
    const chip = screen.getByTestId(`obj-p1-${OBJECTIVES[0]}`).closest('label');
    expect(chip).toHaveClass('min-h-[44px]');
  });
});
