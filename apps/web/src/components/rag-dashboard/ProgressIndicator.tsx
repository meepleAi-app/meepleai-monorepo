'use client';

import { useState, useEffect, useCallback } from 'react';
import { motion } from 'framer-motion';
import { cn } from '@/lib/utils';

export interface ProgressIndicatorProps {
  /** Additional CSS classes */
  className?: string;
}

/**
 * Visual progress indicator showing scroll progress through dashboard content.
 * Uses passive event listeners for optimal performance.
 *
 * @example
 * ```tsx
 * <ProgressIndicator className="px-4 py-3" />
 * ```
 */
export function ProgressIndicator({ className }: ProgressIndicatorProps) {
  const [progress, setProgress] = useState(0);

  const calculateProgress = useCallback(() => {
    if (typeof window === 'undefined') return;

    const windowHeight = window.innerHeight;
    const documentHeight = document.documentElement.scrollHeight - windowHeight;

    // Handle edge case where document is shorter than viewport
    if (documentHeight <= 0) {
      setProgress(100);
      return;
    }

    const scrollTop = window.scrollY;
    const percentage = Math.round((scrollTop / documentHeight) * 100);
    setProgress(Math.min(100, Math.max(0, percentage)));
  }, []);

  useEffect(() => {
    if (typeof window === 'undefined') return;

    // Calculate initial progress
    calculateProgress();

    // Use passive listeners for better scroll performance
    window.addEventListener('scroll', calculateProgress, { passive: true });
    window.addEventListener('resize', calculateProgress, { passive: true });

    return () => {
      window.removeEventListener('scroll', calculateProgress);
      window.removeEventListener('resize', calculateProgress);
    };
  }, [calculateProgress]);

  return (
    <div
      className={cn('border-t border-border px-4 py-3', className)}
      role="progressbar"
      aria-valuenow={progress}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label="Page scroll progress"
    >
      <div className="mb-1 flex items-center justify-between text-xs text-muted-foreground">
        <span>Progress</span>
        <span>{progress}%</span>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-muted">
        <motion.div
          className="h-full rounded-full bg-primary"
          initial={{ width: 0 }}
          animate={{ width: `${progress}%` }}
          transition={{ duration: 0.1 }}
        />
      </div>
    </div>
  );
}

export default ProgressIndicator;
