'use client';

/**
 * useScrollState — Consolidated scroll state hook
 * Mobile UX Epic — Issue 10
 *
 * Single passive scroll listener providing:
 * - scrollY: current scroll position
 * - direction: 'up' | 'down' | null
 * - isScrolled: whether page has scrolled past threshold
 * - isScrolling: whether user is actively scrolling
 *
 * Used by:
 * - TopNavbar (shadow on scroll via isScrolled)
 * - FloatingActionBar (auto-hide via direction)
 * - SmartFAB (hide on fast scroll via direction)
 *
 * Performance:
 * - Single passive scroll listener shared across consumers
 * - requestAnimationFrame throttling for direction/velocity
 * - Configurable thresholds
 */

import { useCallback, useEffect, useRef, useState } from 'react';

// ─── Types ───────────────────────────────────────────────────────────────────

export type ScrollDirection = 'up' | 'down' | null;

export interface ScrollState {
  /** Current scroll position in pixels */
  scrollY: number;
  /** Current scroll direction */
  direction: ScrollDirection;
  /** Whether page is scrolled past the threshold */
  isScrolled: boolean;
  /** Whether user is actively scrolling */
  isScrolling: boolean;
}

export interface UseScrollStateOptions {
  /** Minimum scroll delta to register direction change (default: 50px) */
  directionThreshold?: number;
  /** Scroll position to trigger isScrolled (default: 4px) */
  scrolledThreshold?: number;
  /** Timeout to consider scrolling stopped (default: 150ms) */
  scrollingTimeout?: number;
}

// ─── Hook ────────────────────────────────────────────────────────────────────

export function useScrollState(options: UseScrollStateOptions = {}): ScrollState {
  const { directionThreshold = 50, scrolledThreshold = 4, scrollingTimeout = 150 } = options;

  const [state, setState] = useState<ScrollState>({
    scrollY: 0,
    direction: null,
    isScrolled: false,
    isScrolling: false,
  });

  const lastScrollY = useRef(0);
  const lastDirection = useRef<ScrollDirection>(null);
  const rafId = useRef<number | null>(null);
  const scrollingTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const handleScroll = useCallback(() => {
    // Cancel previous rAF to avoid stacking
    if (rafId.current !== null) {
      cancelAnimationFrame(rafId.current);
    }

    rafId.current = requestAnimationFrame(() => {
      const currentY = window.scrollY;
      const diff = currentY - lastScrollY.current;
      const isScrolled = currentY > scrolledThreshold;

      // Direction detection with threshold
      let newDirection = lastDirection.current;
      if (Math.abs(diff) >= directionThreshold) {
        newDirection = diff > 0 ? 'down' : 'up';
        if (newDirection !== lastDirection.current) {
          lastDirection.current = newDirection;
        }
        lastScrollY.current = currentY;
      }

      // Update scrolling timer
      if (scrollingTimerRef.current) {
        clearTimeout(scrollingTimerRef.current);
      }
      scrollingTimerRef.current = setTimeout(() => {
        setState(prev => ({ ...prev, isScrolling: false }));
      }, scrollingTimeout);

      setState({
        scrollY: currentY,
        direction: newDirection,
        isScrolled,
        isScrolling: true,
      });
    });
  }, [directionThreshold, scrolledThreshold, scrollingTimeout]);

  useEffect(() => {
    // Set initial state
    lastScrollY.current = window.scrollY;
    setState(prev => ({
      ...prev,
      scrollY: window.scrollY,
      isScrolled: window.scrollY > scrolledThreshold,
    }));

    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => {
      window.removeEventListener('scroll', handleScroll);
      if (rafId.current !== null) {
        cancelAnimationFrame(rafId.current);
      }
      if (scrollingTimerRef.current) {
        clearTimeout(scrollingTimerRef.current);
      }
    };
  }, [handleScroll, scrolledThreshold]);

  return state;
}
