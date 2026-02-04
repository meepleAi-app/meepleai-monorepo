/**
 * useLazyWidget - Hook for lazy loading dashboard widgets
 * Issue #3323 - Responsive polish and performance optimization
 *
 * Features:
 * - IntersectionObserver for viewport detection
 * - Configurable threshold and rootMargin
 * - SSR-safe implementation
 * - Loading state management
 *
 * @example
 * ```tsx
 * const { ref, isVisible, hasLoaded } = useLazyWidget();
 *
 * return (
 *   <div ref={ref}>
 *     {hasLoaded ? <ExpensiveWidget /> : <WidgetSkeleton />}
 *   </div>
 * );
 * ```
 */

import { useRef, useState, useEffect, useCallback } from 'react';

// ============================================================================
// Types
// ============================================================================

export interface UseLazyWidgetOptions {
  /** Intersection threshold (0-1) */
  threshold?: number;
  /** Root margin for intersection */
  rootMargin?: string;
  /** Once visible, stay visible (don't unload) */
  once?: boolean;
  /** Disable lazy loading (always visible) */
  disabled?: boolean;
}

export interface UseLazyWidgetReturn<T extends HTMLElement = HTMLDivElement> {
  /** Ref to attach to the container element */
  ref: React.RefObject<T | null>;
  /** Whether element is currently in viewport */
  isVisible: boolean;
  /** Whether element has ever been visible (for "once" mode) */
  hasLoaded: boolean;
  /** Manually trigger load */
  triggerLoad: () => void;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useLazyWidget<T extends HTMLElement = HTMLDivElement>(
  options: UseLazyWidgetOptions = {}
): UseLazyWidgetReturn<T> {
  const {
    threshold = 0,
    rootMargin = '100px',
    once = true,
    disabled = false,
  } = options;

  const ref = useRef<T>(null);
  const [isVisible, setIsVisible] = useState(disabled);
  const [hasLoaded, setHasLoaded] = useState(disabled);

  // Manual load trigger
  const triggerLoad = useCallback(() => {
    setIsVisible(true);
    setHasLoaded(true);
  }, []);

  useEffect(() => {
    // Skip if disabled or already loaded in "once" mode
    if (disabled || (once && hasLoaded)) {
      return;
    }

    // SSR check
    if (typeof window === 'undefined' || !window.IntersectionObserver) {
      // Fallback: immediately visible
      setIsVisible(true);
      setHasLoaded(true);
      return;
    }

    const element = ref.current;
    if (!element) {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        const [entry] = entries;
        const isIntersecting = entry?.isIntersecting ?? false;

        setIsVisible(isIntersecting);

        if (isIntersecting) {
          setHasLoaded(true);

          // Unobserve if "once" mode
          if (once) {
            observer.unobserve(element);
          }
        }
      },
      {
        threshold,
        rootMargin,
      }
    );

    observer.observe(element);

    return () => {
      observer.disconnect();
    };
  }, [threshold, rootMargin, once, disabled, hasLoaded]);

  return {
    ref,
    isVisible,
    hasLoaded,
    triggerLoad,
  };
}

export default useLazyWidget;
