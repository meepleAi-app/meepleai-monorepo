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

module.exports = nextConfig;
