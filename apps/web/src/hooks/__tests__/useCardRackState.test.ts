import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useCardRackState } from '../useCardRackState';

describe('useCardRackState', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts collapsed (isExpanded = false)', () => {
    const { result } = renderHook(() => useCardRackState());
    expect(result.current.isExpanded).toBe(false);
  });

  it('expands on mouse enter after delay', () => {
    const { result } = renderHook(() => useCardRackState());

    act(() => {
      result.current.onMouseEnter();
    });
    expect(result.current.isExpanded).toBe(false);

    act(() => {
      vi.advanceTimersByTime(200);
    });
    expect(result.current.isExpanded).toBe(true);
  });

  it('collapses on mouse leave after delay', () => {
    const { result } = renderHook(() => useCardRackState());

    act(() => {
      result.current.onMouseEnter();
      vi.advanceTimersByTime(200);
    });
    expect(result.current.isExpanded).toBe(true);

    act(() => {
      result.current.onMouseLeave();
      vi.advanceTimersByTime(300);
    });
    expect(result.current.isExpanded).toBe(false);
  });

  it('cancels expand if mouse leaves before delay', () => {
    const { result } = renderHook(() => useCardRackState());

    act(() => {
      result.current.onMouseEnter();
      vi.advanceTimersByTime(100);
      result.current.onMouseLeave();
      vi.advanceTimersByTime(500);
    });

    expect(result.current.isExpanded).toBe(false);
  });

  it('provides ref for the container element', () => {
    const { result } = renderHook(() => useCardRackState());
    expect(result.current.rackRef).toBeDefined();
  });
});
