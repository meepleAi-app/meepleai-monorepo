/**
 * useDebouncedHover Hook (Issue #4081)
 */

'use client';

import { useState, useCallback, useRef } from 'react';

export function useDebouncedHover(delay: number = 100) {
  const [isHovered, setIsHovered] = useState(false);
  const timeoutRef = useRef<NodeJS.Timeout>();

  const handleMouseEnter = useCallback(() => {
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    timeoutRef.current = setTimeout(() => setIsHovered(true), delay);
  }, [delay]);

  const handleMouseLeave = useCallback(() => {
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    setIsHovered(false);
  }, []);

  return { isHovered, hoverProps: { onMouseEnter: handleMouseEnter, onMouseLeave: handleMouseLeave } };
}
