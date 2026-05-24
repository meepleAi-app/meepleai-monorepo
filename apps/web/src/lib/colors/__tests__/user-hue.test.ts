import { describe, expect, it } from 'vitest';

import { userHsl, userHue } from '../user-hue';

describe('userHue', () => {
  it('returns a deterministic value for the same userId across calls', () => {
    const a = userHue('p-marco');
    const b = userHue('p-marco');
    expect(a).toBe(b);
  });

  it('always returns a value in [0, 360)', () => {
    const inputs = [
      'p-marco',
      'p-elisa',
      'p-1',
      'a-very-long-user-id-12345-with-extras',
      '00000000-0000-0000-0000-000000000001',
      'unicode-utente-àèìòù',
    ];
    for (const v of inputs) {
      const h = userHue(v);
      expect(h).toBeGreaterThanOrEqual(0);
      expect(h).toBeLessThan(360);
      expect(Number.isInteger(h)).toBe(true);
    }
  });

  it('returns different hues for different userIds (statistical distribution)', () => {
    // 8 distinct userIds should map to ≥6 distinct hues with overwhelming
    // probability under uniform hash distribution (collision rate ≪ 1%).
    const ids = ['alice', 'bob', 'charlie', 'dave', 'erin', 'frank', 'grace', 'heidi'];
    const hues = new Set(ids.map(id => userHue(id)));
    expect(hues.size).toBeGreaterThanOrEqual(6);
  });

  it('returns 0 for empty userId (edge case)', () => {
    expect(userHue('')).toBe(0);
  });

  it('distinguishes Guid-format userIds that differ by a single character', () => {
    const a = userHue('00000000-0000-0000-0000-000000000001');
    const b = userHue('00000000-0000-0000-0000-000000000002');
    expect(a).not.toBe(b);
  });
});

describe('userHsl', () => {
  it('returns a valid hsl() string with default saturation/lightness when alpha is omitted', () => {
    const s = userHsl('p-marco');
    expect(s).toMatch(/^hsl\(\d{1,3}, 60%, 55%\)$/);
  });

  it('returns a valid hsla() string when alpha is provided', () => {
    const s = userHsl('p-marco', 0.3);
    expect(s).toMatch(/^hsla\(\d{1,3}, 60%, 55%, 0\.3\)$/);
  });

  it('accepts alpha = 0 and alpha = 1 boundary values', () => {
    expect(userHsl('p-marco', 0)).toMatch(/^hsla\(\d{1,3}, 60%, 55%, 0\)$/);
    expect(userHsl('p-marco', 1)).toMatch(/^hsla\(\d{1,3}, 60%, 55%, 1\)$/);
  });

  it('produces stable output across calls for the same userId', () => {
    expect(userHsl('p-marco')).toBe(userHsl('p-marco'));
    expect(userHsl('p-marco', 0.5)).toBe(userHsl('p-marco', 0.5));
  });

  it('produces hsl() (not hsla()) when alpha is undefined explicitly', () => {
    const s = userHsl('p-marco', undefined);
    expect(s.startsWith('hsl(')).toBe(true);
    expect(s.startsWith('hsla(')).toBe(false);
  });
});
