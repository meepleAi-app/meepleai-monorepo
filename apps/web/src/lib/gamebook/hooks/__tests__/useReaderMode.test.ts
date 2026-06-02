import { renderHook, act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useReaderMode } from '../useReaderMode';

describe('useReaderMode', () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  it('returns false initially when localStorage is empty (S1 default)', () => {
    const { result } = renderHook(() => useReaderMode());
    expect(result.current.isReaderMode).toBe(false);
  });

  it('returns true when localStorage has reader-mode-enabled=true (S2 persistence)', () => {
    window.localStorage.setItem('reader-mode-enabled', 'true');
    const { result } = renderHook(() => useReaderMode());
    expect(result.current.isReaderMode).toBe(true);
  });

  it('toggle() flips state and writes to localStorage', () => {
    const { result } = renderHook(() => useReaderMode());
    act(() => result.current.toggle());
    expect(result.current.isReaderMode).toBe(true);
    expect(window.localStorage.getItem('reader-mode-enabled')).toBe('true');
    act(() => result.current.toggle());
    expect(result.current.isReaderMode).toBe(false);
    expect(window.localStorage.getItem('reader-mode-enabled')).toBe('false');
  });

  it('preserves state across remount via localStorage (S4)', () => {
    const { result: r1, unmount } = renderHook(() => useReaderMode());
    act(() => r1.current.toggle());
    unmount();
    const { result: r2 } = renderHook(() => useReaderMode());
    expect(r2.current.isReaderMode).toBe(true);
  });

  it('falls back to in-memory state when localStorage.setItem throws (S5)', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError');
    });
    const { result } = renderHook(() => useReaderMode());
    act(() => result.current.toggle());
    expect(result.current.isReaderMode).toBe(true); // in-memory still works
    expect(consoleError).not.toHaveBeenCalled(); // silent
  });

  it('falls back to false when localStorage.getItem throws on mount', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });
    const { result } = renderHook(() => useReaderMode());
    expect(result.current.isReaderMode).toBe(false);
  });

  it('ignores invalid localStorage values (not "true"/"false")', () => {
    window.localStorage.setItem('reader-mode-enabled', 'garbage');
    const { result } = renderHook(() => useReaderMode());
    expect(result.current.isReaderMode).toBe(false);
  });

  it('toggle() is stable across renders (referential equality)', () => {
    const { result, rerender } = renderHook(() => useReaderMode());
    const firstToggle = result.current.toggle;
    rerender();
    expect(result.current.toggle).toBe(firstToggle);
  });
});
