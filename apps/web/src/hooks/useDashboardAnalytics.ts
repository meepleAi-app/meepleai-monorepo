/**
 * useDashboardAnalytics - Dashboard Business Metrics Tracking Hook
 * Issue #3982 - Dashboard Business Metrics Tracking & Validation
 *
 * Tracks three business success criteria from Epic #3901:
 * 1. Click-through rate: dashboard → library navigation events
 * 2. Time on dashboard: session duration on /dashboard page
 * 3. Mobile bounce rate: mobile users (<640px) who leave without interaction
 *
 * @example
 * ```tsx
 * function DashboardHub() {
 *   const { trackClickThrough } = useDashboardAnalytics();
 *   return <Link onClick={() => trackClickThrough('/library', 'library_snapshot')}>...</Link>;
 * }
 * ```
 */

'use client';

import { useEffect, useRef, useCallback } from 'react';

import { trackEvent, trackTiming } from '@/lib/analytics';

// ============================================================================
// Constants
// ============================================================================

const MOBILE_BREAKPOINT = 640;
const BOUNCE_TIMEOUT_MS = 10_000;

// ============================================================================
// Hook
// ============================================================================

export function useDashboardAnalytics() {
  const startTimeRef = useRef<number>(Date.now());
  const hasInteractedRef = useRef(false);
  const isMobileRef = useRef(false);

  // Track page view + setup mobile detection + bounce timer
  useEffect(() => {
    const isMobile = typeof window !== 'undefined' && window.innerWidth < MOBILE_BREAKPOINT;
    isMobileRef.current = isMobile;
    const capturedStartTime = startTimeRef.current;

    // Track page view
    trackEvent('dashboard_view', {
      source: 'dashboard_hub',
      is_mobile: isMobile,
      viewport_width: typeof window !== 'undefined' ? window.innerWidth : 0,
    });

    // Track mobile-specific view
    if (isMobile) {
      trackEvent('dashboard_mobile_view', {
        viewport_width: window.innerWidth,
      });
    }

    // Interaction listener to mark user as engaged
    const markInteraction = () => {
      if (!hasInteractedRef.current) {
        hasInteractedRef.current = true;
        trackEvent('dashboard_interaction', {
          is_mobile: isMobileRef.current,
          time_to_interact_ms: Date.now() - capturedStartTime,
        });
      }
    };

    window.addEventListener('click', markInteraction);
    window.addEventListener('scroll', markInteraction);
    window.addEventListener('keydown', markInteraction);

    // Mobile bounce detection: exit without interaction within 10s
    let bounceTimer: ReturnType<typeof setTimeout> | undefined;
    if (isMobile) {
      bounceTimer = setTimeout(() => {
        if (!hasInteractedRef.current) {
          trackEvent('dashboard_mobile_bounce', {
            viewport_width: window.innerWidth,
            timeout_ms: BOUNCE_TIMEOUT_MS,
          });
        }
      }, BOUNCE_TIMEOUT_MS);
    }

    // Track time on page when leaving
    const handleBeforeUnload = () => {
      const duration = Date.now() - capturedStartTime;
      trackTiming('Dashboard', 'time_on_page', duration);
    };

    window.addEventListener('beforeunload', handleBeforeUnload);

    return () => {
      // Track time on page on unmount (SPA navigation)
      const duration = Date.now() - capturedStartTime;
      trackTiming('Dashboard', 'time_on_page', duration);

      window.removeEventListener('click', markInteraction);
      window.removeEventListener('scroll', markInteraction);
      window.removeEventListener('keydown', markInteraction);
      window.removeEventListener('beforeunload', handleBeforeUnload);

      if (bounceTimer) {
        clearTimeout(bounceTimer);
      }
    };
  }, []);

  /**
   * Track click-through from dashboard to another section.
   * Use this on navigation links to measure click-through rate.
   */
  const trackClickThrough = useCallback(
    (destination: string, widgetSource: string) => {
      hasInteractedRef.current = true;
      trackEvent('dashboard_click_through', {
        destination,
        source: widgetSource,
        is_mobile: isMobileRef.current,
        time_on_page_ms: Date.now() - startTimeRef.current,
      });
    },
    []
  );

  return { trackClickThrough };
}
