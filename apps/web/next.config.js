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
      // ── Issue #5039: Consolidate User Routes ──────────────────────────────
      // Library sub-routes → query-param tabs (more specific first)
      {
        source: '/library/games/:id/agent',
        destination: '/library/:id?tab=agent',
        permanent: true,
      },
      {
        source: '/library/games/:id/toolkit',
        destination: '/library/:id?tab=toolkit',
        permanent: true,
      },
      { source: '/library/games/:id/faqs', destination: '/library/:id?tab=faq', permanent: true },
      {
        source: '/library/games/:id/reviews',
        destination: '/library/:id?tab=reviews',
        permanent: true,
      },
      {
        source: '/library/games/:id/rules',
        destination: '/library/:id?tab=rules',
        permanent: true,
      },
      {
        source: '/library/games/:id/sessions',
        destination: '/library/:id?tab=sessions',
        permanent: true,
      },
      {
        source: '/library/games/:id/strategies',
        destination: '/library/:id?tab=strategies',
        permanent: true,
      },
      { source: '/library/games/:id', destination: '/library/:id', permanent: true },
      { source: '/library/wishlist', destination: '/library?tab=wishlist', permanent: true },
      { source: '/library/private', destination: '/library?tab=private', permanent: true },
      { source: '/library/proposals', destination: '/discover?tab=proposals', permanent: true },
      { source: '/library/propose', destination: '/discover/propose', permanent: true },

      // Profile / Settings consolidation
      // NOTE: /profile → /settings (Issue #1672) is REMOVED — /profile is now
      //       the canonical profile page; /settings redirects here instead.
      {
        source: '/settings/notifications',
        destination: '/profile?tab=settings&section=notifications',
        permanent: true,
      },
      {
        source: '/settings/security',
        destination: '/profile?tab=settings&section=security',
        permanent: true,
      },
      { source: '/settings', destination: '/profile?tab=settings', permanent: true },
      {
        source: '/profile/achievements',
        destination: '/profile?tab=achievements',
        permanent: true,
      },
      { source: '/badges', destination: '/profile?tab=badges', permanent: true },

      // Agents
      { source: '/agent/slots', destination: '/agents?tab=slots', permanent: true },

      // Sessions & Play Records
      { source: '/sessions/history', destination: '/sessions?tab=history', permanent: true },
      { source: '/play-records/stats', destination: '/play-records?tab=stats', permanent: true },

      // Discover / Community catalog
      // /games/[id] sub-pages → /discover/[id] with tabs
      { source: '/games/:id/faqs', destination: '/discover/:id?tab=faq', permanent: true },
      { source: '/games/:id/reviews', destination: '/discover/:id?tab=reviews', permanent: true },
      { source: '/games/:id/rules', destination: '/discover/:id?tab=rules', permanent: true },
      { source: '/games/:id/sessions', destination: '/discover/:id?tab=sessions', permanent: true },
      {
        source: '/games/:id/strategies',
        destination: '/discover/:id?tab=strategies',
        permanent: true,
      },
      { source: '/games/:id', destination: '/discover/:id', permanent: true },

      // ── Legacy redirects (pre-Issue #5039) ───────────────────────────────
      // Issue #3843: Redirect old /giochi route to new /games route
      {
        source: '/giochi/:id*',
        destination: '/games/:id*',
        permanent: true,
      },
      // Legacy board-game-ai/ask redirect
      {
        source: '/board-game-ai/ask/:path*',
        destination: '/chat/new',
        permanent: true,
      },

      // ── Issue #5055: Game detail sub-routes (KB, agents, chats) ────────────
      {
        source: '/games/:id/knowledge-base',
        destination: '/library/:id?tab=agent',
        permanent: true,
      },
      { source: '/games/:id/agents', destination: '/library/:id?tab=agent', permanent: true },
      { source: '/games/:id/chats', destination: '/chat', permanent: true },
      { source: '/games/catalog', destination: '/discover', permanent: true },
      { source: '/games/add', destination: '/discover/add', permanent: true },

      // Catch-all for any other /settings sub-paths
      { source: '/settings/:path*', destination: '/profile?tab=settings', permanent: true },

      // ── Issue #5040: Admin Route Consolidation ─────────────────────────────

      // Admin AI hub (/admin/ai)
      {
        source: '/admin/agents/catalog',
        destination: '/admin/ai',
        permanent: true,
      },
      {
        source: '/admin/agents/metrics',
        destination: '/admin/ai?tab=agents&section=metrics',
        permanent: true,
      },
      {
        source: '/admin/agents/test',
        destination: '/admin/ai?tab=agents&section=test',
        permanent: true,
      },
      {
        source: '/admin/agents/test-history',
        destination: '/admin/ai?tab=agents&section=test-history',
        permanent: true,
      },
      {
        source: '/admin/agent-typologies',
        destination: '/admin/ai?tab=typologies',
        permanent: true,
      },
      {
        source: '/admin/agent-typologies/create',
        destination: '/admin/ai?tab=typologies&action=create',
        permanent: true,
      },
      {
        source: '/admin/agent-typologies/pending',
        destination: '/admin/ai?tab=typologies&section=pending',
        permanent: true,
      },
      {
        source: '/admin/agent-definitions',
        destination: '/admin/ai?tab=definitions',
        permanent: true,
      },
      {
        source: '/admin/agent-definitions/create',
        destination: '/admin/ai?tab=definitions&action=create',
        permanent: true,
      },
      {
        source: '/admin/agent-definitions/playground',
        destination: '/admin/ai?tab=definitions&section=playground',
        permanent: true,
      },
      {
        source: '/admin/ai-lab',
        destination: '/admin/ai?tab=lab',
        permanent: true,
      },
      {
        source: '/admin/ai-lab/multi-agent',
        destination: '/admin/ai?tab=lab&section=multi-agent',
        permanent: true,
      },
      {
        source: '/admin/prompts',
        destination: '/admin/ai?tab=prompts',
        permanent: true,
      },
      {
        source: '/admin/ai-models',
        destination: '/admin/ai?tab=models',
        permanent: true,
      },
      {
        source: '/admin/ai-requests',
        destination: '/admin/ai?tab=requests',
        permanent: true,
      },
      {
        source: '/admin/rag',
        destination: '/admin/ai?tab=rag',
        permanent: true,
      },
      {
        source: '/admin/rag-executions',
        destination: '/admin/ai?tab=rag&section=executions',
        permanent: true,
      },

      // Admin Content hub (/admin/content)
      {
        source: '/admin/games',
        destination: '/admin/content',
        permanent: true,
      },
      {
        source: '/admin/games/import',
        destination: '/admin/content?tab=games&section=import',
        permanent: true,
      },
      {
        source: '/admin/games/import/:path*',
        destination: '/admin/content?tab=games&section=import',
        permanent: true,
      },
      {
        source: '/admin/shared-games',
        destination: '/admin/content?tab=shared',
        permanent: true,
      },
      {
        source: '/admin/shared-games/approval-queue',
        destination: '/admin/content?tab=shared&section=approval',
        permanent: true,
      },
      {
        source: '/admin/shared-games/pending-approvals',
        destination: '/admin/content?tab=shared&section=pending',
        permanent: true,
      },
      {
        source: '/admin/shared-games/pending-deletes',
        destination: '/admin/content?tab=shared&section=pending-deletes',
        permanent: true,
      },
      {
        source: '/admin/faqs',
        destination: '/admin/content?tab=faqs',
        permanent: true,
      },
      {
        source: '/admin/pdfs',
        destination: '/admin/content?tab=kb',
        permanent: true,
      },
      {
        source: '/admin/game-sessions',
        destination: '/admin/content?tab=sessions',
        permanent: true,
      },
      {
        source: '/admin/share-requests',
        destination: '/admin/content?tab=shared&section=requests',
        permanent: true,
      },

      // Admin Analytics hub (/admin/analytics)
      {
        source: '/admin/usage-stats',
        destination: '/admin/analytics?tab=ai-usage',
        permanent: true,
      },
      {
        source: '/admin/ai-usage',
        destination: '/admin/analytics?tab=ai-usage',
        permanent: true,
      },
      {
        source: '/admin/audit-log',
        destination: '/admin/analytics?tab=audit',
        permanent: true,
      },
      {
        source: '/admin/reports',
        destination: '/admin/analytics?tab=reports',
        permanent: true,
      },
      {
        source: '/admin/api-keys',
        destination: '/admin/analytics?tab=api-keys',
        permanent: true,
      },

      // Admin Config hub (/admin/config)
      {
        source: '/admin/configuration',
        destination: '/admin/config',
        permanent: true,
      },
      {
        source: '/admin/configuration/:path*',
        destination: '/admin/config?tab=limits&section=:path',
        permanent: true,
      },
      {
        source: '/admin/feature-flags',
        destination: '/admin/config?tab=flags',
        permanent: true,
      },
      {
        source: '/admin/config/rate-limits',
        destination: '/admin/config?tab=rate-limits',
        permanent: true,
      },
      {
        source: '/admin/tier-limits',
        destination: '/admin/config?tab=limits&section=tiers',
        permanent: true,
      },
      {
        source: '/admin/n8n-templates',
        destination: '/admin/config?tab=n8n',
        permanent: true,
      },
      {
        source: '/admin/wizard',
        destination: '/admin/config?tab=wizard',
        permanent: true,
      },

      // Admin Monitor hub (/admin/monitor)
      {
        source: '/admin/alerts',
        destination: '/admin/monitor',
        permanent: true,
      },
      {
        source: '/admin/alerts/config',
        destination: '/admin/monitor?tab=alerts&section=config',
        permanent: true,
      },
      {
        source: '/admin/alert-rules',
        destination: '/admin/monitor?tab=alerts&section=rules',
        permanent: true,
      },
      {
        source: '/admin/cache',
        destination: '/admin/monitor?tab=cache',
        permanent: true,
      },
      {
        source: '/admin/infrastructure',
        destination: '/admin/monitor?tab=infra',
        permanent: true,
      },
      {
        source: '/admin/services',
        destination: '/admin/monitor?tab=services',
        permanent: true,
      },
      {
        source: '/admin/command-center',
        destination: '/admin/monitor?tab=command',
        permanent: true,
      },
      {
        source: '/admin/testing',
        destination: '/admin/monitor?tab=testing',
        permanent: true,
      },
      {
        source: '/admin/bulk-export',
        destination: '/admin/monitor?tab=export',
        permanent: true,
      },

      // Admin Users hub (/admin/users)
    ];
  },

  // Note: API proxy is now handled by catch-all API route at app/api/[...path]/route.ts
  // This preserves Set-Cookie headers from backend (Issue #703)

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
