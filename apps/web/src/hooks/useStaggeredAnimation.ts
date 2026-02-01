/**
 * useStaggeredAnimation Hook
 * Issue #3292 - Phase 6: Breadcrumb & Polish
 *
 * Provides staggered animation delays for list items.
 */

'use client';

import { useMemo } from 'react';

import { ANIMATION_TIMING } from '@/types/layout';

/**
 * Stagger animation options
 */
export interface StaggerOptions {
  /** Delay between each item (ms) */
  delay?: number;
  /** Starting index offset */
  startIndex?: number;
  /** Direction of stagger */
  direction?: 'forward' | 'reverse';
  /** Maximum total delay (ms) */
  maxDelay?: number;
}

/**
 * Stagger animation style result
 */
export interface StaggerStyle {
  animationDelay: string;
  animationFillMode: 'backwards';
}

/**
 * useStaggeredAnimation Hook
 *
 * Calculates staggered animation delays for a list of items.
 *
 * @param index - Item index in the list
 * @param options - Animation options
 * @returns Style object with animation delay
 *
 * @example
 * ```tsx
 * function AnimatedList({ items }) {
 *   return (
 *     <ul>
 *       {items.map((item, index) => {
 *         const staggerStyle = useStaggeredAnimation(index);
 *         return (
 *           <li key={item.id} style={staggerStyle} className="animate-in fade-in">
 *             {item.name}
 *           </li>
 *         );
 *       })}
 *     </ul>
 *   );
 * }
 * ```
 */
export function useStaggeredAnimation(
  index: number,
  options: StaggerOptions = {}
): StaggerStyle {
  const {
    delay = ANIMATION_TIMING.stagger,
    startIndex = 0,
    direction = 'forward',
    maxDelay = 500,
  } = options;

  const animationDelay = useMemo(() => {
    const effectiveIndex = direction === 'reverse'
      ? startIndex - index
      : index - startIndex;

    const calculatedDelay = Math.max(0, effectiveIndex * delay);
    const clampedDelay = Math.min(calculatedDelay, maxDelay);

    return `${clampedDelay}ms`;
  }, [index, delay, startIndex, direction, maxDelay]);

  return {
    animationDelay,
    animationFillMode: 'backwards',
  };
}

/**
 * Calculate all stagger delays for a list
 *
 * @param count - Number of items
 * @param options - Animation options
 * @returns Array of delay values in ms
 */
export function calculateStaggerDelays(
  count: number,
  options: StaggerOptions = {}
): number[] {
  const {
    delay = ANIMATION_TIMING.stagger,
    startIndex = 0,
    direction = 'forward',
    maxDelay = 500,
  } = options;

  return Array.from({ length: count }, (_, index) => {
    const effectiveIndex = direction === 'reverse'
      ? startIndex - index
      : index - startIndex;

    const calculatedDelay = Math.max(0, effectiveIndex * delay);
    return Math.min(calculatedDelay, maxDelay);
  });
}

/**
 * Animation configuration for different component types
 */
export const STAGGER_CONFIGS = {
  actionBar: {
    delay: ANIMATION_TIMING.stagger, // 50ms
    direction: 'forward' as const,
    maxDelay: 300,
  },
  menu: {
    delay: 30,
    direction: 'forward' as const,
    maxDelay: 200,
  },
  list: {
    delay: 40,
    direction: 'forward' as const,
    maxDelay: 400,
  },
} as const;
