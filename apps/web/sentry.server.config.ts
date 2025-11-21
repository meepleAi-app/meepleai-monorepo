/**
 * Sentry Server Configuration
 *
 * This file configures the Sentry SDK for server-side error tracking in Next.js.
 * It captures server-side errors, API route errors, and SSR errors.
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

  integrations: [
    Sentry.httpIntegration(),
  ],

  // Ignore common server errors that don't need tracking
  ignoreErrors: [
    'ECONNRESET',
    'EPIPE',
    'ENOTFOUND',
    'ETIMEDOUT',
  ],

  beforeSend(event, hint) {
    // Filter out development errors if needed
    if (SENTRY_ENVIRONMENT === 'development' && !SENTRY_DSN) {
      return null;
    }

    // Sanitize sensitive data
    if (event.request) {
      // Remove potential sensitive headers
      if (event.request.headers) {
        delete event.request.headers['authorization'];
        delete event.request.headers['cookie'];
        delete event.request.headers['x-api-key'];
      }

      // Remove potential sensitive query parameters
      if (event.request.query_string && typeof event.request.query_string === 'string') {
        const sanitized = event.request.query_string
          .replace(/([?&])(password|token|key|secret)=[^&]*/gi, '$1$2=***');
        event.request.query_string = sanitized;
      }
    }

    return event;
  },
});
