/**
 * ISSUE #2426: SharedGameCatalog Lighthouse Performance E2E Test
 *
 * Validates Lighthouse scores:
 * - Performance: > 90
 * - Accessibility: > 95
 * - Best Practices: > 90
 * - SEO: > 90
 *
 * Note: Requires @playwright/test with lighthouse integration
 * Or use separate lighthouse-ci tool
 *
 * Run: pnpm test:e2e apps/web/e2e/admin/shared-games-performance.spec.ts
 */

import { test, expect } from '@playwright/test';
import { playAudit } from 'playwright-lighthouse';

test.describe('SharedGameCatalog Performance (Lighthouse)', () => {
  // Note: Lighthouse tests only work with Chromium, skip on other browsers
  test.skip(({ browserName }) => browserName !== 'chromium', 'Lighthouse requires Chromium');

  test('Public search page should achieve Lighthouse scores', async ({ page, browser }) => {
    await page.goto('/games/add');

    // Wait for page to be fully interactive
    await expect(page.locator('input[placeholder*="Cerca"]')).toBeVisible();

    // Run Lighthouse audit
    await playAudit({
      page,
      port: 9222, // Chrome debugging port
      thresholds: {
        performance: 90,
        accessibility: 95,
        'best-practices': 90,
        seo: 90,
      },
      reports: {
        formats: {
          html: true,
        },
        name: 'shared-games-search-lighthouse',
        directory: 'apps/web/e2e/reports',
      },
    });

    // If thresholds not met, playAudit will throw
    // Test passes if no error thrown
  });

  test('Core Web Vitals should meet targets', async ({ page }) => {
    await page.goto('/games/add');

    // Measure Core Web Vitals using Performance API
    const webVitals = await page.evaluate(() => {
      return new Promise(resolve => {
        const metrics: Record<string, number> = {};

        // LCP (Largest Contentful Paint)
        new PerformanceObserver(list => {
          const entries = list.getEntries();
          const lastEntry = entries[entries.length - 1] as PerformanceEntry & {
            renderTime: number;
            loadTime: number;
          };
          metrics.lcp = lastEntry.renderTime || lastEntry.loadTime;
        }).observe({ type: 'largest-contentful-paint', buffered: true });

        // FID (First Input Delay) - simulate via interaction
        // CLS (Cumulative Layout Shift)
        new PerformanceObserver(list => {
          let cls = 0;
          for (const entry of list.getEntries()) {
            if (!(entry as any).hadRecentInput) {
              cls += (entry as any).value;
            }
          }
          metrics.cls = cls;
        }).observe({ type: 'layout-shift', buffered: true });

        // Wait 3 seconds for metrics to settle
        setTimeout(() => resolve(metrics), 3000);
      });
    });

    // Verify Core Web Vitals targets
    // LCP: < 2.5s (Good), < 4s (Needs Improvement), > 4s (Poor)
    expect((webVitals as any).lcp).toBeLessThan(2500);

    // CLS: < 0.1 (Good), < 0.25 (Needs Improvement), > 0.25 (Poor)
    expect((webVitals as any).cls).toBeLessThan(0.1);
  });

  test('Search results should render within 200ms (P95 target)', async ({ page }) => {
    await page.goto('/games/add');

    // Measure search performance
    const searchInput = page.locator('input[placeholder*="Cerca"]');
    await searchInput.fill('strategia');

    const startTime = Date.now();
    await searchInput.press('Enter');

    // Wait for results
    await expect(page.locator('[data-testid="search-result"]').first()).toBeVisible();

    const duration = Date.now() - startTime;

    // Verify search latency < 200ms (P95 target from Issue #2374)
    expect(duration).toBeLessThan(200);
  });
});
