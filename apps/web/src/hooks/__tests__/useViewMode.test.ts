/**
 * Tests for useViewMode client hook.
 */
import { act, renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { VIEW_MODE_COOKIE } from '@/lib/view-mode/constants';

import { useViewMode } from '../useViewMode';

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => '/admin/overview',
}));

describe('useViewMode', () => {
  beforeEach(() => {
    Object.defineProperty(document, 'cookie', {
      writable: true,
      configurable: true,
      value: '',
    });
    mockPush.mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('defaults to "admin" when cookie absent and on /admin path', () => {
    const { result } = renderHook(() => useViewMode());
    expect(result.current.viewMode).toBe('admin');
  });

  it('reads initial value from cookie when present', () => {
    document.cookie = `${VIEW_MODE_COOKIE}=user`;
    const { result } = renderHook(() => useViewMode());
    expect(result.current.viewMode).toBe('user');
  });

  it('toggle flips admin → user and writes cookie', () => {
    document.cookie = `${VIEW_MODE_COOKIE}=admin`;
    const { result } = renderHook(() => useViewMode());
    act(() => {
      result.current.toggle();
    });
    expect(result.current.viewMode).toBe('user');
    expect(document.cookie).toContain(`${VIEW_MODE_COOKIE}=user`);
  });

  it('toggle flips user → admin', () => {
    document.cookie = `${VIEW_MODE_COOKIE}=user`;
    const { result } = renderHook(() => useViewMode());
    act(() => {
      result.current.toggle();
    });
    expect(result.current.viewMode).toBe('admin');
  });

  it('toggle navigates to / when switching to user mode', () => {
    document.cookie = `${VIEW_MODE_COOKIE}=admin`;
    const { result } = renderHook(() => useViewMode());
    act(() => {
      result.current.toggle();
    });
    expect(mockPush).toHaveBeenCalledWith('/');
  });

  it('toggle navigates to /admin/overview when switching to admin mode', () => {
    document.cookie = `${VIEW_MODE_COOKIE}=user`;
    const { result } = renderHook(() => useViewMode());
    act(() => {
      result.current.toggle();
    });
    expect(mockPush).toHaveBeenCalledWith('/admin/overview');
  });
});
