/** @type {import('next').NextConfig} */

// Issue #1817: DOMMatrix polyfill for pdfjs-dist SSR compatibility
// Must be loaded BEFORE any bundler operations
if (typeof window === 'undefined' && typeof global !== 'undefined') {
  global.DOMMatrix = class DOMMatrix {
    constructor(init) {
      this.m11 = 1;
      this.m12 = 0;
      this.m13 = 0;
      this.m14 = 0;
      this.m21 = 0;
      this.m22 = 1;
      this.m23 = 0;
      this.m24 = 0;
      this.m31 = 0;
      this.m32 = 0;
      this.m33 = 1;
      this.m34 = 0;
      this.m41 = 0;
      this.m42 = 0;
      this.m43 = 0;
      this.m44 = 1;
      this.a = 1;
      this.b = 0;
      this.c = 0;
      this.d = 1;
      this.e = 0;
      this.f = 0;
      this.is2D = true;
      this.isIdentity = true;

      if (Array.isArray(init) && init.length === 16) {
        [
          this.m11,
          this.m12,
          this.m13,
          this.m14,
          this.m21,
          this.m22,
          this.m23,
          this.m24,
          this.m31,
          this.m32,
          this.m33,
          this.m34,
          this.m41,
          this.m42,
          this.m43,
          this.m44,
        ] = init;
      }
    }

    translate() {
      return this;
    }
    scale() {
      return this;
    }
    rotate() {
      return this;
    }
    multiply() {
      return this;
    }
    toString() {
      return '[object DOMMatrix]';
    }
  };

  console.log('[Polyfill] DOMMatrix polyfill loaded in next.config.js');
}

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
 * Issue #1817: DOM API Polyfills for pdfjs-dist SSR
 * -------------------------------------------------
 * - instrumentation.ts provides DOMMatrix polyfill for Node.js SSR
 * - Enables pdfjs-dist to work during Next.js static generation
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
  compress: false, // Disable compression to prevent ERR_CONTENT_DECODING_FAILED in proxy

  // Issue #2209: Next.js Image optimization - configure remote image domains
  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: 'picsum.photos',
        pathname: '/**',
      },
      {
        protocol: 'https',
        hostname: 'cf.geekdo-images.com',
        pathname: '/**',
      },
      {
        protocol: 'https',
        hostname: '**.boardgamegeek.com',
        pathname: '/**',
      },
      {
        protocol: 'https',
        hostname: 'placehold.co',
        pathname: '/**',
      },
    ],
  },

  // Issue #1817: instrumentation.ts provides DOM polyfills for pdfjs-dist SSR
  // Note: Instrumentation is enabled by default in Next.js 16, no experimental flag needed
  experimental: {
    // Tree-shake specific packages for smaller bundles (Issue #994)
    optimizePackageImports: ['lucide-react', '@radix-ui/react-icons', 'date-fns', 'lodash'],
  },

  // Permanent redirects
  async redirects() {
    return [
      // Issue #1672: Remove deprecated /profile page
      {
        source: '/profile',
        destination: '/settings',
        permanent: true, // 308 redirect for SEO
      },
      // Issue #3843: Redirect old /giochi route to new /games route
      {
        source: '/giochi/:id*',
        destination: '/games/:id*',
        permanent: true, // 301 redirect for SEO (update index)
      },
    ];
  },

  // Note: API proxy is now handled by catch-all API route at app/api/[...path]/route.ts
  // This preserves Set-Cookie headers from backend (Issue #703)

  // Turbopack configuration for Next.js 16
  turbopack: {
    // Empty config to silence Turbopack warning
    // Note: PDF.js aliases not needed in Turbopack (handled differently)
  },

  // Fix cross-origin warning from 127.0.0.1 to localhost
  allowedDevOrigins: ['127.0.0.1', 'localhost'],

  // Webpack config for backward compatibility (use --webpack flag if needed)
  webpack: config => {
    // PDF.js worker configuration - only used with --webpack flag
    config.resolve.alias.canvas = false;
    config.resolve.alias.encoding = false;
    return config;
  },
};

module.exports = withBundleAnalyzer(nextConfig);
