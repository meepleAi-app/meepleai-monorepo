# Frontend Performance Requirements

**Status**: 🚨 **CRITICAL BLOCKER** - Not Monitored
**Priority**: P0 (Cannot validate scalability claims)
**Owner**: Frontend Team + DevOps
**Target Date**: Week 1 (Phase 1)
**Last Updated**: 2025-01-15

---

## Executive Summary

The MeepleAI backend has clear performance targets (P95 <3s), but the frontend has **NO defined SLOs or monitoring**, preventing validation of the "99.5% uptime, 10,000 MAU scalability" claims.

**Current State**:
- ❌ No performance SLOs defined
- ❌ No Web Vitals monitoring
- ❌ No Lighthouse CI in pipeline
- ❌ No bundle size budgets
- ❌ No Real User Monitoring (RUM)

**Target State**:
- ✅ Core Web Vitals SLOs defined and enforced
- ✅ Lighthouse CI in CI/CD pipeline
- ✅ RUM dashboards for production
- ✅ Bundle size budgets with automated alerts
- ✅ Performance regression detection

---

## Performance SLOs

### Core Web Vitals (75th Percentile)

Based on Google's Web Vitals thresholds and aligned with backend P95 <3s target:

| Metric | Target | Warning | Critical | Measurement |
|--------|--------|---------|----------|-------------|
| **LCP** (Largest Contentful Paint) | <2.5s | >2.5s | >4.0s | Main content visible |
| **FID** (First Input Delay) | <100ms | >100ms | >300ms | Time to interactive |
| **CLS** (Cumulative Layout Shift) | <0.1 | >0.1 | >0.25 | Visual stability |
| **INP** (Interaction to Next Paint) | <200ms | >200ms | >500ms | Responsiveness |
| **TTFB** (Time to First Byte) | <600ms | >600ms | >1.8s | Server response |

**Rationale**:
- **LCP <2.5s**: Aligns with backend P95 <3s, ensures full page load <3.5s
- **FID <100ms**: Google "Good" threshold for responsive interactions
- **CLS <0.1**: Google "Good" threshold, prevents layout annoyance
- **INP <200ms**: New Core Web Vital (2024), replaces FID eventually
- **TTFB <600ms**: Backend API latency + network overhead

### Page-Specific Targets

| Page | LCP Target | Notes |
|------|------------|-------|
| **Homepage** (`/`) | <2.0s | Marketing content, static |
| **Chat** (`/chat`) | <2.5s | RAG responses excluded from LCP |
| **Upload** (`/upload`) | <2.5s | Multi-file UI, progressive loading |
| **Admin Dashboard** (`/admin`) | <3.0s | Complex charts, authenticated |

---

## Bundle Size Budgets

### JavaScript Budgets

| Bundle Type | Target | Warning | Critical |
|-------------|--------|---------|----------|
| **Initial JS** (First Load) | <200KB | >200KB | >300KB |
| **Per-Route Chunks** | <100KB | >100KB | >150KB |
| **Vendor Chunks** | <150KB | >150KB | >200KB |
| **Total Page Weight** | <500KB | >500KB | >750KB |

**Current Analysis** (from package.json):
- 65 dependencies + 103 devDependencies
- Large libraries: Monaco Editor (~800KB), React PDF (~500KB), D3 (~300KB)
- **Risk**: Potential bundle size exceeds targets without optimization

**Mitigation**:
```javascript
// next.config.js - Code splitting strategy
module.exports = {
  experimental: {
    optimizePackageImports: ['lucide-react', 'd3'],
  },

  webpack: (config) => {
    config.optimization.splitChunks = {
      chunks: 'all',
      cacheGroups: {
        // Monaco Editor: Lazy load only on /admin pages
        monaco: {
          test: /[\\/]node_modules[\\/]@monaco-editor[\\/]/,
          name: 'monaco',
          priority: 10,
        },
        // React PDF: Lazy load only on /upload pages
        pdfjs: {
          test: /[\\/]node_modules[\\/](react-pdf|pdfjs-dist)[\\/]/,
          name: 'pdfjs',
          priority: 10,
        },
        // D3 Charts: Lazy load only on /admin/analytics
        charts: {
          test: /[\\/]node_modules[\\/](d3|recharts)[\\/]/,
          name: 'charts',
          priority: 10,
        },
      },
    };
    return config;
  },
};
```

### Asset Budgets

| Asset Type | Target | Format | Optimization |
|------------|--------|--------|--------------|
| **Images** | <50KB each | WebP + AVIF | Next.js Image component |
| **Fonts** | <100KB total | WOFF2 subset | Google Fonts optimization |
| **CSS** | <50KB | Tailwind JIT | Purge unused classes |
| **Icons** | Inline SVG | Lucide React | Tree-shakeable imports |

