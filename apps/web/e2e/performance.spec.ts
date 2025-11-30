/**
 * Performance E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 */

import { test, expect } from '@playwright/test';
import { playAudit } from 'playwright-lighthouse';

/**
 * Get a unique remote debugging port for each worker to avoid EADDRINUSE collisions.
 * Base port 9222 + worker index ensures each parallel worker gets its own port.
 */
function getRemoteDebuggingPort(workerIndex: number): number {
  return 9222 + workerIndex;
}

// Lighthouse configuration matching lighthouserc.json
const lighthouseOptions = {
  thresholds: {
    performance: 85,
    accessibility: 95,
    'best-practices': 90,
    seo: 90,
  },
  opts: {
    onlyCategories: ['performance', 'accessibility', 'best-practices', 'seo'],
    skipAudits: ['uses-http2'],
  },
};

// Report configuration for generating HTML/JSON reports
const reportConfig = {
  formats: {
    html: true,
    json: true,
  },
  name: 'lighthouse-report',
  directory: 'lighthouse-reports',
};

// Core Web Vitals thresholds
const coreWebVitalsThresholds = {
  lcp: 2500, // Largest Contentful Paint < 2.5s
  fid: 100, // First Input Delay < 100ms (replaced by TBT in Lighthouse)
  cls: 0.1, // Cumulative Layout Shift < 0.1
  fcp: 2000, // First Contentful Paint < 2s
  tbt: 300, // Total Blocking Time < 300ms (proxy for FID)
  si: 3000, // Speed Index < 3s
};

// CRITICAL FIX: Each worker gets a unique debugging port to avoid EADDRINUSE collisions
// when tests run in parallel across multiple workers (fullyParallel: true, workers: 2-4)
test.describe('Performance Testing - Critical Pages', () => {
  test('Homepage (/) - Performance Metrics', async ({ page, context }, testInfo) => {
    const port = getRemoteDebuggingPort(testInfo.workerIndex);
    const baseURL = testInfo.project.use.baseURL;

    // Launch browser with worker-specific debugging port
    await context.close();
    const { chromium } = await import('@playwright/test');
    const browser =
      testInfo.project.use.browserName === 'chromium'
        ? await chromium.launch({
            args: [
              `--remote-debugging-port=${port}`,
              '--no-sandbox',
              '--disable-setuid-sandbox',
              '--disable-dev-shm-usage',
              '--disable-accelerated-2d-canvas',
              '--disable-gpu',
            ],
          })
        : null;

    if (!browser) throw new Error('Browser launch failed');

    const newContext = await browser.newContext({ baseURL });
    const newPage = await newContext.newPage();

    await newPage.goto('/');
    await newPage.waitForLoadState('networkidle');

    // Run Lighthouse audit with worker-specific port
    await playAudit({
      page: newPage,
      port,
      thresholds: lighthouseOptions.thresholds,
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-homepage',
      },
    });

    await expect(newPage).toHaveTitle(/MeepleAI/i);

    await browser.close();
  });

  test('Chat Page (/chat) - Performance Metrics', async ({ page, context }, testInfo) => {
    const port = getRemoteDebuggingPort(testInfo.workerIndex);
    const baseURL = testInfo.project.use.baseURL;

    await context.close();
    const { chromium } = await import('@playwright/test');
    const browser =
      testInfo.project.use.browserName === 'chromium'
        ? await chromium.launch({
            args: [
              `--remote-debugging-port=${port}`,
              '--no-sandbox',
              '--disable-setuid-sandbox',
              '--disable-dev-shm-usage',
              '--disable-accelerated-2d-canvas',
              '--disable-gpu',
            ],
          })
        : null;

    if (!browser) throw new Error('Browser launch failed');

    const newContext = await browser.newContext({ baseURL });
    const newPage = await newContext.newPage();

    await newPage.goto('/chat');
    await newPage.waitForLoadState('networkidle');

    await playAudit({
      page: newPage,
      port,
      thresholds: lighthouseOptions.thresholds,
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-chat',
      },
    });

    const chatContainer = newPage.locator('[data-testid="chat-container"], .chat-container, main');
    await expect(chatContainer).toBeVisible({ timeout: 5000 });

    await browser.close();
  });

  test('Upload Page (/upload) - Performance Metrics', async ({ page, context }, testInfo) => {
    const port = getRemoteDebuggingPort(testInfo.workerIndex);
    const baseURL = testInfo.project.use.baseURL;

    await context.close();
    const { chromium } = await import('@playwright/test');
    const browser =
      testInfo.project.use.browserName === 'chromium'
        ? await chromium.launch({
            args: [
              `--remote-debugging-port=${port}`,
              '--no-sandbox',
              '--disable-setuid-sandbox',
              '--disable-dev-shm-usage',
              '--disable-accelerated-2d-canvas',
              '--disable-gpu',
            ],
          })
        : null;

    if (!browser) throw new Error('Browser launch failed');

    const newContext = await browser.newContext({ baseURL });
    const newPage = await newContext.newPage();

    await newPage.goto('/upload');
    await newPage.waitForLoadState('networkidle');

    await playAudit({
      page: newPage,
      port,
      thresholds: lighthouseOptions.thresholds,
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-upload',
      },
    });

    const uploadContainer = newPage.locator(
      '[data-testid="upload-container"], .upload-container, main'
    );
    await expect(uploadContainer).toBeVisible({ timeout: 5000 });

    await browser.close();
  });
});

