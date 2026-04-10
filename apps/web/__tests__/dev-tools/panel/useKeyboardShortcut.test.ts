import { describe, it, expect, vi, afterEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useKeyboardShortcut } from '@/dev-tools/panel/hooks/useKeyboardShortcut';

describe('useKeyboardShortcut', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('fires handler on Ctrl+Shift+M', () => {
    const handler = vi.fn();
    renderHook(() => useKeyboardShortcut({ ctrl: true, shift: true, key: 'm' }, handler));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'm', ctrlKey: true, shiftKey: true }));
    expect(handler).toHaveBeenCalledOnce();
  });

  it('fires handler on Cmd+Shift+M (macOS, metaKey)', () => {
    const handler = vi.fn();
    renderHook(() => useKeyboardShortcut({ ctrl: true, shift: true, key: 'm' }, handler));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'm', metaKey: true, shiftKey: true }));
    expect(handler).toHaveBeenCalledOnce();
  });

  it('does NOT fire on Shift+M alone', () => {
    const handler = vi.fn();
    renderHook(() => useKeyboardShortcut({ ctrl: true, shift: true, key: 'm' }, handler));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'm', shiftKey: true }));
    expect(handler).not.toHaveBeenCalled();
  });

  it('does NOT fire on Ctrl+M alone (no shift)', () => {
    const handler = vi.fn();
    renderHook(() => useKeyboardShortcut({ ctrl: true, shift: true, key: 'm' }, handler));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'm', ctrlKey: true }));
    expect(handler).not.toHaveBeenCalled();
  });

  it('removes listener on unmount', () => {
    const handler = vi.fn();
    const { unmount } = renderHook(() =>
      useKeyboardShortcut({ ctrl: true, shift: true, key: 'm' }, handler)
    );
    unmount();
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'm', ctrlKey: true, shiftKey: true }));
    expect(handler).not.toHaveBeenCalled();
  });

  it('matches key case-insensitively', () => {
    const handler = vi.fn();
    renderHook(() => useKeyboardShortcut({ ctrl: true, shift: true, key: 'm' }, handler));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'M', ctrlKey: true, shiftKey: true }));
    expect(handler).toHaveBeenCalledOnce();
  });
});
