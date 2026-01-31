/**
 * Tests for useCommandPalette Hook (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Command palette state, keyboard shortcuts, open/close/toggle
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { useCommandPalette } from '../useCommandPalette';

describe('useCommandPalette', () => {
  beforeEach(() => {
    // Clear any event listeners
    document.removeEventListener('keydown', () => {});
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('should initialize with isOpen=false', () => {
    const { result } = renderHook(() => useCommandPalette());

    expect(result.current.isOpen).toBe(false);
  });

  it('should open palette', () => {
    const { result } = renderHook(() => useCommandPalette());

    act(() => {
      result.current.open();
    });

    expect(result.current.isOpen).toBe(true);
  });

  it('should close palette', () => {
    const { result } = renderHook(() => useCommandPalette());

    act(() => {
      result.current.open();
      result.current.close();
    });

    expect(result.current.isOpen).toBe(false);
  });

  it('should toggle palette', () => {
    const { result } = renderHook(() => useCommandPalette());

    expect(result.current.isOpen).toBe(false);

    act(() => {
      result.current.toggle();
    });
    expect(result.current.isOpen).toBe(true);

    act(() => {
      result.current.toggle();
    });
    expect(result.current.isOpen).toBe(false);
  });

  it('should open on Cmd+K (macOS)', () => {
    const { result } = renderHook(() => useCommandPalette());

    act(() => {
      const event = new KeyboardEvent('keydown', {
        key: 'k',
        metaKey: true,
      });
      document.dispatchEvent(event);
    });

    expect(result.current.isOpen).toBe(true);
  });

  it('should open on Ctrl+K (Windows/Linux)', () => {
    const { result } = renderHook(() => useCommandPalette());

    act(() => {
      const event = new KeyboardEvent('keydown', {
        key: 'k',
        ctrlKey: true,
      });
      document.dispatchEvent(event);
    });

    expect(result.current.isOpen).toBe(true);
  });

  it('should toggle on repeated Cmd+K', () => {
    const { result } = renderHook(() => useCommandPalette());

    // First press - open
    act(() => {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true }));
    });
    expect(result.current.isOpen).toBe(true);

    // Second press - close
    act(() => {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true }));
    });
    expect(result.current.isOpen).toBe(false);
  });

  it('should NOT open on K without modifiers', () => {
    const { result } = renderHook(() => useCommandPalette());

    act(() => {
      const event = new KeyboardEvent('keydown', { key: 'k' });
      document.dispatchEvent(event);
    });

    expect(result.current.isOpen).toBe(false);
  });

  it('should cleanup event listener on unmount', () => {
    const removeEventListenerSpy = vi.spyOn(document, 'removeEventListener');

    const { unmount } = renderHook(() => useCommandPalette());

    unmount();

    expect(removeEventListenerSpy).toHaveBeenCalledWith('keydown', expect.any(Function));
  });
});
