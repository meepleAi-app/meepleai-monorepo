import { describe, expect, it } from 'vitest';

import type { PlayerColor } from '@/lib/api/schemas/live-sessions.schemas';

import { CATAN_NEUTRAL_HSL, catanPieceColor } from '../catan-palette';

const ALL_COLORS: PlayerColor[] = [
  'Red',
  'Blue',
  'Green',
  'Yellow',
  'Purple',
  'Orange',
  'White',
  'Black',
  'Pink',
  'Teal',
];

describe('catanPieceColor', () => {
  it('returns a distinct hsl(...) string for every PlayerColor enum member', () => {
    const seen = new Set<string>();
    for (const c of ALL_COLORS) {
      const hsl = catanPieceColor(c);
      expect(hsl).toMatch(/^hsl\(/);
      seen.add(hsl);
    }
    expect(seen.size).toBe(ALL_COLORS.length); // all distinct
  });

  it('falls back to the neutral hsl for an unknown color', () => {
    expect(catanPieceColor('Chartreuse')).toBe(CATAN_NEUTRAL_HSL);
  });
});