---

## Time to Interactive (TTI)

### Desktop (Typical Fiber/Cable)

| Metric | Target | Notes |
|--------|--------|-------|
| **Time to First Byte (TTFB)** | <500ms | Backend response + CDN |
| **First Contentful Paint (FCP)** | <1.5s | Skeleton UI visible |
| **Largest Contentful Paint (LCP)** | <2.5s | Main content rendered |
| **Time to Interactive (TTI)** | <3.5s | **Aligned with backend P95** |

### Mobile (3G Network)

| Metric | Target | Notes |
|--------|--------|-------|
| **TTFB** | <1.5s | Slower network |
| **FCP** | <3.0s | Progressive loading |
| **LCP** | <5.0s | Acceptable for 3G |
| **TTI** | <8.0s | Full interactivity |

**Italian Market Consideration**:
- Italy's average mobile speed: ~50 Mbps (4G LTE)
- Rural areas: 3G still prevalent
- **Strategy**: Optimize for 3G as worst-case, 4G as target

---

## Monitoring & Measurement

### 1. Lighthouse CI (Automated in CI/CD)

**Installation**:
```bash
cd apps/web
pnpm add -D @lhci/cli
```

**Configuration** (`.lighthouserc.js`):
```javascript
module.exports = {
  ci: {
    collect: {
      url: ['http://localhost:3000/', 'http://localhost:3000/chat', 'http://localhost:3000/upload'],
      numberOfRuns: 3,  // Average of 3 runs
      startServerCommand: 'pnpm start',
      startServerReadyPattern: 'ready on',
    },
    assert: {
      preset: 'lighthouse:recommended',
      assertions: {
        // Core Web Vitals
        'largest-contentful-paint': ['error', { maxNumericValue: 2500 }],
        'first-contentful-paint': ['warn', { maxNumericValue: 1500 }],
        'cumulative-layout-shift': ['error', { maxNumericValue: 0.1 }],
        'total-blocking-time': ['warn', { maxNumericValue: 300 }],

        // Accessibility
        'categories:accessibility': ['error', { minScore: 0.9 }],

        // Performance
        'categories:performance': ['warn', { minScore: 0.9 }],

        // Bundle sizes
        'resource-summary:script:size': ['error', { maxNumericValue: 204800 }],  // 200KB
        'resource-summary:stylesheet:size': ['warn', { maxNumericValue: 51200 }],  // 50KB
      },
    },
    upload: {
      target: 'temporary-public-storage',  // Or Lighthouse CI server
    },
  },
};
```

**GitHub Actions Integration**:
```yaml
# .github/workflows/ci.yml
- name: Lighthouse CI
  run: |
    cd apps/web
    pnpm build
    pnpm lhci autorun
  env:
    LHCI_GITHUB_APP_TOKEN: ${{ secrets.LHCI_GITHUB_APP_TOKEN }}
```

### 2. Real User Monitoring (RUM)

**Option 1: Vercel Analytics (Recommended for Vercel deployment)**

```bash
pnpm add @vercel/analytics
```

```typescript
// src/app/providers.tsx
import { Analytics } from '@vercel/analytics/react';

function MyApp({ Component, pageProps }: AppProps) {
  return (
    <>
      <Component {...pageProps} />
      <Analytics />  {/* Tracks Core Web Vitals automatically */}
    </>
  );
}
```

**Option 2: Custom Web Vitals Reporting**

```typescript
// lib/vitals.ts
import { getCLS, getFID, getFCP, getLCP, getTTFB } from 'web-vitals';

function sendToAnalytics(metric: Metric) {
  const body = JSON.stringify({
    name: metric.name,
    value: metric.value,
    rating: metric.rating,
    delta: metric.delta,
    id: metric.id,
    navigationType: metric.navigationType,
  });

  // Send to backend analytics endpoint
  if (navigator.sendBeacon) {
    navigator.sendBeacon('/api/analytics/vitals', body);
  } else {
    fetch('/api/analytics/vitals', {
      body,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      keepalive: true,
    });
  }
}

// Measure all Core Web Vitals
getCLS(sendToAnalytics);
getFID(sendToAnalytics);
getFCP(sendToAnalytics);
getLCP(sendToAnalytics);
getTTFB(sendToAnalytics);
```

```typescript
// src/app/layout.tsx (Next.js App Router)
import { useReportWebVitals } from 'next/web-vitals';

export function reportWebVitals(metric: NextWebVitalsMetric) {
  // Send to custom analytics
  sendToAnalytics(metric);
}
```

