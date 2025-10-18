/**
 * Framer Motion animation variant presets for common UI animation patterns.
 * Each variant includes initial, animate, and exit states for complete animation lifecycles.
 *
 * @module animations/variants
 */

import type { Variants } from 'framer-motion';
import { TRANSITIONS, STAGGER } from './transitions';

/**
 * Fade in/out animation variant
 * Smoothly transitions opacity from 0 to 1
 *
 * @example
 * ```tsx
 * <motion.div variants={VARIANTS.fadeIn} initial="initial" animate="animate" exit="exit">
 *   Content
 * </motion.div>
 * ```
 */
export const fadeIn: Variants = {
  initial: {
    opacity: 0,
  },
  animate: {
    opacity: 1,
    transition: TRANSITIONS.fade,
  },
  exit: {
    opacity: 0,
    transition: TRANSITIONS.fade,
  },
};

/**
 * Slide up animation variant
 * Slides content from below while fading in
 *
 * @example
 * ```tsx
 * <motion.div variants={VARIANTS.slideUp}>
 *   Content slides up from below
 * </motion.div>
 * ```
 */
export const slideUp: Variants = {
  initial: {
    opacity: 0,
    y: 20,
  },
  animate: {
    opacity: 1,
    y: 0,
    transition: TRANSITIONS.slide,
  },
  exit: {
    opacity: 0,
    y: -10,
    transition: TRANSITIONS.fade,
  },
};

/**
 * Slide down animation variant
 * Slides content from above while fading in
 *
 * @example
 * ```tsx
 * <motion.div variants={VARIANTS.slideDown}>
 *   Content slides down from above
 * </motion.div>
 * ```
 */
export const slideDown: Variants = {
  initial: {
    opacity: 0,
    y: -20,
  },
  animate: {
    opacity: 1,
    y: 0,
    transition: TRANSITIONS.slide,
  },
  exit: {
    opacity: 0,
    y: 10,
    transition: TRANSITIONS.fade,
  },
};

/**
 * Slide left animation variant
 * Slides content from the right while fading in
 *
 * @example
 * ```tsx
 * <motion.div variants={VARIANTS.slideLeft}>
 *   Content slides in from right
 * </motion.div>
 * ```
 */
export const slideLeft: Variants = {
  initial: {
    opacity: 0,
    x: 20,
  },
  animate: {
    opacity: 1,
    x: 0,
    transition: TRANSITIONS.slide,
  },
  exit: {
    opacity: 0,
    x: -10,
    transition: TRANSITIONS.fade,
  },
};

/**
 * Slide right animation variant
 * Slides content from the left while fading in
 *
 * @example
 * ```tsx
 * <motion.div variants={VARIANTS.slideRight}>
 *   Content slides in from left
 * </motion.div>
 * ```
 */
export const slideRight: Variants = {
  initial: {
    opacity: 0,
    x: -20,
  },
  animate: {
    opacity: 1,
    x: 0,
    transition: TRANSITIONS.slide,
  },
  exit: {
    opacity: 0,
    x: 10,
    transition: TRANSITIONS.fade,
  },
};

/**
 * Scale in animation variant
 * Scales content from 95% to 100% while fading in
 *
 * @example
 * ```tsx
 * <motion.div variants={VARIANTS.scaleIn}>
 *   Content scales in
 * </motion.div>
 * ```
 */
export const scaleIn: Variants = {
  initial: {
    opacity: 0,
    scale: 0.95,
  },
  animate: {
    opacity: 1,
    scale: 1,
    transition: TRANSITIONS.scale,
  },
  exit: {
    opacity: 0,
    scale: 0.95,
    transition: TRANSITIONS.fade,
  },
};

/**
 * Pop in animation variant
 * Bouncy spring scale animation for attention-grabbing elements
 *
 * @example
 * ```tsx
 * <motion.div variants={VARIANTS.popIn}>
 *   Content pops in with spring
 * </motion.div>
 * ```
 */
export const popIn: Variants = {
  initial: {
    opacity: 0,
    scale: 0.8,
  },
  animate: {
    opacity: 1,
    scale: 1,
    transition: TRANSITIONS.springBouncy,
  },
  exit: {
    opacity: 0,
    scale: 0.9,
    transition: TRANSITIONS.fade,
  },
};

/**
 * Stagger container variant for animating lists
 * Use this on the parent container with stagger children
 *
 * @param delayChildren - Delay before starting children animations (default: 0)
 * @param staggerChildren - Delay between each child animation (default: STAGGER.normal)
 *
 * @example
 * ```tsx
 * <motion.ul variants={createStaggerContainer()} initial="initial" animate="animate">
 *   {items.map(item => (
 *     <motion.li key={item.id} variants={VARIANTS.fadeIn}>
 *       {item.name}
 *     </motion.li>
 *   ))}
 * </motion.ul>
 * ```
 */
export const createStaggerContainer = (
  delayChildren: number = 0,
  staggerChildren: number = STAGGER.normal
): Variants => ({
  initial: {},
  animate: {
    transition: {
      delayChildren,
      staggerChildren,
    },
  },
  exit: {
    transition: {
      staggerChildren: staggerChildren / 2,
      staggerDirection: -1,
    },
  },
});

/**
 * Default stagger container with normal timing
 * Pre-configured version of createStaggerContainer for common use cases
 */
export const staggerContainer: Variants = createStaggerContainer();

/**
 * Fast stagger container for quick list animations
 */
export const staggerContainerFast: Variants = createStaggerContainer(0, STAGGER.fast);

/**
 * Slow stagger container for dramatic list animations
 */
export const staggerContainerSlow: Variants = createStaggerContainer(0, STAGGER.slow);

/**
 * Collection of all animation variants
 * Provides a single export point for all variant presets
 */
export const VARIANTS = {
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
} as const;

/**
 * Type representing any of the predefined animation variants
 */
export type VariantPreset = Variants;
