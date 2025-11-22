/**
 * Sentry Client Configuration
 *
 * This file configures the Sentry SDK for browser/client-side error tracking.
 * It automatically captures unhandled errors and provides structured logging.
 */

import * as Sentry from '@sentry/nextjs';

const SENTRY_DSN = process.env.NEXT_PUBLIC_SENTRY_DSN;
const SENTRY_ENVIRONMENT = process.env.NEXT_PUBLIC_SENTRY_ENVIRONMENT || process.env.NODE_ENV || 'development';

Sentry.init({
  dsn: SENTRY_DSN,
  environment: SENTRY_ENVIRONMENT,

  // Adjust this value in production, or use tracesSampler for greater control
  tracesSampleRate: SENTRY_ENVIRONMENT === 'production' ? 0.1 : 1.0,

  // Setting this option to true will print useful information to the console while you're setting up Sentry.
  debug: SENTRY_ENVIRONMENT === 'development',

  // Replay configuration
  replaysOnErrorSampleRate: SENTRY_ENVIRONMENT === 'production' ? 0.5 : 1.0,
  replaysSessionSampleRate: SENTRY_ENVIRONMENT === 'production' ? 0.1 : 0.0,

  integrations: [
    Sentry.replayIntegration({
      maskAllText: true,
      blockAllMedia: true,
    }),
    Sentry.browserTracingIntegration(),
  ],

  // You can remove this option if you're not planning to use the Sentry Session Replay feature:
  ignoreErrors: [
    // Browser extension errors
    'top.GLOBALS',
    'chrome-extension://',
    'moz-extension://',
    // Network errors that are expected
    'NetworkError',
    'Failed to fetch',
    'Load failed',
    // User cancellation
    'AbortError',
    'The user aborted a request',
  ],

  beforeSend(event, hint) {
    // Filter out development errors if needed
    if (SENTRY_ENVIRONMENT === 'development' && !SENTRY_DSN) {
      return null;
    }

    // Add custom context
    if (typeof window !== 'undefined') {
      event.contexts = {
        ...event.contexts,
        custom: {
          userAgent: navigator.userAgent,
          url: window.location.href,
        },
      };
    }

    return event;
  },
});
