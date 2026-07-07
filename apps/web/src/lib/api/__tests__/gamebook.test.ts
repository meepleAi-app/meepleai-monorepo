import { describe, it, expect } from 'vitest';

import { GameBookRole, hasRole, rolesToNames, GAME_BOOK_ROLE_ORDER } from '../gamebook';

/**
 * SI-6 (#2637): the FE `GameBookRole` bitflag must mirror the backend
 * `[Flags] enum GameBookRole` (GameManagement BC) exactly, otherwise the
 * book-manager mislabels roles decoded from the `roles` int on the wire.
 *
 * Backend source of truth (GameBookRole.cs):
 *   None=0, Tutorial=1, RulesReference=2, Narrative=4, Encounter=8, Lore=16, Setup=32
 */
describe('GameBookRole enum — parity with backend [Flags] enum GameBookRole', () => {
  it('mirrors the backend bitflag values exactly', () => {
    expect(GameBookRole.Tutorial).toBe(1);
    expect(GameBookRole.RulesReference).toBe(2);
    expect(GameBookRole.Narrative).toBe(4);
    expect(GameBookRole.Encounter).toBe(8);
    expect(GameBookRole.Lore).toBe(16);
    expect(GameBookRole.Setup).toBe(32);
  });

  it('exposes exactly the six backend roles', () => {
    expect(Object.keys(GameBookRole).sort()).toEqual(
      ['Encounter', 'Lore', 'Narrative', 'RulesReference', 'Setup', 'Tutorial'].sort()
    );
  });
});

describe('hasRole', () => {
  it('detects each role present in a composite bitflag', () => {
    const roles = GameBookRole.Tutorial | GameBookRole.Setup; // 1 | 32 = 33
    expect(hasRole(roles, GameBookRole.Tutorial)).toBe(true);
    expect(hasRole(roles, GameBookRole.Setup)).toBe(true);
    expect(hasRole(roles, GameBookRole.RulesReference)).toBe(false);
  });

  it('returns false against None (0)', () => {
    expect(hasRole(0, GameBookRole.Narrative)).toBe(false);
  });
});

describe('rolesToNames', () => {
  it('returns present role names in canonical display order', () => {
    const roles = GameBookRole.Narrative | GameBookRole.Tutorial; // 4 | 1
    expect(rolesToNames(roles)).toEqual(['Tutorial', 'Narrative']);
  });

  it('returns an empty array for None (0)', () => {
    expect(rolesToNames(0)).toEqual([]);
  });

  it('returns all six names for the full bitmask, in order', () => {
    const all =
      GameBookRole.Tutorial |
      GameBookRole.Setup |
      GameBookRole.RulesReference |
      GameBookRole.Narrative |
      GameBookRole.Encounter |
      GameBookRole.Lore;
    expect(rolesToNames(all)).toEqual([...GAME_BOOK_ROLE_ORDER]);
  });
});
