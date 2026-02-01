/**
 * useMorphTransition Hook
 * Issue #3292 - Phase 6: Breadcrumb & Polish
 *
 * Provides smooth morph transitions between component states.
 */

'use client';

import { useMemo } from 'react';

import { ANIMATION_TIMING } from '@/types/layout';
import { usePrefersReducedMotion } from '@/hooks/useResponsive';

/**
 * Morph transition configuration
 */
export interface MorphConfig {
  /** Transition duration (ms) */
  duration?: number;
  /** Easing function */
  easing?: string;
  /** Properties to transition */
  properties?: string[];
}

/**
 * Morph style result
 */
export interface MorphStyle {
  transition: string;
  willChange: string;
}

/**
 * Pre-configured morph configurations
 */
export const MORPH_CONFIGS = {
  /** FAB icon morph */
  fab: {
    duration: ANIMATION_TIMING.base, // 200ms
    easing: 'cubic-bezier(0.4, 0, 0.2, 1)',
    properties: ['transform', 'background-color'],
  },

  /** ActionBar item transitions */
  actionBar: {
    duration: ANIMATION_TIMING.fast, // 150ms
    easing: 'cubic-bezier(0.4, 0, 0.2, 1)',
    properties: ['opacity', 'transform'],
  },

  /** Breadcrumb fade/slide */
  breadcrumb: {
    duration: ANIMATION_TIMING.fast, // 150ms
    easing: 'ease-out',
    properties: ['opacity', 'transform'],
  },

  /** Menu open/close */
  menu: {
    duration: ANIMATION_TIMING.fast, // 150ms
    easing: 'cubic-bezier(0.4, 0, 0.2, 1)',
    properties: ['opacity', 'transform', 'scale'],
  },

  /** Default fallback */
  default: {
    duration: ANIMATION_TIMING.base, // 200ms
    easing: 'cubic-bezier(0.4, 0, 0.2, 1)',
    properties: ['all'],
  },
} as const;

export type MorphType = keyof typeof MORPH_CONFIGS;

/**
 * useMorphTransition Hook
 *
 * Provides CSS transition styles for morph animations.
 * Respects reduced motion preferences.
 *
 * @param type - Morph configuration type or custom config
 * @returns Style object for transitions
 *
 * @example
 * ```tsx
 * function FABIcon({ icon }) {
 *   const morphStyle = useMorphTransition('fab');
 *   return (
 *     <div style={morphStyle}>
 *       <Icon name={icon} />
 *     </div>
 *   );
 * }
 * ```
 */
export function useMorphTransition(
  type: MorphType | MorphConfig = 'default'
): MorphStyle {
  const prefersReducedMotion = usePrefersReducedMotion();

  const morphStyle = useMemo(() => {
    // Get configuration
    const config = typeof type === 'string' ? MORPH_CONFIGS[type] : type;
    const {
      duration = ANIMATION_TIMING.base,
      easing = 'cubic-bezier(0.4, 0, 0.2, 1)',
      properties = ['all'],
    } = config;

    // Respect reduced motion preference
    if (prefersReducedMotion) {
      return {
        transition: 'none',
        willChange: 'auto',
      };
    }

    // Build transition string
    const transitionValue = properties
      .map(prop => `${prop} ${duration}ms ${easing}`)
      .join(', ');

    return {
      transition: transitionValue,
      willChange: properties.join(', '),
    };
  }, [type, prefersReducedMotion]);

  return morphStyle;
}

/**
 * Get morph CSS class for Tailwind transitions
 *
 * @param type - Morph type
 * @returns Tailwind class string
 */
export function getMorphClass(type: MorphType = 'default'): string {
  const config = MORPH_CONFIGS[type];
  const duration = Math.round(config.duration);

  // Map to Tailwind duration classes
  if (duration <= 150) return 'transition-all duration-150';
  if (duration <= 200) return 'transition-all duration-200';
  if (duration <= 300) return 'transition-all duration-300';
  return 'transition-all duration-500';
}
