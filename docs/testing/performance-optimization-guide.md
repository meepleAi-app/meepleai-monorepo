# Frontend Performance Optimization Guide

**Issue #2927** | **Last Updated**: 2026-02-01

## Overview

This guide covers frontend performance testing with Lighthouse CI, Core Web Vitals budgets, and optimization techniques for the MeepleAI frontend application.

## Core Web Vitals Budgets

### Desktop Targets (Production)

| Metric | Target | Description |
|--------|--------|-------------|
| **FCP** (First Contentful Paint) | < 1.8s | Time until first content is painted |
| **LCP** (Largest Contentful Paint) | < 2.5s | Time until largest content element is visible |
| **TBT** (Total Blocking Time) | < 300ms | Sum of long task blocking time |
| **CLS** (Cumulative Layout Shift) | < 0.1 | Visual stability score |

### Mobile Targets (Production)

| Metric | Target | Description |
|--------|--------|-------------|
| **FCP** | < 2.5s | Slower network, higher threshold |
| **LCP** | < 4.0s | Mobile-adjusted threshold |
| **TBT** | < 500ms | CPU throttling adjustment |
| **CLS** | < 0.1 | Same visual stability target |

### Category Score Targets

| Category | Desktop | Mobile |
|----------|---------|--------|
| Performance | ≥ 90% | ≥ 85% |
| Accessibility | ≥ 90% | ≥ 90% |
| Best Practices | ≥ 90% | ≥ 90% |
| SEO | ≥ 80% | ≥ 80% |

## Tested Pages

Lighthouse CI runs against 7 key pages covering all user roles:

| # | Page | Route | Role |
|---|------|-------|------|
| 1 | Admin Dashboard | `/admin/dashboard` | Admin |
| 2 | User Dashboard | `/dashboard` | User |
| 3 | Personal Library | `/library` | User |
| 4 | Shared Catalog | `/shared-games` | Public |
| 5 | Profile & Settings | `/settings` | User |
| 6 | User Management | `/admin/users` | Admin |
| 7 | Editor Dashboard | `/editor` | Editor |

## Configuration Files

### Desktop Configuration (`lighthouserc.json`)

```json
{
  "ci": {
    "collect": {
      "numberOfRuns": 3,
      "startServerCommand": "pnpm start",
      "url": [
        "http://localhost:3000/admin/dashboard",
        "http://localhost:3000/dashboard",
        "http://localhost:3000/library",
        "http://localhost:3000/shared-games",
        "http://localhost:3000/settings",
        "http://localhost:3000/admin/users",
        "http://localhost:3000/editor"
      ],
      "settings": {
        "preset": "desktop"
      }
    },
    "assert": {
      "assertions": {
        "first-contentful-paint": ["error", { "maxNumericValue": 1800 }],
        "largest-contentful-paint": ["error", { "maxNumericValue": 2500 }],
        "total-blocking-time": ["error", { "maxNumericValue": 300 }],
        "cumulative-layout-shift": ["error", { "maxNumericValue": 0.1 }]
      }
    }
  }
}
```

### Mobile Configuration (`lighthouserc.mobile.json`)

```json
{
  "ci": {
    "settings": {
      "preset": "mobile",
      "throttling": {
        "rttMs": 150,
        "throughputKbps": 1638,
        "cpuSlowdownMultiplier": 4
      }
    }
  }
}
```

## CI Integration

### Workflow Triggers

The performance workflow (`test-performance.yml`) runs:

- **On PR**: When `apps/web/**` files change
- **On Push to Main**: For baseline tracking
- **Nightly Schedule**: 2 AM UTC for regression detection
- **Manual Dispatch**: With selectable test types

### PR Comments

Lighthouse CI posts performance results as PR comments with:

- Per-page metrics table (FCP, LCP, TBT, CLS)
- Category scores (Performance, Accessibility, etc.)
- Pass/fail status against budgets
- Link to full reports in artifacts

### Failure Behavior

| Level | Action |
|-------|--------|
| `error` | Fails the CI check |
| `warn` | Shows warning but passes |

Budget assertions use `error` level for critical metrics.

## Optimization Techniques

### 1. Reduce FCP (First Contentful Paint)

**Goal**: Show content faster

```tsx
// Use Next.js font optimization
import { Inter } from 'next/font/google';

const inter = Inter({
  subsets: ['latin'],
  display: 'swap', // Prevents FOIT
});
```

**Techniques**:
- Inline critical CSS
- Optimize font loading with `display: swap`
- Minimize render-blocking resources
- Use `<link rel="preconnect">` for external domains

### 2. Reduce LCP (Largest Contentful Paint)

**Goal**: Load hero images/content faster

```tsx
// Optimize LCP element
import Image from 'next/image';

<Image
  src="/hero.webp"
  alt="Hero"
  priority // Preloads this image
  sizes="(max-width: 768px) 100vw, 50vw"
/>
```

