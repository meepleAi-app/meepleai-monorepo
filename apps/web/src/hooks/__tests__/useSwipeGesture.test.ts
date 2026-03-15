import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSwipeGesture } from '../useSwipeGesture';

describe('useSwipeGesture', () => {
  const onSwipeUp = vi.fn();
  const onSwipeDown = vi.fn();

  beforeEach(() => vi.clearAllMocks());

  it('calls onSwipeUp when vertical swipe distance > threshold', () => {
    const { result } = renderHook(() => useSwipeGesture({ onSwipeUp, onSwipeDown, threshold: 50 }));
    act(() => {
      result.current.handlers.onTouchStart({ touches: [{ clientY: 300 }] } as any);
    });
    act(() => {
      result.current.handlers.onTouchEnd({ changedTouches: [{ clientY: 220 }] } as any);
    });
    expect(onSwipeUp).toHaveBeenCalledTimes(1);
    expect(onSwipeDown).not.toHaveBeenCalled();
  });

  it('calls onSwipeDown when swiping down', () => {
    const { result } = renderHook(() => useSwipeGesture({ onSwipeUp, onSwipeDown, threshold: 50 }));
    act(() => {
      result.current.handlers.onTouchStart({ touches: [{ clientY: 200 }] } as any);
    });
    act(() => {
      result.current.handlers.onTouchEnd({ changedTouches: [{ clientY: 300 }] } as any);
    });
    expect(onSwipeDown).toHaveBeenCalledTimes(1);
  });

  it('does not trigger if swipe distance < threshold', () => {
    const { result } = renderHook(() => useSwipeGesture({ onSwipeUp, onSwipeDown, threshold: 50 }));
    act(() => {
      result.current.handlers.onTouchStart({ touches: [{ clientY: 200 }] } as any);
    });
    act(() => {
      result.current.handlers.onTouchEnd({ changedTouches: [{ clientY: 180 }] } as any);
    });
    expect(onSwipeUp).not.toHaveBeenCalled();
    expect(onSwipeDown).not.toHaveBeenCalled();
  });
});
