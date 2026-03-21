/**
 * useResponsive Hook
 * Issue #3287 - Phase 1: Core Layout Structure
 *
 * Provides responsive viewport detection with mobile-first approach.
 * Uses usehooks-ts for media query handling and provides semantic
 * device classifications.
 *
 * Breakpoints:
 * - Mobile: < 640px
 * - Tablet: 640px - 1023px
 * - Desktop: >= 1024px
 *
 * @example
 * ```tsx
 * const { isMobile, isTablet, isDesktop, deviceType } = useResponsive();
 *
 * if (isMobile) {
 *   return <MobileLayout />;
 * }
 * ```
 */

'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';

import { useMediaQuery } from 'usehooks-ts';

import type { ResponsiveState, DeviceType } from '@/types/layout';
import { BREAKPOINTS } from '@/types/layout';

/**
 * Server-side safe initial state
 * Defaults to mobile-first approach
 */
const INITIAL_STATE: ResponsiveState = {
  deviceType: 'mobile',
  isMobile: true,
  isTablet: false,
  isDesktop: false,
  viewportWidth: 0,
};

/**
 * Hook for responsive viewport detection
 *
 * Features:
 * - SSR-safe with hydration handling
 * - Uses CSS media queries for reliability
 * - Provides semantic device classifications
 * - Updates on viewport resize
 *
 * @returns ResponsiveState with current viewport information
 */
export function useResponsive(): ResponsiveState {
  // Use media queries for breakpoint detection
  // These are CSS-based and more reliable than JS resize listeners
  const isSmallScreen = useMediaQuery(`(max-width: ${BREAKPOINTS.sm - 1}px)`);
  const isMediumScreen = useMediaQuery(
    `(min-width: ${BREAKPOINTS.sm}px) and (max-width: ${BREAKPOINTS.lg - 1}px)`
  );
  const isLargeScreen = useMediaQuery(`(min-width: ${BREAKPOINTS.lg}px)`);

  // Track viewport width for fine-grained control
  const [viewportWidth, setViewportWidth] = useState(0);
  const [isHydrated, setIsHydrated] = useState(false);

  // Get viewport width on client
  const updateViewportWidth = useCallback(() => {
    if (typeof window !== 'undefined') {
      setViewportWidth(window.innerWidth);
    }
  }, []);

  // Handle hydration and initial width
  useEffect(() => {
    setIsHydrated(true);
    updateViewportWidth();

    // Add resize listener for viewport width tracking
    const handleResize = () => {
      updateViewportWidth();
    };

    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [updateViewportWidth]);

  // Compute device type from media queries
  const deviceType: DeviceType = useMemo(() => {
    if (!isHydrated) return 'mobile';
    if (isLargeScreen) return 'desktop';
    if (isMediumScreen) return 'tablet';
    return 'mobile';
  }, [isHydrated, isMediumScreen, isLargeScreen]);

  // Return SSR-safe initial state during hydration
  if (!isHydrated) {
    return INITIAL_STATE;
  }

  return {
    deviceType,
    isMobile: isSmallScreen || (!isSmallScreen && !isMediumScreen && !isLargeScreen),
    isTablet: isMediumScreen,
    isDesktop: isLargeScreen,
    viewportWidth,
  };
}

/**
 * Hook to check if current viewport matches or exceeds a specific breakpoint
 *
 * @param breakpoint - Breakpoint to check against
 * @returns boolean - True if viewport >= breakpoint
 *
 * @example
 * ```tsx
 * const isTabletOrLarger = useBreakpoint('md');
 * const isDesktopOrLarger = useBreakpoint('lg');
 * ```
 */
export function useBreakpoint(breakpoint: keyof typeof BREAKPOINTS): boolean {
  const query = `(min-width: ${BREAKPOINTS[breakpoint]}px)`;
  return useMediaQuery(query);
}

/**
 * Hook for checking touch device capability
 * Useful for hover-dependent UI decisions
 *
 * @returns boolean - True if device supports touch
 */
export function useIsTouchDevice(): boolean {
  const [isTouch, setIsTouch] = useState(false);

  useEffect(() => {
    const checkTouch = () => {
      setIsTouch('ontouchstart' in window || navigator.maxTouchPoints > 0);
    };

    checkTouch();
  }, []);

  return isTouch;
}

/**
 * Hook for orientation detection
 *
 * @returns 'portrait' | 'landscape'
 */
export function useOrientation(): 'portrait' | 'landscape' {
  const isPortrait = useMediaQuery('(orientation: portrait)');
  return isPortrait ? 'portrait' : 'landscape';
}

/**
 * Hook for reduced motion preference
 * Important for accessibility compliance
 *
 * @returns boolean - True if user prefers reduced motion
 */
export function usePrefersReducedMotion(): boolean {
  return useMediaQuery('(prefers-reduced-motion: reduce)');
}
