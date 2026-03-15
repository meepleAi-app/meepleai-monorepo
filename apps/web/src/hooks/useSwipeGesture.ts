'use client';

import { useCallback, useRef } from 'react';

interface UseSwipeGestureOptions {
  onSwipeUp?: () => void;
  onSwipeDown?: () => void;
  threshold?: number;
}

interface SwipeHandlers {
  onTouchStart: (e: React.TouchEvent) => void;
  onTouchEnd: (e: React.TouchEvent) => void;
}

export function useSwipeGesture({
  onSwipeUp,
  onSwipeDown,
  threshold = 50,
}: UseSwipeGestureOptions): { handlers: SwipeHandlers } {
  const startY = useRef<number | null>(null);

  const onTouchStart = useCallback((e: React.TouchEvent) => {
    startY.current = e.touches[0].clientY;
  }, []);

  const onTouchEnd = useCallback(
    (e: React.TouchEvent) => {
      if (startY.current === null) return;
      const endY = e.changedTouches[0].clientY;
      const diff = startY.current - endY;
      startY.current = null;
      if (Math.abs(diff) < threshold) return;
      if (diff > 0) {
        onSwipeUp?.();
      } else {
        onSwipeDown?.();
      }
    },
    [onSwipeUp, onSwipeDown, threshold]
  );

  return { handlers: { onTouchStart, onTouchEnd } };
}
