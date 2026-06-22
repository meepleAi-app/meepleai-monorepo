/**
 * Tests for PolymorphicScoreEditor dispatcher.
 *
 * Asse D follow-up P1 (#1899) T4.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PolymorphicScoreEditor } from '../PolymorphicScoreEditor';
import type { PlayerOption } from '../score-strategies/types';

const PLAYERS: PlayerOption[] = [
  { id: 'p1', displayName: 'Anna' },
  { id: 'p2', displayName: 'Bob' },
];

describe('PolymorphicScoreEditor', () => {
  it('renders PointsEditor when scoringType="Points" and tags the payload', () => {
    const onChange = vi.fn();
    render(<PolymorphicScoreEditor scoringType="Points" players={PLAYERS} onChange={onChange} />);
    expect(screen.getByTestId('points-editor')).toBeInTheDocument();
    expect(onChange).toHaveBeenLastCalledWith({
      scoringType: 'Points',
      data: {
        scores: [
          { playerId: 'p1', points: 0 },
          { playerId: 'p2', points: 0 },
        ],
      },
    });
  });

  it('renders BinaryWinEditor when scoringType="BinaryWin" and tags the payload', () => {
    const onChange = vi.fn();
    render(
      <PolymorphicScoreEditor scoringType="BinaryWin" players={PLAYERS} onChange={onChange} />
    );
    expect(screen.getByTestId('binary-win-editor')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('binary-win-p1'));
    expect(onChange).toHaveBeenLastCalledWith({
      scoringType: 'BinaryWin',
      data: {
        results: [
          { playerId: 'p1', isWinner: true },
          { playerId: 'p2', isWinner: false },
        ],
      },
    });
  });

  it('renders ObjectivesEditor when scoringType="Objectives" with availableObjectives', () => {
    const onChange = vi.fn();
    render(
      <PolymorphicScoreEditor
        scoringType="Objectives"
        players={PLAYERS}
        availableObjectives={['Temple', 'Treasure']}
        onChange={onChange}
      />
    );
    expect(screen.getByTestId('objectives-editor')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('obj-p1-Temple'));
    expect(onChange).toHaveBeenLastCalledWith({
      scoringType: 'Objectives',
      data: {
        completedByPlayer: [
          { playerId: 'p1', objectives: ['Temple'] },
          { playerId: 'p2', objectives: [] },
        ],
      },
    });
  });

  it('throws when scoringType="Objectives" is rendered without availableObjectives', () => {
    // Suppress React's expected error log for this throw
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() =>
      render(
        <PolymorphicScoreEditor scoringType="Objectives" players={PLAYERS} onChange={vi.fn()} />
      )
    ).toThrow(/availableObjectives.*required/i);
    errorSpy.mockRestore();
  });

  it('renders RankingEditor when scoringType="Ranking" and tags the payload', () => {
    const onChange = vi.fn();
    render(<PolymorphicScoreEditor scoringType="Ranking" players={PLAYERS} onChange={onChange} />);
    expect(screen.getByTestId('ranking-editor')).toBeInTheDocument();
    expect(onChange).toHaveBeenLastCalledWith({
      scoringType: 'Ranking',
      data: {
        positions: [
          { playerId: 'p1', position: 1 },
          { playerId: 'p2', position: 2 },
        ],
      },
    });
  });

  it('throws when scoringType is unknown at runtime', () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() =>
      render(
        // @ts-expect-error - simulating an out-of-band runtime variant
        <PolymorphicScoreEditor scoringType="Magic" players={PLAYERS} onChange={vi.fn()} />
      )
    ).toThrow(/unknown scoring type/i);
    errorSpy.mockRestore();
  });

  it('forwards the disabled prop to the active editor', () => {
    render(
      <PolymorphicScoreEditor scoringType="Points" players={PLAYERS} onChange={vi.fn()} disabled />
    );
    expect(screen.getByTestId('points-input-p1')).toBeDisabled();
  });
});
