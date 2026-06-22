/**
 * userHue / userHsl - Unit tests (Issue #1470).
 *
 * Deterministic client-side color utility used where the BE doesn't expose
 * `User.AccentHue` (avatar background colors in player/leaderboard surfaces).
 *
 * Test matrix (Crispin):
 *   T1. userHue returns an integer hue in [0, 360) for a known id.
 *   T2. Deterministic — same id → same hue.
 *   T3. Distributes across many distinct hues (not constant).
 *   T4. Empty id → 0 (edge case / "unknown user").
 *   T5. Output stays in [0, 360) across many ids.
 *   T6. userHsl without alpha → hsl(h, 60%, 55%).
 *   T7. userHsl with alpha → hsla(h, 60%, 55%, a).
 *   T8. userHsl on empty id → hsl(0, 60%, 55%).
 */

import { describe, expect, it } from 'vitest';

import { userHue, userHsl } from '../user-hue';

describe('userHue (Issue #1470)', () => {
  // T1
  it('returns an integer hue in [0, 360) for a known id', () => {
    const h = userHue('p-marco');
    expect(Number.isInteger(h)).toBe(true);
    expect(h).toBeGreaterThanOrEqual(0);
    expect(h).toBeLessThan(360);
  });

  // T2
  it('is deterministic — same id yields the same hue', () => {
    expect(userHue('p-marco')).toBe(userHue('p-marco'));
    expect(userHue('00000000-0000-0000-0000-000000000001')).toBe(
      userHue('00000000-0000-0000-0000-000000000001')
    );
  });

  // T3
  it('distributes different ids across many distinct hues', () => {
    const hues = new Set<number>();
    for (let i = 0; i < 100; i++) hues.add(userHue(`user-${i}`));
    // A constant or poorly-distributed hash would collapse to a few buckets.
    expect(hues.size).toBeGreaterThan(50);
  });

  // T4
  it('returns 0 for an empty id', () => {
    expect(userHue('')).toBe(0);
  });

  // T5
  it('keeps every output within [0, 360)', () => {
    for (let i = 0; i < 200; i++) {
      const h = userHue(`u-${i}-${i * 7}`);
      expect(h).toBeGreaterThanOrEqual(0);
      expect(h).toBeLessThan(360);
    }
  });
});

describe('userHsl (Issue #1470)', () => {
  // T6
  it('returns an hsl string tuned for AA when no alpha is given', () => {
    expect(userHsl('p-marco')).toBe(`hsl(${userHue('p-marco')}, 60%, 55%)`);
  });

  // T7
  it('returns an hsla string when alpha is provided', () => {
    expect(userHsl('p-marco', 0.4)).toBe(`hsla(${userHue('p-marco')}, 60%, 55%, 0.4)`);
  });

  // T8
  it('returns hsl with hue 0 for an empty id', () => {
    expect(userHsl('')).toBe('hsl(0, 60%, 55%)');
  });
});
