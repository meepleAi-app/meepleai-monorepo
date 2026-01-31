/**
 * Animation timing and easing utilities for consistent motion across the application.
 * Provides duration constants, easing functions, and transition presets.
 *
 * @module animations/transitions
 */

/**
 * Standard animation durations in milliseconds
 * Use these constants to ensure consistent timing across all animations
 */
export const DURATIONS = {
  /** Fast animations (150ms) - for micro-interactions like button hovers */
  fast: 150,
  /** Normal animations (300ms) - default for most UI transitions */
  normal: 300,
  /** Slow animations (500ms) - for page transitions or complex animations */
  slow: 500,
} as const;

/**
 * Type representing valid animation duration values
 */
export type AnimationDuration = typeof DURATIONS[keyof typeof DURATIONS];

/**
 * CSS easing function strings for smooth, natural-feeling animations
 * These follow Material Design motion principles
 */
export const EASINGS = {
  /** Standard easing for most animations - starts fast, ends slow */
  easeOut: 'cubic-bezier(0.0, 0.0, 0.2, 1)',
  /** Easing for elements entering the screen */
  easeIn: 'cubic-bezier(0.4, 0.0, 1, 1)',
  /** Easing for elements that both enter and exit */
  easeInOut: 'cubic-bezier(0.4, 0.0, 0.2, 1)',
  /** Sharp easing for quick, decisive movements */
  sharp: 'cubic-bezier(0.4, 0.0, 0.6, 1)',
} as const;

/**
 * Type representing valid easing function values
 */
export type EasingFunction = typeof EASINGS[keyof typeof EASINGS];

/**
 * Spring animation configuration for Framer Motion
 * Provides natural, physics-based motion
 */
export const SPRING_CONFIGS = {
  /** Gentle spring - smooth and subtle (damping: 25, stiffness: 120) */
  gentle: {
    type: 'spring' as const,
    damping: 25,
    stiffness: 120,
  },
  /** Default spring - balanced motion (damping: 20, stiffness: 150) */
  default: {
    type: 'spring' as const,
    damping: 20,
    stiffness: 150,
  },
  /** Bouncy spring - playful, energetic motion (damping: 15, stiffness: 200) */
  bouncy: {
    type: 'spring' as const,
    damping: 15,
    stiffness: 200,
  },
  /** Stiff spring - quick, responsive motion (damping: 18, stiffness: 300) */
  stiff: {
    type: 'spring' as const,
    damping: 18,
    stiffness: 300,
  },
} as const;

/**
 * Type representing valid spring configuration objects
 */
export type SpringConfig = typeof SPRING_CONFIGS[keyof typeof SPRING_CONFIGS];

/**
 * Pre-configured transition presets combining duration and easing
 * Use these for common animation patterns
 */
export const TRANSITIONS = {
  /** Fade transition - fast, smooth opacity change */
  fade: {
    duration: DURATIONS.fast / 1000, // Convert to seconds for Framer Motion
    ease: [0.0, 0.0, 0.2, 1], // easeOut bezier curve
  },
  /** Slide transition - normal speed with ease-out */
  slide: {
    duration: DURATIONS.normal / 1000,
    ease: [0.0, 0.0, 0.2, 1],
  },
  /** Scale transition - fast, sharp scaling */
  scale: {
    duration: DURATIONS.fast / 1000,
    ease: [0.4, 0.0, 0.6, 1], // sharp bezier curve
  },
  /** Spring transition - default spring configuration */
  spring: SPRING_CONFIGS.default,
  /** Gentle spring transition - smooth, subtle spring */
  springGentle: SPRING_CONFIGS.gentle,
  /** Bouncy spring transition - playful, energetic spring */
  springBouncy: SPRING_CONFIGS.bouncy,
} as const;

/**
 * Type representing valid transition preset objects
 */
export type TransitionPreset = typeof TRANSITIONS[keyof typeof TRANSITIONS];

/**
 * Stagger configuration for animating lists and groups of elements
 * Controls the delay between each child animation
 */
export const STAGGER = {
  /** Fast stagger - 50ms delay between items */
  fast: 0.05,
  /** Normal stagger - 100ms delay between items */
  normal: 0.1,
  /** Slow stagger - 150ms delay between items */
  slow: 0.15,
} as const;

/**
 * Type representing valid stagger delay values
 */
export type StaggerDelay = typeof STAGGER[keyof typeof STAGGER];
