/** @type {import('next').NextConfig} */

/**
 * Next.js Configuration
 *
 * Issue #1077: App Router Migration (COMPLETE ✅)
 * -----------------------------------------------
 * This project has been fully migrated to App Router.
 *
 * Migration Complete:
 * - All 31 pages migrated to App Router (app/ directory)
 * - Pages Router removed (except pages/api/ for API routes)
 * - Legacy files removed: _app.tsx, _document.tsx, all page components
 *
 * Benefits Realized:
 * - ~10% reduced JavaScript bundle size via Server Components
 * - Improved performance and SEO
 * - Modern React features (Server Components, Suspense, Streaming)
 * - Better code organization with colocation
 *
 * Directory Structure:
 * - app/: All application pages and layouts (App Router)
 * - pages/api/: API routes only (standard Next.js practice)
 *
 * @see https://nextjs.org/docs/app/building-your-application/routing
 */
// Conditionally require Sentry only if DSN is set
let withSentryConfig;
try {
  withSentryConfig = require('@sentry/nextjs').withSentryConfig;
} catch (e) {
  // Sentry not installed, disable it
  withSentryConfig = null;
}

const nextConfig = {
  reactStrictMode: true,
  output: 'standalone', // Enable Docker-optimized output

  // Turbopack configuration for Next.js 16
  turbopack: {
    // Empty config to silence Turbopack warning
    // Note: PDF.js aliases not needed in Turbopack (handled differently)
  },

  // Webpack config for backward compatibility (use --webpack flag if needed)
  webpack: (config) => {
    // PDF.js worker configuration - only used with --webpack flag
    config.resolve.alias.canvas = false;
    config.resolve.alias.encoding = false;
    return config;
  },
};

// Sentry configuration options
const sentryOptions = {
  // For all available options, see:
  // https://github.com/getsentry/sentry-webpack-plugin#options

  // Suppresses source map uploading logs during build
  silent: true,

  org: process.env.SENTRY_ORG,
  project: process.env.SENTRY_PROJECT,

  // Only upload source maps in production
  widenClientFileUpload: true,
  hideSourceMaps: true,
  disableLogger: true,
};

// Export with Sentry configuration (only if Sentry is available and configured)
module.exports = (process.env.NEXT_PUBLIC_SENTRY_DSN && withSentryConfig)
  ? withSentryConfig(nextConfig, sentryOptions)
  : nextConfig;
