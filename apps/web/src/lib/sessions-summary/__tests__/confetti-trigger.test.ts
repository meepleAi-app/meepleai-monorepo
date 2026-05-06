/**
 * Unit tests for `shouldShowConfetti` / `clearConfettiFlag` (Wave D.3 §9).
 *
 * Coverage targets (≥6 tests):
 *   - First call returns true, subsequent calls return false (same id)
 *   - Different sessionIds are independent
 *   - SSR safe: returns false when window is undefined
 *   - clearConfettiFlag resets state
 *   - Defensive: does not throw when sessionStorage write throws
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { clearConfettiFlag, shouldShowConfetti } from '../confetti-trigger';

const ID_A = '00000000-0000-4000-8000-000000000aaa';
const ID_B = '00000000-0000-4000-8000-000000000bbb';

beforeEach(() => {
  // jsdom has window.sessionStorage; clear before every test for hermeticity.
  if (typeof window !== 'undefined') {
    window.sessionStorage.clear();
  }
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('shouldShowConfetti', () => {
  it('returns true on first call for a session id', () => {
    expect(shouldShowConfetti(ID_A)).toBe(true);
  });

  it('returns false on the second call for the same session id', () => {
    shouldShowConfetti(ID_A);
    expect(shouldShowConfetti(ID_A)).toBe(false);
  });

  it('treats different session ids as independent', () => {
    expect(shouldShowConfetti(ID_A)).toBe(true);
    expect(shouldShowConfetti(ID_B)).toBe(true);
    expect(shouldShowConfetti(ID_A)).toBe(false);
    expect(shouldShowConfetti(ID_B)).toBe(false);
  });

  it('writes a sentinel value to sessionStorage so other tabs/instances see it', () => {
    shouldShowConfetti(ID_A);
    const stored = window.sessionStorage.getItem(`meeplai-d3-confetti-${ID_A}`);
    expect(stored).toBe('1');
  });

  it('returns false when sessionStorage.setItem throws (private mode safeguard)', () => {
    const setSpy = vi.spyOn(window.sessionStorage.__proto__, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceeded');
    });
    // First call hits the catch and returns false (defensive default).
    expect(shouldShowConfetti(ID_A)).toBe(false);
    setSpy.mockRestore();
  });
});

describe('clearConfettiFlag', () => {
  it('removes the sessionStorage entry for the given id', () => {
    shouldShowConfetti(ID_A);
    expect(window.sessionStorage.getItem(`meeplai-d3-confetti-${ID_A}`)).toBe('1');
    clearConfettiFlag(ID_A);
    expect(window.sessionStorage.getItem(`meeplai-d3-confetti-${ID_A}`)).toBeNull();
  });

  it('after clear, shouldShowConfetti returns true again', () => {
    shouldShowConfetti(ID_A);
    clearConfettiFlag(ID_A);
    expect(shouldShowConfetti(ID_A)).toBe(true);
  });

  it('does not throw when called for an id with no flag', () => {
    expect(() => clearConfettiFlag(ID_B)).not.toThrow();
  });

  it('does not throw when removeItem throws (private mode safeguard)', () => {
    const remSpy = vi
      .spyOn(window.sessionStorage.__proto__, 'removeItem')
      .mockImplementation(() => {
        throw new Error('boom');
      });
    expect(() => clearConfettiFlag(ID_A)).not.toThrow();
    remSpy.mockRestore();
  });
});
