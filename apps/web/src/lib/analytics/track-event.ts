/**
 * Analytics Event Tracking Utility
 * Issue #3913 - Quick Actions Grid Enhancement
 *
 * Provides centralized event tracking for user interactions.
 * Ready for integration with analytics platforms (PostHog, Google Analytics 4, etc.)
 *
 * @example
 * ```tsx
 * import { trackEvent } from '@/lib/analytics/track-event';
 *
 * // Track simple event
 * trackEvent('button_clicked');
 *
 * // Track with properties
 * trackEvent('dashboard_quick_action_library', {
 *   source: 'dashboard',
 *   timestamp: new Date().toISOString(),
 * });
 * ```
 */

/**
 * Track a user interaction event
 *
 * @param eventName - Event identifier (e.g., 'dashboard_quick_action_library')
 * @param properties - Optional event metadata
 */
export function trackEvent(eventName: string, properties?: Record<string, unknown>): void {
  // Development mode: log to console
  if (process.env.NODE_ENV === 'development') {
    // eslint-disable-next-line no-console
    console.info('[Analytics]', eventName, properties);
  }

  // TODO: Production integration with analytics platform
  // Example PostHog integration:
  // if (typeof window !== 'undefined' && window.posthog) {
  //   window.posthog.capture(eventName, properties);
  // }

  // Example Google Analytics 4:
  // if (typeof window !== 'undefined' && window.gtag) {
  //   window.gtag('event', eventName, properties);
  // }
}

/**
 * Track page view event
 *
 * @param pagePath - Page path (e.g., '/dashboard')
 * @param pageTitle - Optional page title
 */
export function trackPageView(pagePath: string, pageTitle?: string): void {
  trackEvent('page_view', {
    page_path: pagePath,
    page_title: pageTitle,
  });
}

/**
 * Track navigation event
 *
 * @param destination - Destination path
 * @param source - Source location
 */
export function trackNavigation(destination: string, source: string): void {
  trackEvent('navigation', {
    destination,
    source,
  });
}
