'use client';

/**
 * PageTransition Component (Issue #2965 Wave 8)
 *
 * Provides smooth page transition animations for route changes.
 * Use in layout.tsx or page-specific layouts.
 *
 * Usage:
 * <PageTransition>
 *   {children}
 * </PageTransition>
 */

import { ReactNode } from 'react';

import { motion, Variants } from 'framer-motion';

export interface PageTransitionProps {
  /** Page content */
  children: ReactNode;
  /** Transition type */
  variant?: 'fade' | 'slide' | 'scale';
  /** Custom className */
  className?: string;
}

export function PageTransition({
  children,
  variant = 'fade',
  className,
}: PageTransitionProps) {
  const variants: Record<string, Variants> = {
    fade: {
      hidden: { opacity: 0 },
      visible: {
        opacity: 1,
        transition: { duration: 0.3, ease: 'easeInOut' },
      },
      exit: {
        opacity: 0,
        transition: { duration: 0.2, ease: 'easeInOut' },
      },
    },
    slide: {
      hidden: { opacity: 0, x: -20 },
      visible: {
        opacity: 1,
        x: 0,
        transition: { duration: 0.4, ease: 'easeOut' },
      },
      exit: {
        opacity: 0,
        x: 20,
        transition: { duration: 0.3, ease: 'easeIn' },
      },
    },
    scale: {
      hidden: { opacity: 0, scale: 0.95 },
      visible: {
        opacity: 1,
        scale: 1,
        transition: { duration: 0.3, ease: 'easeOut' },
      },
      exit: {
        opacity: 0,
        scale: 0.95,
        transition: { duration: 0.2, ease: 'easeIn' },
      },
    },
  };

  // eslint-disable-next-line security/detect-object-injection -- Safe: variant is a typed union literal from props
  const selectedVariant = variants[variant];

  return (
    <motion.div
      initial="hidden"
      animate="visible"
      exit="exit"
      variants={selectedVariant}
      className={className}
    >
      {children}
    </motion.div>
  );
}
