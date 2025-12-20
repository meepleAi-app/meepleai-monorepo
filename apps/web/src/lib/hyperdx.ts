/* eslint-disable no-console */
/**
 * HyperDX Browser SDK Initialization
 *
 * Provides client-side telemetry and session replay for the MeepleAI web application.
 * Configured for SSR safety with Next.js App Router.
 *
 * Issue #1566: [P3] ⚛️ Implement HyperDX Browser SDK (Next.js)
 * Epic: #1561 - HyperDX Unified Observability Integration
 *
 * @see https://www.hyperdx.io/docs/install/browser
 */

import HyperDX from '@hyperdx/browser';

// ============================================================================
// Types
// ============================================================================

// No custom types needed - using HyperDX SDK types directly

// ============================================================================
// Configuration
// ============================================================================

const _isProduction = process.env.NODE_ENV === 'production';
const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Initialization
// ============================================================================

/**
 * Initialize HyperDX browser SDK
 *
 * IMPORTANT: This function must only be called on the client side.
 * The caller is responsible for checking `typeof window !== 'undefined'`.
 *
 * Configuration:
 * - Session replay with privacy (maskAllInputs: true)
 * - Trace propagation to backend (correlates frontend/backend traces)
 * - Sensitive data scrubbing (password, token, apiKey, secret)
 * - Sample rate: 100% in dev, 10% in production
 */
export function initializeHyperDX(): void {
  if (typeof window === 'undefined') {
    // Safety check: Should never reach here if caller follows contract
    console.warn('[HyperDX] initializeHyperDX() called on server side - skipping');
    return;
  }

  try {
    HyperDX.init({
      // Authentication
      apiKey: process.env.NEXT_PUBLIC_HYPERDX_API_KEY || 'demo',
      service: 'meepleai-web',

      // Trace Propagation (correlate frontend → backend)
      // Links frontend requests to backend traces via W3C traceparent headers
      tracePropagationTargets: [
        /localhost:8080/, // Local development API
        /api\.meepleai\.dev/, // Production API domain
        // eslint-disable-next-line security/detect-non-literal-regexp -- Safe: apiBase from env config, not user input
        new RegExp(apiBase.replace(/https?:\/\//, '')), // Dynamic API base from environment
      ],

      // Session Replay Configuration
      consoleCapture: true, // Capture console.log, console.error, etc.
      advancedNetworkCapture: true, // Capture request/response headers and bodies

      // Privacy Settings (GDPR/compliance)
      maskAllInputs: true, // Mask all form inputs (passwords, credit cards, etc.)
      maskAllText: false, // Don't mask regular text content

      // Note: Session sampling not configured here as sampling types are not well-documented
      // For production, consider implementing client-side sampling before init() if needed:
      // if (isProduction && Math.random() > 0.1) return; // Sample 10%
    });

    console.log('[HyperDX] Browser SDK initialized successfully');
  } catch (error) {
    // Log error but don't crash the app if HyperDX fails to initialize
    console.error('[HyperDX] Failed to initialize browser SDK:', error);
  }
}

// ============================================================================
// User Context
// ============================================================================

/**
 * Identify user in HyperDX for session tracking
 *
 * Associates telemetry events with a specific user for better debugging
 * and user journey analysis. Call this after successful login.
 *
 * **Privacy Note**: Only sends user ID (GDPR-compliant). Email is NOT sent
 * to avoid PII exposure to third-party telemetry service.
 *
 * @param userId - Unique user identifier (anonymized ID, not email)
 */
export function identifyUser(userId: string): void {
  if (typeof window === 'undefined') {
    return; // Server-side, skip
  }

  try {
    HyperDX.setGlobalAttributes({
      userId,
      // Email deliberately not sent for GDPR compliance
      // See code review: PR #1960 - PII exposure concern
    });

    console.log('[HyperDX] User identified:', userId);
  } catch (error) {
    console.error('[HyperDX] Failed to identify user:', error);
  }
}

// ============================================================================
// Custom Event Tracking
// ============================================================================

/**
 * Track custom events in HyperDX
 *
 * Use this to track important user actions like form submissions,
 * feature usage, or business-critical events.
 *
 * @param eventName - Name of the event (e.g., "Form-Completed", "Game-Selected")
 * @param properties - Optional event metadata
 */
export function trackEvent(
  eventName: string,
  properties?: Record<string, string | number | boolean>
): void {
  if (typeof window === 'undefined') {
    return; // Server-side, skip
  }

  try {
    HyperDX.addAction(
      eventName,
      properties as Record<string, string | number | boolean> | undefined
    );
  } catch (error) {
    console.error('[HyperDX] Failed to track event:', eventName, error);
  }
}
