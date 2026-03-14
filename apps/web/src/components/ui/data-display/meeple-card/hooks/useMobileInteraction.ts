'use client';

/**
 * useMobileInteraction - Mobile touch UX hook
 *
 * Extracted from meeple-card.tsx (lines ~550-620).
 * Handles mobile viewport detection, two-tap navigation logic,
 * and outside-click dismissal for mobile action sheets.
 *
 * @module components/ui/data-display/meeple-card/hooks/useMobileInteraction
 */

import { useState, useEffect, useRef } from 'react';

export interface UseMobileInteractionOptions {
  /** Whether the card has mobile-relevant actions */
  hasMobileActions: boolean;
  /** Whether the card is flippable (disables tap handling) */
  flippable?: boolean;
  /** Click handler for navigation (2nd tap) */
  onClick?: () => void;
}

export interface UseMobileInteractionReturn {
  /** Whether current viewport is mobile (<= 768px) */
  isMobile: boolean;
  /** Whether mobile actions sheet is visible */
  showMobileActions: boolean;
  /** Set mobile actions visibility */
  setShowMobileActions: (show: boolean) => void;
  /** Mobile tap handler: 1st tap = show actions, 2nd tap = navigate */
  handleMobileClick: () => void;
  /** Ref to attach to the card element for outside-click detection */
  cardRef: React.RefObject<HTMLDivElement | HTMLElement | null>;
}

/**
 * Hook for mobile touch interaction patterns.
 *
 * Two-tap navigation:
 * 1. First tap shows the action bottom sheet
 * 2. Second tap navigates (calls onClick)
 *
 * Outside clicks dismiss the action sheet.
 */
export function useMobileInteraction({
  hasMobileActions,
  flippable,
  onClick,
}: UseMobileInteractionOptions): UseMobileInteractionReturn {
  const [showMobileActions, setShowMobileActions] = useState(false);
  const [isMobile, setIsMobile] = useState(false);
  const cardRef = useRef<HTMLDivElement | HTMLElement>(null);

  // Detect mobile/tablet viewport
  useEffect(() => {
    const mediaQuery = window.matchMedia('(max-width: 768px)');
    setIsMobile(mediaQuery.matches);

    const handler = (e: MediaQueryListEvent) => setIsMobile(e.matches);
    mediaQuery.addEventListener('change', handler);
    return () => mediaQuery.removeEventListener('change', handler);
  }, []);

  // Dismiss mobile actions on outside click
  useEffect(() => {
    if (!showMobileActions) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (cardRef.current && !cardRef.current.contains(event.target as Node)) {
        setShowMobileActions(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [showMobileActions]);

  // Mobile tap handler: 1st tap = show actions, 2nd tap = navigate
  const handleMobileClick = () => {
    // If card is flippable, don't handle tap (let FlipCard handle it)
    if (flippable) return;

    if (hasMobileActions && !showMobileActions) {
      // 1st tap: Show bottom sheet
      setShowMobileActions(true);
    } else if (onClick) {
      // 2nd tap OR no actions: Navigate
      setShowMobileActions(false);
      onClick();
    }
  };

  return {
    isMobile,
    showMobileActions,
    setShowMobileActions,
    handleMobileClick,
    cardRef,
  };
}
