import { describe, expect, it } from 'vitest';

import { CODENAMES_WORD_BANK, generateCodenamesBoard } from '../codenames-board-preset';
import type { CodenamesKey } from '../codenames-state';

function keyCounts(board: { key: CodenamesKey }[]): Record<string, number> {
  return board.reduce<Record<string, number>>((acc, c) => {
    acc[c.key] = (acc[c.key] ?? 0) + 1;
    return acc;
  }, {});
}

describe('generateCodenamesBoard', () => {
  it('produces exactly 25 cells with 25 distinct words', () => {
    const { board } = generateCodenamesBoard();
    expect(board).toHaveLength(25);
    expect(new Set(board.map(c => c.word)).size).toBe(25);
  });

  it('has a valid key multiset for the starting team (9/8/7/1)', () => {
    const { board, startingTeam } = generateCodenamesBoard('red');
    const counts = keyCounts(board);
    expect(startingTeam).toBe('red');
    expect(counts.red).toBe(9); // starting team
    expect(counts.blue).toBe(8);
    expect(counts.neutral).toBe(7);
    expect(counts.assassin).toBe(1);
  });

  it('gives the OTHER starting team the 9-count when requested', () => {
    const { board } = generateCodenamesBoard('blue');
    const counts = keyCounts(board);
    expect(counts.blue).toBe(9);
    expect(counts.red).toBe(8);
  });

  it('starts all cells unrevealed', () => {
    expect(generateCodenamesBoard().board.every(c => !c.revealed)).toBe(true);
  });

  it('draws only from the word bank, which has at least 25 distinct words', () => {
    expect(new Set(CODENAMES_WORD_BANK).size).toBeGreaterThanOrEqual(25);
    const { board } = generateCodenamesBoard();
    expect(board.every(c => CODENAMES_WORD_BANK.includes(c.word))).toBe(true);
  });
});
