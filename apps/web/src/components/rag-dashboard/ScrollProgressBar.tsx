'use client';

/**
 * ScrollProgressBar Component
 *
 * Horizontal progress bar showing scroll position through the page.
 * Positioned sticky at top of viewport with MeepleAI gradient.
 */

import { motion } from 'framer-motion';

import { cn } from '@/lib/utils';

import { useScrollProgress } from './hooks/useScrollProgress';

export interface ScrollProgressBarProps {
  /** Additional CSS classes */
  className?: string;
}

/**
 * Sticky scroll progress indicator bar.
 * Shows scroll position through the dashboard with gradient animation.
 *
 * @example
 * ```tsx
 * <ScrollProgressBar className="fixed top-0 left-0 right-0 z-50" />
 * ```
 */
export function ScrollProgressBar({ className }: ScrollProgressBarProps) {
  const progress = useScrollProgress();

  return (
    <div
      className={cn('sticky top-0 z-50 h-1 bg-muted/50', className)}
      role="progressbar"
      aria-valuenow={progress}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label="Page scroll progress"
    >
      <motion.div
        className="h-full bg-gradient-to-r from-orange-500 via-primary to-purple-500"
        initial={{ width: 0 }}
        animate={{ width: `${progress}%` }}
        transition={{ duration: 0.1, ease: 'easeOut' }}
      />
    </div>
  );
}

export default ScrollProgressBar;