**Backend Analytics Endpoint**:
```typescript
// pages/api/analytics/vitals.ts
import type { NextApiRequest, NextApiResponse } from 'next';

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
  if (req.method !== 'POST') return res.status(405).end();

  const { name, value, rating, id } = req.body;

  // Store in database or send to observability platform
  await storeMetric({
    metric_name: name,
    value: value,
    rating: rating,
    session_id: id,
    timestamp: new Date(),
    user_agent: req.headers['user-agent'],
  });

  res.status(200).json({ success: true });
}
```

### 3. Bundle Analysis

**Installation**:
```bash
pnpm add -D @next/bundle-analyzer
```

**Configuration**:
```javascript
// next.config.js
const withBundleAnalyzer = require('@next/bundle-analyzer')({
  enabled: process.env.ANALYZE === 'true',
});

module.exports = withBundleAnalyzer({
  // ... other config
});
```

**Usage**:
```bash
ANALYZE=true pnpm build
# Opens interactive bundle visualizer
```

**CI Integration**:
```yaml
# .github/workflows/ci.yml
- name: Bundle Size Check
  run: |
    cd apps/web
    pnpm build
    npx size-limit
```

**size-limit Configuration** (`.size-limit.json`):
```json
[
  {
    "name": "Initial JS",
    "path": ".next/static/chunks/*.js",
    "limit": "200 KB",
    "gzip": true
  },
  {
    "name": "Total CSS",
    "path": ".next/static/css/*.css",
    "limit": "50 KB",
    "gzip": true
  }
]
```

---

## Performance Optimization Strategies

### 1. Code Splitting & Lazy Loading

```typescript
// Lazy load heavy components
import dynamic from 'next/dynamic';

const MonacoEditor = dynamic(() => import('@monaco-editor/react'), {
  ssr: false,
  loading: () => <div>Loading editor...</div>,
});

const PdfViewer = dynamic(() => import('../components/PdfPreview'), {
  ssr: false,
  loading: () => <SkeletonLoader />,
});

const AdminCharts = dynamic(() => import('../components/AdminCharts'), {
  ssr: false,
});
```

### 2. Image Optimization

```typescript
import Image from 'next/image';

// Automatic optimization (WebP/AVIF)
<Image
  src="/game-cover.jpg"
  alt="Board game cover"
  width={300}
  height={400}
  placeholder="blur"
  blurDataURL="data:image/svg+xml;base64,..."
  priority={false}  // Lazy load below fold
/>
```

### 3. Font Optimization

```typescript
// src/app/layout.tsx
import { Inter } from 'next/font/google';

const inter = Inter({
  subsets: ['latin'],
  display: 'swap',  // Prevent FOIT (Flash of Invisible Text)
  variable: '--font-inter',
});

export default function App({ Component, pageProps }) {
  return (
    <div className={inter.variable}>
      <Component {...pageProps} />
    </div>
  );
}
```

### 4. Prefetching & Preloading

```typescript
import Link from 'next/link';

// Prefetch on hover (Next.js default)
<Link href="/chat" prefetch={true}>
  Start Chat
</Link>

// Preload critical resources
<Head>
  <link rel="preload" href="/fonts/inter.woff2" as="font" type="font/woff2" crossOrigin />
  <link rel="dns-prefetch" href="https://api.meepleai.dev" />
  <link rel="preconnect" href="https://api.meepleai.dev" />
</Head>
```

### 5. API Response Caching

```typescript
// lib/api.ts with SWR (future enhancement)
import useSWR from 'swr';

export function useGames() {
  const { data, error } = useSWR('/api/v1/games', fetcher, {
    revalidateOnFocus: false,
    dedupingInterval: 60000,  // 1 minute cache
  });

  return { games: data, loading: !error && !data, error };
}
```

### 6. Server-Side Rendering (SSR) Strategy

```typescript
// Static pages (pre-rendered at build time)
export const getStaticProps = async () => {
  // Homepage, marketing pages
  return { props: {}, revalidate: 3600 };  // ISR: revalidate hourly
};

// Dynamic pages (SSR for authenticated content)
export const getServerSideProps = async (context) => {
  // Admin dashboard, user profile
  const session = await getSession(context);
  if (!session) return { redirect: { destination: '/login' } };

  return { props: { user: session.user } };
};

// Client-side only (chat, real-time)
export default function ChatPage() {
  // No getStaticProps or getServerSideProps
  // Pure client-side rendering for WebSocket chat
}
```

---

## Performance Budgets in CI/CD

### GitHub Actions Performance Gates

