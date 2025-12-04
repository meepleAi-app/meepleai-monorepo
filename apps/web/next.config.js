/** @type {import('next').NextConfig} */
const withBundleAnalyzer = require('@next/bundle-analyzer')({
  enabled: process.env.ANALYZE === 'true',
});

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
 * Issue #994: Frontend Build Optimization (BGAI-054)
 * --------------------------------------------------
 * Build optimizations applied:
 * - Bundle analyzer integration (ANALYZE=true pnpm build)
 * - optimizePackageImports for tree-shaking heavy libraries
 * - Lazy loading for Monaco Editor (PromptEditor)
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

  // Experimental optimizations for package imports (Issue #994)
  experimental: {
    // Tree-shake specific packages for smaller bundles
    optimizePackageImports: ['lucide-react', '@radix-ui/react-icons', 'date-fns', 'lodash'],
  },

  // Permanent redirects (Issue #1672: Remove deprecated /profile page)
  async redirects() {
    return [
      {
        source: '/profile',
        destination: '/settings',
        permanent: true, // 308 redirect for SEO
      },
    ];
  },

  // Turbopack configuration for Next.js 16
  turbopack: {
    // Empty config to silence Turbopack warning
    // Note: PDF.js aliases not needed in Turbopack (handled differently)
  },

  // Webpack config for backward compatibility (use --webpack flag if needed)
  webpack: config => {
    // PDF.js worker configuration - only used with --webpack flag
    config.resolve.alias.canvas = false;
    config.resolve.alias.encoding = false;
    return config;
  },
};

module.exports = withBundleAnalyzer(nextConfig);