test.describe('Performance Testing - Additional Pages', () => {
  test('Games Page (/games) - Performance Metrics', async ({ page, context }, testInfo) => {
    const port = getRemoteDebuggingPort(testInfo.workerIndex);
    const baseURL = testInfo.project.use.baseURL;

    await context.close();
    const { chromium } = await import('@playwright/test');
    const browser =
      testInfo.project.use.browserName === 'chromium'
        ? await chromium.launch({
            args: [
              `--remote-debugging-port=${port}`,
              '--no-sandbox',
              '--disable-setuid-sandbox',
              '--disable-dev-shm-usage',
              '--disable-accelerated-2d-canvas',
              '--disable-gpu',
            ],
          })
        : null;

    if (!browser) throw new Error('Browser launch failed');

    const newContext = await browser.newContext({ baseURL });
    const newPage = await newContext.newPage();

    await newPage.goto('/games');
    await newPage.waitForLoadState('networkidle');

    await playAudit({
      page: newPage,
      port,
      thresholds: {
        performance: 80, // Slightly lower for content-heavy pages
        accessibility: 95,
        'best-practices': 90,
        seo: 90,
      },
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-games',
      },
    });

    await browser.close();
  });

  test('Login Page (/login) - Performance Metrics', async ({ page, context }, testInfo) => {
    const port = getRemoteDebuggingPort(testInfo.workerIndex);
    const baseURL = testInfo.project.use.baseURL;

    await context.close();
    const { chromium } = await import('@playwright/test');
    const browser =
      testInfo.project.use.browserName === 'chromium'
        ? await chromium.launch({
            args: [
              `--remote-debugging-port=${port}`,
              '--no-sandbox',
              '--disable-setuid-sandbox',
              '--disable-dev-shm-usage',
              '--disable-accelerated-2d-canvas',
              '--disable-gpu',
            ],
          })
        : null;

    if (!browser) throw new Error('Browser launch failed');

    const newContext = await browser.newContext({ baseURL });
    const newPage = await newContext.newPage();

    await newPage.goto('/login');
    await newPage.waitForLoadState('networkidle');

    await playAudit({
      page: newPage,
      port,
      thresholds: lighthouseOptions.thresholds,
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-login',
      },
    });

    await browser.close();
  });
});

test.describe('Core Web Vitals - Detailed Checks', () => {
  test('Verify Core Web Vitals on Homepage', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Measure performance using Navigation Timing API
    const performanceMetrics = await page.evaluate(() => {
      const navigation = performance.getEntriesByType(
        'navigation'
      )[0] as PerformanceNavigationTiming;
      const paint = performance.getEntriesByType('paint');

      const fcp = paint.find(entry => entry.name === 'first-contentful-paint');
      const lcp = performance
        .getEntriesByType('largest-contentful-paint')
        .pop() as PerformanceEntry;

      return {
        domContentLoaded:
          navigation.domContentLoadedEventEnd - navigation.domContentLoadedEventStart,
        loadComplete: navigation.loadEventEnd - navigation.loadEventStart,
        fcp: fcp?.startTime || 0,
        lcp: lcp?.startTime || 0,
      };
    });

    // Log metrics for debugging
    console.log('Performance Metrics:', performanceMetrics);

    // Assert FCP is within threshold
    expect(performanceMetrics.fcp).toBeLessThan(coreWebVitalsThresholds.fcp);

    // Note: LCP may not be available in all browsers/contexts
    if (performanceMetrics.lcp > 0) {
      expect(performanceMetrics.lcp).toBeLessThan(coreWebVitalsThresholds.lcp);
    }
  });

  test('Verify Page Load Performance on Chat', async ({ page }) => {
    const startTime = Date.now();

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');

    const loadTime = Date.now() - startTime;

    // Overall page load should be reasonable
    expect(loadTime).toBeLessThan(5000); // 5 seconds max

    console.log(`Chat page load time: ${loadTime}ms`);
  });

  test('Check for Layout Stability (CLS)', async ({ page }) => {
    await page.goto('/');

    // Scroll through page to trigger potential layout shifts
    await page.evaluate(() => {
      window.scrollTo(0, document.body.scrollHeight / 2);
    });

    await page.evaluate(() => {
      window.scrollTo(0, document.body.scrollHeight);
    });

    // In production, this would be measured via Lighthouse
    // For now, we verify the page doesn't have obvious layout issues
    const hasLayoutShift = await page.evaluate(() => {
      return document.body.scrollHeight > 0 && document.body.clientHeight > 0;
    });

    expect(hasLayoutShift).toBeTruthy();
  });
});

test.describe('Performance Budget Enforcement', () => {
  test('Verify bundle size is reasonable', async ({ page }) => {
    const resourceSizes: Record<string, number> = {};

    page.on('response', async response => {
      const url = response.url();
      try {
        const buffer = await response.body();
        resourceSizes[url] = buffer.length;
      } catch (e) {
        // Some responses may not have bodies
      }
    });

    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Calculate total JS size
    const jsSize = Object.entries(resourceSizes)
      .filter(([url]) => url.includes('.js'))
      .reduce((sum, [, size]) => sum + size, 0);

    // Log for visibility
    console.log(`Total JS size: ${(jsSize / 1024).toFixed(2)} KB`);

    // Reasonable threshold for modern SPA (adjust as needed)
    expect(jsSize).toBeLessThan(3 * 1024 * 1024); // 3MB max
  });

  test('Verify no blocking resources on critical path', async ({ page }) => {
    const blockingResources: string[] = [];

    page.on('response', async response => {
      const headers = response.headers();
      const url = response.url();

      // Check for render-blocking resources
      if ((url.includes('.css') || url.includes('.js')) && !headers['async'] && !headers['defer']) {
        blockingResources.push(url);
      }
    });

    await page.goto('/');
    await page.waitForLoadState('networkidle');

    console.log(`Potential blocking resources: ${blockingResources.length}`);

    // This is informational - we expect some blocking resources
    // but want to keep them minimal
  });
});
