/**
 * useScrollDirection - Detect scroll direction for auto-hide UI patterns
 *
 * Issue #4 from mobile-first-ux-epic.md
 *
 * Returns 'up' | 'down' | null based on scroll movement.
 * Uses passive scroll listener for performance.
 * Configurable threshold to avoid jitter on small scrolls.
 */

'use client';

import { useState, useEffect, useRef, useCallback } from 'react';

export type ScrollDirection = 'up' | 'down' | null;

interface UseScrollDirectionOptions {
  /** Minimum scroll delta (px) before direction change registers. Default: 50 */
  threshold?: number;
}

export function useScrollDirection({
  threshold = 50,
}: UseScrollDirectionOptions = {}): ScrollDirection {
  const [direction, setDirection] = useState<ScrollDirection>(null);
  const lastScrollY = useRef(0);
  const lastDirection = useRef<ScrollDirection>(null);

  const handleScroll = useCallback(() => {
    const currentY = window.scrollY;
    const diff = currentY - lastScrollY.current;

    if (Math.abs(diff) < threshold) return;

    const newDirection: ScrollDirection = diff > 0 ? 'down' : 'up';

    if (newDirection !== lastDirection.current) {
      lastDirection.current = newDirection;
      setDirection(newDirection);
    }

    lastScrollY.current = currentY;
  }, [threshold]);

  useEffect(() => {
    lastScrollY.current = window.scrollY;

    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, [handleScroll]);

  return direction;
}
