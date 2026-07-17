import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';

import { CodenamesTeamTracker } from '../CodenamesTeamTracker';
import { generateCodenamesBoard } from '../codenames-board-preset';

const { board } = generateCodenamesBoard('red'); // red = 9, blue = 8
const labels = {
  redLabel: 'Rossi',
  blueLabel: 'Blu',
  foundTemplate: '{found}/{total}',
  turnLabel: 'Al turno',
};

describe('CodenamesTeamTracker', () => {
  it('shows found/total per team derived from the board', () => {
    const b = board.map((c, i) =>
      c.key === 'red' && i === board.findIndex(x => x.key === 'red') ? { ...c, revealed: true } : c
    );
    const { container } = render(
      <CodenamesTeamTracker board={b} currentTeam="red" labels={labels} />
    );
    expect(container.querySelector('[data-team="red"]')?.textContent).toContain('1/9');
    expect(container.querySelector('[data-team="blue"]')?.textContent).toContain('0/8');
  });

  it('marks the current team', () => {
    const { container } = render(
      <CodenamesTeamTracker board={board} currentTeam="blue" labels={labels} />
    );
    expect(container.querySelector('[data-team="blue"][data-current="true"]')).not.toBeNull();
    expect(container.querySelector('[data-team="red"][data-current="true"]')).toBeNull();
  });
});
