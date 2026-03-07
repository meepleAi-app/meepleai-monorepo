/**
 * Haptic Feedback System Tests
 * Mobile UX Epic — Issue 14
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { haptic } from '../haptics';

// ─── Setup ───────────────────────────────────────────────────────────────────

let mockVibrate: ReturnType<typeof vi.fn>;
let mockMatchMedia: ReturnType<typeof vi.fn>;

beforeEach(() => {
  mockVibrate = vi.fn();
  Object.defineProperty(navigator, 'vibrate', {
    value: mockVibrate,
    writable: true,
    configurable: true,
  });

  mockMatchMedia = vi.fn().mockReturnValue({ matches: false });
  Object.defineProperty(window, 'matchMedia', {
    value: mockMatchMedia,
    writable: true,
    configurable: true,
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('haptic', () => {
  it('tap() vibrates for 10ms', () => {
    haptic.tap();
    expect(mockVibrate).toHaveBeenCalledWith(10);
  });

  it('success() vibrates with double pulse pattern', () => {
    haptic.success();
    expect(mockVibrate).toHaveBeenCalledWith([10, 50, 10]);
  });

  it('error() vibrates for 200ms', () => {
    haptic.error();
    expect(mockVibrate).toHaveBeenCalledWith(200);
  });

  it('selection() vibrates for 5ms', () => {
    haptic.selection();
    expect(mockVibrate).toHaveBeenCalledWith(5);
  });

  it('contextChange() vibrates for 15ms', () => {
    haptic.contextChange();
    expect(mockVibrate).toHaveBeenCalledWith(15);
  });

  it('does not vibrate when navigator.vibrate is undefined', () => {
    Object.defineProperty(navigator, 'vibrate', {
      value: undefined,
      writable: true,
      configurable: true,
    });

    haptic.tap();
    // Should not throw, just silently do nothing
    expect(true).toBe(true);
  });

  it('does not vibrate when prefers-reduced-motion is set', () => {
    mockMatchMedia.mockReturnValue({ matches: true });

    haptic.tap();
    expect(mockVibrate).not.toHaveBeenCalled();
  });

  it('vibrates when prefers-reduced-motion is not set', () => {
    mockMatchMedia.mockReturnValue({ matches: false });

    haptic.tap();
    expect(mockVibrate).toHaveBeenCalled();
  });

  it('checks prefers-reduced-motion media query', () => {
    haptic.tap();
    expect(mockMatchMedia).toHaveBeenCalledWith('(prefers-reduced-motion: reduce)');
  });
});
