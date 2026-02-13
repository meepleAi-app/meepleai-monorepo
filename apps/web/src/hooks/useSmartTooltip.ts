/**
 * Smart Tooltip Hook
 * Epic #4068 - Issue #4186
 *
 * Automatic positioning with viewport detection and performance optimization
 */

'use client';

import { useState, useEffect, useRef, useCallback, useMemo } from 'react';

import { calculateOptimalPosition, debounce, type TooltipPosition } from '@/lib/tooltip/positioning';

export interface UseSmartTooltipOptions {
  /** Enable smart positioning (default: true) */
  enabled?: boolean;
  /** Debounce delay for scroll/resize (default: 100ms) */
  debounceMs?: number;
  /** Reposition on scroll (default: true) */
  repositionOnScroll?: boolean;
  /** Reposition on resize (default: true) */
  repositionOnResize?: boolean;
}

export interface UseSmartTooltipReturn {
  /** Calculated position */
  position: TooltipPosition | null;
  /** Whether tooltip is visible */
  isVisible: boolean;
  /** Set visibility */
  setIsVisible: (visible: boolean) => void;
  /** Ref for trigger element */
  triggerRef: React.RefObject<HTMLElement | null>;
  /** Ref for tooltip element */
  tooltipRef: React.RefObject<HTMLDivElement | null>;
  /** Manually trigger position update */
  updatePosition: () => void;
}

/**
 * useSmartTooltip provides automatic positioning with:
 * - Viewport boundary detection
 * - Auto-flip when insufficient space
 * - Debounced scroll/resize handling
 * - Performance optimization (<16ms target)
 *
 * @example
 * const { position, triggerRef, tooltipRef, isVisible, setIsVisible } = useSmartTooltip();
 *
 * <button ref={triggerRef} onClick={() => setIsVisible(true)}>
 *   Trigger
 * </button>
 * {isVisible && (
 *   <div ref={tooltipRef} style={position}>
 *     Tooltip content
 *   </div>
 * )}
 */
export function useSmartTooltip(options: UseSmartTooltipOptions = {}): UseSmartTooltipReturn {
  const {
    enabled = true,
    debounceMs = 100,
    repositionOnScroll = true,
    repositionOnResize = true
  } = options;

  const [position, setPosition] = useState<TooltipPosition | null>(null);
  const [isVisible, setIsVisible] = useState(false);
  const triggerRef = useRef<HTMLElement>(null);
  const tooltipRef = useRef<HTMLDivElement>(null);

  const updatePosition = useCallback(() => {
    if (!enabled || !triggerRef.current || !tooltipRef.current || !isVisible) {
      return;
    }

    const startTime = performance.now();

    const triggerRect = triggerRef.current.getBoundingClientRect();
    const tooltipRect = tooltipRef.current.getBoundingClientRect();
    const viewport = {
      width: window.innerWidth,
      height: window.innerHeight
    };

    const newPosition = calculateOptimalPosition(
      triggerRect,
      { width: tooltipRect.width, height: tooltipRect.height },
      viewport
    );

    const elapsed = performance.now() - startTime;

    // Performance monitoring (Epic #4068 - AC4: <16ms target)
    if (elapsed > 16) {
      console.warn(`Tooltip positioning slow: ${elapsed.toFixed(2)}ms (target: <16ms)`);
    }

    setPosition(newPosition);
  }, [enabled, isVisible]);

  const debouncedUpdate = useMemo(
    () => debounce(updatePosition, debounceMs),
    [updatePosition, debounceMs]
  );

  // Initial position calculation when tooltip becomes visible
  useEffect(() => {
    if (isVisible) {
      // Use requestAnimationFrame for smooth positioning
      requestAnimationFrame(updatePosition);
    }
  }, [isVisible, updatePosition]);

  // Scroll listener (debounced for performance)
  useEffect(() => {
    if (!repositionOnScroll || !isVisible) return;

    window.addEventListener('scroll', debouncedUpdate, { passive: true });
    return () => window.removeEventListener('scroll', debouncedUpdate);
  }, [repositionOnScroll, isVisible, debouncedUpdate]);

  // Resize listener (debounced)
  useEffect(() => {
    if (!repositionOnResize || !isVisible) return;

    window.addEventListener('resize', debouncedUpdate);
    return () => window.removeEventListener('resize', debouncedUpdate);
  }, [repositionOnResize, isVisible, debouncedUpdate]);

  // IntersectionObserver for visibility tracking (performance optimization)
  useEffect(() => {
    if (!enabled || !triggerRef.current) return;

    const observer = new IntersectionObserver(
      (entries) => {
        const [entry] = entries;
        // Hide tooltip if trigger goes off-screen
        if (!entry.isIntersecting && isVisible) {
          setIsVisible(false);
        }
      },
      { threshold: 0 }
    );

    observer.observe(triggerRef.current);
    return () => observer.disconnect();
  }, [enabled, isVisible]);

  return {
    position,
    isVisible,
    setIsVisible,
    triggerRef,
    tooltipRef,
    updatePosition
  };
}
