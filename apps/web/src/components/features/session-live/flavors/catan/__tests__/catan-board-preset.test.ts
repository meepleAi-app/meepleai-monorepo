import { describe, expect, it } from 'vitest';

import { generateStandardBoard } from '../catan-board-preset';
import type { CatanTerrain } from '../catan-state';

function terrainCounts(hexes: { terrain: CatanTerrain }[]): Record<string, number> {
  return hexes.reduce<Record<string, number>>((acc, h) => {
    acc[h.terrain] = (acc[h.terrain] ?? 0) + 1;
    return acc;
  }, {});
}

describe('generateStandardBoard', () => {
  it('produces exactly 19 hexes with ids h0..h18', () => {
    const { hexes } = generateStandardBoard();
    expect(hexes).toHaveLength(19);
    expect(new Set(hexes.map(h => h.id)).size).toBe(19);
    expect(hexes.every(h => /^h\d+$/.test(h.id))).toBe(true);
  });

  it('uses the standard base-game terrain multiset', () => {
    expect(terrainCounts(generateStandardBoard().hexes)).toEqual({
      wood: 4,
      sheep: 4,
      wheat: 4,
      brick: 3,
      ore: 3,
      desert: 1,
    });
  });

  it('assigns the standard 18 number tokens to non-desert hexes; desert is numberless', () => {
    const { hexes } = generateStandardBoard();
    const desert = hexes.filter(h => h.terrain === 'desert');
    expect(desert).toHaveLength(1);
    expect(desert[0]?.number).toBeNull();
    const numbers = hexes
      .filter(h => h.terrain !== 'desert')
      .map(h => h.number)
      .sort((a, b) => (a ?? 0) - (b ?? 0));
    expect(numbers).toEqual([2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12]);
  });

  it('starts the robber on the desert hex', () => {
    const { hexes, robberHexId } = generateStandardBoard();
    const robberHex = hexes.find(h => h.id === robberHexId);
    expect(robberHex?.terrain).toBe('desert');
  });

  it('lays hexes out in columns of heights 3,4,5,4,3', () => {
    const { hexes } = generateStandardBoard();
    const perCol = [0, 1, 2, 3, 4].map(c => hexes.filter(h => h.col === c).length);
    expect(perCol).toEqual([3, 4, 5, 4, 3]);
  });

  it('emits ports anchored to existing hex ids', () => {
    const { hexes, ports } = generateStandardBoard();
    const ids = new Set(hexes.map(h => h.id));
    expect(ports.length).toBeGreaterThan(0);
    expect(ports.every(p => ids.has(p.hexId))).toBe(true);
  });
});
