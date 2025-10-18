/**
 * MessageAnimator Component
 *
 * Wrapper component for animating chat messages with direction-specific slide-in effects.
 * - 'left': AI messages sliding from left (x: -20)
 * - 'right': User messages sliding from right (x: 20)
 *
 * Respects user's reduced motion preferences with simplified variants.
 *
 * @example
 * ```tsx
 * // AI message (from left)
 * <MessageAnimator direction="left" id="msg-1">
 *   <div className="message ai">Hello!</div>
 * </MessageAnimator>
 *
 * // User message (from right)
 * <MessageAnimator direction="right" id="msg-2" delay={0.1}>
 *   <div className="message user">Hi there!</div>
 * </MessageAnimator>
 * ```
 */

import { ReactNode, useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { useReducedMotion } from '@/lib/animations';

export interface MessageAnimatorProps {
  /**
   * Child elements to animate
   */
  children: ReactNode;

  /**
   * Direction of slide animation
   * - 'left': AI messages (slide from left, x: -20)
   * - 'right': User messages (slide from right, x: 20)
   */
  direction: 'left' | 'right';

  /**
   * Animation delay in seconds
   * @default 0
   */
  delay?: number;

  /**
   * Unique message identifier
   */
  id: string;
}

/**
 * MessageAnimator component for chat message animations
 */
export function MessageAnimator({
  children,
  direction,
  delay = 0,
  id,
}: MessageAnimatorProps) {
  const shouldReduceMotion = useReducedMotion();
  const [animationComplete, setAnimationComplete] = useState(false);

  // Determine initial x position based on direction
  const initialX = direction === 'left' ? -20 : 20;

  // Create variants based on reduced motion preference
  const variants = shouldReduceMotion
    ? {
        // Simplified variants for reduced motion
        initial: { opacity: 1, x: 0 },
        animate: { opacity: 1, x: 0 },
      }
    : {
        // Full animation variants
        initial: { opacity: 0, x: initialX },
        animate: {
          opacity: 1,
          x: 0,
        },
      };

  // Transition configuration
  const transition = shouldReduceMotion
    ? { duration: 0 }
    : {
        type: 'spring' as const,
        stiffness: 500,
        damping: 30,
        delay,
      };

  // Set animation complete after delay + animation duration
  useEffect(() => {
    if (!shouldReduceMotion) {
      const animationDuration = 300; // Spring animation approx duration
      const totalDelay = (delay * 1000) + animationDuration;
      const timer = setTimeout(() => {
        setAnimationComplete(true);
      }, totalDelay);
      return () => clearTimeout(timer);
    } else {
      // Immediate completion for reduced motion
      setAnimationComplete(true);
    }
  }, [delay, shouldReduceMotion]);

  return (
    <motion.div
      variants={variants}
      initial="initial"
      animate="animate"
      transition={transition}
      data-message-id={id}
      data-animation-complete={animationComplete.toString()}
    >
      {children}
    </motion.div>
  );
}
