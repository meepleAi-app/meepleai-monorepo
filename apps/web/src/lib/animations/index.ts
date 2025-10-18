/**
 * Animation utilities library for consistent motion design across the application.
 *
 * This module provides a comprehensive set of animation primitives including:
 * - Duration constants and easing functions
 * - Framer Motion variant presets for common patterns
 * - React hook for detecting reduced motion preferences
 *
 * All animations respect the user's accessibility preferences via the
 * prefers-reduced-motion media query.
 *
 * @module animations
 *
 * @example
 * Basic usage with all utilities:
 * ```tsx
 * import { useReducedMotion, VARIANTS, TRANSITIONS } from '@/lib/animations';
 *
 * function MyComponent() {
 *   const shouldReduceMotion = useReducedMotion();
 *
 *   return (
 *     <motion.div
 *       variants={shouldReduceMotion ? VARIANTS.fadeIn : VARIANTS.slideUp}
 *       initial="initial"
 *       animate="animate"
 *       transition={TRANSITIONS.spring}
 *     >
 *       Content
 *     </motion.div>
 *   );
 * }
 * ```
 */

// Export transition utilities
export {
  DURATIONS,
  EASINGS,
  SPRING_CONFIGS,
  TRANSITIONS,
  STAGGER,
  type AnimationDuration,
  type EasingFunction,
  type SpringConfig,
  type TransitionPreset,
  type StaggerDelay,
} from './transitions';

// Export animation variants
export {
  VARIANTS,
  fadeIn,
  slideUp,
  slideDown,
  slideLeft,
  slideRight,
  scaleIn,
  popIn,
  staggerContainer,
  staggerContainerFast,
  staggerContainerSlow,
  createStaggerContainer,
  type VariantPreset,
} from './variants';

// Export React hooks
export {
  useReducedMotion,
  type UseReducedMotionReturn,
} from './useReducedMotion';
