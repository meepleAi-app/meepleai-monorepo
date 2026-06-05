import { describe, expect, it } from 'vitest';

import { simpleHash } from '../simple-hash';

describe('simpleHash (cyrb53)', () => {
  it('returns same hash for same input (deterministic)', () => {
    expect(simpleHash('chunk-42')).toBe(simpleHash('chunk-42'));
    expect(simpleHash('any-seed')).toBe(simpleHash('any-seed'));
  });

  it('returns different hashes for different inputs', () => {
    expect(simpleHash('chunk-1')).not.toBe(simpleHash('chunk-2'));
    expect(simpleHash('a')).not.toBe(simpleHash('b'));
  });

  it('returns a non-negative finite number', () => {
    const h = simpleHash('test');
    expect(Number.isFinite(h)).toBe(true);
    expect(h).toBeGreaterThanOrEqual(0);
  });
});
