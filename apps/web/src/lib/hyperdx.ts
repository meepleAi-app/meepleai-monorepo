/* eslint-disable no-console */
/**
 * HyperDX Browser SDK Initialization
 *
 * Provides client-side telemetry and session replay for the MeepleAI web application.
 * Configured for SSR safety with Next.js App Router.
 *
 * NOTE: When NEXT_PUBLIC_HYPERDX_API_KEY is not set, Turbopack resolves
 * @hyperdx/browser to a stub module (configured in next.config.js) to avoid
 * module factory errors with Node.js polyfill dependencies.
 *
 * Issue #1566: [P3] Implement HyperDX Browser SDK (Next.js)
 * Epic: #1561 - HyperDX Unified Observability Integration
 *
 * @see https://www.hyperdx.io/docs/install/browser
 */

// ============================================================================
// Configuration
// ============================================================================

const HYPERDX_API_KEY = process.env.NEXT_PUBLIC_HYPERDX_API_KEY || '';
const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Lazy SDK accessor
// ============================================================================

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let _sdk: any = null;

async function getHyperDX() {
  if (_sdk) return _sdk;
  const mod = await import('@hyperdx/browser');
  _sdk = mod.default;
  return _sdk;
}

// ============================================================================
// Initialization
// ============================================================================

/**
 * Initialize HyperDX browser SDK
 *
 * IMPORTANT: This function must only be called on the client side.
 */
export async function initializeHyperDX(): Promise<void> {
  if (typeof window === 'undefined') {
    return;
  }

  if (!HYPERDX_API_KEY) {
    console.log('[HyperDX] Skipped - API key not configured');
    return;
  }

  try {
    const HyperDX = await getHyperDX();

    HyperDX.init({
      apiKey: HYPERDX_API_KEY,
      service: 'meepleai-web',
      tracePropagationTargets: [
        /localhost:8080/,
        /api\.meepleai\.dev/,
        // eslint-disable-next-line security/detect-non-literal-regexp -- Safe: apiBase from env config
        new RegExp(apiBase.replace(/https?:\/\//, '')),
      ],
      consoleCapture: true,
      advancedNetworkCapture: true,
      maskAllInputs: true,
      maskAllText: false,
    });

    console.log('[HyperDX] Browser SDK initialized successfully');
  } catch (error) {
    console.error('[HyperDX] Failed to initialize browser SDK:', error);
  }
}

// ============================================================================
// User Context
// ============================================================================

/**
 * Identify user in HyperDX for session tracking
 *
 * @param userId - Unique user identifier (anonymized ID, not email)
 */
export async function identifyUser(userId: string): Promise<void> {
  if (typeof window === 'undefined' || !HYPERDX_API_KEY) return;

  try {
    const HyperDX = await getHyperDX();
    HyperDX.setGlobalAttributes({ userId });
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
 * @param eventName - Name of the event (e.g., "Form-Completed", "Game-Selected")
 * @param properties - Optional event metadata
 */
export async function trackEvent(
  eventName: string,
  properties?: Record<string, string | number | boolean>
): Promise<void> {
  if (typeof window === 'undefined' || !HYPERDX_API_KEY) return;

  try {
    const HyperDX = await getHyperDX();
    HyperDX.addAction(
      eventName,
      properties as Record<string, string | number | boolean> | undefined
    );
  } catch (error) {
    console.error('[HyperDX] Failed to track event:', eventName, error);
  }
}
