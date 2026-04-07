'use client';

import { useRef, useCallback, type ReactNode, type TouchEvent } from 'react';

interface SwipeGestureProps {
  children: ReactNode;
  onSwipeLeft?: () => void;
  onSwipeRight?: () => void;
  threshold?: number;
  className?: string;
}

export function SwipeGesture({ children, onSwipeLeft, onSwipeRight, threshold = 50, className = '' }: SwipeGestureProps) {
  const startX = useRef(0);

  const handleTouchStart = useCallback((e: TouchEvent) => {
    startX.current = e.touches[0].clientX;
  }, []);

  const handleTouchEnd = useCallback((e: TouchEvent) => {
    const diff = e.changedTouches[0].clientX - startX.current;
    if (Math.abs(diff) >= threshold) {
      if (diff > 0) onSwipeRight?.();
      else onSwipeLeft?.();
    }
  }, [threshold, onSwipeLeft, onSwipeRight]);

  return (
    <div className={className} onTouchStart={handleTouchStart} onTouchEnd={handleTouchEnd}>
      {children}
    </div>
  );
}
