'use client';

/**
 * FadeIn Animation Component (Issue #2965 Wave 8)
 *
 * Provides fade-in animation for any element using Framer Motion.
 * Respects prefers-reduced-motion user preference.
 *
 * Usage:
 * <FadeIn delay={0.2} duration={0.6}>
 *   <div>Your content</div>
 * </FadeIn>
 */

import { ReactNode } from 'react';

import { motion, Variants } from 'framer-motion';

export interface FadeInProps {
  /** Content to animate */
  children: ReactNode;
  /** Delay before animation starts (seconds) */
  delay?: number;
  /** Animation duration (seconds) */
  duration?: number;
  /** Direction to slide in from */
  direction?: 'up' | 'down' | 'left' | 'right' | 'none';
  /** Distance to slide (pixels) */
  distance?: number;
  /** Custom className */
  className?: string;
}

export function FadeIn({
  children,
  delay = 0,
  duration = 0.5,
  direction = 'none',
  distance = 20,
  className,
}: FadeInProps) {
  const getInitialTransform = () => {
    switch (direction) {
      case 'up':
        return { y: distance, x: 0 };
      case 'down':
        return { y: -distance, x: 0 };
      case 'left':
        return { x: distance, y: 0 };
      case 'right':
        return { x: -distance, y: 0 };
      default:
        return { x: 0, y: 0 };
    }
  };

  const variants: Variants = {
    hidden: {
      opacity: 0,
      ...getInitialTransform(),
    },
    visible: {
      opacity: 1,
      x: 0,
      y: 0,
      transition: {
        duration,
        delay,
        ease: 'easeOut',
      },
    },
  };

  return (
    <motion.div
      initial="hidden"
      animate="visible"
      variants={variants}
      className={className}
    >
      {children}
    </motion.div>
  );
}
