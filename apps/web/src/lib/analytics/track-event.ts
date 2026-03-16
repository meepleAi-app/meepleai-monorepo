/**
 * Analytics Event Tracking Utility
 * Issue #3913 - Quick Actions Grid Enhancement
 * Issue #3982 - Dashboard Business Metrics Tracking
 *
 * Provides centralized event tracking for user interactions.
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
 * Events are logged to console in development mode.
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

/**
 * Track timing event (e.g., time on page)
 *
 * @param category - Timing category (e.g., 'Dashboard')
 * @param variable - Timing variable (e.g., 'time_on_page')
 * @param valueMs - Duration in milliseconds
 */
export function trackTiming(category: string, variable: string, valueMs: number): void {
  trackEvent('timing', {
    category,
    variable,
    value_ms: valueMs,
    value_seconds: Math.round(valueMs / 1000),
  });
}
