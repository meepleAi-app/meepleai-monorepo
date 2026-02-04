'use client';

import { useState, useEffect, useCallback } from 'react';

/**
 * Hook for tracking scroll progress through the page.
 * Uses passive event listeners for optimal performance.
 *
 * @returns Current scroll progress as a percentage (0-100)
 */
export function useScrollProgress(): number {
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

  return progress;
}

export default useScrollProgress;
