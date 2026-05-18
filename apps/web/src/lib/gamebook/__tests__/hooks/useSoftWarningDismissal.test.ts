import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import {
  useSoftWarningDismissal,
  SOFT_WARNING_STORAGE_KEY,
} from '../../hooks/useSoftWarningDismissal';

describe('useSoftWarningDismissal', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('shouldShow=true when usage is 90% and not dismissed', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(45, 50));
    expect(result.current.shouldShow).toBe(true);
  });

  it('shouldShow=true at the exact 0.9 boundary', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(9, 10));
    expect(result.current.shouldShow).toBe(true);
  });

  it('shouldShow=false when usage < 90%', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(44, 50));
    expect(result.current.shouldShow).toBe(false);
  });

  it('shouldShow=false when usage = 100% (hard limit, not soft)', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(50, 50));
    expect(result.current.shouldShow).toBe(false);
  });

  it('shouldShow=false after dismiss()', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(47, 50));
    expect(result.current.shouldShow).toBe(true);
    act(() => result.current.dismiss());
    expect(result.current.shouldShow).toBe(false);
  });

  it('dismiss() persists ISO timestamp to sessionStorage', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(47, 50));
    act(() => result.current.dismiss());
    const stored = sessionStorage.getItem(SOFT_WARNING_STORAGE_KEY);
    expect(stored).not.toBeNull();
    expect(() => new Date(stored!).toISOString()).not.toThrow();
  });

  it('shouldShow=false on remount when dismissal already in sessionStorage', () => {
    sessionStorage.setItem(SOFT_WARNING_STORAGE_KEY, new Date().toISOString());
    const { result } = renderHook(() => useSoftWarningDismissal(47, 50));
    expect(result.current.shouldShow).toBe(false);
  });

  it('shouldShow=false defensively when total <= 0', () => {
    const { result } = renderHook(() => useSoftWarningDismissal(0, 0));
    expect(result.current.shouldShow).toBe(false);
  });
});