**Techniques**:
- Use `priority` prop on LCP images
- Serve images in modern formats (WebP, AVIF)
- Set explicit `width` and `height` to prevent layout shifts
- Consider `fetchpriority="high"` for critical resources

### 3. Reduce TBT (Total Blocking Time)

**Goal**: Keep main thread responsive

```tsx
// Use dynamic imports for heavy components
const HeavyChart = dynamic(() => import('./HeavyChart'), {
  loading: () => <Skeleton />,
  ssr: false,
});
```

**Techniques**:
- Break up long tasks with `requestIdleCallback`
- Use dynamic imports for non-critical components
- Defer third-party scripts
- Use Web Workers for CPU-intensive operations

### 4. Minimize CLS (Cumulative Layout Shift)

**Goal**: Prevent visual jank

```tsx
// Reserve space for async content
<div className="min-h-[200px]">
  <Suspense fallback={<Skeleton className="h-[200px]" />}>
    <AsyncComponent />
  </Suspense>
</div>
```

**Techniques**:
- Always set dimensions on images
- Reserve space for dynamic content
- Avoid injecting content above existing content
- Use `transform` for animations instead of layout properties

## Common Performance Issues

### Issue: Large JavaScript Bundles

**Detection**: Check `Total Byte Weight` warning in Lighthouse

**Solution**:
```js
// next.config.js - Enable bundle analyzer
const withBundleAnalyzer = require('@next/bundle-analyzer')({
  enabled: process.env.ANALYZE === 'true',
});
```

```bash
# Analyze bundles
pnpm build:analyze
```

### Issue: Unoptimized Images

**Detection**: Lighthouse "Serve images in next-gen formats"

**Solution**:
```tsx
// Use Next.js Image component
import Image from 'next/image';

// Avoid: <img src="/photo.jpg" />
// Use:
<Image src="/photo.jpg" width={800} height={600} alt="..." />
```

### Issue: Render-Blocking CSS

**Detection**: Lighthouse "Eliminate render-blocking resources"

**Solution**:
```tsx
// Split critical vs non-critical CSS
// Critical: Inline in <head>
// Non-critical: Load async
<link rel="preload" href="/non-critical.css" as="style" onLoad="this.rel='stylesheet'" />
```

### Issue: Unused JavaScript

**Detection**: High TBT with large bundles

**Solution**:
```tsx
// Tree-shake unused imports
// Avoid: import * as _ from 'lodash';
// Use:
import { debounce } from 'lodash-es';

// Or use dynamic imports
const debounce = (await import('lodash-es')).debounce;
```

## Running Lighthouse Locally

### Quick Check

```bash
cd apps/web

# Build and start production server
pnpm build && pnpm start

# In another terminal, run Lighthouse
npx lighthouse http://localhost:3000/dashboard --view
```

### Full Lighthouse CI

```bash
# Run with same config as CI
pnpm exec lhci autorun
```

### Mobile Emulation

```bash
# Use mobile config
pnpm exec lhci autorun --config lighthouserc.mobile.json
```

## Monitoring & Alerts

### Performance Regression Detection

The workflow creates GitHub issues when:
- Nightly runs fail budget assertions
- Performance drops significantly

### Tracking Over Time

Lighthouse results are uploaded to:
- **GitHub Artifacts**: 7-day retention
- **Temporary Public Storage**: Lighthouse dashboard

## Best Practices

### Development Workflow

1. **Pre-commit**: Run local Lighthouse on changed pages
2. **PR**: Automated Lighthouse CI with PR comments
3. **Post-merge**: Baseline tracking on main branch
4. **Nightly**: Regression detection

### Page-Specific Considerations

| Page | Focus Areas |
|------|-------------|
| Admin Dashboard | Chart loading, data tables |
| User Dashboard | Widget loading, personalization |
| Library | Image grid, virtualization |
| Shared Catalog | Search, filtering, pagination |
| Settings | Form interactions |
| User Management | Large data tables |
| Editor | Rich text editor, previews |

### Component Guidelines

```tsx
// Optimize heavy components
export function DataTable({ data }) {
  // Virtualize for large datasets
  if (data.length > 100) {
    return <VirtualizedTable data={data} />;
  }
  return <RegularTable data={data} />;
}

// Lazy load below-fold components
const Footer = dynamic(() => import('./Footer'), { ssr: false });
```

## Related Documentation

- [Backend Performance Benchmarks](./performance-benchmarks.md)
- [Admin Dashboard Performance Guide](./admin-dashboard-performance-guide.md)
- [E2E Testing Guide](./e2e-testing-guide.md)

## References

- [Web Vitals](https://web.dev/vitals/)
- [Lighthouse CI Documentation](https://github.com/GoogleChrome/lighthouse-ci)
- [Next.js Performance](https://nextjs.org/docs/pages/building-your-application/optimizing)
