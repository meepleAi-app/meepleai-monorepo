/**
 * Tests for useSidebarState hook
 * Validates localStorage persistence, cookie sync, toggle, and graceful fallback.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useSidebarState } from '../useSidebarState';

describe('useSidebarState', () => {
  let getItemSpy: ReturnType<typeof vi.spyOn>;
  let setItemSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    localStorage.clear();
    document.cookie = 'meepleai-sidebar-collapsed=; max-age=0';
    getItemSpy = vi.spyOn(Storage.prototype, 'getItem');
    setItemSpy = vi.spyOn(Storage.prototype, 'setItem');
  });

  afterEach(() => {
    getItemSpy.mockRestore();
    setItemSpy.mockRestore();
  });

  it('defaults to expanded (isCollapsed = false)', () => {
    const { result } = renderHook(() => useSidebarState());
    expect(result.current.isCollapsed).toBe(false);
  });

  it('respects initialValue parameter', () => {
    const { result } = renderHook(() => useSidebarState(true));
    expect(result.current.isCollapsed).toBe(true);
  });

  it('toggle switches collapsed state', () => {
    const { result } = renderHook(() => useSidebarState());

    act(() => {
      result.current.toggle();
    });
    expect(result.current.isCollapsed).toBe(true);

    act(() => {
      result.current.toggle();
    });
    expect(result.current.isCollapsed).toBe(false);
  });

  it('setCollapsed sets state directly', () => {
    const { result } = renderHook(() => useSidebarState());

    act(() => {
      result.current.setCollapsed(true);
    });
    expect(result.current.isCollapsed).toBe(true);

    act(() => {
      result.current.setCollapsed(false);
    });
    expect(result.current.isCollapsed).toBe(false);
  });

  it('persists to localStorage on toggle', () => {
    const { result } = renderHook(() => useSidebarState());

    act(() => {
      result.current.toggle();
    });

    expect(setItemSpy).toHaveBeenCalledWith('meepleai-sidebar-collapsed', 'true');
  });

  it('sets cookie on toggle', () => {
    const { result } = renderHook(() => useSidebarState());

    act(() => {
      result.current.toggle();
    });

    expect(document.cookie).toContain('meepleai-sidebar-collapsed=true');
  });

  it('sets cookie on setCollapsed', () => {
    const { result } = renderHook(() => useSidebarState());

    act(() => {
      result.current.setCollapsed(true);
    });

    expect(document.cookie).toContain('meepleai-sidebar-collapsed=true');
  });

  it('reads stored value on mount', () => {
    localStorage.setItem('meepleai-sidebar-collapsed', 'true');

    const { result } = renderHook(() => useSidebarState());

    // useEffect runs async, but in tests it's sync after renderHook
    expect(result.current.isCollapsed).toBe(true);
  });

  it('syncs cookie from localStorage on mount', () => {
    localStorage.setItem('meepleai-sidebar-collapsed', 'true');

    renderHook(() => useSidebarState());

    expect(document.cookie).toContain('meepleai-sidebar-collapsed=true');
  });

  it('handles localStorage errors gracefully', () => {
    getItemSpy.mockImplementation(() => {
      throw new Error('localStorage unavailable');
    });

    const { result } = renderHook(() => useSidebarState());
    // Should not throw, defaults to false
    expect(result.current.isCollapsed).toBe(false);
  });

  it('handles localStorage setItem errors gracefully', () => {
    setItemSpy.mockImplementation(() => {
      throw new Error('localStorage unavailable');
    });

    const { result } = renderHook(() => useSidebarState());

    // Should not throw
    act(() => {
      result.current.toggle();
    });
    expect(result.current.isCollapsed).toBe(true);
  });
});
