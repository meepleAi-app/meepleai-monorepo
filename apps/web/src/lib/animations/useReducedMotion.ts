/**
 * React hook to detect user's motion preferences for accessibility.
 * Respects the prefers-reduced-motion media query and updates dynamically.
 *
 * @module animations/useReducedMotion
 */

import { useEffect, useState } from 'react';

/**
 * Media query string for detecting reduced motion preference
 */
const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

/**
 * Custom React hook to detect if the user prefers reduced motion.
 * Listens for changes to the prefers-reduced-motion media query and updates accordingly.
 *
 * This hook respects user accessibility preferences and should be used to disable
 * or simplify animations when the user has requested reduced motion in their OS settings.
 *
 * @returns {boolean} true if the user prefers reduced motion, false otherwise
 *
 * @example
 * Basic usage with conditional animation:
 * ```tsx
 * function MyComponent() {
 *   const shouldReduceMotion = useReducedMotion();
 *
 *   return (
 *     <motion.div
 *       animate={{ opacity: 1 }}
 *       transition={shouldReduceMotion ? { duration: 0 } : { duration: 0.3 }}
 *     >
 *       Content
 *     </motion.div>
 *   );
 * }
 * ```
 *
 * @example
 * Usage with variant selection:
 * ```tsx
 * function AnimatedList() {
 *   const shouldReduceMotion = useReducedMotion();
 *
 *   return (
 *     <motion.ul
 *       variants={shouldReduceMotion ? VARIANTS.fadeIn : VARIANTS.slideUp}
 *       initial="initial"
 *       animate="animate"
 *     >
 *       {items.map(item => <li key={item.id}>{item.name}</li>)}
 *     </motion.ul>
 *   );
 * }
 * ```
 *
 * @example
 * Usage with completely disabled animations:
 * ```tsx
 * function LoadingSpinner() {
 *   const shouldReduceMotion = useReducedMotion();
 *
 *   if (shouldReduceMotion) {
 *     return <div>Loading...</div>; // Simple text instead of spinner
 *   }
 *
 *   return <motion.div animate={{ rotate: 360 }} transition={{ repeat: Infinity }} />;
 * }
 * ```
 */
export function useReducedMotion(): boolean {
  // Initialize state with current media query value
  // Use a function to avoid running matchMedia on every render
  const [prefersReducedMotion, setPrefersReducedMotion] = useState<boolean>(() => {
    // Server-side rendering guard
    if (typeof window === 'undefined') {
      return false;
    }

    // Check initial media query state
    const mediaQuery = window.matchMedia(REDUCED_MOTION_QUERY);
    return mediaQuery.matches;
  });

  useEffect(() => {
    // Early return for server-side rendering
    if (typeof window === 'undefined') {
      return;
    }

    // Create media query list object
    const mediaQuery = window.matchMedia(REDUCED_MOTION_QUERY);

    // Handler for media query changes
    const handleChange = (event: MediaQueryListEvent | MediaQueryList) => {
      setPrefersReducedMotion(event.matches);
    };

    // Set initial state (in case it changed between initial render and effect)
    handleChange(mediaQuery);

    // Modern browsers support addEventListener on MediaQueryList
    // Fallback to addListener for older browsers
    if (mediaQuery.addEventListener) {
      mediaQuery.addEventListener('change', handleChange);
    } else {
      // Legacy API - deprecated but still needed for some browsers
      mediaQuery.addListener(handleChange);
    }

    // Cleanup function to remove event listener
    return () => {
      if (mediaQuery.removeEventListener) {
        mediaQuery.removeEventListener('change', handleChange);
      } else {
        // Legacy API cleanup
        mediaQuery.removeListener(handleChange);
      }
    };
  }, []); // Empty dependency array - only run on mount/unmount

  return prefersReducedMotion;
}

/**
 * Type representing the return value of useReducedMotion hook
 */
export type UseReducedMotionReturn = boolean;