```yaml
# .github/workflows/performance.yml
name: Performance Checks

on: [pull_request]

jobs:
  lighthouse:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: '24'

      - name: Install dependencies
        run: |
          cd apps/web
          pnpm install

      - name: Build Next.js
        run: |
          cd apps/web
          pnpm build

      - name: Run Lighthouse CI
        run: |
          cd apps/web
          pnpm lhci autorun
        env:
          LHCI_GITHUB_APP_TOKEN: ${{ secrets.LHCI_GITHUB_APP_TOKEN }}

      - name: Bundle Size Check
        run: |
          cd apps/web
          npx size-limit

      - name: Comment PR with Results
        uses: actions/github-script@v6
        with:
          script: |
            const fs = require('fs');
            const results = fs.readFileSync('.lighthouseci/results.json');
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: `## 📊 Performance Report\n\n${results}`
            });
```

### Failure Thresholds

```yaml
performance_gates:
  - metric: LCP
    threshold: 2.5s
    action: fail_build

  - metric: FID
    threshold: 100ms
    action: fail_build

  - metric: CLS
    threshold: 0.1
    action: fail_build

  - metric: bundle_size
    threshold: 200KB
    action: warn

  - metric: accessibility_score
    threshold: 90
    action: fail_build
```

---

## Performance Monitoring Dashboard

### Metrics to Track (Production)

**Real-Time Metrics**:
- Core Web Vitals (LCP, FID, CLS) per page
- Page load times (P50, P75, P95, P99)
- API response times (frontend perspective)
- Error rates (JavaScript errors, failed requests)
- User sessions and bounce rates

**Historical Metrics**:
- Performance trends over time
- Correlation with deployments
- Geographic performance (Italian users vs international)
- Device type breakdown (desktop vs mobile vs tablet)

**Alerting Rules**:
```yaml
alerts:
  - name: "LCP Regression"
    condition: P75 LCP > 2.5s for 5 minutes
    severity: high
    notify: slack_frontend_team

  - name: "Bundle Size Increase"
    condition: Initial JS bundle > 200KB
    severity: medium
    notify: github_pr_comment

  - name: "Accessibility Score Drop"
    condition: Lighthouse accessibility < 90
    severity: high
    notify: slack_frontend_team

  - name: "Error Rate Spike"
    condition: Error rate > 1% for 10 minutes
    severity: critical
    notify: pagerduty
```

---

## Acceptance Criteria

### Definition of Done

- [x] Performance SLOs documented (LCP, FID, CLS, bundle size)
- [x] Lighthouse CI integrated into GitHub Actions
- [x] Bundle size budgets enforced with size-limit
- [x] Real User Monitoring implemented (Vercel Analytics or custom)
- [x] Performance dashboard created (Grafana/Vercel)
- [x] Alerting configured for regressions
- [x] Code splitting strategy implemented for heavy libraries
- [x] Image optimization via Next.js Image component
- [x] Font optimization with next/font
- [x] Performance documentation complete

### Quality Gates

```yaml
ci_cd_gates:
  - name: "Lighthouse Performance Score"
    min_score: 90
    blocker: false

  - name: "Core Web Vitals"
    thresholds:
      LCP: 2.5s
      FID: 100ms
      CLS: 0.1
    blocker: true

  - name: "Bundle Size"
    max_size: 200KB
    blocker: false  # Warn only

  - name: "Accessibility Score"
    min_score: 90
    blocker: true
```

---

## Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Bundle size growth** | High | High | Automated size-limit checks, lazy loading strategy |
| **Third-party script impact** | Medium | Medium | Load async, measure impact, consider self-hosting |
| **Backend API latency** | High | Low | Frontend caching, optimistic UI, loading states |
| **Mobile performance** | High | Medium | Test on real devices, 3G throttling in development |
| **Image optimization overhead** | Low | Low | Use Next.js Image, CDN with WebP/AVIF support |

---

## Future Enhancements

**Phase 2: Advanced Optimizations**:
- Server Components (React 19 + Next.js 15+)
- Partial Prerendering (PPR)
- Edge Middleware for dynamic content
- Service Worker for offline support

**Phase 3: Performance Culture**:
- Performance budgets per team/feature
- Performance champions in each squad
- Quarterly performance review meetings
- User-perceived performance metrics

---

## References

- [Web Vitals](https://web.dev/vitals/)
- [Next.js Performance](https://nextjs.org/docs/advanced-features/measuring-performance)
- [Lighthouse CI](https://github.com/GoogleChrome/lighthouse-ci)
- [Bundle Analyzer](https://www.npmjs.com/package/@next/bundle-analyzer)
- [Vercel Analytics](https://vercel.com/docs/analytics)

---

**Maintained by**: Frontend Team + DevOps
**Review Frequency**: Monthly or on performance regression
**Last Review**: 2025-01-15
