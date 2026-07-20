/**
 * useHistoryBackGuard — 1-level Back guard for transient overlays (gap A-03, #3197).
 *
 * Verifies the history-stack contract:
 *   - push exactly one entry on the open transition
 *   - Android/browser Back (popstate) → onClose
 *   - listener cleanup on unmount (no stale onClose)
 *   - non-Back close (open→false) balances the stack via history.back()
 *   - a Back-driven close does NOT also call history.back() (no double-pop)
 *   - open=false is a no-op
 */

import { renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { useHistoryBackGuard } from '../useHistoryBackGuard';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('useHistoryBackGuard (A-03, #3197)', () => {
  it('pushes exactly one history entry when open becomes true', () => {
    const pushSpy = vi.spyOn(window.history, 'pushState');
    renderHook(() => useHistoryBackGuard(true, () => {}));
    expect(pushSpy).toHaveBeenCalledTimes(1);
  });

  it('does nothing when open is false (no pushState)', () => {
    const pushSpy = vi.spyOn(window.history, 'pushState');
    renderHook(() => useHistoryBackGuard(false, () => {}));
    expect(pushSpy).not.toHaveBeenCalled();
  });

  it('calls onClose when the user presses Back (popstate)', () => {
    const onClose = vi.fn();
    renderHook(() => useHistoryBackGuard(true, onClose));
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('removes the popstate listener on unmount (no stale onClose)', () => {
    const onClose = vi.fn();
    const { unmount } = renderHook(() => useHistoryBackGuard(true, onClose));
    unmount();
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(onClose).not.toHaveBeenCalled();
  });

  it('balances the stack with history.back() on a non-Back close (open→false)', () => {
    const backSpy = vi.spyOn(window.history, 'back').mockImplementation(() => {});
    const onClose = vi.fn();
    const { rerender } = renderHook(({ open }) => useHistoryBackGuard(open, onClose), {
      initialProps: { open: true },
    });
    // ESC/backdrop/programmatic close: parent flips open to false.
    rerender({ open: false });
    expect(backSpy).toHaveBeenCalledTimes(1);
    // onClose is NOT re-fired: the reconciling pop happens after the listener is gone.
    expect(onClose).not.toHaveBeenCalled();
  });

  it('does NOT call history.back() after a Back-driven close (no double-pop)', () => {
    const backSpy = vi.spyOn(window.history, 'back').mockImplementation(() => {});
    const onClose = vi.fn();
    const { rerender } = renderHook(({ open }) => useHistoryBackGuard(open, onClose), {
      initialProps: { open: true },
    });
    // User presses Back → popstate consumes the guard entry, onClose fires.
    window.dispatchEvent(new PopStateEvent('popstate'));
    expect(onClose).toHaveBeenCalledTimes(1);
    // Parent reacts by closing; cleanup must NOT pop again (entry already gone).
    rerender({ open: false });
    expect(backSpy).not.toHaveBeenCalled();
  });
});
