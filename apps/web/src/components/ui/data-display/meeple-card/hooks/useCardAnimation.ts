'use client';

/**
 * useCardAnimation - Card hover animation state
 *
 * Manages hover state and shimmer trigger for MeepleCard
 * hover transform and shimmer animation effects.
 *
 * @module components/ui/data-display/meeple-card/hooks/useCardAnimation
 */

import { useState, useCallback } from 'react';

export interface UseCardAnimationReturn {
  /** Whether the card is currently hovered */
  isHovered: boolean;
  /** Mouse enter handler */
  onMouseEnter: () => void;
  /** Mouse leave handler */
  onMouseLeave: () => void;
  /** Whether shimmer effect should be active */
  showShimmer: boolean;
}

/**
 * Hook for managing card hover animation state.
 *
 * The shimmer effect is triggered on hover and managed via
 * CSS class toggling. The isHovered state can be used by
 * variant renderers to apply additional hover transforms.
 */
export function useCardAnimation(): UseCardAnimationReturn {
  const [isHovered, setIsHovered] = useState(false);

  const onMouseEnter = useCallback(() => {
    setIsHovered(true);
  }, []);

  const onMouseLeave = useCallback(() => {
    setIsHovered(false);
  }, []);

  return {
    isHovered,
    onMouseEnter,
    onMouseLeave,
    showShimmer: isHovered,
  };
}
