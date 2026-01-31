'use client';

/**
 * StaggerChildren Animation Component (Issue #2965 Wave 8)
 *
 * Provides staggered reveal animation for lists and grids.
 * Each child element appears sequentially with a delay.
 *
 * Usage:
 * <StaggerChildren>
 *   <div>Item 1</div>
 *   <div>Item 2</div>
 *   <div>Item 3</div>
 * </StaggerChildren>
 */

import { ReactNode } from 'react';

import { motion, Variants } from 'framer-motion';

export interface StaggerChildrenProps {
  /** Children elements to stagger */
  children: ReactNode;
  /** Delay between each child (seconds) */
  staggerDelay?: number;
  /** Initial delay before first child (seconds) */
  initialDelay?: number;
  /** Animation duration for each child (seconds) */
  duration?: number;
  /** Custom className for container */
  className?: string;
}

export function StaggerChildren({
  children,
  staggerDelay = 0.1,
  initialDelay = 0,
  duration = 0.5,
  className,
}: StaggerChildrenProps) {
  const containerVariants: Variants = {
    hidden: { opacity: 0 },
    visible: {
      opacity: 1,
      transition: {
        delayChildren: initialDelay,
        staggerChildren: staggerDelay,
      },
    },
  };

  const itemVariants: Variants = {
    hidden: {
      opacity: 0,
      y: 20,
    },
    visible: {
      opacity: 1,
      y: 0,
      transition: {
        duration,
        ease: 'easeOut',
      },
    },
  };

  return (
    <motion.div
      initial="hidden"
      animate="visible"
      variants={containerVariants}
      className={className}
    >
      {Array.isArray(children)
        ? children.map((child, index) => (
            <motion.div key={index} variants={itemVariants}>
              {child}
            </motion.div>
          ))
        : children}
    </motion.div>
  );
}
