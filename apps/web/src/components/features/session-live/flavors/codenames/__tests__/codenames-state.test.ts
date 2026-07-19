import { describe, expect, it } from 'vitest';

import {
  CODENAMES_KEY_COUNTS,
  codenamesWinner,
  isAssassinRevealed,
  oppositeTeam,
  parseCodenamesGameState,
  teamCounts,
  type CodenamesCell,
} from '../codenames-state';

function cells(spec: Array<[CodenamesCell['key'], boolean]>): CodenamesCell[] {
  return spec.map(([key, revealed], i) => ({ word: `W${i}`, key, revealed }));
}

// 25 cells: 9 red, 8 blue, 7 neutral, 1 assassin
function board25(): CodenamesCell[] {
  const spec: Array<[CodenamesCell['key'], boolean]> = [
    ...Array(9).fill(['red', false]),
    ...Array(8).fill(['blue', false]),
    ...Array(7).fill(['neutral', false]),
    ['assassin', false],
  ];
  return cells(spec);
}

const VALID = { v: 1, game: 'codenames', board: board25(), currentTeam: 'red', clue: null };

describe('parseCodenamesGameState', () => {
  it('parses a well-formed state', () => {
    expect(parseCodenamesGameState(VALID)?.currentTeam).toBe('red');
  });
  it('returns null for a different game', () => {
    expect(parseCodenamesGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parseCodenamesGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when the board is not exactly 25 cells', () => {
    expect(parseCodenamesGameState({ ...VALID, board: board25().slice(0, 24) })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parseCodenamesGameState(null)).toBeNull();
    expect(parseCodenamesGameState('x')).toBeNull();
  });
  it('accepts a non-null clue', () => {
    const parsed = parseCodenamesGameState({ ...VALID, clue: { word: 'MARE', number: 2 } });
    expect(parsed?.clue).toEqual({ word: 'MARE', number: 2 });
  });
});

describe('derivations', () => {
  it('oppositeTeam flips', () => {
    expect(oppositeTeam('red')).toBe('blue');
    expect(oppositeTeam('blue')).toBe('red');
  });
  it('teamCounts derives total + found from the board', () => {
    const b = board25();
    b[0].revealed = true; // one red revealed
    expect(teamCounts(b, 'red')).toEqual({ total: 9, found: 1 });
    expect(teamCounts(b, 'blue')).toEqual({ total: 8, found: 0 });
  });
  it('isAssassinRevealed is true only when the assassin cell is revealed', () => {
    expect(isAssassinRevealed(VALID.board)).toBe(false);
    const b = board25();
    b[24].revealed = true; // the assassin
    expect(isAssassinRevealed(b)).toBe(true);
  });
  it('winner: assassin revealed → the OTHER team (currentTeam loses)', () => {
    const b = board25();
    b[24].revealed = true;
    expect(codenamesWinner({ ...VALID, board: b, currentTeam: 'red' })).toBe('blue');
  });
  it('winner: all of a team revealed → that team', () => {
    const b = board25();
    for (let i = 0; i < 8; i++) b[9 + i].revealed = true; // all 8 blue
    expect(codenamesWinner({ ...VALID, board: b })).toBe('blue');
  });
  it('winner: null when the game is ongoing', () => {
    expect(codenamesWinner(VALID)).toBeNull();
  });
  it('exposes the standard key counts', () => {
    expect(CODENAMES_KEY_COUNTS).toEqual({ starting: 9, other: 8, neutral: 7, assassin: 1 });
  });
});
