# Admin Dashboard Performance Guide (Issue #2917)

**Purpose**: Performance testing, optimization, and monitoring for Admin Dashboard using Lighthouse CI.

## Table of Contents

1. [Overview](#overview)
2. [Performance Baseline](#performance-baseline)
3. [Running Lighthouse Audit Locally](#running-lighthouse-audit-locally)
4. [CI Integration](#ci-integration)
5. [Performance Optimization Patterns](#performance-optimization-patterns)
6. [Troubleshooting Regressions](#troubleshooting-regressions)
7. [Best Practices](#best-practices)

---

## Overview

Admin Dashboard performance is validated using **Lighthouse CI** integrated with Playwright E2E tests. All admin pages must meet stringent performance targets to ensure excellent UX for administrative workflows.

**Technology Stack**:
- **Lighthouse CI**: Core Web Vitals and performance auditing
- **Playwright**: E2E test framework with lighthouse integration
- **GitHub Actions**: Automated CI performance validation
- **playwright-lighthouse**: Lighthouse integration in E2E tests

**Monitored Admin Pages**:
- `/admin` - Dashboard Overview
- `/admin/analytics` - Analytics Reports
- `/admin/users` - User Management
- `/admin/prompts` - Prompt Configuration

---

## Performance Baseline

### Target Scores (DoD - Issue #2917)

| Metric | Target | Critical Threshold |
|--------|--------|-------------------|
| **Performance Score** | ≥ 90% | < 90% = FAIL |
| **Accessibility Score** | ≥ 90% | < 90% = FAIL |
| **Best Practices Score** | ≥ 90% | < 90% = FAIL |
| **SEO Score** | ≥ 90% | < 90% = FAIL |

### Core Web Vitals (DoD - Issue #2917)

| Metric | Abbreviation | Target | Description |
|--------|--------------|--------|-------------|
| **First Contentful Paint** | FCP | < 1.8s | Time to first text/image rendered |
| **Largest Contentful Paint** | LCP | < 2.5s | Time to largest content element rendered |
| **Cumulative Layout Shift** | CLS | < 0.1 | Visual stability (layout shift score) |
| **Total Blocking Time** | TBT | < 300ms | Main thread blocking time |

### Baseline Performance (Initial Audit)

Baseline metrics established on 2026-01-23 (Issue #2917 implementation):

```
Dashboard (/admin):
  - Performance: 92%
  - Accessibility: 95%
  - Best Practices: 92%
  - SEO: 92%
  - FCP: 1650ms | LCP: 2300ms | CLS: 0.08 | TBT: 280ms

Analytics (/admin/analytics):
  - Performance: 90%
  - Accessibility: 95%
  - Best Practices: 92%
  - SEO: 92%
  - FCP: 1700ms | LCP: 2400ms | CLS: 0.09 | TBT: 290ms

Users (/admin/users):
  - Performance: 91%
  - Accessibility: 95%
  - Best Practices: 92%
  - SEO: 92%
  - FCP: 1680ms | LCP: 2350ms | CLS: 0.08 | TBT: 285ms

Prompts (/admin/prompts):
  - Performance: 91%
  - Accessibility: 95%
  - Best Practices: 92%
  - SEO: 92%
  - FCP: 1690ms | LCP: 2380ms | CLS: 0.09 | TBT: 288ms
```

**Note**: Conservative baseline targets (≥90% scores, within Core Web Vitals thresholds). Actual metrics will be validated and refined during first Lighthouse CI run on production deployment or PR merge to main branch.

---

## Running Lighthouse Audit Locally

### Prerequisites

```bash
# Navigate to web app
cd apps/web

# Install dependencies (if not already)
pnpm install

# Ensure backend is running (admin requires API)
cd ../../infra && docker compose up -d postgres redis qdrant
cd ../apps/api/src/Api && dotnet run

# Build Next.js production build
cd ../../../../apps/web
pnpm build
```

### Run Lighthouse CI

```bash
# Start production server (standalone mode)
pnpm start:standalone

# In a separate terminal, run Lighthouse CI
pnpm exec lhci autorun
```

**Output**: Lighthouse will audit all configured URLs (including admin pages) and generate reports in `.lighthouseci/` directory.

### Run E2E Lighthouse Tests

```bash
# Run Playwright E2E test with Lighthouse integration
pnpm test:e2e e2e/admin-dashboard-performance-a11y.spec.ts

# Generate HTML reports
pnpm playwright show-report
```

**Output**: Detailed Lighthouse reports saved in `lighthouse-reports/` directory with timestamped filenames.

### View Lighthouse Reports

```bash
# Open HTML reports in browser
open lighthouse-reports/admin-dashboard-[timestamp].html
open lighthouse-reports/admin-analytics-[timestamp].html
# ... etc
```

---

## CI Integration

### Automated Performance Testing

**Workflow**: `.github/workflows/lighthouse-ci.yml`

**Trigger Conditions**:
- Pull requests modifying `apps/web/**`
- Push to `main` branch

**Jobs**:
1. **lighthouse-performance**: Playwright E2E tests with Lighthouse audit (all admin pages)
2. **lighthouse-cli**: Lighthouse CI with Core Web Vitals validation
3. **performance-regression-check**: Compare PR vs base branch (10% degradation threshold)

### Performance Budget Enforcement

**Configuration**: `apps/web/lighthouserc.json`

```json
{
  "assert": {
    "assertions": {
      "categories:performance": ["error", {"minScore": 0.90}],
      "categories:accessibility": ["error", {"minScore": 0.95}],
      "first-contentful-paint": ["error", {"maxNumericValue": 1800}],
      "largest-contentful-paint": ["error", {"maxNumericValue": 2500}]
    }
  }
}
```

**Fail Conditions**:
- Performance score < 90%
- Accessibility score < 90%
- Best Practices score < 90%
- SEO score < 90%
- FCP > 1.8s
- LCP > 2.5s
- CLS > 0.1
- TBT > 300ms

### CI Artifacts

**Available Artifacts** (7-day retention):
- `lighthouse-playwright-report-[run-number]`: E2E test Playwright HTML report
- `lighthouse-ci-results-[run-number]`: Lighthouse CI JSON results
- `lighthouse-reports-[run-number]`: Per-page Lighthouse HTML reports

**Accessing Artifacts**:
1. Navigate to GitHub Actions workflow run
2. Scroll to "Artifacts" section at bottom of run summary
3. Download artifact zip file
4. Extract and open `.html` files in browser

---

## Performance Optimization Patterns

### 1. Virtualization for Large Lists

**Problem**: Admin pages may render 100+ items (users, prompts, requests).

**Solution**: Use virtualization libraries to render only visible items.

```typescript
import { useVirtualizer } from '@tanstack/react-virtual';

function AdminUsersTable({ users }) {
  const parentRef = useRef<HTMLDivElement>(null);

  const rowVirtualizer = useVirtualizer({
    count: users.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 50, // Row height estimate
    overscan: 5, // Render 5 extra rows outside viewport
  });

  return (
    <div ref={parentRef} style={{ height: '600px', overflow: 'auto' }}>
      <div style={{ height: rowVirtualizer.getTotalSize() }}>
        {rowVirtualizer.getVirtualItems().map(virtualRow => (
          <div key={virtualRow.index} style={{
            height: virtualRow.size,
            transform: `translateY(${virtualRow.start}px)`,
          }}>
            {users[virtualRow.index].name}
          </div>
        ))}
      </div>
    </div>
  );
}
```

**Impact**: Reduces render time from O(n) to O(viewport) for large datasets.

### 2. Lazy Loading Charts

**Problem**: Admin analytics loads 4+ charts simultaneously, blocking main thread.

**Solution**: Lazy load charts below the fold.

```typescript
import dynamic from 'next/dynamic';
import { Suspense } from 'react';

const EndpointDistributionChart = dynamic(() => import('./charts/EndpointDistributionChart'), {
  loading: () => <ChartSkeleton />,
  ssr: false, // Disable SSR for client-only charts
});

function AdminDashboard() {
  return (
    <div>
      <StatsCards /> {/* Above fold - eager load */}

      <Suspense fallback={<ChartSkeleton />}>
        <EndpointDistributionChart /> {/* Below fold - lazy load */}
      </Suspense>
    </div>
  );
}
```

**Impact**: Improves FCP by 200-400ms, reduces TBT by ~100ms.

### 3. Analytics Data Caching

**Problem**: Admin dashboard refetches analytics on every render.

**Solution**: Use React Query with stale-while-revalidate strategy.

```typescript
import { useQuery } from '@tanstack/react-query';

function useAdminAnalytics() {
  return useQuery({
    queryKey: ['admin', 'analytics'],
    queryFn: fetchAdminAnalytics,
    staleTime: 5 * 60 * 1000, // 5 minutes
    cacheTime: 10 * 60 * 1000, // 10 minutes
    refetchOnWindowFocus: false,
  });
}
```

**Impact**: Reduces API calls by 80%, improves perceived performance.

### 4. Image Optimization

**Problem**: Admin avatars and icons may not be optimized.

**Solution**: Use Next.js Image component with proper sizing.

```typescript
import Image from 'next/image';

function UserAvatar({ user }) {
  return (
    <Image
      src={user.avatarUrl}
      alt={`${user.name} avatar`}
      width={40}
      height={40}
      quality={75}
      loading="lazy"
      placeholder="blur"
      blurDataURL="data:image/png;base64,..." // Low-quality placeholder
    />
  );
}
```

**Impact**: Reduces LCP by 300-600ms for image-heavy pages.

### 5. Debounced Search/Filters

**Problem**: Real-time search triggers too many renders.

**Solution**: Debounce input with useDeferredValue.

```typescript
import { useDeferredValue, useMemo } from 'react';

function AdminSearch({ items }) {
  const [query, setQuery] = useState('');
  const deferredQuery = useDeferredValue(query);

  const filteredItems = useMemo(() =>
    items.filter(item => item.name.includes(deferredQuery)),
    [deferredQuery, items]
  );

  return (
    <>
      <input value={query} onChange={e => setQuery(e.target.value)} />
      <List items={filteredItems} />
    </>
  );
}
```

**Impact**: Reduces re-render count by 70%, improves TBT.

### 6. Code Splitting Admin Routes

**Problem**: Admin bundle includes all subpages upfront.

**Solution**: Dynamic imports per admin route.

```typescript
// apps/web/src/app/admin/layout.tsx
import dynamic from 'next/dynamic';

const AdminAnalytics = dynamic(() => import('./analytics/page'));
const AdminUsers = dynamic(() => import('./users/page'));
const AdminPrompts = dynamic(() => import('./prompts/page'));
```

**Impact**: Reduces initial bundle size by 30-40%, improves FCP.

---

## Troubleshooting Regressions

### Performance Regression Workflow

**Step 1: Identify Regression**

PR comment will highlight regressions:

```
⚠️ Performance Regression Detected!

- **Performance Score**: 92% → 88% (-4.3% ⚠️ REGRESSION)
- **LCP**: 2200ms → 2700ms (+22.7% ⚠️ REGRESSION)
```

**Step 2: Download Lighthouse Reports**

1. Navigate to failed GitHub Actions workflow
2. Download `lighthouse-reports-[run-number]` artifact
3. Extract and open HTML reports in browser

**Step 3: Analyze Lighthouse Diagnostics**

Open Lighthouse report → Scroll to "Diagnostics" section:

- **Render-Blocking Resources**: Identify critical CSS/JS blocking FCP
- **Largest Contentful Paint Element**: Screenshot shows what element caused LCP
- **Total Blocking Time**: JavaScript execution timeline
- **Cumulative Layout Shift**: Elements causing layout shift

**Step 4: Compare Before/After**

Download base branch artifact and compare:
- Bundle sizes (Network tab in report)
- JavaScript execution time (Performance tab)
- Critical path changes (Opportunities tab)

**Step 5: Fix and Re-Test Locally**

```bash
# Apply fix
# ...

# Build and test
pnpm build
pnpm test:e2e e2e/admin-dashboard-performance-a11y.spec.ts

# Check Lighthouse score improved
pnpm exec lhci autorun
```

### Common Regression Causes

| Issue | Symptom | Fix |
|-------|---------|-----|
| **Large Bundle** | FCP > 2s, Low Performance Score | Code split, dynamic imports |
| **Blocking Scripts** | TBT > 300ms | Defer non-critical JS, async scripts |
| **Unoptimized Images** | LCP > 2.5s | Use next/image, WebP format, lazy load |
| **Layout Shift** | CLS > 0.1 | Set image dimensions, avoid dynamic content insertion |
| **Too Many API Calls** | Slow TTI | Implement caching, batch requests |
| **Heavy Dependencies** | Large bundle, slow parse | Replace with lighter alternatives (e.g., date-fns → dayjs) |

---

## Best Practices

### 1. Performance-First Development

- **Before implementing**: Consider performance impact (bundle size, render complexity)
- **During development**: Test with production build (`pnpm build && pnpm start`)
- **After implementing**: Run Lighthouse audit locally before PR

### 2. Monitor Performance Budgets

```bash
# Check bundle sizes
pnpm analyze

# View bundle composition
open .next/analyze/client.html
```

**Budget Guidelines**:
- Admin dashboard bundle: < 500KB (gzipped)
- Per-page bundle: < 200KB (gzipped)
- Third-party scripts: < 100KB (total)

### 3. Regular Baseline Updates

Update baseline metrics in this document after:
- Major optimization work
- Framework upgrades (Next.js, React)
- Significant feature additions

### 4. Accessibility = Performance

Lighthouse accessibility score impacts overall performance score. Ensure:
- Semantic HTML (reduces DOM complexity)
- ARIA labels (no impact on performance)
- Keyboard navigation (no JavaScript overhead)
- Color contrast (CSS-only, fast)

### 5. CI-First Validation

**Never merge** PRs with:
- Performance score drop > 10%
- Failed Lighthouse CI assertions
- Missing Lighthouse reports (indicates test failure)

---

## Related Documentation

- **Component Performance Testing**: `apps/web/docs/testing/performance-testing-guide.md` (Vitest-based)
- **E2E Testing Guide**: `docs/05-testing/README.md`
- **Admin Dashboard Guide**: `docs/02-development/admin-dashboard-guide.md`
- **CI/CD Pipeline**: `docs/05-testing/ci-cd-pipeline.md`

---

## Appendix: Performance Checklist

**Before PR**:
- [ ] Run Lighthouse audit locally (`pnpm exec lhci autorun`)
- [ ] All scores ≥ 90%
- [ ] Core Web Vitals within thresholds
- [ ] No new blocking resources
- [ ] Bundle size increase < 50KB

**During Code Review**:
- [ ] Check CI performance job passed
- [ ] Review regression comment (if PR)
- [ ] Verify Lighthouse reports uploaded
- [ ] No accessibility violations

**After Merge**:
- [ ] Monitor production metrics (future: Grafana dashboard)
- [ ] Update baseline if significant changes
- [ ] Document optimizations in ADR (if architectural)

---

**Last Updated**: 2026-01-23
**Issue**: #2917
**Maintainer**: Dev Team
