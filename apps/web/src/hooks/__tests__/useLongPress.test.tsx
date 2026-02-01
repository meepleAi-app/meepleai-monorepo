/**
 * useLongPress Hook Tests
 * Issue #3291 - Phase 5: Smart FAB
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useLongPress } from '../useLongPress';

describe('useLongPress', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should return event handlers', () => {
    const onLongPress = vi.fn();
    const { result } = renderHook(() => useLongPress(onLongPress));

    expect(result.current.onTouchStart).toBeDefined();
    expect(result.current.onTouchEnd).toBeDefined();
    expect(result.current.onTouchMove).toBeDefined();
    expect(result.current.onMouseDown).toBeDefined();
    expect(result.current.onMouseUp).toBeDefined();
    expect(result.current.onMouseLeave).toBeDefined();
  });

  it('should call onLongPress after delay', () => {
    const onLongPress = vi.fn();
    const { result } = renderHook(() => useLongPress(onLongPress));

    // Simulate mouse down
    act(() => {
      result.current.onMouseDown({
        button: 0,
        clientX: 100,
        clientY: 100,
      } as React.MouseEvent);
    });

    expect(onLongPress).not.toHaveBeenCalled();

    // Advance timer past default delay (500ms)
    act(() => {
      vi.advanceTimersByTime(500);
    });

    expect(onLongPress).toHaveBeenCalledTimes(1);
  });

  it('should not call onLongPress if released before delay', () => {
    const onLongPress = vi.fn();
    const onClick = vi.fn();
    const { result } = renderHook(() =>
      useLongPress(onLongPress, { onClick })
    );

    // Simulate mouse down
    act(() => {
      result.current.onMouseDown({
        button: 0,
        clientX: 100,
        clientY: 100,
      } as React.MouseEvent);
    });

    // Release before delay
    act(() => {
      vi.advanceTimersByTime(200);
    });

    act(() => {
      result.current.onMouseUp({} as React.MouseEvent);
    });

    expect(onLongPress).not.toHaveBeenCalled();
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('should call onClick on short press', () => {
    const onLongPress = vi.fn();
    const onClick = vi.fn();
    const { result } = renderHook(() =>
      useLongPress(onLongPress, { onClick })
    );

    // Quick click
    act(() => {
      result.current.onMouseDown({
        button: 0,
        clientX: 100,
        clientY: 100,
      } as React.MouseEvent);
    });

    act(() => {
      result.current.onMouseUp({} as React.MouseEvent);
    });

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(onLongPress).not.toHaveBeenCalled();
  });

  it('should cancel on mouse leave', () => {
    const onLongPress = vi.fn();
    const { result } = renderHook(() => useLongPress(onLongPress));

    act(() => {
      result.current.onMouseDown({
        button: 0,
        clientX: 100,
        clientY: 100,
      } as React.MouseEvent);
    });

    act(() => {
      result.current.onMouseLeave({} as React.MouseEvent);
    });

    act(() => {
      vi.advanceTimersByTime(600);
    });

    expect(onLongPress).not.toHaveBeenCalled();
  });

  it('should use custom delay', () => {
    const onLongPress = vi.fn();
    const { result } = renderHook(() =>
      useLongPress(onLongPress, { delay: 1000 })
    );

    act(() => {
      result.current.onMouseDown({
        button: 0,
        clientX: 100,
        clientY: 100,
      } as React.MouseEvent);
    });

    // Not triggered at default delay
    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(onLongPress).not.toHaveBeenCalled();

    // Triggered at custom delay
    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(onLongPress).toHaveBeenCalledTimes(1);
  });

  it('should ignore non-primary mouse button', () => {
    const onLongPress = vi.fn();
    const onClick = vi.fn();
    const { result } = renderHook(() =>
      useLongPress(onLongPress, { onClick })
    );

    // Right click
    act(() => {
      result.current.onMouseDown({
        button: 2,
        clientX: 100,
        clientY: 100,
      } as React.MouseEvent);
    });

    act(() => {
      vi.advanceTimersByTime(600);
    });

    expect(onLongPress).not.toHaveBeenCalled();
  });

  it('should handle touch events', () => {
    const onLongPress = vi.fn();
    const { result } = renderHook(() => useLongPress(onLongPress));

    act(() => {
      result.current.onTouchStart({
        touches: [{ clientX: 100, clientY: 100 }],
      } as unknown as React.TouchEvent);
    });

    act(() => {
      vi.advanceTimersByTime(500);
    });

    expect(onLongPress).toHaveBeenCalledTimes(1);
  });

  it('should cancel on touch move beyond threshold', () => {
    const onLongPress = vi.fn();
    const { result } = renderHook(() =>
      useLongPress(onLongPress, { moveThreshold: 10 })
    );

    act(() => {
      result.current.onTouchStart({
        touches: [{ clientX: 100, clientY: 100 }],
      } as unknown as React.TouchEvent);
    });

    // Move beyond threshold
    act(() => {
      result.current.onTouchMove({
        touches: [{ clientX: 120, clientY: 100 }],
      } as unknown as React.TouchEvent);
    });

    act(() => {
      vi.advanceTimersByTime(600);
    });

    expect(onLongPress).not.toHaveBeenCalled();
  });
});
