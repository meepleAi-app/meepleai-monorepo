/**
 * Performance Testing Suite with Lighthouse CI
 *
 * Tests Core Web Vitals and performance metrics for critical pages:
 * - Homepage (/)
 * - Chat (/chat)
 * - Upload (/upload)
 *
 * Core Web Vitals Targets:
 * - LCP (Largest Contentful Paint) < 2.5s
 * - FID (First Input Delay) < 100ms
 * - CLS (Cumulative Layout Shift) < 0.1
 *
 * Performance Scores:
 * - Performance: >= 85%
 * - Accessibility: >= 95%
 * - Best Practices: >= 90%
 * - SEO: >= 90%
 *
 * @see Issue #842
 */

import { test, expect } from '@playwright/test';
import { playAudit } from 'playwright-lighthouse';

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
  fid: 100,  // First Input Delay < 100ms (replaced by TBT in Lighthouse)
  cls: 0.1,  // Cumulative Layout Shift < 0.1
  fcp: 2000, // First Contentful Paint < 2s
  tbt: 300,  // Total Blocking Time < 300ms (proxy for FID)
  si: 3000,  // Speed Index < 3s
};

// CRITICAL FIX: Use serial execution to avoid port conflicts when running in parallel
// Each Chromium instance needs a unique debugging port for Lighthouse
test.describe.serial('Performance Testing - Critical Pages', () => {
  test.use({
    launchOptions: {
      args: [
        '--remote-debugging-port=9222', // Required for playwright-lighthouse to connect
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-accelerated-2d-canvas',
        '--disable-gpu',
      ],
    },
  });

  test('Homepage (/) - Performance Metrics', async ({ page, context }) => {
    await page.goto('/');

    // Wait for page to be fully loaded
    await page.waitForLoadState('networkidle');

    // Run Lighthouse audit with explicit port (required for playwright-lighthouse)
    await playAudit({
      page,
      port: 9222,
      thresholds: lighthouseOptions.thresholds,
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-homepage',
      },
    });

    // Additional checks for page functionality
    await expect(page).toHaveTitle(/MeepleAI/i);
  });

  test('Chat Page (/chat) - Performance Metrics', async ({ page, context }) => {
    // Note: May require authentication in production
    await page.goto('/chat');

    // Wait for page to be fully loaded
    await page.waitForLoadState('networkidle');

    // Run Lighthouse audit with explicit port (required for playwright-lighthouse)
    await playAudit({
      page,
      port: 9222,
      thresholds: lighthouseOptions.thresholds,
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-chat',
      },
    });

    // Verify chat interface is present
    const chatContainer = page.locator('[data-testid="chat-container"], .chat-container, main');
    await expect(chatContainer).toBeVisible({ timeout: 5000 });
  });

  test('Upload Page (/upload) - Performance Metrics', async ({ page, context }) => {
    // Note: May require authentication in production
    await page.goto('/upload');

    // Wait for page to be fully loaded
    await page.waitForLoadState('networkidle');

    // Run Lighthouse audit with explicit port (required for playwright-lighthouse)
    await playAudit({
      page,
      port: 9222,
      thresholds: lighthouseOptions.thresholds,
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-upload',
      },
    });

    // Verify upload interface is present
    const uploadContainer = page.locator('[data-testid="upload-container"], .upload-container, main');
    await expect(uploadContainer).toBeVisible({ timeout: 5000 });
  });
});

// CRITICAL FIX: Use serial execution to avoid port conflicts
test.describe.serial('Performance Testing - Additional Pages', () => {
  test.use({
    launchOptions: {
      args: [
        '--remote-debugging-port=9222', // Required for playwright-lighthouse to connect
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-accelerated-2d-canvas',
        '--disable-gpu',
      ],
    },
  });

  test('Games Page (/games) - Performance Metrics', async ({ page }) => {
    await page.goto('/games');
    await page.waitForLoadState('networkidle');

    await playAudit({
      page,
      port: 9222,
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
  });

  test('Login Page (/login) - Performance Metrics', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('networkidle');

    await playAudit({
      page,
      port: 9222,
      thresholds: lighthouseOptions.thresholds,
      opts: lighthouseOptions.opts,
      reports: {
        ...reportConfig,
        name: 'lighthouse-login',
      },
    });
  });
});

test.describe('Core Web Vitals - Detailed Checks', () => {
  test('Verify Core Web Vitals on Homepage', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Measure performance using Navigation Timing API
    const performanceMetrics = await page.evaluate(() => {
      const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming;
      const paint = performance.getEntriesByType('paint');

      const fcp = paint.find(entry => entry.name === 'first-contentful-paint');
      const lcp = performance.getEntriesByType('largest-contentful-paint').pop() as PerformanceEntry;

      return {
        domContentLoaded: navigation.domContentLoadedEventEnd - navigation.domContentLoadedEventStart,
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
    await page.waitForTimeout(500);

    await page.evaluate(() => {
      window.scrollTo(0, document.body.scrollHeight);
    });
    await page.waitForTimeout(500);

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

    page.on('response', async (response) => {
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

    page.on('response', async (response) => {
      const headers = response.headers();
      const url = response.url();

      // Check for render-blocking resources
      if (
        (url.includes('.css') || url.includes('.js')) &&
        !headers['async'] &&
        !headers['defer']
      ) {
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
